using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Dialogs
{
    /// <summary>
    /// Dialog for editing task tags with autocomplete and suggestions
    /// </summary>
    public class TagEditorDialog : Window
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly TagService tagService;
        private Theme theme;

        // UI Controls
        private ListBox currentTagsListBox;
        private TextBox newTagTextBox;
        private ListBox suggestionsListBox;
        private ListBox popularTagsListBox;
        private Button addButton;
        private Button removeButton;
        private Button okButton;
        private Button cancelButton;

        // Data
        private ObservableCollection<string> currentTags;
        private ObservableCollection<string> suggestions;
        private ObservableCollection<string> popularTags;

        public List<string> Tags { get; private set; }

        public TagEditorDialog(List<string> initialTags, ILogger logger, IThemeManager themeManager, TagService tagService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));

            this.Tags = new List<string>(initialTags ?? new List<string>());

            currentTags = new ObservableCollection<string>(this.Tags);
            suggestions = new ObservableCollection<string>();
            popularTags = new ObservableCollection<string>();

            BuildUI();
            LoadPopularTags();
        }

        private void BuildUI()
        {
            theme = themeManager.CurrentTheme;

            Title = "Edit Tags";
            Width = 600;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            Background = new SolidColorBrush(theme.Background);
            Foreground = new SolidColorBrush(theme.Foreground);

            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Content area (row 0)
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left panel: Current tags
            var leftPanel = BuildCurrentTagsPanel();
            Grid.SetColumn(leftPanel, 0);
            contentGrid.Children.Add(leftPanel);

            // Right panel: Add new tags
            var rightPanel = BuildAddTagsPanel();
            Grid.SetColumn(rightPanel, 2);
            contentGrid.Children.Add(rightPanel);

            Grid.SetRow(contentGrid, 0);
            mainGrid.Children.Add(contentGrid);

            // Button bar (row 1)
            var buttonBar = BuildButtonBar();
            Grid.SetRow(buttonBar, 1);
            mainGrid.Children.Add(buttonBar);

            Content = mainGrid;
        }

        private Border BuildCurrentTagsPanel()
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };

            var stackPanel = new StackPanel();

            // Header
            var header = new TextBlock
            {
                Text = "Current Tags",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(header);

            // Current tags list
            currentTagsListBox = new ListBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Height = 200,
                ItemsSource = currentTags,
                Margin = new Thickness(0, 0, 0, 5)
            };
            currentTagsListBox.KeyDown += CurrentTagsListBox_KeyDown;
            stackPanel.Children.Add(currentTagsListBox);

            // Remove button
            removeButton = new Button
            {
                Content = "Remove Selected (Del)",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                Padding = new Thickness(5, 2, 5, 2)
            };
            removeButton.Click += RemoveButton_Click;
            stackPanel.Children.Add(removeButton);

            border.Child = stackPanel;
            return border;
        }

        private Border BuildAddTagsPanel()
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };

            var stackPanel = new StackPanel();

            // Header
            var header = new TextBlock
            {
                Text = "Add Tag",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(header);

            // Tag input
            newTagTextBox = new TextBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 5)
            };
            newTagTextBox.TextChanged += NewTagTextBox_TextChanged;
            newTagTextBox.KeyDown += NewTagTextBox_KeyDown;
            stackPanel.Children.Add(newTagTextBox);

            // Add button
            addButton = new Button
            {
                Content = "Add Tag (Enter)",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 0, 0, 10)
            };
            addButton.Click += AddButton_Click;
            stackPanel.Children.Add(addButton);

            // Suggestions header
            var suggestionsHeader = new TextBlock
            {
                Text = "Suggestions (Type to filter)",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 3)
            };
            stackPanel.Children.Add(suggestionsHeader);

            // Suggestions list
            suggestionsListBox = new ListBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Height = 80,
                ItemsSource = suggestions,
                Margin = new Thickness(0, 0, 0, 5)
            };
            suggestionsListBox.MouseDoubleClick += SuggestionsListBox_MouseDoubleClick;
            stackPanel.Children.Add(suggestionsListBox);

            // Popular tags header
            var popularHeader = new TextBlock
            {
                Text = "Popular Tags",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 3)
            };
            stackPanel.Children.Add(popularHeader);

            // Popular tags list
            popularTagsListBox = new ListBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Height = 60,
                ItemsSource = popularTags
            };
            popularTagsListBox.MouseDoubleClick += PopularTagsListBox_MouseDoubleClick;
            stackPanel.Children.Add(popularTagsListBox);

            border.Child = stackPanel;
            return border;
        }

        private StackPanel BuildButtonBar()
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            okButton = new Button
            {
                Content = "OK",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Width = 80,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 5, 0)
            };
            okButton.Click += OkButton_Click;
            stackPanel.Children.Add(okButton);

            cancelButton = new Button
            {
                Content = "Cancel",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Width = 80,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                Padding = new Thickness(10, 3, 10, 3)
            };
            cancelButton.Click += CancelButton_Click;
            stackPanel.Children.Add(cancelButton);

            return stackPanel;
        }

        #region Event Handlers

        private void NewTagTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Update suggestions based on input
            UpdateSuggestions(newTagTextBox.Text);
        }

        private void NewTagTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddTag(newTagTextBox.Text);
                e.Handled = true;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddTag(newTagTextBox.Text);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveSelectedTag();
        }

        private void CurrentTagsListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                RemoveSelectedTag();
                e.Handled = true;
            }
        }

        private void SuggestionsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (suggestionsListBox.SelectedItem is string tag)
            {
                AddTag(tag);
            }
        }

        private void PopularTagsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (popularTagsListBox.SelectedItem is string tag)
            {
                AddTag(tag);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Tags = currentTags.ToList();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region Tag Operations

        private void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            var trimmedTag = tag.Trim();

            // Validate
            var validationError = tagService.ValidateTag(trimmedTag);
            if (!string.IsNullOrEmpty(validationError))
            {
                MessageBox.Show(validationError, "Invalid Tag", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if already exists (case-insensitive)
            if (currentTags.Any(t => string.Equals(t, trimmedTag, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Tag '{trimmedTag}' already exists", "Duplicate Tag",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check max tags
            if (currentTags.Count >= 10)
            {
                MessageBox.Show("Maximum 10 tags per task", "Too Many Tags",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Add tag
            currentTags.Add(trimmedTag);
            newTagTextBox.Clear();
            newTagTextBox.Focus();

            logger?.Debug("TagEditorDialog", $"Added tag: {trimmedTag}");
        }

        private void RemoveSelectedTag()
        {
            if (currentTagsListBox.SelectedItem is string tag)
            {
                currentTags.Remove(tag);
                logger?.Debug("TagEditorDialog", $"Removed tag: {tag}");
            }
        }

        private void UpdateSuggestions(string prefix)
        {
            suggestions.Clear();

            var suggestionList = tagService.GetTagSuggestions(prefix, 10);

            // Filter out tags already in current tags
            foreach (var tag in suggestionList)
            {
                if (!currentTags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
                {
                    suggestions.Add(tag);
                }
            }
        }

        private void LoadPopularTags()
        {
            popularTags.Clear();

            var popular = tagService.GetTagsByUsage(10);
            foreach (var tagInfo in popular)
            {
                // Don't show tags already in current tags
                if (!currentTags.Any(t => string.Equals(t, tagInfo.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    popularTags.Add($"{tagInfo.Name} ({tagInfo.UsageCount})");
                }
            }

            // Initial suggestions (show recent tags)
            UpdateSuggestions(string.Empty);
        }

        #endregion
    }
}
