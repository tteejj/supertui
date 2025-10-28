# What SuperTUI Should Take from Terminal TUIs
**Focus: Data Models, UI Presentation, Feature Integration**

---

## Executive Summary

After analyzing 5 major TUI implementations (praxis-main, HELIOS, _R2, ALCAR, _CLASSY), here's what SuperTUI should adopt in terms of **data models**, **UI presentation**, and **feature integration**.

---

## PART 1: DATA MODELS - What Fields/Structure to Add

### Current SuperTUI Task Model (from ITaskService)
```csharp
class TaskItem {
    Guid Id;
    string Title;
    string Description;
    TaskStatus Status;        // Pending/InProgress/Completed
    TaskPriority Priority;    // Low/Medium/High
    DateTime? DueDate;
    Guid? ProjectId;
    Guid? ParentTaskId;
    List<Guid> SubtaskIds;
    List<string> Tags;
    string Notes;
}
```

### MISSING Fields Found in Terminal TUIs ⚠️

#### 1. **CompletedDate** (CRITICAL)
**Found in:** ALL terminal TUIs
**Why:** Track when tasks were actually finished
**Add:**
```csharp
DateTime? CompletedDate { get; set; }  // Auto-set when Status → Completed
```

#### 2. **CreatedDate / ModifiedDate** (CRITICAL)
**Found in:** ALL terminal TUIs
**Why:** Audit trail, sorting by recency
**Add:**
```csharp
DateTime CreatedDate { get; set; }      // Auto-set on creation
DateTime ModifiedDate { get; set; }     // Auto-update on any change
```

#### 3. **Priority "Today" Level** (HIGH VALUE)
**Found in:** TaskProPro (4 levels: Today/High/Medium/Low)
**Why:** Separate from "High" - specifically for today's work
**Modify:**
```csharp
enum TaskPriority {
    Low = 0,
    Medium = 1,
    High = 2,
    Today = 3      // NEW - highest priority, must do today
}
```

#### 4. **Estimated vs Actual Time** (HIGH VALUE)
**Found in:** TaskProPro, WPF variant
**Why:** Time budgeting, velocity tracking
**Add:**
```csharp
TimeSpan? EstimatedDuration { get; set; }
TimeSpan ActualDuration { get; set; }  // Sum of time entries
```

#### 5. **External IDs** (MEDIUM VALUE)
**Found in:** ALL TUIs (ID1/ID2 pattern)
**Why:** Excel import/export, external system integration
**Add:**
```csharp
string ExternalId1 { get; set; }  // e.g., "PROJ" category
string ExternalId2 { get; set; }  // e.g., "TaskMgmt" identifier
```

#### 6. **Recurrence Pattern** (MEDIUM VALUE)
**Found in:** None (gap across ALL TUIs)
**Why:** Repeating tasks (daily standup, weekly review, etc.)
**Add:**
```csharp
RecurrencePattern Recurrence { get; set; }

class RecurrencePattern {
    RecurrenceType Type;  // None/Daily/Weekly/Monthly/Yearly
    int Interval;         // Every N days/weeks/months
    DayOfWeek[] DaysOfWeek;  // For weekly
    DateTime? EndDate;
}
```

#### 7. **Dependencies** (LOW VALUE)
**Found in:** None (gap across ALL TUIs)
**Why:** Task ordering, blocking relationships
**Add:**
```csharp
List<Guid> DependsOn { get; set; }     // Can't start until these complete
List<Guid> Blocks { get; set; }        // Completing this unblocks these
```

---

### Current SuperTUI Project Model
```csharp
class Project {
    Guid Id;
    string Name;
    string Description;
    Color Color;
    DateTime? StartDate;
    DateTime? EndDate;
}
```

### MISSING Fields ⚠️

#### 1. **Progress Tracking** (CRITICAL)
**Found in:** ALL TUIs (calculated or stored)
**Add:**
```csharp
int CompletionPercentage { get; }  // Auto-calc: completed tasks / total tasks
int TotalTasks { get; }
int CompletedTasks { get; }
int ActiveTasks { get; }
```

