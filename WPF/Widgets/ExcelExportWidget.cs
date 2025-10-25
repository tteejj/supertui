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
    public class ExcelExportWidget : WidgetBase
    {
        private ListBox projectList;
        private ComboBox profileSelector;
        private ComboBox formatSelector;
        private TextBox previewText;
        private Button exportToClipboardButton;
        private Button exportToFileButton;
        private TextBox filePathText;
        private Button browseButton;
        private TextBlock statusLabel;
        private CheckBox selectAllCheckbox;
        private ObservableCollection<ProjectCheckViewModel> projectViewModels;

        public override void Initialize()
        {
            WidgetName = "ExcelExport";
            BuildUI();
            LoadProfiles();
            LoadProjects();
            RegisterEvents();
        }

        private void BuildUI()
        {
            var grid = new Grid();
            grid.Background = new SolidColorBrush(Color.FromRgb(12, 12, 12));

            // 2 columns: left (project list), right (export config)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            // Left panel: Project selection
            var leftPanel = CreateProjectSelectionPanel();
            Grid.SetColumn(leftPanel, 0);
            grid.Children.Add(leftPanel);

            // Right panel: Export configuration
            var rightPanel = CreateExportConfigPanel();
            Grid.SetColumn(rightPanel, 1);
            grid.Children.Add(rightPanel);

            Content = grid;
        }

        private Border CreateProjectSelectionPanel()
        {
            var border = new Border
            {
                Margin = new Thickness(5),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20))
            };

            var stack = new StackPanel { Margin = new Thickness(5) };

            // Header
            var header = new TextBlock
            {
                Text = "Select Projects to Export:",
                Foreground = Brushes.Cyan,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stack.Children.Add(header);

            // Select all checkbox
            selectAllCheckbox = new CheckBox
            {
                Content = "Select All",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 5)
            };
            selectAllCheckbox.Checked += OnSelectAllChecked;
            selectAllCheckbox.Unchecked += OnSelectAllUnchecked;
            stack.Children.Add(selectAllCheckbox);

            // Project list
            projectViewModels = new ObservableCollection<ProjectCheckViewModel>();

            projectList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                ItemsSource = projectViewModels,
                Height = 400,
                SelectionMode = SelectionMode.Multiple
            };

            // Custom item template with checkbox
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            factory.SetValue(CheckBox.ForegroundProperty, Brushes.White);
            factory.SetBinding(CheckBox.ContentProperty, new System.Windows.Data.Binding("DisplayText"));
            factory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsSelected")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            factory.AddHandler(CheckBox.CheckedEvent, new RoutedEventHandler(OnProjectChecked));
            factory.AddHandler(CheckBox.UncheckedEvent, new RoutedEventHandler(OnProjectChecked));
            template.VisualTree = factory;
            projectList.ItemTemplate = template;

            stack.Children.Add(projectList);

            border.Child = stack;
            return border;
        }

        private Border CreateExportConfigPanel()
        {
            var border = new Border
            {
                Margin = new Thickness(5),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20))
            };

            var grid = new Grid { Margin = new Thickness(5) };

            // 4 rows: header, config, preview, buttons
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: Header
            var header = new TextBlock
            {
                Text = "Export Configuration:",
                Foreground = Brushes.Cyan,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Row 1: Configuration controls
            var configStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            // Profile selector
            var profilePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            profilePanel.Children.Add(new TextBlock
            {
                Text = "Profile:",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });

            profileSelector = new ComboBox
            {
                Width = 250,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11
            };
            profileSelector.SelectionChanged += OnExportProfileChanged;
            profilePanel.Children.Add(profileSelector);

            configStack.Children.Add(profilePanel);

            // Format selector
            var formatPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            formatPanel.Children.Add(new TextBlock
            {
                Text = "Format:",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });

            formatSelector = new ComboBox
            {
                Width = 250,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11
            };
            formatSelector.Items.Add("CSV");
            formatSelector.Items.Add("TSV");
            formatSelector.Items.Add("JSON");
            formatSelector.Items.Add("XML");
            formatSelector.Items.Add("TXT");
            formatSelector.SelectedIndex = 0;
            formatSelector.SelectionChanged += OnFormatChanged;
            formatPanel.Children.Add(formatSelector);

            configStack.Children.Add(formatPanel);

            // File path
            var filePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            filePanel.Children.Add(new TextBlock
            {
                Text = "File:",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });

            filePathText = new TextBox
            {
                Width = 200,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                Text = GetDefaultExportPath()
            };
            filePanel.Children.Add(filePathText);

            browseButton = new Button
            {
                Content = "...",
                Width = 40,
                Margin = new Thickness(5, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11
            };
            browseButton.Click += OnBrowseClick;
            filePanel.Children.Add(browseButton);

            configStack.Children.Add(filePanel);

            Grid.SetRow(configStack, 1);
            grid.Children.Add(configStack);

            // Row 2: Preview
            var previewBorder = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 15)),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var previewStack = new StackPanel { Margin = new Thickness(5) };

            previewStack.Children.Add(new TextBlock
            {
                Text = "Export Preview (first 5 projects):",
                Foreground = Brushes.Yellow,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 5)
            });

            previewText = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 15)),
                Foreground = Brushes.LightGray,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = "Select projects and configure export settings..."
            };
            previewStack.Children.Add(previewText);

            previewBorder.Child = previewStack;
            Grid.SetRow(previewBorder, 2);
            grid.Children.Add(previewBorder);

            // Row 3: Action buttons and status
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            exportToClipboardButton = new Button
            {
                Content = "Copy to Clipboard",
                Width = 150,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(5)
            };
            exportToClipboardButton.Click += OnExportToClipboardClick;
            buttonPanel.Children.Add(exportToClipboardButton);

            exportToFileButton = new Button
            {
                Content = "Export to File",
                Width = 120,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(16, 124, 16)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(5)
            };
            exportToFileButton.Click += OnExportToFileClick;
            buttonPanel.Children.Add(exportToFileButton);

            statusLabel = new TextBlock
            {
                Foreground = Brushes.Yellow,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11
            };
            buttonPanel.Children.Add(statusLabel);

            Grid.SetRow(buttonPanel, 3);
            grid.Children.Add(buttonPanel);

            border.Child = grid;
            return border;
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

        private void LoadProjects()
        {
            projectViewModels.Clear();

            var projects = ProjectService.Instance.GetAllProjects();
            foreach (var project in projects)
            {
                projectViewModels.Add(new ProjectCheckViewModel
                {
                    Project = project,
                    IsSelected = false,
                    DisplayText = $"{project.ID2 ?? project.Id1} - {project.Nickname ?? project.FullProjectName}"
                });
            }

            statusLabel.Text = $"{projects.Count} projects available";
            statusLabel.Foreground = Brushes.White;
        }

        private void RegisterEvents()
        {
            ProjectService.Instance.ProjectAdded += OnProjectChanged;
            ProjectService.Instance.ProjectUpdated += OnProjectChanged;
            ExcelMappingService.Instance.ProfilesLoaded += OnProfilesLoaded;
        }

        private void OnSelectAllChecked(object sender, RoutedEventArgs e)
        {
            foreach (var vm in projectViewModels)
            {
                vm.IsSelected = true;
            }
            UpdatePreview();
        }

        private void OnSelectAllUnchecked(object sender, RoutedEventArgs e)
        {
            foreach (var vm in projectViewModels)
            {
                vm.IsSelected = false;
            }
            UpdatePreview();
        }

        private void OnProjectChecked(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void OnExportProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (profileSelector.SelectedItem is ExcelMappingProfile profile)
            {
                ExcelMappingService.Instance.SetActiveProfile(profile.Id);
                var exportFields = profile.GetExportMappings();
                statusLabel.Text = $"Profile: {profile.Name} ({exportFields.Count} export fields)";
                statusLabel.Foreground = Brushes.White;
                UpdatePreview();
            }
        }

        private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFileExtension();
            UpdatePreview();
        }

        private void UpdateFileExtension()
        {
            string currentPath = filePathText.Text;
            string format = formatSelector.SelectedItem as string;

            if (string.IsNullOrEmpty(format))
                return;

            string extension = format.ToLower();
            string pathWithoutExt = System.IO.Path.ChangeExtension(currentPath, null);
            filePathText.Text = pathWithoutExt + "." + extension;
        }

        private void UpdatePreview()
        {
            try
            {
                var selectedProjects = GetSelectedProjects().Take(5).ToList();
                string format = formatSelector.SelectedItem as string ?? "CSV";

                if (selectedProjects.Count == 0)
                {
                    previewText.Text = "No projects selected. Check projects to export...";
                    return;
                }

                string preview = ExcelMappingService.Instance.ExportToString(selectedProjects, format);

                // Limit preview to first 1000 characters
                if (preview.Length > 1000)
                {
                    preview = preview.Substring(0, 1000) + "\n\n... (preview truncated)";
                }

                previewText.Text = preview;
            }
            catch (Exception ex)
            {
                previewText.Text = $"Preview error: {ex.Message}";
            }
        }

        private void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog();
            string format = formatSelector.SelectedItem as string ?? "CSV";
            dialog.Filter = $"{format} Files|*.{format.ToLower()}|All Files|*.*";
            dialog.DefaultExt = format.ToLower();
            dialog.FileName = System.IO.Path.GetFileName(filePathText.Text);

            if (dialog.ShowDialog() == true)
            {
                filePathText.Text = dialog.FileName;
            }
        }

        private void OnExportToClipboardClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedProjects = GetSelectedProjects();
                string format = formatSelector.SelectedItem as string ?? "CSV";

                if (selectedProjects.Count == 0)
                {
                    statusLabel.Text = "No projects selected";
                    statusLabel.Foreground = Brushes.Red;
                    return;
                }

                ExcelMappingService.Instance.ExportToClipboard(selectedProjects, format);

                statusLabel.Text = $"Exported {selectedProjects.Count} projects to clipboard as {format}";
                statusLabel.Foreground = Brushes.Green;

                SuperTUI.Infrastructure.Logger.Instance.Info("ExcelExportWidget", $"Exported {selectedProjects.Count} projects to clipboard");
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Export failed: {ex.Message}";
                statusLabel.Foreground = Brushes.Red;
                SuperTUI.Infrastructure.Logger.Instance.Error("ExcelExportWidget", $"Export to clipboard failed: {ex.Message}");
            }
        }

        private void OnExportToFileClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedProjects = GetSelectedProjects();
                string format = formatSelector.SelectedItem as string ?? "CSV";
                string filePath = filePathText.Text;

                if (selectedProjects.Count == 0)
                {
                    statusLabel.Text = "No projects selected";
                    statusLabel.Foreground = Brushes.Red;
                    return;
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    statusLabel.Text = "Please specify file path";
                    statusLabel.Foreground = Brushes.Red;
                    return;
                }

                ExcelMappingService.Instance.ExportToFile(selectedProjects, filePath, format);

                statusLabel.Text = $"Exported {selectedProjects.Count} projects to {System.IO.Path.GetFileName(filePath)}";
                statusLabel.Foreground = Brushes.Green;

                SuperTUI.Infrastructure.Logger.Instance.Info("ExcelExportWidget", $"Exported {selectedProjects.Count} projects to {filePath}");
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Export failed: {ex.Message}";
                statusLabel.Foreground = Brushes.Red;
                SuperTUI.Infrastructure.Logger.Instance.Error("ExcelExportWidget", $"Export to file failed: {ex.Message}");
            }
        }

        private List<Project> GetSelectedProjects()
        {
            return projectViewModels
                .Where(vm => vm.IsSelected)
                .Select(vm => vm.Project)
                .ToList();
        }

        private string GetDefaultExportPath()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"projects_export_{timestamp}.csv";
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                fileName);
        }

        private void OnProjectChanged(Project project)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LoadProjects();
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
            ProjectService.Instance.ProjectAdded -= OnProjectChanged;
            ProjectService.Instance.ProjectUpdated -= OnProjectChanged;
            ExcelMappingService.Instance.ProfilesLoaded -= OnProfilesLoaded;
            base.OnDispose();
        }
    }

    /// <summary>
    /// ViewModel for project checkbox list
    /// </summary>
    public class ProjectCheckViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool isSelected;

        public Project Project { get; set; }
        public string DisplayText { get; set; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}
