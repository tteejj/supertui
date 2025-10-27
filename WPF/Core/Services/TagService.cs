using System;
using System.Collections.Generic;
using System.Linq;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Service for managing tags with autocomplete, usage tracking, and validation
    /// </summary>
    public class TagService
    {
        private static TagService instance;
        public static TagService Instance => instance ??= new TagService(
            Logger.Instance,
            ConfigurationManager.Instance,
            TaskService.Instance
        );

        private readonly ILogger logger;
        private readonly IConfigurationManager config;
        private readonly TaskService taskService;

        // Tag storage and indexing
        private readonly Dictionary<string, TagInfo> tagIndex; // Tag name -> TagInfo
        private readonly HashSet<string> allTags; // All known tags (case-insensitive lookup)
        private readonly object tagLock = new object();

        // Configuration
        private const int MaxTagLength = 50;
        private const int MaxTagsPerTask = 10;
        private static readonly char[] InvalidTagChars = new[] { ' ', '\t', '\n', '\r', ',', ';', '|', '/' };

        public TagService(ILogger logger, IConfigurationManager config, TaskService taskService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));

            tagIndex = new Dictionary<string, TagInfo>(StringComparer.OrdinalIgnoreCase);
            allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            RebuildTagIndex();
        }

        #region Tag CRUD

        /// <summary>
        /// Add tag to a task
        /// </summary>
        public bool AddTagToTask(Guid taskId, string tag)
        {
            lock (tagLock)
            {
                var task = taskService.GetTask(taskId);
                if (task == null)
                {
                    logger.Warning("TagService", $"Task {taskId} not found");
                    return false;
                }

                // Validate tag
                var validationError = ValidateTag(tag);
                if (!string.IsNullOrEmpty(validationError))
                {
                    logger.Warning("TagService", $"Invalid tag '{tag}': {validationError}");
                    return false;
                }

                // Normalize tag (trim, lowercase for comparison)
                var normalizedTag = NormalizeTag(tag);

                // Check if already exists on task
                if (task.Tags.Any(t => string.Equals(t, normalizedTag, StringComparison.OrdinalIgnoreCase)))
                {
                    logger.Debug("TagService", $"Tag '{normalizedTag}' already exists on task {taskId}");
                    return false;
                }

                // Check max tags per task
                if (task.Tags.Count >= MaxTagsPerTask)
                {
                    logger.Warning("TagService", $"Task {taskId} already has {MaxTagsPerTask} tags (max)");
                    return false;
                }

                // Add tag
                task.Tags.Add(normalizedTag);
                task.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(task);

                // Update tag index
                if (!allTags.Contains(normalizedTag))
                {
                    allTags.Add(normalizedTag);
                }

                if (tagIndex.ContainsKey(normalizedTag))
                {
                    tagIndex[normalizedTag].UsageCount++;
                    tagIndex[normalizedTag].LastUsed = DateTime.Now;
                }
                else
                {
                    tagIndex[normalizedTag] = new TagInfo
                    {
                        Name = normalizedTag,
                        UsageCount = 1,
                        FirstUsed = DateTime.Now,
                        LastUsed = DateTime.Now
                    };
                }

                logger.Info("TagService", $"Added tag '{normalizedTag}' to task {taskId}");
                return true;
            }
        }

        /// <summary>
        /// Remove tag from a task
        /// </summary>
        public bool RemoveTagFromTask(Guid taskId, string tag)
        {
            lock (tagLock)
            {
                var task = taskService.GetTask(taskId);
                if (task == null)
                {
                    logger.Warning("TagService", $"Task {taskId} not found");
                    return false;
                }

                var normalizedTag = NormalizeTag(tag);
                var removed = task.Tags.RemoveAll(t =>
                    string.Equals(t, normalizedTag, StringComparison.OrdinalIgnoreCase)) > 0;

                if (removed)
                {
                    task.UpdatedAt = DateTime.Now;
                    taskService.UpdateTask(task);

                    // Update usage count
                    if (tagIndex.ContainsKey(normalizedTag))
                    {
                        tagIndex[normalizedTag].UsageCount = Math.Max(0, tagIndex[normalizedTag].UsageCount - 1);
                    }

                    logger.Info("TagService", $"Removed tag '{normalizedTag}' from task {taskId}");
                }

                return removed;
            }
        }

        /// <summary>
        /// Set all tags for a task (replaces existing)
        /// </summary>
        public bool SetTaskTags(Guid taskId, List<string> tags)
        {
            lock (tagLock)
            {
                var task = taskService.GetTask(taskId);
                if (task == null)
                {
                    logger.Warning("TagService", $"Task {taskId} not found");
                    return false;
                }

                // Validate all tags
                var validatedTags = new List<string>();
                foreach (var tag in tags)
                {
                    var validationError = ValidateTag(tag);
                    if (string.IsNullOrEmpty(validationError))
                    {
                        validatedTags.Add(NormalizeTag(tag));
                    }
                    else
                    {
                        logger.Warning("TagService", $"Skipping invalid tag '{tag}': {validationError}");
                    }
                }

                // Check max tags
                if (validatedTags.Count > MaxTagsPerTask)
                {
                    logger.Warning("TagService", $"Too many tags ({validatedTags.Count}), truncating to {MaxTagsPerTask}");
                    validatedTags = validatedTags.Take(MaxTagsPerTask).ToList();
                }

                // Clear old tags and set new ones
                task.Tags.Clear();
                task.Tags.AddRange(validatedTags);
                task.UpdatedAt = DateTime.Now;
                taskService.UpdateTask(task);

                // Rebuild tag index (simple approach)
                RebuildTagIndex();

                logger.Info("TagService", $"Set {validatedTags.Count} tags for task {taskId}");
                return true;
            }
        }

        #endregion

        #region Tag Queries

        /// <summary>
        /// Get all tags currently in use
        /// </summary>
        public List<string> GetAllTags()
        {
            lock (tagLock)
            {
                return allTags.ToList();
            }
        }

        /// <summary>
        /// Get tags sorted by usage count
        /// </summary>
        public List<TagInfo> GetTagsByUsage(int limit = 20)
        {
            lock (tagLock)
            {
                return tagIndex.Values
                    .OrderByDescending(t => t.UsageCount)
                    .ThenBy(t => t.Name)
                    .Take(limit)
                    .ToList();
            }
        }

        /// <summary>
        /// Get tags sorted by last used date
        /// </summary>
        public List<TagInfo> GetRecentTags(int limit = 10)
        {
            lock (tagLock)
            {
                return tagIndex.Values
                    .OrderByDescending(t => t.LastUsed)
                    .Take(limit)
                    .ToList();
            }
        }

        /// <summary>
        /// Get tag autocomplete suggestions
        /// </summary>
        public List<string> GetTagSuggestions(string prefix, int limit = 10)
        {
            lock (tagLock)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    // Return recent tags when no prefix
                    return GetRecentTags(limit).Select(t => t.Name).ToList();
                }

                var normalizedPrefix = prefix.Trim().ToLowerInvariant();

                return tagIndex.Values
                    .Where(t => t.Name.ToLowerInvariant().StartsWith(normalizedPrefix))
                    .OrderByDescending(t => t.UsageCount)
                    .ThenBy(t => t.Name)
                    .Take(limit)
                    .Select(t => t.Name)
                    .ToList();
            }
        }

        /// <summary>
        /// Get tasks by tag
        /// </summary>
        public List<TaskItem> GetTasksByTag(string tag)
        {
            var normalizedTag = NormalizeTag(tag);
            return taskService.GetTasks(t => !t.Deleted &&
                t.Tags.Any(taskTag => string.Equals(taskTag, normalizedTag, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Get tasks that have ALL specified tags
        /// </summary>
        public List<TaskItem> GetTasksByTags(List<string> tags, bool requireAll = true)
        {
            if (tags == null || tags.Count == 0)
                return new List<TaskItem>();

            var normalizedTags = tags.Select(NormalizeTag).ToList();

            if (requireAll)
            {
                // Must have ALL tags
                return taskService.GetTasks(t => !t.Deleted &&
                    normalizedTags.All(requiredTag =>
                        t.Tags.Any(taskTag => string.Equals(taskTag, requiredTag, StringComparison.OrdinalIgnoreCase))));
            }
            else
            {
                // Must have ANY tag
                return taskService.GetTasks(t => !t.Deleted &&
                    t.Tags.Any(taskTag =>
                        normalizedTags.Any(requiredTag => string.Equals(taskTag, requiredTag, StringComparison.OrdinalIgnoreCase))));
            }
        }

        /// <summary>
        /// Get tag info (usage stats)
        /// </summary>
        public TagInfo GetTagInfo(string tag)
        {
            lock (tagLock)
            {
                var normalizedTag = NormalizeTag(tag);
                return tagIndex.ContainsKey(normalizedTag) ? tagIndex[normalizedTag] : null;
            }
        }

        #endregion

        #region Tag Management

        /// <summary>
        /// Rename a tag across all tasks
        /// </summary>
        public int RenameTag(string oldTag, string newTag)
        {
            lock (tagLock)
            {
                var oldNormalized = NormalizeTag(oldTag);
                var newNormalized = NormalizeTag(newTag);

                if (string.Equals(oldNormalized, newNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Debug("TagService", "Old and new tag names are the same");
                    return 0;
                }

                // Validate new tag
                var validationError = ValidateTag(newTag);
                if (!string.IsNullOrEmpty(validationError))
                {
                    logger.Warning("TagService", $"Invalid new tag '{newTag}': {validationError}");
                    return 0;
                }

                int count = 0;
                var tasksWithTag = GetTasksByTag(oldNormalized);

                foreach (var task in tasksWithTag)
                {
                    // Remove old tag
                    task.Tags.RemoveAll(t => string.Equals(t, oldNormalized, StringComparison.OrdinalIgnoreCase));

                    // Add new tag if not already present
                    if (!task.Tags.Any(t => string.Equals(t, newNormalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        task.Tags.Add(newNormalized);
                    }

                    task.UpdatedAt = DateTime.Now;
                    taskService.UpdateTask(task);
                    count++;
                }

                // Rebuild index
                RebuildTagIndex();

                logger.Info("TagService", $"Renamed tag '{oldNormalized}' to '{newNormalized}' on {count} tasks");
                return count;
            }
        }

        /// <summary>
        /// Delete a tag from all tasks
        /// </summary>
        public int DeleteTag(string tag)
        {
            lock (tagLock)
            {
                var normalizedTag = NormalizeTag(tag);
                int count = 0;

                var tasksWithTag = GetTasksByTag(normalizedTag);
                foreach (var task in tasksWithTag)
                {
                    task.Tags.RemoveAll(t => string.Equals(t, normalizedTag, StringComparison.OrdinalIgnoreCase));
                    task.UpdatedAt = DateTime.Now;
                    taskService.UpdateTask(task);
                    count++;
                }

                // Rebuild index
                RebuildTagIndex();

                logger.Info("TagService", $"Deleted tag '{normalizedTag}' from {count} tasks");
                return count;
            }
        }

        /// <summary>
        /// Merge two tags (replace all instances of oldTag with newTag)
        /// </summary>
        public int MergeTags(string sourceTag, string targetTag)
        {
            return RenameTag(sourceTag, targetTag);
        }

        #endregion

        #region Validation and Normalization

        /// <summary>
        /// Validate tag name
        /// </summary>
        public string ValidateTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return "Tag cannot be empty";

            var trimmed = tag.Trim();

            if (trimmed.Length > MaxTagLength)
                return $"Tag too long (max {MaxTagLength} characters)";

            if (trimmed.Any(c => InvalidTagChars.Contains(c)))
                return $"Tag contains invalid characters (spaces, commas, etc.)";

            return null; // Valid
        }

        /// <summary>
        /// Normalize tag (trim, lowercase for storage)
        /// </summary>
        private string NormalizeTag(string tag)
        {
            return tag?.Trim() ?? string.Empty;
        }

        #endregion

        #region Index Management

        /// <summary>
        /// Rebuild tag index from all tasks
        /// </summary>
        public void RebuildTagIndex()
        {
            lock (tagLock)
            {
                tagIndex.Clear();
                allTags.Clear();

                var allTasks = taskService.GetTasks(t => !t.Deleted);

                foreach (var task in allTasks)
                {
                    foreach (var tag in task.Tags)
                    {
                        var normalizedTag = NormalizeTag(tag);

                        if (!allTags.Contains(normalizedTag))
                        {
                            allTags.Add(normalizedTag);
                        }

                        if (tagIndex.ContainsKey(normalizedTag))
                        {
                            tagIndex[normalizedTag].UsageCount++;
                            if (task.UpdatedAt > tagIndex[normalizedTag].LastUsed)
                            {
                                tagIndex[normalizedTag].LastUsed = task.UpdatedAt;
                            }
                        }
                        else
                        {
                            tagIndex[normalizedTag] = new TagInfo
                            {
                                Name = normalizedTag,
                                UsageCount = 1,
                                FirstUsed = task.CreatedAt,
                                LastUsed = task.UpdatedAt
                            };
                        }
                    }
                }

                logger.Info("TagService", $"Rebuilt tag index: {allTags.Count} unique tags");
            }
        }

        #endregion
    }

    /// <summary>
    /// Tag metadata and usage statistics
    /// </summary>
    public class TagInfo
    {
        public string Name { get; set; }
        public int UsageCount { get; set; }
        public DateTime FirstUsed { get; set; }
        public DateTime LastUsed { get; set; }

        public override string ToString()
        {
            return $"{Name} ({UsageCount} uses, last: {LastUsed:yyyy-MM-dd})";
        }
    }
}
