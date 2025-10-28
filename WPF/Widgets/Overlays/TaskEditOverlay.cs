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

namespace SuperTUI.Widgets.Overlays
{
    /// <summary>
    /// Full task editor overlay (center zone)
    /// Provides complete CRUD interface for task editing
    /// </summary>
    public class TaskEditOverlay : UserControl
    {
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly ITagService tagService;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly TaskItem task;
        private readonly bool isNewTask;

        private TextBox titleBox;
        private TextBox descriptionBox;
        private ComboBox statusCombo;
        private ComboBox priorityCombo;
        private DatePicker dueDatePicker;
        private TextBox projectBox;
        private TextBox tagsBox;
        private TextBox notesBox;
        private TextBlock statusText;

        public event Action<TaskItem> TaskSaved;
        public event Action Cancelled;

        public TaskEditOverlay(
            TaskItem task,
            ITaskService taskService,
            IProjectService projectService,
            ITagService tagService,
            bool isNewTask = false)
        {
            this.task = task ?? throw new ArgumentNullException(nameof(task));
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            this.logger = Logger.Instance;
            this.themeManager = ThemeManager.Instance;
            this.isNewTask = isNewTask;

            BuildUI();
            LoadTaskData();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            var mainPanel = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(20),
                Margin = new Thickness(50),
                MaxWidth = 800,
                MaxHeight = 700
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var formPanel = new StackPanel { Margin = new Thickness(0) };

            // Title
            formPanel.Children.Add(CreateLabel("Task Title"));
            titleBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 14,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 15)
            };
            formPanel.Children.Add(titleBox);