#### 2. **Time Budget** (HIGH VALUE)
**Found in:** TaskProPro, praxis-main (CumulativeHrs field)
**Add:**
```csharp
TimeSpan? EstimatedHours { get; set; }
TimeSpan ActualHours { get; }  // Sum of time entries
decimal? BudgetAmount { get; set; }
decimal? BillingRate { get; set; }
```

#### 3. **External IDs** (HIGH VALUE)
**Found in:** ALL TUIs
**Add:**
```csharp
string ExternalId1 { get; set; }
string ExternalId2 { get; set; }
```

#### 4. **Status** (MEDIUM VALUE)
**Found in:** praxis-main
**Add:**
```csharp
ProjectStatus Status { get; set; }  // Planning/Active/OnHold/Completed/Archived

enum ProjectStatus {
    Planning,
    Active,
    OnHold,
    Completed,
    Archived
}
```

---

### Current SuperTUI Time Entry Model
```csharp
class TimeEntry {
    Guid Id;
    Guid? TaskId;
    Guid? ProjectId;
    DateTime StartTime;
    DateTime? EndTime;
    TimeSpan Duration;
    string Description;
}
```

### MISSING Fields ⚠️

#### 1. **Weekly Structure** (CRITICAL - Found in ALL TUIs)
**Pattern:** Time tracked by WEEK, not individual entries
**Why:** Easier batch entry, standard work week (Mon-Fri)
**Add NEW Model:**
```csharp
class WeeklyTimeEntry {
    Guid Id;
    string ExternalId1;           // Category (PROJ, MEET, TRAIN, ADMIN)
    string ExternalId2;           // Project/task name
    string Description;
    DateTime WeekEndingFriday;    // Week identifier (always Friday)
    decimal MondayHours;
    decimal TuesdayHours;
    decimal WednesdayHours;
    decimal ThursdayHours;
    decimal FridayHours;
    decimal TotalHours { get; }   // Auto-calc: sum of daily hours
    string FiscalYear;            // e.g., "2025-2026" (April-March)
    Guid? ProjectId;              // Link to project
    Guid? TaskId;                 // Link to task (optional)
}
```

**Validation Rules (from TUIs):**
- No duplicate ID1/ID2 in same week
- Daily hours: 0.0 - 24.0
- ID1 max 20 chars
- Description max 200 chars
- Fiscal year auto-calculated from week ending date

#### 2. **Billable Flag** (HIGH VALUE)
**Found in:** TaskProPro, praxis-main
**Add to TimeEntry:**
```csharp
bool IsBillable { get; set; }
decimal? BillingRate { get; set; }
```

#### 3. **Activity Type** (MEDIUM VALUE)
**Found in:** praxis-main (ID1 categories)
**Add:**
```csharp
string ActivityType { get; set; }  // PROJ, MEET, TRAIN, ADMIN, etc.
```

---

## PART 2: UI PRESENTATION - How Terminal TUIs Display Data

### Task List Views - What SuperTUI Should Show

#### Option 1: Compact Column View (from praxis-main)
```
Status  Task Name              ID1     ID2         Assigned    Due Date
------  --------------------   ------  ----------  ----------  ----------
[ ]     Implement login        PROJ    Auth        2025-10-20  2025-10-30
[✓]     Fix bug #123          PROJ    BugFix      2025-10-15  2025-10-25
[ ]     Write documentation    DOC     UserGuide   2025-10-22  2025-11-01
  [ ]     ↳ Introduction       DOC     UserGuide   2025-10-23  2025-10-28
  [ ]     ↳ API Reference      DOC     UserGuide   2025-10-24  2025-10-29
```

**Key Features:**
- Status as checkbox symbol (not text)
- Indentation for subtasks with `↳` symbol
- Short IDs visible (ID1/ID2 for Excel integration)
- Compact dates (yyyy-mm-dd, not full format)

