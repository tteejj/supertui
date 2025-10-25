using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Kanban board widget with 3 columns: TODO, IN PROGRESS, DONE
    /// Supports keyboard navigation and drag-drop between columns
    /// </summary>
    public class KanbanBoardWidget : WidgetBase, IThemeable
    {
        private Theme theme;
        private TaskService taskService;

        // UI Components
        private Grid mainGrid;
        private ListBox todoListBox;
        private ListBox inProgressListBox;
        private ListBox doneListBox;
        private TextBlock todoHeader;
        private TextBlock inProgressHeader;
        private TextBlock doneHeader;

        // Data collections
        private ObservableCollection<TaskItem> todoTasks;
        private ObservableCollection<TaskItem> inProgressTasks;
        private ObservableCollection<TaskItem> doneTasks;

        // Focus tracking
        private int currentColumn = 0; // 0=TODO, 1=IN PROGRESS, 2=DONE
        private ListBox CurrentListBox => currentColumn switch
        {
            0 => todoListBox,
            1 => inProgressListBox,
            2 => doneListBox,
            _ => todoListBox
        };

        // Refresh timer
        private DispatcherTimer refreshTimer;

        public KanbanBoardWidget()
        {
            WidgetName = "Kanban Board";
            WidgetType = "KanbanBoard";
        }

        public override void Initialize()
        {
            theme = ThemeManager.Instance.CurrentTheme;
            taskService = TaskService.Instance;

            // Initialize service
            taskService.Initialize();

            // Initialize collections
            todoTasks = new ObservableCollection<TaskItem>();
            inProgressTasks = new ObservableCollection<TaskItem>();
            doneTasks = new ObservableCollection<TaskItem>();

            BuildUI();
            LoadTasks();

            // Subscribe to task service events
            taskService.TaskAdded += OnTaskChanged;
            taskService.TaskUpdated += OnTaskChanged;
            taskService.TaskDeleted += (id) => LoadTasks();
            taskService.TasksReloaded += LoadTasks;

            // Setup refresh timer (every 10 seconds)
            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            refreshTimer.Tick += (s, e) => LoadTasks();
            refreshTimer.Start();

            Logger.Instance?.Info("KanbanWidget", "Kanban board widget initialized");
        }

        private void BuildUI()
        {
            // Main grid with 3 equal columns
            mainGrid = new Grid
            {
                Background = new SolidColorBrush(theme.Background)
            };

            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Build each column
            var todoColumn = BuildColumn("TODO", todoTasks, out todoListBox, out todoHeader, theme.Warning);
            var inProgressColumn = BuildColumn("IN PROGRESS", inProgressTasks, out inProgressListBox, out inProgressHeader, theme.Info);
            var doneColumn = BuildColumn("DONE", doneTasks, out doneListBox, out doneHeader, theme.Success);

            Grid.SetColumn(todoColumn, 0);
            Grid.SetColumn(inProgressColumn, 1);
            Grid.SetColumn(doneColumn, 2);

            mainGrid.Children.Add(todoColumn);
            mainGrid.Children.Add(inProgressColumn);
            mainGrid.Children.Add(doneColumn);

            this.Content = mainGrid;
        }

        private Border BuildColumn(string title, ObservableCollection<TaskItem> dataSource, out ListBox listBox, out TextBlock header, System.Windows.Media.Color headerColor)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5),
                Padding = new Thickness(0)
            };

            var stack = new StackPanel();

            // Header with count
            header = new TextBlock
            {
                Text = $"{title} (0)",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(headerColor),
                Background = new SolidColorBrush(theme.Surface),
                Padding = new Thickness(10, 8, 10, 8),
                TextAlignment = TextAlignment.Center
            };
            stack.Children.Add(header);

            // Separator
            var separator = new Border
            {
                Height = 2,
                Background = new SolidColorBrush(headerColor),
                Margin = new Thickness(0)
            };
            stack.Children.Add(separator);

            // Task list
            listBox = new ListBox
            {
                ItemsSource = dataSource,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Padding = new Thickness(5),
                SelectionMode = SelectionMode.Single,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            // Custom item template
            var itemTemplate = new DataTemplate();
            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.MarginProperty, new Thickness(0, 3, 0, 3));
            stackFactory.SetValue(StackPanel.BackgroundProperty, new SolidColorBrush(theme.Surface));
            stackFactory.SetValue(StackPanel.PaddingProperty, new Thickness(8, 5, 8, 5));

            // Status + Priority + Title
            var titleTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            titleTextFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title"));
            titleTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            titleTextFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            titleTextFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            stackFactory.AppendChild(titleTextFactory);

            // Due date and priority indicator
            var detailsTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            detailsTextFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            detailsTextFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 0));
            detailsTextFactory.SetValue(TextBlock.OpacityProperty, 0.8);

            // Use a converter or multi-binding for complex display
            var detailsBinding = new System.Windows.Data.MultiBinding();
            detailsBinding.StringFormat = "{0} | Due: {1}";
            detailsBinding.Bindings.Add(new System.Windows.Data.Binding("PriorityIcon"));
            detailsBinding.Bindings.Add(new System.Windows.Data.Binding("DueDate") { StringFormat = "yyyy-MM-dd", TargetNullValue = "Not set" });
            detailsTextFactory.SetBinding(TextBlock.TextProperty, detailsBinding);

            stackFactory.AppendChild(detailsTextFactory);

            itemTemplate.VisualTree = stackFactory;
            listBox.ItemTemplate = itemTemplate;

            // Selection and keyboard handling
            listBox.SelectionChanged += ListBox_SelectionChanged;
            listBox.KeyDown += ListBox_KeyDown;

            var scrollViewer = new ScrollViewer
            {
                Content = listBox,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            stack.Children.Add(scrollViewer);

            border.Child = stack;
            return border;
        }

        private void LoadTasks()
        {
            var allTasks = taskService.GetAllTasks();

            // Clear collections
            todoTasks.Clear();
            inProgressTasks.Clear();
            doneTasks.Clear();

            // Group tasks by status
            foreach (var task in allTasks.Where(t => !t.IsSubtask)) // Only show parent tasks
            {
                if (task.Status == TaskStatus.Completed)
                {
                    doneTasks.Add(task);
                }
                else if (task.Status == TaskStatus.InProgress)
                {
                    inProgressTasks.Add(task);
                }
                else // Pending, Cancelled
                {
                    todoTasks.Add(task);
                }
            }

            // Update headers with counts
            if (todoHeader != null)
                todoHeader.Text = $"TODO ({todoTasks.Count})";
            if (inProgressHeader != null)
                inProgressHeader.Text = $"IN PROGRESS ({inProgressTasks.Count})";
            if (doneHeader != null)
                doneHeader.Text = $"DONE ({doneTasks.Count})";

            // Apply color coding
            UpdateTaskColors();

            Logger.Instance?.Debug("KanbanWidget", $"Loaded {allTasks.Count} tasks");
        }

        private void UpdateTaskColors()
        {
            // Color-code ListBox items by due date
            UpdateListBoxItemColors(todoListBox);
            UpdateListBoxItemColors(inProgressListBox);
            UpdateListBoxItemColors(doneListBox);
        }

        private void UpdateListBoxItemColors(ListBox listBox)
        {
            if (listBox == null) return;

            foreach (var item in listBox.Items)
            {
                if (item is TaskItem task)
                {
                    var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                    if (container != null)
                    {
                        if (task.IsOverdue)
                        {
                            container.Foreground = new SolidColorBrush(theme.Error);
                        }
                        else if (task.IsDueToday)
                        {
                            container.Foreground = new SolidColorBrush(theme.Warning);
                        }
                        else if (task.Status == TaskStatus.Completed)
                        {
                            container.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
                        }
                        else
                        {
                            container.Foreground = new SolidColorBrush(theme.Foreground);
                        }
                    }
                }
            }
        }

        private void OnTaskChanged(TaskItem task)
        {
            // Reload all tasks when any task changes
            LoadTasks();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update current column based on which ListBox has selection
            if (sender == todoListBox && todoListBox.SelectedItem != null)
                currentColumn = 0;
            else if (sender == inProgressListBox && inProgressListBox.SelectedItem != null)
                currentColumn = 1;
            else if (sender == doneListBox && doneListBox.SelectedItem != null)
                currentColumn = 2;
        }

        private void ListBox_KeyDown(object sender, KeyEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem is TaskItem task)
            {
                // Enter to edit task
                if (e.Key == Key.Enter)
                {
                    EditTask(task);
                    e.Handled = true;
                }
                // Delete to remove task
                else if (e.Key == Key.Delete)
                {
                    taskService.DeleteTask(task.Id);
                    e.Handled = true;
                }
                // 1, 2, 3 to move between columns
                else if (e.Key == Key.D1 || e.Key == Key.NumPad1)
                {
                    MoveTaskToColumn(task, TaskStatus.Pending);
                    e.Handled = true;
                }
                else if (e.Key == Key.D2 || e.Key == Key.NumPad2)
                {
                    MoveTaskToColumn(task, TaskStatus.InProgress);
                    e.Handled = true;
                }
                else if (e.Key == Key.D3 || e.Key == Key.NumPad3)
                {
                    MoveTaskToColumn(task, TaskStatus.Completed);
                    e.Handled = true;
                }
                // P to cycle priority
                else if (e.Key == Key.P)
                {
                    taskService.CyclePriority(task.Id);
                    e.Handled = true;
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Arrow keys for column navigation
            if (e.Key == Key.Left && currentColumn > 0)
            {
                currentColumn--;
                CurrentListBox.Focus();
                if (CurrentListBox.Items.Count > 0)
                    CurrentListBox.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.Right && currentColumn < 2)
            {
                currentColumn++;
                CurrentListBox.Focus();
                if (CurrentListBox.Items.Count > 0)
                    CurrentListBox.SelectedIndex = 0;
                e.Handled = true;
            }
            // R to refresh
            else if (e.Key == Key.R)
            {
                LoadTasks();
                e.Handled = true;
            }
        }

        private void MoveTaskToColumn(TaskItem task, TaskStatus newStatus)
        {
            if (task.Status == newStatus)
                return;

            task.Status = newStatus;
            task.UpdatedAt = DateTime.Now;

            if (newStatus == TaskStatus.Completed)
                task.Progress = 100;
            else if (newStatus == TaskStatus.InProgress && task.Progress == 0)
                task.Progress = 50;
            else if (newStatus == TaskStatus.Pending)
                task.Progress = 0;

            taskService.UpdateTask(task);
            Logger.Instance?.Info("KanbanWidget", $"Moved task '{task.Title}' to {newStatus}");
        }

        private void EditTask(TaskItem task)
        {
            // Simple edit dialog
            try
            {
                var dialog = new Window
                {
                    Title = "Edit Task",
                    Width = 400,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush(theme.Background)
                };

                var stack = new StackPanel { Margin = new Thickness(20) };

                // Title
                var titleLabel = new TextBlock
                {
                    Text = "Title:",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(theme.Foreground),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stack.Children.Add(titleLabel);

                var titleBox = new TextBox
                {
                    Text = task.Title,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Background = new SolidColorBrush(theme.Surface),
                    Foreground = new SolidColorBrush(theme.Foreground),
                    BorderBrush = new SolidColorBrush(theme.Border),
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                stack.Children.Add(titleBox);

                // Description
                var descLabel = new TextBlock
                {
                    Text = "Description:",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(theme.Foreground),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stack.Children.Add(descLabel);

                var descBox = new TextBox
                {
                    Text = task.Description,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Background = new SolidColorBrush(theme.Surface),
                    Foreground = new SolidColorBrush(theme.Foreground),
                    BorderBrush = new SolidColorBrush(theme.Border),
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 0, 0, 10),
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    MinHeight = 80
                };
                stack.Children.Add(descBox);

                // Buttons
                var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

                var saveBtn = new Button
                {
                    Content = "Save",
                    FontFamily = new FontFamily("Consolas"),
                    Background = new SolidColorBrush(theme.Success),
                    Foreground = Brushes.White,
                    Padding = new Thickness(15, 5, 15, 5),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = Cursors.Hand
                };
                saveBtn.Click += (s, e) =>
                {
                    task.Title = titleBox.Text;
                    task.Description = descBox.Text;
                    task.UpdatedAt = DateTime.Now;
                    taskService.UpdateTask(task);
                    dialog.Close();
                };
                buttonStack.Children.Add(saveBtn);

                var cancelBtn = new Button
                {
                    Content = "Cancel",
                    FontFamily = new FontFamily("Consolas"),
                    Background = new SolidColorBrush(theme.ForegroundDisabled),
                    Foreground = Brushes.White,
                    Padding = new Thickness(15, 5, 15, 5),
                    Cursor = Cursors.Hand
                };
                cancelBtn.Click += (s, e) => dialog.Close();
                buttonStack.Children.Add(cancelBtn);

                stack.Children.Add(buttonStack);

                dialog.Content = stack;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("KanbanWidget", $"Failed to show edit dialog: {ex.Message}", ex);
            }
        }

        public override void OnWidgetFocusReceived()
        {
            CurrentListBox?.Focus();
            Keyboard.Focus(CurrentListBox);
        }

        public override Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            state["CurrentColumn"] = currentColumn;
            if (CurrentListBox?.SelectedItem is TaskItem task)
            {
                state["SelectedTaskId"] = task.Id.ToString();
            }
            return state;
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state == null) return;

            if (state.ContainsKey("CurrentColumn") && state["CurrentColumn"] is int col)
            {
                currentColumn = Math.Max(0, Math.Min(2, col));
            }

            if (state.ContainsKey("SelectedTaskId") && state["SelectedTaskId"] is string idStr &&
                Guid.TryParse(idStr, out var taskId))
            {
                // Try to find and select the task in the current column
                var task = taskService.GetTask(taskId);
                if (task != null)
                {
                    var targetListBox = CurrentListBox;
                    for (int i = 0; i < targetListBox.Items.Count; i++)
                    {
                        if (targetListBox.Items[i] is TaskItem item && item.Id == taskId)
                        {
                            targetListBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }

        protected override void OnDispose()
        {
            // Unsubscribe from events
            if (taskService != null)
            {
                taskService.TaskAdded -= OnTaskChanged;
                taskService.TaskUpdated -= OnTaskChanged;
                taskService.TaskDeleted -= (id) => LoadTasks();
                taskService.TasksReloaded -= LoadTasks;
            }

            // Stop timer
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer = null;
            }

            Logger.Instance?.Info("KanbanWidget", "Kanban board widget disposed");
            base.OnDispose();
        }

        public void ApplyTheme()
        {
            theme = ThemeManager.Instance.CurrentTheme;

            if (mainGrid != null)
            {
                mainGrid.Background = new SolidColorBrush(theme.Background);
            }

            // Refresh UI to apply new theme
            LoadTasks();

            Logger.Instance?.Debug("KanbanWidget", "Applied theme update");
        }
    }
}
