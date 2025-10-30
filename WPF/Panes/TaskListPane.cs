using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Globalization;
using SuperTUI.Core;
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
        private readonly IEventBus eventBus;

        // UI Components
        private Grid mainLayout;
        private ListBox taskListBox;
        private Grid quickAddForm;
        private TextBox quickAddTitle;
        private TextBox quickAddDueDate;
        private TextBox quickAddPriority;
        private TextBlock statusBar;
        private TextBlock filterLabel;

        // Inline editing
        private TextBox inlineEditBox;
        private TextBox dateEditBox;
        private TextBox tagEditBox;
        private TaskItemViewModel editingTask;
        private TaskItemViewModel dateEditingTask;
        private TaskItemViewModel tagEditingTask;

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
            ITaskService taskService,
            IEventBus eventBus)
            : base(logger, themeManager, projectContext)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
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
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Column headers
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Task list
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

            // Quick add form (hidden by default)
            quickAddForm = new Grid
            {
                Background = surfaceBrush,
                Visibility = Visibility.Collapsed
            };
            quickAddForm.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // Title
            quickAddForm.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Separator
            quickAddForm.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // DueDate
            quickAddForm.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Separator
            quickAddForm.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Priority

            // Title field
            quickAddTitle = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Padding = new Thickness(8, 4, 8, 4),
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderThickness = new Thickness(1, 1, 0, 1),
                BorderBrush = accentBrush
            };
            quickAddTitle.KeyDown += QuickAddField_KeyDown;
            Grid.SetColumn(quickAddTitle, 0);
            quickAddForm.Children.Add(quickAddTitle);

            // Separator
            var sep1 = new TextBlock
            {
                Text = " | ",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Foreground = borderBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Background = surfaceBrush
            };
            Grid.SetColumn(sep1, 1);
            quickAddForm.Children.Add(sep1);

            // Due date field
            quickAddDueDate = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Padding = new Thickness(8, 4, 8, 4),
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderThickness = new Thickness(0, 1, 0, 1),
                BorderBrush = accentBrush
            };
            quickAddDueDate.KeyDown += QuickAddField_KeyDown;
            Grid.SetColumn(quickAddDueDate, 2);
            quickAddForm.Children.Add(quickAddDueDate);

            // Separator
            var sep2 = new TextBlock
            {
                Text = " | ",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Foreground = borderBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Background = surfaceBrush
            };
            Grid.SetColumn(sep2, 3);
            quickAddForm.Children.Add(sep2);

            // Priority field
            quickAddPriority = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Padding = new Thickness(8, 4, 8, 4),
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderThickness = new Thickness(0, 1, 1, 1),
                BorderBrush = accentBrush,
                Width = 60,
                MaxLength = 1
            };
            quickAddPriority.KeyDown += QuickAddField_KeyDown;
            Grid.SetColumn(quickAddPriority, 4);
            quickAddForm.Children.Add(quickAddPriority);

            Grid.SetRow(quickAddForm, 0);
            grid.Children.Add(quickAddForm);

            // Filter bar
            var filterBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };

            filterLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Foreground = accentBrush,
                Text = GetFilterText()
            };
            filterBar.Children.Add(filterLabel);

            Grid.SetRow(filterBar, 1);
            grid.Children.Add(filterBar);

            // Column headers
            var headerGrid = new Grid
            {
                Background = surfaceBrush,
                Margin = new Thickness(0, 0, 0, 4)
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Checkbox space
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Indent space
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Expand space
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Priority
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120, GridUnitType.Pixel) }); // Due Date
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150, GridUnitType.Pixel) }); // Tags

            // Checkbox space (empty)
            var checkboxSpace = new TextBlock { Width = 30 };
            Grid.SetColumn(checkboxSpace, 0);
            headerGrid.Children.Add(checkboxSpace);

            // Priority header
            var priorityHeader = new TextBlock
            {
                Text = "P",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = dimBrush,
                Margin = new Thickness(0, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(priorityHeader, 3);
            headerGrid.Children.Add(priorityHeader);

            // Title header
            var titleHeader = new TextBlock
            {
                Text = "Title",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = dimBrush,
                Margin = new Thickness(0, 4, 0, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleHeader, 4);
            headerGrid.Children.Add(titleHeader);

            // Due Date header
            var dueDateHeader = new TextBlock
            {
                Text = "Due Date",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = dimBrush,
                Margin = new Thickness(8, 4, 0, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dueDateHeader, 5);
            headerGrid.Children.Add(dueDateHeader);

            // Tags header
            var tagsHeader = new TextBlock
            {
                Text = "Tags",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = dimBrush,
                Margin = new Thickness(8, 4, 0, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tagsHeader, 6);
            headerGrid.Children.Add(tagsHeader);

            Grid.SetRow(headerGrid, 2);
            grid.Children.Add(headerGrid);

            // Task list with virtualization enabled
            taskListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
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

            // Enable virtualization for large lists (10,000+ items)
            VirtualizingPanel.SetIsVirtualizing(taskListBox, true);
            VirtualizingPanel.SetVirtualizationMode(taskListBox, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(taskListBox, ScrollUnit.Pixel);

            Grid.SetRow(taskListBox, 3);
            grid.Children.Add(taskListBox);

            // Status bar
            statusBar = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Foreground = dimBrush,
                Margin = new Thickness(0, 8, 0, 0)
            };
            Grid.SetRow(statusBar, 4);
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
                    FontSize = 18,
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
                    FontSize = 18,
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
                FontSize = 18,
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
                FontSize = 18,
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
                    FontSize = 18,
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
                    FontSize = 18,
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

            statusBar.Text = $"{total} tasks | {completed} completed | {overdue} overdue | A:Add S:Subtask E:Edit D:Delete Space:Toggle Shift+D:Date Shift+T:Tags";
        }

        // Event Handlers

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (taskListBox.SelectedItem is Grid grid && grid.Tag is TaskItemViewModel vm)
            {
                selectedTask = vm;

                // Publish TaskSelectedEvent for cross-pane communication
                eventBus.Publish(new Core.Events.TaskSelectedEvent
                {
                    TaskId = vm.Task.Id,
                    ProjectId = vm.Task.ProjectId,
                    Task = vm.Task,
                    SourceWidget = "TaskListPane"
                });
            }
        }

        private void TaskListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Check for internal command mode (Ctrl+:)
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

            // Shift+D: Edit due date
            if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (selectedTask != null)
                {
                    StartDateEdit();
                    e.Handled = true;
                }
                return;
            }

            // Shift+T: Edit tags
            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (selectedTask != null)
                {
                    StartTagEdit();
                    e.Handled = true;
                }
                return;
            }

            // Single-key shortcuts (no modifiers, when list focused NOT in text input)
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.A:
                        ShowQuickAdd();
                        e.Handled = true;
                        break;
                    case Key.E:
                    case Key.Enter:
                        if (selectedTask != null)
                        {
                            StartInlineEdit();
                            e.Handled = true;
                        }
                        break;
                    case Key.D:
                        DeleteSelectedTask();
                        e.Handled = true;
                        break;
                    case Key.S:
                        if (selectedTask != null)
                        {
                            CreateSubtask();
                            e.Handled = true;
                        }
                        break;
                    case Key.C:
                        if (selectedTask != null)
                        {
                            ToggleTaskComplete(selectedTask.Task.Id);
                            e.Handled = true;
                        }
                        break;
                    case Key.Space:
                        if (selectedTask != null)
                        {
                            ToggleTaskComplete(selectedTask.Task.Id);
                            e.Handled = true;
                        }
                        break;
                    case Key.PageUp:
                        MoveSelectedTaskUp();
                        e.Handled = true;
                        break;
                    case Key.PageDown:
                        MoveSelectedTaskDown();
                        e.Handled = true;
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

        private void QuickAddField_KeyDown(object sender, KeyEventArgs e)
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
            else if (e.Key == Key.Tab)
            {
                // Tab navigation between fields
                if (sender == quickAddTitle)
                {
                    quickAddDueDate.Focus();
                    e.Handled = true;
                }
                else if (sender == quickAddDueDate)
                {
                    quickAddPriority.Focus();
                    e.Handled = true;
                }
                else if (sender == quickAddPriority)
                {
                    quickAddTitle.Focus();
                    e.Handled = true;
                }
            }
        }

        // Task Operations

        private void ShowQuickAdd()
        {
            quickAddForm.Visibility = Visibility.Visible;
            quickAddTitle.Text = string.Empty;
            quickAddDueDate.Text = string.Empty;
            quickAddPriority.Text = "2"; // Default to Medium
            quickAddTitle.Focus();
        }

        private void HideQuickAdd()
        {
            quickAddForm.Visibility = Visibility.Collapsed;
            taskListBox.Focus();
        }

        private void CreateTaskFromQuickAdd()
        {
            var title = quickAddTitle.Text.Trim();
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

            // Parse due date
            var dueDateText = quickAddDueDate.Text.Trim();
            if (!string.IsNullOrEmpty(dueDateText))
            {
                var parsedDate = ParseDateInput(dueDateText);
                if (parsedDate.HasValue)
                {
                    task.DueDate = parsedDate.Value;
                }
            }

            // Parse priority (1=High, 2=Medium, 3=Low)
            var priorityText = quickAddPriority.Text.Trim();
            if (!string.IsNullOrEmpty(priorityText))
            {
                task.Priority = priorityText switch
                {
                    "1" => TaskPriority.High,
                    "2" => TaskPriority.Medium,
                    "3" => TaskPriority.Low,
                    _ => TaskPriority.Medium
                };
            }

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

            // Store the original item
            var originalItem = taskListBox.Items[selectedIndex];

            // Create NEW inline edit box each time (avoid "already has logical parent" error)
            inlineEditBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Padding = new Thickness(6, 2, 6, 2),
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderThickness = new Thickness(1),
                BorderBrush = accentBrush,
                Tag = originalItem  // Store original to restore on cancel
            };
            inlineEditBox.KeyDown += InlineEditBox_KeyDown;
            inlineEditBox.LostFocus += InlineEditBox_LostFocus;

            editingTask = selectedTask;
            inlineEditBox.Text = selectedTask.Task.Title;

            // Remove old item and insert edit box (avoids parenting error)
            taskListBox.Items.RemoveAt(selectedIndex);
            taskListBox.Items.Insert(selectedIndex, inlineEditBox);
            taskListBox.SelectedIndex = selectedIndex;

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

        // Date editing methods
        private void StartDateEdit()
        {
            if (selectedTask == null)
                return;

            // Find the selected item in the ListBox
            var selectedIndex = taskListBox.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= taskListBox.Items.Count)
                return;

            // Store the original item
            var originalItem = taskListBox.Items[selectedIndex];

            // Create NEW date edit box each time (avoid parenting error)
            dateEditBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Padding = new Thickness(6, 2, 6, 2),
                Background = surfaceBrush,
                Foreground = accentBrush,
                BorderThickness = new Thickness(1),
                BorderBrush = accentBrush,
                Tag = originalItem  // Store original to restore on cancel
            };
            dateEditBox.KeyDown += DateEditBox_KeyDown;
            dateEditBox.LostFocus += DateEditBox_LostFocus;

            dateEditingTask = selectedTask;

            // Show current due date or placeholder
            if (selectedTask.Task.DueDate.HasValue)
            {
                dateEditBox.Text = selectedTask.Task.DueDate.Value.ToString("yyyy-MM-dd");
            }
            else
            {
                dateEditBox.Text = "2d, tomorrow, 2025-12-25, next friday, none";
            }

            // Remove old item and insert edit box (avoids parenting error)
            taskListBox.Items.RemoveAt(selectedIndex);
            taskListBox.Items.Insert(selectedIndex, dateEditBox);
            taskListBox.SelectedIndex = selectedIndex;

            dateEditBox.Focus();
            dateEditBox.SelectAll();

            // Update status bar with hints
            statusBar.Text = "📅 Date formats: 2d, tomorrow, 2025-12-25, next friday, mon, none (to clear) | Enter: save | Esc: cancel";
        }

        private void DateEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveDateEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelDateEdit();
                e.Handled = true;
            }
        }

        private void DateEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveDateEdit();
        }

        private void SaveDateEdit()
        {
            if (dateEditingTask == null || dateEditBox == null)
                return;

            var dateInput = dateEditBox.Text.Trim().ToLowerInvariant();
            DateTime? newDate = ParseDateInput(dateInput);

            var task = taskService.GetTask(dateEditingTask.Task.Id);
            if (task != null)
            {
                task.DueDate = newDate;
                task.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(task);
            }

            dateEditingTask = null;
            RefreshTaskList();
            taskListBox.Focus();
            UpdateStatusBar();
        }

        private void CancelDateEdit()
        {
            dateEditingTask = null;
            RefreshTaskList();
            taskListBox.Focus();
            UpdateStatusBar();
        }

        private DateTime? ParseDateInput(string input)
        {
            if (string.IsNullOrEmpty(input) || input == "none" || input == "clear")
                return null;

            // Try ISO format first: yyyy-MM-dd
            if (DateTime.TryParse(input, out DateTime exactDate))
                return exactDate;

            var today = DateTime.Today;

            // Relative days: "2d", "5d", "30d"
            if (input.EndsWith("d") && int.TryParse(input.Substring(0, input.Length - 1), out int days))
                return today.AddDays(days);

            // Relative weeks: "2w", "3w"
            if (input.EndsWith("w") && int.TryParse(input.Substring(0, input.Length - 1), out int weeks))
                return today.AddDays(weeks * 7);

            // Relative months: "2m", "3m"
            if (input.EndsWith("m") && int.TryParse(input.Substring(0, input.Length - 1), out int months))
                return today.AddMonths(months);

            // Named shortcuts
            switch (input)
            {
                case "today":
                    return today;
                case "tomorrow":
                case "tom":
                    return today.AddDays(1);
                case "yesterday":
                    return today.AddDays(-1);

                // Days of week (next occurrence)
                case "mon":
                case "monday":
                    return GetNextWeekday(DayOfWeek.Monday);
                case "tue":
                case "tuesday":
                    return GetNextWeekday(DayOfWeek.Tuesday);
                case "wed":
                case "wednesday":
                    return GetNextWeekday(DayOfWeek.Wednesday);
                case "thu":
                case "thursday":
                    return GetNextWeekday(DayOfWeek.Thursday);
                case "fri":
                case "friday":
                    return GetNextWeekday(DayOfWeek.Friday);
                case "sat":
                case "saturday":
                    return GetNextWeekday(DayOfWeek.Saturday);
                case "sun":
                case "sunday":
                    return GetNextWeekday(DayOfWeek.Sunday);
            }

            // Prefixed weekdays: "next monday", "next fri"
            if (input.StartsWith("next "))
            {
                var day = input.Substring(5);
                switch (day)
                {
                    case "mon":
                    case "monday":
                        return GetNextWeekday(DayOfWeek.Monday);
                    case "tue":
                    case "tuesday":
                        return GetNextWeekday(DayOfWeek.Tuesday);
                    case "wed":
                    case "wednesday":
                        return GetNextWeekday(DayOfWeek.Wednesday);
                    case "thu":
                    case "thursday":
                        return GetNextWeekday(DayOfWeek.Thursday);
                    case "fri":
                    case "friday":
                        return GetNextWeekday(DayOfWeek.Friday);
                    case "sat":
                    case "saturday":
                        return GetNextWeekday(DayOfWeek.Saturday);
                    case "sun":
                    case "sunday":
                        return GetNextWeekday(DayOfWeek.Sunday);
                }
            }

            // If nothing matched, return null
            return null;
        }

        private DateTime GetNextWeekday(DayOfWeek targetDay)
        {
            var today = DateTime.Today;
            int daysUntilTarget = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;

            // If target is today, go to next week
            if (daysUntilTarget == 0)
                daysUntilTarget = 7;

            return today.AddDays(daysUntilTarget);
        }

        // Tag editing methods
        private void StartTagEdit()
        {
            if (selectedTask == null)
                return;

            // Find the selected item in the ListBox
            var selectedIndex = taskListBox.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= taskListBox.Items.Count)
                return;

            // Store the original item
            var originalItem = taskListBox.Items[selectedIndex];

            // Create NEW tag edit box each time (avoid parenting error)
            tagEditBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Padding = new Thickness(6, 2, 6, 2),
                Background = surfaceBrush,
                Foreground = new SolidColorBrush(Colors.Cyan),  // Cyan for tags
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.Cyan),
                Tag = originalItem  // Store original to restore on cancel
            };
            tagEditBox.KeyDown += TagEditBox_KeyDown;
            tagEditBox.LostFocus += TagEditBox_LostFocus;

            tagEditingTask = selectedTask;

            // Show current tags or placeholder
            if (selectedTask.Task.Tags != null && selectedTask.Task.Tags.Count > 0)
            {
                tagEditBox.Text = string.Join(", ", selectedTask.Task.Tags);
            }
            else
            {
                tagEditBox.Text = "bug, feature, urgent, work, personal";
            }

            // Remove old item and insert edit box (avoids parenting error)
            taskListBox.Items.RemoveAt(selectedIndex);
            taskListBox.Items.Insert(selectedIndex, tagEditBox);
            taskListBox.SelectedIndex = selectedIndex;

            tagEditBox.Focus();
            tagEditBox.SelectAll();

            // Update status bar with hints
            statusBar.Text = "🏷️  Tags: comma-separated (bug, feature, urgent) | Enter: save | Esc: cancel";
        }

        private void TagEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveTagEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelTagEdit();
                e.Handled = true;
            }
        }

        private void TagEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveTagEdit();
        }

        private void SaveTagEdit()
        {
            if (tagEditingTask == null || tagEditBox == null)
                return;

            var tagInput = tagEditBox.Text.Trim();
            var tags = ParseTagInput(tagInput);

            var task = taskService.GetTask(tagEditingTask.Task.Id);
            if (task != null)
            {
                task.Tags = tags;
                task.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(task);
            }

            tagEditingTask = null;
            RefreshTaskList();
            taskListBox.Focus();
            UpdateStatusBar();
        }

        private void CancelTagEdit()
        {
            tagEditingTask = null;
            RefreshTaskList();
            taskListBox.Focus();
            UpdateStatusBar();
        }

        private List<string> ParseTagInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<string>();

            // Split by comma, trim whitespace, remove empties, convert to lowercase
            return input
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim().ToLowerInvariant())
                .Where(tag => !string.IsNullOrEmpty(tag))
                .Distinct()
                .ToList();
        }

        private void ToggleTaskComplete(Guid taskId)
        {
            taskService.ToggleTaskCompletion(taskId);
            RefreshTaskList();
            taskListBox.Focus();
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
                taskListBox.Focus();
            }
        }

        private void CreateSubtask()
        {
            if (selectedTask == null)
                return;

            // Determine parent based on 2-level limit rule:
            // - If selected task is a parent (no parent): Create child under it
            // - If selected task is a child (has parent): Create sibling (same parent as selected)
            Guid? parentId = null;

            if (selectedTask.Task.ParentTaskId.HasValue)
            {
                // Selected task is already a child, create sibling with same parent
                parentId = selectedTask.Task.ParentTaskId.Value;
                logger.Log(LogLevel.Debug, "TaskListPane", $"Creating sibling for child task '{selectedTask.Task.Title}'");
            }
            else
            {
                // Selected task is a parent, create child under it
                parentId = selectedTask.Task.Id;
                logger.Log(LogLevel.Debug, "TaskListPane", $"Creating subtask under parent '{selectedTask.Task.Title}'");
            }

            // Create the new subtask
            var subtask = new TaskItem
            {
                Title = "New Subtask",
                ProjectId = projectContext.CurrentProject?.Id,
                Status = TaskStatus.Pending,
                Priority = TaskPriority.Medium,
                ParentTaskId = parentId
            };

            taskService.AddTask(subtask);
            RefreshTaskList();

            // Select the new subtask and start editing it
            var newTaskVm = taskViewModels.FirstOrDefault(vm => vm.Task.Id == subtask.Id);
            if (newTaskVm != null)
            {
                taskListBox.SelectedItem = newTaskVm;
                selectedTask = newTaskVm;
                StartInlineEdit();
            }
        }

        private void MoveSelectedTaskUp()
        {
            if (selectedTask == null)
                return;

            taskService.MoveTaskUp(selectedTask.Task.Id);
            RefreshTaskList();
            taskListBox.Focus();
        }

        private void MoveSelectedTaskDown()
        {
            if (selectedTask == null)
                return;

            taskService.MoveTaskDown(selectedTask.Task.Id);
            RefreshTaskList();
            taskListBox.Focus();
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
                taskListBox.Focus();
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
