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
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Task management widget with 3-pane layout using purpose-built TaskListControl
    /// Full CRUD with inline editing of all task properties
    /// </summary>
    public class TaskManagementWidget : WidgetBase, IThemeable
    {
        private Theme theme;
        private TaskService taskService;

        // UI Components
        private Grid mainGrid;
        private ListBox filterListBox;
        private TaskListControl taskListControl;
        private StackPanel detailsPanel;

        // Filter state
        private List<TaskFilter> filters;
        private TaskFilter currentFilter;

        // Selection state
        private TaskItem selectedTask;

        // Details panel controls
        private TextBlock detailTitle;
        private TextBox detailDescriptionBox;
        private TextBlock detailStatus;
        private TextBlock detailPriority;
        private TextBlock detailDueDate;
        private TextBlock detailProgress;
        private TextBox detailTagsBox;
        private TextBlock detailCreated;
        private TextBlock detailUpdated;
        private StackPanel subtasksPanel;
        private Button saveDescButton;
        private Button saveTagsButton;

        public TaskManagementWidget()
        {
            Name = "Task Manager";
            WidgetType = "TaskManagement";
        }

        public override void Initialize()
        {
            theme = ThemeManager.Instance.CurrentTheme;
            taskService = TaskService.Instance;

            // Initialize service
            taskService.Initialize();

            // Setup filters
            filters = TaskFilter.GetDefaultFilters();
            currentFilter = TaskFilter.All;

            BuildUI();
            RefreshFilterList();
            LoadCurrentFilter();

            Logger.Instance?.Info("TaskWidget", "Task Management widget initialized");
        }

        private void BuildUI()
        {
            // Main 3-column grid
            mainGrid = new Grid
            {
                Background = new SolidColorBrush(theme.Background)
            };

            // Define columns: Filters (200) | Tasks (flex) | Details (300)
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            // Build each panel
            var filterPanel = BuildFilterPanel();
            var taskPanel = BuildTaskPanel();
            var detailPanel = BuildDetailPanel();

            Grid.SetColumn(filterPanel, 0);
            Grid.SetColumn(taskPanel, 1);
            Grid.SetColumn(detailPanel, 2);

            mainGrid.Children.Add(filterPanel);
            mainGrid.Children.Add(taskPanel);
            mainGrid.Children.Add(detailPanel);

            this.Content = mainGrid;
        }

        #region Filter Panel

        private Border BuildFilterPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(10)
            };

            var stack = new StackPanel();

            // Header
            var header = new TextBlock
            {
                Text = "FILTERS",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(header);

            // Filter list
            filterListBox = new ListBox
            {
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };

            filterListBox.SelectionChanged += FilterListBox_SelectionChanged;
            stack.Children.Add(filterListBox);

            border.Child = stack;
            return border;
        }

        private void RefreshFilterList()
        {
            filterListBox.Items.Clear();

            foreach (var filter in filters)
            {
                var count = taskService.GetTaskCount(filter.Predicate);
                var item = new TextBlock
                {
                    Text = $"{filter.Name} ({count})",
                    Padding = new Thickness(5, 3, 5, 3),
                    Tag = filter
                };

                filterListBox.Items.Add(item);

                if (filter.Name == currentFilter.Name)
                {
                    filterListBox.SelectedItem = item;
                }
            }
        }

        private void FilterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (filterListBox.SelectedItem is TextBlock item && item.Tag is TaskFilter filter)
            {
                currentFilter = filter;
                LoadCurrentFilter();
                Logger.Instance?.Debug("TaskWidget", $"Filter changed to: {filter.Name}");
            }
        }

        private void LoadCurrentFilter()
        {
            if (taskListControl != null)
            {
                taskListControl.LoadTasks(currentFilter.Predicate);
                RefreshFilterList(); // Update counts
            }
        }

        #endregion

        #region Task List Panel

        private Border BuildTaskPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(10)
            };

            taskListControl = new TaskListControl();

            // Subscribe to events
            taskListControl.TaskSelected += OnTaskSelected;
            taskListControl.TaskModified += OnTaskModified;
            taskListControl.TaskAdded += OnTaskAdded;
            taskListControl.TaskDeleted += OnTaskDeleted;

            border.Child = taskListControl;
            return border;
        }

        private void OnTaskSelected(TaskItem task)
        {
            selectedTask = task;
            RefreshDetailPanel();
        }

        private void OnTaskModified(TaskItem task)
        {
            // Refresh filters to update counts
            RefreshFilterList();

            // Refresh details if this is the selected task
            if (selectedTask != null && selectedTask.Id == task.Id)
            {
                selectedTask = taskService.GetTask(task.Id);
                RefreshDetailPanel();
            }
        }

        private void OnTaskAdded(TaskItem task)
        {
            RefreshFilterList();
        }

        private void OnTaskDeleted(Guid taskId)
        {
            RefreshFilterList();

            if (selectedTask != null && selectedTask.Id == taskId)
            {
                selectedTask = null;
                RefreshDetailPanel();
            }
        }

        #endregion

        #region Detail Panel

        private Border BuildDetailPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15)
            };

            detailsPanel = new StackPanel();

            // Header
            var header = new TextBlock
            {
                Text = "TASK DETAILS",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 15)
            };
            detailsPanel.Children.Add(header);

            // Title (read-only, edit in inline editor)
            detailTitle = CreateDetailLabel("", fontSize: 14, bold: true);
            detailsPanel.Children.Add(detailTitle);

            // Description (editable)
            var descLabel = new TextBlock
            {
                Text = "Description:",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 10, 0, 3)
            };
            detailsPanel.Children.Add(descLabel);

            detailDescriptionBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MinHeight = 60,
                MaxHeight = 120,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            detailsPanel.Children.Add(detailDescriptionBox);

            saveDescButton = new Button
            {
                Content = "Save Description",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.Success),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 3, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            saveDescButton.Click += SaveDescription_Click;
            detailsPanel.Children.Add(saveDescButton);

            // Status (read-only, edit in inline editor)
            detailStatus = CreateDetailLabel("");
            detailsPanel.Children.Add(detailStatus);

            // Priority (read-only, edit in inline editor)
            detailPriority = CreateDetailLabel("");
            detailsPanel.Children.Add(detailPriority);

            // Due Date (read-only, edit in inline editor)
            detailDueDate = CreateDetailLabel("");
            detailsPanel.Children.Add(detailDueDate);

            // Progress
            detailProgress = CreateDetailLabel("");
            detailsPanel.Children.Add(detailProgress);

            // Tags (editable)
            var tagsLabel = new TextBlock
            {
                Text = "Tags (comma-separated):",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 10, 0, 3)
            };
            detailsPanel.Children.Add(tagsLabel);

            detailTagsBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };
            detailsPanel.Children.Add(detailTagsBox);

            saveTagsButton = new Button
            {
                Content = "Save Tags",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.Success),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 3, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            saveTagsButton.Click += SaveTags_Click;
            detailsPanel.Children.Add(saveTagsButton);

            // Timestamps
            detailCreated = CreateDetailLabel("", fontSize: 10);
            detailCreated.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            detailsPanel.Children.Add(detailCreated);

            detailUpdated = CreateDetailLabel("", fontSize: 10);
            detailUpdated.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            detailsPanel.Children.Add(detailUpdated);

            // Subtasks section
            var subtasksHeader = new TextBlock
            {
                Text = "SUBTASKS",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 15, 0, 10)
            };
            detailsPanel.Children.Add(subtasksHeader);

            subtasksPanel = new StackPanel();
            detailsPanel.Children.Add(subtasksPanel);

            border.Child = new ScrollViewer
            {
                Content = detailsPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            return border;
        }

        private TextBlock CreateDetailLabel(string text, int fontSize = 12, bool bold = false)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = fontSize,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(theme.Foreground),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private void RefreshDetailPanel()
        {
            if (selectedTask == null)
            {
                detailTitle.Text = "No task selected";
                detailDescriptionBox.Text = "";
                detailDescriptionBox.IsEnabled = false;
                saveDescButton.IsEnabled = false;
                detailStatus.Text = "";
                detailPriority.Text = "";
                detailDueDate.Text = "";
                detailProgress.Text = "";
                detailTagsBox.Text = "";
                detailTagsBox.IsEnabled = false;
                saveTagsButton.IsEnabled = false;
                detailCreated.Text = "";
                detailUpdated.Text = "";
                subtasksPanel.Children.Clear();
                return;
            }

            detailTitle.Text = selectedTask.Title;

            detailDescriptionBox.Text = selectedTask.Description ?? "";
            detailDescriptionBox.IsEnabled = true;
            saveDescButton.IsEnabled = true;

            detailStatus.Text = $"Status: {selectedTask.Status} {selectedTask.StatusIcon}";
            detailStatus.Foreground = new SolidColorBrush(
                selectedTask.Status == TaskStatus.Completed ? theme.Success :
                selectedTask.Status == TaskStatus.InProgress ? theme.Info :
                selectedTask.Status == TaskStatus.Cancelled ? theme.ForegroundDisabled :
                theme.Foreground);

            detailPriority.Text = $"Priority: {selectedTask.Priority} {selectedTask.PriorityIcon}";
            detailPriority.Foreground = new SolidColorBrush(
                selectedTask.Priority == TaskPriority.High ? theme.Error :
                selectedTask.Priority == TaskPriority.Medium ? theme.Warning :
                theme.ForegroundDisabled);

            detailDueDate.Text = selectedTask.DueDate.HasValue ?
                $"Due: {selectedTask.DueDate.Value:yyyy-MM-dd}" : "Due: Not set";
            detailDueDate.Foreground = new SolidColorBrush(
                selectedTask.IsOverdue ? theme.Error : theme.Foreground);

            detailProgress.Text = $"Progress: {selectedTask.Progress}%";

            detailTagsBox.Text = selectedTask.Tags != null && selectedTask.Tags.Any() ?
                string.Join(", ", selectedTask.Tags) : "";
            detailTagsBox.IsEnabled = true;
            saveTagsButton.IsEnabled = true;

            detailCreated.Text = $"Created: {selectedTask.CreatedAt:yyyy-MM-dd HH:mm}";
            detailUpdated.Text = $"Updated: {selectedTask.UpdatedAt:yyyy-MM-dd HH:mm}";

            // Refresh subtasks
            subtasksPanel.Children.Clear();
            var subtasks = taskService.GetSubtasks(selectedTask.Id);

            if (subtasks.Any())
            {
                foreach (var subtask in subtasks)
                {
                    var subtaskText = new TextBlock
                    {
                        Text = $"{subtask.StatusIcon} {subtask.Title}",
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Foreground = new SolidColorBrush(subtask.Status == TaskStatus.Completed ?
                            theme.ForegroundDisabled : theme.Foreground),
                        Margin = new Thickness(0, 2, 0, 2)
                    };
                    subtasksPanel.Children.Add(subtaskText);
                }
            }
            else
            {
                var noSubtasks = new TextBlock
                {
                    Text = "No subtasks",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(theme.ForegroundDisabled)
                };
                subtasksPanel.Children.Add(noSubtasks);
            }
        }

        private void SaveDescription_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask == null) return;

            selectedTask.Description = detailDescriptionBox.Text?.Trim() ?? "";
            selectedTask.UpdatedAt = DateTime.Now;
            taskService.UpdateTask(selectedTask);

            Logger.Instance?.Info("TaskWidget", $"Updated description for: {selectedTask.Title}");
        }

        private void SaveTags_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask == null) return;

            var tagsText = detailTagsBox.Text?.Trim() ?? "";
            selectedTask.Tags = string.IsNullOrWhiteSpace(tagsText)
                ? new List<string>()
                : tagsText.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

            selectedTask.UpdatedAt = DateTime.Now;
            taskService.UpdateTask(selectedTask);

            Logger.Instance?.Info("TaskWidget", $"Updated tags for: {selectedTask.Title}");
        }

        #endregion

        #region Widget Lifecycle

        public override void OnWidgetFocusReceived()
        {
            taskListControl?.Focus();
            Keyboard.Focus(taskListControl);
        }

        public override Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            state["SelectedTaskId"] = selectedTask?.Id.ToString();
            state["CurrentFilter"] = currentFilter?.Name;

            if (taskListControl != null)
            {
                var expanded = taskListControl.GetExpandedTasks();
                state["ExpandedTasks"] = string.Join(",",
                    expanded.Where(kvp => kvp.Value).Select(kvp => kvp.Key.ToString()));
            }

            return state;
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state == null) return;

            if (state.ContainsKey("SelectedTaskId") && state["SelectedTaskId"] is string idStr &&
                Guid.TryParse(idStr, out var id))
            {
                selectedTask = taskService.GetTask(id);
                if (selectedTask == null)
                {
                    Logger.Instance?.Warning("TaskWidget", $"Could not restore task {id}, it may have been deleted");
                }
            }

            if (state.ContainsKey("CurrentFilter") && state["CurrentFilter"] is string filterName)
            {
                currentFilter = filters?.FirstOrDefault(f => f.Name == filterName) ?? TaskFilter.All;
            }

            if (state.ContainsKey("ExpandedTasks") && state["ExpandedTasks"] is string expandedStr && taskListControl != null)
            {
                var expandedDict = new Dictionary<Guid, bool>();
                foreach (var guidStr in expandedStr.Split(','))
                {
                    if (Guid.TryParse(guidStr, out var taskId))
                    {
                        expandedDict[taskId] = true;
                    }
                }
                taskListControl.SetExpandedTasks(expandedDict);
            }

            RefreshFilterList();
            LoadCurrentFilter();
            RefreshDetailPanel();
        }

        protected override void OnDispose()
        {
            // Unsubscribe from UI events
            if (filterListBox != null)
                filterListBox.SelectionChanged -= FilterListBox_SelectionChanged;

            if (taskListControl != null)
            {
                taskListControl.TaskSelected -= OnTaskSelected;
                taskListControl.TaskModified -= OnTaskModified;
                taskListControl.TaskAdded -= OnTaskAdded;
                taskListControl.TaskDeleted -= OnTaskDeleted;
                taskListControl.Dispose();
            }

            Logger.Instance?.Info("TaskWidget", "Task Management widget disposed");
            base.OnDispose();
        }

        public void ApplyTheme()
        {
            theme = ThemeManager.Instance.CurrentTheme;

            if (mainGrid != null)
            {
                mainGrid.Background = new SolidColorBrush(theme.Background);
            }

            taskListControl?.ApplyTheme();
            RefreshDetailPanel();

            Logger.Instance?.Debug("TaskWidget", "Applied theme update");
        }

        #endregion
    }
}
