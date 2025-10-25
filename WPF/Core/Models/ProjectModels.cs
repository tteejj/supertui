using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperTUI.Core.Models
{
    /// <summary>
    /// Project status enumeration
    /// </summary>
    public enum ProjectStatus
    {
        Planned = 0,
        Active = 1,
        OnHold = 2,
        Completed = 3,
        Cancelled = 4
    }

    /// <summary>
    /// Audit period structure for project tracking
    /// </summary>
    public class AuditPeriod
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Description { get; set; }

        public AuditPeriod()
        {
            StartDate = DateTime.Now;
            EndDate = DateTime.Now;
            Description = string.Empty;
        }

        /// <summary>
        /// Check if the period is currently active
        /// </summary>
        public bool IsActive
        {
            get
            {
                var today = DateTime.Now.Date;
                return today >= StartDate.Date && today <= EndDate.Date;
            }
        }

        /// <summary>
        /// Get number of days in this period
        /// </summary>
        public int DurationDays => (EndDate.Date - StartDate.Date).Days + 1;

        /// <summary>
        /// Get number of days remaining (0 if past)
        /// </summary>
        public int DaysRemaining
        {
            get
            {
                var today = DateTime.Now.Date;
                if (today > EndDate.Date) return 0;
                return (EndDate.Date - today).Days;
            }
        }

        /// <summary>
        /// Get fiscal year for this audit period (Apr 1 - Mar 31)
        /// </summary>
        public int FiscalYear
        {
            get
            {
                // Fiscal year starts April 1
                // If start date is Jan-Mar, fiscal year is current year
                // If start date is Apr-Dec, fiscal year is next year
                return StartDate.Month >= 4 ? StartDate.Year + 1 : StartDate.Year;
            }
        }

        public override string ToString()
        {
            return $"{StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}";
        }
    }

    /// <summary>
    /// Project contact information
    /// </summary>
    public class ProjectContact
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        public ProjectContact()
        {
            Id = Guid.NewGuid();
            Name = string.Empty;
            Role = string.Empty;
            Email = string.Empty;
            Phone = string.Empty;
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Role) ? Name : $"{Name} ({Role})";
        }
    }

    /// <summary>
    /// Project note with timestamp
    /// </summary>
    public class ProjectNote
    {
        public Guid Id { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Author { get; set; }

        public ProjectNote()
        {
            Id = Guid.NewGuid();
            Content = string.Empty;
            CreatedAt = DateTime.Now;
            Author = string.Empty;
        }
    }

    /// <summary>
    /// Main project model with complete fields
    /// </summary>
    public class Project
    {
        // Core identity
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Nickname { get; set; }  // Short identifier (e.g., "ABC123")
        public string Id1 { get; set; }       // Legacy/external ID (AuditCase)
        public string ID2 { get; set; }       // CAA Report ID (CASCase) - 6-9 digit unique identifier
        public string FullProjectName { get; set; }  // Full client/project name (TPName)
        public string Description { get; set; }

        // Status and priority
        public ProjectStatus Status { get; set; }
        public TaskPriority Priority { get; set; }

        // Timing
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public AuditPeriod CurrentAuditPeriod { get; set; }
        public List<AuditPeriod> AuditPeriods { get; set; }

        // Budget tracking
        public decimal? BudgetHours { get; set; }
        public decimal? BudgetAmount { get; set; }

        // People
        public List<ProjectContact> Contacts { get; set; }

        // Organization
        public List<string> Tags { get; set; }
        public List<ProjectNote> Notes { get; set; }

        // Metadata
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Deleted { get; set; }
        public bool Archived { get; set; }

        // Custom fields (extensibility)
        public Dictionary<string, string> CustomFields { get; set; }

        // Excel import fields (SVI-CAS Audit Request Form)
        public DateTime? RequestDate { get; set; }
        public string AuditType { get; set; }
        public string AuditorName { get; set; }
        public DateTime? DateAssigned { get; set; }
        public string AuditProgram { get; set; }
        public string ClientID { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public string TaxID { get; set; }
        public DateTime? AuditPeriodFrom { get; set; }
        public DateTime? AuditPeriodTo { get; set; }
        public DateTime? AuditPeriod1Start { get; set; }
        public DateTime? AuditPeriod1End { get; set; }
        public DateTime? AuditPeriod2Start { get; set; }
        public DateTime? AuditPeriod2End { get; set; }
        public DateTime? AuditPeriod3Start { get; set; }
        public DateTime? AuditPeriod3End { get; set; }
        public DateTime? AuditPeriod4Start { get; set; }
        public DateTime? AuditPeriod4End { get; set; }
        public string Contact1Name { get; set; }
        public string Contact1Phone { get; set; }
        public string Contact1Ext { get; set; }
        public string Contact1Address { get; set; }
        public string Contact1Title { get; set; }
        public string Contact2Name { get; set; }
        public string Contact2Phone { get; set; }
        public string Contact2Ext { get; set; }
        public string Contact2Address { get; set; }
        public string Contact2Title { get; set; }
        public string AccountingSoftware1 { get; set; }
        public string AccountingSoftware1Other { get; set; }
        public string AccountingSoftware1Type { get; set; }
        public string AccountingSoftware2 { get; set; }
        public string AccountingSoftware2Other { get; set; }
        public string AccountingSoftware2Type { get; set; }
        public string TPEmailAddress { get; set; }
        public string TPPhoneNumber { get; set; }
        public string CASNumber { get; set; }
        public string EmailReference { get; set; }
        public string Comments { get; set; }
        public string FXInfo { get; set; }
        public string ShipToAddress { get; set; }

        public Project()
        {
            Id = Guid.NewGuid();
            Name = string.Empty;
            Nickname = string.Empty;
            Id1 = string.Empty;
            ID2 = string.Empty;
            FullProjectName = string.Empty;
            Description = string.Empty;
            Status = ProjectStatus.Planned;
            Priority = TaskPriority.Medium;
            AuditPeriods = new List<AuditPeriod>();
            Contacts = new List<ProjectContact>();
            Tags = new List<string>();
            Notes = new List<ProjectNote>();
            CustomFields = new Dictionary<string, string>();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            Deleted = false;
            Archived = false;

            // Initialize Excel import fields
            AuditType = string.Empty;
            AuditorName = string.Empty;
            AuditProgram = string.Empty;
            ClientID = string.Empty;
            Address = string.Empty;
            City = string.Empty;
            Province = string.Empty;
            PostalCode = string.Empty;
            Country = string.Empty;
            TaxID = string.Empty;
            Contact1Name = string.Empty;
            Contact1Phone = string.Empty;
            Contact1Ext = string.Empty;
            Contact1Address = string.Empty;
            Contact1Title = string.Empty;
            Contact2Name = string.Empty;
            Contact2Phone = string.Empty;
            Contact2Ext = string.Empty;
            Contact2Address = string.Empty;
            Contact2Title = string.Empty;
            AccountingSoftware1 = string.Empty;
            AccountingSoftware1Other = string.Empty;
            AccountingSoftware1Type = string.Empty;
            AccountingSoftware2 = string.Empty;
            AccountingSoftware2Other = string.Empty;
            AccountingSoftware2Type = string.Empty;
            TPEmailAddress = string.Empty;
            TPPhoneNumber = string.Empty;
            CASNumber = string.Empty;
            EmailReference = string.Empty;
            Comments = string.Empty;
            FXInfo = string.Empty;
            ShipToAddress = string.Empty;
        }

        /// <summary>
        /// Check if project is overdue based on end date
        /// </summary>
        public bool IsOverdue
        {
            get
            {
                if (!EndDate.HasValue || Status == ProjectStatus.Completed || Status == ProjectStatus.Cancelled)
                    return false;
                return EndDate.Value.Date < DateTime.Now.Date;
            }
        }

        /// <summary>
        /// Check if project is due soon (within 7 days)
        /// </summary>
        public bool IsDueSoon
        {
            get
            {
                if (!EndDate.HasValue || Status == ProjectStatus.Completed || Status == ProjectStatus.Cancelled)
                    return false;
                var daysUntil = (EndDate.Value.Date - DateTime.Now.Date).Days;
                return daysUntil >= 0 && daysUntil <= 7;
            }
        }

        /// <summary>
        /// Get days until due date (negative if overdue, null if no due date)
        /// </summary>
        public int? DaysUntilDue
        {
            get
            {
                if (!EndDate.HasValue)
                    return null;
                return (EndDate.Value.Date - DateTime.Now.Date).Days;
            }
        }

        /// <summary>
        /// Get display identifier (Nickname if available, otherwise truncated Name)
        /// </summary>
        public string DisplayId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Nickname))
                    return Nickname;
                if (Name.Length > 20)
                    return Name.Substring(0, 17) + "...";
                return Name;
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
                    ProjectStatus.Planned => "○",
                    ProjectStatus.Active => "●",
                    ProjectStatus.OnHold => "⏸",
                    ProjectStatus.Completed => "✓",
                    ProjectStatus.Cancelled => "✗",
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
                    _ => "?"
                };
            }
        }

        /// <summary>
        /// Clone this project
        /// </summary>
        public Project Clone()
        {
            return new Project
            {
                Id = this.Id,
                Name = this.Name,
                Nickname = this.Nickname,
                Id1 = this.Id1,
                Description = this.Description,
                Status = this.Status,
                Priority = this.Priority,
                StartDate = this.StartDate,
                EndDate = this.EndDate,
                CurrentAuditPeriod = this.CurrentAuditPeriod,
                AuditPeriods = this.AuditPeriods.Select(ap => new AuditPeriod
                {
                    StartDate = ap.StartDate,
                    EndDate = ap.EndDate,
                    Description = ap.Description
                }).ToList(),
                BudgetHours = this.BudgetHours,
                BudgetAmount = this.BudgetAmount,
                Contacts = this.Contacts.Select(c => new ProjectContact
                {
                    Id = c.Id,
                    Name = c.Name,
                    Role = c.Role,
                    Email = c.Email,
                    Phone = c.Phone
                }).ToList(),
                Tags = new List<string>(this.Tags),
                Notes = this.Notes.Select(n => new ProjectNote
                {
                    Id = n.Id,
                    Content = n.Content,
                    CreatedAt = n.CreatedAt,
                    Author = n.Author
                }).ToList(),
                CustomFields = new Dictionary<string, string>(this.CustomFields),
                CreatedAt = this.CreatedAt,
                UpdatedAt = this.UpdatedAt,
                Deleted = this.Deleted,
                Archived = this.Archived
            };
        }

        public override string ToString()
        {
            return $"{StatusIcon} {DisplayId} - {Name}";
        }
    }

    /// <summary>
    /// Project task statistics for dashboard
    /// </summary>
    public class ProjectTaskStats
    {
        public Guid ProjectId { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int HighPriorityTasks { get; set; }

        public ProjectTaskStats()
        {
            ProjectId = Guid.Empty;
        }

        /// <summary>
        /// Calculate completion percentage (0-100)
        /// </summary>
        public int CompletionPercentage
        {
            get
            {
                if (TotalTasks == 0) return 0;
                return (int)Math.Round((double)CompletedTasks / TotalTasks * 100);
            }
        }

        /// <summary>
        /// Check if project has overdue tasks
        /// </summary>
        public bool HasOverdueTasks => OverdueTasks > 0;

        /// <summary>
        /// Check if project has high priority tasks
        /// </summary>
        public bool HasHighPriorityTasks => HighPriorityTasks > 0;
    }

    /// <summary>
    /// Project with integrated task statistics
    /// </summary>
    public class ProjectWithStats
    {
        public Project Project { get; set; }
        public ProjectTaskStats Stats { get; set; }
        public decimal HoursLogged { get; set; }

        public ProjectWithStats()
        {
            Project = new Project();
            Stats = new ProjectTaskStats();
            HoursLogged = 0;
        }

        /// <summary>
        /// Get budget utilization percentage (hours logged vs budget hours)
        /// </summary>
        public decimal? BudgetUtilization
        {
            get
            {
                if (!Project.BudgetHours.HasValue || Project.BudgetHours.Value == 0)
                    return null;
                return Math.Round((HoursLogged / Project.BudgetHours.Value) * 100, 1);
            }
        }

        /// <summary>
        /// Check if project is over budget
        /// </summary>
        public bool IsOverBudget
        {
            get
            {
                if (!Project.BudgetHours.HasValue)
                    return false;
                return HoursLogged > Project.BudgetHours.Value;
            }
        }

        /// <summary>
        /// Get hours remaining in budget (negative if over)
        /// </summary>
        public decimal? HoursRemaining
        {
            get
            {
                if (!Project.BudgetHours.HasValue)
                    return null;
                return Project.BudgetHours.Value - HoursLogged;
            }
        }
    }

    /// <summary>
    /// Filter preset for project queries
    /// </summary>
    public class ProjectFilter
    {
        public string Name { get; set; }
        public Func<Project, bool> Predicate { get; set; }

        public ProjectFilter(string name, Func<Project, bool> predicate)
        {
            Name = name;
            Predicate = predicate;
        }

        // Common filter presets
        public static ProjectFilter All => new ProjectFilter("All", p => !p.Deleted && !p.Archived);
        public static ProjectFilter Active => new ProjectFilter("Active", p => !p.Deleted && !p.Archived && p.Status == ProjectStatus.Active);
        public static ProjectFilter Planned => new ProjectFilter("Planned", p => !p.Deleted && !p.Archived && p.Status == ProjectStatus.Planned);
        public static ProjectFilter OnHold => new ProjectFilter("On Hold", p => !p.Deleted && !p.Archived && p.Status == ProjectStatus.OnHold);
        public static ProjectFilter Completed => new ProjectFilter("Completed", p => !p.Deleted && p.Status == ProjectStatus.Completed);
        public static ProjectFilter HighPriority => new ProjectFilter("High Priority", p => !p.Deleted && !p.Archived && p.Priority == TaskPriority.High && p.Status == ProjectStatus.Active);
        public static ProjectFilter Overdue => new ProjectFilter("Overdue", p => !p.Deleted && !p.Archived && p.IsOverdue);
        public static ProjectFilter Archived => new ProjectFilter("Archived", p => !p.Deleted && p.Archived);

        public static List<ProjectFilter> GetDefaultFilters()
        {
            return new List<ProjectFilter>
            {
                All, Active, Planned, OnHold, Completed, HighPriority, Overdue, Archived
            };
        }
    }
}
