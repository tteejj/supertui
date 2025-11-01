using System;
using System.Collections.Generic;

namespace SuperTUI.Core.Models
{
    /// <summary>
    /// Data transfer objects for service persistence
    /// These are simple serializable containers with no business logic
    /// </summary>

    /// <summary>
    /// DTO for TaskService persistence
    /// Contains list of all tasks for JSON serialization
    /// </summary>
    public class TaskServiceData
    {
        public List<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }

    /// <summary>
    /// DTO for ProjectService persistence
    /// Contains list of all projects for JSON serialization
    /// </summary>
    public class ProjectServiceData
    {
        public List<Project> Projects { get; set; } = new List<Project>();
    }

    /// <summary>
    /// DTO for TimeTrackingService persistence
    /// Contains list of all time entries for JSON serialization
    /// </summary>
    public class TimeTrackingServiceData
    {
        public List<TimeEntry> Entries { get; set; } = new List<TimeEntry>();
    }
}
