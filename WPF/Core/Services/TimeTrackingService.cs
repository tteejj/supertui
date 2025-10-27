using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Time tracking service for managing project time entries with JSON persistence
    /// Thread-safe singleton service with week-based indexing
    /// </summary>
    public class TimeTrackingService : ITimeTrackingService, IDisposable
    {
        private static TimeTrackingService instance;
        public static TimeTrackingService Instance => instance ??= new TimeTrackingService();

        private Dictionary<Guid, TimeEntry> entries;
        private Dictionary<string, List<Guid>> weekIndex; // WeekEnding date string -> List of entry IDs
        private string dataFilePath;
        private readonly object lockObject = new object();

        // Save debouncing
        private Timer saveTimer;
        private bool pendingSave = false;
        private const int SAVE_DEBOUNCE_MS = 500;

        // Events for time entry changes
        public event Action<TimeEntry> EntryAdded;
        public event Action<TimeEntry> EntryUpdated;
        public event Action<Guid> EntryDeleted;
        public event Action EntriesReloaded;

        private TimeTrackingService()
        {
            entries = new Dictionary<Guid, TimeEntry>();
            weekIndex = new Dictionary<string, List<Guid>>();
        }

        /// <summary>
        /// Initialize service with data file path
        /// </summary>
        public void Initialize(string filePath = null)
        {
            dataFilePath = filePath ?? Path.Combine(
                Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(),
                "timetracking.json");

            Logger.Instance?.Info("TimeTrackingService", $"Initializing with data file: {dataFilePath}");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(dataFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Logger.Instance?.Info("TimeTrackingService", $"Created data directory: {directory}");
            }

            // Initialize save timer
            saveTimer = new Timer(SaveTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Load existing entries
            LoadFromFile();
        }

        #region Week Calculation Helpers

        /// <summary>
        /// Get the week-ending date (Sunday) for a given date
        /// </summary>
        public static DateTime GetWeekEnding(DateTime date)
        {
            // Find the next Sunday (or today if already Sunday)
            int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)date.DayOfWeek + 7) % 7;
            return date.Date.AddDays(daysUntilSunday);
        }

        /// <summary>
        /// Get the current week-ending date (this Sunday)
        /// </summary>
        public static DateTime GetCurrentWeekEnding()
        {
            return GetWeekEnding(DateTime.Now);
        }

        /// <summary>
        /// Get the week-start date (Monday) for a given week-ending date
        /// </summary>
        public static DateTime GetWeekStart(DateTime weekEnding)
        {
            return weekEnding.AddDays(-6);
        }

        /// <summary>
        /// Get fiscal year for a given date (Apr 1 - Mar 31)
        /// </summary>
        public static int GetFiscalYear(DateTime date)
        {
            // Fiscal year starts April 1
            // If date is Jan-Mar, fiscal year is current year
            // If date is Apr-Dec, fiscal year is next year
            return date.Month >= 4 ? date.Year + 1 : date.Year;
        }

        /// <summary>
        /// Get fiscal year start date
        /// </summary>
        public static DateTime GetFiscalYearStart(int fiscalYear)
        {
            return new DateTime(fiscalYear - 1, 4, 1);
        }

        /// <summary>
        /// Get fiscal year end date
        /// </summary>
        public static DateTime GetFiscalYearEnd(int fiscalYear)
        {
            return new DateTime(fiscalYear, 3, 31);
        }

        /// <summary>
        /// Get current fiscal year
        /// </summary>
        public static int GetCurrentFiscalYear()
        {
            return GetFiscalYear(DateTime.Now);
        }

        #endregion

        #region CRUD Operations

        /// <summary>
        /// Get all time entries (excluding deleted unless includeDeleted is true)
        /// </summary>
        public List<TimeEntry> GetAllEntries(bool includeDeleted = false)
        {
            lock (lockObject)
            {
                return entries.Values
                    .Where(e => includeDeleted || !e.Deleted)
                    .OrderByDescending(e => e.WeekEnding)
                    .ThenBy(e => e.ProjectId)
                    .ToList();
            }
        }

        /// <summary>
        /// Get time entries by filter predicate
        /// </summary>
        public List<TimeEntry> GetEntries(Func<TimeEntry, bool> predicate)
        {
            lock (lockObject)
            {
                return entries.Values
                    .Where(predicate)
                    .OrderByDescending(e => e.WeekEnding)
                    .ThenBy(e => e.ProjectId)
                    .ToList();
            }
        }

        /// <summary>
        /// Get time entry by ID
        /// </summary>
        public TimeEntry GetEntry(Guid id)
        {
            lock (lockObject)
            {
                return entries.ContainsKey(id) ? entries[id] : null;
            }
        }

        /// <summary>
        /// Get time entries for a specific week
        /// </summary>
        public List<TimeEntry> GetEntriesForWeek(DateTime weekEnding)
        {
            lock (lockObject)
            {
                var weekKey = weekEnding.Date.ToString("yyyy-MM-dd");
                if (!weekIndex.ContainsKey(weekKey))
                    return new List<TimeEntry>();

                return weekIndex[weekKey]
                    .Select(id => entries.ContainsKey(id) ? entries[id] : null)
                    .Where(e => e != null && !e.Deleted)
                    .ToList();
            }
        }

        /// <summary>
        /// Get time entries for a specific project
        /// </summary>
        public List<TimeEntry> GetEntriesForProject(Guid projectId)
        {
            lock (lockObject)
            {
                return entries.Values
                    .Where(e => !e.Deleted && e.ProjectId == projectId)
                    .OrderByDescending(e => e.WeekEnding)
                    .ToList();
            }
        }

        /// <summary>
        /// Get time entry for a specific project and week (or null if doesn't exist)
        /// </summary>
        public TimeEntry GetEntryForProjectAndWeek(Guid projectId, DateTime weekEnding)
        {
            lock (lockObject)
            {
                var weekKey = weekEnding.Date.ToString("yyyy-MM-dd");
                if (!weekIndex.ContainsKey(weekKey))
                    return null;

                return weekIndex[weekKey]
                    .Select(id => entries.ContainsKey(id) ? entries[id] : null)
                    .FirstOrDefault(e => e != null && !e.Deleted && e.ProjectId == projectId);
            }
        }

        /// <summary>
        /// Add new time entry
        /// </summary>
        public TimeEntry AddEntry(TimeEntry entry)
        {
            lock (lockObject)
            {
                entry.Id = Guid.NewGuid();
                entry.CreatedAt = DateTime.Now;
                entry.UpdatedAt = DateTime.Now;
                entry.Deleted = false;

                // Ensure week ending is normalized to Sunday
                entry.WeekEnding = GetWeekEnding(entry.WeekEnding).Date;

                entries[entry.Id] = entry;

                // Update week index
                var weekKey = entry.WeekEnding.ToString("yyyy-MM-dd");
                if (!weekIndex.ContainsKey(weekKey))
                    weekIndex[weekKey] = new List<Guid>();
                weekIndex[weekKey].Add(entry.Id);

                ScheduleSave();
                EntryAdded?.Invoke(entry);

                Logger.Instance?.Info("TimeTrackingService", $"Added time entry: {entry.Hours}h for project {entry.ProjectId} (Week: {entry.WeekEnding:yyyy-MM-dd})");
                return entry;
            }
        }

        /// <summary>
        /// Update existing time entry
        /// </summary>
        public bool UpdateEntry(TimeEntry entry)
        {
            lock (lockObject)
            {
                if (!entries.ContainsKey(entry.Id))
                {
                    Logger.Instance?.Warning("TimeTrackingService", $"Cannot update non-existent entry: {entry.Id}");
                    return false;
                }

                var oldEntry = entries[entry.Id];

                // Handle week ending change
                if (oldEntry.WeekEnding.Date != entry.WeekEnding.Date)
                {
                    // Remove from old week index
                    var oldWeekKey = oldEntry.WeekEnding.ToString("yyyy-MM-dd");
                    if (weekIndex.ContainsKey(oldWeekKey))
                        weekIndex[oldWeekKey].Remove(entry.Id);

                    // Ensure new week ending is normalized to Sunday
                    entry.WeekEnding = GetWeekEnding(entry.WeekEnding).Date;

                    // Add to new week index
                    var newWeekKey = entry.WeekEnding.ToString("yyyy-MM-dd");
                    if (!weekIndex.ContainsKey(newWeekKey))
                        weekIndex[newWeekKey] = new List<Guid>();
                    weekIndex[newWeekKey].Add(entry.Id);
                }

                entry.UpdatedAt = DateTime.Now;
                entries[entry.Id] = entry;

                ScheduleSave();
                EntryUpdated?.Invoke(entry);

                Logger.Instance?.Debug("TimeTrackingService", $"Updated time entry: {entry.Id}");
                return true;
            }
        }

        /// <summary>
        /// Delete time entry (soft delete by default)
        /// </summary>
        public bool DeleteEntry(Guid id, bool hardDelete = false)
        {
            lock (lockObject)
            {
                if (!entries.ContainsKey(id))
                {
                    Logger.Instance?.Warning("TimeTrackingService", $"Cannot delete non-existent entry: {id}");
                    return false;
                }

                var entry = entries[id];

                if (hardDelete)
                {
                    // Remove from week index
                    var weekKey = entry.WeekEnding.ToString("yyyy-MM-dd");
                    if (weekIndex.ContainsKey(weekKey))
                        weekIndex[weekKey].Remove(id);

                    entries.Remove(id);
                }
                else
                {
                    // Soft delete
                    entry.Deleted = true;
                    entry.UpdatedAt = DateTime.Now;
                }

                ScheduleSave();
                EntryDeleted?.Invoke(id);

                Logger.Instance?.Info("TimeTrackingService", $"Deleted time entry: {id} (Hard: {hardDelete})");
                return true;
            }
        }

        #endregion

        #region Aggregation & Reporting

        /// <summary>
        /// Get total hours for a project
        /// </summary>
        public decimal GetProjectTotalHours(Guid projectId)
        {
            lock (lockObject)
            {
                return entries.Values
                    .Where(e => !e.Deleted && e.ProjectId == projectId)
                    .Sum(e => e.TotalHours);
            }
        }

        /// <summary>
        /// Get total hours for a specific week
        /// </summary>
        public decimal GetWeekTotalHours(DateTime weekEnding)
        {
            lock (lockObject)
            {
                var weekEntries = GetEntriesForWeek(weekEnding);
                return weekEntries.Sum(e => e.TotalHours);
            }
        }

        /// <summary>
        /// Get weekly time report
        /// </summary>
        public WeeklyTimeReport GetWeeklyReport(DateTime weekEnding)
        {
            lock (lockObject)
            {
                var weekEntries = GetEntriesForWeek(GetWeekEnding(weekEnding));
                return new WeeklyTimeReport
                {
                    WeekEnding = GetWeekEnding(weekEnding),
                    Entries = weekEntries
                };
            }
        }

        /// <summary>
        /// Get project time aggregate for a date range
        /// </summary>
        public ProjectTimeAggregate GetProjectAggregate(Guid projectId, DateTime? startDate = null, DateTime? endDate = null)
        {
            lock (lockObject)
            {
                var projectEntries = entries.Values
                    .Where(e => !e.Deleted && e.ProjectId == projectId);

                if (startDate.HasValue)
                    projectEntries = projectEntries.Where(e => e.WeekEnding >= startDate.Value.Date);
                if (endDate.HasValue)
                    projectEntries = projectEntries.Where(e => e.WeekEnding <= endDate.Value.Date);

                var entriesList = projectEntries.ToList();

                var project = ProjectService.Instance?.GetProject(projectId);

                return new ProjectTimeAggregate
                {
                    ProjectId = projectId,
                    ProjectName = project?.Name ?? "Unknown Project",
                    TotalHours = entriesList.Sum(e => e.TotalHours),
                    EntryCount = entriesList.Count,
                    WeekCount = entriesList.Select(e => e.WeekEnding).Distinct().Count(),
                    FirstEntry = entriesList.Any() ? entriesList.Min(e => e.WeekEnding) : (DateTime?)null,
                    LastEntry = entriesList.Any() ? entriesList.Max(e => e.WeekEnding) : (DateTime?)null
                };
            }
        }

        /// <summary>
        /// Get all project aggregates for a date range
        /// </summary>
        public List<ProjectTimeAggregate> GetAllProjectAggregates(DateTime? startDate = null, DateTime? endDate = null)
        {
            lock (lockObject)
            {
                var projectIds = entries.Values
                    .Where(e => !e.Deleted)
                    .Select(e => e.ProjectId)
                    .Distinct()
                    .ToList();

                return projectIds.Select(pid => GetProjectAggregate(pid, startDate, endDate)).ToList();
            }
        }

        /// <summary>
        /// Get fiscal year summary
        /// </summary>
        public FiscalYearSummary GetFiscalYearSummary(int fiscalYear)
        {
            lock (lockObject)
            {
                var startDate = GetFiscalYearStart(fiscalYear);
                var endDate = GetFiscalYearEnd(fiscalYear);

                var fyEntries = entries.Values
                    .Where(e => !e.Deleted && e.WeekEnding >= startDate && e.WeekEnding <= endDate)
                    .ToList();

                return new FiscalYearSummary
                {
                    FiscalYear = fiscalYear,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalHours = fyEntries.Sum(e => e.TotalHours),
                    ProjectCount = fyEntries.Select(e => e.ProjectId).Distinct().Count(),
                    EntryCount = fyEntries.Count,
                    WeekCount = fyEntries.Select(e => e.WeekEnding).Distinct().Count()
                };
            }
        }

        /// <summary>
        /// Get current fiscal year summary
        /// </summary>
        public FiscalYearSummary GetCurrentFiscalYearSummary()
        {
            return GetFiscalYearSummary(GetCurrentFiscalYear());
        }

        #endregion

        #region Persistence

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
        /// Save time entries to JSON file asynchronously
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
                    var backupFiles = Directory.GetFiles(backupDir, "timetracking.json.*.bak")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .Skip(5)
                        .ToList();

                    foreach (var oldBackup in backupFiles)
                    {
                        try
                        {
                            File.Delete(oldBackup);
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance?.Warning("TimeTrackingService", $"Failed to delete old backup {oldBackup}: {ex.Message}");
                        }
                    }
                }

                List<TimeEntry> entryList;
                lock (lockObject)
                {
                    entryList = entries.Values.ToList();
                }

                var json = await Task.Run(() => JsonSerializer.Serialize(entryList, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

                // Atomic write: temp → rename pattern
                string tempFile = dataFilePath + ".tmp";
                await Task.Run(() => File.WriteAllText(tempFile, json));
                await Task.Run(() => File.Replace(tempFile, dataFilePath, dataFilePath + ".bak"));
                Logger.Instance?.Debug("TimeTrackingService", $"Saved {entryList.Count} time entries to {dataFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("TimeTrackingService", $"Failed to save time entries: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Save time entries to JSON file synchronously (for Dispose)
        /// </summary>
        private void SaveToFileSync()
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
                    var backupFiles = Directory.GetFiles(backupDir, "timetracking.json.*.bak")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .Skip(5)
                        .ToList();

                    foreach (var oldBackup in backupFiles)
                    {
                        try
                        {
                            File.Delete(oldBackup);
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance?.Warning("TimeTrackingService", $"Failed to delete old backup {oldBackup}: {ex.Message}");
                        }
                    }
                }

                List<TimeEntry> entryList;
                lock (lockObject)
                {
                    entryList = entries.Values.ToList();
                }

                var json = JsonSerializer.Serialize(entryList, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Atomic write: temp → rename pattern
                string tempFile = dataFilePath + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Replace(tempFile, dataFilePath, dataFilePath + ".bak");
                Logger.Instance?.Debug("TimeTrackingService", $"Saved {entryList.Count} time entries to {dataFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("TimeTrackingService", $"Failed to save time entries: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Load time entries from JSON file
        /// </summary>
        private void LoadFromFile()
        {
            try
            {
                if (!File.Exists(dataFilePath))
                {
                    Logger.Instance?.Info("TimeTrackingService", "No existing time tracking file found, starting fresh");
                    return;
                }

                var json = File.ReadAllText(dataFilePath);
                var loadedEntries = JsonSerializer.Deserialize<List<TimeEntry>>(json);

                lock (lockObject)
                {
                    entries.Clear();
                    weekIndex.Clear();

                    foreach (var entry in loadedEntries)
                    {
                        entries[entry.Id] = entry;

                        // Rebuild week index
                        var weekKey = entry.WeekEnding.Date.ToString("yyyy-MM-dd");
                        if (!weekIndex.ContainsKey(weekKey))
                            weekIndex[weekKey] = new List<Guid>();
                        weekIndex[weekKey].Add(entry.Id);
                    }
                }

                Logger.Instance?.Info("TimeTrackingService", $"Loaded {entries.Count} time entries from {dataFilePath}");
                EntriesReloaded?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("TimeTrackingService", $"Failed to load time entries: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reload time entries from file (useful for external changes)
        /// </summary>
        public void Reload()
        {
            LoadFromFile();
        }

        /// <summary>
        /// Clear all time entries (for testing)
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                entries.Clear();
                weekIndex.Clear();
                ScheduleSave();
                EntriesReloaded?.Invoke();
                Logger.Instance?.Info("TimeTrackingService", "Cleared all time entries");
            }
        }

        #endregion

        #region Export

        /// <summary>
        /// Export time entries to JSON format
        /// </summary>
        public bool ExportToJson(string filePath)
        {
            try
            {
                lock (lockObject)
                {
                    var allEntries = entries.Values.Where(e => !e.Deleted).ToList();
                    var json = JsonSerializer.Serialize(allEntries, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    // Atomic write: temp → rename pattern
                    string tempFile = filePath + ".tmp";
                    File.WriteAllText(tempFile, json);
                    File.Replace(tempFile, filePath, filePath + ".bak");
                    Logger.Instance?.Info("TimeTrackingService", $"Exported {allEntries.Count} time entries to JSON: {filePath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("TimeTrackingService", $"Failed to export to JSON: {ex.Message}", ex);
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
                    SaveToFileSync();  // Use synchronous save to avoid deadlock
                }

                saveTimer.Dispose();
                saveTimer = null;
            }
        }
    }
}
