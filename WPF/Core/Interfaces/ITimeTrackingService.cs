using System;
using System.Collections.Generic;
using SuperTUI.Core.Models;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for time tracking service
    /// Matches actual TimeTrackingService implementation
    /// </summary>
    public interface ITimeTrackingService
    {
        // Events
        event Action<TimeEntry> EntryAdded;
        event Action<TimeEntry> EntryUpdated;
        event Action<Guid> EntryDeleted;

        // Initialization
        void Initialize(string filePath = null);

        // Time entry retrieval
        List<TimeEntry> GetAllEntries(bool includeDeleted = false);
        List<TimeEntry> GetEntries(Func<TimeEntry, bool> predicate);
        TimeEntry GetEntry(Guid id);
        List<TimeEntry> GetEntriesForProject(Guid projectId);
        List<TimeEntry> GetEntriesForWeek(DateTime weekEnding);
        TimeEntry GetEntryForProjectAndWeek(Guid projectId, DateTime weekEnding);

        // Time entry manipulation
        TimeEntry AddEntry(TimeEntry entry);
        bool UpdateEntry(TimeEntry entry);
        bool DeleteEntry(Guid id, bool hardDelete = false);

        // Statistics
        decimal GetProjectTotalHours(Guid projectId);
        decimal GetWeekTotalHours(DateTime weekEnding);
        ProjectTimeAggregate GetProjectAggregate(Guid projectId, DateTime? startDate = null, DateTime? endDate = null);
        List<ProjectTimeAggregate> GetAllProjectAggregates(DateTime? startDate = null, DateTime? endDate = null);
        WeeklyTimeReport GetWeeklyReport(DateTime weekEnding);
        FiscalYearSummary GetFiscalYearSummary(int fiscalYear);
        FiscalYearSummary GetCurrentFiscalYearSummary();

        // Bulk operations
        void Reload();
        void Clear();

        // Export
        bool ExportToJson(string filePath);
    }
}
