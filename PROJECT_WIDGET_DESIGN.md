# SuperTUI Project Management Widget - Design Document

**Date:** 2025-10-25
**Status:** Design Complete - Ready for Implementation

---

## Executive Summary

We're creating a comprehensive **Project Management System** for SuperTUI that synthesizes the best features from three mature TUI implementations (Praxis, ALCAR, PMC) while leveraging WPF's native capabilities.

**Key Insight:** The old TUIs represent years of real-world PMC (audit project) usage. We preserve their **architectural wisdom** and **data models** while using WPF to eliminate their **terminal constraints**.

---

## Design Philosophy

### What We Learned from Old TUIs

**Praxis-Main** ✅ Best Data Models
- Comprehensive Project model (180 lines) for audit work
- Clean Task model with helper methods
- Week-based time tracking with fiscal year support
- Performance-optimized services with hashtable indexes

**ALCAR** ✅ Best UI Patterns
- ThreePaneLayout (25/30/45 split)
- Professional component library
- Color coding and visual hierarchy
- LazyGit integration patterns

**PMC ConsoleUI** ✅ Best Architecture
- Screen-stack navigation with lifecycle
- Render-on-demand pattern
- Kanban board implementation
- Agenda view with time grouping
- Menu system with global shortcuts
- String/layout caching (70% performance gain)

