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
    /// Terminal aesthetic: JetBrains Mono, green-on-dark, keyboard-first
    /// </summary>
    public class TaskListPane : PaneBase
    {
        // Services
        private readonly ITaskService taskService;

        // UI Components
        private Grid mainLayout;
        private ListBox taskListBox;
        private Grid detailPanel;
        private TextBox quickAddBox;
        private TextBlock statusBar;
        private TextBlock filterLabel;

        // Detail panel controls
        private TextBox detailTitleBox;
        private TextBox detailDescriptionBox;
        private ComboBox detailPriorityCombo;
        private ComboBox detailStatusCombo;
        private DatePicker detailDueDatePicker;
        private TextBox detailTagsBox;
        private Button saveDetailButton;
        private Button closeDetailButton;

        // State
        private List<TaskItemViewModel> taskViewModels = new List<TaskItemViewModel>();
        private TaskItemViewModel selectedTask;
        private TaskItemViewModel editingTask;
        private FilterMode currentFilter = FilterMode.Active;
        private SortMode currentSort = SortMode.Priority;
        private bool showDetailPanel = false;
        private bool isInternalCommand = false;
        private string commandBuffer = string.Empty;

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

        protected override UIElement BuildContent()
        {
            mainLayout = new Grid();
            mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) }); // Detail panel (hidden initially)

            // Left side: Task list
            var leftPanel = BuildTaskListPanel();
            Grid.SetColumn(leftPanel, 0);
            mainLayout.Children.Add(leftPanel);

            // Right side: Detail panel
            detailPanel = BuildDetailPanel();
            Grid.SetColumn(detailPanel, 1);
            mainLayout.Children.Add(detailPanel);

            // Subscribe to events
            SubscribeToTaskEvents();

            // Load tasks
            RefreshTaskList();

            return mainLayout;
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
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
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
            taskListBox.MouseDoubleClick += TaskListBox_MouseDoubleClick;

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
                Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x7A, 0x89)),
                Margin = new Thickness(0, 8, 0, 0)
            };
            Grid.SetRow(statusBar, 3);
            grid.Children.Add(statusBar);

            return grid;
        }

        private Grid BuildDetailPanel()
        {
            var grid = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                Width = 400,
                Margin = new Thickness(12, 0, 0, 0)
            };

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Header
            var header = new TextBlock
            {
                Text = "Task Details",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
                Margin = new Thickness(12, 12, 12, 12)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Content
            var contentScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(12, 0, 12, 0)
            };

            var contentStack = new StackPanel();

            // Title
            contentStack.Children.Add(CreateLabel("Title:"));
            detailTitleBox = CreateTextBox(multiline: false);
            contentStack.Children.Add(detailTitleBox);

            // Description
            contentStack.Children.Add(CreateLabel("Description:"));
            detailDescriptionBox = CreateTextBox(multiline: true, height: 80);
            contentStack.Children.Add(detailDescriptionBox);

            // Priority
            contentStack.Children.Add(CreateLabel("Priority:"));
            detailPriorityCombo = CreateComboBox(new[] { "Low", "Medium", "High", "Today" });
            contentStack.Children.Add(detailPriorityCombo);

            // Status
            contentStack.Children.Add(CreateLabel("Status:"));
            detailStatusCombo = CreateComboBox(new[] { "Pending", "In Progress", "Completed", "Cancelled" });
            contentStack.Children.Add(detailStatusCombo);

            // Due Date
            contentStack.Children.Add(CreateLabel("Due Date:"));
            detailDueDatePicker = new DatePicker
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E))
            };
            contentStack.Children.Add(detailDueDatePicker);

            // Tags
            contentStack.Children.Add(CreateLabel("Tags (comma-separated):"));
            detailTagsBox = CreateTextBox(multiline: false);
            contentStack.Children.Add(detailTagsBox);

            contentScroll.Content = contentStack;
            Grid.SetRow(contentScroll, 1);
            grid.Children.Add(contentScroll);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12)
            };

            saveDetailButton = CreateButton("Save (Ctrl+S)", SaveDetailChanges);
            closeDetailButton = CreateButton("Close (Esc)", CloseDetailPanel);

            buttonPanel.Children.Add(saveDetailButton);
            buttonPanel.Children.Add(closeDetailButton);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            return grid;
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x7A, 0x89)),
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private TextBox CreateTextBox(bool multiline = false, double height = 0)
        {
            var textBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(6, 4, 6, 4),
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 12)
            };

            if (multiline)
            {
                textBox.AcceptsReturn = true;
                textBox.TextWrapping = TextWrapping.Wrap;
                textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }

            if (height > 0)
            {
                textBox.Height = height;
            }

            return textBox;
        }

        private ComboBox CreateComboBox(string[] items)
        {
            var comboBox = new ComboBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E))
            };

            foreach (var item in items)
            {
                comboBox.Items.Add(item);
            }

            return comboBox;
        }

        private Button CreateButton(string content, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = content,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(4, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            button.Click += handler;
            return button;
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
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x7A, 0x89)),
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
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x7A, 0x89)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0),
                    Cursor = Cursors.Hand
                };
                expandIcon.MouseDown += (s, e) => ToggleExpand(vm);
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
                Cursor = Cursors.Hand,
                ToolTip = $"Priority: {vm.Task.Priority}"
            };
            priorityIcon.MouseDown += (s, e) => CyclePriority(vm.Task.Id);
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
                title.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x7A, 0x89));
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
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
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
            return priority switch
            {
                TaskPriority.Low => new SolidColorBrush(Color.FromRgb(0x6C, 0x7A, 0x89)),
                TaskPriority.Medium => new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF)),
                TaskPriority.High => new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0x33)),
                TaskPriority.Today => new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)),
                _ => Brushes.White
            };
        }

        private Brush GetTaskForeground(TaskItem task)
        {
            if (task.IsOverdue && task.Status != TaskStatus.Completed)
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33));

            return Brushes.White;
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
            if (task.IsOverdue && task.Status != TaskStatus.Completed)
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33));

            if (task.IsDueToday)
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0x33));

            return new SolidColorBrush(Color.FromRgb(0x6C, 0x7A, 0x89));
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

            statusBar.Text = $"{total} tasks | {completed} completed | {overdue} overdue | Ctrl+N: new | F2: edit | Del: delete | Space: complete";
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
            // Check for internal command mode
            if (e.Key == Key.OemSemicolon && Keyboard.Modifiers == ModifierKeys.Shift)
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
                    case Key.S:
                        if (showDetailPanel)
                        {
                            SaveDetailChanges(null, null);
                            e.Handled = true;
                        }
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
                            ShowDetailPanel();
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
                    case Key.Escape:
                        if (showDetailPanel)
                        {
                            CloseDetailPanel(null, null);
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
                    ShowDetailPanel();
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

        private void TaskListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (selectedTask != null)
            {
                ShowDetailPanel();
            }
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

        private void ToggleTaskComplete(Guid taskId)
        {
            taskService.ToggleTaskCompletion(taskId);
            RefreshTaskList();
        }

        private void CyclePriority(Guid taskId)
        {
            taskService.CyclePriority(taskId);
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

        private void ToggleExpand(TaskItemViewModel vm)
        {
            vm.IsExpanded = !vm.IsExpanded;
            RefreshTaskList();
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

        // Detail Panel

        private void ShowDetailPanel()
        {
            if (selectedTask == null)
                return;

            editingTask = selectedTask;
            showDetailPanel = true;

            // Load task data into detail panel
            var task = taskService.GetTask(editingTask.Task.Id);
            if (task != null)
            {
                detailTitleBox.Text = task.Title;
                detailDescriptionBox.Text = task.Description ?? string.Empty;
                detailPriorityCombo.SelectedIndex = (int)task.Priority;
                detailStatusCombo.SelectedIndex = (int)task.Status;
                detailDueDatePicker.SelectedDate = task.DueDate;
                detailTagsBox.Text = task.Tags != null ? string.Join(", ", task.Tags) : string.Empty;
            }

            // Show panel
            mainLayout.ColumnDefinitions[1].Width = new GridLength(400);
            detailTitleBox.Focus();
            detailTitleBox.SelectAll();
        }

        private void CloseDetailPanel(object sender, RoutedEventArgs e)
        {
            showDetailPanel = false;
            editingTask = null;
            mainLayout.ColumnDefinitions[1].Width = new GridLength(0);
            taskListBox.Focus();
        }

        private void SaveDetailChanges(object sender, RoutedEventArgs e)
        {
            if (editingTask == null)
                return;

            var task = taskService.GetTask(editingTask.Task.Id);
            if (task == null)
                return;

            // Update task properties
            task.Title = detailTitleBox.Text.Trim();
            task.Description = detailDescriptionBox.Text.Trim();
            task.Priority = (TaskPriority)detailPriorityCombo.SelectedIndex;
            task.Status = (TaskStatus)detailStatusCombo.SelectedIndex;
            task.DueDate = detailDueDatePicker.SelectedDate;
            task.UpdatedAt = DateTime.Now;

            // Parse tags
            var tagsText = detailTagsBox.Text.Trim();
            if (!string.IsNullOrEmpty(tagsText))
            {
                task.Tags = tagsText.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();
            }
            else
            {
                task.Tags.Clear();
            }

            // Mark as completed if status changed
            if (task.Status == TaskStatus.Completed && task.CompletedDate == null)
            {
                task.CompletedDate = DateTime.Now;
            }

            taskService.UpdateTask(task);
            CloseDetailPanel(null, null);
            RefreshTaskList();
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
