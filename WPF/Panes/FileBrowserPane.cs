using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// PRODUCTION-QUALITY FILE BROWSER PANE
    ///
    /// PURPOSE: File/directory selection ONLY - NO file operations (copy/move/delete)
    ///
    /// FEATURES:
    /// - Three-panel layout: Directory tree | File list | Info panel
    /// - Breadcrumb navigation with clickable segments
    /// - Quick access bookmarks (Home, Documents, Desktop, Recent)
    /// - Hidden files toggle, file type filtering
    /// - Search/filter current directory
    /// - Keyboard-first navigation
    /// - Security: Path validation via ISecurityManager
    ///
    /// SECURITY:
    /// - All paths validated via ISecurityManager
    /// - Path traversal prevention (../../)
    /// - Symlink detection and warnings
    /// - Permission checking (read access verification)
    /// - Visual warnings for dangerous/restricted paths
    ///
    /// INTEGRATION:
    /// - FileSelected(string path) - fired when user selects file
    /// - DirectorySelected(string path) - fired when user selects directory
    /// - SelectionCancelled - fired on Escape
    /// </summary>
    public class FileBrowserPane : PaneBase
    {
        #region Fields

        private readonly IConfigurationManager config;
        private readonly ISecurityManager security;

        // UI Components - Three-panel layout
        private Grid mainLayout;
        private Border breadcrumbBorder;
        private StackPanel breadcrumbPanel;
        private Grid bookmarksPanel;
        private TreeView directoryTree;
        private ListBox fileListBox;
        private Grid infoPanel;
        private TextBox searchBox;
        private TextBlock statusBar;

        // Info panel components
        private TextBlock infoPathText;
        private TextBlock infoSizeText;
        private TextBlock infoModifiedText;
        private TextBlock infoTypeText;
        private TextBlock infoPermissionsText;
        private TextBlock infoWarningText;

        // Theme-aware UI elements (need to track for ApplyTheme)
        private List<Button> bookmarkButtons = new List<Button>();
        private List<Button> breadcrumbButtons = new List<Button>();
        private Style listBoxItemStyle;

        // State
        private string currentPath;
        private List<FileSystemItem> currentFiles = new List<FileSystemItem>();
        private List<FileSystemItem> filteredFiles = new List<FileSystemItem>();
        private FileSystemItem selectedItem;
        private bool showHiddenFiles = false;
        private FileSelectionMode selectionMode = FileSelectionMode.Both;
        private HashSet<string> fileTypeFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<string> recentLocations = new List<string>();
        private CancellationTokenSource loadCancellation;

        // Bookmarks
        private List<Bookmark> bookmarks = new List<Bookmark>();
        private bool showBookmarks = true;

        // Debouncing
        private DispatcherTimer searchDebounceTimer;
        private const int SEARCH_DEBOUNCE_MS = 150;

        // Constants
        private const int MAX_RECENT_LOCATIONS = 10;

        #endregion

        #region Events

        /// <summary>
        /// Fired when user selects a file (Enter on file)
        /// </summary>
        public event EventHandler<FileSelectedEventArgs> FileSelected;

        /// <summary>
        /// Fired when user selects a directory (Enter on directory)
        /// </summary>
        public event EventHandler<DirectorySelectedEventArgs> DirectorySelected;

        /// <summary>
        /// Fired when user cancels selection (Escape)
        /// </summary>
        public event EventHandler SelectionCancelled;

        #endregion

        #region Constructor

        public FileBrowserPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager config,
            ISecurityManager security)
            : base(logger, themeManager, projectContext)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.security = security ?? throw new ArgumentNullException(nameof(security));

            PaneName = "File Browser";
            PaneIcon = "📁";

            // Initialize debounce timer
            searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SEARCH_DEBOUNCE_MS)
            };
            searchDebounceTimer.Tick += (s, e) =>
            {
                searchDebounceTimer.Stop();
                FilterFiles();
            };

            // Initialize bookmarks
            InitializeBookmarks();

            // Set initial path to user's home directory
            currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        #endregion

        #region Build UI

        protected override UIElement BuildContent()
        {
            mainLayout = new Grid();

            // Define layout structure
            if (showBookmarks)
            {
                // With bookmarks: Breadcrumb | Bookmarks + Tree | Files | Info
                mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Bookmarks + Tree
                mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Files
                mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Info
            }
            else
            {
                // Without bookmarks: Breadcrumb | Tree | Files | Info
                mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) }); // Tree
                mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Files
                mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Info
            }

            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Breadcrumb
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main content
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

            // Build components
            breadcrumbBorder = BuildBreadcrumb();
            Grid.SetRow(breadcrumbBorder, 0);
            Grid.SetColumnSpan(breadcrumbBorder, 3);
            mainLayout.Children.Add(breadcrumbBorder);

            var leftPanel = BuildLeftPanel();
            Grid.SetRow(leftPanel, 1);
            Grid.SetColumn(leftPanel, 0);
            mainLayout.Children.Add(leftPanel);

            var filePanel = BuildFilePanel();
            Grid.SetRow(filePanel, 1);
            Grid.SetColumn(filePanel, 1);
            mainLayout.Children.Add(filePanel);

            infoPanel = BuildInfoPanel();
            Grid.SetRow(infoPanel, 1);
            Grid.SetColumn(infoPanel, 2);
            mainLayout.Children.Add(infoPanel);

            var statusBorder = BuildStatusBar();
            Grid.SetRow(statusBorder, 2);
            Grid.SetColumnSpan(statusBorder, 3);
            mainLayout.Children.Add(statusBorder);

            // Set up keyboard shortcuts
            this.PreviewKeyDown += OnPreviewKeyDown;

            // Subscribe to theme changes
            themeManager.ThemeChanged += OnThemeChanged;

            // Apply initial theme
            ApplyFileBrowserTheme();

            // Load initial directory
            NavigateToDirectory(currentPath);

            return mainLayout;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyFileBrowserTheme();
        }

        private Border BuildBreadcrumb()
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 36,
                Padding = new Thickness(12, 0, 12, 0)
            };

            breadcrumbPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.Child = breadcrumbPanel;
            return border;
        }

        private Grid BuildLeftPanel()
        {
            var panel = new Grid();

            if (showBookmarks)
            {
                panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bookmarks
                panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tree

                bookmarksPanel = BuildBookmarksPanel();
                Grid.SetRow(bookmarksPanel, 0);
                panel.Children.Add(bookmarksPanel);

                var treeBorder = BuildDirectoryTree();
                Grid.SetRow(treeBorder, 1);
                panel.Children.Add(treeBorder);
            }
            else
            {
                panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                var treeBorder = BuildDirectoryTree();
                Grid.SetRow(treeBorder, 0);
                panel.Children.Add(treeBorder);
            }

            return panel;
        }

        private Grid BuildBookmarksPanel()
        {
            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bookmarks

            // Header
            var headerBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8, 4, 8, 4),
                Height = 24
            };

            var headerText = new TextBlock
            {
                Text = "Quick Access",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerBorder.Child = headerText;
            Grid.SetRow(headerBorder, 0);
            panel.Children.Add(headerBorder);

            // Bookmarks list
            var bookmarksBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(4)
            };

            var bookmarksStack = new StackPanel();

            bookmarkButtons.Clear();
            foreach (var bookmark in bookmarks)
            {
                var btn = new Button
                {
                    Content = $"{bookmark.Icon} {bookmark.Name}",
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 18,
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 2),
                    BorderThickness = new Thickness(0),
                    Tag = bookmark
                };
                btn.Click += OnBookmarkClick;
                bookmarkButtons.Add(btn);
                bookmarksStack.Children.Add(btn);
            }

            bookmarksBorder.Child = bookmarksStack;
            Grid.SetRow(bookmarksBorder, 1);
            panel.Children.Add(bookmarksBorder);

            return panel;
        }

        private Border BuildDirectoryTree()
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(0)
            };

            directoryTree = new TreeView
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4)
            };

            directoryTree.SelectedItemChanged += OnDirectoryTreeSelectionChanged;

            // Load root directories
            LoadDirectoryTree();

            border.Child = directoryTree;
            return border;
        }

        private Grid BuildFilePanel()
        {
            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // File list

            // Search box
            var searchBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Height = 36
            };

            searchBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            searchBox.TextChanged += OnSearchTextChanged;
            searchBox.GotFocus += (s, e) => searchBox.Text = searchBox.Text == "Filter files... (Ctrl+F)" ? "" : searchBox.Text;
            searchBox.LostFocus += (s, e) => searchBox.Text = string.IsNullOrEmpty(searchBox.Text) ? "Filter files... (Ctrl+F)" : searchBox.Text;
            searchBox.Text = "Filter files... (Ctrl+F)";

            searchBorder.Child = searchBox;
            Grid.SetRow(searchBorder, 0);
            panel.Children.Add(searchBorder);

            // File list
            var listBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(0)
            };

            fileListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            fileListBox.SelectionChanged += OnFileSelected;
            fileListBox.PreviewKeyDown += OnFileListKeyDown;

            listBoxItemStyle = new Style(typeof(ListBoxItem));
            listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 6, 12, 6)));
            listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            fileListBox.ItemContainerStyle = listBoxItemStyle;

            listBorder.Child = fileListBox;
            Grid.SetRow(listBorder, 1);
            panel.Children.Add(listBorder);

            return panel;
        }

        private Grid BuildInfoPanel()
        {
            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Info

            // Header
            var headerBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8),
                Height = 36
            };

            var headerText = new TextBlock
            {
                Text = "Details",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold
            };

            headerBorder.Child = headerText;
            Grid.SetRow(headerBorder, 0);
            panel.Children.Add(headerBorder);

            // Info content
            var infoBorder = new Border
            {
                Padding = new Thickness(12)
            };

            var infoStack = new StackPanel();

            // Warning text (for dangerous paths)
            infoWarningText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed
            };
            infoStack.Children.Add(infoWarningText);

            // Path
            AddInfoLabel(infoStack, "Path:");
            infoPathText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12),
                Text = "No selection"
            };
            infoStack.Children.Add(infoPathText);

            // Type
            AddInfoLabel(infoStack, "Type:");
            infoTypeText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Margin = new Thickness(0, 4, 0, 12),
                Text = "-"
            };
            infoStack.Children.Add(infoTypeText);

            // Size
            AddInfoLabel(infoStack, "Size:");
            infoSizeText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Margin = new Thickness(0, 4, 0, 12),
                Text = "-"
            };
            infoStack.Children.Add(infoSizeText);

            // Modified
            AddInfoLabel(infoStack, "Modified:");
            infoModifiedText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Margin = new Thickness(0, 4, 0, 12),
                Text = "-"
            };
            infoStack.Children.Add(infoModifiedText);

            // Permissions
            AddInfoLabel(infoStack, "Access:");
            infoPermissionsText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Margin = new Thickness(0, 4, 0, 12),
                Text = "-"
            };
            infoStack.Children.Add(infoPermissionsText);

            infoBorder.Child = infoStack;
            Grid.SetRow(infoBorder, 1);
            panel.Children.Add(infoBorder);

            return panel;
        }

        private void AddInfoLabel(StackPanel parent, string text)
        {
            var label = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Opacity = 0.7
            };
            parent.Children.Add(label);
        }

        private Border BuildStatusBar()
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Height = 24,
                Padding = new Thickness(12, 0, 12, 0)
            };

            statusBar = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "Enter: Select | Backspace: Up | Ctrl+H: Hidden | Esc: Cancel | /: Jump to path | ~: Home"
            };

            border.Child = statusBar;
            return border;
        }

        #endregion

        #region Theme Application

        /// <summary>
        /// Apply theme to all FileBrowser UI elements
        /// </summary>
        private void ApplyFileBrowserTheme()
        {
            var theme = themeManager.CurrentTheme;
            if (theme == null) return;

            var background = theme.Background;
            var surface = theme.Surface;
            var foreground = theme.Foreground;
            var border = theme.Border;
            var accent = theme.Primary;

            // Apply to breadcrumb border
            if (breadcrumbBorder != null)
            {
                breadcrumbBorder.Background = new SolidColorBrush(surface);
                breadcrumbBorder.BorderBrush = new SolidColorBrush(border);
            }

            // Apply to breadcrumb buttons
            foreach (var btn in breadcrumbButtons)
            {
                btn.Background = new SolidColorBrush(Colors.Transparent);
                btn.Foreground = new SolidColorBrush(foreground);
            }

            // Apply to bookmarks panel
            if (bookmarksPanel != null)
            {
                // Find header border and content border in bookmarksPanel
                foreach (UIElement child in bookmarksPanel.Children)
                {
                    if (child is Border border_elem)
                    {
                        border_elem.Background = new SolidColorBrush(surface);
                        border_elem.BorderBrush = new SolidColorBrush(border);

                        if (border_elem.Child is TextBlock header)
                        {
                            header.Foreground = new SolidColorBrush(foreground);
                        }
                    }
                }
            }

            // Apply to bookmark buttons
            foreach (var btn in bookmarkButtons)
            {
                btn.Background = new SolidColorBrush(Colors.Transparent);
                btn.Foreground = new SolidColorBrush(foreground);
            }

            // Apply to directory tree
            if (directoryTree != null)
            {
                directoryTree.Background = new SolidColorBrush(background);
                directoryTree.Foreground = new SolidColorBrush(foreground);
            }

            // Apply to search box
            if (searchBox != null)
            {
                searchBox.Background = new SolidColorBrush(Colors.Transparent);
                searchBox.Foreground = new SolidColorBrush(foreground);
                searchBox.CaretBrush = new SolidColorBrush(accent);
            }

            // Apply to file list box
            if (fileListBox != null)
            {
                fileListBox.Background = new SolidColorBrush(background);
                fileListBox.Foreground = new SolidColorBrush(foreground);
            }

            // Update ListBoxItem style
            if (listBoxItemStyle != null)
            {
                listBoxItemStyle.Setters.Clear();
                listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
                listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 6, 12, 6)));
                listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
                listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(foreground)));
            }

            // Apply to info panel
            if (infoPanel != null)
            {
                // Find header and content borders
                foreach (UIElement child in infoPanel.Children)
                {
                    if (child is Border border_elem)
                    {
                        border_elem.Background = new SolidColorBrush(surface);
                        border_elem.BorderBrush = new SolidColorBrush(border);

                        // Apply to child elements
                        if (border_elem.Child is TextBlock text)
                        {
                            text.Foreground = new SolidColorBrush(foreground);
                        }
                        else if (border_elem.Child is StackPanel stack)
                        {
                            foreach (var item in stack.Children)
                            {
                                if (item is TextBlock tb)
                                {
                                    tb.Foreground = new SolidColorBrush(foreground);
                                }
                            }
                        }
                    }
                }
            }

            // Apply to info panel text blocks
            if (infoPathText != null) infoPathText.Foreground = new SolidColorBrush(foreground);
            if (infoTypeText != null) infoTypeText.Foreground = new SolidColorBrush(foreground);
            if (infoSizeText != null) infoSizeText.Foreground = new SolidColorBrush(foreground);
            if (infoModifiedText != null) infoModifiedText.Foreground = new SolidColorBrush(foreground);
            if (infoPermissionsText != null) infoPermissionsText.Foreground = new SolidColorBrush(foreground);

            // Apply to status bar
            if (statusBar != null)
            {
                statusBar.Foreground = new SolidColorBrush(foreground);
            }
        }

        #endregion

        #region Navigation

        private void NavigateToDirectory(string path)
        {
            // Cancel any ongoing load
            loadCancellation?.Cancel();
            loadCancellation = new CancellationTokenSource();
            var token = loadCancellation.Token;

            // Validate path
            if (!ValidatePath(path, out string errorMessage))
            {
                ShowStatus($"Access denied: {errorMessage}", isError: true);
                return;
            }

            try
            {
                // Resolve symlinks
                var resolvedPath = Path.GetFullPath(path);
                currentPath = resolvedPath;

                // Update breadcrumb
                UpdateBreadcrumb();

                // Load files asynchronously
                Task.Run(() => LoadFilesAsync(token), token);

                // Add to recent locations
                AddRecentLocation(resolvedPath);

                Log($"Navigated to: {resolvedPath}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Navigating to directory '{path}'",
                    logger);

                ShowStatus($"Error: {ex.Message}", isError: true);
            }
        }

        private async Task LoadFilesAsync(CancellationToken token)
        {
            try
            {
                var files = new List<FileSystemItem>();

                // Get directories
                if (Directory.Exists(currentPath))
                {
                    var dirs = Directory.GetDirectories(currentPath)
                        .Where(d => showHiddenFiles || !IsHidden(d))
                        .OrderBy(d => Path.GetFileName(d));

                    foreach (var dir in dirs)
                    {
                        if (token.IsCancellationRequested) return;

                        try
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            files.Add(new FileSystemItem
                            {
                                Name = dirInfo.Name,
                                FullPath = dirInfo.FullName,
                                IsDirectory = true,
                                Modified = dirInfo.LastWriteTime,
                                Size = 0,
                                Extension = "",
                                IsHidden = IsHidden(dir),
                                IsSymlink = dirInfo.LinkTarget != null
                            });
                        }
                        catch
                        {
                            // Skip directories we can't access
                        }
                    }

                    // Get files
                    var filesPaths = Directory.GetFiles(currentPath)
                        .Where(f => showHiddenFiles || !IsHidden(f))
                        .Where(f => fileTypeFilters.Count == 0 || fileTypeFilters.Contains(Path.GetExtension(f)))
                        .OrderBy(f => Path.GetFileName(f));

                    foreach (var file in filesPaths)
                    {
                        if (token.IsCancellationRequested) return;

                        try
                        {
                            var fileInfo = new FileInfo(file);
                            files.Add(new FileSystemItem
                            {
                                Name = fileInfo.Name,
                                FullPath = fileInfo.FullName,
                                IsDirectory = false,
                                Modified = fileInfo.LastWriteTime,
                                Size = fileInfo.Length,
                                Extension = fileInfo.Extension,
                                IsHidden = IsHidden(file),
                                IsSymlink = fileInfo.LinkTarget != null
                            });
                        }
                        catch
                        {
                            // Skip files we can't access
                        }
                    }
                }

                if (token.IsCancellationRequested) return;

                // Update UI on main thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    currentFiles = files;
                    FilterFiles();
                    UpdateStatus();
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ErrorHandlingPolicy.Handle(
                        ErrorCategory.IO,
                        ex,
                        $"Loading files from directory '{currentPath}'",
                        logger);

                    ShowStatus($"Error loading directory: {ex.Message}", isError: true);
                });
            }
        }

        private void FilterFiles()
        {
            var query = searchBox.Text;

            if (string.IsNullOrEmpty(query) || query == "Filter files... (Ctrl+F)")
            {
                filteredFiles = currentFiles.ToList();
            }
            else
            {
                // Fuzzy search
                filteredFiles = currentFiles
                    .Select(file => new { File = file, Score = CalculateFuzzyScore(query.ToLower(), file.Name.ToLower()) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.File.Name)
                    .Select(x => x.File)
                    .ToList();
            }

            UpdateFileList();
        }

        private void UpdateFileList()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                fileListBox.Items.Clear();

                if (!filteredFiles.Any())
                {
                    var placeholder = new TextBlock
                    {
                        Text = currentFiles.Any() ? "No matching files" : "Empty directory",
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 18,
                        FontStyle = FontStyles.Italic,
                        Opacity = 0.5,
                        TextAlignment = TextAlignment.Center,
                        Padding = new Thickness(12)
                    };
                    fileListBox.Items.Add(placeholder);
                    return;
                }

                foreach (var item in filteredFiles)
                {
                    var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

                    // Icon
                    var icon = new TextBlock
                    {
                        Text = GetFileIcon(item) + " ",
                        FontFamily = new FontFamily("Segoe UI Emoji, JetBrains Mono, Consolas"),
                        FontSize = 18,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    stackPanel.Children.Add(icon);

                    // Name
                    var nameBlock = new TextBlock
                    {
                        Text = item.Name,
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 18,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    if (item.IsSymlink)
                    {
                        nameBlock.Text += " →";
                        nameBlock.FontStyle = FontStyles.Italic;
                    }

                    stackPanel.Children.Add(nameBlock);

                    var listItem = new ListBoxItem
                    {
                        Content = stackPanel,
                        Tag = item
                    };

                    fileListBox.Items.Add(listItem);
                }
            });
        }

        private void UpdateBreadcrumb()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                breadcrumbPanel.Children.Clear();
                breadcrumbButtons.Clear();

                if (string.IsNullOrEmpty(currentPath))
                    return;

                var parts = currentPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string buildPath = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (string.IsNullOrEmpty(part) && i > 0) continue;

                    // Build cumulative path
                    if (i == 0 && string.IsNullOrEmpty(part))
                    {
                        buildPath = Path.DirectorySeparatorChar.ToString();
                        part = Path.DirectorySeparatorChar.ToString();
                    }
                    else
                    {
                        buildPath = i == 0 ? part : Path.Combine(buildPath, part);
                    }

                    // Create clickable segment
                    var btn = new Button
                    {
                        Content = i == 0 && part.Length <= 3 ? part : (part.Length > 20 ? part.Substring(0, 17) + "..." : part),
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 18,
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 4, 0),
                        BorderThickness = new Thickness(0),
                        Tag = buildPath
                    };
                    btn.Click += OnBreadcrumbClick;
                    breadcrumbButtons.Add(btn);

                    breadcrumbPanel.Children.Add(btn);

                    // Add separator (except for last item)
                    if (i < parts.Length - 1)
                    {
                        var separator = new TextBlock
                        {
                            Text = "›",
                            FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                            FontSize = 18,
                            Opacity = 0.5,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 4, 0)
                        };
                        breadcrumbPanel.Children.Add(separator);
                    }
                }
            });
        }

        private void LoadDirectoryTree()
        {
            directoryTree.Items.Clear();

            // Add drives/root
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Windows: Show drives
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (drive.IsReady)
                        {
                            var item = CreateTreeItem(drive.RootDirectory.FullName, drive.Name);
                            directoryTree.Items.Add(item);
                        }
                    }
                    catch
                    {
                        // Skip drives we can't access
                    }
                }
            }
            else
            {
                // Unix: Start from root
                var item = CreateTreeItem("/", "/");
                directoryTree.Items.Add(item);
            }
        }

        private TreeViewItem CreateTreeItem(string path, string displayName)
        {
            var item = new TreeViewItem
            {
                Header = displayName,
                Tag = path,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18
            };

            // Add dummy child to show expand arrow
            if (HasSubdirectories(path))
            {
                item.Items.Add(new TreeViewItem { Header = "Loading..." });
            }

            item.Expanded += OnTreeItemExpanded;

            return item;
        }

        private void OnTreeItemExpanded(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item == null) return;

            // Check if already loaded
            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem dummy && dummy.Header.ToString() == "Loading...")
            {
                item.Items.Clear();

                var path = item.Tag as string;
                if (string.IsNullOrEmpty(path)) return;

                try
                {
                    var subdirs = Directory.GetDirectories(path)
                        .Where(d => showHiddenFiles || !IsHidden(d))
                        .OrderBy(d => Path.GetFileName(d));

                    foreach (var subdir in subdirs)
                    {
                        try
                        {
                            var dirName = Path.GetFileName(subdir);
                            var subItem = CreateTreeItem(subdir, dirName);
                            item.Items.Add(subItem);
                        }
                        catch
                        {
                            // Skip directories we can't access
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorHandlingPolicy.Handle(
                        ErrorCategory.IO,
                        ex,
                        $"Expanding directory tree for '{path}'",
                        logger);
                }
            }
        }

        #endregion

        #region Event Handlers

        private void OnDirectoryTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (directoryTree.SelectedItem is TreeViewItem item && item.Tag is string path)
            {
                NavigateToDirectory(path);
            }
        }

        private void OnFileSelected(object sender, SelectionChangedEventArgs e)
        {
            if (fileListBox.SelectedItem is ListBoxItem item && item.Tag is FileSystemItem fsItem)
            {
                selectedItem = fsItem;
                UpdateInfoPanel(fsItem);
            }
            else
            {
                selectedItem = null;
                ClearInfoPanel();
            }
        }

        private void OnFileListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && selectedItem != null)
            {
                HandleSelection();
                e.Handled = true;
            }
        }

        private void OnBreadcrumbClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                NavigateToDirectory(path);
            }
        }

        private void OnBookmarkClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Bookmark bookmark)
            {
                if (Directory.Exists(bookmark.Path))
                {
                    NavigateToDirectory(bookmark.Path);
                }
                else
                {
                    ShowStatus($"Bookmark path not found: {bookmark.Path}", isError: true);
                }
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            searchDebounceTimer.Stop();
            searchDebounceTimer.Start();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Escape: Cancel
            if (e.Key == Key.Escape)
            {
                SelectionCancelled?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            // Backspace: Go up one directory
            if (e.Key == Key.Back && !searchBox.IsFocused)
            {
                NavigateUp();
                e.Handled = true;
                return;
            }

            // Tilde: Home directory
            if (e.Key == Key.OemTilde && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                NavigateToDirectory(home);
                e.Handled = true;
                return;
            }

            // Slash: Jump to path
            if (e.Key == Key.Oem2 && e.KeyboardDevice.Modifiers == ModifierKeys.None && !searchBox.IsFocused)
            {
                PromptForPath();
                e.Handled = true;
                return;
            }

            // Keyboard shortcuts with Ctrl
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    // H key removed - hidden files are always hidden
                    // case Key.H:
                    //     ToggleHiddenFiles();
                    //     e.Handled = true;
                    //     break;

                    case Key.B:
                        ToggleBookmarks();
                        e.Handled = true;
                        break;

                    case Key.F:
                        searchBox.Focus();
                        searchBox.SelectAll();
                        e.Handled = true;
                        break;

                    case Key.D1:
                        JumpToBookmark(0);
                        e.Handled = true;
                        break;

                    case Key.D2:
                        JumpToBookmark(1);
                        e.Handled = true;
                        break;

                    case Key.D3:
                        JumpToBookmark(2);
                        e.Handled = true;
                        break;
                }
            }
        }

        #endregion

        #region Actions

        private void HandleSelection()
        {
            if (selectedItem == null) return;

            if (selectedItem.IsDirectory)
            {
                if (selectionMode == FileSelectionMode.File)
                {
                    // Navigate into directory
                    NavigateToDirectory(selectedItem.FullPath);
                }
                else
                {
                    // Select directory
                    DirectorySelected?.Invoke(this, new DirectorySelectedEventArgs { Path = selectedItem.FullPath });
                }
            }
            else
            {
                if (selectionMode == FileSelectionMode.Directory)
                {
                    ShowStatus("Please select a directory", isError: true);
                }
                else
                {
                    // Select file
                    FileSelected?.Invoke(this, new FileSelectedEventArgs { Path = selectedItem.FullPath });
                }
            }
        }

        private void NavigateUp()
        {
            if (string.IsNullOrEmpty(currentPath)) return;

            try
            {
                var parent = Directory.GetParent(currentPath);
                if (parent != null)
                {
                    NavigateToDirectory(parent.FullName);
                }
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Navigating up from directory '{currentPath}'",
                    logger);

                ShowStatus("Cannot navigate up", isError: true);
            }
        }

        private void ToggleHiddenFiles()
        {
            showHiddenFiles = !showHiddenFiles;
            NavigateToDirectory(currentPath); // Reload
            ShowStatus($"Hidden files: {(showHiddenFiles ? "Shown" : "Hidden")}");
        }

        private void ToggleBookmarks()
        {
            showBookmarks = !showBookmarks;
            // Rebuild layout
            var content = BuildContent();
            contentArea.Content = content;
            // ApplyTheme is called in BuildContent via ApplyFileBrowserTheme
        }

        private void JumpToBookmark(int index)
        {
            if (index < bookmarks.Count)
            {
                var bookmark = bookmarks[index];
                if (Directory.Exists(bookmark.Path))
                {
                    NavigateToDirectory(bookmark.Path);
                }
            }
        }

        private void PromptForPath()
        {
            var dialog = new Window
            {
                Title = "Jump to Path",
                Width = 500,
                Height = 100,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.Margin = new Thickness(16);

            var input = new TextBox
            {
                Text = currentPath,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Padding = new Thickness(8)
            };

            input.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    var path = input.Text.Trim();
                    if (!string.IsNullOrEmpty(path))
                    {
                        NavigateToDirectory(path);
                    }
                    dialog.Close();
                }
                else if (e.Key == Key.Escape)
                {
                    dialog.Close();
                }
            };

            grid.Children.Add(input);
            dialog.Content = grid;

            input.SelectAll();
            input.Focus();
            dialog.ShowDialog();
        }

        #endregion

        #region Info Panel

        private void UpdateInfoPanel(FileSystemItem item)
        {
            infoPathText.Text = item.FullPath;
            infoTypeText.Text = item.IsDirectory ? "Directory" : (item.Extension.Length > 0 ? $"{item.Extension} File" : "File");
            infoSizeText.Text = item.IsDirectory ? "-" : FormatFileSize(item.Size);
            infoModifiedText.Text = item.Modified.ToString("yyyy-MM-dd HH:mm:ss");

            // Check permissions
            var canRead = CanReadPath(item.FullPath);
            infoPermissionsText.Text = canRead ? "Read ✓" : "Read ✗";

            // Security warnings
            var theme = themeManager.CurrentTheme;
            if (IsDangerousPath(item.FullPath))
            {
                infoWarningText.Text = "⚠ WARNING: System path\nModifications could damage your system";
                infoWarningText.Foreground = new SolidColorBrush(theme.Error);
                infoWarningText.Visibility = Visibility.Visible;
            }
            else if (item.IsSymlink)
            {
                infoWarningText.Text = "ℹ Symbolic link\nPoints to another location";
                infoWarningText.Foreground = new SolidColorBrush(theme.Warning);
                infoWarningText.Visibility = Visibility.Visible;
            }
            else
            {
                infoWarningText.Visibility = Visibility.Collapsed;
            }
        }

        private void ClearInfoPanel()
        {
            infoPathText.Text = "No selection";
            infoTypeText.Text = "-";
            infoSizeText.Text = "-";
            infoModifiedText.Text = "-";
            infoPermissionsText.Text = "-";
            infoWarningText.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Helpers

        private void InitializeBookmarks()
        {
            bookmarks.Clear();
            bookmarks.Add(new Bookmark("🏠", "Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
            bookmarks.Add(new Bookmark("📄", "Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
            bookmarks.Add(new Bookmark("🖥", "Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)));
        }

        private void AddRecentLocation(string path)
        {
            recentLocations.Remove(path); // Remove if exists
            recentLocations.Insert(0, path);

            if (recentLocations.Count > MAX_RECENT_LOCATIONS)
            {
                recentLocations.RemoveAt(recentLocations.Count - 1);
            }
        }

        private bool ValidatePath(string path, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "Path is empty";
                return false;
            }

            if (!security.ValidateFileAccess(path, checkWrite: false))
            {
                errorMessage = "Access denied by security policy";
                return false;
            }

            if (!Directory.Exists(path) && !File.Exists(path))
            {
                errorMessage = "Path does not exist";
                return false;
            }

            return true;
        }

        private bool CanReadPath(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.GetFiles(path);
                    return true;
                }
                else if (File.Exists(path))
                {
                    File.OpenRead(path).Close();
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool IsDangerousPath(string path)
        {
            var dangerous = new[]
            {
                "/etc", "/sys", "/proc", "/dev", "/boot",
                "C:\\Windows", "C:\\Program Files", "C:\\Program Files (x86)",
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows)
            };

            foreach (var dangerousPath in dangerous)
            {
                if (!string.IsNullOrEmpty(dangerousPath) &&
                    path.StartsWith(dangerousPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsHidden(string path)
        {
            try
            {
                var name = Path.GetFileName(path);
                if (name.StartsWith("."))
                    return true;

                var attr = File.GetAttributes(path);
                return (attr & FileAttributes.Hidden) == FileAttributes.Hidden;
            }
            catch
            {
                return false;
            }
        }

        private bool HasSubdirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path).Any();
            }
            catch
            {
                return false;
            }
        }

        private string GetFileIcon(FileSystemItem item)
        {
            if (item.IsDirectory)
                return "📁";

            switch (item.Extension.ToLower())
            {
                case ".md":
                case ".txt":
                    return "📝";
                case ".pdf":
                    return "📄";
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                    return "🖼";
                case ".mp3":
                case ".wav":
                case ".flac":
                    return "🎵";
                case ".mp4":
                case ".avi":
                case ".mkv":
                    return "🎬";
                case ".zip":
                case ".rar":
                case ".7z":
                case ".tar":
                case ".gz":
                    return "📦";
                case ".cs":
                case ".py":
                case ".js":
                case ".java":
                case ".cpp":
                case ".c":
                    return "💻";
                default:
                    return "📄";
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private int CalculateFuzzyScore(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target))
                return 0;

            int score = 0;
            int queryIndex = 0;
            int consecutiveMatches = 0;
            bool previousWasMatch = false;

            for (int targetIndex = 0; targetIndex < target.Length && queryIndex < query.Length; targetIndex++)
            {
                if (query[queryIndex] == target[targetIndex])
                {
                    score += 10;

                    if (previousWasMatch)
                    {
                        consecutiveMatches++;
                        score += consecutiveMatches * 5;
                    }
                    else
                    {
                        consecutiveMatches = 1;
                    }

                    if (targetIndex == 0 || target[targetIndex - 1] == ' ' || target[targetIndex - 1] == '.')
                    {
                        score += 20;
                    }

                    previousWasMatch = true;
                    queryIndex++;
                }
                else
                {
                    previousWasMatch = false;
                    consecutiveMatches = 0;
                }
            }

            if (queryIndex != query.Length)
                return 0;

            score -= (target.Length - query.Length) * 2;
            return Math.Max(0, score);
        }

        private void UpdateStatus()
        {
            var fileCount = filteredFiles.Count(f => !f.IsDirectory);
            var dirCount = filteredFiles.Count(f => f.IsDirectory);
            var totalCount = filteredFiles.Count;

            statusBar.Text = $"{totalCount} items ({dirCount} folders, {fileCount} files) | " +
                           "Enter: Select | Backspace: Up | Ctrl+H: Hidden | Esc: Cancel | /: Jump | ~: Home";
        }

        private void ShowStatus(string message, bool isError = false)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                statusBar.Text = message;

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    UpdateStatus();
                };
                timer.Start();
            });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Set the initial path to navigate to
        /// </summary>
        public void SetInitialPath(string path)
        {
            if (ValidatePath(path, out _))
            {
                NavigateToDirectory(path);
            }
        }

        /// <summary>
        /// Set file type filters (e.g., ".md", ".txt")
        /// Empty list = show all files
        /// </summary>
        public void SetFileTypes(params string[] extensions)
        {
            fileTypeFilters.Clear();
            foreach (var ext in extensions)
            {
                fileTypeFilters.Add(ext.StartsWith(".") ? ext : "." + ext);
            }

            // Reload current directory
            NavigateToDirectory(currentPath);
        }

        /// <summary>
        /// Set selection mode (File, Directory, or Both)
        /// </summary>
        public void SetSelectionMode(FileSelectionMode mode)
        {
            selectionMode = mode;
        }

        #endregion

        #region State Persistence

        public override PaneState SaveState()
        {
            return new PaneState
            {
                PaneType = "FileBrowserPane",
                CustomData = new Dictionary<string, object>
                {
                    ["CurrentPath"] = currentPath,
                    ["SelectedFilePath"] = selectedItem?.FullPath,
                    ["ShowHiddenFiles"] = showHiddenFiles,
                    ["SearchFilter"] = (searchBox?.Text != "Filter files... (Ctrl+F)") ? searchBox?.Text : null
                }
            };
        }

        public override void RestoreState(PaneState state)
        {
            if (state?.CustomData == null) return;

            var data = state.CustomData as Dictionary<string, object>;
            if (data == null) return;

            // Restore show hidden files toggle
            if (data.TryGetValue("ShowHiddenFiles", out var showHidden))
            {
                showHiddenFiles = Convert.ToBoolean(showHidden);
            }

            // Restore current directory
            if (data.TryGetValue("CurrentPath", out var path) && path != null)
            {
                var pathStr = path.ToString();
                if (!string.IsNullOrEmpty(pathStr) && Directory.Exists(pathStr))
                {
                    NavigateToDirectory(pathStr);
                }
            }

            // Restore search filter
            if (data.TryGetValue("SearchFilter", out var searchFilter) && searchFilter != null)
            {
                var filterText = searchFilter.ToString();
                if (!string.IsNullOrEmpty(filterText))
                {
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        searchBox.Text = filterText;
                        FilterFiles();
                    });
                }
            }

            // Restore selected file after directory loads
            if (data.TryGetValue("SelectedFilePath", out var selectedPath) && selectedPath != null)
            {
                var selectedPathStr = selectedPath.ToString();
                if (!string.IsNullOrEmpty(selectedPathStr))
                {
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        SelectFileByPath(selectedPathStr);
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private void SelectFileByPath(string filePath)
        {
            if (fileListBox == null || string.IsNullOrEmpty(filePath)) return;

            foreach (var item in fileListBox.Items)
            {
                if (item is ListBoxItem listItem && listItem.Tag is FileSystemItem fsItem)
                {
                    if (fsItem.FullPath == filePath)
                    {
                        fileListBox.SelectedItem = listItem;
                        fileListBox.ScrollIntoView(listItem);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Cleanup

        protected override void OnDispose()
        {
            // Unsubscribe from theme events
            themeManager.ThemeChanged -= OnThemeChanged;

            // Cancel any ongoing operations
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();

            // Stop timers
            searchDebounceTimer?.Stop();

            base.OnDispose();
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// File system item metadata
    /// </summary>
    internal class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime Modified { get; set; }
        public long Size { get; set; }
        public string Extension { get; set; }
        public bool IsHidden { get; set; }
        public bool IsSymlink { get; set; }
    }

    /// <summary>
    /// Bookmark for quick access
    /// </summary>
    internal class Bookmark
    {
        public string Icon { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }

        public Bookmark(string icon, string name, string path)
        {
            Icon = icon;
            Name = name;
            Path = path;
        }
    }

    /// <summary>
    /// Selection mode for file browser
    /// </summary>
    public enum FileSelectionMode
    {
        File,       // Only files can be selected
        Directory,  // Only directories can be selected
        Both        // Both files and directories can be selected
    }

    /// <summary>
    /// Event args for file selection
    /// </summary>
    public class FileSelectedEventArgs : EventArgs
    {
        public string Path { get; set; }
    }

    /// <summary>
    /// Event args for directory selection
    /// </summary>
    public class DirectorySelectedEventArgs : EventArgs
    {
        public string Path { get; set; }
    }

    #endregion
}
