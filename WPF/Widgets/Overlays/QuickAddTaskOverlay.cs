using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets.Overlays
{
    /// <summary>
    /// Quick task creation overlay (bottom zone)
    /// Smart parsing: "title | project | due | priority"
    /// Example: "Fix bug | Backend | +3 | high"
    /// </summary>
    public class QuickAddTaskOverlay : UserControl
    {
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly SmartInputParser inputParser;

        private TextBox inputBox;
        private TextBlock hintText;

        public event Action<TaskItem> TaskCreated;
        public event Action Cancelled;

        public QuickAddTaskOverlay(ITaskService taskService, IProjectService projectService)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.logger = Logger.Instance;
            this.themeManager = ThemeManager.Instance;
            this.inputParser = SmartInputParser.Instance;

            BuildUI();
        }

        private void BuildUI()
        {
            // TUI-style: Full width box at bottom with box-drawing chars
            var mainPanel = new Grid
            {
                Background = new SolidColorBrush(Colors.Black)
            };

            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // Header
            var header = new TextBlock
            {
                Text = "┌─ QUICK ADD ────────────────────────────────────────────────────────────────────────┐",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0)), // Yellow
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(0)
            };
            Grid.SetRow(header, 0);
            mainPanel.Children.Add(header);

            // Input area with border
            var inputBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 0)),
                BorderThickness = new Thickness(1, 0, 1, 0),
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(2, 0, 2, 0)
            };

            var inputStack = new StackPanel { Background = new SolidColorBrush(Colors.Black) };

            inputBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.Black),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            inputBox.KeyDown += OnInputKeyDown;
            inputBox.TextChanged += OnInputTextChanged;

            inputStack.Children.Add(inputBox);

            hintText = new TextBlock
            {
                Text = "│ title | project | due | priority",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 10,
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(0),
                Margin = new Thickness(0)
            };

            inputStack.Children.Add(hintText);

            inputBorder.Child = inputStack;
            Grid.SetRow(inputBorder, 1);
            mainPanel.Children.Add(inputBorder);

            // Footer
            var footerStack = new StackPanel { Background = new SolidColorBrush(Colors.Black) };
            var footerBorder = new TextBlock
            {
                Text = "└────────────────────────────────────────────────────────────────────────────────────┘",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0)),
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(0)
            };
            footerStack.Children.Add(footerBorder);

            var instructions = new TextBlock
            {
                Text = " [Enter]Create [Esc]Cancel",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(1, 0, 0, 0)
            };
            footerStack.Children.Add(instructions);

            Grid.SetRow(footerStack, 2);
            mainPanel.Children.Add(footerStack);

            this.Content = mainPanel;
            this.Focusable = true;
            this.Background = new SolidColorBrush(Colors.Black);
            this.Padding = new Thickness(0);
            this.Margin = new Thickness(0);

            FocusManager.SetIsFocusScope(this, true);

            this.Loaded += (s, e) =>
            {
                inputBox.Focus();
                Keyboard.Focus(inputBox);
            };
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Create task
                var taskText = inputBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(taskText))
                {
                    var task = ParseTaskFromInput(taskText);
                    if (task != null)
                    {
                        try
                        {
                            taskService.AddTask(task);
                            logger?.Info("QuickAddTaskOverlay", $"Created task: {task.Title}");

                            TaskCreated?.Invoke(task);

                            // Clear input for next task (or close overlay)
                            inputBox.Text = "";
                            // Let parent decide whether to close overlay
                        }
                        catch (Exception ex)
                        {
                            logger?.Error("QuickAddTaskOverlay", $"Failed to create task: {ex.Message}", ex);
                            // Show error in hint text
                            hintText.Text = $"Error: {ex.Message}\nPress Esc to cancel";
                            hintText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.Error);
                        }
                    }
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Cancel
                Cancelled?.Invoke();
                e.Handled = true;
            }
        }

        private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            // Real-time preview (optional: show parsed fields)
            var text = inputBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var preview = PreviewParsedTask(text);
                if (!string.IsNullOrEmpty(preview))
                {
                    hintText.Text = preview;
                    hintText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.Success);
                }
            }
            else
            {
                // Reset to default hint
                hintText.Text = "│ title | project | due | priority";
                hintText.Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            }
        }

        /// <summary>
        /// Parse task from input text
        /// Format: "title | project | due | priority"
        /// </summary>
        private TaskItem ParseTaskFromInput(string text)
        {
            var parts = text.Split('|').Select(p => p.Trim()).ToArray();

            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                logger?.Warning("QuickAddTaskOverlay", "Task title is required");
                return null;
            }

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = parts[0],
                Status = TaskStatus.Pending,
                Priority = TaskPriority.Medium,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // Parse optional parts
            for (int i = 1; i < parts.Length; i++)
            {
                var part = parts[i];

                if (string.IsNullOrWhiteSpace(part))
                    continue;

                // Try to parse as date
                var parsedDate = inputParser.ParseDate(part);
                if (parsedDate.HasValue)
                {
                    task.DueDate = parsedDate.Value;
                    continue;
                }

                // Try to parse as priority
                if (Enum.TryParse<TaskPriority>(part, true, out var priority))
                {
                    task.Priority = priority;
                    continue;
                }

                // Try to parse as project name (case-insensitive match)
                var projects = projectService.GetAllProjects()?.Where(p => !p.Deleted).ToList();
                var matchingProject = projects?.FirstOrDefault(p =>
                    p.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

                if (matchingProject != null)
                {
                    task.ProjectId = matchingProject.Id;
                    continue;
                }

                // If nothing matched, might be part of title (concatenate)
                logger?.Debug("QuickAddTaskOverlay", $"Could not parse part: {part}");
            }

            return task;
        }

        /// <summary>
        /// Preview parsed task fields (real-time feedback)
        /// </summary>
        private string PreviewParsedTask(string text)
        {
            var task = ParseTaskFromInput(text);
            if (task == null)
                return string.Empty;

            var preview = $"✓ Will create: {task.Title}\n";

            if (task.ProjectId.HasValue)
            {
                var project = projectService.GetProject(task.ProjectId.Value);
                if (project != null)
                    preview += $"  Project: {project.Name}\n";
            }

            if (task.DueDate.HasValue)
            {
                preview += $"  Due: {task.DueDate.Value:yyyy-MM-dd (ddd)}\n";
            }

            preview += $"  Priority: {task.Priority}";

            return preview;
        }
    }
}
