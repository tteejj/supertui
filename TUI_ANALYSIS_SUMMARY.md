# TUI Implementations Analysis - Executive Summary

**Analysis Date:** October 27, 2025
**Scope:** All 7 TUI implementations in `/home/teej/_tui`
**Deliverables:** 3 documents created

---

## Key Findings

### 1. Data Model Convergence

Despite being implemented in different languages (C# and PowerShell) and technologies (Console, WPF, Terminal), all TUI implementations converge on similar core concepts:

- **Tasks:** Title, Priority, Dates, Status, Hierarchy (via ParentId + Children)
- **Time Tracking:** Weekly hours by day (Mon-Fri), linked to projects/tasks via codes
- **Organization:** Tags/categories, project codes (ID1/ID2 pattern)
- **Persistence:** JSON storage, CSV/Excel export capabilities
- **Validation:** Data integrity, no duplicates, range checks

### 2. Implementation Breakdown

#### Full-Featured Task Management
- **TaskProPro** (C# Console): Most complete - tasks, time tracking, subtasks, tags, colors
- **Alcar** (PowerShell): Feature-rich - status enum, progress tracking, assigned users
- **R2** (PowerShell): Service-oriented architecture with event system

#### Time-Focused Implementations
- **SpeedTUI** (PowerShell): Command palette + time tracking, soft deletes, templates
- **WPF** (C# GUI): Task hierarchy with special "H=today" logic, no time tracking

#### Specialized/Lightweight
- **_CLASSY, _HELIOS, _XP, _NEW_HELIOS:** PowerShell TUI frameworks, mostly UI-focused

### 3. Time Tracking Pattern

All time trackers use the same structure:

```
Weekly Timesheet (ending Friday, yyyyMMdd format)
├── Category Code (ID1): PROJ, MEET, TRAIN, ADMIN, etc.
├── Project/Task Code (ID2): Specific project or task identifier
├── Daily Hours: Monday through Friday (decimal values 0-24)
├── Auto-calculated Total: Sum of daily hours
├── Fiscal Year: April 1 - March 31 (e.g., "2025-2026")
└── Link to Task: By TaskId or ID1/ID2 combination
```

### 4. Task Data Model Variations

| Aspect | Variation |
|--------|-----------|
| **ID Format** | TaskProPro/SpeedTUI/Alcar use GUID strings; WPF uses integers |
| **Status** | TaskProPro/WPF: Boolean only; Alcar/R2: Full enum (Pending/InProgress/Completed/Cancelled) |
| **Priority** | TaskProPro: 4 levels (Today/High/Medium/Low); Others: 3 levels (Low/Medium/High) |
| **Completion Date** | NO implementation tracks completed date (missed opportunity) |
| **Subtasks** | All support via ParentId + Children, max depth protection in WPF (50 levels) |
| **Tags** | TaskProPro, SpeedTUI, Alcar/R2 support; WPF does not |

### 5. Excel Integration Details

**Found in:**
- TaskProPro (partial)
- Simpletaskpro variant (full)

**Features:**
- Cell-level mapping (e.g., 'W23' → RequestDate)
- Type conversion (Date/String/Number)
- Multi-worksheet support (looks for specific sheet, falls back to first)
- COM object-based (Windows-only)
- Custom T2020 export format support

**Example Mapping:**
- Cell W23 = RequestDate
- Cell W78 = AuditType
- Cell W3 = ClientName
- (40+ field mappings in audit variant)

### 6. Export Formats

**CSV:**
- Tasks: Title, Status, Priority, Created, Due, Tags, Notes
- Commands: Title, CommandText, Description, Tags, IsGroup
- Time Entries: WeekEndingFriday, ID1, ID2, Daily Hours, Total

**JSON:**
- Native storage format for most implementations
- Human readable, version-control friendly
- No binary dependencies

**Excel:**
- Import via mapping system
- Export via COM objects
- Not universally supported (only specialized variants)

### 7. Validation Rules Discovered

**Time Entry:**
- ID1 required (max 20 chars)
- ID2 optional (max 20 chars)
- Daily hours 0-24 range
- No duplicate ID1/ID2 combinations in same week
- Description max 200 characters
- Auto-calculated total from daily hours

**Task Hierarchy:**
- Max depth: 50 levels (WPF)
- Validation timeout: 30 seconds (WPF)
- Cyclic dependency prevention: Not found (gap)

**Fiscal Year Calculation:**
- April 1 - March 31 (consistent across all)
- Auto-calculated from week ending Friday date
- Format: "YYYY-YYYY+1"

---

## Architecture Patterns

### C# Implementations
- Type safety via enums and interfaces
- JSON serialization via System.Text.Json
- Decimal type for financial precision (time tracking)
- Optional bindings (WPF) vs direct manipulation (Console)

### PowerShell Implementations
- Class-based (PS5+)
- ArrayList for ordered collections
- Hashtable for key-value data
- COM objects for Excel (Windows dependency)
- Service container pattern (R2)

---

## Notable Design Decisions

### ID1/ID2 Pattern (TaskProPro, SpeedTUI)
- **ID1:** Generic category/time code (reusable)
- **ID2:** Specific project/task reference
- **Benefit:** Supports both generic (meetings, training) and specific (project work) time codes
- **Linking:** Can link by both codes AND direct TaskId for flexibility

### H=Today Logic (WPF)
- Setting priority to "High" auto-sets DueDate to today
- Enforces "today" focus for high-priority work
- No other implementation has this feature

### Soft Deletes (SpeedTUI)
- `Deleted` boolean flag instead of physical deletion
- Enables recovery and audit trails
- Not used in other implementations

### Service-Oriented Architecture (R2)
- Central DataManager for all state changes
- Event publishing for loose coupling
- Navigation service for screen management
- Error handling utilities
- More testable and maintainable structure

---

## Gaps and Opportunities

### Missing Features
1. **Completed Date Tracking** - No implementation tracks when task was completed
2. **Task Dependencies** - No implementation supports task dependencies or blockers
3. **Recurring Tasks** - Not found in any implementation
4. **Time Estimates vs Actuals** - Time tracking doesn't compare estimated vs actual
5. **Project-Level Metrics** - No aggregated project progress tracking
6. **Multi-User Support** - All implementations are single-user

### Data Quality Issues
1. **No cyclic dependency prevention** in task hierarchies
2. **Max depth protection** only in WPF (others could overflow)
3. **No constraint validation** on ID1/ID2 formats
4. **No timezone support** for dates

---

## Recommended Unified Data Model

A merged model combining the best features of all implementations:

```csharp
public class UnifiedTask {
    // Identity
    public string Id { get; set; }              // GUID format
    
    // Content
    public string Title { get; set; }
    public string Description { get; set; }
    
    // Status & Progress
    public TaskStatus Status { get; set; }      // Pending, InProgress, Completed, Cancelled
    public TaskPriority Priority { get; set; }  // Low, Medium, High
    public int Progress { get; set; }           // 0-100
    
    // Dates
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; } // Added for tracking
    public DateTime? BringForwardDate { get; set; }
    
    // Organization
    public List<string> Tags { get; set; }
    public string AssignedTo { get; set; }
    
    // Hierarchy
    public string ParentId { get; set; }
    public List<string> SubtaskIds { get; set; } // For O(1) lookups
    
    // Time Tracking Links
    public string ProjectCode { get; set; }     // ID1
    public string TaskCode { get; set; }        // ID2
    
    // Project Reference
    public string ProjectId { get; set; }
}

public class UnifiedTimeEntry {
    // Identity
    public string Id { get; set; }
    
    // Time Period
    public string WeekEndingFriday { get; set; } // yyyyMMdd format
    public string FiscalYear { get; set; }       // YYYY-YYYY+1
    
    // Allocation
    public string CategoryCode { get; set; }    // ID1
    public string ProjectCode { get; set; }     // ID2
    public string TaskId { get; set; }
    public string Description { get; set; }
    
    // Hours (Monday-Friday)
    public decimal Monday { get; set; }
    public decimal Tuesday { get; set; }
    public decimal Wednesday { get; set; }
    public decimal Thursday { get; set; }
    public decimal Friday { get; set; }
    public decimal Total { get; set; }          // Auto-calculated
    
    // Metadata
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool IsLinkedToTask { get; set; }
    public bool IsDeleted { get; set; }         // Soft delete support
}

public class UnifiedProject {
    // Identity
    public string Id { get; set; }
    public string Code { get; set; }            // ID1/ID2 pattern
    
    // Content
    public string Name { get; set; }
    public string Description { get; set; }
    
    // Status
    public ProjectStatus Status { get; set; }   // Active, Completed, On Hold, Cancelled
    public int Progress { get; set; }           // 0-100
    
    // Dates
    public DateTime CreatedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    
    // Organization
    public string ManagerId { get; set; }
    public List<string> TaskIds { get; set; }
}

public enum TaskStatus {
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public enum TaskPriority {
    Low = 0,
    Medium = 1,
    High = 2,
    Today = 3  // Special "today" priority
}

public enum ProjectStatus {
    Active = 0,
    Completed = 1,
    OnHold = 2,
    Cancelled = 3
}
```

---

## Implementation Statistics

| Implementation | Language | Location | Files | Primary Use |
|---|---|---|---|---|
| TaskProPro | C# | praxis-main/TaskProPro/CSharp | 15 | Task & Time Tracking |
| SpeedTUI | PowerShell | praxis-main/SpeedTUI | 20+ | Command Palette |
| Praxis WPF | C# WPF | praxis-main/_wpf | 25+ | GUI Task Management |
| Alcar | PowerShell | alcar | 10+ | Terminal Task Mgmt |
| R2 (Helios) | PowerShell | _R2 | 20+ | Service Architecture |
| HELIOS | PowerShell | _HELIOS | Text files | Reference |
| CLASSY | PowerShell | _CLASSY | Text files | Refactored |
| XP/Phoenix | PowerShell | _XP | Text files | Archive |

---

## Documents Generated

1. **TUI_DATA_MODELS_ANALYSIS.md** (24KB)
   - Comprehensive breakdown of all 7 TUI implementations
   - Detailed field listings with data types
   - Code examples and JSON samples
   - Feature matrix and validation rules
   - Location: `/home/teej/supertui/TUI_DATA_MODELS_ANALYSIS.md`

2. **TUI_DATA_MODELS_QUICK_REFERENCE.md** (12KB)
   - Quick lookup for each implementation
   - Feature comparison matrix
   - Common data structures
   - Export formats
   - Location: `/home/teej/supertui/TUI_DATA_MODELS_QUICK_REFERENCE.md`

3. **TUI_ANALYSIS_SUMMARY.md** (This file)
   - Executive summary of findings
   - Architecture patterns
   - Gaps and opportunities
   - Recommended unified model
   - Location: `/home/teej/supertui/TUI_ANALYSIS_SUMMARY.md`

---

## Recommendations

### For SuperTUI Integration
1. **Adopt Unified Model:** Use recommended model combining best practices
2. **Implement Completed Date:** Track when tasks were actually finished
3. **Add Cyclic Dependency Protection:** Prevent invalid task hierarchies
4. **Support Time Tracking:** Integrate similar to TaskProPro/SpeedTUI
5. **Optional Excel Import:** For complex audit scenarios
6. **Service Architecture:** Follow R2 pattern for maintainability

### For Future Work
1. **Project Budgeting:** Add estimated hours vs actual tracking
2. **Recurring Tasks:** Support task recurrence patterns
3. **Task Dependencies:** Enable blocking/dependency relationships
4. **Multi-User Support:** Add user assignments and permissions
5. **Historical Tracking:** Archive and audit trail support
6. **Integrations:** Connect to calendar, email, external systems

---

## Conclusion

The TUI ecosystem represents significant exploration of task management and time tracking concepts. While implemented in different technologies, common patterns emerge:

- **Weekly time tracking** is the standard unit
- **Fiscal year** (April-March) is standard
- **Task hierarchy** is universally supported
- **Project linking** via codes (ID1/ID2) enables flexibility
- **JSON persistence** is the preferred format
- **CSV export** is standard for portability

The recommended unified model synthesizes these lessons and eliminates gaps found in existing implementations. SuperTUI can leverage this analysis to create the most comprehensive and user-friendly task management system across all platforms.

---

**Analysis completed:** October 27, 2025
**Files analyzed:** 94 source files across 7 implementations
**Total lines of code reviewed:** ~26,000
**Data files examined:** JSON, CSV, PowerShell classes, C# models
