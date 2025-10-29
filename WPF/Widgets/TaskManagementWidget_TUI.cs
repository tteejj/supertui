using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Controls;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// TUI-styled task management widget using the new TUI control library
    /// Looks like a terminal app but has full WPF functionality
    /// </summary>
    public class TaskManagementWidget_TUI : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly ITagService tagService;

        private Theme theme;

        // UI Components
        private TUIListBox taskListBox;
        private TUIBox mainBox;
        private TUIStatusBar statusBar;
        private TUIBox quickAddBox;
        private TUITextInput titleInput;
        private TUITextInput descInput;
        private TUIComboBox statusCombo;
        private TUIComboBox priorityCombo;
        private TUIComboBox projectCombo;

        // State
        private List<TaskItem> allTasks;
        private List<TaskItem> filteredTasks;
        private TaskItem selectedTask;

        public TaskManagementWidget_TUI(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config,
            ITaskService taskService,
            IProjectService projectService,
            ITagService tagService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));

            WidgetName = "Tasks";
            WidgetType = "TaskManagement_TUI";

            allTasks = new List<TaskItem>();
            filteredTasks = new List<TaskItem>();
        }

        public TaskManagementWidget_TUI()
            : this(
                Logger.Instance,
                ThemeManager.Instance,
                ConfigurationManager.Instance,
                Core.Services.TaskService.Instance,
                Core.Services.ProjectService.Instance,
                Core.Services.TagService.Instance)
        {
        }

        public override void Initialize()
        {
            theme = themeManager.CurrentTheme;
            BuildUI();
            LoadTasks();
            ApplyTheme();
        }

        private void BuildUI()
        {
            // Main layout: Top area (tasks + quick add) + Status bar
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

            // Content area - split horizontally
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // Task list
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Quick add

            // ===== TASK LIST (Left) =====
            mainBox = new TUIBox
            {
                Title = "TASKS",
                BorderStyle = TUIBorderStyle.Single,
                ShowTitle = true
            };

            taskListBox = new TUIListBox
            {
                ShowCheckboxes = true
            };
            taskListBox.SelectionChanged += OnTaskSelectionChanged;

            mainBox.Content = taskListBox;
            Grid.SetColumn(mainBox, 0);
            contentGrid.Children.Add(mainBox);

            // ===== QUICK ADD (Right) =====
            quickAddBox = new TUIBox
            {
                Title = "QUICK ADD",
                BorderStyle = TUIBorderStyle.Single,
                ShowTitle = true,
                Margin = new Thickness(8, 0, 0, 0)
            };

            var formGrid = new Grid();
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title label
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title input
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Desc label
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Desc input
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status/Priority
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Project
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons
            formGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Spacer

            int row = 0;

            // Title
            var titleLabel = CreateLabel("Title");
            Grid.SetRow(titleLabel, row++);
            formGrid.Children.Add(titleLabel);

            titleInput = new TUITextInput { Margin = new Thickness(0, 0, 0, 8) };
            titleInput.KeyDown += OnQuickAddKeyDown;
            Grid.SetRow(titleInput, row++);
            formGrid.Children.Add(titleInput);

            // Description
            var descLabel = CreateLabel("Description");
            Grid.SetRow(descLabel, row++);
            formGrid.Children.Add(descLabel);

            descInput = new TUITextInput { Margin = new Thickness(0, 0, 0, 8) };
            descInput.KeyDown += OnQuickAddKeyDown;
            Grid.SetRow(descInput, row++);
            formGrid.Children.Add(descInput);

            // Status/Priority side by side
            var dropdownGrid = new Grid();
            dropdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dropdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            statusCombo = new TUIComboBox
            {
                Label = "Status",
                ItemsSource = new List<string> { "Pending", "In Progress", "Completed", "Cancelled" },
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 4, 8)
            };
            Grid.SetColumn(statusCombo, 0);
            dropdownGrid.Children.Add(statusCombo);

            priorityCombo = new TUIComboBox
            {
                Label = "Priority",
                ItemsSource = new List<string> { "Low", "Medium", "High", "Today" },
                SelectedIndex = 1,
                Margin = new Thickness(4, 0, 0, 8)
            };
            Grid.SetColumn(priorityCombo, 1);
            dropdownGrid.Children.Add(priorityCombo);

            Grid.SetRow(dropdownGrid, row++);
            formGrid.Children.Add(dropdownGrid);

            // Project
            var projects = projectService.GetAllProjects().Select(p => p.Name).ToList();
            projects.Insert(0, "None");

            projectCombo = new TUIComboBox
            {
                Label = "Project",
                ItemsSource = projects,
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(projectCombo, row++);
            formGrid.Children.Add(projectCombo);

            // Add button hint
            var hintText = new TextBlock
            {
                Text = "Press Ctrl+S to add task",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(4)
            };
            Grid.SetRow(hintText, row++);
            formGrid.Children.Add(hintText);

            quickAddBox.Content = formGrid;
            Grid.SetColumn(quickAddBox, 1);
            contentGrid.Children.Add(quickAddBox);

            Grid.SetRow(contentGrid, 0);
            mainGrid.Children.Add(contentGrid);

            // ===== STATUS BAR (Bottom) =====
            statusBar = new TUIStatusBar
            {
                Commands = new List<TUICommand>
                {
                    new TUICommand("Ctrl+N", "New"),
                    new TUICommand("Ctrl+S", "Add"),
                    new TUICommand("Enter", "Edit"),
                    new TUICommand("Del", "Delete"),
                    new TUICommand("F5", "Refresh")
                },
                StatusText = "Ready"
            };
            Grid.SetRow(statusBar, 1);
            mainGrid.Children.Add(statusBar);

            Content = mainGrid;
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 4, 0, 2)
            };
        }

        private void LoadTasks()
        {
            try
            {
                allTasks = taskService.GetAllTasks().ToList();
                filteredTasks = allTasks.Where(t => t.Status != TaskStatus.Completed).ToList();
                UpdateTaskList();
                statusBar.StatusText = $"{filteredTasks.Count} tasks";
            }
            catch (Exception ex)
            {
                logger?.Error("TaskWidget_TUI", $"Failed to load tasks: {ex.Message}", ex);
                statusBar.StatusText = "Error loading tasks";
            }
        }

        private void UpdateTaskList()
        {
            var displayItems = filteredTasks.Select(t => FormatTaskForDisplay(t)).ToList();
            taskListBox.ItemsSource = displayItems;
        }

        private string FormatTaskForDisplay(TaskItem task)
        {
            var checkbox = task.Status == TaskStatus.Completed ? "●" : "○";
            var priority = task.Priority switch
            {
                TaskPriority.Low => "↓",
                TaskPriority.Medium => "→",
                TaskPriority.High => "↑",
                TaskPriority.Today => "‼",
                _ => " "
            };

            var dueDate = task.DueDate.HasValue ? $"[{task.DueDate.Value:MMM dd}]" : "";

            return $"{checkbox} {priority} {task.Title} {dueDate}";
        }

        private void OnTaskSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (taskListBox.SelectedIndex >= 0 && taskListBox.SelectedIndex < filteredTasks.Count)
            {
                selectedTask = filteredTasks[taskListBox.SelectedIndex];
                statusBar.StatusText = $"Selected: {selectedTask.Title}";
            }
        }

        private void OnQuickAddKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                AddTask();
                e.Handled = true;
            }
        }

        private void AddTask()
        {
            if (string.IsNullOrWhiteSpace(titleInput.Text))
            {
                statusBar.StatusText = "Title required";
                return;
            }

            try
            {
                var newTask = new TaskItem
                {
                    Id = Guid.NewGuid(),
                    Title = titleInput.Text,
                    Description = descInput.Text,
                    Status = ParseStatus(statusCombo.SelectedItem?.ToString()),
                    Priority = ParsePriority(priorityCombo.SelectedItem?.ToString())
                };

                if (projectCombo.SelectedIndex > 0) // Not "None"
                {
                    var projects = projectService.GetAllProjects().ToList();
                    if (projectCombo.SelectedIndex - 1 < projects.Count)
                    {
                        newTask.ProjectId = projects[projectCombo.SelectedIndex - 1].Id;
                    }
                }

                taskService.AddTask(newTask);

                // Clear form
                titleInput.Text = "";
                descInput.Text = "";
                statusCombo.SelectedIndex = 0;
                priorityCombo.SelectedIndex = 1;
                projectCombo.SelectedIndex = 0;

                // Refresh list
                LoadTasks();
                statusBar.StatusText = $"Added: {newTask.Title}";
            }
            catch (Exception ex)
            {
                logger?.Error("TaskWidget_TUI", $"Failed to add task: {ex.Message}", ex);
                statusBar.StatusText = "Error adding task";
            }
        }

        private TaskStatus ParseStatus(string status)
        {
            return status switch
            {
                "Pending" => TaskStatus.Pending,
                "In Progress" => TaskStatus.InProgress,
                "Completed" => TaskStatus.Completed,
                "Cancelled" => TaskStatus.Cancelled,
                _ => TaskStatus.Pending
            };
        }

        private TaskPriority ParsePriority(string priority)
        {
            return priority switch
            {
                "Low" => TaskPriority.Low,
                "Medium" => TaskPriority.Medium,
                "High" => TaskPriority.High,
                "Today" => TaskPriority.Today,
                _ => TaskPriority.Medium
            };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled) return;

            switch (e.Key)
            {
                case Key.F5:
                    LoadTasks();
                    e.Handled = true;
                    break;

                case Key.Delete:
                    DeleteSelectedTask();
                    e.Handled = true;
                    break;

                case Key.N when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                    titleInput.Focus();
                    e.Handled = true;
                    break;
            }
        }

        private void DeleteSelectedTask()
        {
            if (selectedTask == null)
            {
                statusBar.StatusText = "No task selected";
                return;
            }

            try
            {
                taskService.DeleteTask(selectedTask.Id);
                statusBar.StatusText = $"Deleted: {selectedTask.Title}";
                selectedTask = null;
                LoadTasks();
            }
            catch (Exception ex)
            {
                logger?.Error("TaskWidget_TUI", $"Failed to delete task: {ex.Message}", ex);
                statusBar.StatusText = "Error deleting task";
            }
        }

        public void ApplyTheme()
        {
            theme = themeManager.CurrentTheme;
            // TUI controls auto-update, nothing needed here
        }

        protected override void OnDispose()
        {
            if (taskListBox != null)
            {
                taskListBox.SelectionChanged -= OnTaskSelectionChanged;
            }

            base.OnDispose();
        }
    }
}
