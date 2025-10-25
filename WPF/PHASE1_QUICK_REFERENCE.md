# Phase 1 Quick Reference Guide
## Project Management Data Layer

---

## Quick Start

```csharp
// Initialize services (call once at app startup)
ProjectService.Instance.Initialize();
TimeTrackingService.Instance.Initialize();
TaskService.Instance.Initialize();  // Already exists

// Create a project
var project = new Project
{
    Name = "My Project",
    Nickname = "MYPROJ",
    Priority = TaskPriority.High,
    Status = ProjectStatus.Active
};
ProjectService.Instance.AddProject(project);

// Add a task to the project
var task = new TaskItem
{
    Title = "Complete design",
    ProjectId = project.Id
};
TaskService.Instance.AddTask(task);

// Log time
var entry = new TimeEntry
{
    ProjectId = project.Id,
    WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
    Hours = 8.5M
};
TimeTrackingService.Instance.AddEntry(entry);

// Get project dashboard view
var stats = ProjectService.Instance.GetProjectWithStats(project.Id);
Console.WriteLine($"{stats.Project.Name}:");
Console.WriteLine($"  Tasks: {stats.Stats.TotalTasks} ({stats.Stats.CompletedTasks} done)");
Console.WriteLine($"  Hours: {stats.HoursLogged:F2}");
```

---

## Common Patterns

### Project Lookup (Multiple Ways)

```csharp
// By ID (fastest - O(1))
var project = ProjectService.Instance.GetProject(guid);

// By Nickname (fast - O(1) via index)
var project = ProjectService.Instance.GetProjectByNickname("MYPROJ");

// By Id1/legacy ID (fast - O(1) via index)
var project = ProjectService.Instance.GetProjectById1("PRJ-001");

// By filter
var active = ProjectService.Instance.GetProjects(p => p.Status == ProjectStatus.Active);

// Get all (with filtering options)
var all = ProjectService.Instance.GetAllProjects(
    includeDeleted: false,
    includeArchived: false
);
```

### Working with Time Entries

```csharp
// Get current week ending (always a Sunday)
var weekEnding = TimeTrackingService.GetCurrentWeekEnding();

// Get all entries for a week
var entries = TimeTrackingService.Instance.GetEntriesForWeek(weekEnding);

// Get entries for a project
var projectEntries = TimeTrackingService.Instance.GetEntriesForProject(projectId);

// Get specific entry for project + week
var entry = TimeTrackingService.Instance.GetEntryForProjectAndWeek(
    projectId,
    weekEnding
);

// Add or update entry
if (entry == null)
{
    entry = new TimeEntry { ProjectId = projectId, WeekEnding = weekEnding };
    entry.Hours = 10.0M;
    TimeTrackingService.Instance.AddEntry(entry);
}
else
{
    entry.Hours += 5.0M;  // Add more hours
    TimeTrackingService.Instance.UpdateEntry(entry);
}
```

### Fiscal Year Queries

```csharp
// Get current fiscal year (Apr 1 - Mar 31)
var currentFY = TimeTrackingService.GetCurrentFiscalYear();
// If today is 2025-10-15, returns 2026

// Get fiscal year for any date
var fy = TimeTrackingService.GetFiscalYear(new DateTime(2025, 3, 31));
// Returns 2025 (last day of FY2025)

// Get FY summary
var summary = TimeTrackingService.Instance.GetFiscalYearSummary(2026);
// summary.StartDate = 2025-04-01
// summary.EndDate = 2026-03-31
// summary.TotalHours = <all hours in FY2026>

// Get current FY summary
var currentSummary = TimeTrackingService.Instance.GetCurrentFiscalYearSummary();
```

### Project Statistics

