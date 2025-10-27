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
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;
        private readonly ITaskService taskService;
        private readonly ITagService tagService;

        private Theme theme;

        // UI Components
        private Grid mainGrid;
        private ListBox filterListBox;
        private TreeTaskListControl treeTaskListControl;
        private StackPanel detailsPanel;

        // Filter state
        private List<TaskFilter> filters;
        private TaskFilter currentFilter;
        private bool isRefreshingFilters; // Prevent infinite loop in SelectionChanged

        // Selection state
        private TaskItem selectedTask;

        // Details panel controls
        private TextBlock detailTitle;
        private TextBox detailDescriptionBox;
        private TextBlock detailStatus;
        private TextBlock detailPriority;
        private TextBlock detailDueDate;
        private TextBlock detailProgress;
        private TextBlock detailTagsDisplay;
        private Button editTagsButton;
        private TextBlock detailColorTheme;
        private Button cycleColorButton;
        private TextBlock detailCreated;
        private TextBlock detailUpdated;
        private ListBox notesListBox;
        private TextBox addNoteBox;
        private StackPanel subtasksPanel;
        private Button saveDescButton;

        public TaskManagementWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config,
            ITaskService taskService,
            ITagService tagService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));

            WidgetName = "Task Manager";
            WidgetType = "TaskManagement";
        }


        public override void Initialize()
        {
            theme = themeManager.CurrentTheme;

            // Setup filters
            filters = TaskFilter.GetDefaultFilters();
            currentFilter = TaskFilter.All;

            BuildUI();
            RefreshFilterList();
            LoadCurrentFilter();

            // Subscribe to EventBus for inter-widget communication
            EventBus.Subscribe<Core.Events.TaskSelectedEvent>(OnTaskSelectedFromOtherWidget);
            EventBus.Subscribe<Core.Events.NavigationRequestedEvent>(OnNavigationRequested);

            logger?.Info("TaskWidget", "Task Management widget initialized");
        }

        private void OnTaskSelectedFromOtherWidget(Core.Events.TaskSelectedEvent evt)
        {
            if (evt.SourceWidget == WidgetType) return; // Ignore our own events
            if (evt.Task == null || treeTaskListControl == null) return;

            // Try to select the task if it's in the current view
            SelectTaskById(evt.Task.Id);
        }

        private void OnNavigationRequested(Core.Events.NavigationRequestedEvent evt)
        {
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
            // Prevent infinite loop: RefreshFilterList -> SelectedItem set -> SelectionChanged -> LoadCurrentFilter -> RefreshFilterList
            isRefreshingFilters = true;
            try
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
            finally
            {
                isRefreshingFilters = false;
            }
        }

        private void FilterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ignore selection changes during refresh to prevent infinite loop
            if (isRefreshingFilters)
                return;

            if (filterListBox.SelectedItem is TextBlock item && item.Tag is TaskFilter filter)
            {
                currentFilter = filter;
                LoadCurrentFilter();
                logger?.Debug("TaskWidget", $"Filter changed to: {filter.Name}");
            }
        }

        private void LoadCurrentFilter()
        {
            if (treeTaskListControl != null)
            {
                var filteredTasks = taskService.GetTasks(currentFilter.Predicate);
                treeTaskListControl.LoadTasks(filteredTasks);
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

            treeTaskListControl = new TreeTaskListControl(logger, themeManager);

            // Subscribe to events
            treeTaskListControl.TaskSelected += OnTaskSelected;
            treeTaskListControl.TaskActivated += OnTaskActivated;
            treeTaskListControl.CreateSubtask += OnCreateSubtask;
            treeTaskListControl.DeleteTask += OnDeleteTask;
            treeTaskListControl.ToggleExpanded += OnToggleExpanded;

            border.Child = treeTaskListControl;
            return border;
        }

        private void OnTaskSelected(TaskItem task)
        {
            selectedTask = task;
            RefreshDetailPanel();
        }

        private void OnTaskActivated(TaskItem task)
        {
            // Open task edit dialog
            // TODO: Implement task edit dialog
            logger?.Info("TaskWidget", $"Task activated: {task.Title}");
        }

        private void OnCreateSubtask(TaskItem parentTask)
        {
            // Create subtask dialog
            var title = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter subtask title for '{parentTask.Title}':",
                "Create Subtask",
                "");

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
        }

        private void OnDeleteTask(TaskItem task)
        {
            var subtasks = taskService.GetAllSubtasksRecursive(task.Id);
            var message = subtasks.Count > 0
                ? $"Delete '{task.Title}' and {subtasks.Count} subtask(s)?"
                : $"Delete '{task.Title}'?";

            var result = MessageBox.Show(message, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                taskService.DeleteTask(task.Id);
                LoadCurrentFilter();
                RefreshFilterList();

                if (selectedTask != null && selectedTask.Id == task.Id)
                {
                    selectedTask = null;
                    RefreshDetailPanel();
                }

                logger?.Info("TaskWidget", $"Deleted task: {task.Title}");
            }
        }

        private void OnToggleExpanded(TreeTaskItem item)
        {
            // Refresh needed, but TreeTaskListControl handles it internally
            logger?.Debug("TaskWidget", $"Toggled expand for: {item.Task.Title}");
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

            // Tags (with dialog editor)
            var tagsLabel = new TextBlock
            {
                Text = "Tags:",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 10, 0, 3)
            };
            detailsPanel.Children.Add(tagsLabel);

            detailTagsDisplay = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 3),
                TextWrapping = TextWrapping.Wrap
            };
            detailsPanel.Children.Add(detailTagsDisplay);

            editTagsButton = new Button
            {
                Content = "Edit Tags (T)",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            editTagsButton.Click += EditTags_Click;
            detailsPanel.Children.Add(editTagsButton);

            // Color Theme
            var colorLabel = new TextBlock
            {
                Text = "Color Theme:",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 10, 0, 3)
            };
            detailsPanel.Children.Add(colorLabel);

            detailColorTheme = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 3)
            };
            detailsPanel.Children.Add(detailColorTheme);

            cycleColorButton = new Button
            {
                Content = "Cycle Color (C)",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            cycleColorButton.Click += CycleColor_Click;
            detailsPanel.Children.Add(cycleColorButton);

            // Timestamps
            detailCreated = CreateDetailLabel("", fontSize: 10);
            detailCreated.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            detailsPanel.Children.Add(detailCreated);

            detailUpdated = CreateDetailLabel("", fontSize: 10);
            detailUpdated.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            detailsPanel.Children.Add(detailUpdated);

            // Notes section
            var notesHeader = new TextBlock
            {
                Text = "NOTES",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 15, 0, 10)
            };
            detailsPanel.Children.Add(notesHeader);

            notesListBox = new ListBox
            {
                Name = "NotesListBox",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                MinHeight = 60,
                MaxHeight = 120,
                Margin = new Thickness(0, 0, 0, 5)
            };
            detailsPanel.Children.Add(notesListBox);

            var addNoteStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            addNoteBox = new TextBox
            {
                Name = "AddNoteBox",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                Width = 180
            };
            addNoteStack.Children.Add(addNoteBox);

            var addNoteButton = new Button
            {
                Content = "Add Note",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(theme.Info),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(5, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            addNoteButton.Click += AddNote_Click;
            addNoteStack.Children.Add(addNoteButton);

            detailsPanel.Children.Add(addNoteStack);

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
                detailTagsDisplay.Text = "(No tags)";
                editTagsButton.IsEnabled = false;
                detailColorTheme.Text = "None";
                cycleColorButton.IsEnabled = false;
                detailCreated.Text = "";
                detailUpdated.Text = "";
                notesListBox.Items.Clear();
                addNoteBox.Text = "";
                addNoteBox.IsEnabled = false;
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

            detailTagsDisplay.Text = selectedTask.Tags != null && selectedTask.Tags.Any() ?
                string.Join(", ", selectedTask.Tags) : "(No tags)";
            editTagsButton.IsEnabled = true;

            // Color theme
            detailColorTheme.Text = GetColorThemeDisplay(selectedTask.ColorTheme);
            detailColorTheme.Foreground = new SolidColorBrush(GetColorThemeColor(selectedTask.ColorTheme));
            cycleColorButton.IsEnabled = true;

            detailCreated.Text = $"Created: {selectedTask.CreatedAt:yyyy-MM-dd HH:mm}";
            detailUpdated.Text = $"Updated: {selectedTask.UpdatedAt:yyyy-MM-dd HH:mm}";

            // Refresh notes
            notesListBox.Items.Clear();
            addNoteBox.IsEnabled = true;

            if (selectedTask.Notes != null && selectedTask.Notes.Any())
            {
                foreach (var note in selectedTask.Notes.OrderByDescending(n => n.CreatedAt))
                {
                    var noteText = new TextBlock
                    {
                        Text = $"[{note.CreatedAt:MM/dd HH:mm}] {note.Content}",
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Foreground = new SolidColorBrush(theme.Foreground),
                        TextWrapping = TextWrapping.Wrap,
                        Padding = new Thickness(3)
                    };
                    notesListBox.Items.Add(noteText);
                }
            }
            else
            {
                var noNotes = new TextBlock
                {
                    Text = "No notes",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                    Padding = new Thickness(3)
                };
                notesListBox.Items.Add(noNotes);
            }

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

            logger?.Info("TaskWidget", $"Updated description for: {selectedTask.Title}");
        }

        private void EditTags_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask == null) return;

            try
            {
                var dialog = new Core.Dialogs.TagEditorDialog(
                    selectedTask.Tags,
                    logger,
                    themeManager,
                    tagService
                );

                if (dialog.ShowDialog() == true)
                {
                    // Update task tags
                    tagService.SetTaskTags(selectedTask.Id, dialog.Tags);

                    // Refresh display
                    RefreshDetailPanel();
                    LoadCurrentFilter();

                    logger?.Info("TaskWidget", $"Updated tags for: {selectedTask.Title}");
                }
            }
            catch (Exception ex)
            {
                logger?.Error("TaskWidget", $"Failed to edit tags: {ex.Message}", ex);
                MessageBox.Show($"Failed to edit tags: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CycleColor_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask == null) return;

            // Cycle to next color theme
            var currentTheme = (int)selectedTask.ColorTheme;
            var nextTheme = (currentTheme + 1) % 7; // 0-6 (7 themes)
            selectedTask.ColorTheme = (TaskColorTheme)nextTheme;
            selectedTask.UpdatedAt = DateTime.Now;

            taskService.UpdateTask(selectedTask);
            RefreshDetailPanel();
            LoadCurrentFilter(); // Refresh to show color in list

            logger?.Info("TaskWidget", $"Changed color theme to {selectedTask.ColorTheme} for: {selectedTask.Title}");
        }

        private string GetColorThemeDisplay(TaskColorTheme colorTheme)
        {
            return colorTheme switch
            {
                TaskColorTheme.Red => "🔴 Red (Urgent/Critical)",
                TaskColorTheme.Blue => "🔵 Blue (Work/Professional)",
                TaskColorTheme.Green => "🟢 Green (Personal/Health)",
                TaskColorTheme.Yellow => "🟡 Yellow (Learning/Development)",
                TaskColorTheme.Purple => "🟣 Purple (Creative/Projects)",
                TaskColorTheme.Orange => "🟠 Orange (Social/Events)",
                _ => "⚪ None (Default)"
            };
        }

        private System.Windows.Media.Color GetColorThemeColor(TaskColorTheme colorTheme)
        {
            return colorTheme switch
            {
                TaskColorTheme.Red => System.Windows.Media.Color.FromRgb(220, 53, 69),
                TaskColorTheme.Blue => System.Windows.Media.Color.FromRgb(13, 110, 253),
                TaskColorTheme.Green => System.Windows.Media.Color.FromRgb(25, 135, 84),
                TaskColorTheme.Yellow => System.Windows.Media.Color.FromRgb(255, 193, 7),
                TaskColorTheme.Purple => System.Windows.Media.Color.FromRgb(111, 66, 193),
                TaskColorTheme.Orange => System.Windows.Media.Color.FromRgb(253, 126, 20),
                _ => theme.Foreground
            };
        }

        private void AddNote_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask == null || string.IsNullOrWhiteSpace(addNoteBox.Text))
                return;

            var noteContent = addNoteBox.Text.Trim();
            taskService.AddNote(selectedTask.Id, noteContent);

            // Refresh the selected task
            selectedTask = taskService.GetTask(selectedTask.Id);
            RefreshDetailPanel();

            // Clear the input box
            addNoteBox.Text = "";

            logger?.Info("TaskWidget", $"Added note to: {selectedTask.Title}");
        }

        #endregion

        #region Widget Lifecycle

        public override void OnWidgetFocusReceived()
        {
            // Don't steal keyboard focus from workspace!
            // Just update visual indicators to show this widget is focused
            if (treeTaskListControl != null)
            {
                treeTaskListControl.BorderBrush = new SolidColorBrush(theme.Focus);
                treeTaskListControl.BorderThickness = new Thickness(2);
            }
        }

        public override void OnWidgetFocusLost()
        {
            // Reset visual indicators when focus is lost
            if (treeTaskListControl != null)
            {
                treeTaskListControl.BorderBrush = new SolidColorBrush(theme.Border);
                treeTaskListControl.BorderThickness = new Thickness(1);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            // Ctrl+E for export
            if (e.Key == Key.E && isCtrl)
            {
                ShowExportDialog();
                e.Handled = true;
            }

            // Ctrl+M for add note to selected task
            if (e.Key == Key.M && isCtrl)
            {
                if (selectedTask != null && addNoteBox != null)
                {
                    addNoteBox.Focus();
                    Keyboard.Focus(addNoteBox);
                }
                e.Handled = true;
            }

            // Ctrl+T for edit tags
            if (e.Key == Key.T && isCtrl)
            {
                if (selectedTask != null && editTagsButton != null && editTagsButton.IsEnabled)
                {
                    EditTags_Click(this, null);
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

            // C key (without Ctrl) to cycle color theme
            if (e.Key == Key.C && !isCtrl)
            {
                if (selectedTask != null && cycleColorButton != null && cycleColorButton.IsEnabled)
                {
                    CycleColor_Click(this, null);
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
                    Cursor = Cursors.Hand
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

            RefreshFilterList();
            LoadCurrentFilter();
            RefreshDetailPanel();
        }

        protected override void OnDispose()
        {
            // Unsubscribe from UI events
            if (filterListBox != null)
            {
                filterListBox.SelectionChanged -= FilterListBox_SelectionChanged;
            }

            if (treeTaskListControl != null)
            {
                treeTaskListControl.TaskSelected -= OnTaskSelected;
                treeTaskListControl.TaskActivated -= OnTaskActivated;
                treeTaskListControl.CreateSubtask -= OnCreateSubtask;
                treeTaskListControl.DeleteTask -= OnDeleteTask;
                treeTaskListControl.ToggleExpanded -= OnToggleExpanded;
                treeTaskListControl = null;
            }

            if (saveDescButton != null)
            {
                saveDescButton.Click -= SaveDescription_Click;
            }

            if (editTagsButton != null)
            {
                editTagsButton.Click -= EditTags_Click;
            }

            if (cycleColorButton != null)
            {
                cycleColorButton.Click -= CycleColor_Click;
            }

            // Unsubscribe from EventBus
            EventBus.Unsubscribe<Core.Events.TaskSelectedEvent>(OnTaskSelectedFromOtherWidget);
            EventBus.Unsubscribe<Core.Events.NavigationRequestedEvent>(OnNavigationRequested);

            logger?.Info("TaskWidget", "Task Management widget disposed");
            base.OnDispose();
        }

        public void ApplyTheme()
        {
            theme = themeManager.CurrentTheme;

            if (mainGrid != null)
            {
                mainGrid.Background = new SolidColorBrush(theme.Background);
            }

            // TreeTaskListControl doesn't have ApplyTheme - it uses theme from constructor
            RefreshDetailPanel();

            logger?.Debug("TaskWidget", "Applied theme update");
        }

        #endregion
    }
}