**SuperTUI Currently:** Likely shows full "Pending/InProgress/Completed" text - wasteful

**ADOPT:** Use symbols instead of text for status/priority

---

#### Option 2: Symbol-Rich View (from ALCAR)
```
○ Buy groceries                    →  2025-10-30  Personal
◐ Write quarterly report           ↑  2025-10-28  Work
● Fix authentication bug            →  2025-10-25  Work
○ Call dentist                     ↓  2025-11-05  Personal
  ○ ↳ Get insurance info           ↓  2025-11-03  Personal
```

**Symbols:**
- Status: `○` Pending, `◐` InProgress, `●` Completed
- Priority: `↓` Low, `→` Medium, `↑` High, `‼` Today (NEW)
- Hierarchy: `↳` for child tasks

**SuperTUI Currently:** Unknown - check if using symbols

**ADOPT:** Standard symbol vocabulary across all widgets

---

#### Option 3: Three-Pane Layout (from ALCAR)
```
┌─ FILTERS ────────┬─ TASKS ───────────────────────┬─ DETAILS ──────────┐
│                  │                               │                    │
│ All        [25]  │ ○ Buy groceries          →    │ Title: Buy groc... │
│ Active     [12]  │ ◐ Write report           ↑    │ Status: InProgress │
│ Completed  [13]  │ ● Fix bug                →    │ Priority: High     │
│ Overdue     [3]  │ ○ Call dentist           ↓    │ Due: 2025-10-30    │
│                  │                               │ Project: Personal  │
│ Projects:        │ Selected: Write report        │ Created: 2025-10-15│
│ Work       [8]   │                               │ Modified: 2025-10-27│
│ Personal  [17]   │                               │                    │
│                  │                               │ Description:       │
│ Priority:        │                               │ Need to prepare... │
│ Today      [2]   │                               │                    │
│ High       [5]   │                               │ Time Spent: 3.5h   │
│ Medium     [8]   │                               │ Estimated: 8h      │
│ Low       [10]   │                               │                    │
└──────────────────┴───────────────────────────────┴────────────────────┘
Tab to switch panes    Arrow keys navigate    Enter to edit    Esc to close
```

**SuperTUI Currently:** TaskManagementWidget is single pane - no dedicated filters or detail

**ADOPT:** Three-pane layout with:
- **Left (20%):** Filters with counts `[N]`
- **Center (50%):** Task list (compact)
- **Right (30%):** Selected task details

---

### Time Tracking Views - CRITICAL INSIGHT ⚠️

#### Weekly Grid View (Found in ALL TUIs with time tracking)
```
Week Ending: Friday, November 1, 2025                    Fiscal Year: 2025-2026

ID1      ID2           Description        Mon   Tue   Wed   Thu   Fri   Total
------   -----------   ----------------   ----  ----  ----  ----  ----  -----
PROJ     TaskMgmt      Task management    7.5   8.0   8.0   7.5   4.0   35.0
MEET     Standup       Daily standup      0.5   0.5   0.5   0.5   0.5    2.5
TRAIN    Learning      Tech learning      1.0   1.5   0.0   2.0   3.5    8.0
ADMIN    Email         Emails/admin       1.0   0.5   1.0   0.5   0.0    3.0
                                         ────  ────  ────  ────  ────  ─────
                               DAILY TOTAL: 10.0  10.5   9.5  10.5   8.0   48.5

[Current day: ▸ Wed]    [<] Prev Week    [>] Next Week    [E]dit    [N]ew Entry
```

**Key Features:**
- **Week-based structure** (not daily entries)
- **5 columns for Mon-Fri** (not 7 days)
- **Fiscal year display** (April-March calendar)
- **Daily totals row** at bottom
- **Current day highlighted** with `▸` symbol
- **Decimal hours** (7.5, not 7:30)
- **Two-step workflow:** Select ID1/ID2 → Enter hours for each day

**SuperTUI Currently:** Likely hourly entries with start/end times - DIFFERENT MODEL

