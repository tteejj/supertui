using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperTUI.Core.Models
{
    /// <summary>
    /// Time entry for project time tracking
    /// Uses week-ending date as primary grouping
    /// </summary>
    public class TimeEntry
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? TaskId { get; set; }  // Optional task linkage
        public DateTime WeekEnding { get; set; }  // Sunday date for the week
        public decimal Hours { get; set; }
        public string Description { get; set; }

        // Time allocation codes (for Excel integration and categorization)
        public string ID1 { get; set; }  // Category code (PROJ, MEET, TRAIN, ADMIN) - max 20 chars
        public string ID2 { get; set; }  // Project/Task code - max 20 chars

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Deleted { get; set; }

        // Week breakdown (optional detailed tracking)
        public decimal? MondayHours { get; set; }
        public decimal? TuesdayHours { get; set; }
        public decimal? WednesdayHours { get; set; }
        public decimal? ThursdayHours { get; set; }
        public decimal? FridayHours { get; set; }
        public decimal? SaturdayHours { get; set; }
        public decimal? SundayHours { get; set; }

        public TimeEntry()
        {
            Id = Guid.NewGuid();
            ProjectId = Guid.Empty;
            WeekEnding = DateTime.Now;
            Hours = 0;
            Description = string.Empty;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            Deleted = false;
        }

        /// <summary>
        /// Calculate total hours from daily breakdown
        /// </summary>
        public decimal TotalHours
        {
            get
            {
                if (!MondayHours.HasValue && !TuesdayHours.HasValue && !WednesdayHours.HasValue &&
                    !ThursdayHours.HasValue && !FridayHours.HasValue && !SaturdayHours.HasValue && !SundayHours.HasValue)
                {
                    return Hours; // Use summary hours if no breakdown
                }

                return (MondayHours ?? 0) + (TuesdayHours ?? 0) + (WednesdayHours ?? 0) +
                       (ThursdayHours ?? 0) + (FridayHours ?? 0) + (SaturdayHours ?? 0) + (SundayHours ?? 0);
            }
        }

        /// <summary>
        /// Get week start date (Monday)
        /// </summary>
        public DateTime WeekStart
        {
            get
            {
                // Week ending is Sunday, so week start is 6 days earlier
                return WeekEnding.AddDays(-6);
            }
        }

        /// <summary>
        /// Get fiscal year for this time entry (Apr 1 - Mar 31)
        /// </summary>
        public int FiscalYear
        {
            get
            {
                // Fiscal year starts April 1
                // If week ending is Jan-Mar, fiscal year is current year
                // If week ending is Apr-Dec, fiscal year is next year
                return WeekEnding.Month >= 4 ? WeekEnding.Year + 1 : WeekEnding.Year;
            }
        }

        /// <summary>
        /// Check if has detailed daily breakdown
        /// </summary>
        public bool HasDailyBreakdown
        {
            get
            {
                return MondayHours.HasValue || TuesdayHours.HasValue || WednesdayHours.HasValue ||
                       ThursdayHours.HasValue || FridayHours.HasValue || SaturdayHours.HasValue || SundayHours.HasValue;
            }
        }

        /// <summary>
        /// Clone this time entry
        /// </summary>
        public TimeEntry Clone()
        {
            return new TimeEntry
            {
                Id = this.Id,
                ProjectId = this.ProjectId,
                TaskId = this.TaskId,
                WeekEnding = this.WeekEnding,
                Hours = this.Hours,
                Description = this.Description,
                ID1 = this.ID1,
                ID2 = this.ID2,
                CreatedAt = this.CreatedAt,
                UpdatedAt = this.UpdatedAt,
                Deleted = this.Deleted,
                MondayHours = this.MondayHours,
                TuesdayHours = this.TuesdayHours,
                WednesdayHours = this.WednesdayHours,
                ThursdayHours = this.ThursdayHours,
                FridayHours = this.FridayHours,
                SaturdayHours = this.SaturdayHours,
                SundayHours = this.SundayHours
            };
        }

        public override string ToString()
        {
            return $"{WeekEnding:yyyy-MM-dd} - {TotalHours:F2}h";
        }
    }

    /// <summary>
    /// Weekly time report with project breakdown
    /// </summary>
    public class WeeklyTimeReport
    {
        public DateTime WeekEnding { get; set; }
        public List<TimeEntry> Entries { get; set; }

        public WeeklyTimeReport()
        {
            WeekEnding = DateTime.Now;
            Entries = new List<TimeEntry>();
        }

        /// <summary>
        /// Get total hours for the week
        /// </summary>
        public decimal TotalHours
        {
            get
            {
                return Entries.Where(e => !e.Deleted).Sum(e => e.TotalHours);
            }
        }

        /// <summary>
        /// Get number of projects worked on this week
        /// </summary>
        public int ProjectCount
        {
            get
            {
                return Entries.Where(e => !e.Deleted).Select(e => e.ProjectId).Distinct().Count();
            }
        }

        /// <summary>
        /// Get week start date (Monday)
        /// </summary>
        public DateTime WeekStart => WeekEnding.AddDays(-6);

        /// <summary>
        /// Get fiscal year for this week
        /// </summary>
        public int FiscalYear
        {
            get
            {
                return WeekEnding.Month >= 4 ? WeekEnding.Year + 1 : WeekEnding.Year;
            }
        }

        /// <summary>
        /// Group entries by project
        /// </summary>
        public Dictionary<Guid, List<TimeEntry>> GetEntriesByProject()
        {
            return Entries
                .Where(e => !e.Deleted)
                .GroupBy(e => e.ProjectId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        public override string ToString()
        {
            return $"Week of {WeekStart:yyyy-MM-dd} - {TotalHours:F2}h ({ProjectCount} projects)";
        }
    }

    /// <summary>
    /// Project time aggregate for reporting
    /// </summary>
    public class ProjectTimeAggregate
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public decimal TotalHours { get; set; }
        public DateTime? FirstEntry { get; set; }
        public DateTime? LastEntry { get; set; }
        public int EntryCount { get; set; }
        public int WeekCount { get; set; }

        public ProjectTimeAggregate()
        {
            ProjectId = Guid.Empty;
            ProjectName = string.Empty;
            TotalHours = 0;
            EntryCount = 0;
            WeekCount = 0;
        }

        /// <summary>
        /// Get average hours per week
        /// </summary>
        public decimal AverageHoursPerWeek
        {
            get
            {
                if (WeekCount == 0) return 0;
                return Math.Round(TotalHours / WeekCount, 2);
            }
        }

        /// <summary>
        /// Get average hours per entry
        /// </summary>
        public decimal AverageHoursPerEntry
        {
            get
            {
                if (EntryCount == 0) return 0;
                return Math.Round(TotalHours / EntryCount, 2);
            }
        }

        /// <summary>
        /// Get time span of entries (days)
        /// </summary>
        public int? TimeSpanDays
        {
            get
            {
                if (!FirstEntry.HasValue || !LastEntry.HasValue)
                    return null;
                return (LastEntry.Value.Date - FirstEntry.Value.Date).Days + 1;
            }
        }

        public override string ToString()
        {
            return $"{ProjectName}: {TotalHours:F2}h ({EntryCount} entries)";
        }
    }

    /// <summary>
    /// Task time session for real-time tracking
    /// </summary>
    public class TaskTimeSession
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.Now - StartTime;
        public bool IsActive => !EndTime.HasValue;
        public string Notes { get; set; }

        public TaskTimeSession()
        {
            Id = Guid.NewGuid();
            TaskId = Guid.Empty;
            StartTime = DateTime.Now;
            Notes = string.Empty;
        }

        public void Stop()
        {
            if (!EndTime.HasValue)
                EndTime = DateTime.Now;
        }

        public override string ToString()
        {
            var durationStr = EndTime.HasValue
                ? $"{Duration.TotalMinutes:F1}m"
                : $"{Duration.TotalMinutes:F1}m (active)";
            return $"{StartTime:HH:mm} - {durationStr}";
        }
    }

    /// <summary>
    /// Pomodoro session state
    /// </summary>
    public enum PomodoroPhase
    {
        Idle,
        Work,
        ShortBreak,
        LongBreak
    }

    /// <summary>
    /// Pomodoro session tracking
    /// </summary>
    public class PomodoroSession
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public PomodoroPhase Phase { get; set; }
        public DateTime StartTime { get; set; }
        public int WorkMinutes { get; set; }
        public int ShortBreakMinutes { get; set; }
        public int LongBreakMinutes { get; set; }
        public int CompletedPomodoros { get; set; }
        public bool IsActive { get; set; }

        public PomodoroSession()
        {
            Id = Guid.NewGuid();
            TaskId = Guid.Empty;
            Phase = PomodoroPhase.Idle;
            StartTime = DateTime.Now;
            WorkMinutes = 25;
            ShortBreakMinutes = 5;
            LongBreakMinutes = 15;
            CompletedPomodoros = 0;
            IsActive = false;
        }

        public TimeSpan TimeRemaining
        {
            get
            {
                var elapsed = DateTime.Now - StartTime;
                var phaseDuration = Phase switch
                {
                    PomodoroPhase.Work => TimeSpan.FromMinutes(WorkMinutes),
                    PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(ShortBreakMinutes),
                    PomodoroPhase.LongBreak => TimeSpan.FromMinutes(LongBreakMinutes),
                    _ => TimeSpan.Zero
                };

                var remaining = phaseDuration - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public bool IsPhaseComplete => TimeRemaining == TimeSpan.Zero;

        public override string ToString()
        {
            return $"{Phase}: {TimeRemaining.Minutes:D2}:{TimeRemaining.Seconds:D2} ({CompletedPomodoros} completed)";
        }
    }

    /// <summary>
    /// Fiscal year time summary
    /// </summary>
    public class FiscalYearSummary
    {
        public int FiscalYear { get; set; }
        public DateTime StartDate { get; set; }  // Apr 1
        public DateTime EndDate { get; set; }    // Mar 31
        public decimal TotalHours { get; set; }
        public int ProjectCount { get; set; }
        public int EntryCount { get; set; }
        public int WeekCount { get; set; }

        public FiscalYearSummary()
        {
            FiscalYear = DateTime.Now.Month >= 4 ? DateTime.Now.Year + 1 : DateTime.Now.Year;
            StartDate = new DateTime(FiscalYear - 1, 4, 1);
            EndDate = new DateTime(FiscalYear, 3, 31);
        }

        /// <summary>
        /// Check if fiscal year is current
        /// </summary>
        public bool IsCurrent
        {
            get
            {
                var today = DateTime.Now.Date;
                return today >= StartDate && today <= EndDate;
            }
        }

        /// <summary>
        /// Get average hours per week
        /// </summary>
        public decimal AverageHoursPerWeek
        {
            get
            {
                if (WeekCount == 0) return 0;
                return Math.Round(TotalHours / WeekCount, 2);
            }
        }

        public override string ToString()
        {
            return $"FY{FiscalYear}: {TotalHours:F2}h ({ProjectCount} projects)";
        }
    }

    /// <summary>
    /// Weekly time entry model for batch time tracking
    /// Uses Friday as week-ending date (workweek convention)
    /// </summary>
    public class WeeklyTimeEntry
    {
        public Guid Id { get; set; }
        public DateTime WeekEndingFriday { get; set; }  // Friday date (end of workweek)
        public string FiscalYear { get; set; }          // Format: "2025-2026" (Apr-Mar)

        // Time allocation codes
        public string ID1 { get; set; }                 // Category code (PROJ, MEET, TRAIN, ADMIN) - max 20 chars
        public string ID2 { get; set; }                 // Project/task code - max 20 chars
        public string Description { get; set; }         // Max 200 chars

        // Linkage (three-level hierarchy)
        public Guid? ProjectId { get; set; }            // Link to project
        public Guid? TaskId { get; set; }               // Link to task (optional)

        // Daily hours (workweek only - Mon-Fri)
        public decimal MondayHours { get; set; }        // 0.0-24.0
        public decimal TuesdayHours { get; set; }       // 0.0-24.0
        public decimal WednesdayHours { get; set; }     // 0.0-24.0
        public decimal ThursdayHours { get; set; }      // 0.0-24.0
        public decimal FridayHours { get; set; }        // 0.0-24.0

        // Metadata
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Deleted { get; set; }

        public WeeklyTimeEntry()
        {
            Id = Guid.NewGuid();
            WeekEndingFriday = GetFridayOfCurrentWeek();
            FiscalYear = CalculateFiscalYear(WeekEndingFriday);
            ID1 = string.Empty;
            ID2 = string.Empty;
            Description = string.Empty;
            MondayHours = 0;
            TuesdayHours = 0;
            WednesdayHours = 0;
            ThursdayHours = 0;
            FridayHours = 0;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            Deleted = false;
        }

        /// <summary>
        /// Calculate total hours for the week
        /// </summary>
        public decimal TotalHours =>
            MondayHours + TuesdayHours + WednesdayHours + ThursdayHours + FridayHours;

        /// <summary>
        /// Check if linked to a specific task
        /// </summary>
        public bool IsLinkedToTask => TaskId.HasValue;

        /// <summary>
        /// Get week start date (Monday)
        /// </summary>
        public DateTime WeekStart => WeekEndingFriday.AddDays(-4);

        /// <summary>
        /// Get Friday of current week
        /// </summary>
        private static DateTime GetFridayOfCurrentWeek()
        {
            var today = DateTime.Today;
            var daysUntilFriday = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
            if (daysUntilFriday == 0 && today.DayOfWeek != DayOfWeek.Friday)
                daysUntilFriday = 7;
            return today.AddDays(daysUntilFriday);
        }

        /// <summary>
        /// Calculate fiscal year from date (Apr 1 - Mar 31)
        /// </summary>
        private static string CalculateFiscalYear(DateTime date)
        {
            var fiscalYearEnd = date.Month >= 4 ? date.Year + 1 : date.Year;
            return $"{fiscalYearEnd - 1}-{fiscalYearEnd}";
        }

        /// <summary>
        /// Validate time entry
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            // ID1 required
            if (string.IsNullOrWhiteSpace(ID1))
                errors.Add("ID1 (category code) is required");

            // ID1 max 20 chars
            if (ID1?.Length > 20)
                errors.Add("ID1 must be 20 characters or less");

            // ID2 max 20 chars
            if (ID2?.Length > 20)
                errors.Add("ID2 must be 20 characters or less");

            // Description max 200 chars
            if (Description?.Length > 200)
                errors.Add("Description must be 200 characters or less");

            // Daily hours 0-24
            if (MondayHours < 0 || MondayHours > 24)
                errors.Add("Monday hours must be between 0 and 24");
            if (TuesdayHours < 0 || TuesdayHours > 24)
                errors.Add("Tuesday hours must be between 0 and 24");
            if (WednesdayHours < 0 || WednesdayHours > 24)
                errors.Add("Wednesday hours must be between 0 and 24");
            if (ThursdayHours < 0 || ThursdayHours > 24)
                errors.Add("Thursday hours must be between 0 and 24");
            if (FridayHours < 0 || FridayHours > 24)
                errors.Add("Friday hours must be between 0 and 24");

            return errors;
        }

        /// <summary>
        /// Clone this weekly time entry
        /// </summary>
        public WeeklyTimeEntry Clone()
        {
            return new WeeklyTimeEntry
            {
                Id = this.Id,
                WeekEndingFriday = this.WeekEndingFriday,
                FiscalYear = this.FiscalYear,
                ID1 = this.ID1,
                ID2 = this.ID2,
                Description = this.Description,
                ProjectId = this.ProjectId,
                TaskId = this.TaskId,
                MondayHours = this.MondayHours,
                TuesdayHours = this.TuesdayHours,
                WednesdayHours = this.WednesdayHours,
                ThursdayHours = this.ThursdayHours,
                FridayHours = this.FridayHours,
                CreatedAt = this.CreatedAt,
                UpdatedAt = this.UpdatedAt,
                Deleted = this.Deleted
            };
        }

        public override string ToString()
        {
            return $"{WeekEndingFriday:yyyy-MM-dd} - {ID1}/{ID2} - {TotalHours:F2}h";
        }
    }
}
