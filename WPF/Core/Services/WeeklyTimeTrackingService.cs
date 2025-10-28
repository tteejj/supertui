using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using SuperTUI.Core.Interfaces;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Service for managing weekly time entries (Mon-Fri workweek pattern)
    /// Thread-safe singleton service with JSON persistence
    /// </summary>
    public class WeeklyTimeTrackingService : IWeeklyTimeTrackingService, IDisposable
    {
        private static WeeklyTimeTrackingService instance;
        public static WeeklyTimeTrackingService Instance => instance ??= new WeeklyTimeTrackingService();

        private Dictionary<Guid, WeeklyTimeEntry> entries;
        private Dictionary<string, List<Guid>> weekIndex;  // WeekEndingFriday -> List of entry IDs
        private string dataFilePath;
        private readonly object lockObject = new object();

        // Save debouncing
        private Timer saveTimer;
        private bool pendingSave = false;
        private const int SAVE_DEBOUNCE_MS = 500;

        // Events for weekly time entry changes
        public event Action<WeeklyTimeEntry> EntryAdded;
        public event Action<WeeklyTimeEntry> EntryUpdated;
        public event Action<Guid> EntryDeleted;
        public event Action EntriesReloaded;

        private WeeklyTimeTrackingService()
        {
            entries = new Dictionary<Guid, WeeklyTimeEntry>();
            weekIndex = new Dictionary<string, List<Guid>>();
        }

        /// <summary>
        /// Initialize service with data file path
        /// </summary>
        public void Initialize(string filePath = null)
        {
            dataFilePath = filePath ?? Path.Combine(
                Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(),
                "weekly_time_entries.json");

            Logger.Instance?.Info("WeeklyTimeTrackingService", $"Initializing with data file: {dataFilePath}");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(dataFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Logger.Instance?.Info("WeeklyTimeTrackingService", $"Created data directory: {directory}");
            }

            // Initialize save timer
            saveTimer = new Timer(SaveTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Load existing entries
            LoadFromFile();
        }

        #region CRUD Operations

        /// <summary>
        /// Add new weekly time entry
        /// </summary>
        public WeeklyTimeEntry AddEntry(WeeklyTimeEntry entry)
        {
            lock (lockObject)
            {
                if (entry.Id == Guid.Empty)
                    entry.Id = Guid.NewGuid();

                entry.CreatedAt = DateTime.Now;
                entry.UpdatedAt = DateTime.Now;

                // Validate before adding
                var validationErrors = entry.Validate();
                if (validationErrors.Any())
                {
                    var errorMsg = string.Join(", ", validationErrors);
                    Logger.Instance?.Warning("WeeklyTimeTrackingService", $"Validation failed: {errorMsg}");
                    throw new ArgumentException($"Validation failed: {errorMsg}");
                }

                // Check for duplicates
                if (HasDuplicate(entry.WeekEndingFriday, entry.ID1, entry.ID2))
                {
                    Logger.Instance?.Warning("WeeklyTimeTrackingService",
                        $"Duplicate entry for week {entry.WeekEndingFriday:yyyy-MM-dd}, ID1={entry.ID1}, ID2={entry.ID2}");
                    throw new InvalidOperationException($"Duplicate entry for week {entry.WeekEndingFriday:yyyy-MM-dd} with codes {entry.ID1}/{entry.ID2}");
                }

                entries[entry.Id] = entry;
                AddToWeekIndex(entry);

                ScheduleSave();
                EntryAdded?.Invoke(entry);

                Logger.Instance?.Info("WeeklyTimeTrackingService",
                    $"Added weekly time entry: {entry.WeekEndingFriday:yyyy-MM-dd} {entry.ID1}/{entry.ID2} - {entry.TotalHours}h");

                return entry;
            }
        }

        /// <summary>
        /// Update existing weekly time entry
        /// </summary>
        public bool UpdateEntry(WeeklyTimeEntry entry)
        {
            lock (lockObject)
            {
                if (!entries.ContainsKey(entry.Id))
                {
                    Logger.Instance?.Warning("WeeklyTimeTrackingService", $"Entry not found: {entry.Id}");
                    return false;
                }

                var oldEntry = entries[entry.Id];
                entry.UpdatedAt = DateTime.Now;

                // Validate before updating
                var validationErrors = entry.Validate();
                if (validationErrors.Any())
                {
                    var errorMsg = string.Join(", ", validationErrors);
                    Logger.Instance?.Warning("WeeklyTimeTrackingService", $"Validation failed: {errorMsg}");
                    throw new ArgumentException($"Validation failed: {errorMsg}");
                }

                // Check for duplicates (excluding current entry)
                if (HasDuplicate(entry.WeekEndingFriday, entry.ID1, entry.ID2, entry.Id))
                {
                    Logger.Instance?.Warning("WeeklyTimeTrackingService",
                        $"Duplicate entry for week {entry.WeekEndingFriday:yyyy-MM-dd}, ID1={entry.ID1}, ID2={entry.ID2}");
                    throw new InvalidOperationException($"Duplicate entry for week {entry.WeekEndingFriday:yyyy-MM-dd} with codes {entry.ID1}/{entry.ID2}");
                }

                // Update week index if week changed
                if (oldEntry.WeekEndingFriday != entry.WeekEndingFriday)
                {
                    RemoveFromWeekIndex(oldEntry);
                    AddToWeekIndex(entry);
                }

                entries[entry.Id] = entry;

                ScheduleSave();
                EntryUpdated?.Invoke(entry);

                Logger.Instance?.Info("WeeklyTimeTrackingService",
                    $"Updated weekly time entry: {entry.WeekEndingFriday:yyyy-MM-dd} {entry.ID1}/{entry.ID2}");

                return true;
            }
        }

        /// <summary>
        /// Delete weekly time entry
        /// </summary>
        public bool DeleteEntry(Guid id, bool hardDelete = false)
        {
            lock (lockObject)
            {
                if (!entries.ContainsKey(id))
                {
                    Logger.Instance?.Warning("WeeklyTimeTrackingService", $"Entry not found: {id}");
                    return false;
                }

                var entry = entries[id];

                if (hardDelete)
                {
                    entries.Remove(id);
                    RemoveFromWeekIndex(entry);
                    Logger.Instance?.Info("WeeklyTimeTrackingService", $"Hard deleted weekly time entry: {id}");
                }
                else
                {
                    entry.Deleted = true;
                    entry.UpdatedAt = DateTime.Now;
                    Logger.Instance?.Info("WeeklyTimeTrackingService", $"Soft deleted weekly time entry: {id}");
                }

                ScheduleSave();
                EntryDeleted?.Invoke(id);

                return true;
            }
        }

        /// <summary>
        /// Get weekly time entry by ID
        /// </summary>
        public WeeklyTimeEntry GetEntry(Guid id)
        {
            lock (lockObject)
            {
                return entries.TryGetValue(id, out var entry) ? entry : null;
            }
        }

        /// <summary>
        /// Get all weekly time entries (excluding deleted unless includeDeleted is true)
        /// </summary>
        public List<WeeklyTimeEntry> GetAllEntries(bool includeDeleted = false)
        {
            lock (lockObject)
            {
                var query = entries.Values.AsEnumerable();
                if (!includeDeleted)
                    query = query.Where(e => !e.Deleted);

                return query
                    .OrderByDescending(e => e.WeekEndingFriday)
                    .ToList();
            }
        }

        /// <summary>
        /// Get weekly time entries by filter predicate
        /// </summary>
        public List<WeeklyTimeEntry> GetEntries(Func<WeeklyTimeEntry, bool> predicate)
        {
            lock (lockObject)
            {
                return entries.Values
                    .Where(e => !e.Deleted)
                    .Where(predicate)
                    .OrderByDescending(e => e.WeekEndingFriday)
                    .ToList();
            }
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Get entries for a specific week (Friday date)
        /// </summary>
        public List<WeeklyTimeEntry> GetEntriesForWeek(DateTime weekEndingFriday)
        {
            lock (lockObject)
            {
                var weekKey = weekEndingFriday.Date.ToString("yyyy-MM-dd");
                if (!weekIndex.ContainsKey(weekKey))
                    return new List<WeeklyTimeEntry>();

                return weekIndex[weekKey]
                    .Select(id => entries.ContainsKey(id) ? entries[id] : null)
                    .Where(e => e != null && !e.Deleted)
                    .OrderBy(e => e.ID1)
                    .ThenBy(e => e.ID2)
                    .ToList();
            }
        }

        /// <summary>
        /// Get entries for a specific project
        /// </summary>
        public List<WeeklyTimeEntry> GetEntriesForProject(Guid projectId)
        {
            lock (lockObject)
            {
                return entries.Values
                    .Where(e => !e.Deleted && e.ProjectId == projectId)
                    .OrderByDescending(e => e.WeekEndingFriday)
                    .ToList();
            }
        }

        /// <summary>
        /// Get entries for a specific task
        /// </summary>
        public List<WeeklyTimeEntry> GetEntriesForTask(Guid taskId)
        {
            lock (lockObject)
            {
                return entries.Values
                    .Where(e => !e.Deleted && e.TaskId.HasValue && e.TaskId.Value == taskId)
                    .OrderByDescending(e => e.WeekEndingFriday)
                    .ToList();
            }
        }

        /// <summary>
        /// Get entries for a specific fiscal year
        /// </summary>
        public List<WeeklyTimeEntry> GetEntriesForFiscalYear(string fiscalYear)
        {
            lock (lockObject)
            {
                return entries.Values
                    .Where(e => !e.Deleted && e.FiscalYear == fiscalYear)
                    .OrderByDescending(e => e.WeekEndingFriday)
                    .ToList();
            }
        }

        /// <summary>
        /// Get entry by week and ID1/ID2 codes
        /// </summary>
        public WeeklyTimeEntry GetEntryByCode(DateTime weekEndingFriday, string id1, string id2)
        {
            lock (lockObject)
            {
                var weekKey = weekEndingFriday.Date.ToString("yyyy-MM-dd");
                if (!weekIndex.ContainsKey(weekKey))
                    return null;

                return weekIndex[weekKey]
                    .Select(id => entries.ContainsKey(id) ? entries[id] : null)
                    .FirstOrDefault(e => e != null && !e.Deleted &&
                        string.Equals(e.ID1, id1, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(e.ID2, id2, StringComparison.OrdinalIgnoreCase));
            }
        }

        #endregion

        #region Duplicate Detection

        /// <summary>
        /// Check if a duplicate entry exists for the given week and codes
        /// </summary>
        public bool HasDuplicate(DateTime weekEndingFriday, string id1, string id2, Guid? excludeId = null)
        {
            lock (lockObject)
            {
                var existing = GetEntryByCode(weekEndingFriday, id1, id2);
                if (existing == null)
                    return false;

                // If excludeId is provided, check if the found entry is not the one being excluded
                return !excludeId.HasValue || existing.Id != excludeId.Value;
            }
        }

        #endregion

        #region Aggregation

        /// <summary>
        /// Get total hours for a specific week
        /// </summary>
        public decimal GetTotalHoursForWeek(DateTime weekEndingFriday)
        {
            lock (lockObject)
            {
                return GetEntriesForWeek(weekEndingFriday).Sum(e => e.TotalHours);
            }
        }

        /// <summary>
        /// Get total hours for a specific project
        /// </summary>
        public decimal GetTotalHoursForProject(Guid projectId)
        {
            lock (lockObject)
            {
                return GetEntriesForProject(projectId).Sum(e => e.TotalHours);
            }
        }

        /// <summary>
        /// Get total hours for a fiscal year
        /// </summary>
        public decimal GetTotalHoursForFiscalYear(string fiscalYear)
        {
            lock (lockObject)
            {
                return GetEntriesForFiscalYear(fiscalYear).Sum(e => e.TotalHours);
            }
        }

        /// <summary>
        /// Get category breakdown (ID1) for a specific week
        /// </summary>
        public Dictionary<string, decimal> GetCategoryBreakdown(DateTime weekEndingFriday)
        {
            lock (lockObject)
            {
                return GetEntriesForWeek(weekEndingFriday)
                    .GroupBy(e => e.ID1)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.TotalHours));
            }
        }

        #endregion

        #region Conversion

        /// <summary>
        /// Convert TimeEntry records to WeeklyTimeEntry records
        /// Groups by week and ID1/ID2, aggregating daily hours
        /// </summary>
        public List<WeeklyTimeEntry> ConvertFromTimeEntries(List<TimeEntry> timeEntries)
        {
            var weeklyEntries = new List<WeeklyTimeEntry>();

            // Get Friday of the week for each TimeEntry
            var grouped = timeEntries
                .Where(te => !te.Deleted)
                .GroupBy(te => new
                {
                    WeekEndingFriday = GetFridayOfWeek(te.WeekEnding),
                    te.ID1,
                    te.ID2,
                    te.ProjectId,
                    te.TaskId
                });

            foreach (var group in grouped)
            {
                var entry = new WeeklyTimeEntry
                {
                    WeekEndingFriday = group.Key.WeekEndingFriday,
                    ID1 = group.Key.ID1 ?? "PROJ",
                    ID2 = group.Key.ID2 ?? "",
                    ProjectId = group.Key.ProjectId,
                    TaskId = group.Key.TaskId,
                    Description = group.First().Description
                };

                // Aggregate daily hours
                foreach (var timeEntry in group)
                {
                    if (timeEntry.MondayHours.HasValue) entry.MondayHours += timeEntry.MondayHours.Value;
                    if (timeEntry.TuesdayHours.HasValue) entry.TuesdayHours += timeEntry.TuesdayHours.Value;
                    if (timeEntry.WednesdayHours.HasValue) entry.WednesdayHours += timeEntry.WednesdayHours.Value;
                    if (timeEntry.ThursdayHours.HasValue) entry.ThursdayHours += timeEntry.ThursdayHours.Value;
                    if (timeEntry.FridayHours.HasValue) entry.FridayHours += timeEntry.FridayHours.Value;
                }

                weeklyEntries.Add(entry);
            }

            return weeklyEntries;
        }

        private DateTime GetFridayOfWeek(DateTime date)
        {
            var daysUntilFriday = ((int)DayOfWeek.Friday - (int)date.DayOfWeek + 7) % 7;
            if (daysUntilFriday == 0 && date.DayOfWeek != DayOfWeek.Friday)
                daysUntilFriday = 7;
            return date.AddDays(daysUntilFriday).Date;
        }

        #endregion

        #region Week Index Management

        private void AddToWeekIndex(WeeklyTimeEntry entry)
        {
            var weekKey = entry.WeekEndingFriday.Date.ToString("yyyy-MM-dd");
            if (!weekIndex.ContainsKey(weekKey))
                weekIndex[weekKey] = new List<Guid>();

            if (!weekIndex[weekKey].Contains(entry.Id))
                weekIndex[weekKey].Add(entry.Id);
        }

        private void RemoveFromWeekIndex(WeeklyTimeEntry entry)
        {
            var weekKey = entry.WeekEndingFriday.Date.ToString("yyyy-MM-dd");
            if (weekIndex.ContainsKey(weekKey))
            {
                weekIndex[weekKey].Remove(entry.Id);
                if (weekIndex[weekKey].Count == 0)
                    weekIndex.Remove(weekKey);
            }
        }

        private void RebuildWeekIndex()
        {
            weekIndex.Clear();
            foreach (var entry in entries.Values.Where(e => !e.Deleted))
            {
                AddToWeekIndex(entry);
            }
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Save to file (async with debouncing)
        /// </summary>
        public bool SaveToFile()
        {
            ScheduleSave();
            return true;
        }

        private void ScheduleSave()
        {
            lock (lockObject)
            {
                pendingSave = true;
                saveTimer?.Change(SAVE_DEBOUNCE_MS, Timeout.Infinite);
            }
        }

        private void SaveTimerCallback(object state)
        {
            lock (lockObject)
            {
                if (pendingSave)
                {
                    SaveToFileSync();
                    pendingSave = false;
                }
            }
        }

        private bool SaveToFileSync()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(entries.Values.ToList(), options);
                File.WriteAllText(dataFilePath, json);

                Logger.Instance?.Debug("WeeklyTimeTrackingService", $"Saved {entries.Count} weekly entries to {dataFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("WeeklyTimeTrackingService", $"Failed to save entries: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Load from file
        /// </summary>
        public bool LoadFromFile()
        {
            lock (lockObject)
            {
                try
                {
                    if (!File.Exists(dataFilePath))
                    {
                        Logger.Instance?.Info("WeeklyTimeTrackingService", "No existing data file found, starting with empty collection");
                        return true;
                    }

                    var json = File.ReadAllText(dataFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var loadedEntries = JsonSerializer.Deserialize<List<WeeklyTimeEntry>>(json, options);

                    entries.Clear();
                    weekIndex.Clear();

                    foreach (var entry in loadedEntries)
                    {
                        entries[entry.Id] = entry;
                        AddToWeekIndex(entry);
                    }

                    Logger.Instance?.Info("WeeklyTimeTrackingService", $"Loaded {entries.Count} weekly entries from {dataFilePath}");
                    EntriesReloaded?.Invoke();

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance?.Error("WeeklyTimeTrackingService", $"Failed to load entries: {ex.Message}", ex);
                    return false;
                }
            }
        }

        #endregion

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (saveTimer != null)
            {
                // Ensure any pending save is executed before disposal
                if (pendingSave)
                {
                    SaveToFileSync();
                }

                saveTimer.Dispose();
                saveTimer = null;
            }
        }
    }
}
