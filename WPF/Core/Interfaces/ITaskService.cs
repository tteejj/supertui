using System;
using System.Collections.Generic;
using SuperTUI.Core.Models;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for task management service
    /// Matches actual TaskService implementation
    /// </summary>
    public interface ITaskService
    {
        // Events
        event Action<TaskItem> TaskAdded;
        event Action<TaskItem> TaskUpdated;
        event Action<Guid> TaskDeleted;
        event Action<TaskItem> TaskRestored;
        event Action TasksReloaded;

        // Initialization
        void Initialize(string filePath = null);

        // Task retrieval
        List<TaskItem> GetAllTasks(bool includeDeleted = false);
        List<TaskItem> GetTasks(Func<TaskItem, bool> predicate);
        TaskItem GetTask(Guid id);
        List<TaskItem> GetTasksForProject(Guid projectId);
        List<TaskItem> GetSubtasks(Guid parentId);
        List<Guid> GetAllSubtasksRecursive(Guid parentId);
        bool HasSubtasks(Guid taskId);
        List<TaskItem> GetDependencies(Guid taskId);
        List<TaskItem> GetBlockedTasks(Guid taskId);
        ProjectTaskStats GetProjectStats(Guid projectId);
        int GetTaskCount(Func<TaskItem, bool> predicate = null);

        // Task manipulation
        TaskItem AddTask(TaskItem task);
        bool UpdateTask(TaskItem task);
        bool DeleteTask(Guid id, bool hardDelete = false);
        void RestoreTask(TaskItem task);
        bool ToggleTaskCompletion(Guid id);
        bool CyclePriority(Guid id);
        void MoveTaskUp(Guid taskId);
        void MoveTaskDown(Guid taskId);
        void NormalizeSortOrders(Guid? parentTaskId = null);

        // Dependencies
        bool AddDependency(Guid taskId, Guid dependsOnTaskId);
        bool RemoveDependency(Guid taskId, Guid dependsOnTaskId);

        // Notes
        TaskNote AddNote(Guid taskId, string content);
        bool RemoveNote(Guid taskId, Guid noteId);

        // Bulk operations
        void Reload();
        void Clear();
        void ProcessRecurringTasks();

        // Export
        bool ExportToCSV(string filePath);
        bool ExportToJson(string filePath);
        bool ExportToMarkdown(string filePath);
    }
}
