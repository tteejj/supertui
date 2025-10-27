using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

// Resolve ambiguity between SuperTUI.Core.Models.TaskStatus and System.Threading.Tasks.TaskStatus
using TaskStatus = SuperTUI.Core.Models.TaskStatus;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Task service for managing tasks with JSON persistence
    /// Thread-safe service with singleton pattern for backward compatibility
    /// </summary>
    public class TaskService : ITaskService
    {
        private static TaskService instance;
        public static TaskService Instance => instance ??= new TaskService();

        private Dictionary<Guid, TaskItem> tasks;
        private Dictionary<Guid, List<Guid>> subtaskIndex; // ParentId -> List of ChildIds
        private string dataFilePath;
        private readonly object lockObject = new object();

        // Save debouncing
        private Timer saveTimer;
        private bool pendingSave = false;
        private const int SAVE_DEBOUNCE_MS = 500;

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

            // Initialize save timer
            saveTimer = new Timer(SaveTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Load existing tasks
            LoadFromFile();

            // Process recurring tasks on startup
            ProcessRecurringTasks();
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

                ScheduleSave();
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
                ScheduleSave();
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

                ScheduleSave();
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
                task.Status = task.Status == Models.TaskStatus.Completed ? Models.TaskStatus.Pending : Models.TaskStatus.Completed;
                task.Progress = task.Status == Models.TaskStatus.Completed ? 100 : 0;
                task.UpdatedAt = DateTime.Now;

                ScheduleSave();
                TaskUpdated?.Invoke(task);

                Logger.Instance?.Debug("TaskService", $"Toggled completion for task: {task.Title}");

                // Process recurring tasks if this was marked as completed
                if (task.Status == Models.TaskStatus.Completed)
                {
                    ProcessRecurringTasks();
                }

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

                ScheduleSave();
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
        /// Get all subtasks recursively
        /// </summary>
        public List<Guid> GetAllSubtasksRecursive(Guid parentId)
        {
            var result = new List<Guid>();

            lock (lockObject)
            {
                if (!subtaskIndex.ContainsKey(parentId))
                    return result;

                foreach (var childId in subtaskIndex[parentId])
                {
                    if (tasks.ContainsKey(childId) && !tasks[childId].Deleted)
                    {
                        result.Add(childId);
                        result.AddRange(GetAllSubtasksRecursive(childId));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Move task up in sort order (swap with previous sibling)
        /// </summary>
        public void MoveTaskUp(Guid taskId)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(taskId))
                    return;

                var task = tasks[taskId];
                var siblingTasks = GetSiblingTasks(task);

                int currentIndex = siblingTasks.FindIndex(t => t.Id == taskId);
                if (currentIndex <= 0)
                    return; // Already at top

                var previousTask = siblingTasks[currentIndex - 1];
                int tempOrder = task.SortOrder;
                task.SortOrder = previousTask.SortOrder;
                previousTask.SortOrder = tempOrder;

                task.UpdatedAt = DateTime.Now;
                previousTask.UpdatedAt = DateTime.Now;

                ScheduleSave();
                TaskUpdated?.Invoke(task);
                TaskUpdated?.Invoke(previousTask);

                Logger.Instance?.Info("TaskService", $"Moved task up: {task.Title}");
            }
        }

        /// <summary>
        /// Move task down in sort order (swap with next sibling)
        /// </summary>
        public void MoveTaskDown(Guid taskId)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(taskId))
                    return;

                var task = tasks[taskId];
                var siblingTasks = GetSiblingTasks(task);

                int currentIndex = siblingTasks.FindIndex(t => t.Id == taskId);
                if (currentIndex < 0 || currentIndex >= siblingTasks.Count - 1)
                    return; // Already at bottom

                var nextTask = siblingTasks[currentIndex + 1];
                int tempOrder = task.SortOrder;
                task.SortOrder = nextTask.SortOrder;
                nextTask.SortOrder = tempOrder;

                task.UpdatedAt = DateTime.Now;
                nextTask.UpdatedAt = DateTime.Now;

                ScheduleSave();
                TaskUpdated?.Invoke(task);
                TaskUpdated?.Invoke(nextTask);

                Logger.Instance?.Info("TaskService", $"Moved task down: {task.Title}");
            }
        }

        /// <summary>
        /// Get sibling tasks (tasks with same parent)
        /// </summary>
        private List<TaskItem> GetSiblingTasks(TaskItem task)
        {
            Func<TaskItem, bool> siblingFilter = t =>
                t.ParentTaskId == task.ParentTaskId &&
                !t.Deleted;

            return tasks.Values
                .Where(siblingFilter)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Normalize sort orders to sequential values (0, 100, 200, ...)
        /// </summary>
        public void NormalizeSortOrders(Guid? parentTaskId = null)
        {
            lock (lockObject)
            {
                var tasksToNormalize = tasks.Values
                    .Where(t => t.ParentTaskId == parentTaskId && !t.Deleted)
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.CreatedAt)
                    .ToList();

                for (int i = 0; i < tasksToNormalize.Count; i++)
                {
                    tasksToNormalize[i].SortOrder = i * 100;
                }

                ScheduleSave();
                Logger.Instance?.Info("TaskService", $"Normalized sort orders for {tasksToNormalize.Count} tasks");
            }
        }

        /// <summary>
        /// Schedule a debounced save operation
        /// </summary>
        private void ScheduleSave()
        {
            pendingSave = true;
            saveTimer?.Change(SAVE_DEBOUNCE_MS, Timeout.Infinite);
        }

        /// <summary>
        /// Timer callback for debounced save
        /// </summary>
        private void SaveTimerCallback(object state)
        {
            if (pendingSave)
            {
                pendingSave = false;
                Task.Run(async () => await SaveToFileAsync());
            }
        }

        /// <summary>
        /// Save tasks to JSON file asynchronously
        /// </summary>
        private async Task SaveToFileAsync()
        {
            try
            {
                // Create backup before saving
                if (File.Exists(dataFilePath))
                {
                    var backupPath = $"{dataFilePath}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                    await Task.Run(() => File.Copy(dataFilePath, backupPath, overwrite: true));

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

                List<TaskItem> taskList;
                lock (lockObject)
                {
                    taskList = tasks.Values.ToList();
                }

                var json = await Task.Run(() => JsonSerializer.Serialize(taskList, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

                await Task.Run(() => File.WriteAllText(dataFilePath, json));
                Logger.Instance?.Debug("TaskService", $"Saved {taskList.Count} tasks to {dataFilePath}");
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
                ScheduleSave();
                TasksReloaded?.Invoke();
                Logger.Instance?.Info("TaskService", "Cleared all tasks");
            }
        }

        #region Dependencies

        /// <summary>
        /// Add a dependency: taskId depends on dependsOnTaskId
        /// </summary>
        public bool AddDependency(Guid taskId, Guid dependsOnTaskId)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(taskId) || !tasks.ContainsKey(dependsOnTaskId))
                {
                    Logger.Instance?.Warning("TaskService", $"Cannot add dependency: one or both tasks not found");
                    return false;
                }

                var task = tasks[taskId];
                if (task.DependsOn.Contains(dependsOnTaskId))
                {
                    Logger.Instance?.Debug("TaskService", $"Dependency already exists");
                    return false;
                }

                // Prevent circular dependencies
                if (WouldCreateCircularDependency(taskId, dependsOnTaskId))
                {
                    Logger.Instance?.Warning("TaskService", $"Cannot add dependency: would create circular dependency");
                    return false;
                }

                task.DependsOn.Add(dependsOnTaskId);
                task.UpdatedAt = DateTime.Now;

                ScheduleSave();
                TaskUpdated?.Invoke(task);

                Logger.Instance?.Info("TaskService", $"Added dependency: {task.Title} depends on {tasks[dependsOnTaskId].Title}");
                return true;
            }
        }

        /// <summary>
        /// Remove a dependency
        /// </summary>
        public bool RemoveDependency(Guid taskId, Guid dependsOnTaskId)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(taskId))
                {
                    Logger.Instance?.Warning("TaskService", $"Cannot remove dependency: task not found");
                    return false;
                }

                var task = tasks[taskId];
                if (!task.DependsOn.Contains(dependsOnTaskId))
                {
                    Logger.Instance?.Debug("TaskService", $"Dependency does not exist");
                    return false;
                }

                task.DependsOn.Remove(dependsOnTaskId);
                task.UpdatedAt = DateTime.Now;

                ScheduleSave();
                TaskUpdated?.Invoke(task);

                Logger.Instance?.Info("TaskService", $"Removed dependency from task: {task.Title}");
                return true;
            }
        }

        /// <summary>
        /// Get list of tasks that this task depends on
        /// </summary>
        public List<TaskItem> GetDependencies(Guid taskId)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(taskId))
                    return new List<TaskItem>();

                var task = tasks[taskId];
                return task.DependsOn
                    .Select(id => tasks.ContainsKey(id) ? tasks[id] : null)
                    .Where(t => t != null && !t.Deleted)
                    .ToList();
            }
        }

        /// <summary>
        /// Get list of tasks blocked by this task
        /// </summary>
        public List<TaskItem> GetBlockedTasks(Guid taskId)
        {
            lock (lockObject)
            {
                return tasks.Values
                    .Where(t => !t.Deleted && t.DependsOn.Contains(taskId))
                    .ToList();
            }
        }

        /// <summary>
        /// Check if adding a dependency would create a circular dependency
        /// </summary>
        private bool WouldCreateCircularDependency(Guid taskId, Guid dependsOnTaskId)
        {
            // Check if dependsOnTaskId depends on taskId (directly or indirectly)
            var visited = new HashSet<Guid>();
            var toCheck = new Queue<Guid>();
            toCheck.Enqueue(dependsOnTaskId);

            while (toCheck.Count > 0)
            {
                var current = toCheck.Dequeue();
                if (visited.Contains(current))
                    continue;

                visited.Add(current);

                if (current == taskId)
                    return true; // Circular dependency found

                if (tasks.ContainsKey(current))
                {
                    foreach (var depId in tasks[current].DependsOn)
                    {
                        toCheck.Enqueue(depId);
                    }
                }
            }

            return false;
        }

        #endregion

        #region Recurrence

        /// <summary>
        /// Process recurring tasks: check for completed recurring tasks and create new instances
        /// </summary>
        public void ProcessRecurringTasks()
        {
            lock (lockObject)
            {
                var recurringTasks = tasks.Values
                    .Where(t => !t.Deleted && t.Recurrence != RecurrenceType.None && t.Status == TaskStatus.Completed)
                    .ToList();

                foreach (var task in recurringTasks)
                {
                    // Check if task is due for recurrence
                    if (!ShouldRecur(task))
                        continue;

                    // Calculate next due date
                    var nextDueDate = CalculateNextDueDate(task);
                    if (!nextDueDate.HasValue)
                        continue;

                    // Check if past recurrence end date
                    if (task.RecurrenceEndDate.HasValue && nextDueDate.Value > task.RecurrenceEndDate.Value)
                    {
                        Logger.Instance?.Info("TaskService", $"Recurring task '{task.Title}' has reached end date");
                        continue;
                    }

                    // Create new task instance
                    var newTask = new TaskItem
                    {
                        Title = task.Title,
                        Description = task.Description,
                        Priority = task.Priority,
                        DueDate = nextDueDate,
                        Tags = new List<string>(task.Tags),
                        ParentTaskId = task.ParentTaskId,
                        DependsOn = new List<Guid>(task.DependsOn),
                        Recurrence = task.Recurrence,
                        RecurrenceInterval = task.RecurrenceInterval,
                        RecurrenceEndDate = task.RecurrenceEndDate,
                        Status = TaskStatus.Pending,
                        Progress = 0
                    };

                    AddTask(newTask);

                    // Update last recurrence date
                    task.LastRecurrence = DateTime.Now;
                    ScheduleSave();

                    Logger.Instance?.Info("TaskService", $"Created recurring task instance: {newTask.Title} (Due: {nextDueDate.Value:yyyy-MM-dd})");
                }
            }
        }

        /// <summary>
        /// Check if a task should recur now
        /// </summary>
        private bool ShouldRecur(TaskItem task)
        {
            if (!task.LastRecurrence.HasValue)
                return true; // First recurrence

            var timeSinceLastRecurrence = DateTime.Now - task.LastRecurrence.Value;

            return task.Recurrence switch
            {
                RecurrenceType.Daily => timeSinceLastRecurrence.TotalDays >= task.RecurrenceInterval,
                RecurrenceType.Weekly => timeSinceLastRecurrence.TotalDays >= (task.RecurrenceInterval * 7),
                RecurrenceType.Monthly => timeSinceLastRecurrence.TotalDays >= (task.RecurrenceInterval * 30),
                RecurrenceType.Yearly => timeSinceLastRecurrence.TotalDays >= (task.RecurrenceInterval * 365),
                _ => false
            };
        }

        /// <summary>
        /// Calculate next due date for recurring task
        /// </summary>
        private DateTime? CalculateNextDueDate(TaskItem task)
        {
            if (!task.DueDate.HasValue)
                return null;

            return task.Recurrence switch
            {
                RecurrenceType.Daily => task.DueDate.Value.AddDays(task.RecurrenceInterval),
                RecurrenceType.Weekly => task.DueDate.Value.AddDays(task.RecurrenceInterval * 7),
                RecurrenceType.Monthly => task.DueDate.Value.AddMonths(task.RecurrenceInterval),
                RecurrenceType.Yearly => task.DueDate.Value.AddYears(task.RecurrenceInterval),
                _ => null
            };
        }

        #endregion

        #region Notes

        /// <summary>
        /// Add a note to a task
        /// </summary>
        public TaskNote AddNote(Guid taskId, string content)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(taskId))
                {
                    Logger.Instance?.Warning("TaskService", $"Cannot add note: task not found");
                    return null;
                }

                var task = tasks[taskId];
                var note = new TaskNote
                {
                    Content = content,
                    CreatedAt = DateTime.Now
                };

                task.Notes.Add(note);
                task.UpdatedAt = DateTime.Now;

                ScheduleSave();
                TaskUpdated?.Invoke(task);

                Logger.Instance?.Info("TaskService", $"Added note to task: {task.Title}");
                return note;
            }
        }

        /// <summary>
        /// Remove a note from a task
        /// </summary>
        public bool RemoveNote(Guid taskId, Guid noteId)
        {
            lock (lockObject)
            {
                if (!tasks.ContainsKey(taskId))
                {
                    Logger.Instance?.Warning("TaskService", $"Cannot remove note: task not found");
                    return false;
                }

                var task = tasks[taskId];
                var note = task.Notes.FirstOrDefault(n => n.Id == noteId);
                if (note == null)
                {
                    Logger.Instance?.Debug("TaskService", $"Note not found");
                    return false;
                }

                task.Notes.Remove(note);
                task.UpdatedAt = DateTime.Now;

                ScheduleSave();
                TaskUpdated?.Invoke(task);

                Logger.Instance?.Info("TaskService", $"Removed note from task: {task.Title}");
                return true;
            }
        }

        #endregion

        #region Project Integration

        /// <summary>
        /// Get tasks for a specific project
        /// </summary>
        public List<TaskItem> GetTasksForProject(Guid projectId)
        {
            return GetTasks(t => !t.Deleted && t.ProjectId == projectId);
        }

        /// <summary>
        /// Get task statistics for a project
        /// </summary>
        public ProjectTaskStats GetProjectStats(Guid projectId)
        {
            var tasks = GetTasksForProject(projectId);

            return new ProjectTaskStats
            {
                ProjectId = projectId,
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == TaskStatus.Completed),
                InProgressTasks = tasks.Count(t => t.Status == TaskStatus.InProgress),
                PendingTasks = tasks.Count(t => t.Status == TaskStatus.Pending),
                OverdueTasks = tasks.Count(t => t.IsOverdue),
                HighPriorityTasks = tasks.Count(t => t.Priority == TaskPriority.High && t.Status != TaskStatus.Completed)
            };
        }

        #endregion

        #region Export

        /// <summary>
        /// Export tasks to Markdown format
        /// </summary>
        public bool ExportToMarkdown(string filePath)
        {
            try
            {
                lock (lockObject)
                {
                    var lines = new List<string>();
                    lines.Add("# Task Export");
                    lines.Add($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    lines.Add("");

                    // Get all non-deleted tasks
                    var allTasks = tasks.Values.Where(t => !t.Deleted).ToList();
                    var rootTasks = allTasks.Where(t => !t.ParentTaskId.HasValue).OrderBy(t => t.SortOrder).ToList();

                    foreach (var task in rootTasks)
                    {
                        ExportTaskToMarkdown(task, lines, 0);
                    }

                    File.WriteAllLines(filePath, lines);
                    Logger.Instance?.Info("TaskService", $"Exported {allTasks.Count} tasks to Markdown: {filePath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("TaskService", $"Failed to export to Markdown: {ex.Message}", ex);
                return false;
            }
        }

        private void ExportTaskToMarkdown(TaskItem task, List<string> lines, int indent)
        {
            var indentStr = new string(' ', indent * 2);
            var checkbox = task.Status == TaskStatus.Completed ? "[x]" : "[ ]";
            var priorityIcon = task.PriorityIcon;
            var dueStr = task.DueDate.HasValue ? $" (Due: {task.DueDate.Value:yyyy-MM-dd})" : "";
            var blockedStr = task.IsBlocked ? " [BLOCKED]" : "";

            lines.Add($"{indentStr}- {checkbox} **{task.Title}** {priorityIcon}{dueStr}{blockedStr}");

            if (!string.IsNullOrWhiteSpace(task.Description))
            {
                lines.Add($"{indentStr}  {task.Description}");
            }

            if (task.Tags.Any())
            {
                lines.Add($"{indentStr}  Tags: {string.Join(", ", task.Tags)}");
            }

            if (task.Notes.Any())
            {
                lines.Add($"{indentStr}  Notes:");
                foreach (var note in task.Notes)
                {
                    lines.Add($"{indentStr}    - {note.Content}");
                }
            }

            // Add subtasks recursively
            var subtasks = GetSubtasks(task.Id).OrderBy(t => t.SortOrder).ToList();
            foreach (var subtask in subtasks)
            {
                ExportTaskToMarkdown(subtask, lines, indent + 1);
            }

            lines.Add("");
        }

        /// <summary>
        /// Export tasks to CSV format
        /// </summary>
        public bool ExportToCSV(string filePath)
        {
            try
            {
                lock (lockObject)
                {
                    var lines = new List<string>();
                    lines.Add("Id,Title,Status,Priority,DueDate,Progress,Description,Tags,Created,Updated");

                    var allTasks = tasks.Values.Where(t => !t.Deleted).OrderBy(t => t.CreatedAt).ToList();

                    foreach (var task in allTasks)
                    {
                        var fields = new[]
                        {
                            task.Id.ToString(),
                            EscapeCsv(task.Title),
                            task.Status.ToString(),
                            task.Priority.ToString(),
                            task.DueDate?.ToString("yyyy-MM-dd") ?? "",
                            task.Progress.ToString(),
                            EscapeCsv(task.Description),
                            EscapeCsv(string.Join("; ", task.Tags)),
                            task.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            task.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                        };

                        lines.Add(string.Join(",", fields));
                    }

                    File.WriteAllLines(filePath, lines);
                    Logger.Instance?.Info("TaskService", $"Exported {allTasks.Count} tasks to CSV: {filePath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("TaskService", $"Failed to export to CSV: {ex.Message}", ex);
                return false;
            }
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // Escape quotes and wrap in quotes if contains comma, quote, or newline
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        /// <summary>
        /// Export tasks to JSON format
        /// </summary>
        public bool ExportToJson(string filePath)
        {
            try
            {
                lock (lockObject)
                {
                    var allTasks = tasks.Values.Where(t => !t.Deleted).ToList();
                    var json = JsonSerializer.Serialize(allTasks, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(filePath, json);
                    Logger.Instance?.Info("TaskService", $"Exported {allTasks.Count} tasks to JSON: {filePath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("TaskService", $"Failed to export to JSON: {ex.Message}", ex);
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Dispose resources (timer)
        /// </summary>
        public void Dispose()
        {
            if (saveTimer != null)
            {
                // Ensure any pending save is executed before disposal
                if (pendingSave)
                {
                    SaveToFileAsync().Wait();
                }

                saveTimer.Dispose();
                saveTimer = null;
            }
        }
    }
}
