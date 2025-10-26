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

namespace SuperTUI.Widgets
{
    /// <summary>
    /// KEYBOARD-CENTRIC task management widget with terminal aesthetic
    /// Tab/Shift-Tab to cycle filter dropdowns, Arrow keys for task navigation
    /// </summary>
    public class TaskManagementWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;

        private Theme theme;
        private TaskService taskService;

        // UI Components
        private Grid mainGrid;
        private List<FilterDropdown> filterDropdowns = new List<FilterDropdown>();
        private int currentFocusedFilterIndex = -1; // -1 means task list has focus

        // State
        private List<TaskItem> currentTasks = new List<TaskItem>();
        private int selectedTaskIndex = 0;
        private TaskItem selectedTask;

        // Filter state
        private string timeFilter = "ALL";
        private string statusFilter = "ALL";
        private string priorityFilter = "ALL";
        private string tagFilter = "ALL";

        public TaskManagementWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            WidgetName = "Task Manager";
            WidgetType = "TaskManagement";
        }

        public TaskManagementWidget() : this(Logger.Instance, ThemeManager.Instance, ConfigurationManager.Instance)
        {
        }

        public override void Initialize()
        {
            theme = themeManager.CurrentTheme;
            taskService = TaskService.Instance;
            taskService.Initialize();

            LoadCurrentFilter();

            this.Focusable = true;
            this.PreviewKeyDown += OnPreviewKeyDown;

            BuildUI();

            // Force focus after UI is built
            this.Focus();
            Keyboard.Focus(this);

            logger?.Info("TaskWidget", "Keyboard-centric Task Management widget initialized");
        }

        private void LoadCurrentFilter()
        {
            // Apply combined filters
            currentTasks = taskService.GetTasks(task =>
            {
                // Time filter
                bool timeMatch = timeFilter == "ALL" ||
                    (timeFilter == "TODAY" && task.DueDate.HasValue && task.DueDate.Value.Date == DateTime.Today) ||
                    (timeFilter == "WEEK" && task.DueDate.HasValue && task.DueDate.Value.Date <= DateTime.Today.AddDays(7)) ||
                    (timeFilter == "MONTH" && task.DueDate.HasValue && task.DueDate.Value.Date <= DateTime.Today.AddMonths(1)) ||
                    (timeFilter == "OVERDUE" && task.IsOverdue);

                // Status filter
                bool statusMatch = statusFilter == "ALL" ||
                    (statusFilter == "ACTIVE" && task.Status == TaskStatus.InProgress) ||
                    (statusFilter == "PENDING" && task.Status == TaskStatus.Pending) ||
                    (statusFilter == "COMPLETED" && task.Status == TaskStatus.Completed) ||
                    (statusFilter == "BLOCKED" && task.Status == TaskStatus.Cancelled);

                // Priority filter
                bool priorityMatch = priorityFilter == "ALL" ||
                    (priorityFilter == "HIGH" && task.Priority == TaskPriority.High) ||
                    (priorityFilter == "MEDIUM" && task.Priority == TaskPriority.Medium) ||
                    (priorityFilter == "LOW" && task.Priority == TaskPriority.Low);

                // Tag filter (simplified - just check if any tag matches)
                bool tagMatch = tagFilter == "ALL" ||
                    (task.Tags != null && task.Tags.Contains(tagFilter));

                return timeMatch && statusMatch && priorityMatch && tagMatch;
            }).ToList();

            if (selectedTaskIndex >= currentTasks.Count)
                selectedTaskIndex = Math.Max(0, currentTasks.Count - 1);

            selectedTask = currentTasks.Count > 0 ? currentTasks[selectedTaskIndex] : null;
        }

        private void BuildUI()
        {
            mainGrid = new Grid
            {
                Background = new SolidColorBrush(theme.Background),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Outer border - NO MARGIN to fill screen
            var outerBorder = new Border
            {
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0),
                Padding = new Thickness(15)
            };

            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filter bar
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main content
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Stats
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Shortcuts

            // Filter bar
            var filterBar = CreateFilterBar();
            Grid.SetRow(filterBar, 0);
            contentGrid.Children.Add(filterBar);

            // Line after filter
            var line1 = CreateHorizontalLine();
            Grid.SetRow(line1, 1);
            contentGrid.Children.Add(line1);

            // Main content (Tasks | Details)
            var mainContent = CreateMainContent();
            Grid.SetRow(mainContent, 2);
            contentGrid.Children.Add(mainContent);

            // Line after main content
            var line2 = CreateHorizontalLine();
            Grid.SetRow(line2, 3);
            contentGrid.Children.Add(line2);

            // Stats
            var stats = CreateStats();
            Grid.SetRow(stats, 4);
            contentGrid.Children.Add(stats);

            // Line after stats
            var line3 = CreateHorizontalLine();
            Grid.SetRow(line3, 5);
            contentGrid.Children.Add(line3);

            // Shortcuts
            var shortcuts = CreateShortcuts();
            Grid.SetRow(shortcuts, 6);
            contentGrid.Children.Add(shortcuts);

            outerBorder.Child = contentGrid;
            mainGrid.Children.Add(outerBorder);
            this.Content = mainGrid;
        }

        private System.Windows.Shapes.Rectangle CreateHorizontalLine()
        {
            return new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(theme.Primary),
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private System.Windows.Shapes.Rectangle CreateVerticalLine()
        {
            return new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(theme.Primary),
                Width = 1,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }

        private TextBlock CreateText(string text, double size = 12, FontWeight? weight = null)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = size,
                FontWeight = weight ?? FontWeights.Normal,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 3, 0, 3)
            };
        }

        private StackPanel CreateFilterBar()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            panel.Children.Add(CreateText("FILTER: ", 12, FontWeights.Bold));

            // Create filter dropdowns
            filterDropdowns.Clear();

            var timeDropdown = new FilterDropdown("TIME", 1, new[] { "ALL", "TODAY", "WEEK", "MONTH", "OVERDUE" }, theme);
            timeDropdown.SelectionChanged += (selected) => { timeFilter = selected; LoadCurrentFilter(); BuildUI(); };
            filterDropdowns.Add(timeDropdown);
            panel.Children.Add(timeDropdown.GetControl());

            var statusDropdown = new FilterDropdown("STATUS", 2, new[] { "ALL", "ACTIVE", "PENDING", "COMPLETED", "BLOCKED" }, theme);
            statusDropdown.SelectionChanged += (selected) => { statusFilter = selected; LoadCurrentFilter(); BuildUI(); };
            filterDropdowns.Add(statusDropdown);
            panel.Children.Add(statusDropdown.GetControl());

            var priorityDropdown = new FilterDropdown("PRIORITY", 3, new[] { "ALL", "HIGH", "MEDIUM", "LOW" }, theme);
            priorityDropdown.SelectionChanged += (selected) => { priorityFilter = selected; LoadCurrentFilter(); BuildUI(); };
            filterDropdowns.Add(priorityDropdown);
            panel.Children.Add(priorityDropdown.GetControl());

            var tagDropdown = new FilterDropdown("TAGS", 4, new[] { "ALL", "urgent", "bug", "feature", "docs" }, theme);
            tagDropdown.SelectionChanged += (selected) => { tagFilter = selected; LoadCurrentFilter(); BuildUI(); };
            filterDropdowns.Add(tagDropdown);
            panel.Children.Add(tagDropdown.GetControl());

            return panel;
        }

        private Grid CreateMainContent()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Tasks
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) }); // Separator
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); // Details

            // Tasks area
            var tasksArea = CreateTasksArea();
            Grid.SetColumn(tasksArea, 0);
            grid.Children.Add(tasksArea);

            // Vertical separator
            var sep = CreateVerticalLine();
            Grid.SetColumn(sep, 1);
            grid.Children.Add(sep);

            // Details area
            var detailsArea = CreateDetailPanel();
            Grid.SetColumn(detailsArea, 2);
            grid.Children.Add(detailsArea);

            return grid;
        }

        private Grid CreateTasksArea()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) }); // Line
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Rows

            // Table header
            var header = CreateTaskTableHeader();
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Horizontal line after header
            var line = CreateHorizontalLine();
            Grid.SetRow(line, 1);
            grid.Children.Add(line);

            // Task rows in ScrollViewer
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent
            };

            var stack = new StackPanel();
            for (int i = 0; i < currentTasks.Count; i++)
            {
                var task = currentTasks[i];
                var isSelected = (i == selectedTaskIndex);
                var taskRow = CreateTaskRow(task, i + 1, isSelected);
                stack.Children.Add(taskRow);
            }

            if (currentTasks.Count == 0)
            {
                var noTasks = CreateText("No tasks match current filters", 11);
                noTasks.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
                noTasks.Margin = new Thickness(10);
                stack.Children.Add(noTasks);
            }

            scrollViewer.Content = stack;
            Grid.SetRow(scrollViewer, 2);
            grid.Children.Add(scrollViewer);

            return grid;
        }

        private Grid CreateTaskTableHeader()
        {
            var grid = new Grid { Margin = new Thickness(5, 5, 5, 5) };

            // Column definitions MUST match task row exactly
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });   // #
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });    // Line
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });    // Line
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });  // Status
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });    // Line
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });   // Priority
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });    // Line
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });  // Due Date

            AddHeaderCell(grid, "#", 0);
            AddVerticalLine(grid, 1);
            AddHeaderCell(grid, "TITLE", 2);
            AddVerticalLine(grid, 3);
            AddHeaderCell(grid, "STATUS", 4);
            AddVerticalLine(grid, 5);
            AddHeaderCell(grid, "PRIORITY", 6);
            AddVerticalLine(grid, 7);
            AddHeaderCell(grid, "DUE DATE", 8);

            return grid;
        }

        private void AddHeaderCell(Grid grid, string text, int column)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(5, 0, 5, 0)
            };
            Grid.SetColumn(tb, column);
            grid.Children.Add(tb);
        }

        private void AddVerticalLine(Grid grid, int column)
        {
            var line = CreateVerticalLine();
            Grid.SetColumn(line, column);
            grid.Children.Add(line);
        }

        private Grid CreateTaskRow(TaskItem task, int rowNumber, bool isSelected)
        {
            var grid = new Grid
            {
                Background = isSelected ? new SolidColorBrush(Color.FromArgb(40, theme.Primary.R, theme.Primary.G, theme.Primary.B)) : Brushes.Transparent,
                Margin = new Thickness(5, 0, 5, 0)
            };

            // EXACT same column definitions as header
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            // # with selection marker
            AddCell(grid, (isSelected ? "►" : "") + rowNumber, 0);
            AddVerticalLine(grid, 1);

            // Title
            var title = task.Title.Length > 50 ? task.Title.Substring(0, 47) + "..." : task.Title;
            AddCell(grid, title, 2);
            AddVerticalLine(grid, 3);

            // Status
            AddCell(grid, task.Status.ToString().ToUpper(), 4);
            AddVerticalLine(grid, 5);

            // Priority
            AddCell(grid, task.Priority.ToString().ToUpper(), 6);
            AddVerticalLine(grid, 7);

            // Due Date
            var dueText = task.DueDate.HasValue ? task.DueDate.Value.ToString("yyyy-MM-dd") : "-";
            AddCell(grid, dueText, 8);

            return grid;
        }

        private void AddCell(Grid grid, string text, int column)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(5, 3, 5, 3),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(tb, column);
            grid.Children.Add(tb);
        }

        private Border CreateDetailPanel()
        {
            var border = new Border { Padding = new Thickness(10) };
            var stack = new StackPanel();

            var header = CreateText("DETAILS", 11, FontWeights.Bold);
            stack.Children.Add(header);

            var headerLine = CreateHorizontalLine();
            headerLine.Margin = new Thickness(0, 3, 0, 8);
            stack.Children.Add(headerLine);

            if (selectedTask != null)
            {
                stack.Children.Add(CreateText(selectedTask.Title, 11, FontWeights.Bold));

                if (!string.IsNullOrWhiteSpace(selectedTask.Description))
                {
                    var desc = new TextBlock
                    {
                        Text = selectedTask.Description,
                        FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(theme.Foreground),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 5, 0, 5)
                    };
                    stack.Children.Add(desc);
                }

                stack.Children.Add(CreateText($"Status: {selectedTask.Status} {selectedTask.StatusIcon}", 10));
                stack.Children.Add(CreateText($"Priority: {selectedTask.Priority} {selectedTask.PriorityIcon}", 10));

                if (selectedTask.DueDate.HasValue)
                    stack.Children.Add(CreateText($"Due: {selectedTask.DueDate.Value:yyyy-MM-dd}", 10));

                stack.Children.Add(CreateText($"Progress: {selectedTask.Progress}%", 10));

                if (selectedTask.Tags != null && selectedTask.Tags.Any())
                {
                    var tagsSep = CreateHorizontalLine();
                    tagsSep.Margin = new Thickness(0, 8, 0, 5);
                    stack.Children.Add(tagsSep);
                    stack.Children.Add(CreateText("TAGS:", 10, FontWeights.Bold));
                    stack.Children.Add(CreateText(string.Join(", ", selectedTask.Tags), 9));
                }

                if (selectedTask.Notes != null && selectedTask.Notes.Any())
                {
                    var notesSep = CreateHorizontalLine();
                    notesSep.Margin = new Thickness(0, 8, 0, 5);
                    stack.Children.Add(notesSep);
                    stack.Children.Add(CreateText($"NOTES ({selectedTask.Notes.Count}):", 10, FontWeights.Bold));

                    foreach (var note in selectedTask.Notes.OrderByDescending(n => n.CreatedAt).Take(5))
                    {
                        var noteText = new TextBlock
                        {
                            Text = $"[{note.CreatedAt:MM/dd}] {note.Content}",
                            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                            FontSize = 9,
                            Foreground = new SolidColorBrush(theme.Foreground),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                        stack.Children.Add(noteText);
                    }
                }

                var timeSep = CreateHorizontalLine();
                timeSep.Margin = new Thickness(0, 8, 0, 5);
                stack.Children.Add(timeSep);

                var timestamps = new TextBlock
                {
                    Text = $"Created: {selectedTask.CreatedAt:yyyy-MM-dd HH:mm}\nUpdated: {selectedTask.UpdatedAt:yyyy-MM-dd HH:mm}",
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                    Margin = new Thickness(0, 2, 0, 2)
                };
                stack.Children.Add(timestamps);
            }
            else
            {
                stack.Children.Add(CreateText("No task selected", 10));
            }

            border.Child = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.Transparent
            };
            return border;
        }

        private StackPanel CreateStats()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };

            panel.Children.Add(CreateText($"SHOWING: {currentTasks.Count} TASKS", 10));
            panel.Children.Add(CreateText("  │  ", 10));
            panel.Children.Add(CreateText($"TIME: {timeFilter}", 10));
            panel.Children.Add(CreateText("  │  ", 10));
            panel.Children.Add(CreateText($"STATUS: {statusFilter}", 10));
            panel.Children.Add(CreateText("  │  ", 10));
            panel.Children.Add(CreateText($"PRIORITY: {priorityFilter}", 10));

            return panel;
        }

        private StackPanel CreateShortcuts()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };

            var line1 = new StackPanel { Orientation = Orientation.Horizontal };
            line1.Children.Add(CreateText("SHORTCUTS: [TAB] CYCLE FILTERS │ [1-4] OPEN FILTER │ [↑↓] IN DROPDOWN OR TASKS │ [ENTER] SELECT/EDIT", 9));
            panel.Children.Add(line1);

            var line2 = new StackPanel { Orientation = Orientation.Horizontal };
            line2.Children.Add(CreateText("           [N] NEW TASK │ [D] DELETE │ [P] CYCLE PRIORITY │ [S] CYCLE STATUS │ [M] ADD NOTE", 9));
            panel.Children.Add(line2);

            return panel;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            logger?.Debug("TaskWidget", $"Key pressed: {e.Key}");
            bool handled = true;

            // Check if any dropdown is open
            var openDropdown = filterDropdowns.FirstOrDefault(d => d.IsOpen);

            if (openDropdown != null)
            {
                // Dropdown is open - let it handle keys
                handled = openDropdown.HandleKey(e.Key);
                if (handled)
                {
                    e.Handled = true;
                    return;
                }
            }

            // Handle Tab/Shift+Tab for filter focus cycling
            if (e.Key == Key.Tab)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    // Shift+Tab: Previous filter
                    currentFocusedFilterIndex--;
                    if (currentFocusedFilterIndex < 0)
                        currentFocusedFilterIndex = filterDropdowns.Count - 1;
                }
                else
                {
                    // Tab: Next filter
                    currentFocusedFilterIndex++;
                    if (currentFocusedFilterIndex >= filterDropdowns.Count)
                        currentFocusedFilterIndex = 0;
                }

                for (int i = 0; i < filterDropdowns.Count; i++)
                    filterDropdowns[i].SetFocused(i == currentFocusedFilterIndex);

                e.Handled = true;
                return;
            }

            // Handle number keys 1-4 to open filter dropdowns
            if (e.Key >= Key.D1 && e.Key <= Key.D4)
            {
                int index = e.Key - Key.D1;
                if (index < filterDropdowns.Count)
                {
                    currentFocusedFilterIndex = index;
                    for (int i = 0; i < filterDropdowns.Count; i++)
                        filterDropdowns[i].SetFocused(i == currentFocusedFilterIndex);
                    filterDropdowns[index].Open();
                    e.Handled = true;
                    return;
                }
            }

            // Arrow keys: Navigate tasks (when no dropdown open and not focused on filter)
            if (currentFocusedFilterIndex < 0 || currentFocusedFilterIndex >= filterDropdowns.Count)
            {
                switch (e.Key)
                {
                    case Key.Up:
                        if (currentTasks.Count > 0)
                        {
                            if (selectedTaskIndex > 0)
                                selectedTaskIndex--;
                            else
                                selectedTaskIndex = currentTasks.Count - 1;
                            selectedTask = currentTasks[selectedTaskIndex];
                            BuildUI();
                        }
                        break;

                    case Key.Down:
                        if (currentTasks.Count > 0)
                        {
                            if (selectedTaskIndex < currentTasks.Count - 1)
                                selectedTaskIndex++;
                            else
                                selectedTaskIndex = 0;
                            selectedTask = currentTasks[selectedTaskIndex];
                            BuildUI();
                        }
                        break;

                    case Key.Enter:
                        if (selectedTask != null)
                            ShowEditDialog();
                        break;

                    case Key.N:
                        ShowNewTaskDialog();
                        break;

                    case Key.D:
                        if (selectedTask != null && MessageBox.Show($"Delete '{selectedTask.Title}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            taskService.DeleteTask(selectedTask.Id);
                            LoadCurrentFilter();
                            BuildUI();
                        }
                        break;

                    case Key.P:
                        if (selectedTask != null)
                        {
                            CyclePriority();
                            BuildUI();
                        }
                        break;

                    case Key.S:
                        if (selectedTask != null)
                        {
                            CycleStatus();
                            BuildUI();
                        }
                        break;

                    case Key.M:
                        if (selectedTask != null)
                            ShowAddNoteDialog();
                        break;

                    default:
                        handled = false;
                        break;
                }
            }

            if (handled)
                e.Handled = true;
        }

        private void CyclePriority()
        {
            if (selectedTask == null) return;
            selectedTask.Priority = selectedTask.Priority switch
            {
                TaskPriority.Low => TaskPriority.Medium,
                TaskPriority.Medium => TaskPriority.High,
                TaskPriority.High => TaskPriority.Low,
                _ => TaskPriority.Medium
            };
            selectedTask.UpdatedAt = DateTime.Now;
            taskService.UpdateTask(selectedTask);
            LoadCurrentFilter();
        }

        private void CycleStatus()
        {
            if (selectedTask == null) return;
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
        }

        private void ShowEditDialog()
        {
            if (selectedTask == null) return;

            var dialog = new Window
            {
                Title = "Edit Task",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(theme.Background)
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(CreateText("EDIT TASK", 14, FontWeights.Bold));

            var sep = CreateHorizontalLine();
            sep.Margin = new Thickness(0, 5, 0, 10);
            stack.Children.Add(sep);

            stack.Children.Add(CreateText("Title:", 11));
            var titleBox = new TextBox
            {
                Text = selectedTask.Title,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Background),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                Margin = new Thickness(0, 3, 0, 10)
            };
            stack.Children.Add(titleBox);

            stack.Children.Add(CreateText("Description:", 11));
            var descBox = new TextBox
            {
                Text = selectedTask.Description ?? "",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Background),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MinHeight = 100,
                Margin = new Thickness(0, 3, 0, 10)
            };
            stack.Children.Add(descBox);

            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var saveBtn = new Button
            {
                Content = "SAVE",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(5, 0, 5, 0),
                Cursor = Cursors.Hand
            };
            saveBtn.Click += (s, ev) =>
            {
                selectedTask.Title = titleBox.Text?.Trim() ?? "Untitled";
                selectedTask.Description = descBox.Text?.Trim() ?? "";
                selectedTask.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(selectedTask);
                LoadCurrentFilter();
                dialog.Close();
                BuildUI();
            };
            buttonStack.Children.Add(saveBtn);

            var cancelBtn = new Button
            {
                Content = "CANCEL",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15, 5, 15, 5),
                Cursor = Cursors.Hand
            };
            cancelBtn.Click += (s, ev) => dialog.Close();
            buttonStack.Children.Add(cancelBtn);

            stack.Children.Add(buttonStack);
            dialog.Content = stack;
            titleBox.Focus();
            dialog.ShowDialog();
        }

        private void ShowNewTaskDialog()
        {
            var dialog = new Window
            {
                Title = "New Task",
                Width = 500,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(theme.Background)
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(CreateText("NEW TASK", 14, FontWeights.Bold));

            var sep = CreateHorizontalLine();
            sep.Margin = new Thickness(0, 5, 0, 10);
            stack.Children.Add(sep);

            stack.Children.Add(CreateText("Title:", 11));
            var titleBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Background),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                Margin = new Thickness(0, 3, 0, 10)
            };
            stack.Children.Add(titleBox);

            stack.Children.Add(CreateText("Description (optional):", 11));
            var descBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Background),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MinHeight = 100,
                Margin = new Thickness(0, 3, 0, 10)
            };
            stack.Children.Add(descBox);

            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var createBtn = new Button
            {
                Content = "CREATE",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(5, 0, 5, 0),
                Cursor = Cursors.Hand
            };
            createBtn.Click += (s, ev) =>
            {
                var title = titleBox.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(title))
                {
                    MessageBox.Show("Title is required", "Error");
                    return;
                }

                var newTask = new TaskItem
                {
                    Title = title,
                    Description = descBox.Text?.Trim() ?? "",
                    Status = TaskStatus.Pending,
                    Priority = TaskPriority.Medium,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                taskService.AddTask(newTask);
                LoadCurrentFilter();
                dialog.Close();
                BuildUI();
            };
            buttonStack.Children.Add(createBtn);

            var cancelBtn = new Button
            {
                Content = "CANCEL",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15, 5, 15, 5),
                Cursor = Cursors.Hand
            };
            cancelBtn.Click += (s, ev) => dialog.Close();
            buttonStack.Children.Add(cancelBtn);

            stack.Children.Add(buttonStack);
            dialog.Content = stack;
            titleBox.Focus();
            dialog.ShowDialog();
        }

        private void ShowAddNoteDialog()
        {
            if (selectedTask == null) return;

            var dialog = new Window
            {
                Title = "Add Note",
                Width = 400,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(theme.Background)
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(CreateText($"ADD NOTE TO: {selectedTask.Title}", 12, FontWeights.Bold));

            var sep = CreateHorizontalLine();
            sep.Margin = new Thickness(0, 5, 0, 10);
            stack.Children.Add(sep);

            var noteBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Background),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MinHeight = 80,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(noteBox);

            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var addBtn = new Button
            {
                Content = "ADD",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(5, 0, 5, 0),
                Cursor = Cursors.Hand
            };
            addBtn.Click += (s, ev) =>
            {
                var content = noteBox.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(content))
                {
                    MessageBox.Show("Note cannot be empty", "Error");
                    return;
                }

                taskService.AddNote(selectedTask.Id, content);
                selectedTask = taskService.GetTask(selectedTask.Id);
                LoadCurrentFilter();
                dialog.Close();
                BuildUI();
            };
            buttonStack.Children.Add(addBtn);

            var cancelBtn = new Button
            {
                Content = "CANCEL",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15, 5, 15, 5),
                Cursor = Cursors.Hand
            };
            cancelBtn.Click += (s, ev) => dialog.Close();
            buttonStack.Children.Add(cancelBtn);

            stack.Children.Add(buttonStack);
            dialog.Content = stack;
            noteBox.Focus();
            dialog.ShowDialog();
        }

        public override Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["SelectedTaskId"] = selectedTask?.Id.ToString(),
                ["TimeFilter"] = timeFilter,
                ["StatusFilter"] = statusFilter,
                ["PriorityFilter"] = priorityFilter,
                ["TagFilter"] = tagFilter,
                ["SelectedTaskIndex"] = selectedTaskIndex
            };
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state == null) return;

            if (state.ContainsKey("TimeFilter") && state["TimeFilter"] is string tf)
                timeFilter = tf;
            if (state.ContainsKey("StatusFilter") && state["StatusFilter"] is string sf)
                statusFilter = sf;
            if (state.ContainsKey("PriorityFilter") && state["PriorityFilter"] is string pf)
                priorityFilter = pf;
            if (state.ContainsKey("TagFilter") && state["TagFilter"] is string tgf)
                tagFilter = tgf;

            LoadCurrentFilter();

            if (state.ContainsKey("SelectedTaskId") && state["SelectedTaskId"] is string idStr &&
                Guid.TryParse(idStr, out var id))
            {
                var task = currentTasks.FirstOrDefault(t => t.Id == id);
                if (task != null)
                {
                    selectedTaskIndex = currentTasks.IndexOf(task);
                    selectedTask = task;
                }
            }

            BuildUI();
        }

        public override void OnWidgetFocusReceived()
        {
            // Force focus when workspace switches to this widget
            this.Focus();
            Keyboard.Focus(this);
            logger?.Debug("TaskWidget", "Focus received, widget now focused");
        }

        protected override void OnDispose()
        {
            this.PreviewKeyDown -= OnPreviewKeyDown;
            logger?.Info("TaskWidget", "Task Management widget disposed");
            base.OnDispose();
        }

        public void ApplyTheme()
        {
            theme = themeManager.CurrentTheme;
            BuildUI();
            logger?.Debug("TaskWidget", "Applied theme update");
        }
    }

    // Helper class for filter dropdowns
    internal class FilterDropdown
    {
        private string name;
        private int number;
        private string[] options;
        private int selectedIndex = 0;
        private bool isOpen = false;
        private bool isFocused = false;
        private Theme theme;
        private Border control;
        private StackPanel dropdownPanel;

        public event Action<string> SelectionChanged;
        public bool IsOpen => isOpen;

        public FilterDropdown(string name, int number, string[] options, Theme theme)
        {
            this.name = name;
            this.number = number;
            this.options = options;
            this.theme = theme;
            BuildControl();
        }

        private void BuildControl()
        {
            control = new Border
            {
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Background = isFocused ? new SolidColorBrush(Color.FromArgb(40, theme.Primary.R, theme.Primary.G, theme.Primary.B)) : Brushes.Transparent,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(5, 0, 5, 0)
            };

            dropdownPanel = new StackPanel();

            var headerText = new TextBlock
            {
                Text = $"[{number}] {name}: {options[selectedIndex]} {(isOpen ? "▲" : "▼")}",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground)
            };
            dropdownPanel.Children.Add(headerText);

            if (isOpen)
            {
                for (int i = 0; i < options.Length; i++)
                {
                    var optionText = new TextBlock
                    {
                        Text = (i == selectedIndex ? "  ► " : "    ") + options[i],
                        FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(theme.Foreground),
                        Background = i == selectedIndex ? new SolidColorBrush(Color.FromArgb(60, theme.Primary.R, theme.Primary.G, theme.Primary.B)) : Brushes.Transparent
                    };
                    dropdownPanel.Children.Add(optionText);
                }
            }

            control.Child = dropdownPanel;
        }

        public UIElement GetControl() => control;

        public void SetFocused(bool focused)
        {
            isFocused = focused;
            BuildControl();
        }

        public void Open()
        {
            isOpen = true;
            BuildControl();
        }

        public void Close()
        {
            isOpen = false;
            BuildControl();
        }

        public bool HandleKey(Key key)
        {
            if (!isOpen) return false;

            switch (key)
            {
                case Key.Up:
                    if (selectedIndex > 0)
                    {
                        selectedIndex--;
                        BuildControl();
                    }
                    return true;

                case Key.Down:
                    if (selectedIndex < options.Length - 1)
                    {
                        selectedIndex++;
                        BuildControl();
                    }
                    return true;

                case Key.Enter:
                    SelectionChanged?.Invoke(options[selectedIndex]);
                    Close();
                    return true;

                case Key.Escape:
                    Close();
                    return true;

                default:
                    return false;
            }
        }
    }
}