            // Description
            formPanel.Children.Add(CreateLabel("Description"));
            descriptionBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 15),
                MinHeight = 60,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            formPanel.Children.Add(descriptionBox);

            // Status and Priority (side by side)
            var statusPriorityPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var statusPanel = new StackPanel { Margin = new Thickness(0, 0, 20, 0) };
            statusPanel.Children.Add(CreateLabel("Status"));
            statusCombo = new ComboBox
            {
                Width = 200,
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 5, 0, 0)
            };
            statusCombo.Items.Add(new ComboBoxItem { Content = "Pending", Tag = TaskStatus.Pending });
            statusCombo.Items.Add(new ComboBoxItem { Content = "In Progress", Tag = TaskStatus.InProgress });
            statusCombo.Items.Add(new ComboBoxItem { Content = "Completed", Tag = TaskStatus.Completed });
            statusCombo.Items.Add(new ComboBoxItem { Content = "Cancelled", Tag = TaskStatus.Cancelled });
            statusPanel.Children.Add(statusCombo);

            var priorityPanel = new StackPanel();
            priorityPanel.Children.Add(CreateLabel("Priority"));
            priorityCombo = new ComboBox
            {
                Width = 200,
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 5, 0, 0)
            };
            priorityCombo.Items.Add(new ComboBoxItem { Content = "Low", Tag = TaskPriority.Low });
            priorityCombo.Items.Add(new ComboBoxItem { Content = "Medium", Tag = TaskPriority.Medium });
            priorityCombo.Items.Add(new ComboBoxItem { Content = "High", Tag = TaskPriority.High });
            priorityCombo.Items.Add(new ComboBoxItem { Content = "Today", Tag = TaskPriority.Today });
            priorityPanel.Children.Add(priorityCombo);

            statusPriorityPanel.Children.Add(statusPanel);
            statusPriorityPanel.Children.Add(priorityPanel);
            formPanel.Children.Add(statusPriorityPanel);

            // Due Date
            formPanel.Children.Add(CreateLabel("Due Date"));
            dueDatePicker = new DatePicker
            {
                Width = 200,
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 5, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            formPanel.Children.Add(dueDatePicker);

            // Project
            formPanel.Children.Add(CreateLabel("Project"));
            projectBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 15)
            };
            formPanel.Children.Add(projectBox);

            // Tags
            formPanel.Children.Add(CreateLabel("Tags (comma-separated)"));
            tagsBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 15)
            };
            formPanel.Children.Add(tagsBox);

            // Notes
            formPanel.Children.Add(CreateLabel("Notes"));
            notesBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 15),
                MinHeight = 80,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            formPanel.Children.Add(notesBox);

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var saveButton = new Button
            {
                Content = isNewTask ? "Create Task" : "Save Changes",
                Width = 120,
                Height = 35,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            saveButton.Click += OnSave;

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 120,
                Height = 35,
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            cancelButton.Click += (s, e) => Cancelled?.Invoke();

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            formPanel.Children.Add(buttonPanel);

            // Status text
            statusText = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Secondary),
                TextWrapping = TextWrapping.Wrap
            };
            formPanel.Children.Add(statusText);

            scrollViewer.Content = formPanel;
            mainPanel.Child = scrollViewer;
            this.Content = mainPanel;
            this.Focusable = true;

            // Keyboard shortcuts
            this.KeyDown += OnKeyDown;

            // Focus title box when loaded
            this.Loaded += (s, e) => titleBox.Focus();
        }

        private TextBlock CreateLabel(string text)
        {
            var theme = themeManager.CurrentTheme;
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Secondary),
                Margin = new Thickness(0, 0, 0, 0)
            };
        }

        private void LoadTaskData()
        {
            titleBox.Text = task.Title ?? "";
            descriptionBox.Text = task.Description ?? "";

            // Notes is a List<TaskNote>, so convert to text
            if (task.Notes != null && task.Notes.Any())
            {
                notesBox.Text = string.Join("\n---\n", task.Notes.Select(n => n.Content));
            }

            // Set status
            foreach (ComboBoxItem item in statusCombo.Items)
            {
                if ((TaskStatus)item.Tag == task.Status)
                {
                    statusCombo.SelectedItem = item;
                    break;
                }
            }

            // Set priority
            foreach (ComboBoxItem item in priorityCombo.Items)
            {
                if ((TaskPriority)item.Tag == task.Priority)
                {
                    priorityCombo.SelectedItem = item;
                    break;
                }
            }

            // Set due date
            if (task.DueDate.HasValue)
            {
                dueDatePicker.SelectedDate = task.DueDate.Value;
            }

            // Set project - ProjectId is Guid?
            if (task.ProjectId.HasValue)
            {
                var project = projectService.GetProject(task.ProjectId.Value);
                projectBox.Text = project?.Name ?? "";
            }

            // Set tags
            if (task.Tags != null && task.Tags.Any())
            {
                tagsBox.Text = string.Join(", ", task.Tags);
            }

            // Set status text
            if (isNewTask)
            {
                statusText.Text = "Creating new task...";
            }
            else
            {
                statusText.Text = $"Editing task (ID: {task.Id}) | Created: {task.CreatedAt:g}";
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S to save
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                OnSave(this, null);
                e.Handled = true;
            }
            // Escape to cancel
            else if (e.Key == Key.Escape)
            {
                Cancelled?.Invoke();
                e.Handled = true;
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(titleBox.Text))
                {
                    statusText.Text = "Error: Task title is required";
                    statusText.Foreground = new SolidColorBrush(Colors.Red);
                    return;
                }

                // Update task properties
                task.Title = titleBox.Text.Trim();
                task.Description = descriptionBox.Text?.Trim();

                // Convert notes text to List<TaskNote>
                if (!string.IsNullOrWhiteSpace(notesBox.Text))
                {
                    if (task.Notes == null)
                        task.Notes = new List<TaskNote>();

                    // Simple approach: treat entire text as one note
                    if (task.Notes.Count == 0)
                    {
                        task.Notes.Add(new TaskNote { Content = notesBox.Text.Trim() });
                    }
                    else
                    {
                        // Update the first note
                        task.Notes[0].Content = notesBox.Text.Trim();
                    }
                }

                task.Status = (TaskStatus)((ComboBoxItem)statusCombo.SelectedItem).Tag;
                task.Priority = (TaskPriority)((ComboBoxItem)priorityCombo.SelectedItem).Tag;
                task.DueDate = dueDatePicker.SelectedDate;
                task.UpdatedAt = DateTime.Now;

                // Handle project
                if (!string.IsNullOrWhiteSpace(projectBox.Text))
                {
                    var projectName = projectBox.Text.Trim();
                    var project = projectService.GetAllProjects()
                        .FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

                    if (project == null && !string.IsNullOrEmpty(projectName))
                    {
                        // Create new project if it doesn't exist
                        project = new Project
                        {
                            Name = projectName,
                            Description = $"Auto-created project for task: {task.Title}",
                            CreatedAt = DateTime.Now
                        };
                        projectService.AddProject(project);
                        logger?.Info("TaskEditOverlay", $"Created new project: {projectName}");
                    }

                    task.ProjectId = project?.Id;
                }
                else
                {
                    task.ProjectId = null;
                }

                // Handle tags
                if (!string.IsNullOrWhiteSpace(tagsBox.Text))
                {
                    var tags = tagsBox.Text.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                    task.Tags = tags;

                    // Tags will be validated and created automatically by SetTaskTags
                    // No need to manually add them to tag service
                }
                else
                {
                    task.Tags = null;
                }

                // Save to service
                if (isNewTask)
                {
                    task.CreatedAt = DateTime.Now;
                    taskService.AddTask(task);
                    logger?.Info("TaskEditOverlay", $"Created new task: {task.Title}");
                }
                else
                {
                    taskService.UpdateTask(task);
                    logger?.Info("TaskEditOverlay", $"Updated task: {task.Title}");
                }

                // Notify and close
                TaskSaved?.Invoke(task);
            }
            catch (Exception ex)
            {
                logger?.Error("TaskEditOverlay", $"Failed to save task: {ex.Message}");
                statusText.Text = $"Error: {ex.Message}";
                statusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
    }
}
