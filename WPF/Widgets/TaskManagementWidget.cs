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
using SuperTUI.Widgets.Overlays;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Task management widget with 3-pane layout using purpose-built TaskListControl
    /// Full CRUD with inline editing of all task properties
    /// </summary>
    public class TaskManagementWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly ITagService tagService;

        private Theme theme;

        // UI Components
        private TreeTaskListControl treeTaskListControl;

        // Filter state
        private List<TaskFilter> filters;
        private TaskFilter currentFilter;

        // Selection state
        private TaskItem selectedTask;

        public TaskManagementWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config,
            ITaskService taskService,
            IProjectService projectService,
            ITagService tagService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));

            WidgetName = "Task Manager";
            WidgetType = "TaskManagement";
        }


        public override void Initialize()
        {
            try
            {
                theme = themeManager.CurrentTheme;

                // Setup filters
                filters = TaskFilter.GetDefaultFilters();
                currentFilter = TaskFilter.All;

                BuildUI();
                LoadCurrentFilter();

                // Subscribe to EventBus for inter-widget communication
                EventBus.Subscribe<Core.Events.TaskSelectedEvent>(OnTaskSelectedFromOtherWidget);
                EventBus.Subscribe<Core.Events.NavigationRequestedEvent>(OnNavigationRequested);

                logger?.Info("TaskWidget", "Task Management widget initialized");
            }
            catch (Exception ex)
            {
                logger?.Error("TaskWidget", $"Initialization failed: {ex.Message}", ex);
                throw; // Re-throw to let ErrorBoundary handle it
            }
        }

        private void OnTaskSelectedFromOtherWidget(Core.Events.TaskSelectedEvent evt)
        {
            // Check if we're on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                // Marshal to UI thread
                Dispatcher.BeginInvoke(() => OnTaskSelectedFromOtherWidget(evt));
                return;
            }

            if (evt.SourceWidget == WidgetType) return; // Ignore our own events
            if (evt.Task == null || treeTaskListControl == null) return;

            // Try to select the task if it's in the current view
            SelectTaskById(evt.Task.Id);
        }

        private void OnNavigationRequested(Core.Events.NavigationRequestedEvent evt)
        {
            // Check if we're on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                // Marshal to UI thread
                Dispatcher.BeginInvoke(() => OnNavigationRequested(evt));
                return;
            }

            // Handle navigation to this widget
            if (evt.TargetWidgetType != WidgetType) return;
            if (!(evt.Context is TaskItem task)) return;
            if (treeTaskListControl == null) return;

            // Try to select in current filter first
            if (!SelectTaskById(task.Id))
            {
                // Not in current filter, switch to "All" and try again
                currentFilter = TaskFilter.All;
                LoadCurrentFilter();
                SelectTaskById(task.Id);
            }

            // Focus this widget so user can see the selection
            this.Focus();
        }

        private bool SelectTaskById(Guid taskId)
        {
            if (treeTaskListControl != null)
            {
                treeTaskListControl.SelectTask(taskId);
                return treeTaskListControl.GetSelectedTask()?.Id == taskId;
            }

            return false;
        }

        private void BuildUI()
        {
            treeTaskListControl = new TreeTaskListControl(logger, themeManager);

            // Subscribe to events
            treeTaskListControl.TaskSelected += OnTaskSelected;
            treeTaskListControl.TaskActivated += OnTaskActivated;
            treeTaskListControl.CreateSubtask += OnCreateSubtask;
            treeTaskListControl.DeleteTask += OnDeleteTask;
            treeTaskListControl.ToggleExpanded += OnToggleExpanded;

            // Ensure widget is focusable
            this.Focusable = true;
            this.MinHeight = 200;
            this.MinWidth = 300;

            this.Content = treeTaskListControl;
        }

        private void LoadCurrentFilter()
        {
            if (treeTaskListControl != null)
            {
                var filteredTasks = taskService.GetTasks(currentFilter.Predicate);
                treeTaskListControl.LoadTasks(filteredTasks);
            }
        }

        private void OnTaskSelected(TaskItem task)
        {
            selectedTask = task;
        }

        private void OnTaskActivated(TaskItem task)
        {
            // Open task edit dialog
            // TODO: Implement task edit dialog
            logger?.Info("TaskWidget", $"Task activated: {task.Title}");
        }

        private void OnCreateSubtask(TaskItem parentTask)
        {
            var inputOverlay = new InputOverlay(
                themeManager,
                "Create Subtask",
                $"Enter subtask title for '{parentTask.Title}':",
                "",
                title =>
                {
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        var subtask = new TaskItem
                        {
                            Title = title,
                            ParentTaskId = parentTask.Id,
                            Status = TaskStatus.Pending,
                            Priority = TaskPriority.Medium,
                            SortOrder = taskService.GetSubtasks(parentTask.Id).Count * 100
                        };

                        taskService.AddTask(subtask);
                        LoadCurrentFilter();
                        logger?.Info("TaskWidget", $"Created subtask: {title}");
                    }
                    OverlayManager.Instance.HideCenterZone();
                },
                () => OverlayManager.Instance.HideCenterZone());

            OverlayManager.Instance.ShowCenterZone(inputOverlay);
        }

        private void OnDeleteTask(TaskItem task)
        {
            var subtasks = taskService.GetAllSubtasksRecursive(task.Id);
            var message = subtasks.Count > 0
                ? $"Delete '{task.Title}' and {subtasks.Count} subtask(s)?"
                : $"Delete '{task.Title}'?";

            var confirmationOverlay = new ConfirmationOverlay(
                themeManager,
                "Confirm Delete",
                message,
                () =>
                {
                    taskService.DeleteTask(task.Id);
                    LoadCurrentFilter();

                    if (selectedTask != null && selectedTask.Id == task.Id)
                    {
                        selectedTask = null;
                    }

                    logger?.Info("TaskWidget", $"Deleted task: {task.Title}");
                    OverlayManager.Instance.HideCenterZone();
                },
                () => OverlayManager.Instance.HideCenterZone());

            OverlayManager.Instance.ShowCenterZone(confirmationOverlay);
        }

        private void OnToggleExpanded(TreeTaskItem item)
        {
            // Refresh needed, but TreeTaskListControl handles it internally
            logger?.Debug("TaskWidget", $"Toggled expand for: {item.Task.Title}");
        }

        #region Widget Lifecycle

        public override void OnWidgetFocusReceived()
        {
            // Set keyboard focus to the tree control so it can handle input
            if (treeTaskListControl != null)
            {
                treeTaskListControl.BorderBrush = new SolidColorBrush(theme.Focus);
                treeTaskListControl.BorderThickness = new Thickness(2);
                treeTaskListControl.Focus(); // Actually give it keyboard focus
                Keyboard.Focus(treeTaskListControl);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            // OVERLAY SHORTCUTS (NEW)

            // N key (without Ctrl) for quick add task overlay
            if (e.Key == Key.N && !isCtrl)
            {
                var quickAdd = new QuickAddTaskOverlay(taskService, projectService);
                quickAdd.TaskCreated += (task) =>
                {
                    logger?.Info("TaskWidget", $"Quick-created task: {task.Title}");
                    LoadCurrentFilter();
                    treeTaskListControl?.SelectTask(task.Id);
                };
                quickAdd.Cancelled += () =>
                {
                    OverlayManager.Instance.HideBottomZone();
                    this.Focus(); // Return focus to widget
                };
                OverlayManager.Instance.ShowBottomZone(quickAdd);
                e.Handled = true;
                return;
            }

            // / key for filter panel overlay
            if (e.Key == Key.OemQuestion) // Forward slash
            {
                var filter = new FilterPanelOverlay(taskService, projectService);
                filter.FilterChanged += (predicate) =>
                {
                    logger?.Info("TaskWidget", "Filter applied from overlay");
                    var filteredTasks = taskService.GetTasks(predicate);
                    treeTaskListControl?.LoadTasks(filteredTasks);
                };
                OverlayManager.Instance.ShowLeftZone(filter);
                e.Handled = true;
                return;
            }

            // Arrow keys (Up/Down) auto-show task detail overlay
            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                // Let tree control handle navigation first
                base.OnKeyDown(e);

                // Then show detail overlay for newly selected task
                if (selectedTask != null)
                {
                    var detail = new TaskDetailOverlay(selectedTask);
                    OverlayManager.Instance.ShowRightZone(detail);
                }

                e.Handled = true;
                return;
            }

            // EXISTING SHORTCUTS

            // Ctrl+E for export
            if (e.Key == Key.E && isCtrl)
            {
                ShowExportDialog();
                e.Handled = true;
            }

            // Ctrl+M for add note to selected task (TODO: implement note overlay)
            if (e.Key == Key.M && isCtrl)
            {
                if (selectedTask != null)
                {
                    logger?.Info("TaskWidget", "Add note shortcut pressed (not yet implemented)");
                }
                e.Handled = true;
            }

            // Ctrl+T for edit tags (TODO: implement tag editor overlay)
            if (e.Key == Key.T && isCtrl)
            {
                if (selectedTask != null)
                {
                    logger?.Info("TaskWidget", "Edit tags shortcut pressed (not yet implemented)");
                }
                e.Handled = true;
            }

            // Ctrl+Up to move task up
            if (e.Key == Key.Up && isCtrl)
            {
                if (selectedTask != null)
                {
                    try
                    {
                        taskService.MoveTaskUp(selectedTask.Id);
                        LoadCurrentFilter();
                        treeTaskListControl?.SelectTask(selectedTask.Id);
                        logger?.Debug("TaskWidget", $"Moved task up: {selectedTask.Title}");
                    }
                    catch (Exception ex)
                    {
                        logger?.Warning("TaskWidget", $"Cannot move task up: {ex.Message}");
                    }
                }
                e.Handled = true;
            }

            // Ctrl+Down to move task down
            if (e.Key == Key.Down && isCtrl)
            {
                if (selectedTask != null)
                {
                    try
                    {
                        taskService.MoveTaskDown(selectedTask.Id);
                        LoadCurrentFilter();
                        treeTaskListControl?.SelectTask(selectedTask.Id);
                        logger?.Debug("TaskWidget", $"Moved task down: {selectedTask.Title}");
                    }
                    catch (Exception ex)
                    {
                        logger?.Warning("TaskWidget", $"Cannot move task down: {ex.Message}");
                    }
                }
                e.Handled = true;
            }

            // C key (without Ctrl) to cycle color theme (TODO: implement color picker overlay)
            if (e.Key == Key.C && !isCtrl)
            {
                if (selectedTask != null)
                {
                    logger?.Info("TaskWidget", "Cycle color shortcut pressed (not yet implemented)");
                }
                e.Handled = true;
            }

            // F2 to edit selected task (Windows standard)
            if (e.Key == Key.F2)
            {
                if (selectedTask != null && treeTaskListControl != null)
                {
                    // Focus the tree control to enable inline editing
                    treeTaskListControl.Focus();
                    // Note: TreeTaskListControl should implement F2 inline edit internally
                }
                e.Handled = true;
            }

            // Delete key to remove selected task
            if (e.Key == Key.Delete)
            {
                if (selectedTask != null)
                {
                    OnDeleteTask(selectedTask);
                }
                e.Handled = true;
            }

            // Ctrl+N to create new task
            if (e.Key == Key.N && isCtrl)
            {
                try
                {
                    var newTask = new TaskItem
                    {
                        Title = "New Task",
                        Status = TaskStatus.Pending,
                        Priority = TaskPriority.Medium,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    taskService.AddTask(newTask);
                    LoadCurrentFilter();
                    treeTaskListControl?.SelectTask(newTask.Id);
                    logger?.Info("TaskWidget", "Created new task");
                }
                catch (Exception ex)
                {
                    logger?.Error("TaskWidget", $"Error creating task: {ex.Message}", ex);
                }
                e.Handled = true;
            }
        }

        private void ShowExportDialog()
        {
            try
            {
                // Create a simple dialog to choose export format
                var dialog = new Window
                {
                    Title = "Export Tasks",
                    Width = 300,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush(theme.Background)
                };

                var stack = new StackPanel { Margin = new Thickness(20) };

                var titleText = new TextBlock
                {
                    Text = "Choose export format:",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(theme.Foreground),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                stack.Children.Add(titleText);

                var markdownBtn = new Button
                {
                    Content = "Markdown (.md)",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Background = new SolidColorBrush(theme.Info),
                    Foreground = Brushes.White,
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 5, 0, 5),
                    Cursor = Cursors.Hand,
                    IsDefault = true // Enter key activates this button
                };
                markdownBtn.Click += (s, e) => { dialog.DialogResult = true; dialog.Tag = "md"; dialog.Close(); };
                stack.Children.Add(markdownBtn);

                var csvBtn = new Button
                {
                    Content = "CSV (.csv)",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Background = new SolidColorBrush(theme.Success),
                    Foreground = Brushes.White,
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 5, 0, 5),
                    Cursor = Cursors.Hand
                };
                csvBtn.Click += (s, e) => { dialog.DialogResult = true; dialog.Tag = "csv"; dialog.Close(); };
                stack.Children.Add(csvBtn);

                var jsonBtn = new Button
                {
                    Content = "JSON (.json)",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Background = new SolidColorBrush(theme.Warning),
                    Foreground = Brushes.White,
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 5, 0, 5),
                    Cursor = Cursors.Hand
                };
                jsonBtn.Click += (s, e) => { dialog.DialogResult = true; dialog.Tag = "json"; dialog.Close(); };
                stack.Children.Add(jsonBtn);

                dialog.Content = stack;

                if (dialog.ShowDialog() == true && dialog.Tag != null)
                {
                    var format = dialog.Tag.ToString();
                    ExportTasks(format);
                }
            }
            catch (Exception ex)
            {
                logger?.Error("TaskWidget", $"Failed to show export dialog: {ex.Message}", ex);
            }
        }

        private void ExportTasks(string format)
        {
            try
            {
                // Use SaveFileDialog
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Tasks",
                    Filter = format switch
                    {
                        "md" => "Markdown files (*.md)|*.md",
                        "csv" => "CSV files (*.csv)|*.csv",
                        "json" => "JSON files (*.json)|*.json",
                        _ => "All files (*.*)|*.*"
                    },
                    DefaultExt = format,
                    FileName = $"tasks_{DateTime.Now:yyyyMMdd_HHmmss}.{format}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    bool success = format switch
                    {
                        "md" => taskService.ExportToMarkdown(saveDialog.FileName),
                        "csv" => taskService.ExportToCSV(saveDialog.FileName),
                        "json" => taskService.ExportToJson(saveDialog.FileName),
                        _ => false
                    };

                    if (success)
                    {
                        logger?.Info("TaskWidget", $"Successfully exported tasks to {saveDialog.FileName}");
                    }
                    else
                    {
                        logger?.Error("TaskWidget", $"Failed to export tasks");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Error("TaskWidget", $"Failed to export tasks: {ex.Message}", ex);
            }
        }

        public override Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            state["SelectedTaskId"] = selectedTask?.Id.ToString();
            state["CurrentFilter"] = currentFilter?.Name;

            // TreeTaskListControl doesn't need expanded state saved (tasks track their own IsExpanded)

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
                    logger?.Warning("TaskWidget", $"Could not restore task {id}, it may have been deleted");
                }
            }

            if (state.ContainsKey("CurrentFilter") && state["CurrentFilter"] is string filterName)
            {
                currentFilter = filters?.FirstOrDefault(f => f.Name == filterName) ?? TaskFilter.All;
            }

            // TreeTaskListControl: Tasks track their own IsExpanded state, no need to restore

            LoadCurrentFilter();
        }

        public void ApplyTheme()
        {
            theme = themeManager.CurrentTheme;

            // Update TreeTaskListControl styling if needed
            if (treeTaskListControl != null)
            {
                treeTaskListControl.BorderBrush = new SolidColorBrush(theme.Border);
            }

            logger?.Debug("TaskWidget", "Theme applied");
        }

        protected override void OnDispose()
        {
            // Unsubscribe from EventBus
            EventBus.Unsubscribe<Core.Events.TaskSelectedEvent>(OnTaskSelectedFromOtherWidget);
            EventBus.Unsubscribe<Core.Events.NavigationRequestedEvent>(OnNavigationRequested);

            logger?.Info("TaskWidget", "Task Management widget disposed");
            base.OnDispose();
        }

        #endregion
    }
}
