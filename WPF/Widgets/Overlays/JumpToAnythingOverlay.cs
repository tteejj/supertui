using System;
using System.Collections.Generic;
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
    /// Jump-to-anything overlay (center zone)
    /// Global fuzzy search: tasks, projects, widgets, workspaces
    /// Instant navigation with Ctrl+J
    /// </summary>
    public class JumpToAnythingOverlay : UserControl
    {
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;

        private TextBox searchBox;
        private ListBox resultsList;
        private List<JumpItem> allItems = new List<JumpItem>();

        public event Action<JumpItem> ItemSelected;
        public event Action Cancelled;

        public JumpToAnythingOverlay(
            ITaskService taskService,
            IProjectService projectService,
            List<WidgetBase> availableWidgets = null,
            List<string> workspaceNames = null)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.logger = Logger.Instance;
            this.themeManager = ThemeManager.Instance;

            BuildItemList(availableWidgets, workspaceNames);
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            var mainPanel = new StackPanel
            {
                Background = new SolidColorBrush(theme.Surface),
                Width = 700,
                MaxHeight = 500
            };

            // Title
            var titlePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(20, 15, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var titleText = new TextBlock
            {
                Text = "JUMP TO ANYTHING",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary)
            };

            var countText = new TextBlock
            {
                Text = $" ({allItems.Count} items)",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                Margin = new Thickness(5, 0, 0, 0)
            };

            titlePanel.Children.Add(titleText);
            titlePanel.Children.Add(countText);
            mainPanel.Children.Add(titlePanel);

            // Search box
            searchBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 16,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(10),
                Margin = new Thickness(20, 0, 20, 15)
            };

            searchBox.TextChanged += OnSearchTextChanged;
            searchBox.KeyDown += OnSearchKeyDown;

            mainPanel.Children.Add(searchBox);

            // Results list
            resultsList = new ListBox
            {
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0),
                MaxHeight = 350,
                Margin = new Thickness(20, 0, 20, 0),
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 13
            };

            resultsList.SelectionChanged += OnResultSelected;
            resultsList.KeyDown += OnResultKeyDown;
            resultsList.MouseDoubleClick += OnResultDoubleClick;

            mainPanel.Children.Add(resultsList);

            // Hint text
            var hintText = new TextBlock
            {
                Text = "[↑↓]Navigate [Enter]Jump [Esc]Cancel",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                FontSize = 11,
                Margin = new Thickness(0, 15, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.Children.Add(hintText);

            // Wrap in border
            var border = new Border
            {
                Child = mainPanel,
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5)
            };

            this.Content = border;
            this.Focusable = true;

            // Focus search box on load
            this.Loaded += (s, e) =>
            {
                searchBox.Focus();
                UpdateResults("");  // Show all items initially
            };
        }

        private void BuildItemList(List<WidgetBase> availableWidgets, List<string> workspaceNames)
        {
            // Add all tasks
            var tasks = taskService.GetTasks(t => !t.Deleted);
            foreach (var task in tasks)
            {
                var statusSymbol = task.Status switch
                {
                    TaskStatus.Pending => "○",
                    TaskStatus.InProgress => "◐",
                    TaskStatus.Completed => "●",
                    _ => " "
                };

                var prioritySymbol = task.Priority switch
                {
                    TaskPriority.Today => "‼",
                    TaskPriority.High => "↑",
                    TaskPriority.Medium => "→",
                    TaskPriority.Low => "↓",
                    _ => " "
                };

                allItems.Add(new JumpItem
                {
                    Type = JumpItemType.Task,
                    Name = task.Title,
                    Description = $"{statusSymbol} {prioritySymbol} {task.Priority}",
                    Data = task,
                    Icon = "📋"
                });
            }

            // Add all projects
            var projects = projectService.GetProjects(p => !p.Deleted);
            foreach (var project in projects)
            {
                var taskCount = tasks.Count(t => t.ProjectId == project.Id);
                allItems.Add(new JumpItem
                {
                    Type = JumpItemType.Project,
                    Name = project.Name,
                    Description = $"{taskCount} tasks",
                    Data = project,
                    Icon = "📁"
                });
            }

            // Add widgets
            if (availableWidgets != null)
            {
                foreach (var widget in availableWidgets)
                {
                    allItems.Add(new JumpItem
                    {
                        Type = JumpItemType.Widget,
                        Name = widget.WidgetName,
                        Description = "Widget",
                        Data = widget,
                        Icon = "⚙"
                    });
                }
            }

            // Add workspaces
            if (workspaceNames != null)
            {
                for (int i = 0; i < workspaceNames.Count; i++)
                {
                    allItems.Add(new JumpItem
                    {
                        Type = JumpItemType.Workspace,
                        Name = workspaceNames[i],
                        Description = $"Workspace {i + 1}",
                        Data = i,
                        Icon = "🖥"
                    });
                }
            }

            logger?.Info("JumpToAnythingOverlay", $"Built search index with {allItems.Count} items");
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = searchBox.Text.Trim();
            UpdateResults(searchText);
        }

        private void UpdateResults(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all items grouped by type
                var grouped = allItems
                    .OrderBy(item => item.Type)
                    .ThenBy(item => item.Name)
                    .Take(50)
                    .ToList();

                resultsList.ItemsSource = grouped;
            }
            else
            {
                // Fuzzy search with scoring
                var matches = allItems
                    .Select(item => new
                    {
                        Item = item,
                        Score = FuzzyScore(item.Name, searchText)
                    })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Item.Type)
                    .Take(50)
                    .Select(x => x.Item)
                    .ToList();

                resultsList.ItemsSource = matches;
            }

            // Auto-select first item
            if (resultsList.Items.Count > 0)
            {
                resultsList.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Fuzzy matching score
        /// </summary>
        private int FuzzyScore(string text, string pattern)
        {
            text = text.ToLower();
            pattern = pattern.ToLower();

            // Exact match = highest score
            if (text == pattern)
                return 1000;

            // Starts with = high score
            if (text.StartsWith(pattern))
                return 500;

            // Contains = medium score
            if (text.Contains(pattern))
                return 250;

            // Fuzzy char-by-char matching
            int score = 0;
            int patternIndex = 0;

            for (int i = 0; i < text.Length && patternIndex < pattern.Length; i++)
            {
                if (text[i] == pattern[patternIndex])
                {
                    score += 10;
                    patternIndex++;
                }
            }

            // All chars matched?
            if (patternIndex == pattern.Length)
                return score;

            return 0;  // No match
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Jump to selected item
                if (resultsList.SelectedItem is JumpItem item)
                {
                    JumpToItem(item);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                // Cancel
                Cancelled?.Invoke();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // Move to results list
                if (resultsList.Items.Count > 0)
                {
                    resultsList.Focus();
                    resultsList.SelectedIndex = 0;
                    e.Handled = true;
                }
            }
        }

        private void OnResultKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Jump to selected item
                if (resultsList.SelectedItem is JumpItem item)
                {
                    JumpToItem(item);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                // Return to search box
                searchBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && resultsList.SelectedIndex == 0)
            {
                // At top of list, return to search
                searchBox.Focus();
                searchBox.SelectionStart = searchBox.Text.Length;
                e.Handled = true;
            }
        }

        private void OnResultSelected(object sender, SelectionChangedEventArgs e)
        {
            // Selection changed (arrow keys)
        }

        private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Double-click to jump
            if (resultsList.SelectedItem is JumpItem item)
            {
                JumpToItem(item);
            }
        }

        private void JumpToItem(JumpItem item)
        {
            logger?.Info("JumpToAnythingOverlay", $"Jumping to: {item.Type} - {item.Name}");
            ItemSelected?.Invoke(item);
        }

        #region Data Templates

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // Set custom item template for results
            var theme = themeManager.CurrentTheme;

            var template = new DataTemplate(typeof(JumpItem));
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            factory.SetValue(StackPanel.MarginProperty, new Thickness(5));

            // Icon
            var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
            iconFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Icon"));
            iconFactory.SetValue(TextBlock.FontSizeProperty, 16.0);
            iconFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 10, 0));
            iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Name (bold)
            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
            nameFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            nameFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(theme.Foreground));
            nameFactory.SetValue(TextBlock.MinWidthProperty, 300.0);
            nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Description (dimmed)
            var descFactory = new FrameworkElementFactory(typeof(TextBlock));
            descFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Description"));
            descFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(128, 128, 128)));
            descFactory.SetValue(TextBlock.MarginProperty, new Thickness(10, 0, 10, 0));
            descFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Type badge
            var typeFactory = new FrameworkElementFactory(typeof(Border));
            typeFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(theme.Primary));
            typeFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            typeFactory.SetValue(Border.PaddingProperty, new Thickness(5, 2, 5, 2));

            var typeTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            typeTextFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Type"));
            typeTextFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(theme.Background));
            typeTextFactory.SetValue(TextBlock.FontSizeProperty, 10.0);

            typeFactory.AppendChild(typeTextFactory);

            factory.AppendChild(iconFactory);
            factory.AppendChild(nameFactory);
            factory.AppendChild(descFactory);
            factory.AppendChild(typeFactory);

            template.VisualTree = factory;
            resultsList.ItemTemplate = template;
        }

        #endregion
    }

    #region Jump Item Model

    public enum JumpItemType
    {
        Task,
        Project,
        Widget,
        Workspace
    }

    public class JumpItem
    {
        public JumpItemType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public object Data { get; set; }

        public override string ToString() => $"{Icon} {Name}";
    }

    #endregion
}
