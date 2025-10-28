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
            // TUI-style: FULL SCREEN overlay with box characters, NO MARGINS
            var mainPanel = new Grid
            {
                Background = new SolidColorBrush(Colors.Black)
            };

            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Form
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // Header
            var header = new TextBlock
            {
                Text = isNewTask ? "┌─ NEW TASK ─────────────────────────────────────────────────────────────────────────┐" : "┌─ EDIT TASK ────────────────────────────────────────────────────────────────────────┐",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 255)), // Cyan
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(0)
            };
            Grid.SetRow(header, 0);
            mainPanel.Children.Add(header);

            // Form area with border
            var formBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 255)),
                BorderThickness = new Thickness(1, 0, 1, 0),
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(2, 1, 2, 1)
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Colors.Black)
            };

            var formPanel = new StackPanel
            {
                Background = new SolidColorBrush(Colors.Black)
            };

            // Title
            formPanel.Children.Add(CreateLabel("│ Title"));
            titleBox = new TextBox
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
            formPanel.Children.Add(titleBox);

            // Description
            formPanel.Children.Add(CreateLabel("│ Description"));
            descriptionBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.Black),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0),
                MinHeight = 40,
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
                Margin = new Thickness(0),
                Background = new SolidColorBrush(Colors.Black)
            };

            var statusPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0), Width = 200, Background = new SolidColorBrush(Colors.Black) };
            statusPanel.Children.Add(CreateLabel("│ Status"));
            statusCombo = CreateTUIComboBox(200);
            statusCombo.Items.Add(new ComboBoxItem { Content = "Pending", Tag = TaskStatus.Pending, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            statusCombo.Items.Add(new ComboBoxItem { Content = "In Progress", Tag = TaskStatus.InProgress, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            statusCombo.Items.Add(new ComboBoxItem { Content = "Completed", Tag = TaskStatus.Completed, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            statusCombo.Items.Add(new ComboBoxItem { Content = "Cancelled", Tag = TaskStatus.Cancelled, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            statusPanel.Children.Add(statusCombo);

            var priorityPanel = new StackPanel { Width = 200, Background = new SolidColorBrush(Colors.Black) };
            priorityPanel.Children.Add(CreateLabel("│ Priority"));
            priorityCombo = CreateTUIComboBox(200);
            priorityCombo.Items.Add(new ComboBoxItem { Content = "Low", Tag = TaskPriority.Low, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            priorityCombo.Items.Add(new ComboBoxItem { Content = "Medium", Tag = TaskPriority.Medium, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            priorityCombo.Items.Add(new ComboBoxItem { Content = "High", Tag = TaskPriority.High, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            priorityCombo.Items.Add(new ComboBoxItem { Content = "Today", Tag = TaskPriority.Today, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            priorityPanel.Children.Add(priorityCombo);

            statusPriorityPanel.Children.Add(statusPanel);
            statusPriorityPanel.Children.Add(priorityPanel);
            formPanel.Children.Add(statusPriorityPanel);

            // Due Date and Project (side by side)
            var dateProjPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var datePanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0), Width = 200, Background = new SolidColorBrush(Colors.Black) };
            datePanel.Children.Add(CreateLabel("│ Due Date"));
            dueDateCombo = CreateTUIComboBox(200);
            dueDateCombo.IsEditable = true;
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "None", Tag = (DateTime?)null, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "Today", Tag = DateTime.Today, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "Tomorrow", Tag = DateTime.Today.AddDays(1), Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "+3 days", Tag = DateTime.Today.AddDays(3), Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "+7 days", Tag = DateTime.Today.AddDays(7), Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            dueDateCombo.Items.Add(new ComboBoxItem { Content = "Next Mon", Tag = GetNextWeekday(DayOfWeek.Monday), Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });
            datePanel.Children.Add(dueDateCombo);

            var projPanel = new StackPanel { Width = 200, Background = new SolidColorBrush(Colors.Black) };
            projPanel.Children.Add(CreateLabel("│ Project"));
            projectCombo = CreateTUIComboBox(200);
            projectCombo.IsEditable = true;
            projectCombo.Items.Add(new ComboBoxItem { Content = "None", Tag = (Guid?)null, Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Colors.Black) });

            var projects = projectService.GetAllProjects()?.Where(p => !p.Deleted).OrderBy(p => p.Name).ToList();
            if (projects != null)
            {
                foreach (var proj in projects)
                {
                    projectCombo.Items.Add(new ComboBoxItem
                    {
                        Content = proj.Name,
                        Tag = proj.Id,
                        Foreground = new SolidColorBrush(Colors.White),
                        Background = new SolidColorBrush(Colors.Black)
                    });
                }
            }
            projPanel.Children.Add(projectCombo);

            dateProjPanel.Children.Add(datePanel);
            dateProjPanel.Children.Add(projPanel);
            formPanel.Children.Add(dateProjPanel);

            // Tags
            formPanel.Children.Add(CreateLabel("│ Tags"));
            tagsBox = new TextBox
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
            formPanel.Children.Add(tagsBox);

            // Notes
            formPanel.Children.Add(CreateLabel("│ Notes"));
            notesBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.Black),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0),
                MinHeight = 50,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            formPanel.Children.Add(notesBox);

            scrollViewer.Content = formPanel;
            formBorder.Child = scrollViewer;
            Grid.SetRow(formBorder, 1);
            mainPanel.Children.Add(formBorder);

            // Footer (TUI style)
            var footerStack = new StackPanel { Background = new SolidColorBrush(Colors.Black) };
            var footerBorder = new TextBlock
            {
                Text = "└────────────────────────────────────────────────────────────────────────────────────┘",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 255)),
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(0)
            };
            footerStack.Children.Add(footerBorder);

            var instructions = new TextBlock
            {
                Text = " [Ctrl+S]Save [Esc]Cancel [Tab]Next",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(1, 0, 0, 0)
            };
            footerStack.Children.Add(instructions);

            statusText = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0)),
                Background = new SolidColorBrush(Colors.Black),
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(1, 0, 0, 0)
            };
            footerStack.Children.Add(statusText);

            Grid.SetRow(footerStack, 2);
            mainPanel.Children.Add(footerStack);

            this.Content = mainPanel;
            this.Focusable = true;
            this.Background = new SolidColorBrush(Colors.Black);
            this.Padding = new Thickness(0);
            this.Margin = new Thickness(0);

            FocusManager.SetIsFocusScope(this, true);
            this.PreviewKeyDown += OnKeyDown;

            this.Loaded += (s, e) =>
            {
                titleBox.Focus();
                Keyboard.Focus(titleBox);
            };
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                Background = new SolidColorBrush(Colors.Black),
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };
        }

        private ComboBox CreateTUIComboBox(double width)
        {
            var combo = new ComboBox
            {
                Width = width,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.Black),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

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