**DECISION NEEDED:** Does SuperTUI want to support **both models**?
1. Keep current: Precise time entries (9:00 AM - 5:30 PM)
2. Add weekly: Batch time entry (easier data entry, matches TUI pattern)

**RECOMMEND:** Support BOTH:
- `TimeEntry` model for precise tracking (timer, Pomodoro)
- `WeeklyTimeEntry` model for bulk entry (end of day/week reporting)
- Can convert between them (aggregate TimeEntry → WeeklyTimeEntry)

---

### Project Views

#### Project List with Stats (from ALCAR ProjectsScreen)
```
Projects                                                    Total Time
─────────────────────────────────────────────────────────  ──────────
○ SuperTUI Development              [████████░░] 80%       120.5h
● Website Redesign                  [██████████] 100%       45.0h
◐ Client Project Alpha              [████░░░░░░] 40%        28.5h
○ Documentation Update              [███░░░░░░░] 30%        12.0h
```

**Key Features:**
- Status symbol (○◐●) for project status
- Progress bar with percentage
- Total time spent (from time entries)
- Visual progress indicator

**SuperTUI Currently:** ProjectStatsWidget exists - check if it has progress bars

**ADOPT:** Progress bar visualization (ASCII: `[████░░]` or WPF ProgressBar)

---

#### Project Detail View (from praxis-main)
```
Project: SuperTUI Development
Status: Active          Started: 2025-09-01          Due: 2025-12-31
Estimated: 200h         Actual: 120.5h              Remaining: 79.5h

Tasks: 25 total, 20 completed (80%), 5 active, 0 blocked

Recent Time Entries (Week of Oct 27):
ID1      ID2           Mon   Tue   Wed   Thu   Fri   Total
PROJ     Architecture  8.0   7.5   8.0   8.0   4.0   35.5
PROJ     Testing       2.0   3.0   2.5   2.0   4.0   13.5
                      ────  ────  ────  ────  ────  ─────
                      10.0  10.5  10.5  10.0   8.0   49.0
```

**ADOPT:** Combine project info + task summary + time tracking in one view

---

### Dashboard/Overview

#### KPI Dashboard (from TaskProPro)
```
╔════════════════════════════════════════════════════════════════════╗
║                     PRODUCTIVITY DASHBOARD                         ║
╚════════════════════════════════════════════════════════════════════╝

TODAY                          THIS WEEK                   THIS MONTH
─────────────────────────────  ──────────────────────────  ────────────────────
Tasks Due: 5                   Tasks Completed: 18         Tasks Completed: 67
Completed: 3                   Hours Logged: 42.5          Hours Logged: 175.0
Remaining: 2                   Avg/Day: 8.5                Projects Active: 5
Hours Today: 6.5               Top Project: SuperTUI       Top Priority: Today (8)

OVERDUE TASKS (3)              PROJECT PROGRESS
─────────────────────────────  ────────────────────────────────────────────────
‼ Fix critical bug (Work)      SuperTUI         [████████░░] 80%  120.5h/200h
↑ Submit report (Work)         Website          [██████████] 100%  45.0h/45h
→ Call client (Personal)       Client Alpha     [████░░░░░░] 40%   28.5h/80h

RECENT ACTIVITY
────────────────────────────────────────────────────────────────────────────
2025-10-27 14:30  Completed: Write test cases
2025-10-27 13:15  Started: Code review
2025-10-27 09:00  Completed: Daily standup
```

**SuperTUI Currently:** Unknown - check if dashboard widget exists

**ADOPT:**
- KPI summary boxes (today/week/month)
- Overdue tasks prominent display
- Project progress visualization
- Recent activity feed

---

## PART 3: FEATURE INTEGRATION - How They Work Together

### 1. Task → Project Integration

