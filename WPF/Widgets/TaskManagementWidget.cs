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
    /// Arrow keys for navigation, number keys for filters, Enter for actions
    /// </summary>
    public class TaskManagementWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;

        private Theme theme;
        private TaskService taskService;

        // Terminal colors
        private Color terminalGreen = Color.FromRgb(0, 255, 0);
        private Color terminalBg = Color.FromRgb(0, 0, 0);

        // UI Components
        private Grid mainGrid;

        // State
        private List<TaskFilter> filters;
        private TaskFilter currentFilter;
        private List<TaskItem> currentTasks = new List<TaskItem>();
        private int selectedTaskIndex = 0;
        private TaskItem selectedTask;

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

            filters = TaskFilter.GetDefaultFilters();
            currentFilter = TaskFilter.All;

            LoadCurrentFilter();
            BuildUI();

            this.Focusable = true;
            this.KeyDown += OnKeyDown;
            this.Focus();

            logger?.Info("TaskWidget", "Keyboard-centric Task Management widget initialized");
        }

        private void LoadCurrentFilter()
        {
            currentTasks = taskService.GetTasks(currentFilter.Predicate).ToList();
            if (selectedTaskIndex >= currentTasks.Count)
                selectedTaskIndex = Math.Max(0, currentTasks.Count - 1);

            selectedTask = currentTasks.Count > 0 ? currentTasks[selectedTaskIndex] : null;
        }

        private void BuildUI()
        {
            mainGrid = new Grid
            {
                Background = new SolidColorBrush(terminalBg)
            };

            // Outer border
            var outerBorder = new Border
            {
                BorderBrush = new SolidColorBrush(terminalGreen),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(10),
                Padding = new Thickness(15)
            };

            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filter bar
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main content (3 panes)
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Stats
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Shortcuts

            // Header
            var header = CreateText("TASK MANAGEMENT SYSTEM v2.0", 16, FontWeights.Bold);
            header.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetRow(header, 0);
            contentGrid.Children.Add(header);

            // Separator
            Grid.SetRow(CreateHorizontalLine(), 1);
            contentGrid.Children.Add(CreateHorizontalLine());

            // Filter bar
            var filterBar = CreateFilterBar();
            Grid.SetRow(filterBar, 2);
            contentGrid.Children.Add(filterBar);

            // Separator
            var line2 = CreateHorizontalLine();
            Grid.SetRow(line2, 3);
            contentGrid.Children.Add(line2);

            // Main 3-pane content
            var mainContent = CreateMainContent();
            Grid.SetRow(mainContent, 4);
            contentGrid.Children.Add(mainContent);

            // Separator
            var line3 = CreateHorizontalLine();
            Grid.SetRow(line3, 5);
            contentGrid.Children.Add(line3);

            // Stats
            var stats = CreateStats();
            Grid.SetRow(stats, 6);
            contentGrid.Children.Add(stats);

            // Separator
            var line4 = CreateHorizontalLine();
            Grid.SetRow(line4, 7);
            contentGrid.Children.Add(line4);

            // Shortcuts
            var shortcuts = CreateShortcuts();
            Grid.SetRow(shortcuts, 8);
            contentGrid.Children.Add(shortcuts);

            outerBorder.Child = contentGrid;
            mainGrid.Children.Add(outerBorder);
            this.Content = mainGrid;
        }

        private System.Windows.Shapes.Rectangle CreateHorizontalLine()
        {
            return new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(terminalGreen),
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private System.Windows.Shapes.Rectangle CreateVerticalLine()
        {
            return new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(terminalGreen),
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
                Foreground = new SolidColorBrush(terminalGreen),
                Margin = new Thickness(0, 3, 0, 3)
            };
        }

        private StackPanel CreateFilterBar()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };

            panel.Children.Add(CreateText("FILTER: ", 12, FontWeights.Bold));

            var filterNames = new[] { "ALL", "ACTIVE", "PENDING", "BLOCKED" };
            for (int i = 0; i < filterNames.Length; i++)
            {
                var filterName = filterNames[i];
                var isSelected = currentFilter.Name == filterName;

                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(terminalGreen),
                    BorderThickness = new Thickness(1),
                    Background = isSelected ? new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)) : Brushes.Transparent,
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(5, 0, 5, 0)
                };

                var text = new TextBlock
                {
                    Text = $"[{i + 1}] {filterName}",
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 11,
                    FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(terminalGreen)
                };

                border.Child = text;
                panel.Children.Add(border);
            }

            return panel;
        }

        private Grid CreateMainContent()
        {
            var grid = new Grid();

            // Define columns: Filters (200) | Sep | Tasks (flex) | Sep | Details (300)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            // Filter list
            var filterPanel = CreateFilterListPanel();
            Grid.SetColumn(filterPanel, 0);
            grid.Children.Add(filterPanel);

            // Separator
            var sep1 = CreateVerticalLine();
            Grid.SetColumn(sep1, 1);
            grid.Children.Add(sep1);

            // Task list
            var taskPanel = CreateTaskListPanel();
            Grid.SetColumn(taskPanel, 2);
            grid.Children.Add(taskPanel);

            // Separator
            var sep2 = CreateVerticalLine();
            Grid.SetColumn(sep2, 3);
            grid.Children.Add(sep2);

            // Details
            var detailPanel = CreateDetailPanel();
            Grid.SetColumn(detailPanel, 4);
            grid.Children.Add(detailPanel);

            return grid;
        }

        private Border CreateFilterListPanel()
        {
            var border = new Border { Padding = new Thickness(10) };
            var stack = new StackPanel();

            var header = CreateText("FILTERS", 11, FontWeights.Bold);
            stack.Children.Add(header);

            var headerLine = CreateHorizontalLine();
            headerLine.Margin = new Thickness(0, 3, 0, 8);
            stack.Children.Add(headerLine);

            foreach (var filter in filters)
            {
                var count = taskService.GetTaskCount(filter.Predicate);
                var isSelected = filter.Name == currentFilter.Name;

                var text = new TextBlock
                {
                    Text = $"{(isSelected ? "► " : "  ")}{filter.Name} ({count})",
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 10,
                    FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(terminalGreen),
                    Background = isSelected ? new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)) : Brushes.Transparent,
                    Padding = new Thickness(3, 2, 3, 2),
                    Margin = new Thickness(0, 1, 0, 1)
                };
                stack.Children.Add(text);
            }

            border.Child = stack;
            return border;
        }

        private ScrollViewer CreateTaskListPanel()
        {
            var stack = new StackPanel();

            var header = CreateText("TASKS", 11, FontWeights.Bold);
            header.Margin = new Thickness(10, 10, 10, 5);
            stack.Children.Add(header);

            var headerLine = CreateHorizontalLine();
            headerLine.Margin = new Thickness(10, 0, 10, 10);
            stack.Children.Add(headerLine);

            // Table header
            var tableHeader = CreateTaskTableHeader();
            tableHeader.Margin = new Thickness(10, 0, 10, 5);
            stack.Children.Add(tableHeader);

            var headerSep = CreateHorizontalLine();
            headerSep.Margin = new Thickness(10, 0, 10, 5);
            stack.Children.Add(headerSep);

            // Task rows
            for (int i = 0; i < currentTasks.Count; i++)
            {
                var task = currentTasks[i];
                var isSelected = (i == selectedTaskIndex);
                var taskRow = CreateTaskRow(task, isSelected);
                taskRow.Margin = new Thickness(10, 0, 10, 0);
                stack.Children.Add(taskRow);
            }

            if (currentTasks.Count == 0)
            {
                var noTasks = CreateText("No tasks", 11);
                noTasks.Foreground = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));
                noTasks.Margin = new Thickness(10);
                stack.Children.Add(noTasks);
            }

            return new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent
            };
        }

        private Grid CreateTaskTableHeader()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });  // ID
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Status
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // Priority

            AddHeaderCell(grid, "#", 0);
            Grid.SetColumn(CreateVerticalLine(), 1);
            grid.Children.Add(CreateVerticalLine());

            AddHeaderCell(grid, "TITLE", 2);
            var line2 = CreateVerticalLine();
            Grid.SetColumn(line2, 3);
            grid.Children.Add(line2);

            AddHeaderCell(grid, "STATUS", 4);
            var line3 = CreateVerticalLine();
            Grid.SetColumn(line3, 5);
            grid.Children.Add(line3);

            AddHeaderCell(grid, "PRIORITY", 6);

            return grid;
        }

        private void AddHeaderCell(Grid grid, string text, int column)
        {
            var tb = CreateText(text, 10, FontWeights.Bold);
            tb.Margin = new Thickness(5, 0, 5, 0);
            Grid.SetColumn(tb, column);
            grid.Children.Add(tb);
        }

        private Grid CreateTaskRow(TaskItem task, bool isSelected)
        {
            var grid = new Grid
            {
                Background = isSelected ? new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)) : Brushes.Transparent,
                Margin = new Thickness(0, 1, 0, 1)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            // ID with selection marker
            var idText = (isSelected ? "► " : "  ") + (currentTasks.IndexOf(task) + 1).ToString();
            AddCell(grid, idText, 0);

            Grid.SetColumn(CreateVerticalLine(), 1);
            grid.Children.Add(CreateVerticalLine());

            // Title
            var title = task.Title.Length > 40 ? task.Title.Substring(0, 37) + "..." : task.Title;
            AddCell(grid, title, 2);
            var line2 = CreateVerticalLine();
            Grid.SetColumn(line2, 3);
            grid.Children.Add(line2);

            // Status
            AddCell(grid, task.Status.ToString().ToUpper(), 4);
            var line3 = CreateVerticalLine();
            Grid.SetColumn(line3, 5);
            grid.Children.Add(line3);

            // Priority
            AddCell(grid, task.Priority.ToString().ToUpper(), 6);

            return grid;
        }

        private void AddCell(Grid grid, string text, int column)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 10,
                Foreground = new SolidColorBrush(terminalGreen),
                Margin = new Thickness(5, 2, 5, 2)
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
                        Foreground = new SolidColorBrush(terminalGreen),
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

                // Notes
                if (selectedTask.Notes != null && selectedTask.Notes.Any())
                {
                    var notesSep = CreateHorizontalLine();
                    notesSep.Margin = new Thickness(0, 8, 0, 5);
                    stack.Children.Add(notesSep);
                    stack.Children.Add(CreateText($"NOTES ({selectedTask.Notes.Count}):", 10, FontWeights.Bold));

                    foreach (var note in selectedTask.Notes.OrderByDescending(n => n.CreatedAt).Take(3))
                    {
                        var noteText = new TextBlock
                        {
                            Text = $"[{note.CreatedAt:MM/dd}] {note.Content}",
                            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                            FontSize = 9,
                            Foreground = new SolidColorBrush(terminalGreen),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                        stack.Children.Add(noteText);
                    }
                }

                // Timestamps
                var timeSep = CreateHorizontalLine();
                timeSep.Margin = new Thickness(0, 8, 0, 5);
                stack.Children.Add(timeSep);

                var timestamps = new TextBlock
                {
                    Text = $"Created: {selectedTask.CreatedAt:yyyy-MM-dd HH:mm}\nUpdated: {selectedTask.UpdatedAt:yyyy-MM-dd HH:mm}",
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0)),
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

            panel.Children.Add(CreateText($"SHOWING: {currentTasks.Count} TASKS", 11));
            panel.Children.Add(CreateText("  │  ", 11));
            panel.Children.Add(CreateText($"FILTER: {currentFilter.Name}", 11));
            panel.Children.Add(CreateText("  │  ", 11));
            panel.Children.Add(CreateText($"TOTAL: {taskService.GetTaskCount(_ => true)} TASKS", 11));

            return panel;
        }

        private StackPanel CreateShortcuts()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };

            var line1 = new StackPanel { Orientation = Orientation.Horizontal };
            line1.Children.Add(CreateText("SHORTCUTS: [↑↓] NAVIGATE │ [1-4] FILTER │ [ENTER] EDIT │ [N] NEW │ [D] DELETE", 10));
            panel.Children.Add(line1);

            var line2 = new StackPanel { Orientation = Orientation.Horizontal };
            line2.Children.Add(CreateText("           [P] SET PRIORITY │ [S] SET STATUS │ [M] ADD NOTE │ [TAB] NEXT WIDGET", 10));
            panel.Children.Add(line2);

            return panel;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            bool handled = true;

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
                    }
                    break;

                case Key.D1:
                case Key.NumPad1:
                    currentFilter = filters[0]; // ALL
                    LoadCurrentFilter();
                    break;

                case Key.D2:
                case Key.NumPad2:
                    currentFilter = filters[1]; // ACTIVE
                    LoadCurrentFilter();
                    break;

                case Key.D3:
                case Key.NumPad3:
                    currentFilter = filters[2]; // PENDING
                    LoadCurrentFilter();
                    break;

                case Key.D4:
                case Key.NumPad4:
                    currentFilter = filters[3]; // BLOCKED
                    LoadCurrentFilter();
                    break;

                case Key.Enter:
                    if (selectedTask != null)
                    {
                        ShowEditDialog();
                    }
                    break;

                case Key.N:
                    ShowNewTaskDialog();
                    break;

                case Key.D:
                    if (selectedTask != null && MessageBox.Show($"Delete task '{selectedTask.Title}'?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
                    }
                    break;

                case Key.S:
                    if (selectedTask != null)
                    {
                        CycleStatus();
                    }
                    break;

                case Key.M:
                    if (selectedTask != null)
                    {
                        ShowAddNoteDialog();
                    }
                    break;

                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                BuildUI(); // Rebuild UI to show changes
                e.Handled = true;
            }
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
                Background = new SolidColorBrush(terminalBg)
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
                Background = new SolidColorBrush(terminalBg),
                Foreground = new SolidColorBrush(terminalGreen),
                BorderBrush = new SolidColorBrush(terminalGreen),
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
                Background = new SolidColorBrush(terminalBg),
                Foreground = new SolidColorBrush(terminalGreen),
                BorderBrush = new SolidColorBrush(terminalGreen),
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
                Background = new SolidColorBrush(terminalGreen),
                Foreground = new SolidColorBrush(terminalBg),
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
                Foreground = new SolidColorBrush(terminalGreen),
                BorderBrush = new SolidColorBrush(terminalGreen),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15, 5, 15, 5),
                Cursor = Cursors.Hand
            };
            cancelBtn.Click += (s, ev) => dialog.Close();
            buttonStack.Children.Add(cancelBtn);

            stack.Children.Add(buttonStack);
            dialog.Content = stack;
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
                Background = new SolidColorBrush(terminalBg)
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
                Background = new SolidColorBrush(terminalBg),
                Foreground = new SolidColorBrush(terminalGreen),
                BorderBrush = new SolidColorBrush(terminalGreen),
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
                Background = new SolidColorBrush(terminalBg),
                Foreground = new SolidColorBrush(terminalGreen),
                BorderBrush = new SolidColorBrush(terminalGreen),
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
                Background = new SolidColorBrush(terminalGreen),
                Foreground = new SolidColorBrush(terminalBg),
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
                Foreground = new SolidColorBrush(terminalGreen),
                BorderBrush = new SolidColorBrush(terminalGreen),
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
                Background = new SolidColorBrush(terminalBg)
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
                Background = new SolidColorBrush(terminalBg),
                Foreground = new SolidColorBrush(terminalGreen),
                BorderBrush = new SolidColorBrush(terminalGreen),
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
                Background = new SolidColorBrush(terminalGreen),
                Foreground = new SolidColorBrush(terminalBg),
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
                Foreground = new SolidColorBrush(terminalGreen),
                BorderBrush = new SolidColorBrush(terminalGreen),
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
                ["CurrentFilter"] = currentFilter?.Name,
                ["SelectedTaskIndex"] = selectedTaskIndex
            };
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state == null) return;

            if (state.ContainsKey("CurrentFilter") && state["CurrentFilter"] is string filterName)
            {
                currentFilter = filters?.FirstOrDefault(f => f.Name == filterName) ?? TaskFilter.All;
            }

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

        protected override void OnDispose()
        {
            this.KeyDown -= OnKeyDown;
            logger?.Info("TaskWidget", "Keyboard-centric Task Management widget disposed");
            base.OnDispose();
        }

        public void ApplyTheme()
        {
            theme = themeManager.CurrentTheme;
            BuildUI();
            logger?.Debug("TaskWidget", "Applied theme update");
        }
    }
}
