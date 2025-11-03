using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Excel import pane for COM-based and clipboard-based project import
    /// User can read directly from Excel via COM or paste clipboard data
    /// Uses ExcelMappingService with SVI-CAS profile
    /// </summary>
    public class ExcelImportPane : PaneBase
    {
        // Services
        private readonly IProjectService projectService;
        private readonly IExcelMappingService excelMappingService;
        private readonly IEventBus eventBus;
        private ExcelComReader excelComReader;

        // UI Components
        private Grid mainLayout;
        private TextBox clipboardTextBox;
        private TextBlock profileDisplay;
        private TextBox startCellBox;
        private TextBlock statusLabel;
        private TextBlock helpText;
        private TextBlock previewText;
        private Button readExcelButton;
        private Button selectFileButton;

        // State
        private List<ExcelMappingProfile> availableProfiles;
        private int currentProfileIndex = 0;

        // Theme colors
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush borderBrush;
        private SolidColorBrush dimBrush;
        private SolidColorBrush surfaceBrush;
        private SolidColorBrush successBrush;
        private SolidColorBrush errorBrush;

        public ExcelImportPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IProjectService projectService,
            IExcelMappingService excelMappingService,
            IEventBus eventBus)
            : base(logger, themeManager, projectContext)
        {
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.excelMappingService = excelMappingService ?? throw new ArgumentNullException(nameof(excelMappingService));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.excelComReader = new ExcelComReader(logger);
            PaneName = "Excel Import";
        }

        public override void Initialize()
        {
            base.Initialize();

            // Initialize ExcelMappingService
            excelMappingService.Initialize();

            // Register pane-specific shortcuts
            RegisterPaneShortcuts();

            // Subscribe to theme changes
            themeManager.ThemeChanged += OnThemeChanged;

            // Set initial focus
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (clipboardTextBox != null) System.Windows.Input.Keyboard.Focus(clipboardTextBox);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;
            shortcuts.RegisterForPane(PaneName, Key.I, ModifierKeys.None, () => ImportFromClipboard(), "Import from clipboard or Excel");
            shortcuts.RegisterForPane(PaneName, Key.P, ModifierKeys.None, () => CycleProfile(), "Cycle mapping profile");
            shortcuts.RegisterForPane(PaneName, Key.R, ModifierKeys.Control, () => ReadFromRunningExcel(), "Read from running Excel");
        }

        protected override UIElement BuildContent()
        {
            CacheThemeColors();

            mainLayout = new Grid();
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Instructions
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Button row (COM reading)
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Fallback label
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Clipboard input
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Preview
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status
            mainLayout.Background = bgBrush;
            mainLayout.PreviewKeyDown += MainLayout_PreviewKeyDown;
            mainLayout.Focusable = true;

            // Header
            var header = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Padding = new Thickness(16, 16, 16, 8),
                Text = "📊 Import from Excel"
            };
            Grid.SetRow(header, 0);
            mainLayout.Children.Add(header);

            // Instructions
            helpText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Padding = new Thickness(16, 0, 16, 12),
                TextWrapping = TextWrapping.Wrap,
                Text = "1. Open Excel audit request form (SVI-CAS)\n" +
                       "2. Click 'Read from Running Excel' (Ctrl+R) to read selected cells\n" +
                       "   OR click 'Open Excel File' to select .xlsx file\n" +
                       "   OR manually copy cells W3:W130 and paste into textbox below\n" +
                       "3. Press P to cycle profile (if needed)\n" +
                       "4. Press I to import"
            };
            Grid.SetRow(helpText, 1);
            mainLayout.Children.Add(helpText);

            // Controls panel
            var controlsPanel = BuildControlsPanel();
            Grid.SetRow(controlsPanel, 2);
            mainLayout.Children.Add(controlsPanel);

            // Button row for COM reading
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 0, 16, 12),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            readExcelButton = new Button
            {
                Content = "Read from Running Excel (Ctrl+R)",
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Background = surfaceBrush,
                Foreground = accentBrush,
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand
            };
            readExcelButton.Click += ReadExcelButton_Click;

            selectFileButton = new Button
            {
                Content = "Open Excel File...",
                Padding = new Thickness(12, 8, 12, 8),
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand
            };
            selectFileButton.Click += SelectFileButton_Click;

            buttonPanel.Children.Add(readExcelButton);
            buttonPanel.Children.Add(selectFileButton);
            Grid.SetRow(buttonPanel, 3);
            mainLayout.Children.Add(buttonPanel);

            // Fallback label
            var fallbackLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Padding = new Thickness(16, 0, 16, 8),
                Text = "OR paste clipboard data below:"
            };
            Grid.SetRow(fallbackLabel, 4);
            mainLayout.Children.Add(fallbackLabel);

            // Clipboard input box
            clipboardTextBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(16, 0, 16, 8),
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap
            };
            clipboardTextBox.TextChanged += ClipboardTextBox_TextChanged;
            Grid.SetRow(clipboardTextBox, 5);
            mainLayout.Children.Add(clipboardTextBox);

            // Preview
            previewText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = dimBrush,
                Padding = new Thickness(16, 8, 16, 8),
                Background = surfaceBrush,
                Margin = new Thickness(16, 0, 16, 8),
                TextWrapping = TextWrapping.Wrap,
                Text = "Preview: (paste data to see preview)"
            };
            Grid.SetRow(previewText, 6);
            mainLayout.Children.Add(previewText);

            // Status bar
            statusLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = fgBrush,
                Background = surfaceBrush,
                Padding = new Thickness(16, 8, 16, 8),
                Text = "Ready to import | Ctrl+R:Read from Excel | P:CycleProfile I:Import"
            };
            Grid.SetRow(statusLabel, 7);
            mainLayout.Children.Add(statusLabel);

            return mainLayout;
        }

        private Grid BuildControlsPanel()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Margin = new Thickness(16, 0, 16, 12);
            grid.Background = surfaceBrush;
            grid.Height = 40;

            // Profile label
            var profileLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Text = "Profile:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };
            Grid.SetColumn(profileLabel, 0);
            grid.Children.Add(profileLabel);

            // Profile display (terminal-style, keyboard cycling with P key)
            profileDisplay = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                Foreground = accentBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "[Loading...]"
            };
            LoadProfiles();
            Grid.SetColumn(profileDisplay, 1);
            grid.Children.Add(profileDisplay);

            // Start cell label
            var startCellLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Text = "Start Cell:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 8, 0)
            };
            Grid.SetColumn(startCellLabel, 2);
            grid.Children.Add(startCellLabel);

            // Start cell box
            startCellBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Background = bgBrush,
                Foreground = fgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 0, 8, 0),
                Text = "W3",
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(startCellBox, 3);
            grid.Children.Add(startCellBox);

            return grid;
        }

        private void CacheThemeColors()
        {
            var theme = themeManager.CurrentTheme;
            bgBrush = new SolidColorBrush(theme.Background);
            fgBrush = new SolidColorBrush(theme.Foreground);
            accentBrush = new SolidColorBrush(theme.Primary);
            borderBrush = new SolidColorBrush(theme.Border);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
            surfaceBrush = new SolidColorBrush(theme.Surface);
            successBrush = new SolidColorBrush(theme.Success);
            errorBrush = new SolidColorBrush(theme.Error);
        }

        private void LoadProfiles()
        {
            availableProfiles = excelMappingService.GetAllProfiles().ToList();

            // Find active profile index
            var activeProfile = excelMappingService.GetActiveProfile();
            if (activeProfile != null)
            {
                currentProfileIndex = availableProfiles.FindIndex(p => p.Id == activeProfile.Id);
                if (currentProfileIndex < 0) currentProfileIndex = 0;
            }
            else if (availableProfiles.Count > 0)
            {
                currentProfileIndex = 0;
            }

            UpdateProfileDisplay();
        }

        private void UpdateProfileDisplay()
        {
            if (availableProfiles == null || availableProfiles.Count == 0)
            {
                profileDisplay.Text = "[No profiles]";
                return;
            }

            var profile = availableProfiles[currentProfileIndex];
            profileDisplay.Text = $"{profile.Name} ({currentProfileIndex + 1}/{availableProfiles.Count})";
        }

        private void CycleProfile()
        {
            if (availableProfiles == null || availableProfiles.Count == 0) return;

            currentProfileIndex = (currentProfileIndex + 1) % availableProfiles.Count;
            UpdateProfileDisplay();
            logger.Log(LogLevel.Debug, "ExcelImport", $"Cycled to profile: {availableProfiles[currentProfileIndex].Name}");
        }

        private void MainLayout_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Block shortcuts when typing in textboxes
            bool isTypingInClipboard = clipboardTextBox != null && clipboardTextBox.IsFocused;
            bool isTypingInStartCell = startCellBox != null && startCellBox.IsFocused;

            if (isTypingInClipboard || isTypingInStartCell)
            {
                return; // Let text input work normally
            }

            // Dispatch to ShortcutManager
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
            {
                e.Handled = true;
            }
        }

        private void ReadExcelButton_Click(object sender, RoutedEventArgs e)
        {
            ReadFromRunningExcel();
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls",
                Title = "Select Excel Audit Request Form"
            };

            if (dialog.ShowDialog() == true)
            {
                ReadFromExcelFile(dialog.FileName);
            }
        }

        private void ReadFromRunningExcel()
        {
            UpdateStatus("Reading from Excel...", accentBrush);

            var startCell = startCellBox.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(startCell))
                startCell = "W3";

            var (success, cellData, errorMessage) = excelComReader.TryReadFromRunningExcel(startCell);

            if (success)
            {
                // Convert cellData to preview format
                var previewLines = cellData.Take(5).Select(kvp => $"{kvp.Key}: {kvp.Value}");
                var previewText = $"Read {cellData.Count} cells from Excel\n\n{string.Join("\n", previewLines)}\n...";

                // Store cellData in Tag for import
                clipboardTextBox.Tag = cellData;
                clipboardTextBox.Text = previewText;

                UpdatePreview(previewText);
                UpdateStatus($"✓ Read {cellData.Count} cells from Excel", successBrush);
            }
            else
            {
                UpdateStatus($"ERROR: {errorMessage}", errorBrush);
                MessageBox.Show(
                    $"Could not read from Excel:\n\n{errorMessage}\n\n" +
                    $"Please ensure Excel is running with cells selected, or use 'Open Excel File' button.",
                    "Excel Read Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ReadFromExcelFile(string filePath)
        {
            UpdateStatus("Reading Excel file...", accentBrush);

            var startCell = startCellBox.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(startCell))
                startCell = "W3";

            // Default SVI-CAS: 128 rows (W3:W130)
            int rowCount = 128;

            var (success, cellData, errorMessage) = excelComReader.ReadFromFile(filePath, startCell, rowCount);

            if (success)
            {
                var previewLines = cellData.Take(5).Select(kvp => $"{kvp.Key}: {kvp.Value}");
                var previewText = $"Read {cellData.Count} cells from {System.IO.Path.GetFileName(filePath)}\n\n{string.Join("\n", previewLines)}\n...";

                clipboardTextBox.Tag = cellData;
                clipboardTextBox.Text = previewText;

                UpdatePreview(previewText);
                UpdateStatus($"✓ Read {cellData.Count} cells from file", successBrush);
            }
            else
            {
                UpdateStatus($"ERROR: {errorMessage}", errorBrush);
                MessageBox.Show(
                    $"Could not read Excel file:\n\n{errorMessage}",
                    "Excel Read Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ClipboardTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // If Tag contains COM data, don't update preview on text change
            if (clipboardTextBox.Tag is Dictionary<string, string>)
            {
                return; // Preview already set by COM reader
            }
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            string data = clipboardTextBox.Text;
            if (string.IsNullOrWhiteSpace(data))
            {
                previewText.Text = "Preview: (paste data to see preview)";
                previewText.Foreground = dimBrush;
                return;
            }

            try
            {
                // Count lines
                var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                int lineCount = lines.Length;

                // Get first few fields as preview
                var preview = string.Join("\n", lines.Take(5).Select((line, idx) => $"Row {idx + 1}: {line}"));

                previewText.Text = $"Preview: {lineCount} rows detected\n{preview}\n...";
                previewText.Foreground = fgBrush;
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Internal,
                    ex,
                    "Generating preview of clipboard data",
                    logger);

                previewText.Text = $"Preview error: {ex.Message}";
                previewText.Foreground = errorBrush;
            }
        }

        private void UpdatePreview(string previewData)
        {
            if (string.IsNullOrWhiteSpace(previewData))
            {
                previewText.Text = "Preview: (no data)";
                previewText.Foreground = dimBrush;
                return;
            }

            previewText.Text = previewData;
            previewText.Foreground = fgBrush;
        }

        private void ImportFromClipboard()
        {
            try
            {
                Dictionary<string, string> cellData;

                // Check if we have COM-read data
                if (clipboardTextBox.Tag is Dictionary<string, string> comData)
                {
                    cellData = comData;
                    logger.Log(LogLevel.Info, "ExcelImport", "Using COM-read Excel data");
                }
                else
                {
                    // Fallback: parse clipboard text
                    string clipboardText = clipboardTextBox.Text;
                    if (string.IsNullOrWhiteSpace(clipboardText))
                    {
                        UpdateStatus("ERROR: No data to import", errorBrush);
                        return;
                    }

                    string startCell = startCellBox.Text.Trim().ToUpperInvariant();
                    if (string.IsNullOrEmpty(startCell))
                    {
                        startCell = "W3";
                    }

                    cellData = ClipboardDataParser.ParseTSV(clipboardText, startCell);
                    logger.Log(LogLevel.Info, "ExcelImport", "Using clipboard TSV data");
                }

                // Set active profile from current selection
                if (availableProfiles != null && availableProfiles.Count > 0)
                {
                    var selectedProfile = availableProfiles[currentProfileIndex];
                    excelMappingService.SetActiveProfile(selectedProfile.Id);
                }

                UpdateStatus("Importing...", accentBrush);

                // Import project using the cellData dictionary (works for both COM and clipboard data)
                Project project = excelMappingService.ImportProjectFromCellData(cellData);

                // Add to project service
                projectService.AddProject(project);

                logger.Log(LogLevel.Info, "ExcelImport", $"Imported project: {project.Name} (ID2: {project.ID2})");

                // Publish event
                eventBus.Publish(new Core.Events.ProjectSelectedEvent
                {
                    Project = project,
                    SourceWidget = "ExcelImportPane"
                });

                // Success!
                UpdateStatus($"✓ Imported: {project.Name} (ID2: {project.ID2})", successBrush);

                // Clear clipboard box and Tag
                clipboardTextBox.Text = "";
                clipboardTextBox.Tag = null;

                // Show success message
                MessageBox.Show(
                    $"Successfully imported project:\n\n" +
                    $"Name: {project.Name}\n" +
                    $"ID2 (CAS Case): {project.ID2}\n" +
                    $"Client: {project.FullProjectName}\n\n" +
                    $"Go to Projects pane to view/edit details.",
                    "Import Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Importing project from Excel clipboard data using profile '{availableProfiles[currentProfileIndex].Name}'",
                    logger);

                UpdateStatus($"ERROR: {ex.Message}", errorBrush);

                MessageBox.Show(
                    $"Import failed:\n\n{ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateStatus(string message, SolidColorBrush color = null)
        {
            statusLabel.Text = message;
            statusLabel.Foreground = color ?? fgBrush;
        }

        public override PaneState SaveState()
        {
            return new PaneState
            {
                PaneType = "ExcelImportPane",
                CustomData = new Dictionary<string, object>
                {
                    ["CurrentProfileIndex"] = currentProfileIndex,
                    ["StartCell"] = startCellBox?.Text,
                    ["ClipboardContent"] = clipboardTextBox?.Text
                }
            };
        }

        public override void RestoreState(PaneState state)
        {
            if (state?.CustomData == null) return;

            var data = state.CustomData as Dictionary<string, object>;
            if (data == null) return;

            // Restore profile index
            if (data.TryGetValue("CurrentProfileIndex", out var profileIndex))
            {
                currentProfileIndex = Convert.ToInt32(profileIndex);
                if (availableProfiles != null && currentProfileIndex >= 0 && currentProfileIndex < availableProfiles.Count)
                {
                    UpdateProfileDisplay();
                }
            }

            // Restore start cell
            if (data.TryGetValue("StartCell", out var startCell) && startCell != null)
            {
                var startCellText = startCell.ToString();
                if (!string.IsNullOrEmpty(startCellText) && startCellBox != null)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        startCellBox.Text = startCellText;
                    });
                }
            }

            // Restore clipboard content
            if (data.TryGetValue("ClipboardContent", out var clipboardContent) && clipboardContent != null)
            {
                var contentText = clipboardContent.ToString();
                if (!string.IsNullOrEmpty(contentText) && clipboardTextBox != null)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        clipboardTextBox.Text = contentText;
                    });
                }
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ApplyTheme();
            });
        }

        private void ApplyTheme()
        {
            CacheThemeColors();

            // Update all controls
            if (clipboardTextBox != null)
            {
                clipboardTextBox.Background = surfaceBrush;
                clipboardTextBox.Foreground = fgBrush;
                clipboardTextBox.BorderBrush = borderBrush;
            }

            if (previewText != null)
            {
                previewText.Foreground = fgBrush;
                previewText.Background = surfaceBrush;
            }

            if (statusLabel != null)
            {
                statusLabel.Foreground = fgBrush;
                statusLabel.Background = surfaceBrush;
            }

            this.InvalidateVisual();
        }

        protected override void OnDispose()
        {
            // Clear pane-specific shortcuts
            try
            {
                var shortcuts = ShortcutManager.Instance;
                shortcuts.ClearPane(PaneName);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, "ExcelImport", $"Failed to clear pane shortcuts: {ex.Message}");
            }

            // Unsubscribe from text changed event
            if (clipboardTextBox != null)
            {
                clipboardTextBox.TextChanged -= ClipboardTextBox_TextChanged;
            }

            // Unsubscribe from button clicks (if they exist)
            if (readExcelButton != null)
            {
                readExcelButton.Click -= ReadExcelButton_Click;
            }
            if (selectFileButton != null)
            {
                selectFileButton.Click -= SelectFileButton_Click;
            }

            // Unsubscribe from main layout keyboard events
            if (mainLayout != null)
            {
                mainLayout.PreviewKeyDown -= MainLayout_PreviewKeyDown;
            }

            // Unsubscribe from theme changes
            themeManager.ThemeChanged -= OnThemeChanged;

            // Dispose COM reader
            if (excelComReader != null)
            {
                excelComReader.Dispose();
                excelComReader = null;
            }

            base.OnDispose();
        }
    }
}
