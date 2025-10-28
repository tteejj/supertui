using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperTUI.Core.Models
{
    /// <summary>
    /// Task status enumeration
    /// </summary>
    public enum TaskStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Cancelled = 3
    }

    /// <summary>
    /// Task priority enumeration
    /// </summary>
    public enum TaskPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Today = 3      // Highest priority - must do today
    }

    /// <summary>
    /// Recurrence type enumeration
    /// </summary>
    public enum RecurrenceType
    {
        None = 0,
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
        Yearly = 4
    }

    /// <summary>
    /// Task color theme for visual organization
    /// </summary>
    public enum TaskColorTheme
    {
        None = 0,      // Default theme
        Red = 1,       // Urgent/Critical
        Blue = 2,      // Work/Professional
        Green = 3,     // Personal/Health
        Yellow = 4,    // Learning/Development
        Purple = 5,    // Creative/Projects
        Orange = 6     // Social/Events
    }

    /// <summary>
    /// Task note model
    /// </summary>
    public class TaskNote
    {
        public Guid Id { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        public TaskNote()
        {
            Id = Guid.NewGuid();
            Content = string.Empty;
            CreatedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Task model with full properties and subtask support
    /// </summary>
    public class TaskItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public TaskStatus Status { get; set; }
        public TaskPriority Priority { get; set; }
        public int Progress { get; set; } // 0-100
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }  // When task was completed
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Deleted { get; set; } // Soft delete

        // Time tracking
        public TimeSpan? EstimatedDuration { get; set; }  // User-entered estimate

        // Project integration (for future)
        public Guid? ProjectId { get; set; }

        // Assignment
        public string AssignedTo { get; set; }  // Username or email

        // External system integration (Excel, etc.)
        public string ExternalId1 { get; set; }  // Category/Type code (max 20 chars)
        public string ExternalId2 { get; set; }  // Project/Task code (max 20 chars)

        // Tags
        public List<string> Tags { get; set; }

        // Color theme
        public TaskColorTheme ColorTheme { get; set; }

        // Subtask support
        public Guid? ParentTaskId { get; set; }
        public int SortOrder { get; set; } // For ordering subtasks
        public int IndentLevel { get; set; } = 0; // For tree display
        public bool IsExpanded { get; set; } = true; // For tree collapse/expand

        // Dependencies
        public List<Guid> DependsOn { get; set; } // Tasks that must be completed first

        // Recurrence
        public RecurrenceType Recurrence { get; set; }
        public int RecurrenceInterval { get; set; } // Every N days/weeks/months
        public DateTime? RecurrenceEndDate { get; set; }
        public DateTime? LastRecurrence { get; set; }

        // Notes
        public List<TaskNote> Notes { get; set; }

        public TaskItem()
        {
            Id = Guid.NewGuid();
            Title = string.Empty;
            Description = string.Empty;
            Status = TaskStatus.Pending;
            Priority = TaskPriority.Medium;
            Progress = 0;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            Deleted = false;
            Tags = new List<string>();
            ColorTheme = TaskColorTheme.None;
            SortOrder = 0;
            DependsOn = new List<Guid>();
            Recurrence = RecurrenceType.None;
            RecurrenceInterval = 1;
            Notes = new List<TaskNote>();
        }

        /// <summary>
        /// Get actual duration from time entries
        /// Note: Requires TimeTrackingService to calculate
        /// </summary>
        public TimeSpan ActualDuration
        {
            get
            {
                try
                {
                    var timeTracking = SuperTUI.Core.Services.TimeTrackingService.Instance;
                    var entries = timeTracking.GetTimeEntriesForTask(Id);
                    return TimeSpan.FromHours((double)entries.Sum(e => e.Hours));
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        /// <summary>
        /// Get time variance (actual - estimated)
        /// </summary>
        public TimeSpan? TimeVariance
        {
            get
            {
                if (!EstimatedDuration.HasValue)
                    return null;
                return ActualDuration - EstimatedDuration.Value;
            }
        }

        /// <summary>
        /// Check if task is over estimate
        /// </summary>
        public bool IsOverEstimate => ActualDuration > (EstimatedDuration ?? TimeSpan.MaxValue);

        /// <summary>
        /// Check if this task is a subtask
        /// </summary>
        public bool IsSubtask => ParentTaskId.HasValue;

        /// <summary>
        /// Check if this task is blocked by dependencies
        /// Note: Requires TaskService to evaluate dependencies
        /// </summary>
        public bool IsBlocked
        {
            get
            {
                if (DependsOn == null || !DependsOn.Any())
                    return false;

                // Check if any dependencies are not completed
                var taskService = SuperTUI.Core.Services.TaskService.Instance;
                foreach (var depId in DependsOn)
                {
                    var depTask = taskService.GetTask(depId);
                    if (depTask != null && depTask.Status != TaskStatus.Completed)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Check if task is overdue
        /// </summary>
        public bool IsOverdue
        {
            get
            {
                if (!DueDate.HasValue || Status == TaskStatus.Completed || Status == TaskStatus.Cancelled)
                    return false;
                return DueDate.Value.Date < DateTime.Now.Date;
            }
        }

        /// <summary>
        /// Check if task is due today
        /// </summary>
        public bool IsDueToday
        {
            get
            {
                if (!DueDate.HasValue || Status == TaskStatus.Completed || Status == TaskStatus.Cancelled)
                    return false;
                return DueDate.Value.Date == DateTime.Now.Date;
            }
        }

        /// <summary>
        /// Check if task is due this week
        /// </summary>
        public bool IsDueThisWeek
        {
            get
            {
                if (!DueDate.HasValue || Status == TaskStatus.Completed || Status == TaskStatus.Cancelled)
                    return false;

                var today = DateTime.Now.Date;
                var endOfWeek = today.AddDays(7 - (int)today.DayOfWeek);
                return DueDate.Value.Date >= today && DueDate.Value.Date <= endOfWeek;
            }
        }

        /// <summary>
        /// Get status icon for display
        /// </summary>
        public string StatusIcon
        {
            get
            {
                return Status switch
                {
                    TaskStatus.Pending => "☐",
                    TaskStatus.InProgress => "◐",
                    TaskStatus.Completed => "☑",
                    TaskStatus.Cancelled => "✗",
                    _ => "?"
                };
            }
        }

        /// <summary>
        /// Get priority icon for display
        /// </summary>
        public string PriorityIcon
        {
            get
            {
                return Priority switch
                {
                    TaskPriority.Low => "↓",
                    TaskPriority.Medium => "●",
                    TaskPriority.High => "↑",
                    TaskPriority.Today => "‼",
                    _ => "?"
                };
            }
        }

        /// <summary>
        /// Clone this task
        /// </summary>
        public TaskItem Clone()
        {
            return new TaskItem
            {
                Id = this.Id,
                Title = this.Title,
                Description = this.Description,
                Status = this.Status,
                Priority = this.Priority,
                Progress = this.Progress,
                DueDate = this.DueDate,
                CompletedDate = this.CompletedDate,
                CreatedAt = this.CreatedAt,
                UpdatedAt = this.UpdatedAt,
                Deleted = this.Deleted,
                EstimatedDuration = this.EstimatedDuration,
                ProjectId = this.ProjectId,
                AssignedTo = this.AssignedTo,
                ExternalId1 = this.ExternalId1,
                ExternalId2 = this.ExternalId2,
                Tags = new List<string>(this.Tags),
                ColorTheme = this.ColorTheme,
                ParentTaskId = this.ParentTaskId,
                SortOrder = this.SortOrder,
                DependsOn = new List<Guid>(this.DependsOn),
                Recurrence = this.Recurrence,
                RecurrenceInterval = this.RecurrenceInterval,
                RecurrenceEndDate = this.RecurrenceEndDate,
                LastRecurrence = this.LastRecurrence,
                Notes = this.Notes.Select(n => new TaskNote
                {
                    Id = n.Id,
                    Content = n.Content,
                    CreatedAt = n.CreatedAt
                }).ToList()
            };
        }

        /// <summary>
        /// Format task for display in list (used by ToString())
        /// </summary>
        public string ToDisplayString(bool showSubtaskIndicator = false, int subtaskCount = 0, bool isExpanded = false)
        {
            var parts = new List<string>();

            // Indentation for subtasks
            if (IsSubtask)
                parts.Add("  ");

            // Status icon
            parts.Add(StatusIcon);

            // Blocked indicator
            if (IsBlocked)
                parts.Add("⛔");

            // Priority icon
            var priorityChar = Priority == TaskPriority.High ? "!" :
                              Priority == TaskPriority.Medium ? "●" : "·";
            parts.Add(priorityChar);

            // Title (with visual indicator for completed)
            var title = Title;
            if (Status == TaskStatus.Completed)
                title = $"[✓] {title}";
            parts.Add(title);

            // Due date badge
            if (DueDate.HasValue)
            {
                var dueText = IsOverdue ? $"[OVERDUE {DueDate.Value:MMM dd}]" :
                             IsDueToday ? "[TODAY]" :
                             $"[{DueDate.Value:MMM dd}]";
                parts.Add(dueText);
            }

            // Subtask indicator
            if (showSubtaskIndicator && subtaskCount > 0)
            {
                var expandIcon = isExpanded ? "▼" : "▶";
                parts.Add($"{expandIcon}({subtaskCount})");
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Override ToString for display in ListBox
        /// Note: This is used by EditableListControl's ListBox binding
        /// </summary>
        public override string ToString()
        {
            return ToDisplayString();
        }
    }

    /// <summary>
    /// Filter preset for task queries
    /// </summary>
    public class TaskFilter
    {
        public string Name { get; set; }
        public Func<TaskItem, bool> Predicate { get; set; }

        public TaskFilter(string name, Func<TaskItem, bool> predicate)
        {
            Name = name;
            Predicate = predicate;
        }

        // Common filter presets
        public static TaskFilter All => new TaskFilter("All", t => !t.Deleted);
        public static TaskFilter Today => new TaskFilter("Today", t => !t.Deleted && t.IsDueToday);
        public static TaskFilter ThisWeek => new TaskFilter("This Week", t => !t.Deleted && t.IsDueThisWeek);
        public static TaskFilter Overdue => new TaskFilter("Overdue", t => !t.Deleted && t.IsOverdue);
        public static TaskFilter Pending => new TaskFilter("Pending", t => !t.Deleted && t.Status == TaskStatus.Pending);
        public static TaskFilter InProgress => new TaskFilter("In Progress", t => !t.Deleted && t.Status == TaskStatus.InProgress);
        public static TaskFilter Completed => new TaskFilter("Completed", t => !t.Deleted && t.Status == TaskStatus.Completed);
        public static TaskFilter HighPriority => new TaskFilter("High Priority", t => !t.Deleted && t.Priority == TaskPriority.High && t.Status != TaskStatus.Completed);

        public static List<TaskFilter> GetDefaultFilters()
        {
            return new List<TaskFilter>
            {
                All, Today, ThisWeek, Overdue, Pending, InProgress, Completed, HighPriority
            };
        }
    }
}