#### Pattern from Terminal TUIs:
```csharp
// ASSIGNMENT
task.ProjectId = project.Id;
task.ExternalId1 = project.ExternalId1;  // Inherit for Excel export

// FILTERING
var projectTasks = taskService.GetTasks(t => t.ProjectId == projectId);

// AUTO-UPDATE PROJECT STATS
project.TotalTasks = tasks.Count;
project.CompletedTasks = tasks.Count(t => t.Status == Completed);
project.CompletionPercentage = (completed / total) * 100;

// CONTEXT-AWARE CREATION
// When viewing a project, new tasks auto-assign to that project
if (currentContext.ProjectId != null) {
    newTask.ProjectId = currentContext.ProjectId;
}
```

**SuperTUI Currently:** Has ProjectId field - check if stats auto-update

**ADOPT:**
- Auto-update project stats when tasks change
- Context-aware task creation (inherit project from current view)
- Bulk operations (complete all tasks in project)

---

### 2. Task → Time Integration

#### Pattern from Terminal TUIs:
```csharp
// TWO LINKING APPROACHES

// Approach 1: Direct task link (hourly time entries)
timeEntry.TaskId = task.Id;
task.ActualDuration = Sum(timeEntries.Where(te => te.TaskId == task.Id).Duration);

// Approach 2: Indirect via External IDs (weekly time entries)
weeklyEntry.ExternalId1 = task.ExternalId1;  // e.g., "PROJ"
weeklyEntry.ExternalId2 = task.ExternalId2;  // e.g., "BugFix"
// Match via string comparison for Excel import/export

// TIME SUMMARY IN TASK DETAIL
task.TotalTimeSpent = Sum(timeEntries.Duration);
task.RemainingTime = task.EstimatedDuration - task.TotalTimeSpent;

// REPORTS
var taskTimeReport = tasks.Select(t => new {
    Title = t.Title,
    Estimated = t.EstimatedDuration,
    Actual = Sum(timeEntries[t.Id]),
    Variance = Actual - Estimated
});
```

**SuperTUI Currently:** TimeTrackingService exists - check if tasks link to time

**ADOPT:**
- Show time summary in task detail view (estimated vs actual)
- Support both direct TaskId link AND External ID matching
- Variance tracking (over/under estimate)

---

### 3. Project → Time Integration

#### Pattern from Terminal TUIs:
```csharp
// PROJECT TIME BUDGET
project.EstimatedHours = 200;
project.ActualHours = Sum(weeklyEntries.Where(w => w.ProjectId == projectId).TotalHours);
project.RemainingHours = project.EstimatedHours - project.ActualHours;
project.BudgetStatus = project.ActualHours > project.EstimatedHours ? "Over" : "Under";

// WEEKLY PROJECT TIME VIEW
var projectWeek = weeklyEntries
    .Where(w => w.ProjectId == projectId && w.WeekEndingFriday == currentWeek)
    .Sum(w => w.TotalHours);

// BILLING CALCULATION
var billableHours = timeEntries
    .Where(te => te.ProjectId == projectId && te.IsBillable)
    .Sum(te => te.Duration.TotalHours);
var billingAmount = billableHours * project.BillingRate;

// FISCAL YEAR REPORTING
var fiscalYearTime = weeklyEntries
    .Where(w => w.FiscalYear == "2025-2026" && w.ProjectId == projectId)
    .Sum(w => w.TotalHours);
```

**ADOPT:**
- Project-level time budgets with over/under tracking
- Billing calculations (billable hours × rate)
- Fiscal year aggregation (April-March, not calendar year)

---

### 4. Tags Integration

#### Pattern from Terminal TUIs:
```csharp
// STORAGE
task.Tags = new List<string> { "urgent", "feature", "backend" };

// FILTERING (AND logic)
var taggedTasks = tasks.Where(t =>
    t.Tags.Contains("urgent") && t.Tags.Contains("feature")
);

// AGGREGATION
var tagCounts = tasks
    .SelectMany(t => t.Tags)
    .GroupBy(tag => tag)
    .Select(g => new { Tag = g.Key, Count = g.Count() })
    .OrderByDescending(x => x.Count);

// TAG MANAGEMENT
// Rename: Replace "old-tag" with "new-tag" across all tasks
foreach (var task in tasks) {
    if (task.Tags.Contains("old-tag")) {
        task.Tags.Remove("old-tag");
        task.Tags.Add("new-tag");
    }
}

// Auto-suggest recent tags
var recentTags = tasks
    .SelectMany(t => t.Tags)
    .Distinct()
    .OrderByDescending(tag => tasks.Count(t => t.Tags.Contains(tag)))
    .Take(10);
```

