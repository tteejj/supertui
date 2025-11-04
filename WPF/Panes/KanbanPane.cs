// KanbanPane.cs - Kanban board for task visualization (Todo → In Progress → Done)
// Keyboard-driven card management with arrow keys to move between columns

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Extensions;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    public enum KanbanColumn
    {
        Todo,
        InProgress,
        Done
    }

    /// <summary>
    /// Kanban board pane - Visual task board with drag/keyboard movement
    /// </summary>
    public class KanbanPane : PaneBase
    {
        // Services
        private readonly ITaskService taskService;
        private readonly IEventBus eventBus;

        // UI Components
        private Grid mainGrid;
        private ListBox todoListBox;
        private ListBox inProgressListBox;
        private ListBox doneListBox;
        private TextBlock statusBar;

        // State
        private List<TaskItem> allTasks = new List<TaskItem>();
        private KanbanColumn currentColumn = KanbanColumn.Todo;
        private ListBox CurrentListBox => currentColumn == KanbanColumn.Todo ? todoListBox : (currentColumn == KanbanColumn.InProgress ? inProgressListBox : doneListBox);

        // Theme colors
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush successBrush;
        private SolidColorBrush surfaceBrush;
        private SolidColorBrush dimBrush;
        private SolidColorBrush borderBrush;

        public KanbanPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            ITaskService taskService,
            IEventBus eventBus)
            : base(logger, themeManager, projectContext)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            PaneName = "Kanban";
            PaneIcon = "📋";
        }

        public override void Initialize()
        {
            base.Initialize();
            CacheThemeColors();
            RegisterPaneShortcuts();
            LoadTasks();

            // Subscribe to task changes
            taskService.TaskAdded += (t) => LoadTasks();
            taskService.TaskUpdated += (t) => LoadTasks();
            taskService.TaskDeleted += (id) => LoadTasks();

            // Focus first column
            this.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.Input.Keyboard.Focus(todoListBox);
                if (todoListBox.Items.Count > 0)
                    todoListBox.SelectedIndex = 0;
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override UIElement BuildContent()
        {
            CacheThemeColors();

            mainGrid = new Grid(); // No background - let PaneBase border show through for focus indicator
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Columns
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status

            // Header
            var header = new TextBlock
            {
                Text = "📋 Kanban Board",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Columns grid
            var columnsGrid = new Grid { Margin = new Thickness(16, 0, 16, 0) };
            columnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Todo column
            var todoColumn = BuildColumn("TODO", out todoListBox);
            Grid.SetColumn(todoColumn, 0);
            columnsGrid.Children.Add(todoColumn);

            // In Progress column
            var inProgressColumn = BuildColumn("IN PROGRESS", out inProgressListBox);
            Grid.SetColumn(inProgressColumn, 1);
            columnsGrid.Children.Add(inProgressColumn);

            // Done column
            var doneColumn = BuildColumn("DONE", out doneListBox);
            Grid.SetColumn(doneColumn, 2);
            columnsGrid.Children.Add(doneColumn);

            Grid.SetRow(columnsGrid, 1);
            mainGrid.Children.Add(columnsGrid);

            // Status bar
            statusBar = new TextBlock
            {
                Text = "←→:Switch column | Shift+←→:Move task | ↑↓:Select | A:Add | D:Delete | Ctrl+R:Refresh",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = fgBrush,
                Background = bgBrush,
                Padding = new Thickness(16, 8, 16, 8),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(statusBar, 2);
            mainGrid.Children.Add(statusBar);

            return mainGrid;
        }

        private Border BuildColumn(string title, out ListBox listBox)
        {
            var column = new Grid { Margin = new Thickness(8) };
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            // Column title
            var titleBlock = new TextBlock
            {
                Text = title,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Background = bgBrush,
                Padding = new Thickness(12, 8, 12, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = TextAlignment.Center
            };
            Grid.SetRow(titleBlock, 0);
            column.Children.Add(titleBlock);

            // Task list
            listBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 13,
                Background = bgBrush,
                Foreground = fgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 8, 0, 0)
            };
            listBox.KeyDown += ListBox_KeyDown;
            listBox.GotFocus += ListBox_GotFocus;
            Grid.SetRow(listBox, 1);
            column.Children.Add(listBox);

            var border = new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Child = column
            };

            return border;
        }

        private void CacheThemeColors()
        {
            var theme = themeManager.CurrentTheme;
            bgBrush = new SolidColorBrush(theme.Background);
            fgBrush = new SolidColorBrush(theme.Foreground);
            accentBrush = new SolidColorBrush(theme.Primary);
            successBrush = new SolidColorBrush(theme.Success);
            surfaceBrush = new SolidColorBrush(theme.Surface);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
            borderBrush = new SolidColorBrush(theme.Border);
        }

        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;
            // Column navigation (without modifier)
            shortcuts.RegisterForPane(PaneName, Key.Left, ModifierKeys.None, () => SwitchColumnLeft(), "Switch to left column");
            shortcuts.RegisterForPane(PaneName, Key.Right, ModifierKeys.None, () => SwitchColumnRight(), "Switch to right column");
            // Task movement (with Shift)
            shortcuts.RegisterForPane(PaneName, Key.Left, ModifierKeys.Shift, () => MoveTaskLeft(), "Move task left");
            shortcuts.RegisterForPane(PaneName, Key.Right, ModifierKeys.Shift, () => MoveTaskRight(), "Move task right");
            // Other actions
            shortcuts.RegisterForPane(PaneName, Key.A, ModifierKeys.None, () => AddTask(), "Add new task");
            shortcuts.RegisterForPane(PaneName, Key.D, ModifierKeys.None, () => DeleteTask(), "Delete selected task");
            shortcuts.RegisterForPane(PaneName, Key.R, ModifierKeys.Control, () => LoadTasks(), "Refresh board");
        }

        private void ListBox_KeyDown(object sender, KeyEventArgs e)
        {
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
                e.Handled = true;
        }

        private void ListBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender == todoListBox) currentColumn = KanbanColumn.Todo;
            else if (sender == inProgressListBox) currentColumn = KanbanColumn.InProgress;
            else if (sender == doneListBox) currentColumn = KanbanColumn.Done;
        }

        private void LoadTasks()
        {
            try
            {
                allTasks = taskService.GetAllTasks().ToList();
                RefreshColumns();
                ShowStatus($"Loaded {allTasks.Count} tasks", false);
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(ErrorCategory.IO, ex, "Loading tasks for Kanban", logger);
                ShowStatus("Failed to load tasks", true);
            }
        }

        private void RefreshColumns()
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher
            this.Dispatcher.Invoke(() =>
            {
                // Todo: Not completed, not in progress
                var todoTasks = allTasks.Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.InProgress).ToList();
                RefreshColumn(todoListBox, todoTasks);

                // In Progress: Status is InProgress
                var inProgressTasks = allTasks.Where(t => t.Status == TaskStatus.InProgress).ToList();
                RefreshColumn(inProgressListBox, inProgressTasks);

                // Done: Completed
                var doneTasks = allTasks.Where(t => t.Status == TaskStatus.Completed).ToList();
                RefreshColumn(doneListBox, doneTasks);
            });
        }

        private void RefreshColumn(ListBox listBox, List<TaskItem> tasks)
        {
            listBox.Items.Clear();
            foreach (var task in tasks.OrderBy(t => t.Priority).ThenBy(t => t.DueDate))
            {
                var card = CreateTaskCard(task);
                listBox.Items.Add(card);
            }
        }

        private Border CreateTaskCard(TaskItem task)
        {
            var priorityColor = task.Priority == TaskPriority.High ? new SolidColorBrush(themeManager.CurrentTheme.Error) :
                               task.Priority == TaskPriority.Medium ? new SolidColorBrush(themeManager.CurrentTheme.Warning) :
                               dimBrush;

            var cardText = $"[{task.Priority}] {task.Title}";
            if (task.DueDate.HasValue)
                cardText += $"\n📅 {task.DueDate.Value:MM/dd}";
            if (task.Tags?.Count > 0)
                cardText += $"\n🏷️ {string.Join(", ", task.Tags)}";

            var textBlock = new TextBlock
            {
                Text = cardText,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = fgBrush,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap
            };

            var border = new Border
            {
                BorderBrush = priorityColor,
                BorderThickness = new Thickness(2, 0, 0, 0),
                Background = bgBrush,
                Margin = new Thickness(2),
                Child = textBlock,
                Tag = task // Store task reference
            };

            return border;
        }

        private void SwitchColumnRight()
        {
            if (currentColumn == KanbanColumn.Done)
            {
                ShowStatus("Already at rightmost column", false);
                return;
            }

            var newColumn = currentColumn + 1;
            SwitchToColumn(newColumn);
        }

        private void SwitchColumnLeft()
        {
            if (currentColumn == KanbanColumn.Todo)
            {
                ShowStatus("Already at leftmost column", false);
                return;
            }

            var newColumn = currentColumn - 1;
            SwitchToColumn(newColumn);
        }

        private void SwitchToColumn(KanbanColumn targetColumn)
        {
            currentColumn = targetColumn;
            var targetListBox = CurrentListBox;
            System.Windows.Input.Keyboard.Focus(targetListBox);

            // Select first item if available, otherwise clear selection
            if (targetListBox.Items.Count > 0)
                targetListBox.SelectedIndex = 0;
            else
                targetListBox.SelectedIndex = -1;

            ShowStatus($"Switched to {currentColumn} column", false);
        }

        private void MoveTaskRight()
        {
            if (currentColumn == KanbanColumn.Done)
            {
                ShowStatus("Task already in Done column", false);
                return;
            }
            MoveTask(1);
        }

        private void MoveTaskLeft()
        {
            if (currentColumn == KanbanColumn.Todo)
            {
                ShowStatus("Task already in Todo column", false);
                return;
            }
            MoveTask(-1);
        }

        private void MoveTask(int direction)
        {
            var listBox = CurrentListBox;
            if (listBox.SelectedItem is Border border && border.Tag is TaskItem task)
            {
                var newColumn = (KanbanColumn)((int)currentColumn + direction);

                // Update task status
                if (newColumn == KanbanColumn.InProgress)
                    task.Status = TaskStatus.InProgress;
                else if (newColumn == KanbanColumn.Done)
                {
                    task.Status = TaskStatus.Completed;
                    task.CompletedDate = DateTime.Now;
                }
                else // Moving back to Todo
                {
                    task.Status = TaskStatus.Pending;
                    task.CompletedDate = null;
                }

                taskService.UpdateTask(task);
                RefreshColumns();

                // Update current column and focus the new column
                currentColumn = newColumn;
                var targetListBox = CurrentListBox;
                System.Windows.Input.Keyboard.Focus(targetListBox);
                if (targetListBox.Items.Count > 0)
                {
                    targetListBox.SelectedIndex = 0; // Select first item in new column
                }

                ShowStatus($"Moved '{task.Title}' to {currentColumn}", false);
            }
            else
            {
                ShowStatus("No task selected", false);
            }
        }

        private void AddTask()
        {
            var dialog = new Window
            {
                Title = "Add Task",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = bgBrush,
                Owner = Window.GetWindow(this)
            };

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                Background = bgBrush,
                Foreground = fgBrush,
                CaretBrush = fgBrush,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 12)
            };
            titleBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) dialog.DialogResult = true;
                else if (e.Key == Key.Escape) dialog.DialogResult = false;
            };
            titleBox.ApplyFocusStyling(themeManager);
            Grid.SetRow(titleBox, 0);
            grid.Children.Add(titleBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var saveButton = new Button { Content = "Add", Width = 80, Margin = new Thickness(0, 0, 8, 0), Background = accentBrush, Foreground = bgBrush };
            saveButton.Click += (s, e) => dialog.DialogResult = true;
            var cancelButton = new Button { Content = "Cancel", Width = 80, Background = bgBrush, Foreground = fgBrush };
            cancelButton.Click += (s, e) => dialog.DialogResult = false;
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            dialog.Loaded += (s, e) => System.Windows.Input.Keyboard.Focus(titleBox);

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(titleBox.Text))
            {
                var task = new TaskItem
                {
                    Title = titleBox.Text.Trim(),
                    Status = TaskStatus.Pending,
                    Priority = TaskPriority.Medium,
                    CreatedAt = DateTime.Now
                };

                taskService.AddTask(task);
                ShowStatus($"Added: {titleBox.Text.Trim()}", false);
            }
        }

        private void DeleteTask()
        {
            var listBox = CurrentListBox;
            if (listBox.SelectedItem is Border border && border.Tag is TaskItem task)
            {
                var result = MessageBox.Show($"Delete task '{task.Title}'?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    taskService.DeleteTask(task.Id);
                    ShowStatus($"Deleted: {task.Title}", false);
                }
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            this.Dispatcher.Invoke(() =>
            {
                statusBar.Text = message + " | ←→:Switch column | Shift+←→:Move task | A:Add | D:Delete";
            });
        }

        protected override void OnDispose()
        {
            // Event handlers are lambda-based, no need to unsubscribe
            base.OnDispose();
        }
    }
}
