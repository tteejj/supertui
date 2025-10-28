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
    /// Filter panel overlay for task filtering (left zone)
    /// Live count updates, checkboxes, keyboard navigation
    /// </summary>
    public class FilterPanelOverlay : UserControl
    {
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;

        private StackPanel mainPanel;
        private Dictionary<string, CheckBox> filterCheckboxes = new Dictionary<string, CheckBox>();
        private Dictionary<string, TextBlock> filterCounts = new Dictionary<string, TextBlock>();

        // Active filters
        private HashSet<TaskStatus> statusFilters = new HashSet<TaskStatus>();
        private HashSet<TaskPriority> priorityFilters = new HashSet<TaskPriority>();
        private HashSet<Guid> projectFilters = new HashSet<Guid>();
        private bool showOverdueOnly = false;
        private bool showTodayOnly = false;

        public event Action<Func<TaskItem, bool>> FilterChanged;

        public FilterPanelOverlay(ITaskService taskService, IProjectService projectService)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.logger = Logger.Instance;
            this.themeManager = ThemeManager.Instance;

            BuildUI();
            CalculateCounts();

            // Subscribe to task changes for live count updates
            taskService.TaskAdded += OnTaskChanged;
            taskService.TaskUpdated += OnTaskChanged;
            taskService.TaskDeleted += OnTaskDeleted;

            // Subscribe to unload event for cleanup
            this.Unloaded += OnOverlayUnloaded;
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            mainPanel = new StackPanel
            {
                Margin = new Thickness(15),
                Background = new SolidColorBrush(theme.Surface)
            };

            // Title
            var titleText = new TextBlock
            {
                Text = "FILTERS",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 0, 0, 15)
            };
            mainPanel.Children.Add(titleText);

            // Status Section
            AddSectionHeader("Status", theme);
            AddFilterCheckbox("all", "All Tasks", true, OnAllTasksChanged, theme);
            AddFilterCheckbox("active", "Active", false, OnStatusFilterChanged, theme);
            AddFilterCheckbox("completed", "Completed", false, OnStatusFilterChanged, theme);

            AddSeparator(theme);

            // Priority Section
            AddSectionHeader("Priority", theme);
            AddFilterCheckbox("priority_today", "‼ Today", false, OnPriorityFilterChanged, theme);
            AddFilterCheckbox("priority_high", "↑ High", false, OnPriorityFilterChanged, theme);
            AddFilterCheckbox("priority_medium", "→ Medium", false, OnPriorityFilterChanged, theme);
            AddFilterCheckbox("priority_low", "↓ Low", false, OnPriorityFilterChanged, theme);

            AddSeparator(theme);

            // Due Date Section
            AddSectionHeader("Due Date", theme);
            AddFilterCheckbox("overdue", "Overdue", false, OnDueDateFilterChanged, theme);
            AddFilterCheckbox("today", "Due Today", false, OnDueDateFilterChanged, theme);
            AddFilterCheckbox("this_week", "This Week", false, OnDueDateFilterChanged, theme);

            AddSeparator(theme);

            // Projects Section
            AddSectionHeader("Projects", theme);
            var projects = projectService.GetProjects(p => !p.Deleted);
            foreach (var project in projects.Take(5))  // Show top 5 projects
            {
                AddFilterCheckbox($"project_{project.Id}", project.Name, false, OnProjectFilterChanged, theme);
            }

            AddSeparator(theme);

            // Actions
            var clearButton = new Button
            {
                Content = "Clear All Filters",
                Margin = new Thickness(0, 10, 0, 0),
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            clearButton.Click += OnClearAllFilters;
            mainPanel.Children.Add(clearButton);

            // Keyboard hints
            var hintsText = new TextBlock
            {
                Text = "[Space]Toggle [↑↓]Navigate [Esc]Close",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                FontSize = 11,
                Margin = new Thickness(0, 15, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.Children.Add(hintsText);

            // Wrap in ScrollViewer
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = mainPanel
            };

            this.Content = scrollViewer;
            this.Focusable = true;
        }

        private void AddSectionHeader(string text, Theme theme)
        {
            var header = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 10, 0, 5)
            };
            mainPanel.Children.Add(header);
        }

        private void AddFilterCheckbox(string key, string label, bool isChecked, RoutedEventHandler handler, Theme theme)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 3)
            };

            var checkbox = new CheckBox
            {
                IsChecked = isChecked,
                Foreground = new SolidColorBrush(theme.Foreground),
                VerticalAlignment = VerticalAlignment.Center
            };
            checkbox.Checked += handler;
            checkbox.Unchecked += handler;

            var labelText = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(5, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var countText = new TextBlock
            {
                Text = "[0]",
                Foreground = new SolidColorBrush(theme.Primary),
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(checkbox);
            panel.Children.Add(labelText);
            panel.Children.Add(countText);
            mainPanel.Children.Add(panel);

            filterCheckboxes[key] = checkbox;
            filterCounts[key] = countText;
        }

        private void AddSeparator(Theme theme)
        {
            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(theme.Border),
                Margin = new Thickness(0, 10, 0, 5)
            };
            mainPanel.Children.Add(separator);
        }

        #region Event Handlers

        private void OnAllTasksChanged(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            if (checkbox?.IsChecked == true)
            {
                // Uncheck all other status filters
                if (filterCheckboxes.ContainsKey("active"))
                    filterCheckboxes["active"].IsChecked = false;
                if (filterCheckboxes.ContainsKey("completed"))
                    filterCheckboxes["completed"].IsChecked = false;

                statusFilters.Clear();
            }

            ApplyFilters();
        }

        private void OnStatusFilterChanged(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            var isChecked = checkbox?.IsChecked == true;

            // Determine which status was toggled
            if (sender == filterCheckboxes.GetValueOrDefault("active"))
            {
                if (isChecked)
                {
                    statusFilters.Add(TaskStatus.Pending);
                    statusFilters.Add(TaskStatus.InProgress);
                    filterCheckboxes["all"].IsChecked = false;
                }
                else
                {
                    statusFilters.Remove(TaskStatus.Pending);
                    statusFilters.Remove(TaskStatus.InProgress);
                }
            }
            else if (sender == filterCheckboxes.GetValueOrDefault("completed"))
            {
                if (isChecked)
                {
                    statusFilters.Add(TaskStatus.Completed);
                    filterCheckboxes["all"].IsChecked = false;
                }
                else
                {
                    statusFilters.Remove(TaskStatus.Completed);
                }
            }

            ApplyFilters();
        }

        private void OnPriorityFilterChanged(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            var isChecked = checkbox?.IsChecked == true;

            if (sender == filterCheckboxes.GetValueOrDefault("priority_today"))
            {
                if (isChecked)
                    priorityFilters.Add(TaskPriority.Today);
                else
                    priorityFilters.Remove(TaskPriority.Today);
            }
            else if (sender == filterCheckboxes.GetValueOrDefault("priority_high"))
            {
                if (isChecked)
                    priorityFilters.Add(TaskPriority.High);
                else
                    priorityFilters.Remove(TaskPriority.High);
            }
            else if (sender == filterCheckboxes.GetValueOrDefault("priority_medium"))
            {
                if (isChecked)
                    priorityFilters.Add(TaskPriority.Medium);
                else
                    priorityFilters.Remove(TaskPriority.Medium);
            }
            else if (sender == filterCheckboxes.GetValueOrDefault("priority_low"))
            {
                if (isChecked)
                    priorityFilters.Add(TaskPriority.Low);
                else
                    priorityFilters.Remove(TaskPriority.Low);
            }

            ApplyFilters();
        }

        private void OnDueDateFilterChanged(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            var isChecked = checkbox?.IsChecked == true;

            if (sender == filterCheckboxes.GetValueOrDefault("overdue"))
            {
                showOverdueOnly = isChecked;
            }
            else if (sender == filterCheckboxes.GetValueOrDefault("today"))
            {
                showTodayOnly = isChecked;
            }

            ApplyFilters();
        }

        private void OnProjectFilterChanged(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            var isChecked = checkbox?.IsChecked == true;

            // Find which project checkbox was toggled
            foreach (var kvp in filterCheckboxes)
            {
                if (kvp.Key.StartsWith("project_") && sender == kvp.Value)
                {
                    var projectIdStr = kvp.Key.Substring("project_".Length);
                    if (Guid.TryParse(projectIdStr, out var projectId))
                    {
                        if (isChecked)
                            projectFilters.Add(projectId);
                        else
                            projectFilters.Remove(projectId);
                    }
                    break;
                }
            }

            ApplyFilters();
        }

        private void OnClearAllFilters(object sender, RoutedEventArgs e)
        {
            // Uncheck all filters
            foreach (var checkbox in filterCheckboxes.Values)
            {
                checkbox.IsChecked = false;
            }

            // Check "All Tasks"
            if (filterCheckboxes.ContainsKey("all"))
            {
                filterCheckboxes["all"].IsChecked = true;
            }

            statusFilters.Clear();
            priorityFilters.Clear();
            projectFilters.Clear();
            showOverdueOnly = false;
            showTodayOnly = false;

            ApplyFilters();
        }

        #endregion

        #region Filter Logic

        private void ApplyFilters()
        {
            // Build composite filter predicate
            Func<TaskItem, bool> filter = task =>
            {
                if (task.Deleted)
                    return false;

                // Status filter
                if (statusFilters.Count > 0 && !statusFilters.Contains(task.Status))
                    return false;

                // Priority filter
                if (priorityFilters.Count > 0 && !priorityFilters.Contains(task.Priority))
                    return false;

                // Project filter
                if (projectFilters.Count > 0 && (!task.ProjectId.HasValue || !projectFilters.Contains(task.ProjectId.Value)))
                    return false;

                // Overdue filter
                if (showOverdueOnly)
                {
                    if (!task.DueDate.HasValue || task.DueDate.Value >= DateTime.Today)
                        return false;
                }

                // Today filter
                if (showTodayOnly)
                {
                    if (!task.DueDate.HasValue || task.DueDate.Value.Date != DateTime.Today)
                        return false;
                }

                return true;
            };

            // Recalculate counts
            CalculateCounts();

            // Notify listeners
            FilterChanged?.Invoke(filter);

            logger?.Debug("FilterPanelOverlay", "Filters applied");
        }

        private void CalculateCounts()
        {
            var allTasks = taskService.GetTasks(t => !t.Deleted);

            // All tasks
            if (filterCounts.ContainsKey("all"))
                filterCounts["all"].Text = $"[{allTasks.Count}]";

            // Active tasks
            var activeCount = allTasks.Count(t => t.Status == TaskStatus.Pending || t.Status == TaskStatus.InProgress);
            if (filterCounts.ContainsKey("active"))
                filterCounts["active"].Text = $"[{activeCount}]";

            // Completed tasks
            var completedCount = allTasks.Count(t => t.Status == TaskStatus.Completed);
            if (filterCounts.ContainsKey("completed"))
                filterCounts["completed"].Text = $"[{completedCount}]";

            // Priority counts
            var todayCount = allTasks.Count(t => t.Priority == TaskPriority.Today);
            if (filterCounts.ContainsKey("priority_today"))
                filterCounts["priority_today"].Text = $"[{todayCount}]";

            var highCount = allTasks.Count(t => t.Priority == TaskPriority.High);
            if (filterCounts.ContainsKey("priority_high"))
                filterCounts["priority_high"].Text = $"[{highCount}]";

            var mediumCount = allTasks.Count(t => t.Priority == TaskPriority.Medium);
            if (filterCounts.ContainsKey("priority_medium"))
                filterCounts["priority_medium"].Text = $"[{mediumCount}]";

            var lowCount = allTasks.Count(t => t.Priority == TaskPriority.Low);
            if (filterCounts.ContainsKey("priority_low"))
                filterCounts["priority_low"].Text = $"[{lowCount}]";

            // Due date counts
            var overdueCount = allTasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Today);
            if (filterCounts.ContainsKey("overdue"))
                filterCounts["overdue"].Text = $"[{overdueCount}]";

            var todayDueCount = allTasks.Count(t => t.DueDate.HasValue && t.DueDate.Value.Date == DateTime.Today);
            if (filterCounts.ContainsKey("today"))
                filterCounts["today"].Text = $"[{todayDueCount}]";

            var thisWeekCount = allTasks.Count(t => t.DueDate.HasValue &&
                t.DueDate.Value >= DateTime.Today &&
                t.DueDate.Value < DateTime.Today.AddDays(7));
            if (filterCounts.ContainsKey("this_week"))
                filterCounts["this_week"].Text = $"[{thisWeekCount}]";

            // Project counts
            foreach (var kvp in filterCounts)
            {
                if (kvp.Key.StartsWith("project_"))
                {
                    var projectIdStr = kvp.Key.Substring("project_".Length);
                    if (Guid.TryParse(projectIdStr, out var projectId))
                    {
                        var projectTaskCount = allTasks.Count(t => t.ProjectId == projectId);
                        kvp.Value.Text = $"[{projectTaskCount}]";
                    }
                }
            }
        }

        #endregion

        #region Live Updates

        private void OnTaskChanged(TaskItem task)
        {
            // Recalculate counts when tasks change
            CalculateCounts();
        }

        private void OnTaskDeleted(Guid taskId)
        {
            // Recalculate counts when task deleted
            CalculateCounts();
        }

        #endregion

        #region Cleanup

        private void OnOverlayUnloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from events
            taskService.TaskAdded -= OnTaskChanged;
            taskService.TaskUpdated -= OnTaskChanged;
            taskService.TaskDeleted -= OnTaskDeleted;
        }

        #endregion

        #region Keyboard Navigation

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Let parent handle Esc to close overlay
            // Space toggles focused checkbox (default WPF behavior)
            // Arrow keys navigate checkboxes (default WPF behavior)

            base.OnKeyDown(e);
        }

        #endregion
    }
}
