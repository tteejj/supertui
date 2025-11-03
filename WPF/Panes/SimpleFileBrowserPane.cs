using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// DEAD SIMPLE FILE BROWSER - NO BULLSHIT
    /// Just shows files and lets you pick them. That's it.
    /// </summary>
    public class SimpleFileBrowserPane : PaneBase
    {
        private readonly IConfigurationManager config;
        private readonly IEventBus eventBus;

        private ListBox fileList;
        private TextBlock pathDisplay;
        private string currentPath;

        public event EventHandler<string> FileSelected;

        public SimpleFileBrowserPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager config,
            IEventBus eventBus)
            : base(logger, themeManager, projectContext)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            PaneName = "Files";
            PaneIcon = "📁";

            // Start in Documents folder
            currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        public override void Initialize()
        {
            base.Initialize();
            LoadDirectory(currentPath);

            // CRITICAL: Ensure focus goes to file list, not ContentControl wrapper
            // Schedule focus after pane is fully loaded
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (fileList != null)
                {
                    Keyboard.Focus(fileList);
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Set the initial path for the file browser (can be called after creation)
        /// </summary>
        public void SetInitialPath(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                logger?.Log(LogLevel.Debug, "FileBrowser", $"SetInitialPath called: {path}");
                LoadDirectory(path);

                // Focus the file list after path change
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    if (fileList != null)
                    {
                        Keyboard.Focus(fileList);
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        protected override UIElement BuildContent()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Path display at top
            pathDisplay = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 14,
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Colors.White),
                Text = currentPath
            };
            Grid.SetRow(pathDisplay, 0);
            grid.Children.Add(pathDisplay);

            // File list
            fileList = new ListBox
            {
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 14,
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Focusable = true,
                IsTabStop = true
            };
            fileList.PreviewKeyDown += OnKeyDown;
            fileList.MouseDoubleClick += OnDoubleClick;

            // CRITICAL: Set focus when ListBox is loaded and ready
            fileList.Loaded += (s, e) =>
            {
                logger?.Log(LogLevel.Debug, "FileBrowser", "ListBox Loaded event - setting keyboard focus");
                // Must set SelectedIndex BEFORE setting focus for proper keyboard navigation
                if (fileList.Items.Count > 0 && fileList.SelectedIndex < 0)
                {
                    fileList.SelectedIndex = 0;
                }
                Keyboard.Focus(fileList);
                fileList.Focus(); // Also call WPF Focus() for good measure
                logger?.Log(LogLevel.Debug, "FileBrowser", $"Focus set. IsKeyboardFocused: {fileList.IsKeyboardFocused}, SelectedIndex: {fileList.SelectedIndex}");
            };

            Grid.SetRow(fileList, 1);
            grid.Children.Add(fileList);

            logger?.Log(LogLevel.Debug, "FileBrowser", "BuildContent: ListBox created and configured");
            return grid;
        }

        private void LoadDirectory(string path)
        {
            try
            {
                currentPath = path;
                pathDisplay.Text = currentPath;
                fileList.Items.Clear();

                // Add parent directory if not at root
                var parent = Directory.GetParent(path);
                if (parent != null)
                {
                    fileList.Items.Add(new FileItem
                    {
                        Name = "..",
                        FullPath = parent.FullName,
                        IsDirectory = true
                    });
                }

                // Add directories
                foreach (var dir in Directory.GetDirectories(path).OrderBy(d => Path.GetFileName(d)))
                {
                    try
                    {
                        fileList.Items.Add(new FileItem
                        {
                            Name = "📁 " + Path.GetFileName(dir),
                            FullPath = dir,
                            IsDirectory = true
                        });
                    }
                    catch { /* Skip inaccessible */ }
                }

                // Add files
                foreach (var file in Directory.GetFiles(path).OrderBy(f => Path.GetFileName(f)))
                {
                    try
                    {
                        fileList.Items.Add(new FileItem
                        {
                            Name = "📄 " + Path.GetFileName(file),
                            FullPath = file,
                            IsDirectory = false
                        });
                    }
                    catch { /* Skip inaccessible */ }
                }

                // Select first item for immediate keyboard navigation
                if (fileList.Items.Count > 0)
                {
                    fileList.SelectedIndex = 0;
                }

                logger?.Log(LogLevel.Info, PaneName, $"Loaded {fileList.Items.Count} items from {path}");
            }
            catch (Exception ex)
            {
                logger?.Log(LogLevel.Error, PaneName, $"Error loading directory: {ex.Message}");
                MessageBox.Show($"Cannot access: {path}\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HandleSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Back || e.Key == Key.Left)
            {
                // Go up one directory
                var parent = Directory.GetParent(currentPath);
                if (parent != null)
                {
                    LoadDirectory(parent.FullName);
                }
                e.Handled = true;
            }
        }

        private void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            HandleSelection();
        }

        private void HandleSelection()
        {
            if (fileList.SelectedItem is FileItem item)
            {
                if (item.IsDirectory)
                {
                    // Navigate into directory
                    LoadDirectory(item.FullPath);
                }
                else
                {
                    // File selected - publish to EventBus so other panes can react
                    logger?.Log(LogLevel.Info, PaneName, $"File selected: {item.FullPath}");
                    FileSelected?.Invoke(this, item.FullPath);

                    // Also publish via EventBus for cross-pane communication
                    logger?.Log(LogLevel.Debug, PaneName, $"Publishing FileSelectedEvent to EventBus for: {item.FullPath}");
                    var evt = new Core.Events.FileSelectedEvent
                    {
                        FilePath = item.FullPath,
                        FileName = Path.GetFileName(item.FullPath),
                        FileSize = new FileInfo(item.FullPath).Length,
                        SelectedAt = DateTime.Now
                    };
                    eventBus?.Publish(evt);
                    logger?.Log(LogLevel.Debug, PaneName, $"FileSelectedEvent published. EventBus has subscribers: {eventBus?.HasSubscribers<Core.Events.FileSelectedEvent>()}");
                }
            }
        }

        protected override void OnDispose()
        {
            FileSelected = null;
            base.OnDispose();
        }

        private class FileItem
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public bool IsDirectory { get; set; }

            public override string ToString() => Name;
        }
    }
}