**SuperTUI Currently:** Tags exist in TaskItem - check if autocomplete/management exists

**ADOPT:**
- Tag autocomplete from recent/popular tags
- Tag counts in filter pane
- Tag rename/merge operations
- Multi-tag filtering (AND/OR logic)

---

### 5. Excel Integration - THE BIG ONE ⚠️

#### Pattern from Terminal TUIs:

**EXPORT - Cell Mapping**
```csharp
// Export tasks to Excel with specific cell mapping
void ExportToExcel(string filePath, List<TaskItem> tasks) {
    var excel = new Excel.Application();
    var workbook = excel.Workbooks.Add();
    var sheet = workbook.Sheets[1];

    // HEADER ROW
    sheet.Cells[1, 1] = "ID";
    sheet.Cells[1, 2] = "Title";
    sheet.Cells[1, 3] = "Status";
    sheet.Cells[1, 4] = "Priority";
    sheet.Cells[1, 5] = "Due Date";
    sheet.Cells[1, 6] = "Project";
    sheet.Cells[1, 7] = "External ID1";
    sheet.Cells[1, 8] = "External ID2";
    sheet.Cells[1, 9] = "Estimated (hrs)";
    sheet.Cells[1, 10] = "Actual (hrs)";

    // DATA ROWS
    for (int i = 0; i < tasks.Count; i++) {
        var task = tasks[i];
        int row = i + 2;  // Skip header

        sheet.Cells[row, 1] = task.Id.ToString();
        sheet.Cells[row, 2] = task.Title;
        sheet.Cells[row, 3] = task.Status.ToString();
        sheet.Cells[row, 4] = task.Priority.ToString();
        sheet.Cells[row, 5] = task.DueDate?.ToString("yyyy-MM-dd");
        sheet.Cells[row, 6] = task.ProjectId; // Or project name lookup
        sheet.Cells[row, 7] = task.ExternalId1;
        sheet.Cells[row, 8] = task.ExternalId2;
        sheet.Cells[row, 9] = task.EstimatedDuration?.TotalHours;
        sheet.Cells[row, 10] = task.ActualDuration.TotalHours;
    }

    workbook.SaveAs(filePath);
    excel.Quit();
}
```

**IMPORT - Field Mapping with Validation**
```csharp
// Import from Excel with duplicate detection
List<TaskItem> ImportFromExcel(string filePath) {
    var excel = new Excel.Application();
    var workbook = excel.Workbooks.Open(filePath);
    var sheet = workbook.Sheets[1];

    var tasks = new List<TaskItem>();
    int row = 2;  // Skip header

    while (!string.IsNullOrEmpty(sheet.Cells[row, 2].Value)) {
        var task = new TaskItem {
            // Parse ID - check if exists for update vs create
            Id = TryParseGuid(sheet.Cells[row, 1].Value) ?? Guid.NewGuid(),
            Title = sheet.Cells[row, 2].Value,
            Status = Enum.Parse<TaskStatus>(sheet.Cells[row, 3].Value),
            Priority = Enum.Parse<TaskPriority>(sheet.Cells[row, 4].Value),
            DueDate = TryParseDate(sheet.Cells[row, 5].Value),
            ExternalId1 = sheet.Cells[row, 7].Value,
            ExternalId2 = sheet.Cells[row, 8].Value,
            EstimatedDuration = TryParseHours(sheet.Cells[row, 9].Value),
        };

        // DUPLICATE DETECTION
        var existing = taskService.GetTasks(t =>
            t.ExternalId1 == task.ExternalId1 &&
            t.ExternalId2 == task.ExternalId2
        ).FirstOrDefault();

        if (existing != null) {
            // UPDATE existing task
            task.Id = existing.Id;
            task.ActualDuration = existing.ActualDuration;  // Preserve time data
            task.SubtaskIds = existing.SubtaskIds;  // Preserve relationships
        }

        tasks.Add(task);
        row++;
    }

    excel.Quit();
    return tasks;
}
```

