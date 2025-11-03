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
using SuperTUI.Core.Commands;
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
        private readonly CommandHistory commandHistory;

        // Event handlers
        private Action<Core.Events.ProjectSelectedEvent> projectSelectedHandler;
        private Action<Core.Events.CommandExecutedFromPaletteEvent> commandExecutedHandler;
        private Action<Core.Events.RefreshRequestedEvent> refreshRequestedHandler;

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
        private Guid? pendingSubtaskParentId = null; // Parent ID for subtask being created via quick add

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
            IEventBus eventBus,
            CommandHistory commandHistory)
            : base(logger, themeManager, projectContext)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.commandHistory = commandHistory ?? throw new ArgumentNullException(nameof(commandHistory));
            PaneName = "Tasks";
        }

        public override void Initialize()
        {
            base.Initialize();

            // Register pane-specific shortcuts with ShortcutManager (migrated from hardcoded handlers)
            RegisterPaneShortcuts();

            // Subscribe to theme changes
            themeManager.ThemeChanged += OnThemeChanged;

            // Subscribe to ProjectSelectedEvent for cross-pane project context awareness
            projectSelectedHandler = OnProjectSelected;
            eventBus.Subscribe(projectSelectedHandler);

            // Subscribe to CommandExecutedFromPaletteEvent for command palette coordination
            commandExecutedHandler = OnCommandExecutedFromPalette;
            eventBus.Subscribe(commandExecutedHandler);

            // Subscribe to RefreshRequestedEvent for global refresh (Ctrl+R)
            refreshRequestedHandler = OnRefreshRequested;
            eventBus.Subscribe(refreshRequestedHandler);

            // Set initial focus to task list
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (taskListBox != null) System.Windows.Input.Keyboard.Focus(taskListBox);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Handle ProjectSelectedEvent from other panes (e.g., ProjectsPane)
        /// Filters tasks to show only those belonging to the selected project
        /// </summary>
        private void OnProjectSelected(Core.Events.ProjectSelectedEvent evt)
        {
            if (evt?.Project == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Update project context (inherited from PaneBase)
                // This will trigger OnProjectContextChanged which calls RefreshTaskList()
                // The RefreshTaskList() method already filters by projectContext.CurrentProject

                logger.Log(LogLevel.Info, "TaskListPane",
                    $"Project selected from {evt.SourceWidget}: {evt.Project.Name}");

                // The filtering happens automatically in RefreshTaskList() via OnProjectContextChanged
                // No additional code needed here - just log for debugging
            });
        }

        /// <summary>
        /// Handle CommandExecutedFromPaletteEvent from command palette
        /// Responds to specific commands by refreshing and selecting tasks
        /// </summary>
        private void OnCommandExecutedFromPalette(Core.Events.CommandExecutedFromPaletteEvent evt)
        {
            if (evt == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Check if command is related to task creation
                var commandLower = evt.CommandName?.ToLower() ?? "";
                if (commandLower == "tasks" || commandLower == "create task" || commandLower == "new task")
                {
                    logger.Log(LogLevel.Info, "TaskListPane",
                        $"Command palette executed '{evt.CommandName}' - refreshing and selecting newest task");

                    // Refresh task list
                    RefreshTaskList();

                    // Select newest task (first in list after refresh)
                    if (taskListBox.Items.Count > 0)
                    {
                        taskListBox.SelectedIndex = 0;
                        taskListBox.ScrollIntoView(taskListBox.Items[0]);
                        System.Windows.Input.Keyboard.Focus(taskListBox);
                    }
                }
            });
        }

        /// <summary>
        /// Register all TaskListPane shortcuts with ShortcutManager
        /// These shortcuts only execute when this pane is focused
        /// </summary>
        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;

            // Ctrl+: (OemSemicolon) - Enter command mode
            shortcuts.RegisterForPane(PaneName, Key.OemSemicolon, ModifierKeys.Control,
                () => { isInternalCommand = true; commandBuffer = ":"; UpdateStatusBar(); },
                "Enter command mode");

            // Shift+D: Edit due date
            shortcuts.RegisterForPane(PaneName, Key.D, ModifierKeys.Shift,
                () => { if (selectedTask != null) StartDateEdit(); },
                "Edit task due date");

            // Shift+T: Edit tags
            shortcuts.RegisterForPane(PaneName, Key.T, ModifierKeys.Shift,
                () => { if (selectedTask != null) StartTagEdit(); },
                "Edit task tags");

            // A (no modifiers): Show quick add form
            shortcuts.RegisterForPane(PaneName, Key.A, ModifierKeys.None,
                () => ShowQuickAdd(),
                "Show quick add form");

            // E (no modifiers): Start inline edit
            shortcuts.RegisterForPane(PaneName, Key.E, ModifierKeys.None,
                () => { if (selectedTask != null) StartInlineEdit(); },
                "Start inline edit");

            // Enter (no modifiers): Start inline edit
            shortcuts.RegisterForPane(PaneName, Key.Enter, ModifierKeys.None,
                () => { if (selectedTask != null) StartInlineEdit(); },
                "Start inline edit");

            // D (no modifiers): Delete selected task
            shortcuts.RegisterForPane(PaneName, Key.D, ModifierKeys.None,
                () => DeleteSelectedTask(),
                "Delete selected task");

            // S (no modifiers): Create subtask
            shortcuts.RegisterForPane(PaneName, Key.S, ModifierKeys.None,
                () => { if (selectedTask != null) CreateSubtask(); },
                "Create subtask");

            // C (no modifiers): Toggle complete
            shortcuts.RegisterForPane(PaneName, Key.C, ModifierKeys.None,
                () => { if (selectedTask != null) ToggleTaskComplete(selectedTask.Task.Id); },
                "Toggle task completion");

            // Space (no modifiers): Toggle complete
            shortcuts.RegisterForPane(PaneName, Key.Space, ModifierKeys.None,
                () => { if (selectedTask != null) ToggleTaskComplete(selectedTask.Task.Id); },
                "Toggle task completion");

            // PageUp (no modifiers): Move selected task up
            shortcuts.RegisterForPane(PaneName, Key.PageUp, ModifierKeys.None,
                () => MoveSelectedTaskUp(),
                "Move task up");

            // PageDown (no modifiers): Move selected task down
            shortcuts.RegisterForPane(PaneName, Key.PageDown, ModifierKeys.None,
                () => MoveSelectedTaskDown(),
                "Move task down");

            // Ctrl+Z: Undo (handled via command history)
            shortcuts.RegisterForPane(PaneName, Key.Z, ModifierKeys.Control,
                () => commandHistory?.Undo(),
                "Undo last command");

            // Ctrl+Y: Redo (handled via command history)
            shortcuts.RegisterForPane(PaneName, Key.Y, ModifierKeys.Control,
                () => commandHistory?.Redo(),
                "Redo last command");
        }

        /// <summary>
        /// Override to handle when pane gains focus - focus appropriate control
        /// </summary>
        protected override void OnPaneGainedFocus()
        {
            // Determine which control should have focus based on current state
            if (inlineEditBox != null && inlineEditBox.Visibility == Visibility.Visible)
            {
                // If editing, return focus to edit box
                System.Windows.Input.Keyboard.Focus(inlineEditBox);
            }
            else if (taskListBox != null)
            {
                // Default: focus the task list
                System.Windows.Input.Keyboard.Focus(taskListBox);
            }
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
            taskService.TaskRestored += OnTaskChanged;
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
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Internal,
                    ex,
                    "Refreshing task list from service",
                    logger);
            }
        }

        private List<TaskItem> ApplyFilter(List<TaskItem> tasks)
        {
            var filtered = currentFilter switch
            {
                FilterMode.Active => tasks.Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled).ToList(),
                FilterMode.Today => tasks.Where(t => t.IsDueToday && t.Status != TaskStatus.Completed).ToList(),
                FilterMode.ThisWeek => tasks.Where(t => t.IsDueThisWeek && t.Status != TaskStatus.Completed).ToList(),
                FilterMode.Overdue => tasks.Where(t => t.IsOverdue).ToList(),
                FilterMode.HighPriority => tasks.Where(t => (t.Priority == TaskPriority.High || t.Priority == TaskPriority.Today) && t.Status != TaskStatus.Completed).ToList(),
                _ => tasks
            };

            return filtered;
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
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Foreground = GetTaskForeground(vm.Task),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Text = vm.Task.Title
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
            // Handle command mode separately (Ctrl+: puts us in command mode)
            if (isInternalCommand)
            {
                HandleInternalCommand(e);
                return;
            }

            // Check if we're typing in a TextBox (single-key shortcuts shouldn't fire during editing)
            if (Keyboard.Modifiers == ModifierKeys.None && System.Windows.Input.Keyboard.FocusedElement is TextBox)
            {
                return; // Let the TextBox handle the key
            }

            // Try to handle via ShortcutManager (all registered pane shortcuts)
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, Keyboard.Modifiers, null, PaneName))
            {
                e.Handled = true;
                return;
            }

            // If not handled by ShortcutManager, leave for default handling
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
                    System.Windows.Input.Keyboard.Focus(quickAddDueDate);
                    e.Handled = true;
                }
                else if (sender == quickAddDueDate)
                {
                    System.Windows.Input.Keyboard.Focus(quickAddPriority);
                    e.Handled = true;
                }
                else if (sender == quickAddPriority)
                {
                    System.Windows.Input.Keyboard.Focus(quickAddTitle);
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
            System.Windows.Input.Keyboard.Focus(quickAddTitle);
        }

        private void HideQuickAdd()
        {
            quickAddForm.Visibility = Visibility.Collapsed;
            pendingSubtaskParentId = null; // Clear if cancelled
            System.Windows.Input.Keyboard.Focus(taskListBox);
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
                Priority = TaskPriority.Medium,
                ParentTaskId = pendingSubtaskParentId // Will be null for regular tasks, set for subtasks
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
            pendingSubtaskParentId = null; // Clear after creating task
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

            System.Windows.Input.Keyboard.Focus(inlineEditBox);
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
            System.Windows.Input.Keyboard.Focus(taskListBox);
        }

        private void CancelInlineEdit()
        {
            editingTask = null;
            RefreshTaskList();
            System.Windows.Input.Keyboard.Focus(taskListBox);
        }

        // Date editing methods
        private void StartDateEdit()
        {
            if (selectedTask == null)
                return;

            var newDate = ShowDatePicker(selectedTask.Task.DueDate);
            if (newDate != selectedTask.Task.DueDate)
            {
                var task = taskService.GetTask(selectedTask.Task.Id);
                if (task != null)
                {
                    task.DueDate = newDate;
                    task.UpdatedAt = DateTime.Now;
                    taskService.UpdateTask(task);
                    RefreshTaskList();
                }
            }
            System.Windows.Input.Keyboard.Focus(taskListBox);
        }

        private DateTime? ShowDatePicker(DateTime? currentDate)
        {
            var theme = themeManager.CurrentTheme;
            DateTime? selectedDate = null;
            bool dialogResult = false;

            // Create overlay
            var overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(204, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Create calendar
            var calendar = new System.Windows.Controls.Calendar
            {
                DisplayDate = currentDate ?? DateTime.Today,
                SelectedDate = currentDate,
                Width = 250,
                Height = 250
            };

            // Create buttons
            var btnOk = new Button { Content = "OK", Width = 80, Margin = new Thickness(5) };
            var btnClear = new Button { Content = "Clear", Width = 80, Margin = new Thickness(5) };
            var btnCancel = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(5) };

            btnOk.Click += (s, e) => {
                selectedDate = calendar.SelectedDate;
                dialogResult = true;
                ((Panel)overlay.Parent).Children.Remove(overlay);
            };

            btnClear.Click += (s, e) => {
                selectedDate = null;
                dialogResult = true;
                ((Panel)overlay.Parent).Children.Remove(overlay);
            };

            btnCancel.Click += (s, e) => {
                ((Panel)overlay.Parent).Children.Remove(overlay);
            };

            // Build dialog
            var dialog = new Border
            {
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Width = 300,
                Height = 350,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Select Due Date",
                            FontSize = 16,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(theme.Primary),
                            Margin = new Thickness(10)
                        },
                        calendar,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(10),
                            Children = { btnOk, btnClear, btnCancel }
                        }
                    }
                }
            };

            overlay.Child = dialog;

            // Add to parent (find mainLayout or similar)
            var parent = this.Parent as Panel;
            if (parent != null)
            {
                parent.Children.Add(overlay);
            }

            return dialogResult ? selectedDate : currentDate;
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
            System.Windows.Input.Keyboard.Focus(taskListBox);
            UpdateStatusBar();
        }

        private void CancelDateEdit()
        {
            dateEditingTask = null;
            RefreshTaskList();
            System.Windows.Input.Keyboard.Focus(taskListBox);
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

        private void AddHighlightedText(TextBlock textBlock, string text, string searchQuery)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var theme = themeManager.CurrentTheme;
            var highlightBrush = new SolidColorBrush(theme.Success);
            var normalBrush = new SolidColorBrush(theme.Foreground);

            int queryIndex = 0;
            for (int i = 0; i < text.Length && queryIndex < searchQuery.Length; i++)
            {
                bool isMatch = char.ToLower(text[i]) == char.ToLower(searchQuery[queryIndex]);

                var run = new System.Windows.Documents.Run(text[i].ToString())
                {
                    Foreground = isMatch ? highlightBrush : normalBrush,
                    FontWeight = isMatch ? FontWeights.Bold : FontWeights.Normal
                };

                if (isMatch) queryIndex++;
                textBlock.Inlines.Add(run);
            }
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
                Foreground = new SolidColorBrush(themeManager.CurrentTheme.Info),  // Theme-aware tag color
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(themeManager.CurrentTheme.Info),
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

            System.Windows.Input.Keyboard.Focus(tagEditBox);
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
            System.Windows.Input.Keyboard.Focus(taskListBox);
            UpdateStatusBar();
        }

        private void CancelTagEdit()
        {
            tagEditingTask = null;
            RefreshTaskList();
            System.Windows.Input.Keyboard.Focus(taskListBox);
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
            System.Windows.Input.Keyboard.Focus(taskListBox);
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
                System.Windows.Input.Keyboard.Focus(taskListBox);
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

            // Set pending parent ID and show quick add form (just like adding regular tasks)
            // This allows user to enter title, due date, and priority inline
            pendingSubtaskParentId = parentId;
            ShowQuickAdd();
        }

        private void MoveSelectedTaskUp()
        {
            if (selectedTask == null)
                return;

            taskService.MoveTaskUp(selectedTask.Task.Id);
            RefreshTaskList();
            System.Windows.Input.Keyboard.Focus(taskListBox);
        }

        private void MoveSelectedTaskDown()
        {
            if (selectedTask == null)
                return;

            taskService.MoveTaskDown(selectedTask.Task.Id);
            RefreshTaskList();
            System.Windows.Input.Keyboard.Focus(taskListBox);
        }

        private void DeleteSelectedTask()
        {
            if (selectedTask == null)
                return;

            var result = MessageBox.Show(
                $"Delete task '{selectedTask.Task.Title}'?\n\nYou can undo with Ctrl+Z.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Use command pattern for undo support
                var deleteCommand = new DeleteTaskCommand(taskService, selectedTask.Task);
                commandHistory.Execute(deleteCommand);

                RefreshTaskList();
                System.Windows.Input.Keyboard.Focus(taskListBox);
            }
        }

        public override PaneState SaveState()
        {
            var state = new Dictionary<string, object>
            {
                ["SelectedTaskId"] = selectedTask?.Task.Id.ToString(),
                ["SelectedIndex"] = taskListBox?.SelectedIndex ?? -1,
                ["FilterMode"] = currentFilter.ToString(),
                ["SortMode"] = currentSort.ToString(),
                ["ScrollPosition"] = GetScrollPosition(),

                // FOCUS MEMORY - Track exactly what the user was doing
                ["FocusedControl"] = GetCurrentFocusedControl(),

                // INLINE EDIT STATE - Remember if user was editing
                ["IsInlineEditing"] = inlineEditBox?.Visibility == Visibility.Visible,
                ["InlineEditTaskId"] = inlineEditBox?.Tag?.ToString(),
                ["InlineEditText"] = inlineEditBox?.Text,
                ["InlineEditCursorPos"] = inlineEditBox?.CaretIndex ?? 0,
                ["InlineEditSelectionStart"] = inlineEditBox?.SelectionStart ?? 0,
                ["InlineEditSelectionLength"] = inlineEditBox?.SelectionLength ?? 0,

                // QUICK ADD STATE
                ["IsQuickAdding"] = quickAddTitle?.Visibility == Visibility.Visible,
                ["QuickAddText"] = quickAddTitle?.Text,
                ["QuickAddCursorPos"] = quickAddTitle?.CaretIndex ?? 0,

                // DATE/TAG EDIT STATE
                ["IsDateEditing"] = dateEditBox?.Visibility == Visibility.Visible,
                ["DateEditTaskId"] = dateEditBox?.Tag?.ToString(),
                ["DateEditText"] = dateEditBox?.Text,
                ["IsTagEditing"] = tagEditBox?.Visibility == Visibility.Visible,
                ["TagEditTaskId"] = tagEditBox?.Tag?.ToString(),
                ["TagEditText"] = tagEditBox?.Text,

                // EXPANDED STATE for subtasks
                ["ExpandedTasks"] = GetExpandedTaskIds()
            };

            return new PaneState
            {
                PaneType = "TaskListPane",
                CustomData = state
            };
        }

        private string GetCurrentFocusedControl()
        {
            if (inlineEditBox?.IsFocused == true) return "InlineEdit";
            if (quickAddTitle?.IsFocused == true) return "QuickAdd";
            if (dateEditBox?.IsFocused == true) return "DateEdit";
            if (tagEditBox?.IsFocused == true) return "TagEdit";
            if (taskListBox?.IsFocused == true) return "TaskList";
            return "None";
        }

        private List<string> GetExpandedTaskIds()
        {
            var expanded = new List<string>();
            foreach (var item in taskListBox.Items)
            {
                if (item is Grid grid && grid.Tag is TaskItemViewModel vm && vm.IsExpanded)
                {
                    expanded.Add(vm.Task.Id.ToString());
                }
            }
            return expanded;
        }

        public override void RestoreState(PaneState state)
        {
            if (state?.CustomData == null) return;

            var data = state.CustomData as Dictionary<string, object>;
            if (data == null) return;

            // Store state for deferred restoration after list loads
            pendingRestoreData = data;

            // Restore filter
            if (data.TryGetValue("FilterMode", out var filterStr))
            {
                if (Enum.TryParse<FilterMode>(filterStr?.ToString(), out var filter))
                {
                    currentFilter = filter;
                    filterLabel.Text = GetFilterText();
                }
            }

            // Restore sort
            if (data.TryGetValue("SortMode", out var sortStr))
            {
                if (Enum.TryParse<SortMode>(sortStr?.ToString(), out var sort))
                {
                    currentSort = sort;
                    filterLabel.Text = GetFilterText();
                }
            }

            // Refresh with new filter/sort
            RefreshTaskList();

            // Use dispatcher with Loaded priority to ensure list is built
            Dispatcher.BeginInvoke(new Action(() => RestoreDetailedState(data)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private Dictionary<string, object> pendingRestoreData;

        private void RestoreDetailedState(Dictionary<string, object> data)
        {
            // Restore selection by index first (more reliable)
            if (data.TryGetValue("SelectedIndex", out var indexObj))
            {
                var index = Convert.ToInt32(indexObj);
                if (index >= 0 && index < taskListBox.Items.Count)
                {
                    taskListBox.SelectedIndex = index;
                    selectedTask = GetTaskViewModelAtIndex(index);
                }
            }
            // Fallback to ID-based selection
            else if (data.TryGetValue("SelectedTaskId", out var taskIdStr))
            {
                if (Guid.TryParse(taskIdStr?.ToString(), out var taskId))
                {
                    SelectTaskById(taskId);
                }
            }

            // Restore expanded tasks
            if (data.TryGetValue("ExpandedTasks", out var expandedObj) && expandedObj is List<string> expandedIds)
            {
                foreach (var item in taskListBox.Items)
                {
                    if (item is Grid grid && grid.Tag is TaskItemViewModel vm)
                    {
                        vm.IsExpanded = expandedIds.Contains(vm.Task.Id.ToString());
                        UpdateSubtaskVisibility(grid, vm);
                    }
                }
            }

            // Restore inline editing state
            if (data.TryGetValue("IsInlineEditing", out var isEditingObj) && (bool)isEditingObj)
            {
                if (data.TryGetValue("InlineEditTaskId", out var editTaskIdStr))
                {
                    if (Guid.TryParse(editTaskIdStr?.ToString(), out var editTaskId))
                    {
                        // Find the task and start inline edit
                        foreach (var item in taskListBox.Items)
                        {
                            if (item is Grid grid && grid.Tag is TaskItemViewModel vm && vm.Task.Id == editTaskId)
                            {
                                StartInlineEditForGrid(grid);

                                // Restore text and cursor
                                if (inlineEditBox != null)
                                {
                                    if (data.TryGetValue("InlineEditText", out var text))
                                        inlineEditBox.Text = text?.ToString() ?? "";
                                    if (data.TryGetValue("InlineEditCursorPos", out var cursorPos))
                                        inlineEditBox.CaretIndex = Convert.ToInt32(cursorPos);
                                    if (data.TryGetValue("InlineEditSelectionStart", out var selStart))
                                        inlineEditBox.SelectionStart = Convert.ToInt32(selStart);
                                    if (data.TryGetValue("InlineEditSelectionLength", out var selLen))
                                        inlineEditBox.SelectionLength = Convert.ToInt32(selLen);
                                }
                                break;
                            }
                        }
                    }
                }
            }

            // Restore quick add state
            if (data.TryGetValue("IsQuickAdding", out var isQuickAddingObj) && (bool)isQuickAddingObj)
            {
                ShowQuickAdd();
                if (quickAddTitle != null)
                {
                    if (data.TryGetValue("QuickAddText", out var text))
                        quickAddTitle.Text = text?.ToString() ?? "";
                    if (data.TryGetValue("QuickAddCursorPos", out var cursorPos))
                        quickAddTitle.CaretIndex = Convert.ToInt32(cursorPos);
                }
            }

            // Restore scroll position
            if (data.TryGetValue("ScrollPosition", out var scrollPos))
            {
                SetScrollPosition(scrollPos);
            }

            // Finally, restore focus to the correct control
            if (data.TryGetValue("FocusedControl", out var focusedControl))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    switch (focusedControl?.ToString())
                    {
                        case "InlineEdit":
                            if (inlineEditBox != null) System.Windows.Input.Keyboard.Focus(inlineEditBox);
                            System.Windows.Input.Keyboard.Focus(inlineEditBox);
                            break;
                        case "QuickAdd":
                            if (quickAddTitle != null) System.Windows.Input.Keyboard.Focus(quickAddTitle);
                            System.Windows.Input.Keyboard.Focus(quickAddTitle);
                            break;
                        case "DateEdit":
                            if (dateEditBox != null) System.Windows.Input.Keyboard.Focus(dateEditBox);
                            System.Windows.Input.Keyboard.Focus(dateEditBox);
                            break;
                        case "TagEdit":
                            if (tagEditBox != null) System.Windows.Input.Keyboard.Focus(tagEditBox);
                            System.Windows.Input.Keyboard.Focus(tagEditBox);
                            break;
                        case "TaskList":
                        default:
                            if (taskListBox != null) System.Windows.Input.Keyboard.Focus(taskListBox);
                            System.Windows.Input.Keyboard.Focus(taskListBox);
                            break;
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private TaskItemViewModel GetTaskViewModelAtIndex(int index)
        {
            if (index < 0 || index >= taskListBox.Items.Count) return null;
            var item = taskListBox.Items[index];
            if (item is Grid grid && grid.Tag is TaskItemViewModel vm)
                return vm;
            return null;
        }

        private void StartInlineEditForGrid(Grid taskGrid)
        {
            // Implementation to start inline edit for a specific grid
            // This needs to be extracted from existing StartInlineEdit method
            if (taskGrid?.Tag is TaskItemViewModel vm)
            {
                selectedTask = vm;
                taskListBox.SelectedItem = taskGrid;
                StartInlineEdit();
            }
        }

        private void UpdateSubtaskVisibility(Grid grid, TaskItemViewModel vm)
        {
            // Find and update the subtask container visibility
            var subtaskContainer = grid.Children.OfType<StackPanel>()
                .FirstOrDefault(sp => sp.Name == "SubtaskContainer");
            if (subtaskContainer != null)
            {
                subtaskContainer.Visibility = vm.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private double GetScrollPosition()
        {
            if (taskListBox == null) return 0;

            var scrollViewer = FindScrollViewer(taskListBox);
            return scrollViewer?.VerticalOffset ?? 0;
        }

        private void SetScrollPosition(object scrollPos)
        {
            if (taskListBox == null || scrollPos == null) return;

            var offset = Convert.ToDouble(scrollPos);
            var scrollViewer = FindScrollViewer(taskListBox);
            scrollViewer?.ScrollToVerticalOffset(offset);
        }

        private void SelectTaskById(Guid taskId)
        {
            if (taskListBox == null) return;

            foreach (var item in taskListBox.Items)
            {
                if (item is Grid grid && grid.Tag is TaskItemViewModel vm)
                {
                    if (vm.Task.Id == taskId)
                    {
                        taskListBox.SelectedItem = item;
                        taskListBox.ScrollIntoView(item);
                        break;
                    }
                }
            }
        }

        private ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            if (obj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is ScrollViewer scrollViewer)
                    return scrollViewer;

                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ApplyTheme();
            });
        }

        private void ApplyTheme()
        {
            CacheThemeColors();

            // Update all controls
            if (quickAddForm != null)
            {
                quickAddForm.Background = surfaceBrush;
            }

            if (quickAddTitle != null)
            {
                quickAddTitle.Background = surfaceBrush;
                quickAddTitle.Foreground = fgBrush;
                quickAddTitle.BorderBrush = accentBrush;
            }

            if (quickAddDueDate != null)
            {
                quickAddDueDate.Background = surfaceBrush;
                quickAddDueDate.Foreground = fgBrush;
                quickAddDueDate.BorderBrush = accentBrush;
            }

            if (quickAddPriority != null)
            {
                quickAddPriority.Background = surfaceBrush;
                quickAddPriority.Foreground = fgBrush;
                quickAddPriority.BorderBrush = accentBrush;
            }

            if (filterLabel != null)
            {
                filterLabel.Foreground = accentBrush;
            }

            if (taskListBox != null)
            {
                taskListBox.Background = Brushes.Transparent;
            }

            if (statusBar != null)
            {
                statusBar.Foreground = dimBrush;
            }

            // Refresh the task list to update all task items with new colors
            RefreshTaskList();

            this.InvalidateVisual();
        }

        protected override void OnDispose()
        {
            // Unsubscribe from task service events
            taskService.TaskAdded -= OnTaskChanged;
            taskService.TaskUpdated -= OnTaskChanged;
            taskService.TaskDeleted -= OnTaskDeleted;
            taskService.TaskRestored -= OnTaskChanged;

            // Unsubscribe from theme changes
            themeManager.ThemeChanged -= OnThemeChanged;

            // Unsubscribe from event bus to prevent memory leaks
            if (projectSelectedHandler != null)
            {
                eventBus.Unsubscribe(projectSelectedHandler);
            }

            if (commandExecutedHandler != null)
            {
                eventBus.Unsubscribe(commandExecutedHandler);
            }

            if (refreshRequestedHandler != null)
            {
                eventBus.Unsubscribe(refreshRequestedHandler);
            }

            base.OnDispose();
        }

        /// <summary>
        /// Handle RefreshRequestedEvent - refresh task list
        /// </summary>
        private void OnRefreshRequested(Core.Events.RefreshRequestedEvent evt)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                RefreshTaskList();
                Log("TaskListPane refreshed (RefreshRequestedEvent)");
            });
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