```csharp
// Basic stats (just tasks)
var stats = ProjectService.Instance.GetProjectStats(projectId);
Console.WriteLine($"Tasks: {stats.TotalTasks}");
Console.WriteLine($"Completed: {stats.CompletedTasks}");
Console.WriteLine($"In Progress: {stats.InProgressTasks}");
Console.WriteLine($"Overdue: {stats.OverdueTasks}");

// Full stats (tasks + time)
var fullStats = ProjectService.Instance.GetProjectWithStats(projectId);
Console.WriteLine($"Hours logged: {fullStats.HoursLogged}");
Console.WriteLine($"Budget: {fullStats.Project.BudgetHours}h");
Console.WriteLine($"Utilization: {fullStats.BudgetUtilization}%");
Console.WriteLine($"Remaining: {fullStats.HoursRemaining}h");

// Get all projects with stats
var allProjects = ProjectService.Instance.GetProjectsWithStats(includeArchived: false);
foreach (var p in allProjects)
{
    Console.WriteLine($"{p.Project.DisplayId}: {p.Stats.CompletionPercentage}% done");
}
```

### Time Aggregation

```csharp
// Project time summary
var aggregate = TimeTrackingService.Instance.GetProjectAggregate(
    projectId,
    startDate: new DateTime(2025, 1, 1),
    endDate: new DateTime(2025, 12, 31)
);
Console.WriteLine($"Total: {aggregate.TotalHours}h over {aggregate.WeekCount} weeks");
Console.WriteLine($"Average: {aggregate.AverageHoursPerWeek}h/week");

// All projects time summary
var allAggregates = TimeTrackingService.Instance.GetAllProjectAggregates(
    startDate: TimeTrackingService.GetFiscalYearStart(2026),
    endDate: TimeTrackingService.GetFiscalYearEnd(2026)
);

// Weekly report
var report = TimeTrackingService.Instance.GetWeeklyReport(weekEnding);
Console.WriteLine($"Week of {report.WeekStart:yyyy-MM-dd}:");
Console.WriteLine($"  Total: {report.TotalHours}h across {report.ProjectCount} projects");
```

---

## Event Subscriptions

```csharp
// Subscribe to project changes
ProjectService.Instance.ProjectAdded += (project) =>
{
    Console.WriteLine($"New project: {project.Name}");
};

ProjectService.Instance.ProjectUpdated += (project) =>
{
    Console.WriteLine($"Updated: {project.Name}");
};

ProjectService.Instance.ProjectDeleted += (id) =>
{
    Console.WriteLine($"Deleted project: {id}");
};

ProjectService.Instance.ProjectsReloaded += () =>
{
    Console.WriteLine("Projects reloaded from disk");
};

// Subscribe to time entry changes
TimeTrackingService.Instance.EntryAdded += (entry) =>
{
    Console.WriteLine($"Logged {entry.Hours}h to project {entry.ProjectId}");
};

TimeTrackingService.Instance.EntryUpdated += (entry) =>
{
    Console.WriteLine($"Updated time entry: {entry.Id}");
};
```

---

## Useful Computed Properties

### Project

```csharp
var project = ProjectService.Instance.GetProject(id);

// Status/priority icons
project.StatusIcon     // "●" for Active, "○" for Planned, etc.
project.PriorityIcon   // "↑" for High, "●" for Medium, "↓" for Low

// Display ID (nickname or truncated name)
project.DisplayId      // "MYPROJ" or "My Really Long Proj..."

// Due date helpers
project.IsOverdue      // true if past end date
project.IsDueSoon      // true if within 7 days of end date
project.DaysUntilDue   // int? days until end date (negative if overdue)
```

### TimeEntry

```csharp
var entry = TimeTrackingService.Instance.GetEntry(id);

// Total hours (uses daily breakdown if available, else summary hours)
entry.TotalHours       // decimal

// Week boundaries
entry.WeekStart        // Monday of the week
entry.WeekEnding       // Sunday of the week (always)

// Fiscal year
entry.FiscalYear       // int (e.g., 2026 for FY2026)

// Check for daily breakdown
entry.HasDailyBreakdown  // true if any daily hours set
```

### AuditPeriod

```csharp
var period = project.CurrentAuditPeriod;

// Active status
period.IsActive        // true if today is within period

// Duration
period.DurationDays    // total days in period
period.DaysRemaining   // days left (0 if past)

// Fiscal year
period.FiscalYear      // int based on start date
```

