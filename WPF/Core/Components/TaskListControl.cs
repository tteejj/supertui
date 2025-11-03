using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Core.ViewModels;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Purpose-built control for displaying and editing tasks
    /// Supports inline editing of all fields, subtask hierarchy, and keyboard-driven operations
    /// </summary>
    public class TaskListControl : UserControl, IThemeable
    {
        private Theme theme;
        private TaskService taskService;

        // UI Components
        private ListBox listBox;
        private TextBlock titleLabel;
        private TextBlock statusLabel;

        // Display state
        private ObservableCollection<TaskViewModel> displayTasks;
        private TaskViewModel selectedTaskVM;
        private Dictionary<Guid, bool> expandedTasks;

        // Inline edit state
        private TaskViewModel editingTaskVM;
        private Panel editPanel;
        private TextBox editTitleBox;
        private DatePicker editDueDatePicker;
        private ComboBox editStatusCombo;
        private ComboBox editPriorityCombo;

        // Events
        public event Action<TaskItem> TaskSelected;
        public event Action<TaskItem> TaskModified;
        public event Action<TaskItem> TaskAdded;
        public event Action<Guid> TaskDeleted;

        // Properties
        public TaskItem SelectedTask => selectedTaskVM?.Task;
        public int TaskCount => displayTasks?.Count ?? 0;

        public TaskListControl()
        {
            theme = ThemeManager.Instance?.CurrentTheme ?? Theme.CreateDarkTheme();
            taskService = TaskService.Instance;
            displayTasks = new ObservableCollection<TaskViewModel>();
            expandedTasks = new Dictionary<Guid, bool>();

            BuildUI();
            SetupEventHandlers();
        }

        private void BuildUI()
        {
            var mainPanel = new DockPanel
            {
                LastChildFill = true,
                Background = new SolidColorBrush(theme.Background)
            };

            // Title
            titleLabel = new TextBlock
            {
                Text = "TASKS",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(titleLabel, Dock.Top);
            mainPanel.Children.Add(titleLabel);

            // Help text
            var helpText = new TextBlock
            {
                Text = "a:Add e:Edit d:Del Space:Toggle p:Priority s:Subtask x:Expand",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            DockPanel.SetDock(helpText, Dock.Top);
            mainPanel.Children.Add(helpText);

            // Status label at bottom
            statusLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 5, 0, 0),
                Text = "0 tasks"
            };
            DockPanel.SetDock(statusLabel, Dock.Bottom);
            mainPanel.Children.Add(statusLabel);

            // Main list box
            listBox = new ListBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            // Enable virtualization for performance
            VirtualizingPanel.SetIsVirtualizing(listBox, true);
            VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);

            // Custom item container style for better control
            var itemContainerStyle = new Style(typeof(ListBoxItem));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0)));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            listBox.ItemContainerStyle = itemContainerStyle;

            // Custom item template
            listBox.ItemTemplate = CreateItemTemplate();

            // Set ItemsSource once (will auto-update via ObservableCollection)
            listBox.ItemsSource = displayTasks;

            mainPanel.Children.Add(listBox);

            Content = mainPanel;
        }

        private DataTemplate CreateItemTemplate()
        {
            var template = new DataTemplate();

            // Root panel for each item
            var panelFactory = new FrameworkElementFactory(typeof(Border));
            panelFactory.SetValue(Border.PaddingProperty, new Thickness(5, 3, 5, 3));
            panelFactory.SetValue(Border.MarginProperty, new Thickness(0, 1, 0, 1));
            panelFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            // TextBlock to display task
            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("."));
            textFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas"));
            textFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            textFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(theme.Foreground));

            panelFactory.AppendChild(textFactory);
            template.VisualTree = panelFactory;

            return template;
        }

        private void SetupEventHandlers()
        {
            listBox.SelectionChanged += ListBox_SelectionChanged;
            listBox.KeyDown += ListBox_KeyDown;
            listBox.MouseDoubleClick += ListBox_MouseDoubleClick;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listBox.SelectedItem is TaskViewModel taskVM)
            {
                selectedTaskVM = taskVM;
                TaskSelected?.Invoke(taskVM.Task);
            }
            else
            {
                selectedTaskVM = null;
            }
        }

        private void ListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (editingTaskVM != null)
            {
                HandleEditModeKeys(e);
                return;
            }

            switch (e.Key)
            {
                case Key.A:
                    AddNewTask();
                    e.Handled = true;
                    break;

                case Key.E:
                    if (selectedTaskVM != null)
                    {
                        StartInlineEdit(selectedTaskVM);
                        e.Handled = true;
                    }
                    break;

                case Key.D:
                    if (selectedTaskVM != null)
                    {
                        DeleteTask(selectedTaskVM);
                        e.Handled = true;
                    }
                    break;

                case Key.Space:
                    if (selectedTaskVM != null)
                    {
                        CycleTaskStatus(selectedTaskVM);
                        e.Handled = true;
                    }
                    break;

                case Key.P:
                    if (selectedTaskVM != null)
                    {
                        taskService.CyclePriority(selectedTaskVM.Task.Id);
                        RefreshDisplay();
                        e.Handled = true;
                    }
                    break;

                case Key.S:
                    if (selectedTaskVM != null && !selectedTaskVM.Task.IsSubtask)
                    {
                        AddSubtask(selectedTaskVM);
                        e.Handled = true;
                    }
                    break;

                case Key.X:
                case Key.Enter:
                    if (selectedTaskVM != null && !selectedTaskVM.Task.IsSubtask &&
                        taskService.HasSubtasks(selectedTaskVM.Task.Id))
                    {
                        ToggleExpansion(selectedTaskVM);
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (selectedTaskVM != null)
            {
                StartInlineEdit(selectedTaskVM);
            }
        }

        #region Public API

        public void LoadTasks(Func<TaskItem, bool> filter)
        {
            // Get filtered top-level tasks
            var topLevelTasks = taskService.GetTasks(filter)
                .Where(t => !t.IsSubtask)
                .ToList();

            // Build display list with expanded subtasks using Clear/Add for ObservableCollection
            displayTasks.Clear();
            foreach (var task in topLevelTasks)
            {
                var taskVM = new TaskViewModel(task, taskService);
                displayTasks.Add(taskVM);

                // Add subtasks if expanded
                if (expandedTasks.ContainsKey(task.Id) && expandedTasks[task.Id])
                {
                    taskVM.IsExpanded = true;
                    var subtasks = taskService.GetSubtasks(task.Id);
                    foreach (var subtask in subtasks)
                    {
                        displayTasks.Add(new TaskViewModel(subtask, taskService));
                    }
                }
            }

            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            var previousSelection = selectedTaskVM?.Task.Id;

            // No need to reset ItemsSource - ObservableCollection auto-updates UI

            // Restore selection
            if (previousSelection.HasValue)
            {
                var vmToSelect = displayTasks.FirstOrDefault(vm => vm.Task.Id == previousSelection.Value);
                if (vmToSelect != null)
                {
                    listBox.SelectedItem = vmToSelect;
                }
            }

            UpdateStatusLabel();
        }

        public void SetExpandedTasks(Dictionary<Guid, bool> expanded)
        {
            expandedTasks = expanded ?? new Dictionary<Guid, bool>();
        }

        public Dictionary<Guid, bool> GetExpandedTasks()
        {
            return new Dictionary<Guid, bool>(expandedTasks);
        }

        #endregion

        #region Task Operations

        private void AddNewTask()
        {
            var newTask = new TaskItem
            {
                Title = "New Task",
                Description = "",
                Status = TaskStatus.Pending,
                Priority = TaskPriority.Medium
            };

            taskService.AddTask(newTask);

            // Reload and select new task
            LoadTasks(t => !t.Deleted);

            var newTaskVM = displayTasks.FirstOrDefault(vm => vm.Task.Id == newTask.Id);
            if (newTaskVM != null)
            {
                listBox.SelectedItem = newTaskVM;
                StartInlineEdit(newTaskVM);
            }

            TaskAdded?.Invoke(newTask);
            Logger.Instance?.Info("TaskList", $"Added new task: {newTask.Title}");
        }

        private void AddSubtask(TaskViewModel parentVM)
        {
            var subtask = new TaskItem
            {
                Title = "New Subtask",
                Description = "",
                Status = TaskStatus.Pending,
                Priority = parentVM.Task.Priority,
                ParentTaskId = parentVM.Task.Id
            };

            taskService.AddTask(subtask);

            // Auto-expand parent
            expandedTasks[parentVM.Task.Id] = true;
            parentVM.IsExpanded = true;

            // Reload
            LoadTasks(t => !t.Deleted);

            // Select new subtask
            var newSubtaskVM = displayTasks.FirstOrDefault(vm => vm.Task.Id == subtask.Id);
            if (newSubtaskVM != null)
            {
                listBox.SelectedItem = newSubtaskVM;
                StartInlineEdit(newSubtaskVM);
            }

            TaskAdded?.Invoke(subtask);
            Logger.Instance?.Info("TaskList", $"Added subtask to: {parentVM.Task.Title}");
        }

        private void DeleteTask(TaskViewModel taskVM)
        {
            var hasSubtasks = taskService.HasSubtasks(taskVM.Task.Id);
            var message = hasSubtasks
                ? $"Delete '{taskVM.Task.Title}' and all its subtasks?\n\nThis cannot be undone."
                : $"Delete '{taskVM.Task.Title}'?\n\nThis cannot be undone.";

            var result = MessageBox.Show(message, "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var taskId = taskVM.Task.Id;
                taskService.DeleteTask(taskId);

                LoadTasks(t => !t.Deleted);

                TaskDeleted?.Invoke(taskId);
                Logger.Instance?.Info("TaskList", $"Deleted task: {taskVM.Task.Title}");
            }
        }

        private void CycleTaskStatus(TaskViewModel taskVM)
        {
            var task = taskVM.Task;
            task.Status = task.Status switch
            {
                TaskStatus.Pending => TaskStatus.InProgress,
                TaskStatus.InProgress => TaskStatus.Completed,
                TaskStatus.Completed => TaskStatus.Pending,
                TaskStatus.Cancelled => TaskStatus.Pending,
                _ => TaskStatus.Pending
            };

            task.Progress = task.Status == TaskStatus.Completed ? 100 :
                           task.Status == TaskStatus.InProgress ? 50 : 0;

            taskService.UpdateTask(task);
            RefreshDisplay();

            TaskModified?.Invoke(task);
            Logger.Instance?.Debug("TaskList", $"Cycled status: {task.Title} to {task.Status}");
        }

        private void ToggleExpansion(TaskViewModel taskVM)
        {
            if (!taskService.HasSubtasks(taskVM.Task.Id)) return;

            var taskId = taskVM.Task.Id;
            if (expandedTasks.ContainsKey(taskId))
                expandedTasks[taskId] = !expandedTasks[taskId];
            else
                expandedTasks[taskId] = true;

            taskVM.IsExpanded = expandedTasks[taskId];

            LoadTasks(t => !t.Deleted);

            // Reselect the parent
            var vmToSelect = displayTasks.FirstOrDefault(vm => vm.Task.Id == taskId);
            if (vmToSelect != null)
            {
                listBox.SelectedItem = vmToSelect;
            }

            Logger.Instance?.Debug("TaskList", $"Toggled expansion for: {taskVM.Task.Title}");
        }

        #endregion

        #region Inline Editing

        private void StartInlineEdit(TaskViewModel taskVM)
        {
            if (editingTaskVM != null) return; // Already editing

            editingTaskVM = taskVM;

            // Create inline edit panel
            editPanel = CreateInlineEditPanel(taskVM.Task);

            // Replace the ListBoxItem content
            var selectedIndex = listBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < displayTasks.Count)
            {
                // We need to replace the visual element
                // This is tricky with ListBox DataTemplate
                // Better approach: show edit panel in a popup or overlay

                // For now, use a simple approach: show in status area
                ShowInlineEditPanel(editPanel);
            }

            Logger.Instance?.Debug("TaskList", $"Started inline edit: {taskVM.Task.Title}");
        }

        private Panel CreateInlineEditPanel(TaskItem task)
        {
            var panel = new StackPanel
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Margin = new Thickness(0, 5, 0, 5)
            };

            // Title
            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var titleLabel = new TextBlock
            {
                Text = "Title:",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5)
            };
            Grid.SetColumn(titleLabel, 0);
            titleGrid.Children.Add(titleLabel);

            editTitleBox = new TextBox
            {
                Text = task.Title,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Info),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(5),
                Margin = new Thickness(5)
            };
            Grid.SetColumn(editTitleBox, 1);
            titleGrid.Children.Add(editTitleBox);

            panel.Children.Add(titleGrid);

            // Status and Priority row
            var statusPriorityGrid = new Grid();
            statusPriorityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusPriorityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Status
            var statusPanel = new StackPanel { Margin = new Thickness(5) };
            statusPanel.Children.Add(new TextBlock
            {
                Text = "Status:",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary)
            });

            editStatusCombo = new ComboBox
            {
                ItemsSource = Enum.GetValues(typeof(TaskStatus)),
                SelectedItem = task.Status,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                Margin = new Thickness(0, 3, 0, 0)
            };
            statusPanel.Children.Add(editStatusCombo);
            Grid.SetColumn(statusPanel, 0);
            statusPriorityGrid.Children.Add(statusPanel);

            // Priority
            var priorityPanel = new StackPanel { Margin = new Thickness(5) };
            priorityPanel.Children.Add(new TextBlock
            {
                Text = "Priority:",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary)
            });

            editPriorityCombo = new ComboBox
            {
                ItemsSource = Enum.GetValues(typeof(TaskPriority)),
                SelectedItem = task.Priority,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                Margin = new Thickness(0, 3, 0, 0)
            };
            priorityPanel.Children.Add(editPriorityCombo);
            Grid.SetColumn(priorityPanel, 1);
            statusPriorityGrid.Children.Add(priorityPanel);

            panel.Children.Add(statusPriorityGrid);

            // Due Date
            var dueDateGrid = new Grid();
            dueDateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            dueDateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dueDateLabel = new TextBlock
            {
                Text = "Due:",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5)
            };
            Grid.SetColumn(dueDateLabel, 0);
            dueDateGrid.Children.Add(dueDateLabel);

            editDueDatePicker = new DatePicker
            {
                SelectedDate = task.DueDate,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                Margin = new Thickness(5)
            };
            Grid.SetColumn(editDueDatePicker, 1);
            dueDateGrid.Children.Add(editDueDatePicker);

            panel.Children.Add(dueDateGrid);

            // Instructions
            var instructions = new TextBlock
            {
                Text = "Enter: Save  |  Esc: Cancel",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(instructions);

            return panel;
        }

        private void ShowInlineEditPanel(Panel panel)
        {
            // Show edit panel above list
            if (Content is DockPanel mainPanel)
            {
                // Find and remove old edit panel if exists
                var oldEditPanel = mainPanel.Children.OfType<Panel>()
                    .FirstOrDefault(p => !ReferenceEquals(p, listBox) && p.Background != null);
                if (oldEditPanel != null)
                {
                    mainPanel.Children.Remove(oldEditPanel);
                }

                // Add new edit panel above list
                DockPanel.SetDock(panel, Dock.Top);
                mainPanel.Children.Insert(mainPanel.Children.IndexOf(listBox), panel);

                // Focus title box
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (editTitleBox != null) System.Windows.Input.Keyboard.Focus(editTitleBox);
                    editTitleBox?.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void HandleEditModeKeys(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        }

        private void CommitEdit()
        {
            if (editingTaskVM == null) return;

            var task = editingTaskVM.Task;
            task.Title = editTitleBox.Text?.Trim() ?? task.Title;
            task.Status = (TaskStatus)(editStatusCombo.SelectedItem ?? task.Status);
            task.Priority = (TaskPriority)(editPriorityCombo.SelectedItem ?? task.Priority);
            task.DueDate = editDueDatePicker.SelectedDate;
            task.UpdatedAt = DateTime.Now;

            if (string.IsNullOrWhiteSpace(task.Title))
            {
                statusLabel.Text = "Title cannot be empty";
                statusLabel.Foreground = new SolidColorBrush(theme.Error);
                return;
            }

            taskService.UpdateTask(task);
            CloseEditPanel();
            RefreshDisplay();

            TaskModified?.Invoke(task);
            Logger.Instance?.Info("TaskList", $"Updated task: {task.Title}");
        }

        private void CancelEdit()
        {
            CloseEditPanel();
            Logger.Instance?.Debug("TaskList", "Cancelled inline edit");
        }

        private void CloseEditPanel()
        {
            editingTaskVM = null;

            if (editPanel != null && Content is DockPanel mainPanel)
            {
                mainPanel.Children.Remove(editPanel);
                editPanel = null;
            }

            editTitleBox = null;
            editStatusCombo = null;
            editPriorityCombo = null;
            editDueDatePicker = null;

            // Return focus to listbox
            if (listBox != null) System.Windows.Input.Keyboard.Focus(listBox);
        }

        #endregion

        #region Helper Methods

        private void UpdateStatusLabel()
        {
            var count = displayTasks?.Count ?? 0;
            statusLabel.Text = count == 1 ? "1 task" : $"{count} tasks";
            statusLabel.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
        }

        public void ApplyTheme()
        {
            theme = ThemeManager.Instance?.CurrentTheme ?? Theme.CreateDarkTheme();

            if (Content is DockPanel mainPanel)
            {
                mainPanel.Background = new SolidColorBrush(theme.Background);
            }

            if (titleLabel != null)
            {
                titleLabel.Foreground = new SolidColorBrush(theme.Foreground);
            }

            if (listBox != null)
            {
                listBox.Background = new SolidColorBrush(theme.Surface);
                listBox.Foreground = new SolidColorBrush(theme.Foreground);
                listBox.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (statusLabel != null)
            {
                statusLabel.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            }

            // Refresh display to update item colors
            RefreshDisplay();
        }

        #endregion

        public void Dispose()
        {
            if (listBox != null)
            {
                listBox.SelectionChanged -= ListBox_SelectionChanged;
                listBox.KeyDown -= ListBox_KeyDown;
                listBox.MouseDoubleClick -= ListBox_MouseDoubleClick;
            }

            CloseEditPanel();

            displayTasks?.Clear();
            expandedTasks?.Clear();
        }
    }
}
