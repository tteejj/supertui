using System;
using System.Collections.Generic;
using SuperTUI.Core.Models;

namespace SuperTUI.Core.Interfaces
{
    /// <summary>
    /// Interface for weekly time tracking service
    /// Manages WeeklyTimeEntry records with Mon-Fri workweek pattern
    /// </summary>
    public interface IWeeklyTimeTrackingService
    {
        // Events for weekly time entry changes
        event Action<WeeklyTimeEntry> EntryAdded;
        event Action<WeeklyTimeEntry> EntryUpdated;
        event Action<Guid> EntryDeleted;
        event Action EntriesReloaded;

        /// <summary>
        /// Initialize service with data file path
        /// </summary>
        void Initialize(string filePath = null);

        // CRUD Operations
        WeeklyTimeEntry AddEntry(WeeklyTimeEntry entry);
        bool UpdateEntry(WeeklyTimeEntry entry);
        bool DeleteEntry(Guid id, bool hardDelete = false);
        WeeklyTimeEntry GetEntry(Guid id);
        List<WeeklyTimeEntry> GetAllEntries(bool includeDeleted = false);
        List<WeeklyTimeEntry> GetEntries(Func<WeeklyTimeEntry, bool> predicate);

        // Query Operations
        List<WeeklyTimeEntry> GetEntriesForWeek(DateTime weekEndingFriday);
        List<WeeklyTimeEntry> GetEntriesForProject(Guid projectId);
        List<WeeklyTimeEntry> GetEntriesForTask(Guid taskId);
        List<WeeklyTimeEntry> GetEntriesForFiscalYear(string fiscalYear);
        WeeklyTimeEntry GetEntryByCode(DateTime weekEndingFriday, string id1, string id2);

        // Duplicate Detection
        bool HasDuplicate(DateTime weekEndingFriday, string id1, string id2, Guid? excludeId = null);

        // Aggregation
        decimal GetTotalHoursForWeek(DateTime weekEndingFriday);
        decimal GetTotalHoursForProject(Guid projectId);
        decimal GetTotalHoursForFiscalYear(string fiscalYear);
        Dictionary<string, decimal> GetCategoryBreakdown(DateTime weekEndingFriday);

        // Conversion
        List<WeeklyTimeEntry> ConvertFromTimeEntries(List<TimeEntry> timeEntries);

        // Persistence
        bool SaveToFile();
        bool LoadFromFile();
    }
}
