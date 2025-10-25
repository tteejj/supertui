// FilePickerDialog.cs - Modal file/folder picker for Open/Save operations
// Terminal-styled, keyboard-driven file navigation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Dialogs
{
    /// <summary>
    /// Modal dialog for selecting files or folders.
    /// Used by NotesWidget and other widgets for Open/Save operations.
    /// Terminal aesthetic with keyboard navigation.
    /// </summary>
    public class FilePickerDialog : Window
    {
        public enum PickerMode
        {
            Open,           // Select existing file
            Save,           // Select location and optionally enter filename
            SelectFolder    // Select directory only
        }

        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly PickerMode mode;
        private readonly string extensionFilter;  // e.g., ".txt" (null = all files)

        private string currentPath;
        private ListBox itemsListBox;
        private TextBlock pathLabel;
        private TextBlock statusLabel;
        private TextBox fileNameTextBox;  // For Save mode
        private Button newFolderButton;
        private List<FileSystemInfo> currentItems;

        public string SelectedPath { get; private set; }

        public FilePickerDialog(
            PickerMode mode,
            string extensionFilter = null,
            string initialPath = null,
            ILogger logger = null,
            IThemeManager themeManager = null)
        {
            this.mode = mode;
            this.extensionFilter = extensionFilter;
            this.logger = logger ?? Logger.Instance;
            this.themeManager = themeManager ?? ThemeManager.Instance;

            currentPath = initialPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            currentItems = new List<FileSystemInfo>();

            InitializeWindow();
            BuildUI();
            LoadDirectory(currentPath);
        }

        private void InitializeWindow()
        {
            var theme = themeManager.CurrentTheme;

            Title = mode switch
            {
                PickerMode.Open => "Open File",
                PickerMode.Save => "Save File",
                PickerMode.SelectFolder => "Select Folder",
                _ => "File Picker"
            };

            Width = 700;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(theme.Background);
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Path
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // List
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Filename (Save mode)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Status
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Buttons

            // Path label
            pathLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Info),
                Margin = new Thickness(10, 10, 10, 5),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(pathLabel, 0);
            mainGrid.Children.Add(pathLabel);

            // Items list
            itemsListBox = new ListBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            itemsListBox.KeyDown += ItemsListBox_KeyDown;
            itemsListBox.MouseDoubleClick += ItemsListBox_MouseDoubleClick;
            Grid.SetRow(itemsListBox, 1);
            mainGrid.Children.Add(itemsListBox);

            // Filename input (Save mode only)
            if (mode == PickerMode.Save)
            {
                var fileNamePanel = new DockPanel
                {
                    Margin = new Thickness(10, 5, 10, 5)
                };

                var fileNameLabel = new TextBlock
                {
                    Text = "File name:",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(theme.Foreground),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                DockPanel.SetDock(fileNameLabel, Dock.Left);
                fileNamePanel.Children.Add(fileNameLabel);

                fileNameTextBox = new TextBox
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Background = new SolidColorBrush(theme.Surface),
                    Foreground = new SolidColorBrush(theme.Foreground),
                    BorderBrush = new SolidColorBrush(theme.Border),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(5)
                };
                fileNameTextBox.KeyDown += FileNameTextBox_KeyDown;
                fileNamePanel.Children.Add(fileNameTextBox);

                Grid.SetRow(fileNamePanel, 2);
                mainGrid.Children.Add(fileNamePanel);
            }

            // Status label
            statusLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(10, 5, 10, 5)
            };
            Grid.SetRow(statusLabel, 3);
            mainGrid.Children.Add(statusLabel);

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10, 5, 10, 10)
            };

            // New Folder button
            newFolderButton = new Button
            {
                Content = "New Folder",
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border)
            };
            newFolderButton.Click += NewFolderButton_Click;
            buttonPanel.Children.Add(newFolderButton);

            // Select button
            var selectButton = new Button
            {
                Content = mode == PickerMode.Save ? "Save" : "Select",
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                IsDefault = true
            };
            selectButton.Click += SelectButton_Click;
            buttonPanel.Children.Add(selectButton);

            // Cancel button
            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 100,
                Height = 30,
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                IsCancel = true
            };
            cancelButton.Click += CancelButton_Click;
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 4);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;

            // Focus the list on load
            Loaded += (s, e) => itemsListBox.Focus();
        }

        private void LoadDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    UpdateStatus($"Directory not found: {path}", themeManager.CurrentTheme.Error);
                    return;
                }

                currentPath = path;
                pathLabel.Text = $"Path: {currentPath}";
                currentItems.Clear();
                itemsListBox.Items.Clear();

                var dirInfo = new DirectoryInfo(currentPath);

                // Add parent directory if not root
                if (dirInfo.Parent != null)
                {
                    currentItems.Add(dirInfo.Parent);
                    itemsListBox.Items.Add(FormatItem(dirInfo.Parent, true));
                }

                // Add directories
                var dirs = dirInfo.GetDirectories()
                    .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden))
                    .OrderBy(d => d.Name)
                    .ToList();

                foreach (var dir in dirs)
                {
                    currentItems.Add(dir);
                    itemsListBox.Items.Add(FormatItem(dir, false));
                }

                // Add files (with extension filter if specified)
                var files = dirInfo.GetFiles()
                    .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                    .Where(f => extensionFilter == null || f.Extension.Equals(extensionFilter, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.Name)
                    .ToList();

                // Only show files in Open mode (not in SelectFolder mode)
                if (mode != PickerMode.SelectFolder)
                {
                    foreach (var file in files)
                    {
                        currentItems.Add(file);
                        itemsListBox.Items.Add(FormatItem(file, false));
                    }
                }

                UpdateStatus($"{dirs.Count} folders, {files.Count} files", themeManager.CurrentTheme.ForegroundSecondary);

                logger.Debug("FilePickerDialog", $"Loaded directory: {currentPath}");
            }
            catch (UnauthorizedAccessException)
            {
                UpdateStatus("Access denied", themeManager.CurrentTheme.Error);
                logger.Error("FilePickerDialog", $"Access denied: {path}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", themeManager.CurrentTheme.Error);
                logger.Error("FilePickerDialog", $"Error loading directory: {ex.Message}");
            }
        }

        private string FormatItem(FileSystemInfo item, bool isParent)
        {
            if (isParent)
                return "/ ..";

            if (item is DirectoryInfo dir)
            {
                var modified = dir.LastWriteTime.ToString("yyyy-MM-dd");
                return $"/ {dir.Name,-50} <DIR>  {modified}";
            }

            if (item is FileInfo file)
            {
                var size = FormatFileSize(file.Length);
                var modified = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                return $"- {file.Name,-50} {size,8}   {modified}";
            }

            return item.Name;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }

        private void ItemsListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HandleSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                NavigateUp();
                e.Handled = true;
            }
        }

        private void ItemsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            HandleSelection();
        }

        private void FileNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SelectButton_Click(null, null);
                e.Handled = true;
            }
        }

        private void HandleSelection()
        {
            if (itemsListBox.SelectedIndex < 0 || itemsListBox.SelectedIndex >= currentItems.Count)
                return;

            var item = currentItems[itemsListBox.SelectedIndex];

            // Navigate into directory
            if (item is DirectoryInfo dir)
            {
                LoadDirectory(dir.FullName);
            }
            // Select file (Open mode)
            else if (item is FileInfo file && mode == PickerMode.Open)
            {
                SelectedPath = file.FullName;
                DialogResult = true;
                Close();
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

        private void NewFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TextInputDialog(
                "New Folder",
                "Enter folder name:",
                "New Folder",
                logger,
                themeManager);

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                try
                {
                    var newFolderPath = Path.Combine(currentPath, dialog.InputText);
                    Directory.CreateDirectory(newFolderPath);
                    LoadDirectory(currentPath);  // Refresh
                    logger.Info("FilePickerDialog", $"Created folder: {newFolderPath}");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Cannot create folder: {ex.Message}", themeManager.CurrentTheme.Error);
                    logger.Error("FilePickerDialog", $"Error creating folder: {ex.Message}");
                }
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (mode == PickerMode.SelectFolder)
            {
                // Select current directory
                SelectedPath = currentPath;
                DialogResult = true;
                Close();
            }
            else if (mode == PickerMode.Save)
            {
                // Get filename from textbox
                var fileName = fileNameTextBox?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    UpdateStatus("Please enter a file name", themeManager.CurrentTheme.Error);
                    return;
                }

                // Add extension if not present
                if (extensionFilter != null && !fileName.EndsWith(extensionFilter, StringComparison.OrdinalIgnoreCase))
                {
                    fileName += extensionFilter;
                }

                SelectedPath = Path.Combine(currentPath, fileName);
                DialogResult = true;
                Close();
            }
            else if (mode == PickerMode.Open)
            {
                // Must have file selected
                if (itemsListBox.SelectedIndex < 0 || itemsListBox.SelectedIndex >= currentItems.Count)
                {
                    UpdateStatus("Please select a file", themeManager.CurrentTheme.Error);
                    return;
                }

                var item = currentItems[itemsListBox.SelectedIndex];
                if (item is FileInfo file)
                {
                    SelectedPath = file.FullName;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    UpdateStatus("Please select a file, not a folder", themeManager.CurrentTheme.Error);
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateStatus(string message, System.Windows.Media.Color color)
        {
            statusLabel.Text = message;
            statusLabel.Foreground = new SolidColorBrush(color);
        }
    }

    /// <summary>
    /// Simple text input dialog for New Folder prompt
    /// </summary>
    internal class TextInputDialog : Window
    {
        public string InputText { get; private set; }

        public TextInputDialog(
            string title,
            string prompt,
            string defaultValue,
            ILogger logger,
            IThemeManager themeManager)
        {
            var theme = themeManager.CurrentTheme;

            Title = title;
            Width = 400;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(theme.Background);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Prompt
            var promptLabel = new TextBlock
            {
                Text = prompt,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(10, 10, 10, 5)
            };
            Grid.SetRow(promptLabel, 0);
            grid.Children.Add(promptLabel);

            // Input
            var inputBox = new TextBox
            {
                Text = defaultValue,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                Margin = new Thickness(10, 5, 10, 10)
            };
            inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    InputText = inputBox.Text;
                    DialogResult = true;
                    Close();
                }
            };
            Grid.SetRow(inputBox, 1);
            grid.Children.Add(inputBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10, 5, 10, 10)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                IsDefault = true
            };
            okButton.Click += (s, e) =>
            {
                InputText = inputBox.Text;
                DialogResult = true;
                Close();
            };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                IsCancel = true
            };
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;

            Loaded += (s, e) =>
            {
                inputBox.Focus();
                inputBox.SelectAll();
            };
        }
    }
}
