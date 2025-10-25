using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Task service for managing tasks with JSON persistence
    /// Thread-safe singleton service
    /// </summary>
    public class TaskService
    {
        private static TaskService instance;
        public static TaskService Instance => instance ??= new TaskService();

        private Dictionary<Guid, TaskItem> tasks;
        private Dictionary<Guid, List<Guid>> subtaskIndex; // ParentId -> List of ChildIds
        private string dataFilePath;
        private readonly object lockObject = new object();

        // Events for task changes
        public event Action<TaskItem> TaskAdded;
        public event Action<TaskItem> TaskUpdated;
        public event Action<Guid> TaskDeleted;
        public event Action TasksReloaded;

        private TaskService()
        {
            tasks = new Dictionary<Guid, TaskItem>();
            subtaskIndex = new Dictionary<Guid, List<Guid>>();
        }

        /// <summary>
        /// Initialize service with data file path
        /// </summary>
        public void Initialize(string filePath = null)
        {
            dataFilePath = filePath ?? Path.Combine(
                Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(),
                "tasks.json");

            Logger.Instance?.Info("TaskService", $"Initializing with data file: {dataFilePath}");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(dataFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Logger.Instance?.Info("TaskService", $"Created data directory: {directory}");
            }

            // Load existing tasks
            LoadFromFile();
        }

        /// <summary>
        /// Get all tasks (excluding deleted, unless includeDeleted is true)
        /// </summary>
        public List<TaskItem> GetAllTasks(bool includeDeleted = false)
        {
            lock (lockObject)
            {
                return tasks.Values
                    .Where(t => includeDeleted || !t.Deleted)
                    .OrderBy(t => t.SortOrder)
                    .ThenByDescending(t => t.CreatedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Get tasks by filter predicate
        /// </summary>
        public List<TaskItem> GetTasks(Func<TaskItem, bool> predicate)
        {
            lock (lockObject)
            {
                return tasks.Values
                    .Where(predicate)
                    .OrderBy(t => t.SortOrder)
                    .ThenByDescending(t => t.CreatedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Get task by ID
        /// </summary>
        public TaskItem GetTask(Guid id)
        {
            lock (lockObject)
            {
                return tasks.ContainsKey(id) ? tasks[id] : null;
            }
        }

        /// <summary>
        /// Get subtasks for a parent task
        /// </summary>
        public List<TaskItem> GetSubtasks(Guid parentId)
        {
            lock (lockObject)
            {
                if (!subtaskIndex.ContainsKey(parentId))
                    return new List<TaskItem>();

                return subtaskIndex[parentId]
                    .Select(id => tasks.ContainsKey(id) ? tasks[id] : null)
                    .Where(t => t != null && !t.Deleted)
                    .OrderBy(t => t.SortOrder)
                    .ToList();
            }
        }

        /// <summary>
        /// Check if task has subtasks
        /// </summary>
        public bool HasSubtasks(Guid taskId)
        {
            lock (lockObject)
            {
                return subtaskIndex.ContainsKey(taskId) &&
                       subtaskIndex[taskId].Any(id => tasks.ContainsKey(id) && !tasks[id].Deleted);
            }
        }

        /// <summary>
        /// Add new task
        /// </summary>
        public TaskItem AddTask(TaskItem task)
        {
            lock (lockObject)
            {
                task.Id = Guid.NewGuid();
                task.CreatedAt = DateTime.Now;
                task.UpdatedAt = DateTime.Now;
                task.Deleted = false;

                tasks[task.Id] = task;

                // Update subtask index if this is a subtask
                if (task.ParentTaskId.HasValue)
                {
                    if (!subtaskIndex.ContainsKey(task.ParentTaskId.Value))
                        subtaskIndex[task.ParentTaskId.Value] = new List<Guid>();

                    subtaskIndex[task.ParentTaskId.Value].Add(task.Id);
                }

                SaveToFile();
                TaskAdded?.Invoke(task);

                Logger.Instance?.Info("TaskService", $"Added task: {task.Title} (ID: {task.Id})");
                return task;
            }
        }

        /// <summary>
        /// Update existing task
        /// </summary>
        public bool UpdateTask(TaskItem task)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(task.Id))
                {
                    Logger.Instance?.Warning("TaskService", $"Cannot update non-existent task: {task.Id}");
                    return false;
                }

                var oldTask = tasks[task.Id];
                task.UpdatedAt = DateTime.Now;

                // Handle parent change for subtasks
                if (oldTask.ParentTaskId != task.ParentTaskId)
                {
                    // Remove from old parent's index
                    if (oldTask.ParentTaskId.HasValue && subtaskIndex.ContainsKey(oldTask.ParentTaskId.Value))
                    {
                        subtaskIndex[oldTask.ParentTaskId.Value].Remove(task.Id);
                    }

                    // Add to new parent's index
                    if (task.ParentTaskId.HasValue)
                    {
                        if (!subtaskIndex.ContainsKey(task.ParentTaskId.Value))
                            subtaskIndex[task.ParentTaskId.Value] = new List<Guid>();

                        subtaskIndex[task.ParentTaskId.Value].Add(task.Id);
                    }
                }

                tasks[task.Id] = task;
                SaveToFile();
                TaskUpdated?.Invoke(task);

                Logger.Instance?.Debug("TaskService", $"Updated task: {task.Title} (ID: {task.Id})");
                return true;
            }
        }

        /// <summary>
        /// Delete task (soft delete)
        /// </summary>
        public bool DeleteTask(Guid id, bool hardDelete = false)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(id))
                {
                    Logger.Instance?.Warning("TaskService", $"Cannot delete non-existent task: {id}");
                    return false;
                }

                var task = tasks[id];

                if (hardDelete)
                {
                    // Remove from parent's subtask index
                    if (task.ParentTaskId.HasValue && subtaskIndex.ContainsKey(task.ParentTaskId.Value))
                    {
                        subtaskIndex[task.ParentTaskId.Value].Remove(id);
                    }

                    // Remove subtask index for this task
                    subtaskIndex.Remove(id);

                    // Hard delete all subtasks
                    if (subtaskIndex.ContainsKey(id))
                    {
                        foreach (var subtaskId in subtaskIndex[id].ToList())
                        {
                            DeleteTask(subtaskId, hardDelete: true);
                        }
                    }

                    tasks.Remove(id);
                }
                else
                {
                    // Soft delete
                    task.Deleted = true;
                    task.UpdatedAt = DateTime.Now;

                    // Soft delete all subtasks
                    var subtasks = GetSubtasks(id);
                    foreach (var subtask in subtasks)
                    {
                        DeleteTask(subtask.Id, hardDelete: false);
                    }
                }

                SaveToFile();
                TaskDeleted?.Invoke(id);

                Logger.Instance?.Info("TaskService", $"Deleted task: {task.Title} (ID: {id}, Hard: {hardDelete})");
                return true;
            }
        }

        /// <summary>
        /// Toggle task completion status
        /// </summary>
        public bool ToggleTaskCompletion(Guid id)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(id))
                    return false;

                var task = tasks[id];
                task.Status = task.Status == TaskStatus.Completed ? TaskStatus.Pending : TaskStatus.Completed;
                task.Progress = task.Status == TaskStatus.Completed ? 100 : 0;
                task.UpdatedAt = DateTime.Now;

                SaveToFile();
                TaskUpdated?.Invoke(task);

                Logger.Instance?.Debug("TaskService", $"Toggled completion for task: {task.Title}");
                return true;
            }
        }

        /// <summary>
        /// Cycle task priority (Low -> Medium -> High -> Low)
        /// </summary>
        public bool CyclePriority(Guid id)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(id))
                    return false;

                var task = tasks[id];
                task.Priority = task.Priority switch
                {
                    TaskPriority.Low => TaskPriority.Medium,
                    TaskPriority.Medium => TaskPriority.High,
                    TaskPriority.High => TaskPriority.Low,
                    _ => TaskPriority.Medium
                };
                task.UpdatedAt = DateTime.Now;

                SaveToFile();
                TaskUpdated?.Invoke(task);

                Logger.Instance?.Debug("TaskService", $"Cycled priority for task: {task.Title} to {task.Priority}");
                return true;
            }
        }

        /// <summary>
        /// Get task count by filter
        /// </summary>
        public int GetTaskCount(Func<TaskItem, bool> predicate = null)
        {
            lock (lockObject)
            {
                if (predicate == null)
                    return tasks.Values.Count(t => !t.Deleted);

                return tasks.Values.Count(predicate);
            }
        }

        /// <summary>
        /// Save tasks to JSON file
        /// </summary>
        private void SaveToFile()
        {
            try
            {
                // Create backup before saving
                if (File.Exists(dataFilePath))
                {
                    var backupPath = $"{dataFilePath}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                    File.Copy(dataFilePath, backupPath, overwrite: true);

                    // Keep only last 5 backups
                    var backupDir = Path.GetDirectoryName(dataFilePath);
                    var backupFiles = Directory.GetFiles(backupDir, "tasks.json.*.bak")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .Skip(5)
                        .ToList();

                    foreach (var oldBackup in backupFiles)
                    {
                        try { File.Delete(oldBackup); } catch { }
                    }
                }

                var json = JsonSerializer.Serialize(tasks.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(dataFilePath, json);
                Logger.Instance?.Debug("TaskService", $"Saved {tasks.Count} tasks to {dataFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("TaskService", $"Failed to save tasks: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Load tasks from JSON file
        /// </summary>
        private void LoadFromFile()
        {
            try
            {
                if (!File.Exists(dataFilePath))
                {
                    Logger.Instance?.Info("TaskService", "No existing task file found, starting fresh");
                    return;
                }

                var json = File.ReadAllText(dataFilePath);
                var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);

                lock (lockObject)
                {
                    tasks.Clear();
                    subtaskIndex.Clear();

                    foreach (var task in loadedTasks)
                    {
                        tasks[task.Id] = task;

                        // Rebuild subtask index
                        if (task.ParentTaskId.HasValue)
                        {
                            if (!subtaskIndex.ContainsKey(task.ParentTaskId.Value))
                                subtaskIndex[task.ParentTaskId.Value] = new List<Guid>();

                            subtaskIndex[task.ParentTaskId.Value].Add(task.Id);
                        }
                    }
                }

                Logger.Instance?.Info("TaskService", $"Loaded {tasks.Count} tasks from {dataFilePath}");
                TasksReloaded?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("TaskService", $"Failed to load tasks: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reload tasks from file (useful for external changes)
        /// </summary>
        public void Reload()
        {
            LoadFromFile();
        }

        /// <summary>
        /// Clear all tasks (for testing)
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                tasks.Clear();
                subtaskIndex.Clear();
                SaveToFile();
                TasksReloaded?.Invoke();
                Logger.Instance?.Info("TaskService", "Cleared all tasks");
            }
        }
    }
}
