using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SuperTUI.Core;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Task widget using WPF drawing primitives for pixel-perfect borders
    /// No Unicode characters - actual drawn lines that look like terminal
    /// </summary>
    public class DrawnTasksWidget : WidgetBase
    {
        private Grid mainGrid;
        private int selectedTask = 0;
        private string selectedFilter = "ALL";
        private Color terminalGreen = Color.FromRgb(0, 255, 0);
        private Color terminalBg = Color.FromRgb(0, 0, 0);

        private List<FakeTask> tasks = new List<FakeTask>
        {
            new FakeTask { Id = 1, Name = "Fix authentication bug", Status = "ACTIVE", Priority = "HIGH", Due = "TODAY" },
            new FakeTask { Id = 2, Name = "Implement search feature", Status = "ACTIVE", Priority = "MED", Due = "2 DAYS" },
            new FakeTask { Id = 3, Name = "Update documentation", Status = "PENDING", Priority = "LOW", Due = "1 WEEK" },
            new FakeTask { Id = 4, Name = "Code review PR #245", Status = "ACTIVE", Priority = "HIGH", Due = "TODAY" },
            new FakeTask { Id = 5, Name = "Refactor payment module", Status = "PENDING", Priority = "MED", Due = "3 DAYS" },
            new FakeTask { Id = 6, Name = "Add unit tests", Status = "BLOCKED", Priority = "LOW", Due = "1 WEEK" },
            new FakeTask { Id = 7, Name = "Deploy to staging", Status = "ACTIVE", Priority = "HIGH", Due = "TODAY" },
            new FakeTask { Id = 8, Name = "Client meeting prep", Status = "PENDING", Priority = "MED", Due = "5 DAYS" },
        };

        private class FakeTask
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Status { get; set; }
            public string Priority { get; set; }
            public string Due { get; set; }
        }

        public DrawnTasksWidget()
        {
            WidgetName = "Drawn Tasks";
            WidgetType = "DrawnTasks";
        }

        public override void Initialize()
        {
            BuildUI();
            this.Focusable = true;
            this.Focus();
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
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filter
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Table header
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tasks
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Stats
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) }); // Line
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Shortcuts

            // Header
            var header = CreateText("TASK MANAGEMENT SYSTEM v2.0", 16, FontWeights.Bold);
            header.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetRow(header, 0);
            contentGrid.Children.Add(header);

            // Separator line
            Grid.SetRow(CreateHorizontalLine(), 1);
            contentGrid.Children.Add(CreateHorizontalLine());

            // Filter section
            var filterPanel = CreateFilterBar();
            Grid.SetRow(filterPanel, 2);
            contentGrid.Children.Add(filterPanel);

            // Separator
            var line2 = CreateHorizontalLine();
            Grid.SetRow(line2, 3);
            contentGrid.Children.Add(line2);

            // Table header
            var tableHeader = CreateTableHeader();
            Grid.SetRow(tableHeader, 4);
            contentGrid.Children.Add(tableHeader);

            // Separator
            var line3 = CreateHorizontalLine();
            Grid.SetRow(line3, 5);
            contentGrid.Children.Add(line3);

            // Task rows
            var taskPanel = CreateTaskRows();
            Grid.SetRow(taskPanel, 6);
            contentGrid.Children.Add(taskPanel);

            // Separator
            var line4 = CreateHorizontalLine();
            Grid.SetRow(line4, 7);
            contentGrid.Children.Add(line4);

            // Stats
            var stats = CreateStats();
            Grid.SetRow(stats, 8);
            contentGrid.Children.Add(stats);

            // Separator
            var line5 = CreateHorizontalLine();
            Grid.SetRow(line5, 9);
            contentGrid.Children.Add(line5);

            // Shortcuts
            var shortcuts = CreateShortcuts();
            Grid.SetRow(shortcuts, 10);
            contentGrid.Children.Add(shortcuts);

            outerBorder.Child = contentGrid;
            mainGrid.Children.Add(outerBorder);
            this.Content = mainGrid;

            this.KeyDown += OnKeyDown;
        }

        private Rectangle CreateHorizontalLine()
        {
            return new Rectangle
            {
                Fill = new SolidColorBrush(terminalGreen),
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch
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

            foreach (var filter in new[] { "ALL", "ACTIVE", "PENDING", "BLOCKED" })
            {
                var isSelected = selectedFilter == filter;
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
                    Text = filter,
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

        private Grid CreateTableHeader()
        {
            var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });  // ID
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });   // Separator
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); // Name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });   // Separator
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Status
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });   // Separator
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Priority
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });   // Separator
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Due

            AddHeaderCell(grid, "ID", 0);
            Grid.SetColumn(CreateVerticalLine(), 1);
            grid.Children.Add(CreateVerticalLine());

            AddHeaderCell(grid, "TASK NAME", 2);
            var line2 = CreateVerticalLine();
            Grid.SetColumn(line2, 3);
            grid.Children.Add(line2);

            AddHeaderCell(grid, "STATUS", 4);
            var line3 = CreateVerticalLine();
            Grid.SetColumn(line3, 5);
            grid.Children.Add(line3);

            AddHeaderCell(grid, "PRIORITY", 6);
            var line4 = CreateVerticalLine();
            Grid.SetColumn(line4, 7);
            grid.Children.Add(line4);

            AddHeaderCell(grid, "DUE", 8);

            return grid;
        }

        private void AddHeaderCell(Grid grid, string text, int column)
        {
            var tb = CreateText(text, 11, FontWeights.Bold);
            tb.Margin = new Thickness(5, 0, 5, 0);
            Grid.SetColumn(tb, column);
            grid.Children.Add(tb);
        }

        private Rectangle CreateVerticalLine()
        {
            return new Rectangle
            {
                Fill = new SolidColorBrush(terminalGreen),
                Width = 1,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }

        private ScrollViewer CreateTaskRows()
        {
            var stack = new StackPanel();
            var filteredTasks = GetFilteredTasks();

            for (int i = 0; i < filteredTasks.Count; i++)
            {
                var task = filteredTasks[i];
                var isSelected = (i == selectedTask);

                var taskRow = CreateTaskRow(task, isSelected);
                stack.Children.Add(taskRow);
            }

            return new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(terminalBg)
            };
        }

        private Grid CreateTaskRow(FakeTask task, bool isSelected)
        {
            var grid = new Grid
            {
                Background = isSelected ? new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)) : Brushes.Transparent,
                Margin = new Thickness(0, 1, 0, 1)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            // ID with selection marker
            var idText = (isSelected ? "► " : "  ") + task.Id;
            AddCell(grid, idText, 0);

            Grid.SetColumn(CreateVerticalLine(), 1);
            grid.Children.Add(CreateVerticalLine());

            AddCell(grid, task.Name, 2);
            var line2 = CreateVerticalLine();
            Grid.SetColumn(line2, 3);
            grid.Children.Add(line2);

            AddCell(grid, task.Status, 4);
            var line3 = CreateVerticalLine();
            Grid.SetColumn(line3, 5);
            grid.Children.Add(line3);

            AddCell(grid, task.Priority, 6);
            var line4 = CreateVerticalLine();
            Grid.SetColumn(line4, 7);
            grid.Children.Add(line4);

            AddCell(grid, task.Due, 8);

            return grid;
        }

        private void AddCell(Grid grid, string text, int column)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(terminalGreen),
                Margin = new Thickness(5, 2, 5, 2)
            };
            Grid.SetColumn(tb, column);
            grid.Children.Add(tb);
        }

        private StackPanel CreateStats()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            var filteredCount = GetFilteredTasks().Count;

            panel.Children.Add(CreateText($"STATS: {filteredCount} TASKS SHOWN", 11));
            panel.Children.Add(CreateText("  │  ", 11));
            panel.Children.Add(CreateText($"TOTAL: {tasks.Count} TASKS", 11));

            return panel;
        }

        private StackPanel CreateShortcuts()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };

            var line1 = new StackPanel { Orientation = Orientation.Horizontal };
            line1.Children.Add(CreateText("SHORTCUTS: [↑↓] NAVIGATE │ [1-4] FILTER │ [ENTER] VIEW │ [D] DELETE", 10));
            panel.Children.Add(line1);

            var line2 = new StackPanel { Orientation = Orientation.Horizontal };
            line2.Children.Add(CreateText("           [N] NEW TASK │ [E] EDIT │ [S] SORT │ [Q] QUIT", 10));
            panel.Children.Add(line2);

            return panel;
        }

        private List<FakeTask> GetFilteredTasks()
        {
            if (selectedFilter == "ALL")
                return tasks;

            var filtered = new List<FakeTask>();
            foreach (var task in tasks)
            {
                if (task.Status == selectedFilter)
                    filtered.Add(task);
            }
            return filtered;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            bool handled = true;
            var filteredTasks = GetFilteredTasks();

            switch (e.Key)
            {
                case Key.Up:
                    if (selectedTask > 0) selectedTask--;
                    else selectedTask = filteredTasks.Count - 1;
                    break;

                case Key.Down:
                    if (selectedTask < filteredTasks.Count - 1) selectedTask++;
                    else selectedTask = 0;
                    break;

                case Key.D1:
                case Key.NumPad1:
                    selectedFilter = "ALL";
                    selectedTask = 0;
                    break;

                case Key.D2:
                case Key.NumPad2:
                    selectedFilter = "ACTIVE";
                    selectedTask = 0;
                    break;

                case Key.D3:
                case Key.NumPad3:
                    selectedFilter = "PENDING";
                    selectedTask = 0;
                    break;

                case Key.D4:
                case Key.NumPad4:
                    selectedFilter = "BLOCKED";
                    selectedTask = 0;
                    break;

                case Key.Enter:
                    if (filteredTasks.Count > 0)
                    {
                        var task = filteredTasks[selectedTask];
                        MessageBox.Show($"TASK DETAILS\n\nID: {task.Id}\nName: {task.Name}\nStatus: {task.Status}\nPriority: {task.Priority}\nDue: {task.Due}",
                            "Task Details");
                    }
                    break;

                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                BuildUI(); // Rebuild to update selection
                e.Handled = true;
            }
        }

        public override Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["SelectedTask"] = selectedTask,
                ["SelectedFilter"] = selectedFilter
            };
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state.TryGetValue("SelectedTask", out var idx))
                selectedTask = (int)idx;
            if (state.TryGetValue("SelectedFilter", out var filter))
                selectedFilter = (string)filter;

            BuildUI();
        }

        protected override void OnDispose()
        {
            this.KeyDown -= OnKeyDown;
        }
    }
}