### ProjectWithStats

```csharp
var stats = ProjectService.Instance.GetProjectWithStats(projectId);

// Budget tracking
stats.BudgetUtilization  // decimal? percentage (e.g., 85.5 = 85.5%)
stats.IsOverBudget       // bool
stats.HoursRemaining     // decimal? (negative if over)

// Task stats
stats.Stats.CompletionPercentage    // int 0-100
stats.Stats.HasOverdueTasks         // bool
stats.Stats.HasHighPriorityTasks    // bool
```

---

## Data Validation

### Project Constraints

```csharp
// Nickname must be unique (case-insensitive)
var project = new Project { Nickname = "ABC" };
ProjectService.Instance.AddProject(project);
// Throws: InvalidOperationException if "ABC" already exists

// Id1 must be unique (case-insensitive)
var project = new Project { Id1 = "PRJ-001" };
ProjectService.Instance.AddProject(project);
// Throws: InvalidOperationException if "PRJ-001" already exists

// Both can be null/empty (no uniqueness check)
var project = new Project { Name = "Unnamed Project" };
ProjectService.Instance.AddProject(project);  // OK
```

### TimeEntry Normalization

```csharp
// Week ending is always normalized to Sunday
var entry = new TimeEntry
{
    WeekEnding = new DateTime(2025, 10, 15)  // Wednesday
};
TimeTrackingService.Instance.AddEntry(entry);
// entry.WeekEnding is now 2025-10-19 (Sunday)

// Use helper to get correct date
var correctDate = TimeTrackingService.GetWeekEnding(new DateTime(2025, 10, 15));
// Returns 2025-10-19 (Sunday)
```

---

## Filtering Presets

### Projects

```csharp
// Use predefined filters
var active = ProjectService.Instance.GetProjects(ProjectFilter.Active.Predicate);
var planned = ProjectService.Instance.GetProjects(ProjectFilter.Planned.Predicate);
var overdue = ProjectService.Instance.GetProjects(ProjectFilter.Overdue.Predicate);
var highPriority = ProjectService.Instance.GetProjects(ProjectFilter.HighPriority.Predicate);

// Get all default filters
var filters = ProjectFilter.GetDefaultFilters();
// Returns: All, Active, Planned, OnHold, Completed, HighPriority, Overdue, Archived
```

---

## Persistence

### Data Files

```
~/.local/share/SuperTUI/  (Linux)
%APPDATA%/SuperTUI/       (Windows)

├── projects.json          # All projects
├── timetracking.json      # All time entries
├── tasks.json             # All tasks
│
└── Backups:
    ├── projects.json.20251025_143022.bak
    ├── projects.json.20251025_142518.bak
    └── ... (keeps last 5)
```

### Manual Save/Load

```csharp
// Force immediate save (bypasses debounce)
await ProjectService.Instance.SaveToFileAsync();  // Internal method

// Reload from disk
ProjectService.Instance.Reload();

// Clear all data (for testing)
ProjectService.Instance.Clear();
```

### Export

```csharp
// Export to JSON
ProjectService.Instance.ExportToJson("/path/to/export.json");
TimeTrackingService.Instance.ExportToJson("/path/to/timesheet.json");
```

---

## Performance Tips

1. **Use indexes for lookups:**
   ```csharp
   // Fast (O(1))
   var project = ProjectService.Instance.GetProjectByNickname("ABC");

   // Slow (O(n))
   var project = ProjectService.Instance.GetProjects(p => p.Nickname == "ABC").FirstOrDefault();
   ```

2. **Use GetProjectsWithStats() for bulk operations:**
   ```csharp
   // Good: Single call gets everything
   var all = ProjectService.Instance.GetProjectsWithStats();

   // Bad: N+1 query problem
   var projects = ProjectService.Instance.GetAllProjects();
   foreach (var p in projects)
   {
       var stats = ProjectService.Instance.GetProjectWithStats(p.Id);  // N queries!
   }
   ```

