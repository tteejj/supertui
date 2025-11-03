using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Calendar view pane showing tasks on a month/week calendar grid
    /// Features: Month/week view toggle, keyboard navigation, task preview
    /// Terminal aesthetic with monospace font and theme colors
    /// </summary>
    public class CalendarPane : PaneBase
    {
        #region Fields

        // Services
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly IEventBus eventBus;

        // Event handlers (stored for unsubscription)
        private Action<Core.Events.ProjectSelectedEvent> projectSelectedHandler;
        private Action<Core.Events.TaskSelectedEvent> taskSelectedHandler;
        private Action<Core.Events.TaskCreatedEvent> taskCreatedHandler;
        private Action<Core.Events.TaskUpdatedEvent> taskUpdatedHandler;
        private Action<Core.Events.RefreshRequestedEvent> refreshRequestedHandler;

        // UI Components
        private Grid mainLayout;
        private Grid calendarGrid;
        private TextBlock monthYearLabel;
        private TextBlock statusBar;
        private StackPanel legendPanel;

        // State
        private DateTime currentMonth;
        private CalendarViewMode viewMode = CalendarViewMode.Month;
        private DateTime? selectedDate;
        private List<TaskItem> allTasks = new List<TaskItem>();
        private Guid? highlightedTaskId; // For highlighting a specific task's due date

        // Theme colors (cached)
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush borderBrush;
        private SolidColorBrush dimBrush;
        private SolidColorBrush surfaceBrush;
        private SolidColorBrush errorBrush;
        private SolidColorBrush warningBrush;
        private SolidColorBrush infoBrush;

        #endregion

        #region Constructor

        public CalendarPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            ITaskService taskService,
            IProjectService projectService,
            IEventBus eventBus)
            : base(logger, themeManager, projectContext)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            PaneName = "Calendar";
            PaneIcon = "📅";
            currentMonth = DateTime.Today;
            selectedDate = DateTime.Today;
        }

        #endregion

        #region Initialization

        public override void Initialize()
        {
            base.Initialize();

            // Register pane-specific shortcuts
            RegisterPaneShortcuts();

            // Subscribe to task events
            taskService.TaskAdded += OnTaskChanged;
            taskService.TaskUpdated += OnTaskChanged;
            taskService.TaskDeleted += OnTaskDeleted;

            // Subscribe to theme changes
            themeManager.ThemeChanged += OnThemeChanged;

            // Subscribe to EventBus events for cross-pane communication
            projectSelectedHandler = OnProjectSelected;
            eventBus.Subscribe(projectSelectedHandler);

            taskSelectedHandler = OnTaskSelected;
            eventBus.Subscribe(taskSelectedHandler);

            taskCreatedHandler = OnTaskCreated;
            eventBus.Subscribe(taskCreatedHandler);

            taskUpdatedHandler = OnTaskUpdatedEvent;
            eventBus.Subscribe(taskUpdatedHandler);

            refreshRequestedHandler = OnRefreshRequested;
            eventBus.Subscribe(refreshRequestedHandler);

            // Set initial focus
            Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Windows.Input.Keyboard.Focus(this);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;

            // Navigation shortcuts
            shortcuts.RegisterForPane(PaneName, Key.Left, ModifierKeys.None,
                () => {
                    if (viewMode == CalendarViewMode.Month)
                    {
                        currentMonth = currentMonth.AddMonths(-1);
                    }
                    else
                    {
                        selectedDate = (selectedDate ?? DateTime.Today).AddDays(-7);
                    }
                    RenderCalendar();
                },
                "Previous month/week");

            shortcuts.RegisterForPane(PaneName, Key.Right, ModifierKeys.None,
                () => {
                    if (viewMode == CalendarViewMode.Month)
                    {
                        currentMonth = currentMonth.AddMonths(1);
                    }
                    else
                    {
                        selectedDate = (selectedDate ?? DateTime.Today).AddDays(7);
                    }
                    RenderCalendar();
                },
                "Next month/week");

            shortcuts.RegisterForPane(PaneName, Key.Up, ModifierKeys.None,
                () => {
                    if (selectedDate.HasValue)
                    {
                        selectedDate = selectedDate.Value.AddDays(-7);
                        RenderCalendar();
                    }
                },
                "Move selection up one week");

            shortcuts.RegisterForPane(PaneName, Key.Down, ModifierKeys.None,
                () => {
                    if (selectedDate.HasValue)
                    {
                        selectedDate = selectedDate.Value.AddDays(7);
                        RenderCalendar();
                    }
                },
                "Move selection down one week");

            // View mode shortcuts
            shortcuts.RegisterForPane(PaneName, Key.M, ModifierKeys.None,
                () => {
                    viewMode = CalendarViewMode.Month;
                    RenderCalendar();
                },
                "Switch to month view");

            shortcuts.RegisterForPane(PaneName, Key.W, ModifierKeys.None,
                () => {
                    viewMode = CalendarViewMode.Week;
                    RenderCalendar();
                },
                "Switch to week view");

            // Action shortcuts
            shortcuts.RegisterForPane(PaneName, Key.Enter, ModifierKeys.None,
                () => {
                    if (selectedDate.HasValue)
                    {
                        ShowTasksForDate(selectedDate.Value);
                    }
                },
                "Show tasks for selected date");

            shortcuts.RegisterForPane(PaneName, Key.T, ModifierKeys.Control,
                () => {
                    if (selectedDate.HasValue)
                    {
                        CreateTaskForDate(selectedDate.Value);
                    }
                },
                "Create new task for selected date");

            shortcuts.RegisterForPane(PaneName, Key.Home, ModifierKeys.None,
                () => {
                    currentMonth = DateTime.Today;
                    selectedDate = DateTime.Today;
                    RenderCalendar();
                },
                "Jump to today");
        }

        protected override UIElement BuildContent()
        {
            // Cache theme colors
            CacheThemeColors();

            mainLayout = new Grid();
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Calendar
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Legend
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

            // Month/Year header
            var headerPanel = BuildHeaderPanel();
            Grid.SetRow(headerPanel, 0);
            mainLayout.Children.Add(headerPanel);

            // Calendar grid
            calendarGrid = BuildCalendarGrid();
            Grid.SetRow(calendarGrid, 1);
            mainLayout.Children.Add(calendarGrid);

            // Legend
            legendPanel = BuildLegendPanel();
            Grid.SetRow(legendPanel, 2);
            mainLayout.Children.Add(legendPanel);

            // Status bar
            statusBar = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = dimBrush,
                Margin = new Thickness(8, 4, 8, 4),
                Text = "←/→: Prev/Next Month | M: Month View | W: Week View | Enter: View Tasks | Ctrl+T: New Task"
            };
            Grid.SetRow(statusBar, 3);
            mainLayout.Children.Add(statusBar);

            // Keyboard shortcuts
            this.PreviewKeyDown += OnPreviewKeyDown;

            // Load initial data
            LoadTasks();
            RenderCalendar();

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
            errorBrush = new SolidColorBrush(theme.Error);
            warningBrush = new SolidColorBrush(theme.Warning);
            infoBrush = new SolidColorBrush(theme.Info);
        }

        #endregion

        #region Header

        private Grid BuildHeaderPanel()
        {
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Month/Year label
            monthYearLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Margin = new Thickness(8, 8, 8, 12),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(monthYearLabel, 0);
            headerGrid.Children.Add(monthYearLabel);

            // View mode indicator
            var viewModeLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Margin = new Thickness(8, 8, 8, 12),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            viewModeLabel.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text")
            {
                Source = this,
                Path = new PropertyPath(nameof(ViewModeText))
            });
            Grid.SetColumn(viewModeLabel, 1);
            headerGrid.Children.Add(viewModeLabel);

            return headerGrid;
        }

        private string ViewModeText => viewMode == CalendarViewMode.Month ? "[Month View]" : "[Week View]";

        #endregion

        #region Calendar Grid

        private Grid BuildCalendarGrid()
        {
            var grid = new Grid();

            // 7 columns (days of week)
            for (int i = 0; i < 7; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // 7 rows: header + 6 weeks
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Day names
            for (int i = 0; i < 6; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            // Day names header
            var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            for (int col = 0; col < 7; col++)
            {
                var dayHeader = new TextBlock
                {
                    Text = dayNames[col],
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = dimBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 8)
                };
                Grid.SetColumn(dayHeader, col);
                Grid.SetRow(dayHeader, 0);
                grid.Children.Add(dayHeader);
            }

            return grid;
        }

        #endregion

        #region Legend

        private StackPanel BuildLegendPanel()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, 8, 8, 4)
            };

            // Priority legend
            var priorities = new[]
            {
                new { Icon = "‼", Label = "Today", Brush = errorBrush },
                new { Icon = "↑", Label = "High", Brush = warningBrush },
                new { Icon = "●", Label = "Medium", Brush = infoBrush },
                new { Icon = "↓", Label = "Low", Brush = dimBrush }
            };

            foreach (var pri in priorities)
            {
                var legendItem = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(12, 0, 12, 0)
                };

                var icon = new TextBlock
                {
                    Text = pri.Icon,
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 12,
                    Foreground = pri.Brush,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                legendItem.Children.Add(icon);

                var label = new TextBlock
                {
                    Text = pri.Label,
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 12,
                    Foreground = dimBrush
                };
                legendItem.Children.Add(label);

                panel.Children.Add(legendItem);
            }

            return panel;
        }

        #endregion

        #region Data Loading

        private void LoadTasks()
        {
            try
            {
                allTasks = taskService.GetAllTasks(includeDeleted: false);

                // Filter by project context
                if (projectContext.CurrentProject != null)
                {
                    allTasks = allTasks.Where(t => t.ProjectId == projectContext.CurrentProject.Id).ToList();
                }

                // Only include tasks with due dates
                allTasks = allTasks.Where(t => t.DueDate.HasValue).ToList();

                Log($"Loaded {allTasks.Count} tasks with due dates");
            }
            catch (Exception ex)
            {
                Log($"Error loading tasks: {ex.Message}", LogLevel.Error);
                allTasks = new List<TaskItem>();
            }
        }

        private void OnTaskChanged(TaskItem task)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                LoadTasks();
                RenderCalendar();
            });
        }

        private void OnTaskDeleted(Guid taskId)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                LoadTasks();
                RenderCalendar();
            });
        }

        protected override void OnProjectContextChanged(object sender, ProjectContextChangedEventArgs e)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                LoadTasks();
                RenderCalendar();
            });
        }

        #endregion

        #region Calendar Rendering

        private void RenderCalendar()
        {
            // Clear existing cells (keep header row)
            var cellsToRemove = calendarGrid.Children.OfType<Border>().ToList();
            foreach (var cell in cellsToRemove)
            {
                calendarGrid.Children.Remove(cell);
            }

            if (viewMode == CalendarViewMode.Month)
            {
                RenderMonthView();
            }
            else
            {
                RenderWeekView();
            }

            UpdateHeader();
        }

        private void RenderMonthView()
        {
            // Get first day of month
            var firstDayOfMonth = new DateTime(currentMonth.Year, currentMonth.Month, 1);
            var firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek; // 0 = Sunday

            // Get days in current month
            var daysInMonth = DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month);

            // Start date (may be in previous month)
            var currentDate = firstDayOfMonth.AddDays(-firstDayOfWeek);

            // Render 6 weeks (42 days)
            for (int week = 0; week < 6; week++)
            {
                for (int day = 0; day < 7; day++)
                {
                    var cellDate = currentDate;
                    var cell = BuildCalendarCell(cellDate, cellDate.Month == currentMonth.Month);

                    Grid.SetColumn(cell, day);
                    Grid.SetRow(cell, week + 1); // +1 for header row
                    calendarGrid.Children.Add(cell);

                    currentDate = currentDate.AddDays(1);
                }
            }
        }

        private void RenderWeekView()
        {
            // Get start of week (Sunday) for selected date
            var startOfWeek = selectedDate ?? DateTime.Today;
            while (startOfWeek.DayOfWeek != DayOfWeek.Sunday)
            {
                startOfWeek = startOfWeek.AddDays(-1);
            }

            // Render 1 week (7 days) - spread across all 6 rows for more space
            for (int day = 0; day < 7; day++)
            {
                var cellDate = startOfWeek.AddDays(day);
                var cell = BuildCalendarCell(cellDate, true, isWeekView: true);

                Grid.SetColumn(cell, day);
                Grid.SetRow(cell, 1);
                Grid.SetRowSpan(cell, 6); // Use all 6 rows for better visibility
                calendarGrid.Children.Add(cell);
            }
        }

        private Border BuildCalendarCell(DateTime date, bool isCurrentMonth, bool isWeekView = false)
        {
            var theme = themeManager.CurrentTheme;
            var isToday = date.Date == DateTime.Today;
            var isSelected = selectedDate.HasValue && date.Date == selectedDate.Value.Date;

            // Get tasks for this date
            var tasksForDate = allTasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == date.Date)
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.Title)
                .ToList();

            // Check if this date contains the highlighted task
            var hasHighlightedTask = highlightedTaskId.HasValue &&
                                    tasksForDate.Any(t => t.Id == highlightedTaskId.Value);

            // Cell container with visual highlight for selected task
            var cellBorder = new Border
            {
                BorderBrush = hasHighlightedTask ? accentBrush : borderBrush,
                BorderThickness = hasHighlightedTask ? new Thickness(2) : new Thickness(0.5),
                Background = isSelected ? new SolidColorBrush(theme.Surface) :
                             hasHighlightedTask ? new SolidColorBrush(Color.FromArgb(60, theme.Primary.R, theme.Primary.G, theme.Primary.B)) :
                             isToday ? new SolidColorBrush(Color.FromArgb(40, theme.Primary.R, theme.Primary.G, theme.Primary.B)) :
                             bgBrush,
                Padding = new Thickness(4),
                Cursor = Cursors.Hand
            };

            // Cell content
            var cellStack = new StackPanel();

            // Date number
            var dateText = new TextBlock
            {
                Text = date.Day.ToString(),
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = isWeekView ? 16 : 14,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isCurrentMonth ? (isToday ? accentBrush : fgBrush) : dimBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 4)
            };
            cellStack.Children.Add(dateText);

            // Task list (limit based on view mode)
            int maxTasks = isWeekView ? 15 : 5;
            int displayedTasks = 0;

            foreach (var task in tasksForDate.Take(maxTasks))
            {
                var taskPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 1, 0, 1)
                };

                // Priority icon
                var priorityIcon = new TextBlock
                {
                    Text = GetPriorityIcon(task.Priority),
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 10,
                    Foreground = GetPriorityColor(task.Priority),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                taskPanel.Children.Add(priorityIcon);

                // Task title (truncated)
                var taskText = new TextBlock
                {
                    Text = task.Title,
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = isWeekView ? 12 : 10,
                    Foreground = task.Status == TaskStatus.Completed ? dimBrush : fgBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextDecorations = task.Status == TaskStatus.Completed ? TextDecorations.Strikethrough : null
                };
                taskPanel.Children.Add(taskText);

                cellStack.Children.Add(taskPanel);
                displayedTasks++;
            }

            // Show "more" indicator if tasks exceed limit
            if (tasksForDate.Count > maxTasks)
            {
                var moreText = new TextBlock
                {
                    Text = $"+{tasksForDate.Count - maxTasks} more...",
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 10,
                    Foreground = dimBrush,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                cellStack.Children.Add(moreText);
            }

            cellBorder.Child = cellStack;

            // Click handler
            cellBorder.MouseLeftButtonDown += (s, e) =>
            {
                selectedDate = date;
                RenderCalendar();
                e.Handled = true;
            };

            // Double-click to view tasks
            cellBorder.MouseLeftButtonUp += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    ShowTasksForDate(date);
                }
            };

            // Store date in Tag for keyboard navigation
            cellBorder.Tag = date;

            return cellBorder;
        }

        #endregion

        #region Header Update

        private void UpdateHeader()
        {
            if (viewMode == CalendarViewMode.Month)
            {
                monthYearLabel.Text = currentMonth.ToString("MMMM yyyy");
            }
            else
            {
                var startOfWeek = selectedDate ?? DateTime.Today;
                while (startOfWeek.DayOfWeek != DayOfWeek.Sunday)
                {
                    startOfWeek = startOfWeek.AddDays(-1);
                }
                var endOfWeek = startOfWeek.AddDays(6);
                monthYearLabel.Text = $"{startOfWeek:MMM d} - {endOfWeek:MMM d, yyyy}";
            }
        }

        #endregion

        #region Keyboard Navigation

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Dispatch to ShortcutManager
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
            {
                e.Handled = true;
            }
        }

        #endregion

        #region Actions

        private void ShowTasksForDate(DateTime date)
        {
            var tasksForDate = allTasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == date.Date)
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.Title)
                .ToList();

            if (!tasksForDate.Any())
            {
                MessageBox.Show(
                    $"No tasks due on {date:MMM d, yyyy}",
                    "Calendar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // If there's only one task, publish TaskSelectedEvent for cross-pane communication
            if (tasksForDate.Count == 1)
            {
                var task = tasksForDate[0];
                eventBus.Publish(new Core.Events.TaskSelectedEvent
                {
                    TaskId = task.Id,
                    ProjectId = task.ProjectId,
                    Task = task,
                    SourceWidget = PaneName
                });
                Log($"Published TaskSelectedEvent for task: {task.Title}");
            }

            // Build task list
            var taskList = string.Join("\n", tasksForDate.Select(t =>
                $"{GetPriorityIcon(t.Priority)} {t.Title} {(t.Status == TaskStatus.Completed ? "(✓)" : "")}"));

            MessageBox.Show(
                $"Tasks due on {date:MMM d, yyyy}:\n\n{taskList}",
                "Calendar - Tasks",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CreateTaskForDate(DateTime date)
        {
            // Simple task creation dialog
            var theme = themeManager.CurrentTheme;
            var dialog = new Window
            {
                Title = $"New Task - {date:MMM d, yyyy}",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(theme.Surface)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Task Title:",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(16, 16, 16, 8)
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var titleInput = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Margin = new Thickness(16, 0, 16, 16),
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1)
            };
            Grid.SetRow(titleInput, 1);
            grid.Children.Add(titleInput);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 16, 16)
            };

            var createBtn = new Button
            {
                Content = "Create",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("JetBrains Mono, Consolas")
            };
            createBtn.Click += (s, e) =>
            {
                var title = titleInput.Text.Trim();
                if (string.IsNullOrEmpty(title))
                {
                    MessageBox.Show("Task title cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var task = new TaskItem
                {
                    Title = title,
                    DueDate = date,
                    ProjectId = projectContext.CurrentProject?.Id,
                    Status = TaskStatus.Pending,
                    Priority = TaskPriority.Medium
                };

                taskService.AddTask(task);
                dialog.DialogResult = true;
                dialog.Close();

                Log($"Created task '{title}' for {date:MMM d, yyyy}");
            };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 80,
                Padding = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("JetBrains Mono, Consolas")
            };
            cancelBtn.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(createBtn);
            buttonPanel.Children.Add(cancelBtn);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            titleInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    createBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else if (e.Key == Key.Escape)
                {
                    dialog.Close();
                }
            };

            System.Windows.Input.Keyboard.Focus(titleInput);
            dialog.ShowDialog();
        }

        #endregion

        #region Helpers

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
                TaskPriority.Low => dimBrush,
                TaskPriority.Medium => infoBrush,
                TaskPriority.High => warningBrush,
                TaskPriority.Today => errorBrush,
                _ => fgBrush
            };
        }

        #endregion

        #region State Persistence

        public override PaneState SaveState()
        {
            return new PaneState
            {
                PaneType = "CalendarPane",
                CustomData = new Dictionary<string, object>
                {
                    ["CurrentMonth"] = currentMonth.ToString("yyyy-MM-dd"),
                    ["ViewMode"] = viewMode.ToString(),
                    ["SelectedDate"] = selectedDate?.ToString("yyyy-MM-dd")
                }
            };
        }

        public override void RestoreState(PaneState state)
        {
            if (state?.CustomData == null) return;

            var data = state.CustomData as Dictionary<string, object>;
            if (data == null) return;

            // Restore current month
            if (data.TryGetValue("CurrentMonth", out var monthStr) && DateTime.TryParse(monthStr?.ToString(), out var month))
            {
                currentMonth = month;
            }

            // Restore view mode
            if (data.TryGetValue("ViewMode", out var viewStr) && Enum.TryParse<CalendarViewMode>(viewStr?.ToString(), out var mode))
            {
                viewMode = mode;
            }

            // Restore selected date
            if (data.TryGetValue("SelectedDate", out var dateStr) && DateTime.TryParse(dateStr?.ToString(), out var date))
            {
                selectedDate = date;
            }

            RenderCalendar();
        }

        #endregion

        #region EventBus Handlers

        /// <summary>
        /// Handle ProjectSelectedEvent - filter calendar to show only project tasks
        /// </summary>
        private void OnProjectSelected(Core.Events.ProjectSelectedEvent evt)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                if (evt.Project != null)
                {
                    Log($"Project selected via EventBus: {evt.Project.Name}");
                    // ProjectContext will be updated separately, just refresh calendar
                    LoadTasks();
                    RenderCalendar();
                }
            });
        }

        /// <summary>
        /// Handle TaskSelectedEvent - highlight the selected task's due date on calendar
        /// </summary>
        private void OnTaskSelected(Core.Events.TaskSelectedEvent evt)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                if (evt.Task != null && evt.Task.DueDate.HasValue)
                {
                    Log($"Task selected via EventBus: {evt.Task.Title}");
                    highlightedTaskId = evt.TaskId;

                    // Navigate to the month containing the task's due date
                    currentMonth = new DateTime(evt.Task.DueDate.Value.Year, evt.Task.DueDate.Value.Month, 1);
                    selectedDate = evt.Task.DueDate.Value.Date;

                    RenderCalendar();
                }
            });
        }

        /// <summary>
        /// Handle TaskCreatedEvent - refresh calendar when tasks are created
        /// </summary>
        private void OnTaskCreated(Core.Events.TaskCreatedEvent evt)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                Log($"Task created via EventBus: {evt.Title}");
                LoadTasks();
                RenderCalendar();
            });
        }

        /// <summary>
        /// Handle TaskUpdatedEvent - refresh calendar when tasks are updated
        /// </summary>
        private void OnTaskUpdatedEvent(Core.Events.TaskUpdatedEvent evt)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                if (evt.Task != null)
                {
                    Log($"Task updated via EventBus: {evt.Task.Title}");
                    LoadTasks();
                    RenderCalendar();
                }
            });
        }

        #endregion

        #region Cleanup

        private void OnThemeChanged(object sender, EventArgs e)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                ApplyTheme();
            });
        }

        private void ApplyTheme()
        {
            CacheThemeColors();

            // Update all controls
            if (calendarGrid != null)
            {
                // Calendar cells will be updated in RenderCalendar()
                RenderCalendar();
            }

            if (monthYearLabel != null)
            {
                monthYearLabel.Foreground = accentBrush;
            }

            if (statusBar != null)
            {
                statusBar.Foreground = dimBrush;
            }

            this.InvalidateVisual();
        }

        protected override void OnDispose()
        {
            // Unsubscribe from EventBus to prevent memory leaks
            if (projectSelectedHandler != null)
            {
                eventBus.Unsubscribe(projectSelectedHandler);
            }

            if (taskSelectedHandler != null)
            {
                eventBus.Unsubscribe(taskSelectedHandler);
            }

            if (taskCreatedHandler != null)
            {
                eventBus.Unsubscribe(taskCreatedHandler);
            }

            if (taskUpdatedHandler != null)
            {
                eventBus.Unsubscribe(taskUpdatedHandler);
            }

            if (refreshRequestedHandler != null)
            {
                eventBus.Unsubscribe(refreshRequestedHandler);
            }

            // Unsubscribe from task service events
            taskService.TaskAdded -= OnTaskChanged;
            taskService.TaskUpdated -= OnTaskChanged;
            taskService.TaskDeleted -= OnTaskDeleted;

            // Unsubscribe from theme changes
            themeManager.ThemeChanged -= OnThemeChanged;

            base.OnDispose();
        }

        /// <summary>
        /// Handle RefreshRequestedEvent - reload tasks and re-render calendar
        /// </summary>
        private void OnRefreshRequested(Core.Events.RefreshRequestedEvent evt)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                LoadTasks();
                RenderCalendar();
                Log("CalendarPane refreshed (RefreshRequestedEvent)");
            });
        }

        #endregion
    }

    /// <summary>
    /// Calendar view mode enumeration
    /// </summary>
    public enum CalendarViewMode
    {
        Month,
        Week
    }
}