**SuperTUI Currently:** ExcelExportWidget, ExcelImportWidget, ExcelMappingEditorWidget exist!

**CHECK NEEDED:**
1. Does mapping editor support **custom cell locations**? (e.g., W23, W78)
2. Does it handle **duplicate detection** via External IDs?
3. Does it preserve **relationships** (subtasks, time entries) on import?
4. Does it support **update existing** vs **create new**?

**ADOPT (if missing):**
- External ID fields for Excel round-trip
- Duplicate detection logic
- Relationship preservation on import
- Custom cell mapping (not just column mapping)
- Type conversion with validation

---

### 6. Search & Filtering

#### Multi-Criteria Filtering (from praxis-main)
```csharp
interface IFilterCriteria {
    string ProjectId { get; set; }
    TaskStatus? Status { get; set; }
    TaskPriority? Priority { get; set; }
    DateRange DueDate { get; set; }  // Overdue, Today, ThisWeek, Custom
    List<string> Tags { get; set; }  // AND or OR logic
    string SearchText { get; set; }
}

// APPLY FILTERS
var filtered = tasks.Where(t => {
    if (criteria.ProjectId != null && t.ProjectId != criteria.ProjectId) return false;
    if (criteria.Status != null && t.Status != criteria.Status) return false;
    if (criteria.Priority != null && t.Priority != criteria.Priority) return false;
    if (criteria.DueDate == DateRange.Overdue && t.DueDate >= DateTime.Now) return false;
    if (criteria.Tags.Any() && !criteria.Tags.All(tag => t.Tags.Contains(tag))) return false;
    if (!string.IsNullOrEmpty(criteria.SearchText) &&
        !t.Title.Contains(criteria.SearchText, StringComparison.OrdinalIgnoreCase)) return false;
    return true;
});

// FILTER COUNTS
var filterCounts = new {
    All = tasks.Count,
    Active = tasks.Count(t => t.Status != TaskStatus.Completed),
    Completed = tasks.Count(t => t.Status == TaskStatus.Completed),
    Overdue = tasks.Count(t => t.DueDate < DateTime.Now && t.Status != TaskStatus.Completed),
    Today = tasks.Count(t => t.Priority == TaskPriority.Today),
    HighPriority = tasks.Count(t => t.Priority == TaskPriority.High)
};
```

**ADOPT:**
- Multi-criteria filtering with AND logic
- Pre-calculate filter counts for display `[N]`
- Special filters: Overdue, Today, This Week
- Search across title + description + tags

---

## PART 4: IMPLEMENTATION PRIORITIES

### Phase 1: Data Model Enhancements (Week 1)
**Goal:** Add missing fields to match TUI capabilities

1. **Add to TaskItem:**
   ```csharp
   DateTime? CompletedDate;
   DateTime CreatedDate;
   DateTime ModifiedDate;
   TimeSpan? EstimatedDuration;
   TimeSpan ActualDuration;
   string ExternalId1;
   string ExternalId2;
   ```

2. **Add TaskPriority.Today:**
   ```csharp
   enum TaskPriority { Low, Medium, High, Today }
   ```

3. **Add to Project:**
   ```csharp
   TimeSpan? EstimatedHours;
   TimeSpan ActualHours { get; }  // Calculated
   decimal? BudgetAmount;
   decimal? BillingRate;
   ProjectStatus Status;
   string ExternalId1;
   string ExternalId2;
   ```

4. **Create WeeklyTimeEntry model:**
   ```csharp
   class WeeklyTimeEntry {
       // Full model from earlier
   }
   ```

**Effort:** 8-12 hours
**Impact:** HIGH - enables all other features