3. **Week index is fast:**
   ```csharp
   // Fast (O(1) via index)
   var entries = TimeTrackingService.Instance.GetEntriesForWeek(weekEnding);

   // Slower (O(n) scan)
   var entries = TimeTrackingService.Instance.GetEntries(e => e.WeekEnding == weekEnding);
   ```

---

## Common Mistakes

❌ **Don't forget to initialize:**
```csharp
// Wrong - service not initialized
var projects = ProjectService.Instance.GetAllProjects();  // Empty list!

// Right
ProjectService.Instance.Initialize();
var projects = ProjectService.Instance.GetAllProjects();
```

❌ **Don't assume week ending is always Sunday:**
```csharp
// Wrong
var entry = new TimeEntry { WeekEnding = DateTime.Now };  // Might be Tuesday!

// Right
var entry = new TimeEntry
{
    WeekEnding = TimeTrackingService.GetCurrentWeekEnding()  // Always Sunday
};
```

❌ **Don't ignore uniqueness constraints:**
```csharp
// Wrong - will throw if nickname exists
var project = new Project { Nickname = existingNickname };
ProjectService.Instance.AddProject(project);  // Exception!

// Right - check first
var existing = ProjectService.Instance.GetProjectByNickname(nickname);
if (existing == null)
{
    var project = new Project { Nickname = nickname };
    ProjectService.Instance.AddProject(project);
}
```

---

## Integration Example: Dashboard Widget

```csharp
public class ProjectDashboardWidget
{
    private void LoadDashboard()
    {
        // Get all active projects with stats
        var projects = ProjectService.Instance.GetProjectsWithStats(includeArchived: false)
            .Where(p => p.Project.Status == ProjectStatus.Active)
            .OrderByDescending(p => p.Project.Priority)
            .ToList();

        foreach (var p in projects)
        {
            // Project info
            Console.WriteLine($"{p.Project.DisplayId} - {p.Project.Name}");
            Console.WriteLine($"  Status: {p.Project.StatusIcon} {p.Project.Status}");
            Console.WriteLine($"  Priority: {p.Project.PriorityIcon} {p.Project.Priority}");

            // Task stats
            Console.WriteLine($"  Tasks: {p.Stats.TotalTasks} total");
            Console.WriteLine($"    ✓ {p.Stats.CompletedTasks} completed ({p.Stats.CompletionPercentage}%)");
            Console.WriteLine($"    ⚡ {p.Stats.InProgressTasks} in progress");
            Console.WriteLine($"    ⏱ {p.Stats.PendingTasks} pending");
            if (p.Stats.OverdueTasks > 0)
                Console.WriteLine($"    ⚠ {p.Stats.OverdueTasks} OVERDUE");

            // Time stats
            Console.WriteLine($"  Time: {p.HoursLogged:F2}h logged");
            if (p.Project.BudgetHours.HasValue)
            {
                Console.WriteLine($"    Budget: {p.Project.BudgetHours:F2}h");
                Console.WriteLine($"    Utilization: {p.BudgetUtilization:F1}%");
                if (p.IsOverBudget)
                    Console.WriteLine($"    ⚠ OVER BUDGET by {-p.HoursRemaining:F2}h");
                else
                    Console.WriteLine($"    Remaining: {p.HoursRemaining:F2}h");
            }

            Console.WriteLine();
        }
    }

    // Subscribe to updates
    private void InitializeEvents()
    {
        ProjectService.Instance.ProjectUpdated += (p) => LoadDashboard();
        TaskService.Instance.TaskAdded += (t) => LoadDashboard();
        TaskService.Instance.TaskUpdated += (t) => LoadDashboard();
        TimeTrackingService.Instance.EntryAdded += (e) => LoadDashboard();
        TimeTrackingService.Instance.EntryUpdated += (e) => LoadDashboard();
    }
}
```

---

## Next Steps

See `PHASE1_IMPLEMENTATION_SUMMARY.md` for:
- Complete architecture details
- Full API reference
- Integration patterns
- Performance characteristics

Ready to build UI components in Phase 2!