**TaskProPro (C#)** ✅ Best Performance
- Zero-flicker rendering
- Gap buffer text editing
- Hierarchical task display
- Professional keyboard handling

---

## Core Architecture

### Widget Structure

```
ProjectManagementWidget (main container)
├── ProjectListWidget (left pane - 25%)
│   ├── Project list with stats
│   ├── Progress bars
│   ├── Color-coded status
│   └── Quick filters (Active/All/Archived)
│
├── ProjectContextWidget (middle pane - 35%)
│   ├── Tab view: Tasks/Files/Timeline/Notes
│   ├── Task list for selected project
│   ├── File browser integration
│   └── Project timeline
│
└── ProjectDetailsWidget (right pane - 40%)
    ├── Full project information
    ├── Client details
    ├── Audit periods
    ├── Contacts
    └── Statistics
```

**Additional Widgets (Separate):**
- `KanbanBoardWidget` - 3-column board (TODO/InProgress/Done)
- `AgendaWidget` - Time-grouped tasks (Overdue/Today/Tomorrow/Week)
- `ProjectStatsWidget` - Dashboard with metrics
- `TimeTrackingWidget` - Weekly time reports

---

## Data Models

### 1. Project Model (Praxis-based)

```csharp
public class Project
{
    // Identity
    public Guid Id { get; set; }
    public string FullProjectName { get; set; }
    public string Nickname { get; set; }

    // Project Codes (PMC standard)
    public string ID1 { get; set; }
    public string ID2 { get; set; }

    // Dates
    public DateTime? DateAssigned { get; set; }
    public DateTime? BFDate { get; set; }  // Brought Forward date
    public DateTime? DateDue { get; set; }
    public DateTime? ClosedDate { get; set; }

    // Audit Information
    public string AuditType { get; set; }
    public string AuditProgram { get; set; }
    public string AuditCase { get; set; }

    // Audit Periods (5 periods)
    public List<AuditPeriod> AuditPeriods { get; set; }

    // Client Information
    public string ClientID { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string Province { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }

    // Contacts (2 contacts)
    public List<ProjectContact> Contacts { get; set; }

    // Auditor Information
    public string AuditorName { get; set; }
    public string AuditorPhone { get; set; }
    public string AuditorTL { get; set; }  // Team Lead

    // System Information
    public string AccountingSoftware1 { get; set; }
    public string AccountingSoftware1Type { get; set; }
    public string AccountingSoftware2 { get; set; }
    public string AccountingSoftware2Type { get; set; }

    // File Paths
    public string CAAPath { get; set; }
    public string RequestPath { get; set; }
    public string T2020Path { get; set; }

    // Time Tracking
    public decimal CumulativeHrs { get; set; }

    // Notes
    public string Note { get; set; }
    public List<ProjectNote> Notes { get; set; }

    // Status
    public ProjectStatus Status { get; set; }
    public bool Archived { get; set; }

    // Base Model Fields
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool Deleted { get; set; }

    // Computed Properties
    public bool IsOverdue { get; }
    public bool IsDueSoon { get; }  // Within 7 days
    public int DaysUntilDue { get; }
    public decimal CompletionPercentage { get; }
}

public class AuditPeriod
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class ProjectContact
{
    public string Name { get; set; }
    public string Title { get; set; }
    public string Phone { get; set; }
    public string Address { get; set; }
}

public class ProjectNote
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
}

public enum ProjectStatus
{
    Planning = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4
}
```

### 2. Integration with Existing TaskItem

```csharp
// TaskItem already has:
public Guid? ProjectId { get; set; }  // ✅ Already exists!

// Add helper to TaskService:
public List<TaskItem> GetTasksForProject(Guid projectId)
{
    return GetTasks(t => t.ProjectId == projectId && !t.Deleted);
}

public ProjectTaskStats GetProjectStats(Guid projectId)
{
    var tasks = GetTasksForProject(projectId);
    return new ProjectTaskStats {
        Total = tasks.Count,
        Completed = tasks.Count(t => t.Status == TaskStatus.Completed),
        InProgress = tasks.Count(t => t.Status == TaskStatus.InProgress),
        Pending = tasks.Count(t => t.Status == TaskStatus.Pending),
        Overdue = tasks.Count(t => t.IsOverdue),
        CompletionPercentage = tasks.Count == 0 ? 0 :
            (decimal)tasks.Count(t => t.Status == TaskStatus.Completed) / tasks.Count * 100
    };
}
```

### 3. TimeEntry Model (Week-Based)

```csharp
public class TimeEntry
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }

    // Week-based tracking (Friday week ending)
    public string WeekEndingFriday { get; set; }  // yyyyMMdd format

    // Daily hours (Monday-Friday)
    public decimal Monday { get; set; }
    public decimal Tuesday { get; set; }
    public decimal Wednesday { get; set; }
    public decimal Thursday { get; set; }
    public decimal Friday { get; set; }

    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Computed
    public decimal TotalHours => Monday + Tuesday + Wednesday + Thursday + Friday;
    public int FiscalYear { get; }  // Apr 1 - Mar 31
}
```

---

## Service Layer

### ProjectService

```csharp
public class ProjectService
{
    private static ProjectService instance;
    public static ProjectService Instance => instance ??= new ProjectService();

    private Dictionary<Guid, Project> projects;
    private Dictionary<string, Guid> nicknameIndex;  // Fast nickname lookup
    private Dictionary<string, Guid> id1Index;       // Fast ID1 lookup
    private string dataFilePath;
    private readonly object lockObject = new object();

    // Events
    public event Action<Project> ProjectAdded;
    public event Action<Project> ProjectUpdated;
    public event Action<Guid> ProjectDeleted;
    public event Action ProjectsReloaded;

    // CRUD
    public void Initialize(string filePath = null);
    public List<Project> GetAllProjects(bool includeDeleted = false);
    public List<Project> GetProjects(Func<Project, bool> predicate);
    public Project GetProject(Guid id);
    public Project GetProjectByNickname(string nickname);
    public Project GetProjectByID1(string id1);
    public Project AddProject(Project project);
    public bool UpdateProject(Project project);
    public bool DeleteProject(Guid id, bool hardDelete = false);

    // Statistics
    public List<ProjectWithStats> GetProjectsWithStats();
    public ProjectStats GetProjectStats(Guid projectId);

    // Search/Filter
    public List<Project> SearchProjects(string searchText);
    public List<Project> GetActiveProjects();
    public List<Project> GetOverdueProjects();
    public List<Project> GetProjectsByStatus(ProjectStatus status);

    // Persistence
    private async Task SaveToFileAsync();
    private void LoadFromFile();
    public void Dispose();
}

public class ProjectWithStats
{
    public Project Project { get; set; }
    public ProjectTaskStats TaskStats { get; set; }
    public decimal TimeSpent { get; set; }
}

public class ProjectTaskStats
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Pending { get; set; }
    public int Overdue { get; set; }
    public decimal CompletionPercentage { get; set; }
}
```

### TimeTrackingService

```csharp
public class TimeTrackingService
{
    private static TimeTrackingService instance;
    public static TimeTrackingService Instance => instance ??= new TimeTrackingService();

    private Dictionary<Guid, TimeEntry> timeEntries;
    private Dictionary<string, List<Guid>> weekIndex;  // Week -> Entry IDs
    private string dataFilePath;
    private readonly object lockObject = new object();

    // Events
    public event Action<TimeEntry> TimeEntryAdded;
    public event Action<TimeEntry> TimeEntryUpdated;
    public event Action<Guid> TimeEntryDeleted;

    // CRUD
    public void Initialize(string filePath = null);
    public List<TimeEntry> GetAllTimeEntries();
    public TimeEntry GetTimeEntry(Guid id);
    public List<TimeEntry> GetTimeEntriesForProject(Guid projectId);
    public List<TimeEntry> GetTimeEntriesForWeek(string weekEndingFriday);
    public TimeEntry AddTimeEntry(TimeEntry entry);
    public bool UpdateTimeEntry(TimeEntry entry);
    public bool DeleteTimeEntry(Guid id);

    // Calculations
    public decimal GetTotalHoursForProject(Guid projectId);
    public decimal GetTotalHoursForWeek(string weekEndingFriday);
    public WeeklyTimeReport GetWeeklyReport(string weekEndingFriday);
    public List<ProjectTimeAggregate> GetProjectTimeAggregates();

    // Helpers
    public string GetCurrentWeekEnding();
    public string GetPreviousWeekEnding(string weekEndingFriday);
    public string GetNextWeekEnding(string weekEndingFriday);
    public int GetFiscalYear(DateTime date);

    // Persistence
    private async Task SaveToFileAsync();
    private void LoadFromFile();
    public void Dispose();
}
```

---

## UI Components

### 1. ProjectManagementWidget (Main)

**Layout:** 3-column Grid (25% | 35% | 40%)

**Left Pane (ProjectListWidget):**
```
┌─ PROJECTS ────────────┐
│ 🔍 Search...          │
│ ┌─ Filters ─────────┐ │
│ │ ○ Active          │ │
│ │ ● All             │ │
│ │ ○ Archived        │ │
│ └───────────────────┘ │
│                       │
│ > ABC Corp [████░░] 80%│
│   Smith Audit         │
│   Due: Oct 30         │
│                       │
│   XYZ Ltd [███░░░] 60% │
│   Jones Review        │
│   OVERDUE (Oct 20)    │
│                       │
│   ... 15 more         │
└───────────────────────┘
 Status: 17 projects
```

**Features:**
- Search textbox at top
- Filter radio buttons (Active/All/Archived)
- Project list with:
  - Progress bars
  - Nickname (bold) + FullProjectName (gray)
  - Due date with color coding
  - Selection highlighting
- Status bar with count

**Middle Pane (ProjectContextWidget):**
```
┌─ ABC Corp ─────────────────┐
│ [Tasks] Files Timeline Notes│
│                             │
│ ☐ M Complete audit fieldwork│
│   Due: Oct 28               │
│                             │
│ ☑ H Review financials       │
│   Completed: Oct 24         │
│                             │
│ ☐ L Draft report            │
│   Pending                   │
│                             │
│ ... 12 more tasks           │
│                             │
│ [+ Add Task]                │
└─────────────────────────────┘
 8 tasks | 3 complete | 1 overdue
```

**Features:**
- Tab control: Tasks/Files/Timeline/Notes
- **Tasks Tab:**
  - Task list for selected project
  - Status icons, priority indicators
  - Quick add button
  - Keyboard shortcuts from TaskManagementWidget
- **Files Tab:**
  - CAAPath, RequestPath, T2020Path folders
  - File browser integration
  - Open in explorer
- **Timeline Tab:**
  - Key dates visualization
  - Audit periods display
  - Milestone markers
- **Notes Tab:**
  - Project notes list
  - Add/edit/delete notes

**Right Pane (ProjectDetailsWidget):**
```
┌─ PROJECT DETAILS ───────────┐
│ Full Name: ABC Corp Annual  │
│ Nickname:  ABC Corp         │
│ ID1/ID2:   12345 / 67890    │
│                             │
│ Status:    Active           │
│ Assigned:  Oct 1, 2025      │
│ Due:       Oct 30, 2025     │
│ Progress:  80% (8/10 tasks) │
│                             │
│ ▸ Client Information        │
│   ClientID: C12345          │
│   Address:  123 Main St     │
│   City:     Toronto         │
│                             │
│ ▸ Audit Details             │
│   Type:     Annual Review   │
│   Program:  Standard        │
│   Case:     2025-001        │
│                             │
│ ▸ Contacts                  │
│   John Smith - CEO          │
│   Phone: 416-555-1234       │
│                             │
│ ▸ Auditor                   │
│   Name: Jane Doe            │
│   TL: Bob Manager           │
│                             │
│ ▸ Time Tracking             │
│   Total: 45.5 hours         │
│   This Week: 8.0 hours      │
│                             │
│ [Edit Project]              │
└─────────────────────────────┘
```

**Features:**
- Collapsible sections (▸/▾)
- All project fields displayed
- Word-wrapped notes
- Edit button at bottom
- Statistics summary

---

### 2. KanbanBoardWidget

**Layout:** 3 columns

```
┌────── TODO ───────┬──── IN PROGRESS ────┬───── DONE ──────┐
│                   │                     │                 │
│ ☐ M Complete docs │ ◐ H Review code     │ ☑ Draft report  │
│   ABC Corp        │   XYZ Ltd           │   ABC Corp      │
│   Due: Oct 28     │   Due: Oct 26       │   Oct 24        │
│                   │                     │                 │
│ ☐ L Update tests  │ ◐ M Client meeting  │ ☑ Setup env     │
│   ABC Corp        │   ABC Corp          │   XYZ Ltd       │
│   Due: Oct 30     │   Today             │   Oct 23        │
│                   │                     │                 │
│ ... 8 more        │ ... 3 more          │ ... 12 more     │
│                   │                     │                 │
└───────────────────┴─────────────────────┴─────────────────┘
 ←→: Column | ↑↓: Task | 1-3: Move | Enter: Edit | Esc: Back
```

**Features:**
- 3 columns: Pending / InProgress / Completed
- Drag-and-drop between columns (or 1/2/3 keys)
- Color-coded by due date
- Project name shown
- Arrow navigation between columns
- Enter to edit task details

---

### 3. AgendaWidget

**Layout:** Time-grouped sections

```
┌─ AGENDA VIEW ──────────────────────────────────────────────┐
│                                                            │
│ ▾ OVERDUE (3)                                              │
│   ☐ H Review financials               XYZ Ltd    Oct 20   │
│   ☐ M Complete fieldwork               ABC Corp   Oct 22   │
│   ☐ L Update documentation             DEF Inc    Oct 23   │
│                                                            │
│ ▾ TODAY (2)                                                │
│   ☐ H Client meeting                   ABC Corp   Oct 25   │
│   ◐ M Draft report section             XYZ Ltd    Oct 25   │
│                                                            │
│ ▾ TOMORROW (1)                                             │
│   ☐ M Review with manager              ABC Corp   Oct 26   │
│                                                            │
│ ▸ THIS WEEK (5)                                            │
│                                                            │
│ ▸ LATER (8)                                                │
│                                                            │
│ ▸ NO DUE DATE (4)                                          │
│                                                            │
└────────────────────────────────────────────────────────────┘
 Space: Expand/Collapse | Enter: Edit | D: Mark Done | Esc: Back
```

**Features:**
- Collapsible sections by time group
- Overdue (red), Today (yellow), Tomorrow, Week, Later, No Due
- Expand/collapse with Space or mouse
- Shows project name
- Color-coded priorities

---

### 4. ProjectStatsWidget (Dashboard)

**Layout:** Grid of metrics

```
┌─ PROJECT DASHBOARD ────────────────────────────────────────┐
│                                                            │
│  ACTIVE PROJECTS          TASKS                TIME        │
│  ┌─────────────┐      ┌─────────────┐     ┌─────────────┐│
│  │     17      │      │    142      │     │   324.5     ││
│  │   projects  │      │    tasks    │     │   hours     ││
│  └─────────────┘      └─────────────┘     └─────────────┘│
│                                                            │
│  COMPLETION           OVERDUE              DUE SOON       │
│  ┌─────────────┐      ┌─────────────┐     ┌─────────────┐│
│  │     68%     │      │      8      │     │      5      ││
│  │ [████████░░]│      │    tasks    │     │   projects  ││
│  └─────────────┘      └─────────────┘     └─────────────┘│
│                                                            │
│  TOP PROJECTS BY TIME         RECENT ACTIVITY             │
│  ┌────────────────────┐      ┌──────────────────────────┐│
│  │ ABC Corp    45.5h  │      │ ● Task completed          ││
│  │ XYZ Ltd     38.2h  │      │   "Review financials"     ││
│  │ DEF Inc     29.8h  │      │   2 hours ago             ││
│  │ GHI Co      24.1h  │      │                           ││
│  │ JKL LLC     19.5h  │      │ ○ Project created         ││
│  └────────────────────┘      │   "MNO Partners Audit"    ││
│                              │   5 hours ago             ││
│                              └──────────────────────────┘│
└────────────────────────────────────────────────────────────┘
```

**Features:**
- 6 metric cards
- Top projects by time chart
- Recent activity feed
- Auto-refresh every 30 seconds
- Click metric to drill down

---

## Keyboard Shortcuts

### Global (Works in any widget)
| Shortcut | Action |
|----------|--------|
| **Ctrl+Alt+P** | Switch to Projects view |
| **Ctrl+Alt+K** | Switch to Kanban board |
| **Ctrl+Alt+A** | Switch to Agenda view |
| **Ctrl+Alt+D** | Switch to Dashboard |

### Project List
| Shortcut | Action |
|----------|--------|
| **↑/↓** | Navigate projects |
| **Enter** | Select project / view details |
| **N** | New project |
| **E** | Edit selected project |
| **D** | Delete selected project |
| **A** | Archive/unarchive |
| **/** | Focus search box |

### Task List (in Project Context)
| Shortcut | Action |
|----------|--------|
| **A** | Add task to project |
| **E** | Edit selected task |
| **D** | Delete selected task |
| **Space** | Toggle task status |
| **P** | Cycle priority |
| **M** | Move to different project |

### Kanban Board
| Shortcut | Action |
|----------|--------|
| **←/→** | Switch column |
| **↑/↓** | Navigate tasks |
| **1/2/3** | Move task to TODO/InProgress/Done |
| **Enter** | Edit task |
| **D** | Mark done (move to Done column) |

---

## Implementation Plan

### Phase 1: Data Models & Services (Subagent 1)
1. Create `ProjectModels.cs` with Project, AuditPeriod, ProjectContact, etc.
2. Create `TimeTrackingModels.cs` with TimeEntry
3. Create `ProjectService.cs` with full CRUD + statistics
4. Create `TimeTrackingService.cs` with week-based tracking
5. Add integration methods to `TaskService.cs`

### Phase 2: Main Project Widget (Subagent 2)
1. Create `ProjectManagementWidget.cs` - 3-pane layout
2. Create `ProjectListControl.cs` - Left pane with search/filters
3. Create `ProjectContextControl.cs` - Middle pane with tabs
4. Create `ProjectDetailsControl.cs` - Right pane with details
5. Wire up navigation and data binding

### Phase 3: Additional Widgets (Subagent 3)
1. Create `KanbanBoardWidget.cs` - 3-column board
2. Create `AgendaWidget.cs` - Time-grouped view
3. Create `ProjectStatsWidget.cs` - Dashboard
4. Create `TimeTrackingWidget.cs` - Weekly time reports

### Phase 4: Integration & Polish (Final)
1. Update SuperTUI.ps1 to include new widgets
2. Add keyboard shortcuts to ShortcutManager
3. Test all CRUD operations
4. Verify state persistence
5. Build verification

---

## Performance Considerations

### From Old TUI Lessons:

1. **Hashtable Indexes** (Praxis pattern):
   ```csharp
   private Dictionary<string, Guid> nicknameIndex;
   private Dictionary<string, Guid> id1Index;
   ```
   O(1) lookups instead of O(n) searches

2. **ObservableCollection** (Already implemented in TaskManagementWidget):
   ```csharp
   public ObservableCollection<ProjectWithStats> Projects { get; set; }
   ```
   WPF auto-updates UI

3. **Async Save with Debouncing** (Already implemented):
   ```csharp
   private Timer saveTimer;
   private const int SAVE_DEBOUNCE_MS = 500;
   ```
   Non-blocking I/O

4. **Computed Property Caching**:
   ```csharp
   private decimal? cachedCompletionPercentage;
   public decimal CompletionPercentage {
       get {
           if (!cachedCompletionPercentage.HasValue)
               cachedCompletionPercentage = CalculateCompletion();
           return cachedCompletionPercentage.Value;
       }
   }
   ```

5. **Virtualization** (Already enabled):
   ```csharp
   VirtualizingPanel.SetIsVirtualizing(listBox, true);
   ```

---

## Success Criteria

✅ **All project CRUD operations work**
✅ **3-pane layout with synchronized selection**
✅ **Tasks integrate with projects seamlessly**
✅ **Kanban board with drag-and-drop**
✅ **Agenda view with time grouping**
✅ **Dashboard with real-time statistics**
✅ **Time tracking with week-based entries**
✅ **Keyboard-driven workflow**
✅ **State persistence with backup**
✅ **Build succeeds with 0 errors**
✅ **Performance: <100ms for all operations**

---

## Next Steps

1. **Approve this design**
2. **Execute implementation with 3 parallel subagents**
3. **Test on Windows**
4. **Document for users**

**Estimated Time:** 3-4 hours with parallel subagents
**Lines of Code:** ~3,000 new lines across 12 files
**Build Target:** Same as current (0 errors, 0 warnings)

---

**This design synthesizes 14,599 lines of PMC ConsoleUI + 7,641 lines of Praxis + ALCAR + TaskProPro patterns into a cohesive WPF-native project management system that preserves what worked while eliminating terminal constraints.**
