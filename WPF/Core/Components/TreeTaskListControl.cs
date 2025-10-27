using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Tree-based task list control with hierarchical display
    /// Supports expand/collapse, subtask creation, and visual tree lines
    /// </summary>
    public class TreeTaskListControl : UserControl
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;

        private ListBox taskListBox;
        private ObservableCollection<TreeTaskItem> flattenedTasks;

        public event Action<TaskItem> TaskSelected;
        public event Action<TaskItem> TaskActivated; // Double-click or Enter
        public event Action<TaskItem> CreateSubtask;
        public event Action<TaskItem> DeleteTask;
        public event Action<TreeTaskItem> ToggleExpanded;

        public TreeTaskListControl(ILogger logger, IThemeManager themeManager)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

            flattenedTasks = new ObservableCollection<TreeTaskItem>();
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            taskListBox = new ListBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            taskListBox.SelectionChanged += TaskListBox_SelectionChanged;
            taskListBox.MouseDoubleClick += TaskListBox_MouseDoubleClick;
            taskListBox.KeyDown += TaskListBox_KeyDown;

            taskListBox.ItemsSource = flattenedTasks;

            this.Content = taskListBox;
        }

        public void LoadTasks(List<TaskItem> tasks)
        {
            // Build tree structure
            var tree = BuildTree(tasks);

            // Flatten tree for display
            flattenedTasks.Clear();
            FlattenTree(tree, flattenedTasks, 0);

            logger?.Info("TreeTaskList", $"Loaded {flattenedTasks.Count} tasks (hierarchical)");
        }

        private List<TreeTaskItem> BuildTree(List<TaskItem> allTasks)
        {
            var tree = new List<TreeTaskItem>();
            var taskDict = allTasks.ToDictionary(t => t.Id, t => new TreeTaskItem(t));

            // Build parent-child relationships
            foreach (var task in allTasks)
            {
                if (task.ParentTaskId.HasValue && taskDict.ContainsKey(task.ParentTaskId.Value))
                {
                    var parent = taskDict[task.ParentTaskId.Value];
                    parent.Children.Add(taskDict[task.Id]);
                }
                else if (!task.ParentTaskId.HasValue)
                {
                    // Root-level task
                    tree.Add(taskDict[task.Id]);
                }
            }

            // Sort at each level by SortOrder
            SortTree(tree);

            return tree;
        }

        private void SortTree(List<TreeTaskItem> items)
        {
            items.Sort((a, b) => a.Task.SortOrder.CompareTo(b.Task.SortOrder));

            foreach (var item in items)
            {
                if (item.Children.Count > 0)
                    SortTree(item.Children);
            }
        }

        private void FlattenTree(List<TreeTaskItem> items, ObservableCollection<TreeTaskItem> flat, int level)
        {
            foreach (var item in items)
            {
                item.IndentLevel = level;
                item.UpdateTreePrefix();
                flat.Add(item);

                if (item.Task.IsExpanded && item.Children.Count > 0)
                {
                    FlattenTree(item.Children, flat, level + 1);
                }
            }
        }

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = taskListBox.SelectedItem as TreeTaskItem;
            if (selectedItem != null)
            {
                TaskSelected?.Invoke(selectedItem.Task);
            }
        }

        private void TaskListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedItem = taskListBox.SelectedItem as TreeTaskItem;
            if (selectedItem != null)
            {
                TaskActivated?.Invoke(selectedItem.Task);
            }
        }

        private void TaskListBox_KeyDown(object sender, KeyEventArgs e)
        {
            var selectedItem = taskListBox.SelectedItem as TreeTaskItem;
            if (selectedItem == null) return;

            var isCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (e.Key == Key.C && !isCtrl)
            {
                // Toggle expand/collapse
                ToggleExpandCollapse(selectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.G && !isCtrl)
            {
                // Global collapse/expand all
                ToggleCollapseAll();
                e.Handled = true;
            }
            else if (e.Key == Key.S && !isCtrl)
            {
                // Create subtask
                CreateSubtask?.Invoke(selectedItem.Task);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                // Delete task
                DeleteTask?.Invoke(selectedItem.Task);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                // Activate/edit task
                TaskActivated?.Invoke(selectedItem.Task);
                e.Handled = true;
            }
        }

        private void ToggleExpandCollapse(TreeTaskItem item)
        {
            if (item.Children.Count == 0)
                return;

            item.Task.IsExpanded = !item.Task.IsExpanded;

            // Rebuild flattened list
            var currentIndex = flattenedTasks.IndexOf(item);
            var savedSelection = taskListBox.SelectedIndex;

            var allTasks = flattenedTasks.Select(ti => ti.Task).ToList();
            LoadTasks(allTasks);

            // Restore selection
            if (currentIndex >= 0 && currentIndex < flattenedTasks.Count)
                taskListBox.SelectedIndex = currentIndex;

            ToggleExpanded?.Invoke(item);
        }

        private void ToggleCollapseAll()
        {
            bool allExpanded = flattenedTasks.All(ti => ti.Task.IsExpanded || ti.Children.Count == 0);

            foreach (var item in flattenedTasks)
            {
                if (item.Children.Count > 0)
                {
                    item.Task.IsExpanded = !allExpanded;
                }
            }

            // Rebuild flattened list
            var allTasks = flattenedTasks.Select(ti => ti.Task).ToList();
            LoadTasks(allTasks);
        }

        public TaskItem GetSelectedTask()
        {
            return (taskListBox.SelectedItem as TreeTaskItem)?.Task;
        }

        public void SelectTask(Guid taskId)
        {
            var item = flattenedTasks.FirstOrDefault(ti => ti.Task.Id == taskId);
            if (item != null)
            {
                taskListBox.SelectedItem = item;
                taskListBox.ScrollIntoView(item);
            }
        }

        public void RefreshDisplay()
        {
            var allTasks = flattenedTasks.Select(ti => ti.Task).ToList();
            var selectedTaskId = GetSelectedTask()?.Id;

            LoadTasks(allTasks);

            if (selectedTaskId.HasValue)
                SelectTask(selectedTaskId.Value);
        }
    }

    /// <summary>
    /// Tree item wrapper for TaskItem with display properties
    /// </summary>
    public class TreeTaskItem
    {
        public TaskItem Task { get; set; }
        public List<TreeTaskItem> Children { get; set; }
        public int IndentLevel { get; set; }
        public string TreePrefix { get; set; }
        public string ExpandIcon { get; set; }

        public TreeTaskItem(TaskItem task)
        {
            Task = task;
            Children = new List<TreeTaskItem>();
            IndentLevel = 0;
            UpdateTreePrefix();
        }

        public void UpdateTreePrefix()
        {
            // Calculate tree prefix based on indent level
            if (IndentLevel == 0)
            {
                TreePrefix = "";
            }
            else
            {
                // Build indentation string
                var indent = new string(' ', (IndentLevel - 1) * 2);
                TreePrefix = indent + "└─ ";
            }

            // Update expand icon
            if (Children.Count > 0)
            {
                ExpandIcon = Task.IsExpanded ? "▼" : "▶";
            }
            else
            {
                ExpandIcon = "  ";
            }
        }

        public override string ToString()
        {
            var parts = new List<string>();

            // Tree structure
            parts.Add(TreePrefix);
            parts.Add(ExpandIcon);

            // Status icon
            parts.Add(Task.StatusIcon);

            // Priority
            var priorityChar = Task.Priority == TaskPriority.High ? "!" :
                              Task.Priority == TaskPriority.Medium ? "●" : "·";
            parts.Add(priorityChar);

            // Title
            var title = Task.Title;
            if (Task.Status == TaskStatus.Completed)
                title = $"[✓] {title}";
            parts.Add(title);

            // Due date
            if (Task.DueDate.HasValue)
            {
                var dueText = Task.IsOverdue ? $"[OVERDUE {Task.DueDate.Value:MMM dd}]" :
                             Task.IsDueToday ? "[TODAY]" :
                             $"[{Task.DueDate.Value:MMM dd}]";
                parts.Add(dueText);
            }

            // Subtask count
            if (Children.Count > 0)
            {
                parts.Add($"({Children.Count})");
            }

            return string.Join(" ", parts);
        }
    }
}
