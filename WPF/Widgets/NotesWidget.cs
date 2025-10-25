// NotesWidget.cs - Simple text editor with file operations
// Supports multiple .txt files with tab management, atomic saves, and auto-save on exit

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
using SuperTUI.Core.Dialogs;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Text editor widget for .txt files.
    /// Features: Multiple files (tabs), atomic saves, auto-save on exit, 5MB limit.
    /// </summary>
    public class NotesWidget : WidgetBase, IThemeable
    {
        private const int MaxOpenFiles = 5;
        private const long MaxFileSize = 5 * 1024 * 1024;  // 5 MB

        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;

        private StandardWidgetFrame frame;
        private Border containerBorder;
        private StackPanel tabPanel;
        private Grid contentGrid;
        private TextBlock statusLabel;
        private TextBlock helpLabel;

        private List<NoteFile> openFiles;
        private int activeFileIndex;

        /// <summary>
        /// Represents an open note file
        /// </summary>
        private class NoteFile
        {
            public string FilePath { get; set; }      // null if unsaved
            public string FileName { get; set; }      // Display name
            public TextBox TextBox { get; set; }      // UI element
            public Button TabButton { get; set; }     // Tab button
            public bool IsDirty { get; set; }         // Modified since save

            public string Content
            {
                get => TextBox?.Text ?? "";
                set { if (TextBox != null) TextBox.Text = value; }
            }
        }

        /// <summary>
        /// DI constructor - preferred for new code
        /// </summary>
        public NotesWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            WidgetType = "Notes";
            openFiles = new List<NoteFile>();
            activeFileIndex = -1;

            BuildUI();
            CreateNewFile();  // Start with one untitled file
        }

        /// <summary>
        /// Parameterless constructor for backward compatibility
        /// </summary>
        public NotesWidget()
            : this(Logger.Instance, ThemeManager.Instance, ConfigurationManager.Instance)
        {
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            // Create standard frame
            frame = new StandardWidgetFrame(themeManager)
            {
                Title = "NOTES"
            };
            frame.SetStandardShortcuts("Ctrl+N: New", "Ctrl+O: Open", "Ctrl+S: Save", "Ctrl+W: Close", "?: Help");

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Padding = new Thickness(10)
            };

            contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Tabs
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // Editor
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Status

            // Tab panel
            tabPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(tabPanel, 0);
            contentGrid.Children.Add(tabPanel);

            // Editor area (will contain active TextBox)
            Grid.SetRow(new Border(), 1);  // Placeholder

            // Status bar
            statusLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(statusLabel, 2);
            contentGrid.Children.Add(statusLabel);

            containerBorder.Child = contentGrid;
            frame.Content = containerBorder;
            Content = frame;

            // Register keyboard shortcuts
            KeyDown += NotesWidget_KeyDown;
        }

        private void NotesWidget_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+N - New file
            if (e.Key == Key.N && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                CreateNewFile();
                e.Handled = true;
            }
            // Ctrl+O - Open file
            else if (e.Key == Key.O && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                OpenFile();
                e.Handled = true;
            }
            // Ctrl+S - Save file
            else if (e.Key == Key.S && e.KeyboardDevice.Modifiers == ModifierKeys.Control &&
                     e.KeyboardDevice.Modifiers != ModifierKeys.Shift)
            {
                SaveFile();
                e.Handled = true;
            }
            // Ctrl+Shift+S - Save As
            else if (e.Key == Key.S &&
                     e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                SaveFileAs();
                e.Handled = true;
            }
            // Ctrl+W - Close tab
            else if (e.Key == Key.W && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                CloseFile(activeFileIndex);
                e.Handled = true;
            }
            // Ctrl+Tab - Next tab
            else if (e.Key == Key.Tab && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                SwitchToNextTab();
                e.Handled = true;
            }
        }

        private void CreateNewFile()
        {
            if (openFiles.Count >= MaxOpenFiles)
            {
                ShowToast($"Maximum {MaxOpenFiles} files open", themeManager.CurrentTheme.Warning);
                return;
            }

            var theme = themeManager.CurrentTheme;
            var fileName = $"untitled-{openFiles.Count + 1}.txt";

            var textBox = new TextBox
            {
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            textBox.TextChanged += (s, e) => MarkDirty();
            textBox.SelectionChanged += UpdateStatusBar;

            var tabButton = CreateTabButton(fileName);

            var noteFile = new NoteFile
            {
                FilePath = null,
                FileName = fileName,
                TextBox = textBox,
                TabButton = tabButton,
                IsDirty = false
            };

            openFiles.Add(noteFile);
            tabPanel.Children.Add(tabButton);

            SwitchToFile(openFiles.Count - 1);
            logger.Info("NotesWidget", $"Created new file: {fileName}");
        }

        private Button CreateTabButton(string fileName)
        {
            var theme = themeManager.CurrentTheme;

            var button = new Button
            {
                Content = fileName,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 5, 0),
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1)
            };

            button.Click += (s, e) =>
            {
                var index = openFiles.FindIndex(f => f.TabButton == button);
                if (index >= 0)
                    SwitchToFile(index);
            };

            return button;
        }

        private void SwitchToFile(int index)
        {
            if (index < 0 || index >= openFiles.Count)
                return;

            var theme = themeManager.CurrentTheme;

            // Update active index
            activeFileIndex = index;

            // Update tab button styles
            for (int i = 0; i < openFiles.Count; i++)
            {
                var isActive = i == activeFileIndex;
                openFiles[i].TabButton.Background = new SolidColorBrush(
                    isActive ? theme.Primary : theme.Surface
                );
                openFiles[i].TabButton.Foreground = new SolidColorBrush(
                    isActive ? theme.Background : theme.Foreground
                );
            }

            // Switch editor content
            var activeFile = openFiles[activeFileIndex];
            if (contentGrid.Children.Count > 1)
            {
                contentGrid.Children.RemoveAt(1);  // Remove old editor
            }

            Grid.SetRow(activeFile.TextBox, 1);
            contentGrid.Children.Insert(1, activeFile.TextBox);

            activeFile.TextBox.Focus();
            UpdateStatusBar(null, null);

            logger.Debug("NotesWidget", $"Switched to file: {activeFile.FileName}");
        }

        private void SwitchToNextTab()
        {
            if (openFiles.Count == 0)
                return;

            var nextIndex = (activeFileIndex + 1) % openFiles.Count;
            SwitchToFile(nextIndex);
        }

        private void OpenFile()
        {
            if (openFiles.Count >= MaxOpenFiles)
            {
                ShowToast($"Maximum {MaxOpenFiles} files open", themeManager.CurrentTheme.Warning);
                return;
            }

            var picker = new FilePickerDialog(
                FilePickerDialog.PickerMode.Open,
                extensionFilter: ".txt",
                logger: logger,
                themeManager: themeManager
            );

            if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedPath))
            {
                LoadFile(picker.SelectedPath);
            }
        }

        private void LoadFile(string filePath)
        {
            try
            {
                // Check if already open
                var existingIndex = openFiles.FindIndex(f => f.FilePath == filePath);
                if (existingIndex >= 0)
                {
                    SwitchToFile(existingIndex);
                    ShowToast("File already open", themeManager.CurrentTheme.Info);
                    return;
                }

                // Check file size (5MB limit)
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > MaxFileSize)
                {
                    ShowToast($"File too large (max {MaxFileSize / 1024 / 1024} MB)", themeManager.CurrentTheme.Error);
                    logger.Warning("NotesWidget", $"File too large: {filePath} ({fileInfo.Length} bytes)");
                    return;
                }

                // Check if at max files
                if (openFiles.Count >= MaxOpenFiles)
                {
                    ShowToast($"Maximum {MaxOpenFiles} files open", themeManager.CurrentTheme.Warning);
                    return;
                }

                // Read file
                var content = File.ReadAllText(filePath);
                var fileName = Path.GetFileName(filePath);

                var theme = themeManager.CurrentTheme;

                var textBox = new TextBox
                {
                    Text = content,
                    Background = new SolidColorBrush(theme.Surface),
                    Foreground = new SolidColorBrush(theme.Foreground),
                    BorderBrush = new SolidColorBrush(theme.Border),
                    BorderThickness = new Thickness(1),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    Padding = new Thickness(8),
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    AcceptsTab = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                textBox.TextChanged += (s, e) => MarkDirty();
                textBox.SelectionChanged += UpdateStatusBar;

                var tabButton = CreateTabButton(fileName);

                var noteFile = new NoteFile
                {
                    FilePath = filePath,
                    FileName = fileName,
                    TextBox = textBox,
                    TabButton = tabButton,
                    IsDirty = false
                };

                openFiles.Add(noteFile);
                tabPanel.Children.Add(tabButton);
                SwitchToFile(openFiles.Count - 1);

                logger.Info("NotesWidget", $"Opened file: {filePath}");
            }
            catch (Exception ex)
            {
                ShowToast($"Error opening file: {ex.Message}", themeManager.CurrentTheme.Error);
                logger.Error("NotesWidget", $"Error opening file: {ex.Message}");
            }
        }

        private void SaveFile()
        {
            if (activeFileIndex < 0 || activeFileIndex >= openFiles.Count)
                return;

            var noteFile = openFiles[activeFileIndex];

            if (noteFile.FilePath == null)
            {
                // New file, need Save As
                SaveFileAs();
                return;
            }

            try
            {
                AtomicSave(noteFile.FilePath, noteFile.Content);
                noteFile.IsDirty = false;
                UpdateTabLabel();
                UpdateStatusBar(null, null);
                ShowToast("Saved", themeManager.CurrentTheme.Success);
                logger.Info("NotesWidget", $"Saved file: {noteFile.FilePath}");
            }
            catch (Exception ex)
            {
                ShowToast($"Error saving: {ex.Message}", themeManager.CurrentTheme.Error);
                logger.Error("NotesWidget", $"Error saving file: {ex.Message}");
            }
        }

        private void SaveFileAs()
        {
            if (activeFileIndex < 0 || activeFileIndex >= openFiles.Count)
                return;

            var noteFile = openFiles[activeFileIndex];

            var picker = new FilePickerDialog(
                FilePickerDialog.PickerMode.Save,
                extensionFilter: ".txt",
                logger: logger,
                themeManager: themeManager
            );

            if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedPath))
            {
                try
                {
                    AtomicSave(picker.SelectedPath, noteFile.Content);
                    noteFile.FilePath = picker.SelectedPath;
                    noteFile.FileName = Path.GetFileName(picker.SelectedPath);
                    noteFile.IsDirty = false;
                    UpdateTabLabel();
                    UpdateStatusBar(null, null);
                    ShowToast("Saved", themeManager.CurrentTheme.Success);
                    logger.Info("NotesWidget", $"Saved file as: {picker.SelectedPath}");
                }
                catch (Exception ex)
                {
                    ShowToast($"Error saving: {ex.Message}", themeManager.CurrentTheme.Error);
                    logger.Error("NotesWidget", $"Error saving file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Atomic save: Write to temp file, then rename (crash-safe)
        /// </summary>
        private void AtomicSave(string path, string content)
        {
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, path, overwrite: true);
        }

        private void CloseFile(int index)
        {
            if (index < 0 || index >= openFiles.Count)
                return;

            var noteFile = openFiles[index];

            // Note: No unsaved warning - auto-save on exit handles this
            tabPanel.Children.Remove(noteFile.TabButton);
            openFiles.RemoveAt(index);

            // Adjust active index
            if (openFiles.Count == 0)
            {
                CreateNewFile();  // Always have at least one file
            }
            else if (activeFileIndex >= openFiles.Count)
            {
                SwitchToFile(openFiles.Count - 1);
            }
            else
            {
                SwitchToFile(Math.Max(0, activeFileIndex));
            }

            logger.Info("NotesWidget", $"Closed file: {noteFile.FileName}");
        }

        private void MarkDirty()
        {
            if (activeFileIndex >= 0 && activeFileIndex < openFiles.Count)
            {
                openFiles[activeFileIndex].IsDirty = true;
                UpdateTabLabel();
                UpdateStatusBar(null, null);
            }
        }

        private void UpdateTabLabel()
        {
            if (activeFileIndex < 0 || activeFileIndex >= openFiles.Count)
                return;

            var noteFile = openFiles[activeFileIndex];
            var label = noteFile.FileName;
            if (noteFile.IsDirty)
                label += "*";

            noteFile.TabButton.Content = label;
        }

        private void UpdateStatusBar(object sender, RoutedEventArgs e)
        {
            if (activeFileIndex < 0 || activeFileIndex >= openFiles.Count)
                return;

            var noteFile = openFiles[activeFileIndex];
            var textBox = noteFile.TextBox;

            // Get cursor position
            var lineIndex = textBox.GetLineIndexFromCharacterIndex(textBox.CaretIndex);
            var colIndex = textBox.CaretIndex - textBox.GetCharacterIndexFromLineIndex(lineIndex);
            var charCount = textBox.Text.Length;

            var status = $"{noteFile.FileName}";
            if (noteFile.FilePath != null)
                status += $" | {noteFile.FilePath}";
            status += $" | Ln {lineIndex + 1}, Col {colIndex + 1} | {charCount} chars";
            if (noteFile.IsDirty)
                status += " | Modified";

            statusLabel.Text = status;
        }

        private void ShowToast(string message, System.Windows.Media.Color color)
        {
            // Simple status bar toast (could be enhanced with actual toast notification later)
            var originalText = statusLabel.Text;
            var originalColor = statusLabel.Foreground;

            statusLabel.Text = message;
            statusLabel.Foreground = new SolidColorBrush(color);

            // Restore after 2 seconds
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, e) =>
            {
                statusLabel.Text = originalText;
                statusLabel.Foreground = originalColor;
                timer.Stop();
            };
            timer.Start();
        }

        public override void Initialize()
        {
            // Already initialized in constructor
        }

        public override void OnWidgetFocusReceived()
        {
            // Focus active textbox
            if (activeFileIndex >= 0 && activeFileIndex < openFiles.Count)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    openFiles[activeFileIndex].TextBox.Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        public override Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();

            // Save list of open file paths (not content, to avoid bloat)
            var filePaths = openFiles
                .Where(f => f.FilePath != null)
                .Select(f => f.FilePath)
                .ToList();

            state["OpenFiles"] = filePaths;
            state["ActiveFileIndex"] = activeFileIndex;

            return state;
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state.TryGetValue("OpenFiles", out var openFilesObj))
            {
                // Clear current files
                tabPanel.Children.Clear();
                openFiles.Clear();
                activeFileIndex = -1;

                // Restore files
                var filePaths = openFilesObj as List<string>;
                if (filePaths != null && filePaths.Count > 0)
                {
                    foreach (var filePath in filePaths)
                    {
                        if (File.Exists(filePath))
                        {
                            LoadFile(filePath);
                        }
                    }

                    // Restore active index
                    if (state.TryGetValue("ActiveFileIndex", out var activeIndexObj))
                    {
                        var activeIndex = Convert.ToInt32(activeIndexObj);
                        if (activeIndex >= 0 && activeIndex < openFiles.Count)
                        {
                            SwitchToFile(activeIndex);
                        }
                    }
                }
                else
                {
                    CreateNewFile();  // Fallback to new file
                }
            }
        }

        protected override void OnDispose()
        {
            // AUTO-SAVE ON EXIT - Save all dirty files
            foreach (var noteFile in openFiles)
            {
                if (noteFile.IsDirty && noteFile.FilePath != null)
                {
                    try
                    {
                        AtomicSave(noteFile.FilePath, noteFile.Content);
                        logger.Info("NotesWidget", $"Auto-saved on exit: {noteFile.FilePath}");
                    }
                    catch (Exception ex)
                    {
                        logger.Error("NotesWidget", $"Error auto-saving on exit: {ex.Message}");
                    }
                }
            }

            KeyDown -= NotesWidget_KeyDown;
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            if (frame != null)
            {
                frame.ApplyTheme();
            }

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
            }

            if (statusLabel != null)
            {
                statusLabel.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            }

            // Update all open file textboxes
            foreach (var noteFile in openFiles)
            {
                if (noteFile.TextBox != null)
                {
                    noteFile.TextBox.Background = new SolidColorBrush(theme.Surface);
                    noteFile.TextBox.Foreground = new SolidColorBrush(theme.Foreground);
                    noteFile.TextBox.BorderBrush = new SolidColorBrush(theme.Border);
                }

                if (noteFile.TabButton != null)
                {
                    var isActive = openFiles.IndexOf(noteFile) == activeFileIndex;
                    noteFile.TabButton.Background = new SolidColorBrush(
                        isActive ? theme.Primary : theme.Surface
                    );
                    noteFile.TabButton.Foreground = new SolidColorBrush(
                        isActive ? theme.Background : theme.Foreground
                    );
                    noteFile.TabButton.BorderBrush = new SolidColorBrush(theme.Border);
                }
            }
        }
    }
}
