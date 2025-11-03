using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        private ListBox fileList;
        private TextBlock pathDisplay;
        private string currentPath;

        public event EventHandler<string> FileSelected;

        public SimpleFileBrowserPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager config)
            : base(logger, themeManager, projectContext)
        {
            this.config = config;
            PaneName = "Files";
            PaneIcon = "📁";

            // Start in Documents folder
            currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        public override void Initialize()
        {
            base.Initialize();
            LoadDirectory(currentPath);
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
                BorderThickness = new Thickness(0)
            };
            fileList.PreviewKeyDown += OnKeyDown;
            fileList.MouseDoubleClick += OnDoubleClick;
            Grid.SetRow(fileList, 1);
            grid.Children.Add(fileList);

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
                    // File selected
                    logger?.Log(LogLevel.Info, PaneName, $"File selected: {item.FullPath}");
                    FileSelected?.Invoke(this, item.FullPath);
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
