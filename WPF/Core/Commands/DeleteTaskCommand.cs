using System;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Commands
{
    /// <summary>
    /// Command to delete a task with undo support
    /// Captures full task state for restoration
    /// </summary>
    public class DeleteTaskCommand : ICommand
    {
        private readonly ITaskService taskService;
        private readonly TaskItem taskSnapshot;
        private readonly Guid taskId;

        public string Description { get; }
        public DateTime ExecutedAt { get; private set; }

        public DeleteTaskCommand(ITaskService taskService, TaskItem task)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            this.taskId = task.Id;

            // Deep copy task for restoration
            this.taskSnapshot = new TaskItem
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Priority = task.Priority,
                Progress = task.Progress,
                DueDate = task.DueDate,
                CompletedDate = task.CompletedDate,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                Deleted = task.Deleted,
                ProjectId = task.ProjectId,
                ParentTaskId = task.ParentTaskId,
                SortOrder = task.SortOrder,
                Tags = task.Tags != null ? new System.Collections.Generic.List<string>(task.Tags) : new System.Collections.Generic.List<string>(),
                DependsOn = task.DependsOn != null ? new System.Collections.Generic.List<Guid>(task.DependsOn) : new System.Collections.Generic.List<Guid>(),
                Notes = task.Notes != null ? new System.Collections.Generic.List<TaskNote>(task.Notes) : new System.Collections.Generic.List<TaskNote>(),
                ExternalId1 = task.ExternalId1,
                ExternalId2 = task.ExternalId2,
                Recurrence = task.Recurrence,
                RecurrenceInterval = task.RecurrenceInterval,
                RecurrenceEndDate = task.RecurrenceEndDate,
                LastRecurrence = task.LastRecurrence
            };

            Description = $"Delete task: {task.Title}";
        }

        public void Execute()
        {
            taskService.DeleteTask(taskId, hardDelete: false);
            ExecutedAt = DateTime.Now;
        }

        public void Undo()
        {
            taskService.RestoreTask(taskSnapshot);
        }
    }
}
