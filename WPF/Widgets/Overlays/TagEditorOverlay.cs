using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets.Overlays
{
    /// <summary>
    /// Tag editor overlay for managing task tags (center zone)
    /// Provides tag selection and creation interface
    /// </summary>
    public class TagEditorOverlay : UserControl
    {
        private readonly ITaskService taskService;
        private readonly ITagService tagService;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly TaskItem task;

        private TextBox tagsBox;
        private ListBox availableTagsList;
        private TextBlock statusText;

        public event Action<TaskItem> TagsSaved;
        public event Action Cancelled;

        public TagEditorOverlay(TaskItem task, ITaskService taskService, ITagService tagService)
        {
            this.task = task ?? throw new ArgumentNullException(nameof(task));
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            this.logger = Logger.Instance;
            this.themeManager = ThemeManager.Instance;

            BuildUI();
            LoadTags();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            var mainPanel = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(20),
                Margin = new Thickness(100, 50, 100, 50),
                MaxWidth = 700,
                MaxHeight = 600
            };

            var formPanel = new StackPanel();

            // Title
            var titleText = new TextBlock
            {
                Text = $"Edit Tags: {task.Title}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 0, 0, 15)
            };
            formPanel.Children.Add(titleText);

            // Current tags editor
            var tagsLabel = new TextBlock
            {
                Text = "Current Tags (comma-separated):",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Secondary),
                Margin = new Thickness(0, 0, 0, 5)
            };
            formPanel.Children.Add(tagsLabel);

            tagsBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 20)
            };
            formPanel.Children.Add(tagsBox);

            // Available tags list
            var availableLabel = new TextBlock
            {
                Text = "Available Tags (double-click to add):",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Secondary),
                Margin = new Thickness(0, 0, 0, 5)
            };
            formPanel.Children.Add(availableLabel);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 200,
                Margin = new Thickness(0, 0, 0, 15)
            };

            availableTagsList = new ListBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };
            availableTagsList.MouseDoubleClick += OnTagDoubleClick;

            scrollViewer.Content = availableTagsList;
            formPanel.Children.Add(scrollViewer);

            // Hint text
            var hintText = new TextBlock
            {
                Text = "Type new tags separated by commas, or double-click existing tags to add them.\n[Ctrl+S] Save  [Esc] Cancel",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(
                    (byte)(theme.Foreground.R * 0.6),
                    (byte)(theme.Foreground.G * 0.6),
                    (byte)(theme.Foreground.B * 0.6)
                )),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };
            formPanel.Children.Add(hintText);

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var saveButton = new Button
            {
                Content = "Save Tags",
                Width = 120,
                Height = 35,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            saveButton.Click += OnSave;

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 120,
                Height = 35,
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            cancelButton.Click += (s, e) => Cancelled?.Invoke();

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            formPanel.Children.Add(buttonPanel);

            // Status text
            statusText = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Secondary),
                TextWrapping = TextWrapping.Wrap
            };
            formPanel.Children.Add(statusText);

            mainPanel.Child = formPanel;
            this.Content = mainPanel;
            this.Focusable = true;

            // Keyboard shortcuts
            this.KeyDown += OnKeyDown;

            // Focus tags box when loaded
            this.Loaded += (s, e) => tagsBox.Focus();
        }

        private void LoadTags()
        {
            // Load current task tags
            if (task.Tags != null && task.Tags.Any())
            {
                tagsBox.Text = string.Join(", ", task.Tags);
            }

            // Load all available tags
            var allTags = tagService.GetAllTags();
            foreach (var tag in allTags)
            {
                availableTagsList.Items.Add(tag);
            }

            statusText.Text = $"Editing tags for task ID: {task.Id} | {allTags.Count} available tags";
        }

        private void OnTagDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (availableTagsList.SelectedItem is string selectedTag)
            {
                // Add the tag to the tags box
                var currentTags = tagsBox.Text.Trim();
                if (string.IsNullOrEmpty(currentTags))
                {
                    tagsBox.Text = selectedTag;
                }
                else
                {
                    // Check if tag already exists
                    var tags = currentTags.Split(',').Select(t => t.Trim()).ToList();
                    if (!tags.Contains(selectedTag, StringComparer.OrdinalIgnoreCase))
                    {
                        tagsBox.Text = currentTags + ", " + selectedTag;
                    }
                }

                tagsBox.Focus();
                tagsBox.CaretIndex = tagsBox.Text.Length;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S to save
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                OnSave(this, null);
                e.Handled = true;
            }
            // Escape to cancel
            else if (e.Key == Key.Escape)
            {
                Cancelled?.Invoke();
                e.Handled = true;
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            try
            {
                // Parse tags
                if (!string.IsNullOrWhiteSpace(tagsBox.Text))
                {
                    var tags = tagsBox.Text.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();

                    task.Tags = tags;

                    // Tags will be validated and created automatically by SetTaskTags
                    // No need to manually add them to tag service
                }
                else
                {
                    task.Tags = null;
                }

                task.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(task);
                logger?.Info("TagEditorOverlay", $"Updated tags for task: {task.Title}");

                TagsSaved?.Invoke(task);
            }
            catch (Exception ex)
            {
                logger?.Error("TagEditorOverlay", $"Failed to save tags: {ex.Message}");
                statusText.Text = $"Error: {ex.Message}";
                statusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
    }
}
