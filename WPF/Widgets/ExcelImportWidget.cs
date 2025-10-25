using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Core.Infrastructure;

namespace SuperTUI.Widgets
{
    public class ExcelImportWidget : WidgetBase
    {
        private ComboBox profileSelector;
        private DataGrid mappingGrid;
        private TextBox previewText;
        private Button pasteButton;
        private Button importButton;
        private Button clearButton;
        private TextBlock statusLabel;
        private ObservableCollection<MappingViewModel> mappingViewModels;
        private Dictionary<string, string> clipboardData;

        public override void Initialize()
        {
            WidgetName = "ExcelImport";
            BuildUI();
            LoadProfiles();
            RegisterEvents();
        }

        private void BuildUI()
        {
            var grid = new Grid();
            grid.Background = new SolidColorBrush(Color.FromRgb(12, 12, 12));

            // 3 rows: controls, data grid, preview
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150) });

            // Row 0: Controls
            var controlPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Profile selector
            var profileLabel = new TextBlock
            {
                Text = "Profile:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12
            };
            controlPanel.Children.Add(profileLabel);

            profileSelector = new ComboBox
            {
                Width = 200,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12
            };
            profileSelector.SelectionChanged += OnProfileChanged;
            controlPanel.Children.Add(profileSelector);

            // Paste button
            pasteButton = new Button
            {
                Content = "Paste from Excel",
                Width = 130,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Padding = new Thickness(5)
            };
            pasteButton.Click += OnPasteClick;
            controlPanel.Children.Add(pasteButton);

            // Import button
            importButton = new Button
            {
                Content = "Import Project",
                Width = 120,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(16, 124, 16)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Padding = new Thickness(5),
                IsEnabled = false
            };
            importButton.Click += OnImportClick;
            controlPanel.Children.Add(importButton);

            // Clear button
            clearButton = new Button
            {
                Content = "Clear",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Padding = new Thickness(5)
            };
            clearButton.Click += OnClearClick;
            controlPanel.Children.Add(clearButton);

            // Status label
            statusLabel = new TextBlock
            {
                Foreground = Brushes.Yellow,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11
            };
            controlPanel.Children.Add(statusLabel);

            Grid.SetRow(controlPanel, 0);
            grid.Children.Add(controlPanel);

            // Row 1: Data Grid
            mappingViewModels = new ObservableCollection<MappingViewModel>();

            mappingGrid = new DataGrid
            {
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserReorderColumns = false,
                CanUserSortColumns = true,
                IsReadOnly = false,
                ItemsSource = mappingViewModels,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                RowBackground = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(35, 35, 35))
            };

            // Define columns
            mappingGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Display Name",
                Binding = new System.Windows.Data.Binding("DisplayName"),
                Width = 180,
                IsReadOnly = true
            });

            mappingGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Excel Cell",
                Binding = new System.Windows.Data.Binding("ExcelCellRef"),
                Width = 80,
                IsReadOnly = true
            });

            mappingGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Category",
                Binding = new System.Windows.Data.Binding("Category"),
                Width = 120,
                IsReadOnly = true
            });

            mappingGrid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "Export",
                Binding = new System.Windows.Data.Binding("IncludeInExport"),
                Width = 60,
                IsReadOnly = true
            });

            mappingGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Preview Value",
                Binding = new System.Windows.Data.Binding("PreviewValue"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                IsReadOnly = true
            });

            Grid.SetRow(mappingGrid, 1);
            grid.Children.Add(mappingGrid);

            // Row 2: Preview
            var previewPanel = new Border
            {
                Margin = new Thickness(5),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20))
            };

            var previewStack = new StackPanel
            {
                Margin = new Thickness(5)
            };

            var previewLabel = new TextBlock
            {
                Text = "Import Preview:",
                Foreground = Brushes.Cyan,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            previewStack.Children.Add(previewLabel);

            previewText = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
                Foreground = Brushes.LightGray,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = "Paste Excel data to see preview..."
            };
            previewStack.Children.Add(previewText);

            previewPanel.Child = previewStack;
            Grid.SetRow(previewPanel, 2);
            grid.Children.Add(previewPanel);

            Content = grid;
        }

        private void LoadProfiles()
        {
            var profiles = ExcelMappingService.Instance.GetAllProfiles();
            profileSelector.Items.Clear();

            foreach (var profile in profiles)
            {
                profileSelector.Items.Add(profile);
            }

            if (profiles.Count > 0)
            {
                profileSelector.SelectedIndex = 0;
            }
        }

        private void RegisterEvents()
        {
            ExcelMappingService.Instance.ProfileChanged += OnServiceProfileChanged;
            ExcelMappingService.Instance.ProfilesLoaded += OnProfilesLoaded;
        }

        private void OnProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (profileSelector.SelectedItem is ExcelMappingProfile profile)
            {
                ExcelMappingService.Instance.SetActiveProfile(profile.Id);
                LoadMappings(profile);
                statusLabel.Text = $"Profile: {profile.Name} ({profile.Mappings.Count} fields)";
            }
        }

        private void LoadMappings(ExcelMappingProfile profile)
        {
            mappingViewModels.Clear();

            foreach (var mapping in profile.Mappings.OrderBy(m => m.SortOrder))
            {
                mappingViewModels.Add(new MappingViewModel
                {
                    DisplayName = mapping.DisplayName,
                    ExcelCellRef = mapping.ExcelCellRef,
                    Category = mapping.Category,
                    IncludeInExport = mapping.IncludeInExport,
                    PreviewValue = mapping.PreviewValue ?? string.Empty,
                    Mapping = mapping
                });
            }

            // Apply clipboard data if available
            if (clipboardData != null)
            {
                UpdatePreviewValues();
            }
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    statusLabel.Text = "Clipboard does not contain text data";
                    statusLabel.Foreground = Brushes.Red;
                    return;
                }

                string clipboardText = Clipboard.GetText();

                // Parse clipboard data (assuming W3 start cell by default)
                clipboardData = ClipboardDataParser.ParseTSV(clipboardText, "W3");

                statusLabel.Text = $"Parsed {clipboardData.Count} cells from clipboard";
                statusLabel.Foreground = Brushes.Green;

                UpdatePreviewValues();
                importButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
                statusLabel.Foreground = Brushes.Red;
                Logger.Instance.Error("ExcelImportWidget", $"Paste failed: {ex.Message}");
            }
        }

        private void UpdatePreviewValues()
        {
            if (clipboardData == null)
                return;

            // Update preview values in grid
            foreach (var vm in mappingViewModels)
            {
                if (clipboardData.ContainsKey(vm.ExcelCellRef))
                {
                    vm.PreviewValue = clipboardData[vm.ExcelCellRef];
                }
                else
                {
                    vm.PreviewValue = string.Empty;
                }
            }

            // Update preview text
            UpdatePreviewText();
        }

        private void UpdatePreviewText()
        {
            var preview = new System.Text.StringBuilder();
            preview.AppendLine("Project Preview:");
            preview.AppendLine("================");
            preview.AppendLine();

            int count = 0;
            foreach (var vm in mappingViewModels.Where(m => !string.IsNullOrEmpty(m.PreviewValue)))
            {
                preview.AppendLine($"{vm.DisplayName,-25}: {vm.PreviewValue}");
                count++;

                if (count >= 20) // Limit preview to first 20 fields
                {
                    preview.AppendLine($"... and {mappingViewModels.Count - count} more fields");
                    break;
                }
            }

            if (count == 0)
            {
                preview.AppendLine("No data parsed. Check Excel cell references.");
            }

            previewText.Text = preview.ToString();
        }

        private void OnImportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (clipboardData == null || clipboardData.Count == 0)
                {
                    statusLabel.Text = "No data to import. Please paste from Excel first.";
                    statusLabel.Foreground = Brushes.Red;
                    return;
                }

                // Get clipboard text
                string clipboardText = Clipboard.GetText();

                // Import using service
                var project = ExcelMappingService.Instance.ImportProjectFromClipboard(clipboardText, "W3");

                // Add to ProjectService
                ProjectService.Instance.AddProject(project);

                statusLabel.Text = $"Imported: {project.Nickname ?? project.FullProjectName}";
                statusLabel.Foreground = Brushes.Green;

                // Clear after import
                OnClearClick(null, null);

                Logger.Instance.Info("ExcelImportWidget", $"Imported project: {project.Id}");
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Import failed: {ex.Message}";
                statusLabel.Foreground = Brushes.Red;
                Logger.Instance.Error("ExcelImportWidget", $"Import failed: {ex.Message}");
            }
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            clipboardData = null;
            importButton.IsEnabled = false;

            foreach (var vm in mappingViewModels)
            {
                vm.PreviewValue = string.Empty;
            }

            previewText.Text = "Paste Excel data to see preview...";
            statusLabel.Text = "Cleared";
            statusLabel.Foreground = Brushes.White;
        }

        private void OnServiceProfileChanged(ExcelMappingProfile profile)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LoadProfiles();
            });
        }

        private void OnProfilesLoaded()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LoadProfiles();
            });
        }

        protected override void OnDispose()
        {
            ExcelMappingService.Instance.ProfileChanged -= OnServiceProfileChanged;
            ExcelMappingService.Instance.ProfilesLoaded -= OnProfilesLoaded;
            base.OnDispose();
        }
    }

    /// <summary>
    /// ViewModel for DataGrid binding
    /// </summary>
    public class MappingViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string previewValue;

        public string DisplayName { get; set; }
        public string ExcelCellRef { get; set; }
        public string Category { get; set; }
        public bool IncludeInExport { get; set; }

        public string PreviewValue
        {
            get => previewValue;
            set
            {
                if (previewValue != value)
                {
                    previewValue = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(PreviewValue)));
                }
            }
        }

        public ExcelFieldMapping Mapping { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}
