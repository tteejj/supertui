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
        public DateTime WeekEnding { get; set; }  // Sunday date for the week
        public decimal Hours { get; set; }
        public string Description { get; set; }
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
                WeekEnding = this.WeekEnding,
                Hours = this.Hours,
                Description = this.Description,
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
}
