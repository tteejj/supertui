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
    /// Excel import pane for clipboard-based project import
    /// User copies cells from Excel (W3:W130), pastes here, clicks Import
    /// Uses ExcelMappingService with SVI-CAS profile
    /// </summary>
    public class ExcelImportPane : PaneBase
    {
        // Services
        private readonly IProjectService projectService;
        private readonly IExcelMappingService excelMappingService;
        private readonly IEventBus eventBus;

        // UI Components
        private Grid mainLayout;
        private TextBox clipboardTextBox;
        private ComboBox profileComboBox;
        private TextBox startCellBox;
        private TextBlock statusLabel;
        private Button importButton;
        private TextBlock helpText;
        private TextBlock previewText;

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
            PaneName = "Excel Import";
        }

        public override void Initialize()
        {
            base.Initialize();

            // Initialize ExcelMappingService
            excelMappingService.Initialize();

            // Set initial focus
            Dispatcher.BeginInvoke(new Action(() =>
            {
                clipboardTextBox?.Focus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override UIElement BuildContent()
        {
            CacheThemeColors();

            mainLayout = new Grid();
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Instructions
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Clipboard input
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Preview
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status
            mainLayout.Background = bgBrush;
            mainLayout.KeyDown += MainLayout_KeyDown;
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
                       "2. Select cells W3:W130 (48 fields)\n" +
                       "3. Copy to clipboard (Ctrl+C)\n" +
                       "4. Paste below (focus textbox, system paste)\n" +
                       "5. Press I to import"
            };
            Grid.SetRow(helpText, 1);
            mainLayout.Children.Add(helpText);

            // Controls panel
            var controlsPanel = BuildControlsPanel();
            Grid.SetRow(controlsPanel, 2);
            mainLayout.Children.Add(controlsPanel);

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
            Grid.SetRow(clipboardTextBox, 3);
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
            Grid.SetRow(previewText, 4);
            mainLayout.Children.Add(previewText);

            // Status bar
            statusLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = fgBrush,
                Background = surfaceBrush,
                Padding = new Thickness(16, 8, 16, 8),
                Text = "Ready to import | Paste data into textbox | I:Import"
            };
            Grid.SetRow(statusLabel, 5);
            mainLayout.Children.Add(statusLabel);

            return mainLayout;
        }

        private Grid BuildControlsPanel()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Margin = new Thickness(16, 0, 16, 12);

            // Profile label
            var profileLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = fgBrush,
                Text = "Profile:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(profileLabel, 0);
            grid.Children.Add(profileLabel);

            // Profile combo
            profileComboBox = new ComboBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderBrush = borderBrush,
                MinWidth = 200
            };
            LoadProfiles();
            Grid.SetColumn(profileComboBox, 1);
            grid.Children.Add(profileComboBox);

            // Start cell label
            var startCellLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = fgBrush,
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
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderBrush = borderBrush,
                Text = "W3",
                Width = 60,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(startCellBox, 3);
            grid.Children.Add(startCellBox);

            // Import button
            importButton = new Button
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = accentBrush,
                Foreground = bgBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24, 8, 24, 8),
                Margin = new Thickness(16, 0, 0, 0),
                Content = "Import (I)",
                Cursor = Cursors.Hand
            };
            importButton.Click += ImportButton_Click;
            Grid.SetColumn(importButton, 4);
            grid.Children.Add(importButton);

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
            var profiles = excelMappingService.GetAllProfiles();
            profileComboBox.Items.Clear();

            foreach (var profile in profiles)
            {
                profileComboBox.Items.Add(new ComboBoxItem
                {
                    Content = profile.Name,
                    Tag = profile
                });
            }

            // Select active profile
            var activeProfile = excelMappingService.GetActiveProfile();
            if (activeProfile != null)
            {
                for (int i = 0; i < profileComboBox.Items.Count; i++)
                {
                    if (profileComboBox.Items[i] is ComboBoxItem item && item.Tag is ExcelMappingProfile p && p.Id == activeProfile.Id)
                    {
                        profileComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (profileComboBox.Items.Count > 0)
            {
                profileComboBox.SelectedIndex = 0;
            }
        }

        private void MainLayout_KeyDown(object sender, KeyEventArgs e)
        {
            // I to import
            if (e.Key == Key.I)
            {
                ImportFromClipboard();
                e.Handled = true;
            }
        }

        private void ClipboardTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            ImportFromClipboard();
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
                previewText.Text = $"Preview error: {ex.Message}";
                previewText.Foreground = errorBrush;
            }
        }

        private void ImportFromClipboard()
        {
            try
            {
                string clipboardData = clipboardTextBox.Text;
                if (string.IsNullOrWhiteSpace(clipboardData))
                {
                    UpdateStatus("ERROR: No data to import", errorBrush);
                    return;
                }

                string startCell = startCellBox.Text.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(startCell))
                {
                    startCell = "W3";
                }

                // Set active profile from combo
                if (profileComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is ExcelMappingProfile profile)
                {
                    excelMappingService.SetActiveProfile(profile.Id);
                }

                UpdateStatus("Importing...", accentBrush);

                // Import project
                var project = excelMappingService.ImportProjectFromClipboard(clipboardData, startCell);

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

                // Clear clipboard box
                clipboardTextBox.Text = "";

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
                logger.Log(LogLevel.Error, "ExcelImport", $"Import failed: {ex.Message}", ex);
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

        protected override void OnDispose()
        {
            base.OnDispose();
        }
    }
}
