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
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// SIMPLIFIED FILE BROWSER PANE
    ///
    /// PURPOSE: Simple directory browsing like 'ls'
    ///
    /// FEATURES:
    /// - Single list showing directory contents
    /// - "[...]" item at top to go up one level
    /// - Enter to navigate into directories or select files
    /// - Keyboard-focused, no search or other distractions
    /// - Security: Path validation via ISecurityManager
    ///
    /// INTEGRATION:
    /// - FileSelected(string path) - fired when user selects file
    /// - DirectorySelected(string path) - fired when user selects directory
    /// </summary>
    public class FileBrowserPane : PaneBase
    {
        #region Fields

        private readonly IConfigurationManager config;
        private readonly ISecurityManager security;
        private readonly IEventBus eventBus;

        // Event handlers (store references for proper unsubscription)
        private Action<Core.Events.ProjectSelectedEvent> projectSelectedHandler;
        private Action<Core.Events.RefreshRequestedEvent> refreshRequestedHandler;

        // UI Components - Simple single list
        private ListBox fileListBox;
        private TextBlock pathHeader;
        private Style listBoxItemStyle;

        // State
        private string currentPath;
        private List<FileSystemItem> currentFiles = new List<FileSystemItem>();
        private FileSystemItem selectedItem;
        private bool showHiddenFiles = false;
        private CancellationTokenSource loadCancellation;
        private CancellationTokenSource disposalCancellation;

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

        #endregion

        #region Constructor

        public FileBrowserPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager config,
            ISecurityManager security,
            IEventBus eventBus)
            : base(logger, themeManager, projectContext)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.security = security ?? throw new ArgumentNullException(nameof(security));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            PaneName = "File Browser";
            PaneIcon = "📁";

            // Initialize cancellation token
            disposalCancellation = new CancellationTokenSource();

            // Default to current directory, will be overridden by SetInitialPath or config
            currentPath = Directory.GetCurrentDirectory();
        }

        #endregion

        #region Initialization

        public override void Initialize()
        {
            base.Initialize();

            logger.Log(LogLevel.Info, PaneName, "=== FileBrowser Initialize() START ===");

            // Try to get initial path from config
            try
            {
                var configPath = config.Get<string>("FileBrowser.InitialPath", null);
                if (!string.IsNullOrEmpty(configPath) && Directory.Exists(configPath))
                {
                    currentPath = configPath;
                    logger.Log(LogLevel.Info, PaneName, $"Using configured initial path: {currentPath}");
                }
                else
                {
                    // Fall back to Documents folder
                    var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (Directory.Exists(documents))
                    {
                        currentPath = documents;
                        logger.Log(LogLevel.Info, PaneName, $"Using Documents folder: {currentPath}");
                    }
                    else
                    {
                        logger.Log(LogLevel.Info, PaneName, $"Using working directory: {currentPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, PaneName, $"Error reading config, using default path: {ex.Message}");
            }

            logger.Log(LogLevel.Info, PaneName, $"Final path before navigation: {currentPath}");
            logger.Log(LogLevel.Info, PaneName, $"fileListBox null? {fileListBox == null}");

            // Subscribe to EventBus for cross-pane communication
            projectSelectedHandler = OnProjectSelected;
            eventBus.Subscribe(projectSelectedHandler);

            refreshRequestedHandler = OnRefreshRequested;
            eventBus.Subscribe(refreshRequestedHandler);

            logger.Log(LogLevel.Info, PaneName, "About to schedule NavigateToDirectory");

            // Post navigation to dispatcher queue AFTER UI is fully loaded
            // This ensures the file list UI is attached to the visual tree before populating it
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    logger.Log(LogLevel.Info, PaneName, $"=== NavigateToDirectory DISPATCHER CALLBACK START ===");
                    logger.Log(LogLevel.Info, PaneName, $"Navigating to initial path: {currentPath}");
                    logger.Log(LogLevel.Info, PaneName, $"fileListBox null in callback? {fileListBox == null}");
                    NavigateToDirectory(currentPath);
                    logger.Log(LogLevel.Info, PaneName, $"=== NavigateToDirectory DISPATCHER CALLBACK END ===");
                })
            );

            logger.Log(LogLevel.Info, PaneName, "=== FileBrowser Initialize() END ===");
        }

        #endregion

        #region Build UI

        protected override UIElement BuildContent()
        {
            var mainLayout = new Grid();
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Path header
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // File list

            // Path header
            var headerBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 32,
                Padding = new Thickness(12, 6, 12, 6)
            };

            pathHeader = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Text = currentPath
            };

            headerBorder.Child = pathHeader;
            Grid.SetRow(headerBorder, 0);
            mainLayout.Children.Add(headerBorder);

            // File list
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

            Grid.SetRow(fileListBox, 1);
            mainLayout.Children.Add(fileListBox);

            // Subscribe to theme changes
            themeManager.ThemeChanged += OnThemeChanged;

            // Apply initial theme
            ApplyFileBrowserTheme();

            // Don't load directory here - wait for Initialize() to set correct path
            return mainLayout;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyFileBrowserTheme();
        }

        #endregion

        #region Theme Application

        /// <summary>
        /// Apply theme to FileBrowser UI elements
        /// </summary>
        private void ApplyFileBrowserTheme()
        {
            var theme = themeManager.CurrentTheme;
            if (theme == null) return;

            var background = theme.Background;
            var foreground = theme.Foreground;
            var border = theme.Border;
            var surface = theme.Surface;

            // Apply to path header
            if (pathHeader != null)
            {
                pathHeader.Foreground = new SolidColorBrush(foreground);
                if (pathHeader.Parent is Border headerBorder)
                {
                    headerBorder.Background = new SolidColorBrush(background);
                    headerBorder.BorderBrush = new SolidColorBrush(border);
                }
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
        }

        #endregion

        #region Navigation

        private void NavigateToDirectory(string path)
        {
            logger.Log(LogLevel.Debug, PaneName, $"NavigateToDirectory called with path: {path}");

            // Cancel any ongoing load
            loadCancellation?.Cancel();
            loadCancellation = new CancellationTokenSource();

            // Link load cancellation to disposal cancellation
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                loadCancellation.Token,
                disposalCancellation.Token);
            var token = linkedCts.Token;

            // Validate path
            if (!ValidatePath(path, out string errorMessage))
            {
                logger.Log(LogLevel.Warning, PaneName, $"Path validation failed: {errorMessage}");
                return;
            }

            try
            {
                // Resolve symlinks
                var resolvedPath = Path.GetFullPath(path);
                currentPath = resolvedPath;

                logger.Log(LogLevel.Debug, PaneName, $"Resolved path to: {resolvedPath}");

                // Update path header
                if (pathHeader != null)
                {
                    // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
                    this.Dispatcher.Invoke(() =>
                    {
                        pathHeader.Text = currentPath;
                    });
                }

                // Load files asynchronously on background thread
                // Use async lambda to properly await the async method
                _ = Task.Run(async () => await LoadFilesAsync(token), token);

                logger.Log(LogLevel.Info, PaneName, $"Started loading files from: {resolvedPath}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Navigating to directory '{path}'",
                    logger);
            }
        }

        private async Task LoadFilesAsync(CancellationToken token)
        {
            logger.Log(LogLevel.Debug, PaneName, $"LoadFilesAsync started for: {currentPath}");

            try
            {
                var files = new List<FileSystemItem>();

                // Get directories
                if (Directory.Exists(currentPath))
                {
                    logger.Log(LogLevel.Debug, PaneName, $"Directory exists, loading contents...");

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

                logger.Log(LogLevel.Debug, PaneName, $"Found {files.Count} items (dirs + files)");

                // Update UI on main thread with DataBind priority
                // DataBind priority ensures UI updates complete before any focus operations
                // This prevents race conditions where focus tries to target not-yet-rendered items
                // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
                try
                {
                    await this.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            if (token.IsCancellationRequested)
                            {
                                logger.Log(LogLevel.Debug, PaneName, "Token cancelled before UI update");
                                return;
                            }

                            logger.Log(LogLevel.Debug, PaneName, $"About to set currentFiles to {files.Count} items");
                            currentFiles = files;
                            logger.Log(LogLevel.Debug, PaneName, $"Set currentFiles, now calling UpdateFileList");
                            UpdateFileList();
                            logger.Log(LogLevel.Debug, PaneName, "UpdateFileList completed successfully");
                        }
                        catch (Exception innerEx)
                        {
                            logger.Log(LogLevel.Error, PaneName, $"Exception in UI update: {innerEx.GetType().Name}: {innerEx.Message}\n{innerEx.StackTrace}");
                            throw;
                        }
                    }, DispatcherPriority.DataBind);  // Changed from default (Normal) to DataBind
                }
                catch (Exception dispatcherEx)
                {
                    logger.Log(LogLevel.Error, PaneName, $"Dispatcher.InvokeAsync failed: {dispatcherEx.GetType().Name}: {dispatcherEx.Message}\n{dispatcherEx.StackTrace}");
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation, ignore
                logger.Log(LogLevel.Debug, PaneName, "LoadFilesAsync cancelled");
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    logger.Log(LogLevel.Error, PaneName, $"Error loading files: {ex.Message}");
                    // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
                    await this.Dispatcher.InvokeAsync(() =>
                    {
                        ErrorHandlingPolicy.Handle(
                            ErrorCategory.IO,
                            ex,
                            $"Loading files from directory '{currentPath}'",
                            logger);
                    });
                }
            }
        }

        private void UpdateFileList()
        {
            // NOTE: This is called from LoadFilesAsync which already uses Dispatcher.InvokeAsync,
            // so we're already on the UI thread. No need for another dispatcher wrapper.
            logger.Log(LogLevel.Debug, PaneName, $"UpdateFileList called with {currentFiles.Count} items");

            if (fileListBox == null)
            {
                logger.Log(LogLevel.Error, PaneName, "fileListBox is null!");
                return;
            }

            fileListBox.Items.Clear();
            logger.Log(LogLevel.Debug, PaneName, "Cleared fileListBox");

            // Add parent directory "[...]" item at the top (except for root)
            var parent = Directory.GetParent(currentPath);
            if (parent != null)
            {
                var parentItem = new ListBoxItem
                {
                    Content = new TextBlock
                    {
                        Text = "[...]",
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 18,
                        Foreground = fileListBox.Foreground
                    },
                    Tag = new FileSystemItem
                    {
                        Name = "[...]",
                        FullPath = parent.FullName,
                        IsDirectory = true,
                        Modified = DateTime.Now,
                        Size = 0,
                        Extension = "",
                        IsHidden = false,
                        IsSymlink = false
                    }
                };
                fileListBox.Items.Add(parentItem);
                logger.Log(LogLevel.Debug, PaneName, "Added [...] parent item");
            }

            // Add current directory contents
            int addedCount = 0;
            foreach (var item in currentFiles)
            {
                var icon = GetFileIcon(item);
                var text = $"{icon} {item.Name}";

                var listItem = new ListBoxItem
                {
                    Content = new TextBlock
                    {
                        Text = text,
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 18,
                        Foreground = fileListBox.Foreground
                    },
                    Tag = item
                };

                fileListBox.Items.Add(listItem);
                addedCount++;
            }

            logger.Log(LogLevel.Info, PaneName, $"Added {addedCount} items to fileListBox, total count: {fileListBox.Items.Count}");

            // Auto-select first item if list is not empty
            if (fileListBox.Items.Count > 0)
            {
                fileListBox.SelectedIndex = 0;
                logger.Log(LogLevel.Debug, PaneName, "Selected first item");
            }
        }

        #endregion

        #region Event Handlers

        private void OnFileSelected(object sender, SelectionChangedEventArgs e)
        {
            if (fileListBox.SelectedItem is ListBoxItem item && item.Tag is FileSystemItem fsItem)
            {
                selectedItem = fsItem;
            }
            else
            {
                selectedItem = null;
            }
        }

        private void OnFileListKeyDown(object sender, KeyEventArgs e)
        {
            // Guard against processing input when we don't have focus
            if (!this.IsKeyboardFocusWithin) return;

            if (e.Key == Key.Enter && selectedItem != null)
            {
                HandleSelection();
                e.Handled = true;
            }
        }

        #endregion

        #region EventBus Handlers

        /// <summary>
        /// Handle ProjectSelectedEvent - navigate to project directory when project selected
        /// </summary>
        private void OnProjectSelected(Core.Events.ProjectSelectedEvent evt)
        {
            if (evt?.Project == null) return;

            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() =>
            {
                // Try to get working directory from CustomFields
                string projectPath = null;
                if (evt.Project.CustomFields != null && evt.Project.CustomFields.TryGetValue("WorkingDirectory", out projectPath))
                {
                    if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
                    {
                        NavigateToDirectory(projectPath);
                        Log($"Navigated to project directory: {projectPath}");
                        return;
                    }
                }

                Log($"Project {evt.Project.Name} has no valid working directory in CustomFields", LogLevel.Info);
            });
        }

        /// <summary>
        /// Handle RefreshRequestedEvent - refresh current directory
        /// </summary>
        private void OnRefreshRequested(Core.Events.RefreshRequestedEvent evt)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() =>
            {
                RefreshCurrentDirectory();
                Log("FileBrowserPane refreshed (RefreshRequestedEvent)");
            });
        }

        /// <summary>
        /// Refresh current directory without changing navigation
        /// </summary>
        private void RefreshCurrentDirectory()
        {
            if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
            {
                NavigateToDirectory(currentPath);
            }
        }

        #endregion

        #region Actions

        private void HandleSelection()
        {
            if (selectedItem == null) return;

            if (selectedItem.IsDirectory)
            {
                // Navigate into directory (or up if it's the [...] item)
                NavigateToDirectory(selectedItem.FullPath);
            }
            else
            {
                // Preserve focus state before event publishing
                // Event handlers in other panes might try to steal focus
                var hadFocus = this.IsKeyboardFocusWithin;

                // Select file
                FileSelected?.Invoke(this, new FileSelectedEventArgs { Path = selectedItem.FullPath });

                // Publish FileSelectedEvent to EventBus for cross-pane communication
                eventBus.Publish(new Core.Events.FileSelectedEvent
                {
                    FilePath = selectedItem.FullPath,
                    FileName = selectedItem.Name,
                    FileSize = selectedItem.Size,
                    SelectedAt = DateTime.Now
                });

                Log($"Published FileSelectedEvent: {selectedItem.FullPath}");

                // Restore focus after event handlers complete
                // This ensures file browser remains navigable after file selection
                if (hadFocus)
                {
                    // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
                    this.Dispatcher.InvokeAsync(() =>
                    {
                        if (fileListBox != null && fileListBox.Items.Count > 0)
                        {
                            Keyboard.Focus(fileListBox);
                        }
                    }, DispatcherPriority.Render);
                }
            }
        }

        #endregion


        #region Helpers

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

        #endregion

        #region State Persistence

        public override PaneState SaveState()
        {
            var data = new Dictionary<string, object>
            {
                ["CurrentPath"] = currentPath,
                ["ShowHiddenFiles"] = showHiddenFiles
            };

            // Save selection state for restoration
            if (fileListBox != null)
            {
                data["SelectedIndex"] = fileListBox.SelectedIndex;

                // Save selected item path for reliable restoration across refreshes
                if (selectedItem != null)
                {
                    data["SelectedItemPath"] = selectedItem.FullPath;
                    data["SelectedItemName"] = selectedItem.Name;
                }

                // Save scroll position (requires finding ScrollViewer)
                var scrollViewer = FindVisualChild<ScrollViewer>(fileListBox);
                if (scrollViewer != null)
                {
                    data["ScrollOffset"] = scrollViewer.VerticalOffset;
                }
            }

            return new PaneState
            {
                PaneType = "FileBrowserPane",
                CustomData = data
            };
        }

        // Helper method to find ScrollViewer in visual tree
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// Override to handle when pane gains focus - focus file list
        /// </summary>
        protected override void OnPaneGainedFocus()
        {
            // Use async dispatch to avoid race conditions with focus system
            // and provide proper fallback when list is empty
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() =>
            {
                if (fileListBox != null && fileListBox.Items.Count > 0)
                {
                    // List has items - focus it for keyboard navigation
                    Keyboard.Focus(fileListBox);
                }
                else if (fileListBox != null)
                {
                    // List is empty but exists - focus it anyway for keyboard navigation
                    // This ensures keyboard input works even when directory is empty
                    Keyboard.Focus(fileListBox);
                }
                else
                {
                    // Fallback - focus the pane itself if list hasn't been created yet
                    // This handles race conditions during initialization
                    Keyboard.Focus(this);
                }
            }, DispatcherPriority.Render);
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

                    // Restore selection and scroll position after navigation
                    // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
                    this.Dispatcher.InvokeAsync(() =>
                    {
                        if (fileListBox == null || fileListBox.Items.Count == 0) return;

                        // Try to restore by SelectedItemPath first (most reliable)
                        if (data.TryGetValue("SelectedItemPath", out var selectedPath) && selectedPath != null)
                        {
                            var pathToFind = selectedPath.ToString();
                            for (int i = 0; i < fileListBox.Items.Count; i++)
                            {
                                if (fileListBox.Items[i] is ListBoxItem item &&
                                    item.Tag is FileSystemItem fsItem &&
                                    fsItem.FullPath == pathToFind)
                                {
                                    fileListBox.SelectedIndex = i;
                                    fileListBox.ScrollIntoView(item);
                                    logger.Log(LogLevel.Debug, PaneName,
                                        $"Restored selection to: {fsItem.Name}");
                                    break;
                                }
                            }
                        }
                        // Fallback to SelectedIndex if path not found
                        else if (data.TryGetValue("SelectedIndex", out var selectedIdx))
                        {
                            int index = Convert.ToInt32(selectedIdx);
                            if (index >= 0 && index < fileListBox.Items.Count)
                            {
                                fileListBox.SelectedIndex = index;
                                fileListBox.ScrollIntoView(fileListBox.Items[index]);
                            }
                        }

                        // Restore scroll position
                        if (data.TryGetValue("ScrollOffset", out var scrollOffset))
                        {
                            var scrollViewer = FindVisualChild<ScrollViewer>(fileListBox);
                            if (scrollViewer != null)
                            {
                                double offset = Convert.ToDouble(scrollOffset);
                                scrollViewer.ScrollToVerticalOffset(offset);
                            }
                        }
                    }, DispatcherPriority.Loaded); // Use Loaded to ensure list is populated
                }
            }
        }

        #endregion

        #region Cleanup

        protected override void OnDispose()
        {
            // Cancel all async operations FIRST
            disposalCancellation?.Cancel();

            // Unsubscribe from EventBus to prevent memory leaks
            if (projectSelectedHandler != null)
            {
                eventBus.Unsubscribe(projectSelectedHandler);
                projectSelectedHandler = null;
            }

            if (refreshRequestedHandler != null)
            {
                eventBus.Unsubscribe(refreshRequestedHandler);
                refreshRequestedHandler = null;
            }

            // Unsubscribe from theme events
            themeManager.ThemeChanged -= OnThemeChanged;

            // Cancel any ongoing operations
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();

            // Dispose cancellation token source
            disposalCancellation?.Dispose();

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