---

### Phase 2: UI Presentation (Week 2-3)
**Goal:** Adopt TUI visual patterns

5. **VisualVocabulary for symbols:**
   - Status: ○◐●
   - Priority: ↓→↑‼
   - Hierarchy: ↳

6. **Three-pane TaskManagementWidget:**
   - Left: Filters with counts
   - Center: Task list (compact)
   - Right: Details

7. **Weekly time grid view:**
   - Mon-Fri columns
   - ID1/ID2 rows
   - Daily totals
   - Fiscal year display

8. **Progress bars in ProjectStatsWidget:**
   - Visual completion percentage
   - Time budget tracking

**Effort:** 24-32 hours
**Impact:** HIGH - major UX improvement

---

### Phase 3: Feature Integration (Week 4)
**Goal:** Connect features like TUIs do

9. **Auto-update project stats:**
   - Listen to task changes
   - Recalculate completion %
   - Update time totals

10. **Context-aware task creation:**
    - Inherit project from current view
    - Suggest recent tags
    - Default priority to "Today" if due today

11. **Time integration:**
    - Show time summary in task details
    - Show variance (estimated vs actual)
    - Link WeeklyTimeEntry to tasks/projects

12. **Enhanced filtering:**
    - Multi-criteria with counts
    - Overdue, Today, This Week filters
    - Tag filtering (AND/OR logic)

**Effort:** 20-28 hours
**Impact:** MEDIUM - improved workflows

---

### Phase 4: Excel Integration (Week 5)
**Goal:** Robust import/export

13. **External ID fields in Excel export**
14. **Duplicate detection on import**
15. **Relationship preservation**
16. **Custom cell mapping** (if not already supported)

**Effort:** 12-16 hours
**Impact:** MEDIUM - critical for some users

---

## PART 5: QUESTIONS FOR USER

Before implementing, clarify:

### Data Model Questions:

1. **Weekly vs Hourly Time Entries:**
   - Keep current hourly TimeEntry model?
   - Add WeeklyTimeEntry model alongside it?
   - OR replace hourly with weekly?

2. **Fiscal Year:**
   - Use April-March fiscal year (like TUIs)?
   - OR allow configurable fiscal year start month?
   - OR ignore fiscal year and use calendar year?

3. **Task Priority "Today":**
   - Add as 4th priority level?
   - OR use a separate `IsDueToday` flag?

4. **External IDs:**
   - Add ExternalId1/ExternalId2 to all models?
   - OR different naming (ExternalCategory, ExternalCode)?

### UI Questions:

5. **Three-Pane Layout:**
   - Apply to TaskManagementWidget only?
   - OR also FileExplorerWidget, GitStatusWidget?
   - Make it optional layout or required?

6. **Visual Symbols:**
   - Use Unicode symbols (○◐●↓→↑)?
   - OR provide text-only mode for accessibility?

7. **Weekly Time Grid:**
   - Create new WeeklyTimeWidget?
   - OR add grid view to existing TimeTrackingWidget?

### Integration Questions:

8. **Auto-Update Behavior:**
   - Auto-update project stats on every task change (performance impact)?
   - OR update on demand (manual refresh)?

9. **Context Awareness:**
   - Track current project/task context globally?
   - OR per-workspace context?

10. **Filter Counts:**
    - Pre-calculate all filter counts (performance)?
    - OR calculate on-demand when filter pane visible?

---

## BOTTOM LINE

**SuperTUI is architecturally superior to all terminal TUIs.**

BUT it's missing battle-tested **data fields**, **UI patterns**, and **feature integrations** that users expect from productivity tools.

**The 10 recommendations above would bring SuperTUI to feature parity with the best terminal TUIs while leveraging WPF's superior capabilities.**

**Start with Phase 1 (data model) - it's the foundation for everything else.**

---

**Analysis Date:** 2025-10-27
**TUIs Analyzed:** 5 major implementations
**Recommendation:** Implement all 4 phases for production-ready productivity suite
