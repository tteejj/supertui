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
            var theme = themeManager.CurrentTheme;

            // Simple terminal-style panel
            var mainPanel = new Border
            {
                Margin = new Thickness(10, 0, 10, 10),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var stack = new StackPanel();

            // Terminal header
            var header = new TextBlock
            {
                Text = "┌─ QUICK ADD ────────────────────────────────────────────┐",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 0, 0, 5)
            };
            stack.Children.Add(header);

            // Input box
            inputBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 13,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Secondary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            inputBox.KeyDown += OnInputKeyDown;
            inputBox.TextChanged += OnInputTextChanged;

            stack.Children.Add(inputBox);

            // Hint text (shows format and examples)
            hintText = new TextBlock
            {
                Text = "│ title | project | due | priority\n" +
                       "│ Ex: Fix bug | Backend | +3 | high\n" +
                       "└────────────────────────────────────────────────────────┘\n" +
                       "  [Enter] Create    [Esc] Cancel",
                Foreground = new SolidColorBrush(theme.Secondary),
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            stack.Children.Add(hintText);

            mainPanel.Child = stack;
            this.Content = mainPanel;
            this.Focusable = true;

            // CRITICAL: Trap focus inside overlay
            FocusManager.SetIsFocusScope(this, true);

            // Focus input box when overlay loads and KEEP focus
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
                var theme = themeManager.CurrentTheme;
                hintText.Text = "│ title | project | due | priority\n" +
                               "│ Ex: Fix bug | Backend | +3 | high\n" +
                               "└────────────────────────────────────────────────────────┘\n" +
                               "  [Enter] Create    [Esc] Cancel";
                hintText.Foreground = new SolidColorBrush(theme.Secondary);
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
