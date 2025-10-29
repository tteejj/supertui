using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Globalization;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Production-quality task manager pane with full CRUD, subtasks, filtering, keyboard shortcuts
    /// Terminal aesthetic: JetBrains Mono, theme-aware colors, 100% keyboard-driven
    /// NO MOUSE-REQUIRED CONTROLS - All editing is inline with keyboard shortcuts
    /// </summary>
    public class TaskListPane : PaneBase
    {
        // Services
        private readonly ITaskService taskService;

        // UI Components
        private Grid mainLayout;
        private ListBox taskListBox;
        private TextBox quickAddBox;
        private TextBlock statusBar;
        private TextBlock filterLabel;

        // Inline editing
        private TextBox inlineEditBox;
        private TaskItemViewModel editingTask;

        // State
        private List<TaskItemViewModel> taskViewModels = new List<TaskItemViewModel>();
        private TaskItemViewModel selectedTask;
        private FilterMode currentFilter = FilterMode.Active;
        private SortMode currentSort = SortMode.Priority;
        private bool isInternalCommand = false;
        private string commandBuffer = string.Empty;

        // Theme colors (cached for performance)
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush borderBrush;
        private SolidColorBrush dimBrush;
        private SolidColorBrush surfaceBrush;

        // Enums
        private enum FilterMode
        {
            All,
            Active,
            Today,
            ThisWeek,
            Overdue,
            HighPriority,
            ByTag
        }

        private enum SortMode
        {
            Priority,
            DueDate,
            Created,
            Name,
            Manual
        }

        public TaskListPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            ITaskService taskService)
            : base(logger, themeManager, projectContext)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            PaneName = "Tasks";
        }

        public override void Initialize()
        {
            base.Initialize();

            // Set initial focus to task list
            Dispatcher.BeginInvoke(new Action(() =>
            {
                taskListBox?.Focus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override UIElement BuildContent()
        {
            // Cache theme colors
            CacheThemeColors();

            mainLayout = new Grid();
            mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Task list panel
            var taskPanel = BuildTaskListPanel();
            Grid.SetColumn(taskPanel, 0);
            mainLayout.Children.Add(taskPanel);

            // Subscribe to events
            SubscribeToTaskEvents();

            // Load tasks
            RefreshTaskList();

            return mainLayout;
        }

        private void CacheThemeColors()
        {
            var theme = themeManager.CurrentTheme;
            bgBrush = new SolidColorBrush(theme.Background);
            fgBrush = new SolidColorBrush(theme.Foreground);
            accentBrush = new SolidColorBrush(theme.Primary);
            borderBrush = new SolidColorBrush(theme.Border);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
            surfaceBrush = new SolidColorBrush(theme.Surface);
        }

        private Grid BuildTaskListPanel()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Quick add
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filter bar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Task list
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

            // Quick add box (hidden by default)
            quickAddBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(8, 4, 8, 4),
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderThickness = new Thickness(1),
                BorderBrush = accentBrush,
                Visibility = Visibility.Collapsed
            };
            quickAddBox.KeyDown += QuickAddBox_KeyDown;
            Grid.SetRow(quickAddBox, 0);
            grid.Children.Add(quickAddBox);

            // Filter bar
            var filterBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };

            filterLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Foreground = accentBrush,
                Text = GetFilterText()
            };
            filterBar.Children.Add(filterLabel);

            Grid.SetRow(filterBar, 1);
            grid.Children.Add(filterBar);

            // Task list
            taskListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            taskListBox.SelectionChanged += TaskListBox_SelectionChanged;
            taskListBox.KeyDown += TaskListBox_KeyDown;

            // Clean list style
            var itemContainerStyle = new Style(typeof(ListBoxItem));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0, 2, 0, 2)));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.FocusVisualStyleProperty, null));
            taskListBox.ItemContainerStyle = itemContainerStyle;

            var scrollViewer = new ScrollViewer
            {
                Content = taskListBox,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            Grid.SetRow(scrollViewer, 2);
            grid.Children.Add(scrollViewer);

            // Status bar
            statusBar = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 9,
                Foreground = dimBrush,
                Margin = new Thickness(0, 8, 0, 0)
            };
            Grid.SetRow(statusBar, 3);
            grid.Children.Add(statusBar);

            return grid;
        }

        private void SubscribeToTaskEvents()
        {
            taskService.TaskAdded += OnTaskChanged;
            taskService.TaskUpdated += OnTaskChanged;
            taskService.TaskDeleted += OnTaskDeleted;
        }

        private void OnTaskChanged(TaskItem task)
        {
            Application.Current?.Dispatcher.Invoke(() => RefreshTaskList());
        }

        private void OnTaskDeleted(Guid taskId)
        {
            Application.Current?.Dispatcher.Invoke(() => RefreshTaskList());
        }

        protected override void OnProjectContextChanged(object sender, ProjectContextChangedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() => RefreshTaskList());
        }

        private void RefreshTaskList()
        {
            try
            {
                var allTasks = taskService.GetAllTasks(includeDeleted: false);

                // Filter by project context
                if (projectContext.CurrentProject != null)
                {
                    allTasks = allTasks.Where(t => t.ProjectId == projectContext.CurrentProject.Id).ToList();
                }

                // Apply filter
                allTasks = ApplyFilter(allTasks);

                // Build hierarchy
                taskViewModels = BuildTaskHierarchy(allTasks);

                // Apply sort
                taskViewModels = ApplySort(taskViewModels);

                // Update UI
                RenderTaskList();
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                Log($"Error refreshing task list: {ex.Message}", LogLevel.Error);
            }
        }

        private List<TaskItem> ApplyFilter(List<TaskItem> tasks)
        {
            return currentFilter switch
            {
                FilterMode.Active => tasks.Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled).ToList(),
                FilterMode.Today => tasks.Where(t => t.IsDueToday && t.Status != TaskStatus.Completed).ToList(),
                FilterMode.ThisWeek => tasks.Where(t => t.IsDueThisWeek && t.Status != TaskStatus.Completed).ToList(),
                FilterMode.Overdue => tasks.Where(t => t.IsOverdue).ToList(),
                FilterMode.HighPriority => tasks.Where(t => (t.Priority == TaskPriority.High || t.Priority == TaskPriority.Today) && t.Status != TaskStatus.Completed).ToList(),
                _ => tasks
            };
        }

        private List<TaskItemViewModel> BuildTaskHierarchy(List<TaskItem> tasks)
        {
            var viewModels = new List<TaskItemViewModel>();
            var taskDict = tasks.ToDictionary(t => t.Id);

            // Find root tasks (no parent)
            var rootTasks = tasks.Where(t => !t.ParentTaskId.HasValue).ToList();

            foreach (var task in rootTasks)
            {
                var vm = new TaskItemViewModel(task, 0);
                viewModels.Add(vm);

                // Add subtasks recursively
                if (vm.IsExpanded)
                {
                    AddSubtasks(vm, taskDict, viewModels);
                }
            }

            return viewModels;
        }

        private void AddSubtasks(TaskItemViewModel parent, Dictionary<Guid, TaskItem> taskDict, List<TaskItemViewModel> viewModels)
        {
            var subtasks = taskService.GetSubtasks(parent.Task.Id);

            foreach (var subtask in subtasks)
            {
                var vm = new TaskItemViewModel(subtask, parent.IndentLevel + 1);
                viewModels.Add(vm);

                if (vm.IsExpanded)
                {
                    AddSubtasks(vm, taskDict, viewModels);
                }
            }
        }

        private List<TaskItemViewModel> ApplySort(List<TaskItemViewModel> viewModels)
        {
            // Sort only root-level tasks; subtasks maintain their hierarchy
            var rootVMs = viewModels.Where(vm => vm.IndentLevel == 0).ToList();
            var subtaskVMs = viewModels.Where(vm => vm.IndentLevel > 0).ToList();

            rootVMs = currentSort switch
            {
                SortMode.Priority => rootVMs.OrderByDescending(vm => vm.Task.Priority).ThenBy(vm => vm.Task.DueDate).ToList(),
                SortMode.DueDate => rootVMs.OrderBy(vm => vm.Task.DueDate ?? DateTime.MaxValue).ToList(),
                SortMode.Created => rootVMs.OrderByDescending(vm => vm.Task.CreatedAt).ToList(),
                SortMode.Name => rootVMs.OrderBy(vm => vm.Task.Title).ToList(),
                SortMode.Manual => rootVMs.OrderBy(vm => vm.Task.SortOrder).ToList(),
                _ => rootVMs
            };

            // Rebuild list with sorted roots and their subtasks
            var result = new List<TaskItemViewModel>();
            foreach (var root in rootVMs)
            {
                result.Add(root);
                var rootSubtasks = subtaskVMs.Where(vm => IsDescendantOf(vm.Task, root.Task.Id)).ToList();
                result.AddRange(rootSubtasks);
            }

            return result;
        }

        private bool IsDescendantOf(TaskItem task, Guid ancestorId)
        {
            var current = task;
            while (current.ParentTaskId.HasValue)
            {
                if (current.ParentTaskId.Value == ancestorId)
                    return true;
                current = taskService.GetTask(current.ParentTaskId.Value);
                if (current == null)
                    break;
            }
            return false;
        }

        private void RenderTaskList()
        {
            taskListBox.Items.Clear();

            if (!taskViewModels.Any())
            {
                var emptyText = new TextBlock
                {
                    Text = "No tasks. Press Ctrl+N to create one.",
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 11,
                    Foreground = dimBrush,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 20, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                taskListBox.Items.Add(emptyText);
                return;
            }

            foreach (var vm in taskViewModels)
            {
                var item = CreateTaskListItem(vm);
                taskListBox.Items.Add(item);
            }
        }

        private Grid CreateTaskListItem(TaskItemViewModel vm)
        {
            var theme = themeManager.CurrentTheme;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Checkbox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Indent
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Expand icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Priority
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Due date
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Tags

            grid.Tag = vm;

            // Checkbox
            var checkbox = new CheckBox
            {
                IsChecked = vm.Task.Status == TaskStatus.Completed,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            checkbox.Click += (s, e) => ToggleTaskComplete(vm.Task.Id);
            Grid.SetColumn(checkbox, 0);
            grid.Children.Add(checkbox);

            // Indent for subtasks
            if (vm.IndentLevel > 0)
            {
                var indent = new TextBlock
                {
                    Text = new string(' ', vm.IndentLevel * 2),
                    FontFamily = new FontFamily("JetBrains Mono, Consolas")
                };
                Grid.SetColumn(indent, 1);
                grid.Children.Add(indent);
            }

            // Expand/collapse icon for tasks with subtasks
            if (taskService.HasSubtasks(vm.Task.Id))
            {
                var expandIcon = new TextBlock
                {
                    Text = vm.IsExpanded ? "▼" : "▶",
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 9,
                    Foreground = dimBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                Grid.SetColumn(expandIcon, 2);
                grid.Children.Add(expandIcon);
            }

            // Priority icon
            var priorityIcon = new TextBlock
            {
                Text = GetPriorityIcon(vm.Task.Priority),
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                Foreground = GetPriorityColor(vm.Task.Priority),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = $"Priority: {vm.Task.Priority} (Ctrl+1/2/3 to change)"
            };
            Grid.SetColumn(priorityIcon, 3);
            grid.Children.Add(priorityIcon);

            // Title
            var title = new TextBlock
            {
                Text = vm.Task.Title,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                Foreground = GetTaskForeground(vm.Task),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            if (vm.Task.Status == TaskStatus.Completed)
            {
                title.TextDecorations = TextDecorations.Strikethrough;
                title.Foreground = dimBrush;
            }

            Grid.SetColumn(title, 4);
            grid.Children.Add(title);

            // Due date
            if (vm.Task.DueDate.HasValue)
            {
                var dueDate = new TextBlock
                {
                    Text = FormatDueDate(vm.Task),
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 9,
                    Foreground = GetDueDateColor(vm.Task),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(dueDate, 5);
                grid.Children.Add(dueDate);
            }

            // Tags
            if (vm.Task.Tags != null && vm.Task.Tags.Any())
            {
                var tags = new TextBlock
                {
                    Text = string.Join(" ", vm.Task.Tags.Select(t => $"#{t}")),
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 9,
                    Foreground = accentBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(tags, 6);
                grid.Children.Add(tags);
            }

            return grid;
        }

        private string GetPriorityIcon(TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.Low => "↓",
                TaskPriority.Medium => "●",
                TaskPriority.High => "↑",
                TaskPriority.Today => "‼",
                _ => "●"
            };
        }

        private Brush GetPriorityColor(TaskPriority priority)
        {
            var theme = themeManager.CurrentTheme;
            return priority switch
            {
                TaskPriority.Low => dimBrush,
                TaskPriority.Medium => new SolidColorBrush(theme.Info),
                TaskPriority.High => new SolidColorBrush(theme.Warning),
                TaskPriority.Today => new SolidColorBrush(theme.Error),
                _ => fgBrush
            };
        }

        private Brush GetTaskForeground(TaskItem task)
        {
            var theme = themeManager.CurrentTheme;
            if (task.IsOverdue && task.Status != TaskStatus.Completed)
                return new SolidColorBrush(theme.Error);

            return fgBrush;
        }

        private string FormatDueDate(TaskItem task)
        {
            if (!task.DueDate.HasValue)
                return string.Empty;

            if (task.IsDueToday)
                return "TODAY";

            if (task.IsOverdue)
                return $"OVERDUE {task.DueDate.Value:MMM dd}";

            return task.DueDate.Value.ToString("MMM dd");
        }

        private Brush GetDueDateColor(TaskItem task)
        {
            var theme = themeManager.CurrentTheme;
            if (task.IsOverdue && task.Status != TaskStatus.Completed)
                return new SolidColorBrush(theme.Error);

            if (task.IsDueToday)
                return new SolidColorBrush(theme.Warning);

            return dimBrush;
        }

        private string GetFilterText()
        {
            var projectText = projectContext.CurrentProject != null
                ? $"{projectContext.CurrentProject.Name} / "
                : "";

            var filterText = currentFilter switch
            {
                FilterMode.All => "All",
                FilterMode.Active => "Active",
                FilterMode.Today => "Today",
                FilterMode.ThisWeek => "This Week",
                FilterMode.Overdue => "Overdue",
                FilterMode.HighPriority => "High Priority",
                _ => "All"
            };

            var sortText = currentSort switch
            {
                SortMode.Priority => "priority",
                SortMode.DueDate => "due date",
                SortMode.Created => "created",
                SortMode.Name => "name",
                SortMode.Manual => "manual",
                _ => "priority"
            };

            return $"[{projectText}{filterText}] sort: {sortText}";
        }

        private void UpdateStatusBar()
        {
            var total = taskViewModels.Count;
            var completed = taskViewModels.Count(vm => vm.Task.Status == TaskStatus.Completed);
            var overdue = taskViewModels.Count(vm => vm.Task.IsOverdue);

            statusBar.Text = $"{total} tasks | {completed} completed | {overdue} overdue | Ctrl+N: new | F2: edit | Del: delete | Space: toggle | Tab: indent";
        }

        // Event Handlers

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (taskListBox.SelectedItem is Grid grid && grid.Tag is TaskItemViewModel vm)
            {
                selectedTask = vm;
            }
        }

        private void TaskListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Check for internal command mode (changed from Shift+: to Ctrl+:)
            if (e.Key == Key.OemSemicolon && Keyboard.Modifiers == ModifierKeys.Control)
            {
                isInternalCommand = true;
                commandBuffer = ":";
                e.Handled = true;
                return;
            }

            if (isInternalCommand)
            {
                HandleInternalCommand(e);
                return;
            }

            // Normal keyboard shortcuts
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.N:
                        ShowQuickAdd();
                        e.Handled = true;
                        break;
                    case Key.D1:
                        SetSelectedTaskPriority(TaskPriority.High);
                        e.Handled = true;
                        break;
                    case Key.D2:
                        SetSelectedTaskPriority(TaskPriority.Medium);
                        e.Handled = true;
                        break;
                    case Key.D3:
                        SetSelectedTaskPriority(TaskPriority.Low);
                        e.Handled = true;
                        break;
                    case Key.Up:
                        MoveSelectedTaskUp();
                        e.Handled = true;
                        break;
                    case Key.Down:
                        MoveSelectedTaskDown();
                        e.Handled = true;
                        break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.F2:
                    case Key.Enter:
                        if (selectedTask != null)
                        {
                            StartInlineEdit();
                            e.Handled = true;
                        }
                        break;
                    case Key.Delete:
                        DeleteSelectedTask();
                        e.Handled = true;
                        break;
                    case Key.Space:
                        if (selectedTask != null)
                        {
                            ToggleTaskComplete(selectedTask.Task.Id);
                            e.Handled = true;
                        }
                        break;
                    case Key.Tab:
                        if (selectedTask != null)
                        {
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                                UnindentTask();
                            else
                                IndentTask();
                            e.Handled = true;
                        }
                        break;
                }
            }
        }

        private void HandleInternalCommand(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                isInternalCommand = false;
                commandBuffer = string.Empty;
                UpdateStatusBar();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                ExecuteInternalCommand(commandBuffer);
                isInternalCommand = false;
                commandBuffer = string.Empty;
                e.Handled = true;
                return;
            }

            // Build command buffer
            if (e.Key == Key.Back && commandBuffer.Length > 1)
            {
                commandBuffer = commandBuffer.Substring(0, commandBuffer.Length - 1);
            }
            else if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                commandBuffer += e.Key.ToString().ToLower();
            }
            else if (e.Key == Key.Space)
            {
                commandBuffer += " ";
            }

            // Show command in status bar
            statusBar.Text = commandBuffer;
            e.Handled = true;
        }

        private void ExecuteInternalCommand(string command)
        {
            var parts = command.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            var cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "new":
                    ShowQuickAdd();
                    break;
                case "edit":
                    StartInlineEdit();
                    break;
                case "delete":
                case "del":
                    DeleteSelectedTask();
                    break;
                case "complete":
                case "done":
                    if (selectedTask != null)
                        ToggleTaskComplete(selectedTask.Task.Id);
                    break;
                case "filter":
                    if (parts.Length > 1)
                        SetFilter(parts[1]);
                    break;
                case "sort":
                    if (parts.Length > 1)
                        SetSort(parts[1]);
                    break;
                case "priority":
                case "p":
                    if (parts.Length > 1 && selectedTask != null)
                        SetTaskPriorityFromString(parts[1]);
                    break;
            }

            UpdateStatusBar();
        }

        private void SetFilter(string filter)
        {
            currentFilter = filter.ToLower() switch
            {
                "all" => FilterMode.All,
                "active" => FilterMode.Active,
                "today" => FilterMode.Today,
                "week" => FilterMode.ThisWeek,
                "overdue" => FilterMode.Overdue,
                "high" => FilterMode.HighPriority,
                _ => currentFilter
            };

            filterLabel.Text = GetFilterText();
            RefreshTaskList();
        }

        private void SetSort(string sort)
        {
            currentSort = sort.ToLower() switch
            {
                "priority" or "p" => SortMode.Priority,
                "due" or "date" => SortMode.DueDate,
                "created" or "c" => SortMode.Created,
                "name" or "n" => SortMode.Name,
                "manual" or "m" => SortMode.Manual,
                _ => currentSort
            };

            filterLabel.Text = GetFilterText();
            RefreshTaskList();
        }

        private void SetTaskPriorityFromString(string priority)
        {
            var p = priority.ToLower() switch
            {
                "low" or "l" or "3" => TaskPriority.Low,
                "medium" or "m" or "2" => TaskPriority.Medium,
                "high" or "h" or "1" => TaskPriority.High,
                "today" or "t" or "0" => TaskPriority.Today,
                _ => (TaskPriority?)null
            };

            if (p.HasValue)
                SetSelectedTaskPriority(p.Value);
        }

        private void QuickAddBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateTaskFromQuickAdd();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideQuickAdd();
                e.Handled = true;
            }
        }

        // Task Operations

        private void ShowQuickAdd()
        {
            quickAddBox.Visibility = Visibility.Visible;
            quickAddBox.Text = string.Empty;
            quickAddBox.Focus();
        }

        private void HideQuickAdd()
        {
            quickAddBox.Visibility = Visibility.Collapsed;
            taskListBox.Focus();
        }

        private void CreateTaskFromQuickAdd()
        {
            var title = quickAddBox.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                HideQuickAdd();
                return;
            }

            var task = new TaskItem
            {
                Title = title,
                ProjectId = projectContext.CurrentProject?.Id,
                Status = TaskStatus.Pending,
                Priority = TaskPriority.Medium
            };

            taskService.AddTask(task);
            HideQuickAdd();
            RefreshTaskList();
        }

        private void StartInlineEdit()
        {
            if (selectedTask == null)
                return;

            // Find the selected item in the ListBox
            var selectedIndex = taskListBox.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= taskListBox.Items.Count)
                return;

            // Create inline edit box
            if (inlineEditBox == null)
            {
                inlineEditBox = new TextBox
                {
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 11,
                    Padding = new Thickness(6, 2, 6, 2),
                    Background = surfaceBrush,
                    Foreground = fgBrush,
                    BorderThickness = new Thickness(1),
                    BorderBrush = accentBrush
                };
                inlineEditBox.KeyDown += InlineEditBox_KeyDown;
                inlineEditBox.LostFocus += InlineEditBox_LostFocus;
            }

            editingTask = selectedTask;
            inlineEditBox.Text = selectedTask.Task.Title;

            // Replace the task item with the edit box temporarily
            taskListBox.Items[selectedIndex] = inlineEditBox;
            inlineEditBox.Focus();
            inlineEditBox.SelectAll();
        }

        private void InlineEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveInlineEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelInlineEdit();
                e.Handled = true;
            }
        }

        private void InlineEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveInlineEdit();
        }

        private void SaveInlineEdit()
        {
            if (editingTask == null || inlineEditBox == null)
                return;

            var newTitle = inlineEditBox.Text.Trim();
            if (!string.IsNullOrEmpty(newTitle))
            {
                var task = taskService.GetTask(editingTask.Task.Id);
                if (task != null)
                {
                    task.Title = newTitle;
                    task.UpdatedAt = DateTime.Now;
                    taskService.UpdateTask(task);
                }
            }

            editingTask = null;
            RefreshTaskList();
            taskListBox.Focus();
        }

        private void CancelInlineEdit()
        {
            editingTask = null;
            RefreshTaskList();
            taskListBox.Focus();
        }

        private void ToggleTaskComplete(Guid taskId)
        {
            taskService.ToggleTaskCompletion(taskId);
            RefreshTaskList();
        }

        private void SetSelectedTaskPriority(TaskPriority priority)
        {
            if (selectedTask == null)
                return;

            var task = taskService.GetTask(selectedTask.Task.Id);
            if (task != null)
            {
                task.Priority = priority;
                task.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(task);
                RefreshTaskList();
            }
        }

        private void IndentTask()
        {
            if (selectedTask == null || selectedTask.IndentLevel > 0)
                return;

            // Find previous task at same level to become parent
            var index = taskViewModels.IndexOf(selectedTask);
            if (index == 0)
                return;

            var previousTask = taskViewModels[index - 1];

            var task = taskService.GetTask(selectedTask.Task.Id);
            if (task != null)
            {
                task.ParentTaskId = previousTask.Task.Id;
                task.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(task);
                RefreshTaskList();
            }
        }

        private void UnindentTask()
        {
            if (selectedTask == null || !selectedTask.Task.ParentTaskId.HasValue)
                return;

            var task = taskService.GetTask(selectedTask.Task.Id);
            if (task != null)
            {
                task.ParentTaskId = null;
                task.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(task);
                RefreshTaskList();
            }
        }

        private void MoveSelectedTaskUp()
        {
            if (selectedTask == null)
                return;

            taskService.MoveTaskUp(selectedTask.Task.Id);
            RefreshTaskList();
        }

        private void MoveSelectedTaskDown()
        {
            if (selectedTask == null)
                return;

            taskService.MoveTaskDown(selectedTask.Task.Id);
            RefreshTaskList();
        }

        private void DeleteSelectedTask()
        {
            if (selectedTask == null)
                return;

            var result = MessageBox.Show(
                $"Delete task '{selectedTask.Task.Title}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                taskService.DeleteTask(selectedTask.Task.Id);
                RefreshTaskList();
            }
        }

        protected override void OnDispose()
        {
            taskService.TaskAdded -= OnTaskChanged;
            taskService.TaskUpdated -= OnTaskChanged;
            taskService.TaskDeleted -= OnTaskDeleted;
            base.OnDispose();
        }
    }

    // View Model for task list display
    internal class TaskItemViewModel
    {
        public TaskItem Task { get; }
        public int IndentLevel { get; }
        public bool IsExpanded { get; set; }

        public TaskItemViewModel(TaskItem task, int indentLevel)
        {
            Task = task;
            IndentLevel = indentLevel;
            IsExpanded = task.IsExpanded;
        }
    }
}
