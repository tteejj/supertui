using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Widget for importing projects from Excel clipboard data
    /// </summary>
    public class ExcelImportWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IExcelMappingService excelService;
        private readonly IProjectService projectService;

        private TextBox clipboardTextBox;
        private TextBlock statusText;
        private ListBox previewList;
        private Button importButton;
        private Button clearButton;
        private ComboBox profileComboBox;
        private TextBlock instructionsText;

        // DI constructor
        public ExcelImportWidget(
            ILogger logger,
            IThemeManager themeManager,
            IExcelMappingService excelService,
            IProjectService projectService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));

            WidgetName = "Excel Import";
            WidgetType = "ExcelImportWidget";
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            var mainPanel = new DockPanel
            {
                Background = new SolidColorBrush(theme.Background),
                LastChildFill = true
            };

            // Header
            var header = new TextBlock
            {
                Text = "📥 Import from Excel",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(theme.BackgroundSecondary)
            };
            DockPanel.SetDock(header, Dock.Top);
            mainPanel.Children.Add(header);

            // Instructions
            instructionsText = new TextBlock
            {
                Text = "1. Copy Excel data (select cells in Excel, Ctrl+C)\n" +
                       "2. Paste here (Ctrl+V in text box below)\n" +
                       "3. Review preview\n" +
                       "4. Click Import to add to Projects",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Color.FromArgb(50, theme.Primary.R, theme.Primary.G, theme.Primary.B))
            };
            DockPanel.SetDock(instructionsText, Dock.Top);
            mainPanel.Children.Add(instructionsText);

            // Profile selector
            var profilePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 5, 10, 5)
            };

            var profileLabel = new TextBlock
            {
                Text = "Profile:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            profilePanel.Children.Add(profileLabel);

            profileComboBox = new ComboBox
            {
                Width = 300,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground)
            };
            profileComboBox.SelectionChanged += ProfileComboBox_SelectionChanged;
            profilePanel.Children.Add(profileComboBox);

            DockPanel.SetDock(profilePanel, Dock.Top);
            mainPanel.Children.Add(profilePanel);

            // Clipboard input area
            var inputPanel = new DockPanel
            {
                Margin = new Thickness(10)
            };

            var inputLabel = new TextBlock
            {
                Text = "Clipboard Data (Paste Excel data here):",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 5)
            };
            DockPanel.SetDock(inputLabel, Dock.Top);
            inputPanel.Children.Add(inputLabel);

            clipboardTextBox = new TextBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 150
            };
            clipboardTextBox.TextChanged += ClipboardTextBox_TextChanged;
            inputPanel.Children.Add(clipboardTextBox);

            DockPanel.SetDock(inputPanel, Dock.Top);
            mainPanel.Children.Add(inputPanel);

            // Preview section
            var previewPanel = new DockPanel
            {
                Margin = new Thickness(10, 0, 10, 10)
            };

            var previewLabel = new TextBlock
            {
                Text = "Preview:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 5)
            };
            DockPanel.SetDock(previewLabel, Dock.Top);
            previewPanel.Children.Add(previewLabel);

            previewList = new ListBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Height = 200
            };
            previewPanel.Children.Add(previewList);

            mainPanel.Children.Add(previewPanel);

            // Bottom buttons and status
            var bottomPanel = new DockPanel
            {
                Margin = new Thickness(10),
                LastChildFill = false
            };

            statusText = new TextBlock
            {
                Text = "Ready to import",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(statusText, Dock.Left);
            bottomPanel.Children.Add(statusText);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(buttonPanel, Dock.Right);

            clearButton = new Button
            {
                Content = "Clear",
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border)
            };
            clearButton.Click += ClearButton_Click;
            buttonPanel.Children.Add(clearButton);

            importButton = new Button
            {
                Content = "Import",
                Width = 100,
                Height = 30,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                IsEnabled = false
            };
            importButton.Click += ImportButton_Click;
            buttonPanel.Children.Add(importButton);

            bottomPanel.Children.Add(buttonPanel);

            DockPanel.SetDock(bottomPanel, Dock.Bottom);
            mainPanel.Children.Add(bottomPanel);

            this.Content = mainPanel;
        }

        public override void Initialize()
        {
            try
            {
                // Load profiles
                LoadProfiles();

                logger.Info("ExcelImport", "Widget initialized");
            }
            catch (Exception ex)
            {
                logger.Error("ExcelImport", $"Initialization failed: {ex.Message}", ex);
            }
        }

        private void LoadProfiles()
        {
            try
            {
                var profiles = excelService.GetAllProfiles();
                profileComboBox.Items.Clear();

                foreach (var profile in profiles)
                {
                    profileComboBox.Items.Add(profile.Name);
                }

                var activeProfile = excelService.GetActiveProfile();
                if (activeProfile != null)
                {
                    profileComboBox.SelectedItem = activeProfile.Name;
                }
            }
            catch (Exception ex)
            {
                logger.Error("ExcelImport", $"Failed to load profiles: {ex.Message}", ex);
            }
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (profileComboBox.SelectedItem is string selectedName)
            {
                var profiles = excelService.GetAllProfiles();
                var profile = profiles.FirstOrDefault(p => p.Name == selectedName);
                if (profile != null)
                {
                    excelService.SetActiveProfile(profile.Id);
                    UpdatePreview();
                }
            }
        }

        private void ClipboardTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            previewList.Items.Clear();

            if (string.IsNullOrWhiteSpace(clipboardTextBox.Text))
            {
                importButton.IsEnabled = false;
                statusText.Text = "Paste Excel data to preview";
                statusText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.ForegroundSecondary);
                return;
            }

            try
            {
                var project = excelService.ImportProjectFromClipboard(clipboardTextBox.Text, "W3");

                // Show preview of mapped fields
                var activeProfile = excelService.GetActiveProfile();
                if (activeProfile != null)
                {
                    previewList.Items.Add($"Profile: {activeProfile.Name}");
                    previewList.Items.Add($"Fields: {activeProfile.Mappings.Count}");
                    previewList.Items.Add("─────────────────────────────────");

                    // Show key project fields
                    if (!string.IsNullOrEmpty(project.Name))
                        previewList.Items.Add($"Name: {project.Name}");
                    if (!string.IsNullOrEmpty(project.Description))
                        previewList.Items.Add($"Description: {project.Description}");
                    previewList.Items.Add($"Status: {project.Status}");

                    previewList.Items.Add("─────────────────────────────────");
                    previewList.Items.Add($"Ready to import 1 project");
                }

                importButton.IsEnabled = true;
                statusText.Text = "✓ Data parsed successfully";
                statusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
            }
            catch (Exception ex)
            {
                importButton.IsEnabled = false;
                statusText.Text = $"✗ Error: {ex.Message}";
                statusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                logger.Warning("ExcelImport", $"Preview failed: {ex.Message}");
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var project = excelService.ImportProjectFromClipboard(clipboardTextBox.Text, "W3");
                projectService.AddProject(project);

                statusText.Text = $"✓ Imported: {project.Name}";
                statusText.Foreground = new SolidColorBrush(Colors.LimeGreen);

                logger.Info("ExcelImport", $"Imported project: {project.Name}");

                // Clear after successful import
                clipboardTextBox.Clear();
                previewList.Items.Clear();
                importButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                statusText.Text = $"✗ Import failed: {ex.Message}";
                statusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                logger.Error("ExcelImport", $"Import failed: {ex.Message}", ex);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            clipboardTextBox.Clear();
            previewList.Items.Clear();
            importButton.IsEnabled = false;
            statusText.Text = "Cleared";
            statusText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.ForegroundSecondary);
        }

        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            if (this.Content is Panel panel)
            {
                panel.Background = new SolidColorBrush(theme.Background);
            }

            // Update all child controls
            UpdateChildThemes(this, theme);
        }

        private void UpdateChildThemes(DependencyObject parent, Theme theme)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is TextBlock tb)
                {
                    if (tb == statusText)
                        continue; // Status text color is dynamic
                    tb.Foreground = new SolidColorBrush(theme.Foreground);
                }
                else if (child is TextBox textBox)
                {
                    textBox.Background = new SolidColorBrush(theme.BackgroundSecondary);
                    textBox.Foreground = new SolidColorBrush(theme.Foreground);
                    textBox.BorderBrush = new SolidColorBrush(theme.Border);
                }
                else if (child is ListBox lb)
                {
                    lb.Background = new SolidColorBrush(theme.BackgroundSecondary);
                    lb.Foreground = new SolidColorBrush(theme.Foreground);
                    lb.BorderBrush = new SolidColorBrush(theme.Border);
                }
                else if (child is Button btn && btn != importButton)
                {
                    btn.Background = new SolidColorBrush(theme.BackgroundSecondary);
                    btn.Foreground = new SolidColorBrush(theme.Foreground);
                    btn.BorderBrush = new SolidColorBrush(theme.Border);
                }
                else if (child is ComboBox cb)
                {
                    cb.Background = new SolidColorBrush(theme.BackgroundSecondary);
                    cb.Foreground = new SolidColorBrush(theme.Foreground);
                }
                else if (child is Panel p)
                {
                    if (p.Background is SolidColorBrush)
                        p.Background = new SolidColorBrush(theme.Background);
                }

                UpdateChildThemes(child, theme);
            }
        }

        protected override void OnDispose()
        {
            // Unsubscribe from events
            if (profileComboBox != null)
                profileComboBox.SelectionChanged -= ProfileComboBox_SelectionChanged;
            if (clipboardTextBox != null)
                clipboardTextBox.TextChanged -= ClipboardTextBox_TextChanged;
            if (importButton != null)
                importButton.Click -= ImportButton_Click;
            if (clearButton != null)
                clearButton.Click -= ClearButton_Click;

            base.OnDispose();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            // Ctrl+V to paste from clipboard
            if (e.Key == Key.V && isCtrl && clipboardTextBox != null && !clipboardTextBox.IsFocused)
            {
                try
                {
                    clipboardTextBox.Text = Clipboard.GetText();
                    clipboardTextBox.Focus();
                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    logger.Warning("ExcelImport", $"Paste failed: {ex.Message}");
                }
            }

            // Ctrl+I to import
            if (e.Key == Key.I && isCtrl && importButton != null && importButton.IsEnabled)
            {
                ImportButton_Click(this, null);
                e.Handled = true;
            }

            // Escape to clear
            if (e.Key == Key.Escape)
            {
                ClearButton_Click(this, null);
                e.Handled = true;
            }
        }
    }
}
