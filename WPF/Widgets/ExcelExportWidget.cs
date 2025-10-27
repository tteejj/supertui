using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Widget for exporting projects to various formats (CSV, TSV, JSON, XML)
    /// </summary>
    public class ExcelExportWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IExcelMappingService excelService;
        private readonly IProjectService projectService;

        private ListBox projectListBox;
        private ComboBox formatComboBox;
        private ComboBox profileComboBox;
        private TextBox previewTextBox;
        private TextBlock statusText;
        private TextBlock fieldCountText;
        private Button exportClipboardButton;
        private Button exportFileButton;
        private Button selectAllButton;
        private Button clearSelectionButton;
        private Button refreshButton;

        private List<Project> allProjects;

        // DI constructor
        public ExcelExportWidget(
            ILogger logger,
            IThemeManager themeManager,
            IExcelMappingService excelService,
            IProjectService projectService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));

            WidgetName = "Excel Export";
            WidgetType = "ExcelExportWidget";
            allProjects = new List<Project>();
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
                Text = "📤 Export to Excel",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(theme.BackgroundSecondary)
            };
            DockPanel.SetDock(header, Dock.Top);
            mainPanel.Children.Add(header);

            // Profile and format selector panel
            var selectorPanel = new DockPanel
            {
                Margin = new Thickness(10, 10, 10, 5),
                LastChildFill = false
            };

            // Profile selector (left)
            var profileStack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            DockPanel.SetDock(profileStack, Dock.Left);

            var profileLabel = new TextBlock
            {
                Text = "Profile:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            profileStack.Children.Add(profileLabel);

            profileComboBox = new ComboBox
            {
                Width = 250,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground)
            };
            profileComboBox.SelectionChanged += ProfileComboBox_SelectionChanged;
            profileStack.Children.Add(profileComboBox);

            selectorPanel.Children.Add(profileStack);

            // Format selector (right)
            var formatStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(formatStack, Dock.Right);

            var formatLabel = new TextBlock
            {
                Text = "Format:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            formatStack.Children.Add(formatLabel);

            formatComboBox = new ComboBox
            {
                Width = 150,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground)
            };
            formatComboBox.Items.Add("CSV");
            formatComboBox.Items.Add("TSV (Excel Paste)");
            formatComboBox.Items.Add("JSON");
            formatComboBox.Items.Add("XML");
            formatComboBox.SelectedIndex = 1; // Default to TSV
            formatComboBox.SelectionChanged += FormatComboBox_SelectionChanged;
            formatStack.Children.Add(formatComboBox);

            selectorPanel.Children.Add(formatStack);

            DockPanel.SetDock(selectorPanel, Dock.Top);
            mainPanel.Children.Add(selectorPanel);

            // Field count info
            fieldCountText = new TextBlock
            {
                Text = "Export fields: 0",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(10, 0, 10, 5)
            };
            DockPanel.SetDock(fieldCountText, Dock.Top);
            mainPanel.Children.Add(fieldCountText);

            // Main content - split between project list and preview
            var contentGrid = new Grid
            {
                Margin = new Thickness(10, 0, 10, 10)
            };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

            // Left panel - Project selection
            var leftPanel = new DockPanel();
            Grid.SetColumn(leftPanel, 0);

            // Project list header with buttons
            var projectHeaderPanel = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 5)
            };

            var projectLabel = new TextBlock
            {
                Text = "Select Projects:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(projectLabel, Dock.Left);
            projectHeaderPanel.Children.Add(projectLabel);

            var projectButtonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(projectButtonStack, Dock.Right);

            selectAllButton = new Button
            {
                Content = "All",
                Width = 60,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border)
            };
            selectAllButton.Click += SelectAllButton_Click;
            projectButtonStack.Children.Add(selectAllButton);

            clearSelectionButton = new Button
            {
                Content = "Clear",
                Width = 60,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border)
            };
            clearSelectionButton.Click += ClearSelectionButton_Click;
            projectButtonStack.Children.Add(clearSelectionButton);

            refreshButton = new Button
            {
                Content = "↻",
                Width = 30,
                Height = 25,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border)
            };
            refreshButton.Click += RefreshButton_Click;
            projectButtonStack.Children.Add(refreshButton);

            projectHeaderPanel.Children.Add(projectButtonStack);

            DockPanel.SetDock(projectHeaderPanel, Dock.Top);
            leftPanel.Children.Add(projectHeaderPanel);

            projectListBox = new ListBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                SelectionMode = SelectionMode.Multiple
            };
            projectListBox.SelectionChanged += ProjectListBox_SelectionChanged;
            leftPanel.Children.Add(projectListBox);

            contentGrid.Children.Add(leftPanel);

            // Splitter
            var splitter = new GridSplitter
            {
                Width = 5,
                Background = new SolidColorBrush(theme.Border),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetColumn(splitter, 1);
            contentGrid.Children.Add(splitter);

            // Right panel - Preview
            var rightPanel = new DockPanel();
            Grid.SetColumn(rightPanel, 2);

            var previewLabel = new TextBlock
            {
                Text = "Preview:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 5)
            };
            DockPanel.SetDock(previewLabel, Dock.Top);
            rightPanel.Children.Add(previewLabel);

            previewTextBox = new TextBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            rightPanel.Children.Add(previewTextBox);

            contentGrid.Children.Add(rightPanel);

            mainPanel.Children.Add(contentGrid);

            // Bottom panel - Status and buttons
            var bottomPanel = new DockPanel
            {
                Margin = new Thickness(10),
                LastChildFill = false
            };

            statusText = new TextBlock
            {
                Text = "Select projects and format, then export",
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

            exportFileButton = new Button
            {
                Content = "Save to File (Ctrl+S)",
                Width = 160,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                IsEnabled = false
            };
            exportFileButton.Click += ExportFileButton_Click;
            buttonPanel.Children.Add(exportFileButton);

            exportClipboardButton = new Button
            {
                Content = "Copy to Clipboard (Ctrl+E)",
                Width = 200,
                Height = 30,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                IsEnabled = false
            };
            exportClipboardButton.Click += ExportClipboardButton_Click;
            buttonPanel.Children.Add(exportClipboardButton);

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

                // Load projects
                LoadProjects();

                logger.Info("ExcelExport", "Widget initialized");
            }
            catch (Exception ex)
            {
                logger.Error("ExcelExport", $"Initialization failed: {ex.Message}", ex);
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

                UpdateFieldCount();
            }
            catch (Exception ex)
            {
                logger.Error("ExcelExport", $"Failed to load profiles: {ex.Message}", ex);
            }
        }

        private void LoadProjects()
        {
            try
            {
                allProjects = projectService.GetAllProjects();
                projectListBox.Items.Clear();

                foreach (var project in allProjects)
                {
                    var displayText = !string.IsNullOrEmpty(project.Name)
                        ? project.Name
                        : $"Project {project.Id}";

                    displayText += $" [{project.Status}]";

                    projectListBox.Items.Add(new ListBoxItem
                    {
                        Content = displayText,
                        Tag = project,
                        FontFamily = new FontFamily("Cascadia Mono, Consolas")
                    });
                }

                logger.Info("ExcelExport", $"Loaded {allProjects.Count} projects");
            }
            catch (Exception ex)
            {
                logger.Error("ExcelExport", $"Failed to load projects: {ex.Message}", ex);
            }
        }

        private void UpdateFieldCount()
        {
            var exportFields = excelService.GetExportMappings();
            fieldCountText.Text = $"Export fields: {exportFields.Count}";
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
                    UpdateFieldCount();
                    UpdatePreview();
                }
            }
        }

        private void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void ProjectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            previewTextBox.Clear();

            var selectedProjects = GetSelectedProjects();
            if (selectedProjects.Count == 0)
            {
                exportClipboardButton.IsEnabled = false;
                exportFileButton.IsEnabled = false;
                statusText.Text = "Select at least one project";
                statusText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.ForegroundSecondary);
                return;
            }

            try
            {
                var format = GetSelectedFormat();
                var previewData = excelService.ExportToString(selectedProjects, format);

                // Limit preview to first 50 lines
                var lines = previewData.Split('\n');
                var preview = string.Join("\n", lines.Take(50));
                if (lines.Length > 50)
                    preview += $"\n\n... ({lines.Length - 50} more lines)";

                previewTextBox.Text = preview;

                exportClipboardButton.IsEnabled = true;
                exportFileButton.IsEnabled = true;
                statusText.Text = $"Ready to export {selectedProjects.Count} project(s) as {format}";
                statusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
            }
            catch (Exception ex)
            {
                exportClipboardButton.IsEnabled = false;
                exportFileButton.IsEnabled = false;
                statusText.Text = $"✗ Error: {ex.Message}";
                statusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                logger.Warning("ExcelExport", $"Preview failed: {ex.Message}");
            }
        }

        private List<Project> GetSelectedProjects()
        {
            var selected = new List<Project>();
            foreach (var item in projectListBox.SelectedItems)
            {
                if (item is ListBoxItem lbi && lbi.Tag is Project project)
                {
                    selected.Add(project);
                }
            }
            return selected;
        }

        private string GetSelectedFormat()
        {
            var selected = formatComboBox.SelectedItem as string;
            return selected switch
            {
                "CSV" => "csv",
                "TSV (Excel Paste)" => "tsv",
                "JSON" => "json",
                "XML" => "xml",
                _ => "tsv"
            };
        }

        private void ExportClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedProjects = GetSelectedProjects();
                var format = GetSelectedFormat();

                excelService.ExportToClipboard(selectedProjects, format);

                statusText.Text = $"✓ Copied {selectedProjects.Count} project(s) to clipboard";
                statusText.Foreground = new SolidColorBrush(Colors.LimeGreen);

                logger.Info("ExcelExport", $"Exported {selectedProjects.Count} projects to clipboard as {format}");
            }
            catch (Exception ex)
            {
                statusText.Text = $"✗ Export failed: {ex.Message}";
                statusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                logger.Error("ExcelExport", $"Export to clipboard failed: {ex.Message}", ex);
            }
        }

        private void ExportFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedProjects = GetSelectedProjects();
                var format = GetSelectedFormat();

                var saveDialog = new SaveFileDialog
                {
                    Title = "Export Projects",
                    Filter = format switch
                    {
                        "csv" => "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                        "tsv" => "TSV Files (*.tsv;*.txt)|*.tsv;*.txt|All Files (*.*)|*.*",
                        "json" => "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                        "xml" => "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                        _ => "All Files (*.*)|*.*"
                    },
                    DefaultExt = format,
                    FileName = $"projects_export_{DateTime.Now:yyyyMMdd_HHmmss}.{format}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    excelService.ExportToFile(selectedProjects, saveDialog.FileName, format);

                    statusText.Text = $"✓ Exported {selectedProjects.Count} project(s) to {System.IO.Path.GetFileName(saveDialog.FileName)}";
                    statusText.Foreground = new SolidColorBrush(Colors.LimeGreen);

                    logger.Info("ExcelExport", $"Exported {selectedProjects.Count} projects to file: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"✗ Export failed: {ex.Message}";
                statusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                logger.Error("ExcelExport", $"Export to file failed: {ex.Message}", ex);
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            projectListBox.SelectAll();
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            projectListBox.UnselectAll();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadProjects();
            UpdatePreview();
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
                    if (tb == statusText || tb == fieldCountText)
                        continue; // Dynamic colors
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
                else if (child is Button btn && btn != exportClipboardButton)
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
            if (formatComboBox != null)
                formatComboBox.SelectionChanged -= FormatComboBox_SelectionChanged;
            if (projectListBox != null)
                projectListBox.SelectionChanged -= ProjectListBox_SelectionChanged;
            if (exportClipboardButton != null)
                exportClipboardButton.Click -= ExportClipboardButton_Click;
            if (exportFileButton != null)
                exportFileButton.Click -= ExportFileButton_Click;
            if (selectAllButton != null)
                selectAllButton.Click -= SelectAllButton_Click;
            if (clearSelectionButton != null)
                clearSelectionButton.Click -= ClearSelectionButton_Click;
            if (refreshButton != null)
                refreshButton.Click -= RefreshButton_Click;

            base.OnDispose();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            // Ctrl+A to select all projects
            if (e.Key == Key.A && isCtrl && projectListBox != null)
            {
                projectListBox.SelectAll();
                e.Handled = true;
            }

            // Ctrl+E to export to clipboard
            if (e.Key == Key.E && isCtrl && exportClipboardButton != null && exportClipboardButton.IsEnabled)
            {
                ExportClipboardButton_Click(this, null);
                e.Handled = true;
            }

            // Ctrl+S to save to file
            if (e.Key == Key.S && isCtrl && exportFileButton != null && exportFileButton.IsEnabled)
            {
                ExportFileButton_Click(this, null);
                e.Handled = true;
            }

            // Escape to clear selection
            if (e.Key == Key.Escape && projectListBox != null)
            {
                projectListBox.UnselectAll();
                e.Handled = true;
            }
        }
    }
}
