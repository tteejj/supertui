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
    /// Agenda widget showing tasks grouped by due date timeframes
    /// Expandable sections: OVERDUE, TODAY, TOMORROW, THIS WEEK, LATER, NO DUE DATE
    /// </summary>
    public class AgendaWidget : WidgetBase, IThemeable
    {
        private Theme theme;
        private TaskService taskService;

        // UI Components
        private StackPanel mainPanel;
        private ScrollViewer scrollViewer;

        // Expander controls for each time group
        private Expander overdueExpander;
        private Expander todayExpander;
        private Expander tomorrowExpander;
        private Expander thisWeekExpander;
        private Expander laterExpander;
        private Expander noDueDateExpander;

        // ListBoxes for each group
        private ListBox overdueListBox;
        private ListBox todayListBox;
        private ListBox tomorrowListBox;
        private ListBox thisWeekListBox;
        private ListBox laterListBox;
        private ListBox noDueDateListBox;

        // Data collections
        private ObservableCollection<TaskItem> overdueTasks;
        private ObservableCollection<TaskItem> todayTasks;
        private ObservableCollection<TaskItem> tomorrowTasks;
        private ObservableCollection<TaskItem> thisWeekTasks;
        private ObservableCollection<TaskItem> laterTasks;
        private ObservableCollection<TaskItem> noDueDateTasks;

        // Refresh timer
        private DispatcherTimer refreshTimer;

        public AgendaWidget()
        {
            WidgetName = "Agenda";
            WidgetType = "Agenda";
        }

        public override void Initialize()
        {
            theme = ThemeManager.Instance.CurrentTheme;
            taskService = TaskService.Instance;

            // Initialize service
            taskService.Initialize();

            // Initialize collections
            overdueTasks = new ObservableCollection<TaskItem>();
            todayTasks = new ObservableCollection<TaskItem>();
            tomorrowTasks = new ObservableCollection<TaskItem>();
            thisWeekTasks = new ObservableCollection<TaskItem>();
            laterTasks = new ObservableCollection<TaskItem>();
            noDueDateTasks = new ObservableCollection<TaskItem>();

            BuildUI();
            LoadTasks();

            // Subscribe to task service events
            taskService.TaskAdded += OnTaskChanged;
            taskService.TaskUpdated += OnTaskChanged;
            taskService.TaskDeleted += (id) => LoadTasks();
            taskService.TasksReloaded += LoadTasks;

            // Setup refresh timer (every 30 seconds)
            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            refreshTimer.Tick += (s, e) => LoadTasks();
            refreshTimer.Start();

            Logger.Instance?.Info("AgendaWidget", "Agenda widget initialized");
        }

        private void BuildUI()
        {
            mainPanel = new StackPanel
            {
                Background = new SolidColorBrush(theme.Background),
                Margin = new Thickness(10)
            };

            // Title
            var title = new TextBlock
            {
                Text = "AGENDA",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 15)
            };
            mainPanel.Children.Add(title);

            // Build each time group
            overdueExpander = BuildTimeGroup("OVERDUE", overdueTasks, out overdueListBox, theme.Error);
            todayExpander = BuildTimeGroup("TODAY", todayTasks, out todayListBox, theme.Warning);
            tomorrowExpander = BuildTimeGroup("TOMORROW", tomorrowTasks, out tomorrowListBox, theme.Info);
            thisWeekExpander = BuildTimeGroup("THIS WEEK", thisWeekTasks, out thisWeekListBox, theme.Foreground);
            laterExpander = BuildTimeGroup("LATER", laterTasks, out laterListBox, theme.ForegroundSecondary);
            noDueDateExpander = BuildTimeGroup("NO DUE DATE", noDueDateTasks, out noDueDateListBox, theme.ForegroundDisabled);

            // All expanders start expanded except LATER and NO DUE DATE
            overdueExpander.IsExpanded = true;
            todayExpander.IsExpanded = true;
            tomorrowExpander.IsExpanded = true;
            thisWeekExpander.IsExpanded = true;
            laterExpander.IsExpanded = false;
            noDueDateExpander.IsExpanded = false;

            mainPanel.Children.Add(overdueExpander);
            mainPanel.Children.Add(todayExpander);
            mainPanel.Children.Add(tomorrowExpander);
            mainPanel.Children.Add(thisWeekExpander);
            mainPanel.Children.Add(laterExpander);
            mainPanel.Children.Add(noDueDateExpander);

            scrollViewer = new ScrollViewer
            {
                Content = mainPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            this.Content = scrollViewer;
        }

        private Expander BuildTimeGroup(string title, ObservableCollection<TaskItem> dataSource, out ListBox listBox, System.Windows.Media.Color headerColor)
        {
            var expander = new Expander
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Header
            var header = new TextBlock
            {
                Text = $"{title} (0)",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(headerColor),
                Tag = title // Store original title for updating
            };
            expander.Header = header;

            // Content ListBox
            listBox = new ListBox
            {
                ItemsSource = dataSource,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Padding = new Thickness(5),
                SelectionMode = SelectionMode.Single,
                MaxHeight = 300,
                Margin = new Thickness(20, 5, 0, 5)
            };

            // Custom item template
            var itemTemplate = new DataTemplate();
            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackFactory.SetValue(StackPanel.MarginProperty, new Thickness(0, 2, 0, 2));

            // Status icon
            var statusFactory = new FrameworkElementFactory(typeof(TextBlock));
            statusFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("StatusIcon"));
            statusFactory.SetValue(TextBlock.WidthProperty, 20.0);
            statusFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            stackFactory.AppendChild(statusFactory);

            // Priority icon
            var priorityFactory = new FrameworkElementFactory(typeof(TextBlock));
            priorityFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("PriorityIcon"));
            priorityFactory.SetValue(TextBlock.WidthProperty, 20.0);
            priorityFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            stackFactory.AppendChild(priorityFactory);

            // Title
            var titleFactory = new FrameworkElementFactory(typeof(TextBlock));
            titleFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title"));
            titleFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            titleFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            titleFactory.SetValue(TextBlock.MarginProperty, new Thickness(5, 0, 10, 0));
            stackFactory.AppendChild(titleFactory);

            // Project name (if available)
            var projectFactory = new FrameworkElementFactory(typeof(TextBlock));
            projectFactory.SetValue(TextBlock.TextProperty, ""); // Placeholder for project name
            projectFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            projectFactory.SetValue(TextBlock.OpacityProperty, 0.7);
            projectFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 10, 0));
            stackFactory.AppendChild(projectFactory);

            // Due date
            var dueDateFactory = new FrameworkElementFactory(typeof(TextBlock));
            dueDateFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DueDate") { StringFormat = "yyyy-MM-dd", TargetNullValue = "" });
            dueDateFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            dueDateFactory.SetValue(TextBlock.OpacityProperty, 0.7);
            stackFactory.AppendChild(dueDateFactory);

            itemTemplate.VisualTree = stackFactory;
            listBox.ItemTemplate = itemTemplate;

            // Event handlers
            listBox.KeyDown += ListBox_KeyDown;
            expander.PreviewKeyDown += Expander_PreviewKeyDown;

            expander.Content = listBox;
            return expander;
        }

        private void LoadTasks()
        {
            var allTasks = taskService.GetAllTasks()
                .Where(t => !t.IsSubtask && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled)
                .ToList();

            // Clear collections
            overdueTasks.Clear();
            todayTasks.Clear();
            tomorrowTasks.Clear();
            thisWeekTasks.Clear();
            laterTasks.Clear();
            noDueDateTasks.Clear();

            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);
            var endOfWeek = today.AddDays(7 - (int)today.DayOfWeek);

            // Group tasks by timeframe
            foreach (var task in allTasks)
            {
                if (!task.DueDate.HasValue)
                {
                    noDueDateTasks.Add(task);
                }
                else
                {
                    var dueDate = task.DueDate.Value.Date;

                    if (dueDate < today)
                    {
                        overdueTasks.Add(task);
                    }
                    else if (dueDate == today)
                    {
                        todayTasks.Add(task);
                    }
                    else if (dueDate == tomorrow)
                    {
                        tomorrowTasks.Add(task);
                    }
                    else if (dueDate > tomorrow && dueDate <= endOfWeek)
                    {
                        thisWeekTasks.Add(task);
                    }
                    else
                    {
                        laterTasks.Add(task);
                    }
                }
            }

            // Update headers with counts
            UpdateHeader(overdueExpander, "OVERDUE", overdueTasks.Count);
            UpdateHeader(todayExpander, "TODAY", todayTasks.Count);
            UpdateHeader(tomorrowExpander, "TOMORROW", tomorrowTasks.Count);
            UpdateHeader(thisWeekExpander, "THIS WEEK", thisWeekTasks.Count);
            UpdateHeader(laterExpander, "LATER", laterTasks.Count);
            UpdateHeader(noDueDateExpander, "NO DUE DATE", noDueDateTasks.Count);

            Logger.Instance?.Debug("AgendaWidget", $"Loaded {allTasks.Count} tasks");
        }

        private void UpdateHeader(Expander expander, string title, int count)
        {
            if (expander?.Header is TextBlock header)
            {
                header.Text = $"{title} ({count})";
            }
        }

        private void OnTaskChanged(TaskItem task)
        {
            // Reload all tasks when any task changes
            LoadTasks();
        }

        private void Expander_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                var expander = sender as Expander;
                if (expander != null)
                {
                    expander.IsExpanded = !expander.IsExpanded;
                    e.Handled = true;
                }
            }
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
                // D to mark done
                else if (e.Key == Key.D)
                {
                    task.Status = TaskStatus.Completed;
                    task.Progress = 100;
                    task.UpdatedAt = DateTime.Now;
                    taskService.UpdateTask(task);
                    e.Handled = true;
                }
                // Delete to remove task
                else if (e.Key == Key.Delete)
                {
                    taskService.DeleteTask(task.Id);
                    e.Handled = true;
                }
                // P to cycle priority
                else if (e.Key == Key.P)
                {
                    taskService.CyclePriority(task.Id);
                    e.Handled = true;
                }
                // I to mark in progress
                else if (e.Key == Key.I)
                {
                    task.Status = TaskStatus.InProgress;
                    if (task.Progress == 0)
                        task.Progress = 50;
                    task.UpdatedAt = DateTime.Now;
                    taskService.UpdateTask(task);
                    e.Handled = true;
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // R to refresh
            if (e.Key == Key.R)
            {
                LoadTasks();
                e.Handled = true;
            }
            // Ctrl+E to expand/collapse all
            else if (e.Key == Key.E && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                bool allExpanded = overdueExpander.IsExpanded && todayExpander.IsExpanded &&
                                  tomorrowExpander.IsExpanded && thisWeekExpander.IsExpanded &&
                                  laterExpander.IsExpanded && noDueDateExpander.IsExpanded;

                bool newState = !allExpanded;
                overdueExpander.IsExpanded = newState;
                todayExpander.IsExpanded = newState;
                tomorrowExpander.IsExpanded = newState;
                thisWeekExpander.IsExpanded = newState;
                laterExpander.IsExpanded = newState;
                noDueDateExpander.IsExpanded = newState;

                e.Handled = true;
            }
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
                    Height = 350,
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

                // Due Date
                var dueDateLabel = new TextBlock
                {
                    Text = "Due Date:",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(theme.Foreground),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stack.Children.Add(dueDateLabel);

                var dueDatePicker = new DatePicker
                {
                    SelectedDate = task.DueDate,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Background = new SolidColorBrush(theme.Surface),
                    Foreground = new SolidColorBrush(theme.Foreground),
                    BorderBrush = new SolidColorBrush(theme.Border),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                stack.Children.Add(dueDatePicker);

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
                    task.DueDate = dueDatePicker.SelectedDate;
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
                Logger.Instance?.Error("AgendaWidget", $"Failed to show edit dialog: {ex.Message}", ex);
            }
        }

        public override void OnWidgetFocusReceived()
        {
            // Focus the first non-empty list box
            var firstNonEmptyListBox = new[] { overdueListBox, todayListBox, tomorrowListBox, thisWeekListBox, laterListBox, noDueDateListBox }
                .FirstOrDefault(lb => lb.Items.Count > 0);

            if (firstNonEmptyListBox != null)
            {
                firstNonEmptyListBox.Focus();
                Keyboard.Focus(firstNonEmptyListBox);
                if (firstNonEmptyListBox.Items.Count > 0)
                    firstNonEmptyListBox.SelectedIndex = 0;
            }
        }

        public override Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            state["OverdueExpanded"] = overdueExpander?.IsExpanded ?? true;
            state["TodayExpanded"] = todayExpander?.IsExpanded ?? true;
            state["TomorrowExpanded"] = tomorrowExpander?.IsExpanded ?? true;
            state["ThisWeekExpanded"] = thisWeekExpander?.IsExpanded ?? true;
            state["LaterExpanded"] = laterExpander?.IsExpanded ?? false;
            state["NoDueDateExpanded"] = noDueDateExpander?.IsExpanded ?? false;
            return state;
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state == null) return;

            if (state.ContainsKey("OverdueExpanded") && state["OverdueExpanded"] is bool overdueExp)
                overdueExpander.IsExpanded = overdueExp;
            if (state.ContainsKey("TodayExpanded") && state["TodayExpanded"] is bool todayExp)
                todayExpander.IsExpanded = todayExp;
            if (state.ContainsKey("TomorrowExpanded") && state["TomorrowExpanded"] is bool tomorrowExp)
                tomorrowExpander.IsExpanded = tomorrowExp;
            if (state.ContainsKey("ThisWeekExpanded") && state["ThisWeekExpanded"] is bool thisWeekExp)
                thisWeekExpander.IsExpanded = thisWeekExp;
            if (state.ContainsKey("LaterExpanded") && state["LaterExpanded"] is bool laterExp)
                laterExpander.IsExpanded = laterExp;
            if (state.ContainsKey("NoDueDateExpanded") && state["NoDueDateExpanded"] is bool noDueDateExp)
                noDueDateExpander.IsExpanded = noDueDateExp;
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

            Logger.Instance?.Info("AgendaWidget", "Agenda widget disposed");
            base.OnDispose();
        }

        public void ApplyTheme()
        {
            theme = ThemeManager.Instance.CurrentTheme;

            if (mainPanel != null)
            {
                mainPanel.Background = new SolidColorBrush(theme.Background);
            }

            // Refresh UI to apply new theme
            LoadTasks();

            Logger.Instance?.Debug("AgendaWidget", "Applied theme update");
        }
    }
}
