using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Mock tasks screen demonstrating proper terminal aesthetic
    /// - Single-line Unicode box characters (PROPERLY CONNECTED!)
    /// - Keyboard-driven navigation
    /// - Fake data for demonstration
    /// </summary>
    public class MockTasksWidget : WidgetBase
    {
        private TextBlock displayText;
        private int selectedTask = 0;
        private string selectedFilter = "ALL";

        // Fake task data
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

        public MockTasksWidget()
        {
            WidgetName = "Mock Tasks";
            WidgetType = "MockTasks";
        }

        public override void Initialize()
        {
            BuildUI();
            this.Focusable = true;
            this.Focus();
        }

        private void BuildUI()
        {
            var mainGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 0, 0))
            };

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0),
                Padding = new Thickness(10)
            };

            displayText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                LineHeight = 16,
                Text = ""
            };

            border.Child = displayText;
            mainGrid.Children.Add(border);
            this.Content = mainGrid;

            UpdateDisplay();

            this.KeyDown += OnKeyDown;
        }

        private void UpdateDisplay()
        {
            if (displayText == null) return;

            var output = "";

            // Top border
            output += "┌─────────────────────────────────────────────────────────────────────────────┐\n";
            output += "│                        TASK MANAGEMENT SYSTEM v2.0                          │\n";
            output += "├─────────────────────────────────────────────────────────────────────────────┤\n";

            // Filter bar
            output += "│ FILTER: [";
            output += selectedFilter == "ALL" ? "█ALL█" : " ALL ";
            output += "] [";
            output += selectedFilter == "ACTIVE" ? "█ACTIVE█" : " ACTIVE ";
            output += "] [";
            output += selectedFilter == "PENDING" ? "█PENDING█" : " PENDING ";
            output += "] [";
            output += selectedFilter == "BLOCKED" ? "█BLOCKED█" : " BLOCKED ";
            output += "]         │\n";
            output += "├────┬──────────────────────────────────┬──────────┬──────────┬─────────────┤\n";

            // Header
            output += "│ ID │ TASK NAME                        │ STATUS   │ PRIORITY │ DUE         │\n";
            output += "├────┼──────────────────────────────────┼──────────┼──────────┼─────────────┤\n";

            // Tasks
            var filteredTasks = GetFilteredTasks();
            for (int i = 0; i < filteredTasks.Count; i++)
            {
                var task = filteredTasks[i];
                var isSelected = (i == selectedTask);
                var marker = isSelected ? "►" : " ";

                output += "│";
                output += marker;
                output += task.Id.ToString().PadLeft(2);
                output += " │ ";
                output += task.Name.PadRight(32).Substring(0, 32);
                output += " │ ";
                output += task.Status.PadRight(8);
                output += " │ ";
                output += task.Priority.PadRight(8);
                output += " │ ";
                output += task.Due.PadRight(11);
                output += " │\n";
            }

            // Pad empty rows
            for (int i = filteredTasks.Count; i < 8; i++)
            {
                output += "│    │                                  │          │          │             │\n";
            }

            // Bottom section
            output += "├────┴──────────────────────────────────┴──────────┴──────────┴─────────────┤\n";
            output += "│ STATS: " + filteredTasks.Count.ToString().PadLeft(2) + " TASKS SHOWN  │  TOTAL: " + tasks.Count.ToString().PadLeft(2) + " TASKS                           │\n";
            output += "├─────────────────────────────────────────────────────────────────────────────┤\n";
            output += "│ SHORTCUTS: [↑↓] NAVIGATE │ [1-4] FILTER │ [ENTER] VIEW │ [D] DELETE      │\n";
            output += "│            [N] NEW TASK  │ [E] EDIT     │ [S] SORT     │ [Q] QUIT        │\n";
            output += "└─────────────────────────────────────────────────────────────────────────────┘";

            displayText.Text = output;
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
                    if (selectedTask > 0)
                        selectedTask--;
                    else
                        selectedTask = filteredTasks.Count - 1;
                    break;

                case Key.Down:
                    if (selectedTask < filteredTasks.Count - 1)
                        selectedTask++;
                    else
                        selectedTask = 0;
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
                        ShowTaskDetails(task);
                    }
                    break;

                case Key.N:
                    MessageBox.Show("NEW TASK\n\nWould open task creation dialog.", "Mock Tasks");
                    break;

                case Key.E:
                    if (filteredTasks.Count > 0)
                    {
                        var task = filteredTasks[selectedTask];
                        MessageBox.Show($"EDIT TASK #{task.Id}\n\n{task.Name}\n\nWould open edit dialog.", "Mock Tasks");
                    }
                    break;

                case Key.D:
                    if (filteredTasks.Count > 0)
                    {
                        var task = filteredTasks[selectedTask];
                        MessageBox.Show($"DELETE TASK #{task.Id}\n\n{task.Name}\n\nWould show confirmation dialog.", "Mock Tasks");
                    }
                    break;

                case Key.S:
                    MessageBox.Show("SORT OPTIONS\n\nWould show sort menu:\n- By Priority\n- By Due Date\n- By Status\n- By Name", "Mock Tasks");
                    break;

                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                UpdateDisplay();
                e.Handled = true;
            }
        }

        private void ShowTaskDetails(FakeTask task)
        {
            var details = "┌────────────────────────────┐\n";
            details += "│      TASK DETAILS          │\n";
            details += "├────────────────────────────┤\n";
            details += $"│ ID:       {task.Id,-17}│\n";
            details += $"│ NAME:     {task.Name.Substring(0, Math.Min(17, task.Name.Length)),-17}│\n";
            details += $"│ STATUS:   {task.Status,-17}│\n";
            details += $"│ PRIORITY: {task.Priority,-17}│\n";
            details += $"│ DUE:      {task.Due,-17}│\n";
            details += "└────────────────────────────┘\n\n";
            details += "Press OK to close.";

            MessageBox.Show(details, "Task Details", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public override Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["SelectedTask"] = selectedTask,
                ["SelectedFilter"] = selectedFilter
            };
        }

        public override void LoadState(Dictionary<string, object> state)
        {
            if (state.TryGetValue("SelectedTask", out var idx))
                selectedTask = (int)idx;
            if (state.TryGetValue("SelectedFilter", out var filter))
                selectedFilter = (string)filter;

            UpdateDisplay();
        }

        protected override void OnDispose()
        {
            this.KeyDown -= OnKeyDown;
        }
    }
}
