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
using Microsoft.Win32;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Widget for automated Excel-to-Excel data copying using field mappings
    /// </summary>
    public class ExcelAutomationWidget : WidgetBase
    {
        private TextBox sourceFileText;
        private TextBox destFileText;
        private TextBox sourceSheetText;
        private TextBox destSheetText;
        private Button browseSourceButton;
        private Button browseDestButton;
        private ComboBox profileSelector;
        private Button runCopyButton;
        private Button batchProcessButton;
        private ProgressBar progressBar;
        private TextBox statusLog;
        private TextBlock statusLabel;
        private CheckBox batchModeCheckbox;
        private ListBox sourceFilesList;
        private ObservableCollection<string> batchSourceFiles;

        public override void Initialize()
        {
            WidgetName = "ExcelAutomation";
            BuildUI();
            LoadProfiles();
            RegisterEvents();
        }

        private void BuildUI()
        {
            var grid = new Grid();
            grid.Background = new SolidColorBrush(Color.FromRgb(12, 12, 12));

            // 5 rows: controls, file selection, progress, log, status
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });  // Profile selector
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) }); // File selection
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });  // Progress
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Log
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });  // Status

            // Row 0: Profile selector
            var profilePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var profileLabel = new TextBlock
            {
                Text = "Mapping Profile:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12
            };
            profilePanel.Children.Add(profileLabel);

            profileSelector = new ComboBox
            {
                Width = 200,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12
            };
            profileSelector.SelectionChanged += OnProfileChanged;
            profilePanel.Children.Add(profileSelector);

            batchModeCheckbox = new CheckBox
            {
                Content = "Batch Mode (Multiple Sources)",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Margin = new Thickness(10, 0, 0, 0)
            };
            batchModeCheckbox.Checked += OnBatchModeChanged;
            batchModeCheckbox.Unchecked += OnBatchModeChanged;
            profilePanel.Children.Add(batchModeCheckbox);

            Grid.SetRow(profilePanel, 0);
            grid.Children.Add(profilePanel);

            // Row 1: File selection
            var fileSelectionPanel = new Grid();
            fileSelectionPanel.Margin = new Thickness(5);
            fileSelectionPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileSelectionPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left column: Source file(s)
            var sourcePanel = BuildSourcePanel();
            Grid.SetColumn(sourcePanel, 0);
            fileSelectionPanel.Children.Add(sourcePanel);

            // Right column: Destination file
            var destPanel = BuildDestinationPanel();
            Grid.SetColumn(destPanel, 1);
            fileSelectionPanel.Children.Add(destPanel);

            Grid.SetRow(fileSelectionPanel, 1);
            grid.Children.Add(fileSelectionPanel);

            // Row 2: Progress and action buttons
            var progressPanel = new Grid();
            progressPanel.Margin = new Thickness(5);
            progressPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            progressPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            runCopyButton = new Button
            {
                Content = "Run Automated Copy",
                Width = 150,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(16, 124, 16)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Padding = new Thickness(5),
                IsEnabled = false
            };
            runCopyButton.Click += OnRunCopyClick;
            buttonPanel.Children.Add(runCopyButton);

            batchProcessButton = new Button
            {
                Content = "Run Batch Process",
                Width = 150,
                Background = new SolidColorBrush(Color.FromRgb(16, 124, 16)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Padding = new Thickness(5),
                IsEnabled = false,
                Visibility = Visibility.Collapsed
            };
            batchProcessButton.Click += OnBatchProcessClick;
            buttonPanel.Children.Add(batchProcessButton);

            Grid.SetRow(buttonPanel, 0);
            progressPanel.Children.Add(buttonPanel);

            progressBar = new ProgressBar
            {
                Height = 20,
                Margin = new Thickness(0, 5, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            Grid.SetRow(progressBar, 1);
            progressPanel.Children.Add(progressBar);

            Grid.SetRow(progressPanel, 2);
            grid.Children.Add(progressPanel);

            // Row 3: Status log
            var logPanel = new Border
            {
                Margin = new Thickness(5),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20))
            };

            var logStack = new StackPanel { Margin = new Thickness(5) };

            var logLabel = new TextBlock
            {
                Text = "Status Log:",
                Foreground = Brushes.Cyan,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            logStack.Children.Add(logLabel);

            statusLog = new TextBox
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
                Text = "Ready. Select source and destination files to begin."
            };
            logStack.Children.Add(statusLog);

            logPanel.Child = logStack;
            Grid.SetRow(logPanel, 3);
            grid.Children.Add(logPanel);

            // Row 4: Status label
            statusLabel = new TextBlock
            {
                Foreground = Brushes.Yellow,
                Margin = new Thickness(5),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Text = "Select files to begin"
            };
            Grid.SetRow(statusLabel, 4);
            grid.Children.Add(statusLabel);

            Content = grid;
        }

        private StackPanel BuildSourcePanel()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 5, 0) };

            var header = new TextBlock
            {
                Text = "Source Excel File(s):",
                Foreground = Brushes.Cyan,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(header);

            // Single file mode controls
            var singleFilePanel = new StackPanel { Name = "SingleFilePanel" };

            var filePanel = new Grid();
            filePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            sourceFileText = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                IsReadOnly = true,
                Text = "No file selected",
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(sourceFileText, 0);
            filePanel.Children.Add(sourceFileText);

            browseSourceButton = new Button
            {
                Content = "Browse...",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(5)
            };
            browseSourceButton.Click += OnBrowseSourceClick;
            Grid.SetColumn(browseSourceButton, 1);
            filePanel.Children.Add(browseSourceButton);

            singleFilePanel.Children.Add(filePanel);

            var sheetLabel = new TextBlock
            {
                Text = "Source Worksheet Name:",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 3)
            };
            singleFilePanel.Children.Add(sheetLabel);

            sourceSheetText = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Text = "SVI-CAS"
            };
            singleFilePanel.Children.Add(sourceSheetText);

            panel.Children.Add(singleFilePanel);

            // Batch mode controls
            batchSourceFiles = new ObservableCollection<string>();

            var batchPanel = new StackPanel { Name = "BatchPanel", Visibility = Visibility.Collapsed };

            var batchButtonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

            var addFilesButton = new Button
            {
                Content = "Add Files...",
                Width = 90,
                Margin = new Thickness(0, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(5)
            };
            addFilesButton.Click += OnAddBatchFilesClick;
            batchButtonPanel.Children.Add(addFilesButton);

            var clearFilesButton = new Button
            {
                Content = "Clear All",
                Width = 90,
                Background = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(5)
            };
            clearFilesButton.Click += OnClearBatchFilesClick;
            batchButtonPanel.Children.Add(clearFilesButton);

            batchPanel.Children.Add(batchButtonPanel);

            sourceFilesList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Height = 80,
                ItemsSource = batchSourceFiles
            };
            batchPanel.Children.Add(sourceFilesList);

            panel.Children.Add(batchPanel);

            return panel;
        }

        private StackPanel BuildDestinationPanel()
        {
            var panel = new StackPanel { Margin = new Thickness(5, 0, 0, 0) };

            var header = new TextBlock
            {
                Text = "Destination Excel File:",
                Foreground = Brushes.Cyan,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(header);

            var filePanel = new Grid();
            filePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            destFileText = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                IsReadOnly = true,
                Text = "No file selected",
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(destFileText, 0);
            filePanel.Children.Add(destFileText);

            browseDestButton = new Button
            {
                Content = "Browse...",
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(5)
            };
            browseDestButton.Click += OnBrowseDestClick;
            Grid.SetColumn(browseDestButton, 1);
            filePanel.Children.Add(browseDestButton);

            panel.Children.Add(filePanel);

            var sheetLabel = new TextBlock
            {
                Text = "Destination Worksheet Name:",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 3)
            };
            panel.Children.Add(sheetLabel);

            destSheetText = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Text = "Output"
            };
            panel.Children.Add(destSheetText);

            return panel;
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

            ExcelAutomationService.Instance.StatusChanged += OnAutomationStatusChanged;
            ExcelAutomationService.Instance.ProgressChanged += OnAutomationProgressChanged;
        }

        private void OnProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (profileSelector.SelectedItem is ExcelMappingProfile profile)
            {
                ExcelMappingService.Instance.SetActiveProfile(profile.Id);
                statusLabel.Text = $"Profile: {profile.Name} ({profile.Mappings.Count} fields)";
                UpdateButtonStates();
            }
        }

        private void OnBatchModeChanged(object sender, RoutedEventArgs e)
        {
            bool isBatch = batchModeCheckbox.IsChecked == true;

            // Find the panels
            var singlePanel = FindVisualChild<StackPanel>(Content as Grid, "SingleFilePanel");
            var batchPanel = FindVisualChild<StackPanel>(Content as Grid, "BatchPanel");

            if (singlePanel != null)
                singlePanel.Visibility = isBatch ? Visibility.Collapsed : Visibility.Visible;

            if (batchPanel != null)
                batchPanel.Visibility = isBatch ? Visibility.Visible : Visibility.Collapsed;

            runCopyButton.Visibility = isBatch ? Visibility.Collapsed : Visibility.Visible;
            batchProcessButton.Visibility = isBatch ? Visibility.Visible : Visibility.Collapsed;

            UpdateButtonStates();
        }

        private void OnBrowseSourceClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
                Title = "Select Source Excel File"
            };

            if (dialog.ShowDialog() == true)
            {
                sourceFileText.Text = dialog.FileName;
                AppendLog($"Source file selected: {System.IO.Path.GetFileName(dialog.FileName)}");
                UpdateButtonStates();
            }
        }

        private void OnBrowseDestClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
                Title = "Select Destination Excel File"
            };

            if (dialog.ShowDialog() == true)
            {
                destFileText.Text = dialog.FileName;
                AppendLog($"Destination file selected: {System.IO.Path.GetFileName(dialog.FileName)}");
                UpdateButtonStates();
            }
        }

        private void OnAddBatchFilesClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
                Title = "Select Source Excel Files",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!batchSourceFiles.Contains(file))
                    {
                        batchSourceFiles.Add(file);
                    }
                }
                AppendLog($"Added {dialog.FileNames.Length} file(s) to batch queue");
                UpdateButtonStates();
            }
        }

        private void OnClearBatchFilesClick(object sender, RoutedEventArgs e)
        {
            batchSourceFiles.Clear();
            AppendLog("Cleared batch file queue");
            UpdateButtonStates();
        }

        private void OnRunCopyClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(sourceFileText.Text) || sourceFileText.Text == "No file selected")
            {
                statusLabel.Text = "Please select a source file";
                statusLabel.Foreground = Brushes.Red;
                return;
            }

            if (string.IsNullOrEmpty(destFileText.Text) || destFileText.Text == "No file selected")
            {
                statusLabel.Text = "Please select a destination file";
                statusLabel.Foreground = Brushes.Red;
                return;
            }

            runCopyButton.IsEnabled = false;
            browseSourceButton.IsEnabled = false;
            browseDestButton.IsEnabled = false;
            progressBar.Value = 0;

            AppendLog("========================================");
            AppendLog($"Starting automated copy: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            AppendLog($"Source: {System.IO.Path.GetFileName(sourceFileText.Text)}");
            AppendLog($"Destination: {System.IO.Path.GetFileName(destFileText.Text)}");
            AppendLog("========================================");

            // Run on background thread to avoid blocking UI
            System.Threading.Tasks.Task.Run(() =>
            {
                bool success = ExcelAutomationService.Instance.CopyExcelToExcel(
                    sourceFileText.Text,
                    destFileText.Text,
                    sourceSheetText.Text,
                    destSheetText.Text
                );

                Application.Current.Dispatcher.Invoke(() =>
                {
                    runCopyButton.IsEnabled = true;
                    browseSourceButton.IsEnabled = true;
                    browseDestButton.IsEnabled = true;

                    if (success)
                    {
                        statusLabel.Text = "Copy completed successfully";
                        statusLabel.Foreground = Brushes.Green;
                        progressBar.Value = 100;
                    }
                    else
                    {
                        statusLabel.Text = "Copy failed - see log for details";
                        statusLabel.Foreground = Brushes.Red;
                    }
                });
            });
        }

        private void OnBatchProcessClick(object sender, RoutedEventArgs e)
        {
            if (batchSourceFiles.Count == 0)
            {
                statusLabel.Text = "No source files in batch queue";
                statusLabel.Foreground = Brushes.Red;
                return;
            }

            if (string.IsNullOrEmpty(destFileText.Text) || destFileText.Text == "No file selected")
            {
                statusLabel.Text = "Please select a destination file";
                statusLabel.Foreground = Brushes.Red;
                return;
            }

            batchProcessButton.IsEnabled = false;
            browseDestButton.IsEnabled = false;
            progressBar.Value = 0;

            AppendLog("========================================");
            AppendLog($"Starting batch process: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            AppendLog($"Files to process: {batchSourceFiles.Count}");
            AppendLog($"Destination: {System.IO.Path.GetFileName(destFileText.Text)}");
            AppendLog("========================================");

            // Run on background thread
            System.Threading.Tasks.Task.Run(() =>
            {
                int successCount = ExcelAutomationService.Instance.BatchCopyExcelToExcel(
                    batchSourceFiles.ToList(),
                    destFileText.Text,
                    sourceSheetText.Text,
                    destSheetText.Text
                );

                Application.Current.Dispatcher.Invoke(() =>
                {
                    batchProcessButton.IsEnabled = true;
                    browseDestButton.IsEnabled = true;

                    statusLabel.Text = $"Batch complete: {successCount}/{batchSourceFiles.Count} files succeeded";
                    statusLabel.Foreground = successCount == batchSourceFiles.Count ? Brushes.Green : Brushes.Yellow;
                    progressBar.Value = 100;
                });
            });
        }

        private void OnAutomationStatusChanged(string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AppendLog(status);
                statusLabel.Text = status;
                statusLabel.Foreground = status.Contains("ERROR") ? Brushes.Red :
                                        status.Contains("SUCCESS") ? Brushes.Green :
                                        Brushes.Yellow;
            });
        }

        private void OnAutomationProgressChanged(int current, int total)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                double percentage = (double)current / total * 100;
                progressBar.Value = percentage;
            });
        }

        private void UpdateButtonStates()
        {
            bool isBatch = batchModeCheckbox.IsChecked == true;

            if (isBatch)
            {
                batchProcessButton.IsEnabled = batchSourceFiles.Count > 0 &&
                                              !string.IsNullOrEmpty(destFileText.Text) &&
                                              destFileText.Text != "No file selected";
            }
            else
            {
                runCopyButton.IsEnabled = !string.IsNullOrEmpty(sourceFileText.Text) &&
                                         sourceFileText.Text != "No file selected" &&
                                         !string.IsNullOrEmpty(destFileText.Text) &&
                                         destFileText.Text != "No file selected";
            }
        }

        private void AppendLog(string message)
        {
            statusLog.AppendText(message + Environment.NewLine);
            statusLog.ScrollToEnd();
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

        private T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element && element.Name == name)
                {
                    return element;
                }

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        protected override void OnDispose()
        {
            ExcelMappingService.Instance.ProfileChanged -= OnServiceProfileChanged;
            ExcelMappingService.Instance.ProfilesLoaded -= OnProfilesLoaded;
            ExcelAutomationService.Instance.StatusChanged -= OnAutomationStatusChanged;
            ExcelAutomationService.Instance.ProgressChanged -= OnAutomationProgressChanged;

            base.OnDispose();
        }
    }
}
