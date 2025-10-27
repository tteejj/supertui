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
    /// Retro terminal-styled task management widget
    /// Keyboard-centric with XCOM/Fallout aesthetic
    /// 100% navigable without mouse
    /// </summary>
    public class RetroTaskManagementWidget : WidgetBase, IThemeable
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
        private ListBox taskListBox;
        private TextBlock detailsTextBlock;
        private TextBlock statusBar;

        // Filter state
        private List<TaskFilter> filters;
        private TaskFilter currentFilter;

        // Selection state
        private TaskItem selectedTask;

        // Focus state (which panel has keyboard focus: 0=filters, 1=tasks, 2=details)
        private int focusedPanel = 1; // Start on task list

        public RetroTaskManagementWidget(
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

            WidgetName = "RETRO TASK MANAGER";
            WidgetType = "RetroTaskManagement";
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

            logger?.Info("RetroTaskWidget", "Retro Task Management widget initialized");
        }

        private void BuildUI()
        {
            // Main container with status bar at bottom
            mainGrid = new Grid
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)) // Pure black for retro feel
            };

            // Rows: Main content area | Status bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Content grid (3 panels)
            var contentGrid = new Grid { Margin = new Thickness(2) };

            // Define columns: Filters (250) | Tasks (flex) | Details (400)
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });

            // Build each panel with ASCII borders
            var filterPanel = BuildFilterPanel();
            var taskPanel = BuildTaskPanel();
            var detailPanel = BuildDetailPanel();

            Grid.SetColumn(filterPanel, 0);
            Grid.SetColumn(taskPanel, 1);
            Grid.SetColumn(detailPanel, 2);

            contentGrid.Children.Add(filterPanel);
            contentGrid.Children.Add(taskPanel);
            contentGrid.Children.Add(detailPanel);

            Grid.SetRow(contentGrid, 0);
            mainGrid.Children.Add(contentGrid);

            // Status bar
            statusBar = BuildStatusBar();
            Grid.SetRow(statusBar, 1);
            mainGrid.Children.Add(statusBar);

            this.Content = mainGrid;
            this.Focusable = true;
            this.FocusVisualStyle = null;
        }

        #region ASCII Border Helper

        private Border CreateRetroPanel(string title, UIElement content)
        {
            var outerBorder = new Border
            {
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)),
                Margin = new Thickness(2)
            };

            var stack = new StackPanel();

            // Title bar with ASCII borders
            var titleBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 4, 8, 4)
            };

            var titleText = new TextBlock
            {
                Text = $"┌─ {title.ToUpper()} ─┐",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                TextAlignment = TextAlignment.Left
            };
            titleBorder.Child = titleText;
            stack.Children.Add(titleBorder);

            // Content area
            var contentBorder = new Border
            {
                Padding = new Thickness(4)
            };
            contentBorder.Child = content;
            stack.Children.Add(contentBorder);

            outerBorder.Child = stack;
            return outerBorder;
        }

        #endregion

        #region Filter Panel

        private UIElement BuildFilterPanel()
        {
            filterListBox = new ListBox
            {
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                Padding = new Thickness(4),
                FocusVisualStyle = null
            };

            filterListBox.SelectionChanged += FilterListBox_SelectionChanged;
            filterListBox.GotFocus += (s, e) => { focusedPanel = 0; UpdateStatusBar(); };

            return CreateRetroPanel("FILTERS", filterListBox);
        }

        private void RefreshFilterList()
        {
            var selectedFilter = currentFilter;
            filterListBox.Items.Clear();

            foreach (var filter in filters)
            {
                var count = taskService.GetTaskCount(filter.Predicate);

                var item = new TextBlock
                {
                    Text = $"  {filter.Name,-18} [{count,3}]",
                    Padding = new Thickness(4, 2, 4, 2),
                    Tag = filter,
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 11
                };

                filterListBox.Items.Add(item);

                if (filter.Name == selectedFilter.Name)
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
                logger?.Debug("RetroTaskWidget", $"Filter changed to: {filter.Name}");
            }
        }

        private void LoadCurrentFilter()
        {
            if (taskListBox != null)
            {
                var filteredTasks = taskService.GetTasks(currentFilter.Predicate);
                RefreshTaskList(filteredTasks);
                RefreshFilterList(); // Update counts
            }
        }

        #endregion

        #region Task List Panel

        private UIElement BuildTaskPanel()
        {
            taskListBox = new ListBox
            {
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                Padding = new Thickness(4),
                FocusVisualStyle = null,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            taskListBox.SelectionChanged += TaskListBox_SelectionChanged;
            taskListBox.GotFocus += (s, e) => { focusedPanel = 1; UpdateStatusBar(); };
            taskListBox.MouseDoubleClick += (s, e) => ActivateSelectedTask();

            return CreateRetroPanel("TASKS", taskListBox);
        }

        private void RefreshTaskList(List<TaskItem> tasks)
        {
            var selectedId = selectedTask?.Id;
            taskListBox.Items.Clear();

            if (!tasks.Any())
            {
                var emptyText = new TextBlock
                {
                    Text = "  ── NO TASKS ──",
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                    Padding = new Thickness(4)
                };
                taskListBox.Items.Add(emptyText);
                return;
            }

            foreach (var task in tasks.OrderBy(t => t.SortOrder))
            {
                var statusIcon = task.Status switch
                {
                    TaskStatus.Completed => "✓",
                    TaskStatus.InProgress => "►",
                    TaskStatus.Cancelled => "✗",
                    _ => "○"
                };

                var priorityIcon = task.Priority switch
                {
                    TaskPriority.High => "!",
                    TaskPriority.Medium => "·",
                    _ => " "
                };

                // Format: [✓] ! Task Title                 [DUE: 2025-10-26]
                var dueStr = task.DueDate.HasValue ? $"[DUE: {task.DueDate.Value:yyyy-MM-dd}]" : "";
                var text = $"  [{statusIcon}] {priorityIcon} {task.Title,-35} {dueStr}";

                var textBlock = new TextBlock
                {
                    Text = text,
                    Tag = task,
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 11,
                    Padding = new Thickness(4, 2, 4, 2),
                    TextTrimming = TextTrimming.None
                };

                // Color coding
                if (task.Status == TaskStatus.Completed)
                {
                    textBlock.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
                }
                else if (task.IsOverdue)
                {
                    textBlock.Foreground = new SolidColorBrush(theme.Error);
                }
                else if (task.Priority == TaskPriority.High)
                {
                    textBlock.Foreground = new SolidColorBrush(theme.Warning);
                }
                else
                {
                    textBlock.Foreground = new SolidColorBrush(theme.Foreground);
                }

                taskListBox.Items.Add(textBlock);

                if (task.Id == selectedId)
                {
                    taskListBox.SelectedItem = textBlock;
                }
            }

            // Select first if nothing selected
            if (taskListBox.SelectedItem == null && taskListBox.Items.Count > 0)
            {
                taskListBox.SelectedIndex = 0;
            }
        }

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (taskListBox.SelectedItem is TextBlock item && item.Tag is TaskItem task)
            {
                selectedTask = task;
                RefreshDetailPanel();
            }
        }

        private void ActivateSelectedTask()
        {
            if (selectedTask == null) return;

            // Toggle status when activated (common terminal UI pattern)
            selectedTask.Status = selectedTask.Status switch
            {
                TaskStatus.Pending => TaskStatus.InProgress,
                TaskStatus.InProgress => TaskStatus.Completed,
                TaskStatus.Completed => TaskStatus.Pending,
                _ => TaskStatus.Pending
            };

            selectedTask.UpdatedAt = DateTime.Now;
            taskService.UpdateTask(selectedTask);

            LoadCurrentFilter();
            RefreshDetailPanel();

            logger?.Info("RetroTaskWidget", $"Toggled task status: {selectedTask.Title} -> {selectedTask.Status}");
        }

        #endregion

        #region Detail Panel

        private UIElement BuildDetailPanel()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(4)
            };

            detailsTextBlock = new TextBlock
            {
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(4)
            };

            scrollViewer.Content = detailsTextBlock;
            scrollViewer.GotFocus += (s, e) => { focusedPanel = 2; UpdateStatusBar(); };

            return CreateRetroPanel("DETAILS", scrollViewer);
        }

        private void RefreshDetailPanel()
        {
            if (selectedTask == null)
            {
                detailsTextBlock.Text = "┌─────────────────────────────────┐\n" +
                                       "│  NO TASK SELECTED               │\n" +
                                       "│                                 │\n" +
                                       "│  Select a task from the list    │\n" +
                                       "│  to view details                │\n" +
                                       "└─────────────────────────────────┘";
                detailsTextBlock.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
                return;
            }

            detailsTextBlock.Foreground = new SolidColorBrush(theme.Foreground);

            // Build retro-style detail view
            var lines = new List<string>
            {
                "╔═══════════════════════════════════════════════════════╗",
                $"║ {selectedTask.Title.PadRight(53)} ║",
                "╠═══════════════════════════════════════════════════════╣",
                "║                                                       ║",
                $"║  STATUS:    {GetStatusDisplay(selectedTask.Status).PadRight(39)} ║",
                $"║  PRIORITY:  {GetPriorityDisplay(selectedTask.Priority).PadRight(39)} ║",
                $"║  PROGRESS:  {selectedTask.Progress}%".PadRight(55) + " ║",
                "║                                                       ║"
            };

            if (selectedTask.DueDate.HasValue)
            {
                var dueStatus = selectedTask.IsOverdue ? "[OVERDUE]" : "";
                lines.Add($"║  DUE DATE:  {selectedTask.DueDate.Value:yyyy-MM-dd} {dueStatus}".PadRight(55) + " ║");
            }

            if (selectedTask.Tags != null && selectedTask.Tags.Any())
            {
                lines.Add("║                                                       ║");
                lines.Add($"║  TAGS:      {string.Join(", ", selectedTask.Tags).PadRight(39)} ║");
            }

            lines.Add("║                                                       ║");
            lines.Add("╠═══════════════════════════════════════════════════════╣");
            lines.Add("║ DESCRIPTION                                           ║");
            lines.Add("╠═══════════════════════════════════════════════════════╣");

            if (!string.IsNullOrWhiteSpace(selectedTask.Description))
            {
                // Word wrap description
                var descLines = WrapText(selectedTask.Description, 51);
                foreach (var line in descLines)
                {
                    lines.Add($"║ {line.PadRight(53)} ║");
                }
            }
            else
            {
                lines.Add("║ (No description)                                      ║");
            }

            lines.Add("║                                                       ║");

            // Notes section
            if (selectedTask.Notes != null && selectedTask.Notes.Any())
            {
                lines.Add("╠═══════════════════════════════════════════════════════╣");
                lines.Add("║ NOTES                                                 ║");
                lines.Add("╠═══════════════════════════════════════════════════════╣");

                foreach (var note in selectedTask.Notes.OrderByDescending(n => n.CreatedAt).Take(5))
                {
                    var noteHeader = $"[{note.CreatedAt:MM/dd HH:mm}]";
                    lines.Add($"║ {noteHeader.PadRight(53)} ║");

                    var noteLines = WrapText(note.Content, 51);
                    foreach (var line in noteLines)
                    {
                        lines.Add($"║   {line.PadRight(51)} ║");
                    }
                    lines.Add("║                                                       ║");
                }
            }

            // Subtasks
            var subtasks = taskService.GetSubtasks(selectedTask.Id);
            if (subtasks.Any())
            {
                lines.Add("╠═══════════════════════════════════════════════════════╣");
                lines.Add("║ SUBTASKS                                              ║");
                lines.Add("╠═══════════════════════════════════════════════════════╣");

                foreach (var subtask in subtasks)
                {
                    var icon = subtask.Status == TaskStatus.Completed ? "✓" : "○";
                    var subtaskLine = $"  [{icon}] {subtask.Title}";
                    lines.Add($"║ {subtaskLine.PadRight(53)} ║");
                }
                lines.Add("║                                                       ║");
            }

            // Metadata
            lines.Add("╠═══════════════════════════════════════════════════════╣");
            lines.Add($"║ Created: {selectedTask.CreatedAt:yyyy-MM-dd HH:mm:ss}".PadRight(55) + " ║");
            lines.Add($"║ Updated: {selectedTask.UpdatedAt:yyyy-MM-dd HH:mm:ss}".PadRight(55) + " ║");
            lines.Add("╚═══════════════════════════════════════════════════════╝");

            detailsTextBlock.Text = string.Join("\n", lines);
        }

        private List<string> WrapText(string text, int maxWidth)
        {
            var lines = new List<string>();
            var words = text.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                if ((currentLine + " " + word).Length > maxWidth)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine.Trim());
                    }
                    currentLine = word;
                }
                else
                {
                    currentLine += (currentLine.Length > 0 ? " " : "") + word;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine.Trim());
            }

            return lines;
        }

        private string GetStatusDisplay(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Completed => "[✓] COMPLETED",
                TaskStatus.InProgress => "[►] IN PROGRESS",
                TaskStatus.Cancelled => "[✗] CANCELLED",
                _ => "[○] PENDING"
            };
        }

        private string GetPriorityDisplay(TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.High => "[!] HIGH",
                TaskPriority.Medium => "[·] MEDIUM",
                _ => "[ ] LOW"
            };
        }

        #endregion

        #region Status Bar

        private TextBlock BuildStatusBar()
        {
            var statusBar = new TextBlock
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 10,
                Padding = new Thickness(8, 4, 8, 4),
                TextAlignment = TextAlignment.Left
            };

            UpdateStatusBarText(statusBar);
            return statusBar;
        }

        private void UpdateStatusBar()
        {
            if (statusBar != null)
            {
                UpdateStatusBarText(statusBar);
            }
        }

        private void UpdateStatusBarText(TextBlock bar)
        {
            var shortcuts = focusedPanel switch
            {
                0 => "↑/↓=SELECT  ENTER=APPLY  TAB=NEXT PANEL",
                1 => "↑/↓=SELECT  ENTER=TOGGLE STATUS  TAB=NEXT PANEL  N=NEW  D=DELETE  E=EDIT",
                2 => "TAB=NEXT PANEL  ↑/↓=SCROLL",
                _ => ""
            };

            bar.Text = $"┤ {shortcuts.PadRight(80)} ├  ESC=EXIT";
        }

        #endregion

        #region Keyboard Navigation

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Tab to cycle focus between panels
            if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                focusedPanel = (focusedPanel + 1) % 3;
                FocusCurrentPanel();
                e.Handled = true;
                return;
            }

            // Shift+Tab to cycle backwards
            if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                focusedPanel = (focusedPanel - 1 + 3) % 3;
                FocusCurrentPanel();
                e.Handled = true;
                return;
            }

            // Panel-specific shortcuts
            if (focusedPanel == 1) // Task list focused
            {
                HandleTaskListKeys(e);
            }
        }

        private void FocusCurrentPanel()
        {
            switch (focusedPanel)
            {
                case 0:
                    filterListBox?.Focus();
                    Keyboard.Focus(filterListBox);
                    break;
                case 1:
                    taskListBox?.Focus();
                    Keyboard.Focus(taskListBox);
                    break;
                case 2:
                    // Details panel is read-only, just update visual
                    break;
            }
            UpdateStatusBar();
        }

        private void HandleTaskListKeys(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.N: // New task
                    CreateNewTask();
                    e.Handled = true;
                    break;

                case Key.D: // Delete task
                    DeleteSelectedTask();
                    e.Handled = true;
                    break;

                case Key.E: // Edit task
                    EditSelectedTask();
                    e.Handled = true;
                    break;

                case Key.Enter: // Toggle status
                    ActivateSelectedTask();
                    e.Handled = true;
                    break;

                case Key.Space: // Also toggle status
                    ActivateSelectedTask();
                    e.Handled = true;
                    break;
            }
        }

        private void CreateNewTask()
        {
            var title = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter task title:",
                "NEW TASK",
                "");

            if (!string.IsNullOrWhiteSpace(title))
            {
                var task = new TaskItem
                {
                    Title = title,
                    Status = TaskStatus.Pending,
                    Priority = TaskPriority.Medium,
                    SortOrder = taskService.GetTasks(t => true).Count * 100
                };

                taskService.AddTask(task);
                LoadCurrentFilter();
                logger?.Info("RetroTaskWidget", $"Created task: {title}");
            }
        }

        private void DeleteSelectedTask()
        {
            if (selectedTask == null) return;

            var result = MessageBox.Show(
                $"DELETE TASK:\n\n{selectedTask.Title}\n\nAre you sure?",
                "CONFIRM DELETE",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                taskService.DeleteTask(selectedTask.Id);
                selectedTask = null;
                LoadCurrentFilter();
                RefreshDetailPanel();
                logger?.Info("RetroTaskWidget", "Deleted task");
            }
        }

        private void EditSelectedTask()
        {
            if (selectedTask == null) return;

            // Simple edit dialog for title
            var newTitle = Microsoft.VisualBasic.Interaction.InputBox(
                "Edit task title:",
                "EDIT TASK",
                selectedTask.Title);

            if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != selectedTask.Title)
            {
                selectedTask.Title = newTitle;
                selectedTask.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(selectedTask);
                LoadCurrentFilter();
                RefreshDetailPanel();
                logger?.Info("RetroTaskWidget", $"Updated task title: {newTitle}");
            }
        }

        #endregion

        #region Widget Lifecycle

        public override void OnWidgetFocusReceived()
        {
            FocusCurrentPanel();
        }

        public override void OnWidgetFocusLost()
        {
            // Keep visual state
        }

        public void ApplyTheme()
        {
            theme = themeManager.CurrentTheme;

            // Rebuild UI with new theme
            if (mainGrid != null)
            {
                BuildUI();
                RefreshFilterList();
                LoadCurrentFilter();
                RefreshDetailPanel();
            }

            logger?.Debug("RetroTaskWidget", "Applied theme update");
        }

        protected override void OnDispose()
        {
            if (filterListBox != null)
            {
                filterListBox.SelectionChanged -= FilterListBox_SelectionChanged;
            }

            if (taskListBox != null)
            {
                taskListBox.SelectionChanged -= TaskListBox_SelectionChanged;
            }

            logger?.Info("RetroTaskWidget", "Retro Task Management widget disposed");
            base.OnDispose();
        }

        #endregion
    }
}
