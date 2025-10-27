using System;
using System.Collections.Generic;
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
    /// Widget for editing Excel field mapping profiles
    /// Allows creating, editing, and managing field mappings
    /// </summary>
    public class ExcelMappingEditorWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IExcelMappingService excelService;

        private ComboBox profileComboBox;
        private TextBox profileNameTextBox;
        private TextBox profileDescTextBox;
        private Button newProfileButton;
        private Button deleteProfileButton;
        private Button saveProfileButton;

        private ListBox mappingListBox;
        private Button addMappingButton;
        private Button editMappingButton;
        private Button deleteMappingButton;
        private Button moveUpButton;
        private Button moveDownButton;

        // Mapping editor panel
        private Panel editorPanel;
        private TextBox displayNameTextBox;
        private TextBox cellRefTextBox;
        private TextBox propertyNameTextBox;
        private TextBox categoryTextBox;
        private TextBox dataTypeTextBox;
        private TextBox defaultValueTextBox;
        private CheckBox requiredCheckBox;
        private CheckBox includeInExportCheckBox;
        private Button saveMappingButton;
        private Button cancelMappingButton;

        private TextBlock statusText;

        private ExcelMappingProfile currentProfile;
        private ExcelFieldMapping editingMapping;
        private bool isEditingMapping = false;

        // DI constructor
        public ExcelMappingEditorWidget(
            ILogger logger,
            IThemeManager themeManager,
            IExcelMappingService excelService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));

            WidgetName = "Excel Mapping Editor";
            WidgetType = "ExcelMappingEditorWidget";
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
                Text = "⚙️ Excel Mapping Editor",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(theme.BackgroundSecondary)
            };
            DockPanel.SetDock(header, Dock.Top);
            mainPanel.Children.Add(header);

            // Main content - split layout
            var contentGrid = new Grid
            {
                Margin = new Thickness(10)
            };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

            // Left panel - Profile management and mapping list
            var leftPanel = new DockPanel();
            Grid.SetColumn(leftPanel, 0);

            // Profile selector section
            var profileSection = BuildProfileSection(theme);
            DockPanel.SetDock(profileSection, Dock.Top);
            leftPanel.Children.Add(profileSection);

            // Mapping list section
            var mappingSection = BuildMappingListSection(theme);
            leftPanel.Children.Add(mappingSection);

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

            // Right panel - Mapping editor
            editorPanel = BuildMappingEditorSection(theme);
            Grid.SetColumn(editorPanel, 2);
            editorPanel.IsEnabled = false;
            contentGrid.Children.Add(editorPanel);

            mainPanel.Children.Add(contentGrid);

            // Bottom status bar
            var bottomPanel = new DockPanel
            {
                Margin = new Thickness(10, 0, 10, 10),
                LastChildFill = true
            };

            statusText = new TextBlock
            {
                Text = "Select a profile to begin",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            bottomPanel.Children.Add(statusText);

            DockPanel.SetDock(bottomPanel, Dock.Bottom);
            mainPanel.Children.Add(bottomPanel);

            this.Content = mainPanel;
        }

        private Panel BuildProfileSection(Theme theme)
        {
            var section = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            var titlePanel = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 5)
            };

            var titleText = new TextBlock
            {
                Text = "Profile:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground)
            };
            DockPanel.SetDock(titleText, Dock.Left);
            titlePanel.Children.Add(titleText);

            var buttonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(buttonStack, Dock.Right);

            newProfileButton = new Button
            {
                Content = "+",
                Width = 25,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                Background = new SolidColorBrush(theme.Success),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Success)
            };
            newProfileButton.Click += NewProfileButton_Click;
            buttonStack.Children.Add(newProfileButton);

            deleteProfileButton = new Button
            {
                Content = "✕",
                Width = 25,
                Height = 25,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                Background = new SolidColorBrush(theme.Error),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Error),
                IsEnabled = false
            };
            deleteProfileButton.Click += DeleteProfileButton_Click;
            buttonStack.Children.Add(deleteProfileButton);

            titlePanel.Children.Add(buttonStack);

            DockPanel.SetDock(titlePanel, Dock.Top);
            section.Children.Add(titlePanel);

            profileComboBox = new ComboBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 5)
            };
            profileComboBox.SelectionChanged += ProfileComboBox_SelectionChanged;
            DockPanel.SetDock(profileComboBox, Dock.Top);
            section.Children.Add(profileComboBox);

            // Profile details
            var detailsPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 5)
            };

            profileNameTextBox = new TextBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 5),
                IsEnabled = false
            };
            profileNameTextBox.TextChanged += ProfileDetails_Changed;
            detailsPanel.Children.Add(profileNameTextBox);

            profileDescTextBox = new TextBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 60,
                IsEnabled = false
            };
            profileDescTextBox.TextChanged += ProfileDetails_Changed;
            detailsPanel.Children.Add(profileDescTextBox);

            saveProfileButton = new Button
            {
                Content = "Save Profile",
                Height = 25,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                IsEnabled = false
            };
            saveProfileButton.Click += SaveProfileButton_Click;
            detailsPanel.Children.Add(saveProfileButton);

            DockPanel.SetDock(detailsPanel, Dock.Top);
            section.Children.Add(detailsPanel);

            return section;
        }

        private Panel BuildMappingListSection(Theme theme)
        {
            var section = new DockPanel();

            var titlePanel = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 5)
            };

            var titleText = new TextBlock
            {
                Text = "Field Mappings:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground)
            };
            DockPanel.SetDock(titleText, Dock.Left);
            titlePanel.Children.Add(titleText);

            var buttonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(buttonStack, Dock.Right);

            addMappingButton = new Button
            {
                Content = "+",
                Width = 25,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                Background = new SolidColorBrush(theme.Success),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Success),
                IsEnabled = false
            };
            addMappingButton.Click += AddMappingButton_Click;
            buttonStack.Children.Add(addMappingButton);

            editMappingButton = new Button
            {
                Content = "✎",
                Width = 25,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                IsEnabled = false
            };
            editMappingButton.Click += EditMappingButton_Click;
            buttonStack.Children.Add(editMappingButton);

            deleteMappingButton = new Button
            {
                Content = "✕",
                Width = 25,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                Background = new SolidColorBrush(theme.Error),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Error),
                IsEnabled = false
            };
            deleteMappingButton.Click += DeleteMappingButton_Click;
            buttonStack.Children.Add(deleteMappingButton);

            moveUpButton = new Button
            {
                Content = "↑",
                Width = 25,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                IsEnabled = false
            };
            moveUpButton.Click += MoveUpButton_Click;
            buttonStack.Children.Add(moveUpButton);

            moveDownButton = new Button
            {
                Content = "↓",
                Width = 25,
                Height = 25,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                IsEnabled = false
            };
            moveDownButton.Click += MoveDownButton_Click;
            buttonStack.Children.Add(moveDownButton);

            titlePanel.Children.Add(buttonStack);

            DockPanel.SetDock(titlePanel, Dock.Top);
            section.Children.Add(titlePanel);

            mappingListBox = new ListBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1)
            };
            mappingListBox.SelectionChanged += MappingListBox_SelectionChanged;
            section.Children.Add(mappingListBox);

            return section;
        }

        private Panel BuildMappingEditorSection(Theme theme)
        {
            var section = new DockPanel();

            var titleText = new TextBlock
            {
                Text = "Edit Mapping:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(titleText, Dock.Top);
            section.Children.Add(titleText);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var fieldsPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Display Name
            fieldsPanel.Children.Add(CreateLabel("Display Name:", theme));
            displayNameTextBox = CreateTextBox(theme);
            fieldsPanel.Children.Add(displayNameTextBox);

            // Excel Cell Reference
            fieldsPanel.Children.Add(CreateLabel("Excel Cell (e.g., W3):", theme));
            cellRefTextBox = CreateTextBox(theme);
            fieldsPanel.Children.Add(cellRefTextBox);

            // Project Property Name
            fieldsPanel.Children.Add(CreateLabel("Project Property:", theme));
            propertyNameTextBox = CreateTextBox(theme);
            fieldsPanel.Children.Add(propertyNameTextBox);

            // Category
            fieldsPanel.Children.Add(CreateLabel("Category:", theme));
            categoryTextBox = CreateTextBox(theme);
            fieldsPanel.Children.Add(categoryTextBox);

            // Data Type
            fieldsPanel.Children.Add(CreateLabel("Data Type:", theme));
            dataTypeTextBox = CreateTextBox(theme);
            dataTypeTextBox.Text = "String";
            fieldsPanel.Children.Add(dataTypeTextBox);

            // Default Value
            fieldsPanel.Children.Add(CreateLabel("Default Value (optional):", theme));
            defaultValueTextBox = CreateTextBox(theme);
            fieldsPanel.Children.Add(defaultValueTextBox);

            // Required checkbox
            requiredCheckBox = new CheckBox
            {
                Content = "Required",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 10, 0, 5)
            };
            fieldsPanel.Children.Add(requiredCheckBox);

            // Include in export checkbox
            includeInExportCheckBox = new CheckBox
            {
                Content = "Include in Export",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 15)
            };
            fieldsPanel.Children.Add(includeInExportCheckBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            saveMappingButton = new Button
            {
                Content = "Save",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary)
            };
            saveMappingButton.Click += SaveMappingButton_Click;
            buttonPanel.Children.Add(saveMappingButton);

            cancelMappingButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border)
            };
            cancelMappingButton.Click += CancelMappingButton_Click;
            buttonPanel.Children.Add(cancelMappingButton);

            fieldsPanel.Children.Add(buttonPanel);

            scrollViewer.Content = fieldsPanel;
            section.Children.Add(scrollViewer);

            return section;
        }

        private TextBlock CreateLabel(string text, Theme theme)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 5, 0, 2)
            };
        }

        private TextBox CreateTextBox(Theme theme)
        {
            return new TextBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 5)
            };
        }

        public override void Initialize()
        {
            try
            {
                LoadProfiles();
                logger.Info("ExcelMappingEditor", "Widget initialized");
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMappingEditor", $"Initialization failed: {ex.Message}", ex);
                throw; // Re-throw to let ErrorBoundary handle it
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

                logger.Info("ExcelMappingEditor", $"Loaded {profiles.Count} profiles");
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMappingEditor", $"Failed to load profiles: {ex.Message}", ex);
            }
        }

        private void LoadProfileDetails()
        {
            if (currentProfile == null) return;

            profileNameTextBox.Text = currentProfile.Name;
            profileDescTextBox.Text = currentProfile.Description;

            profileNameTextBox.IsEnabled = true;
            profileDescTextBox.IsEnabled = true;
            deleteProfileButton.IsEnabled = true;
            addMappingButton.IsEnabled = true;

            LoadMappings();
        }

        private void LoadMappings()
        {
            mappingListBox.Items.Clear();

            if (currentProfile == null) return;

            var sortedMappings = currentProfile.Mappings.OrderBy(m => m.SortOrder).ToList();

            foreach (var mapping in sortedMappings)
            {
                var displayText = $"{mapping.DisplayName} ({mapping.ExcelCellRef} → {mapping.ProjectPropertyName})";
                if (mapping.IncludeInExport)
                    displayText += " [Export]";

                mappingListBox.Items.Add(new ListBoxItem
                {
                    Content = displayText,
                    Tag = mapping,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas")
                });
            }

            statusText.Text = $"Profile: {currentProfile.Name} | {currentProfile.Mappings.Count} mappings";
            statusText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.ForegroundSecondary);
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (profileComboBox.SelectedItem is string selectedName)
            {
                var profiles = excelService.GetAllProfiles();
                currentProfile = profiles.FirstOrDefault(p => p.Name == selectedName);
                LoadProfileDetails();
            }
        }

        private void ProfileDetails_Changed(object sender, TextChangedEventArgs e)
        {
            if (currentProfile == null) return;
            saveProfileButton.IsEnabled = true;
        }

        private void NewProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = new ExcelMappingProfile
            {
                Name = $"Profile {DateTime.Now:yyyyMMdd_HHmmss}",
                Description = "New profile"
            };

            excelService.SaveProfile(profile);
            LoadProfiles();
            profileComboBox.SelectedItem = profile.Name;

            statusText.Text = $"Created new profile: {profile.Name}";
            statusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile == null) return;

            var result = MessageBox.Show(
                $"Delete profile '{currentProfile.Name}'?\n\nThis cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    excelService.DeleteProfile(currentProfile.Id);
                    currentProfile = null;
                    LoadProfiles();

                    statusText.Text = "Profile deleted";
                    statusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile == null) return;

            currentProfile.Name = profileNameTextBox.Text;
            currentProfile.Description = profileDescTextBox.Text;

            excelService.SaveProfile(currentProfile);
            LoadProfiles();
            profileComboBox.SelectedItem = currentProfile.Name;

            saveProfileButton.IsEnabled = false;
            statusText.Text = "Profile saved";
            statusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
        }

        private void AddMappingButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile == null) return;

            editingMapping = new ExcelFieldMapping();
            isEditingMapping = false;

            ClearMappingEditor();
            editorPanel.IsEnabled = true;

            statusText.Text = "Creating new mapping...";
            statusText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.Info);
        }

        private void EditMappingButton_Click(object sender, RoutedEventArgs e)
        {
            if (mappingListBox.SelectedItem is ListBoxItem lbi && lbi.Tag is ExcelFieldMapping mapping)
            {
                editingMapping = mapping;
                isEditingMapping = true;

                LoadMappingToEditor(mapping);
                editorPanel.IsEnabled = true;

                statusText.Text = $"Editing: {mapping.DisplayName}";
                statusText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.Info);
            }
        }

        private void DeleteMappingButton_Click(object sender, RoutedEventArgs e)
        {
            if (mappingListBox.SelectedItem is ListBoxItem lbi && lbi.Tag is ExcelFieldMapping mapping)
            {
                var result = MessageBox.Show(
                    $"Delete mapping '{mapping.DisplayName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    excelService.DeleteMapping(mapping.Id);
                    LoadProfileDetails();

                    statusText.Text = $"Deleted: {mapping.DisplayName}";
                    statusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                }
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (mappingListBox.SelectedItem is ListBoxItem lbi && lbi.Tag is ExcelFieldMapping mapping)
            {
                var sortedMappings = currentProfile.Mappings.OrderBy(m => m.SortOrder).ToList();
                int index = sortedMappings.IndexOf(mapping);

                if (index > 0)
                {
                    var temp = sortedMappings[index - 1].SortOrder;
                    sortedMappings[index - 1].SortOrder = mapping.SortOrder;
                    mapping.SortOrder = temp;

                    excelService.SaveProfile(currentProfile);
                    LoadMappings();
                    mappingListBox.SelectedIndex = index - 1;
                }
            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (mappingListBox.SelectedItem is ListBoxItem lbi && lbi.Tag is ExcelFieldMapping mapping)
            {
                var sortedMappings = currentProfile.Mappings.OrderBy(m => m.SortOrder).ToList();
                int index = sortedMappings.IndexOf(mapping);

                if (index < sortedMappings.Count - 1)
                {
                    var temp = sortedMappings[index + 1].SortOrder;
                    sortedMappings[index + 1].SortOrder = mapping.SortOrder;
                    mapping.SortOrder = temp;

                    excelService.SaveProfile(currentProfile);
                    LoadMappings();
                    mappingListBox.SelectedIndex = index + 1;
                }
            }
        }

        private void MappingListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = mappingListBox.SelectedItem != null;
            editMappingButton.IsEnabled = hasSelection;
            deleteMappingButton.IsEnabled = hasSelection;
            moveUpButton.IsEnabled = hasSelection && mappingListBox.SelectedIndex > 0;
            moveDownButton.IsEnabled = hasSelection && mappingListBox.SelectedIndex < mappingListBox.Items.Count - 1;
        }

        private void SaveMappingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(displayNameTextBox.Text))
                {
                    MessageBox.Show("Display Name is required", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(cellRefTextBox.Text))
                {
                    MessageBox.Show("Excel Cell Reference is required", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(propertyNameTextBox.Text))
                {
                    MessageBox.Show("Project Property Name is required", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Update mapping
                editingMapping.DisplayName = displayNameTextBox.Text.Trim();
                editingMapping.ExcelCellRef = cellRefTextBox.Text.Trim().ToUpper();
                editingMapping.ProjectPropertyName = propertyNameTextBox.Text.Trim();
                editingMapping.Category = categoryTextBox.Text.Trim();
                editingMapping.DataType = dataTypeTextBox.Text.Trim();
                editingMapping.DefaultValue = defaultValueTextBox.Text.Trim();
                editingMapping.Required = requiredCheckBox.IsChecked == true;
                editingMapping.IncludeInExport = includeInExportCheckBox.IsChecked == true;
                editingMapping.ModifiedDate = DateTime.Now;

                if (isEditingMapping)
                {
                    excelService.UpdateMapping(editingMapping);
                }
                else
                {
                    excelService.AddMapping(editingMapping);
                }

                LoadProfileDetails();
                CancelMappingButton_Click(null, null);

                statusText.Text = $"Saved: {editingMapping.DisplayName}";
                statusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save mapping: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelMappingButton_Click(object sender, RoutedEventArgs e)
        {
            ClearMappingEditor();
            editorPanel.IsEnabled = false;
            editingMapping = null;
            isEditingMapping = false;

            statusText.Text = $"Profile: {currentProfile?.Name} | {currentProfile?.Mappings.Count ?? 0} mappings";
            statusText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.ForegroundSecondary);
        }

        private void LoadMappingToEditor(ExcelFieldMapping mapping)
        {
            displayNameTextBox.Text = mapping.DisplayName;
            cellRefTextBox.Text = mapping.ExcelCellRef;
            propertyNameTextBox.Text = mapping.ProjectPropertyName;
            categoryTextBox.Text = mapping.Category;
            dataTypeTextBox.Text = mapping.DataType;
            defaultValueTextBox.Text = mapping.DefaultValue;
            requiredCheckBox.IsChecked = mapping.Required;
            includeInExportCheckBox.IsChecked = mapping.IncludeInExport;
        }

        private void ClearMappingEditor()
        {
            displayNameTextBox.Clear();
            cellRefTextBox.Clear();
            propertyNameTextBox.Clear();
            categoryTextBox.Text = "General";
            dataTypeTextBox.Text = "String";
            defaultValueTextBox.Clear();
            requiredCheckBox.IsChecked = false;
            includeInExportCheckBox.IsChecked = false;
        }

        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            if (this.Content is Panel panel)
            {
                panel.Background = new SolidColorBrush(theme.Background);
            }

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
                        continue; // Dynamic color
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
            if (profileNameTextBox != null)
                profileNameTextBox.TextChanged -= ProfileDetails_Changed;
            if (profileDescTextBox != null)
                profileDescTextBox.TextChanged -= ProfileDetails_Changed;
            if (mappingListBox != null)
                mappingListBox.SelectionChanged -= MappingListBox_SelectionChanged;

            // Unsubscribe button events
            if (newProfileButton != null)
                newProfileButton.Click -= NewProfileButton_Click;
            if (deleteProfileButton != null)
                deleteProfileButton.Click -= DeleteProfileButton_Click;
            if (saveProfileButton != null)
                saveProfileButton.Click -= SaveProfileButton_Click;
            if (addMappingButton != null)
                addMappingButton.Click -= AddMappingButton_Click;
            if (editMappingButton != null)
                editMappingButton.Click -= EditMappingButton_Click;
            if (deleteMappingButton != null)
                deleteMappingButton.Click -= DeleteMappingButton_Click;
            if (moveUpButton != null)
                moveUpButton.Click -= MoveUpButton_Click;
            if (moveDownButton != null)
                moveDownButton.Click -= MoveDownButton_Click;
            if (saveMappingButton != null)
                saveMappingButton.Click -= SaveMappingButton_Click;
            if (cancelMappingButton != null)
                cancelMappingButton.Click -= CancelMappingButton_Click;

            base.OnDispose();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            // Ctrl+N to create new mapping
            if (e.Key == Key.N && isCtrl && addMappingButton != null && addMappingButton.IsEnabled)
            {
                AddMappingButton_Click(this, null);
                e.Handled = true;
            }

            // Ctrl+S to save (profile or mapping)
            if (e.Key == Key.S && isCtrl)
            {
                if (editorPanel.IsEnabled && saveMappingButton != null)
                {
                    SaveMappingButton_Click(this, null);
                }
                else if (saveProfileButton != null && saveProfileButton.IsEnabled)
                {
                    SaveProfileButton_Click(this, null);
                }
                e.Handled = true;
            }

            // Escape to cancel editing
            if (e.Key == Key.Escape && editorPanel.IsEnabled)
            {
                CancelMappingButton_Click(this, null);
                e.Handled = true;
            }
        }
    }
}
