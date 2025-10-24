using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Events;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// File explorer widget for navigating directories and viewing files.
    /// Supports navigation, file selection, and opening files.
    /// </summary>
    public class FileExplorerWidget : WidgetBase, IThemeable
    {
        private string currentPath;
        private ListBox fileListBox;
        private TextBlock pathLabel;
        private TextBlock statusLabel;
        private List<FileSystemInfo> currentItems;
        private Theme theme;

        public FileExplorerWidget(string initialPath = null)
        {
            Name = "File Explorer";
            currentPath = initialPath ?? Directory.GetCurrentDirectory();
            currentItems = new List<FileSystemInfo>();
        }

        public override void Initialize()
        {
            theme = ThemeManager.Instance.CurrentTheme;
            BuildUI();
            LoadDirectory(currentPath);
        }

        private void BuildUI()
        {
            var mainPanel = new DockPanel
            {
                Background = new SolidColorBrush(theme.Background),
                LastChildFill = true
            };

            // Title
            var title = new TextBlock
            {
                Text = "FILE EXPLORER",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(title, Dock.Top);
            mainPanel.Children.Add(title);

            // Path label
            pathLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.Info),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            DockPanel.SetDock(pathLabel, Dock.Top);
            mainPanel.Children.Add(pathLabel);

            // Status bar
            statusLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 5, 0, 0)
            };
            DockPanel.SetDock(statusLabel, Dock.Bottom);
            mainPanel.Children.Add(statusLabel);

            // File list
            fileListBox = new ListBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            fileListBox.KeyDown += FileListBox_KeyDown;
            fileListBox.MouseDoubleClick += FileListBox_MouseDoubleClick;

            mainPanel.Children.Add(fileListBox);
            Content = mainPanel;
        }

        private void LoadDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    UpdateStatus($"Directory not found: {path}", theme.Error);
                    return;
                }

                currentPath = path;
                pathLabel.Text = $"Path: {currentPath}";
                currentItems.Clear();
                fileListBox.Items.Clear();

                var dirInfo = new DirectoryInfo(currentPath);

                // Add parent directory if not root
                if (dirInfo.Parent != null)
                {
                    currentItems.Add(dirInfo.Parent);
                    fileListBox.Items.Add(FormatItem(dirInfo.Parent, true));
                }

                // Add directories
                var dirs = dirInfo.GetDirectories().OrderBy(d => d.Name).ToList();
                foreach (var dir in dirs)
                {
                    if (!dir.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        currentItems.Add(dir);
                        fileListBox.Items.Add(FormatItem(dir, false));
                    }
                }

                // Add files
                var files = dirInfo.GetFiles().OrderBy(f => f.Name).ToList();
                foreach (var file in files)
                {
                    if (!file.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        currentItems.Add(file);
                        fileListBox.Items.Add(FormatItem(file, false));
                    }
                }

                UpdateStatus($"{dirs.Count} directories, {files.Count} files", theme.ForegroundSecondary);

                // Publish directory changed event
                EventBus.Instance.Publish(new DirectoryChangedEvent
                {
                    OldPath = null,
                    NewPath = currentPath,
                    ChangedAt = DateTime.Now
                });

                Logger.Instance.Info("FileExplorer", $"Loaded directory: {currentPath}");
            }
            catch (UnauthorizedAccessException)
            {
                UpdateStatus("Access denied", theme.Error);
                Logger.Instance.Error("FileExplorer", $"Access denied: {path}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", theme.Error);
                Logger.Instance.Error("FileExplorer", $"Failed to load directory: {ex.Message}");
            }
        }

        private string FormatItem(FileSystemInfo item, bool isParent)
        {
            if (isParent)
                return "📁 ..";

            if (item is DirectoryInfo)
                return $"📁 {item.Name}";

            var file = item as FileInfo;
            var size = FormatFileSize(file.Length);
            var icon = GetFileIcon(file.Extension);
            return $"{icon} {file.Name} ({size})";
        }

        private string GetFileIcon(string extension)
        {
            switch (extension.ToLower())
            {
                case ".txt": case ".md": case ".log": return "📄";
                case ".cs": case ".ps1": case ".psm1": case ".psd1": case ".js": case ".py": return "📝";
                case ".exe": case ".dll": case ".bin": return "⚙️";
                case ".zip": case ".rar": case ".7z": case ".tar": case ".gz": return "📦";
                case ".jpg": case ".jpeg": case ".png": case ".gif": case ".bmp": return "🖼️";
                case ".mp3": case ".wav": case ".flac": case ".ogg": return "🎵";
                case ".mp4": case ".avi": case ".mkv": case ".mov": return "🎬";
                case ".pdf": return "📕";
                case ".json": case ".xml": case ".yaml": case ".yml": case ".toml": return "🔧";
                default: return "📄";
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
            return $"{len:0.#} {sizes[order]}";
        }

        private void FileListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OpenSelectedItem();
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                NavigateUp();
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                LoadDirectory(currentPath);
                e.Handled = true;
            }
        }

        private void FileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedItem();
        }

        private void OpenSelectedItem()
        {
            if (fileListBox.SelectedIndex < 0 || fileListBox.SelectedIndex >= currentItems.Count)
                return;

            var item = currentItems[fileListBox.SelectedIndex];

            if (item is DirectoryInfo dir)
            {
                LoadDirectory(dir.FullName);
            }
            else if (item is FileInfo file)
            {
                // Publish file selected event
                EventBus.Instance.Publish(new FileSelectedEvent
                {
                    FilePath = file.FullName,
                    FileName = file.Name,
                    FileSize = file.Length,
                    SelectedAt = DateTime.Now
                });

                // Try to open file with default application
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = file.FullName,
                        UseShellExecute = true
                    });
                    UpdateStatus($"Opened: {file.Name}", theme.Success);
                    Logger.Instance.Info("FileExplorer", $"Opened file: {file.FullName}");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Cannot open: {ex.Message}", theme.Error);
                    Logger.Instance.Error("FileExplorer", $"Failed to open file: {ex.Message}");
                }
            }
        }

        private void NavigateUp()
        {
            var dirInfo = new DirectoryInfo(currentPath);
            if (dirInfo.Parent != null)
            {
                LoadDirectory(dirInfo.Parent.FullName);
            }
        }

        private void UpdateStatus(string message, Color color)
        {
            statusLabel.Text = message;
            statusLabel.Foreground = new SolidColorBrush(color);
        }

        public override Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["CurrentPath"] = currentPath
            };
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state.TryGetValue("CurrentPath", out var path))
            {
                LoadDirectory(path.ToString());
            }
        }

        protected override void OnDispose()
        {
            fileListBox.KeyDown -= FileListBox_KeyDown;
            fileListBox.MouseDoubleClick -= FileListBox_MouseDoubleClick;
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            theme = ThemeManager.Instance.CurrentTheme;
            BuildUI();
            LoadDirectory(currentPath);
        }
    }
}
