using System;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;

namespace SuperTUI.Core.ViewModels
{
    /// <summary>
    /// View model wrapper for TaskItem to handle display concerns
    /// Separates domain model from UI concerns
    /// </summary>
    public class TaskViewModel
    {
        private readonly TaskService taskService;

        public TaskItem Task { get; private set; }
        public bool IsExpanded { get; set; }

        public TaskViewModel(TaskItem task, TaskService service)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            taskService = service ?? throw new ArgumentNullException(nameof(service));
            IsExpanded = false;
        }

        /// <summary>
        /// Update the wrapped task (call after service updates)
        /// </summary>
        public void UpdateTask(TaskItem updatedTask)
        {
            Task = updatedTask ?? throw new ArgumentNullException(nameof(updatedTask));
        }

        /// <summary>
        /// Get formatted display string for ListBox
        /// </summary>
        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();

            // Indentation for subtasks
            if (Task.IsSubtask)
                parts.Add("  ");

            // Status icon
            parts.Add(Task.StatusIcon);

            // Priority icon
            var priorityChar = Task.Priority == TaskPriority.High ? "!" :
                              Task.Priority == TaskPriority.Medium ? "●" : "·";
            parts.Add(priorityChar);

            // Title (with visual indicator for completed)
            var title = Task.Title;
            if (Task.Status == TaskStatus.Completed)
                title = $"[✓] {title}";
            parts.Add(title);

            // Due date badge
            if (Task.DueDate.HasValue)
            {
                var dueText = Task.IsOverdue ? $"[OVERDUE {Task.DueDate.Value:MMM dd}]" :
                             Task.IsDueToday ? "[TODAY]" :
                             $"[{Task.DueDate.Value:MMM dd}]";
                parts.Add(dueText);
            }

            // Subtask indicator (only for parent tasks)
            if (!Task.IsSubtask && taskService.HasSubtasks(Task.Id))
            {
                var subtaskCount = taskService.GetSubtasks(Task.Id).Count;
                var expandIcon = IsExpanded ? "▼" : "▶";
                parts.Add($"{expandIcon}({subtaskCount})");
            }

            return string.Join(" ", parts);
        }
    }
}
