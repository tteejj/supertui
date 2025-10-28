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
        private ComboBox dueDateCombo; // Changed from DatePicker for keyboard friendliness
        private ComboBox projectCombo; // Changed from TextBox for dropdown
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
                Margin = new Thickness(0, 5, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 30
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
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            formPanel.Children.Add(descriptionBox);

            // Status and Priority (side by side)
            var statusPriorityPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var statusPanel = new StackPanel { Margin = new Thickness(0, 0, 20, 0) };
            statusPanel.Children.Add(CreateLabel("Status [Space/Enter=Open, ↑↓=Select]"));
            statusCombo = CreateKeyboardComboBox(theme, 200);
            statusCombo.Items.Add(new ComboBoxItem { Content = "Pending", Tag = TaskStatus.Pending, Foreground = new SolidColorBrush(theme.Foreground) });
            statusCombo.Items.Add(new ComboBoxItem { Content = "In Progress", Tag = TaskStatus.InProgress, Foreground = new SolidColorBrush(theme.Foreground) });
            statusCombo.Items.Add(new ComboBoxItem { Content = "Completed", Tag = TaskStatus.Completed, Foreground = new SolidColorBrush(theme.Foreground) });
            statusCombo.Items.Add(new ComboBoxItem { Content = "Cancelled", Tag = TaskStatus.Cancelled, Foreground = new SolidColorBrush(theme.Foreground) });
            statusPanel.Children.Add(statusCombo);

            var priorityPanel = new StackPanel();
            priorityPanel.Children.Add(CreateLabel("Priority [Space/Enter=Open, ↑↓=Select]"));
            priorityCombo = CreateKeyboardComboBox(theme, 200);
            priorityCombo.Items.Add(new ComboBoxItem { Content = "Low", Tag = TaskPriority.Low, Foreground = new SolidColorBrush(theme.Foreground) });
            priorityCombo.Items.Add(new ComboBoxItem { Content = "Medium", Tag = TaskPriority.Medium, Foreground = new SolidColorBrush(theme.Foreground) });
            priorityCombo.Items.Add(new ComboBoxItem { Content = "High", Tag = TaskPriority.High, Foreground = new SolidColorBrush(theme.Foreground) });
            priorityCombo.Items.Add(new ComboBoxItem { Content = "Today", Tag = TaskPriority.Today, Foreground = new SolidColorBrush(theme.Foreground) });
            priorityPanel.Children.Add(priorityCombo);

            statusPriorityPanel.Children.Add(statusPanel);
            statusPriorityPanel.Children.Add(priorityPanel);
            formPanel.Children.Add(statusPriorityPanel);

            // Due Date - keyboard-friendly ComboBox with smart date options
            formPanel.Children.Add(CreateLabel("Due Date [Space/Enter=Open, Type to filter]"));
            dueDateCombo = CreateKeyboardComboBox(theme, 200);
            dueDateCombo.IsEditable = true; // Allow typing dates like "tomorrow", "+3", "2025-11-01"
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "None", Tag = (DateTime?)null, Foreground = new SolidColorBrush(theme.Foreground) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "Today", Tag = DateTime.Today, Foreground = new SolidColorBrush(theme.Foreground) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "Tomorrow", Tag = DateTime.Today.AddDays(1), Foreground = new SolidColorBrush(theme.Foreground) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "+3 days", Tag = DateTime.Today.AddDays(3), Foreground = new SolidColorBrush(theme.Foreground) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "+7 days", Tag = DateTime.Today.AddDays(7), Foreground = new SolidColorBrush(theme.Foreground) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "Next Monday", Tag = GetNextWeekday(DayOfWeek.Monday), Foreground = new SolidColorBrush(theme.Foreground) });
            formPanel.Children.Add(dueDateCombo);

            // Project - dropdown with existing projects
            formPanel.Children.Add(CreateLabel("Project [Space/Enter=Open, Type to filter or create new]"));
            projectCombo = CreateKeyboardComboBox(theme, 200);
            projectCombo.IsEditable = true; // Allow typing new project names
            projectCombo.Items.Add(new ComboBoxItem { Content = "None", Tag = (Guid?)null, Foreground = new SolidColorBrush(theme.Foreground) });

            // Populate with existing projects
            var projects = projectService.GetAllProjects()?.Where(p => !p.Deleted).OrderBy(p => p.Name).ToList();
            if (projects != null)
            {
                foreach (var proj in projects)
                {
                    projectCombo.Items.Add(new ComboBoxItem
                    {
                        Content = proj.Name,
                        Tag = proj.Id,
                        Foreground = new SolidColorBrush(theme.Foreground)
                    });
                }
            }
            formPanel.Children.Add(projectCombo);

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
                Margin = new Thickness(0, 5, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 30
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
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalAlignment = HorizontalAlignment.Stretch
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
                Content = isNewTask ? "Create Task [Ctrl+S]" : "Save Changes [Ctrl+S]",
                Width = 180,
                Height = 35,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0)
            };
            // NO CLICK HANDLER - Ctrl+S only

            var cancelButton = new Button
            {
                Content = "Cancel [Esc]",
                Width = 120,
                Height = 35,
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1)
            };
            // NO CLICK HANDLER - Esc only

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

            // CRITICAL: Make this a focus scope so Tab stays INSIDE the overlay
            FocusManager.SetIsFocusScope(this, true);

            // Keyboard shortcuts - CAPTURE ALL KEYS
            this.PreviewKeyDown += OnKeyDown;

            // Focus title box when loaded and KEEP focus trapped
            this.Loaded += (s, e) =>
            {
                titleBox.Focus();
                Keyboard.Focus(titleBox);
            };
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

        private ComboBox CreateKeyboardComboBox(Theme theme, double width)
        {
            var combo = new ComboBox
            {
                Width = width,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Keyboard-friendly: Space or Enter opens dropdown
            combo.PreviewKeyDown += (s, e) =>
            {
                if ((e.Key == Key.Space || e.Key == Key.Enter) && !combo.IsDropDownOpen)
                {
                    combo.IsDropDownOpen = true;
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && combo.IsDropDownOpen)
                {
                    combo.IsDropDownOpen = false;
                    e.Handled = true;
                }
            };

            return combo;
        }

        private DateTime GetNextWeekday(DayOfWeek day)
        {
            var today = DateTime.Today;
            int daysUntil = ((int)day - (int)today.DayOfWeek + 7) % 7;
            if (daysUntil == 0) daysUntil = 7; // Next week, not today
            return today.AddDays(daysUntil);
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

            // Set due date - match to predefined option or show as custom text
            if (task.DueDate.HasValue)
            {
                bool matched = false;
                foreach (ComboBoxItem item in dueDateCombo.Items)
                {
                    if (item.Tag is DateTime dt && dt.Date == task.DueDate.Value.Date)
                    {
                        dueDateCombo.SelectedItem = item;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    // Custom date, set text directly (editable ComboBox)
                    dueDateCombo.Text = task.DueDate.Value.ToString("yyyy-MM-dd");
                }
            }
            else
            {
                // Select "None" option
                dueDateCombo.SelectedIndex = 0;
            }

            // Set project - ProjectId is Guid?
            if (task.ProjectId.HasValue)
            {
                var project = projectService.GetProject(task.ProjectId.Value);
                if (project != null)
                {
                    // Find matching ComboBoxItem
                    foreach (ComboBoxItem item in projectCombo.Items)
                    {
                        if (item.Tag is Guid projId && projId == project.Id)
                        {
                            projectCombo.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Select "None" option
                projectCombo.SelectedIndex = 0;
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
                task.UpdatedAt = DateTime.Now;

                // Handle due date - from ComboBox selection or custom text
                if (dueDateCombo.SelectedItem is ComboBoxItem dueDateItem)
                {
                    task.DueDate = dueDateItem.Tag as DateTime?;
                }
                else if (!string.IsNullOrWhiteSpace(dueDateCombo.Text))
                {
                    // Try to parse custom date input
                    if (DateTime.TryParse(dueDateCombo.Text, out var customDate))
                    {
                        task.DueDate = customDate;
                    }
                    else
                    {
                        // Try smart parsing ("+3", "tomorrow", etc.)
                        var smartDate = SmartInputParser.Instance.ParseDate(dueDateCombo.Text);
                        task.DueDate = smartDate;
                    }
                }
                else
                {
                    task.DueDate = null;
                }

                // Handle project - from ComboBox selection or custom text
                if (projectCombo.SelectedItem is ComboBoxItem projectItem)
                {
                    var projectId = projectItem.Tag as Guid?;
                    task.ProjectId = projectId;
                }
                else if (!string.IsNullOrWhiteSpace(projectCombo.Text))
                {
                    // User typed a new project name
                    var projectName = projectCombo.Text.Trim();
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
