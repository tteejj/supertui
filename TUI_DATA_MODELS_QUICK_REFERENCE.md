# TUI Data Models - Quick Reference

## At a Glance

### TaskProPro (C# Console)
**Location:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/`

**Task Model:**
- `Id` (string GUID), `Title`, `Notes`
- `Priority` enum: {Today, High, Medium, Low}
- `Completed` (bool - no full status enum)
- `DueDate`, `CreatedDate`, `ModifiedDate`
- `Tags` (List<string>)
- `Subtasks` (List<SimpleTask>) - Full hierarchy
- `ID1`/`ID2` project codes
- `ColorTheme`, `CustomColor`

**Time Tracking:**
- `SimpleTimeEntry` class
- Weekly: Monday-Friday hours + Total
- Links by `ID1`/`ID2` AND `TaskId`
- `IsLinkedToTask` boolean flag
- Fiscal year: April 1 - March 31
- Supports both task-specific and generic time codes

**Exports:** CSV, JSON

---

### SpeedTUI (PowerShell Terminal)
**Location:** `/home/teej/_tui/praxis-main/SpeedTUI/`

**Command Model (instead of tasks):**
- `Title`, `Category`, `Group`, `Description`
- `CommandText` (actual command)
- `Language` (PowerShell, Git, Docker, etc.)
- `Tags`, `UseCount`, `LastUsed`, `IsTemplate`
- `CreatedAt`, `UpdatedAt`, `Deleted` (soft delete)

**Time Tracking:**
- Same as TaskProPro but with extra fields
- `Name`, `TimeCodeID`, `IsProjectEntry`, `CreatedAt`/`UpdatedAt`
- Soft delete support

**Config:**
- Fiscal year start: April
- Week start: Monday
- Auto-calculate totals
- 15-minute rounding
- Require project code validation

---

### WPF (C# Windows GUI)
**Location:** `/home/teej/_tui/praxis-main/_wpf/`

**Task Model:**
- `Id1`/`Id2` (integers instead of guids)
- `Name` (not Title)
- `Priority` enum: {Low, Medium, High}
- `AssignedDate`, `DueDate`, `BringForwardDate`
- **NO tags, NO notes, NO status enum**
- `Children` (ObservableCollection for hierarchy)
- `IsExpanded`, `IsInEditMode` (UI state)
- **Special:** "H=today" logic (High priority auto-sets DueDate to today)

**No time tracking built-in**

---

### Alcar (PowerShell Classes)
**Location:** `/home/teej/_tui/alcar/`

**Task Model:**
- `Title`, `Description`
- `Status` enum: {Pending, InProgress, Completed, Cancelled}
- `Priority` enum: {Low, Medium, High}
- `Progress` (0-100 integer)
- `ProjectId`, `AssignedTo`
- `Tags` (ArrayList)
- `CreatedDate`, `ModifiedDate`, `DueDate`
- `ParentId`, `SubtaskIds` (hierarchy)
- `IsExpanded`, `Level` (display props)

---

### R2 (PowerShell Service Architecture)
**Location:** `/home/teej/_tui/_R2/`

**Architecture:**
- DataManager (centralized)
- NavigationService (screen stack)
- Event system (loose coupling)
- Error handling (standardized)

**Task Model:** Similar to Alcar + formal service pattern

---

## Feature Comparison Matrix

| Feature | TaskProPro | SpeedTUI | WPF | Alcar | R2 |
|---------|-----------|----------|-----|-------|-----|
| **Status enum** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **Progress %** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **Tags** | ✅ | ✅ | ❌ | ✅ | ✅ |
| **Time Tracking** | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Subtasks** | ✅ | ❌ | ✅ | ✅ | ✅ |
| **AssignedTo** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **Notes/Description** | ✅ | ❌ | ❌ | ✅ | ✅ |
| **Full Date Tracking** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **CSV Export** | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Excel Import** | ✅ | ❌ | ❌ | ✅ | ❌ |

---

## Common Data Structures Found

### Priority
- **TaskProPro:** `{Today, High, Medium, Low}`
- **WPF:** `{Low, Medium, High}`
- **Alcar/R2:** `{Low, Medium, High}`

### Status (where implemented)
- **Alcar/R2:** `{Pending, InProgress, Completed, Cancelled}`

### Time Entry Structure (TaskProPro/SpeedTUI)
```
WeekEndingFriday (yyyyMMdd format)
├── ID1 (category code: PROJ, MEET, TRAIN, etc.)
├── ID2 (project/task identifier)
├── Monday-Friday hours (decimal)
├── Total (auto-calculated)
├── FiscalYear (April 1 - March 31)
└── Linking:
    ├── TaskId (direct task link)
    └── IsLinkedToTask (boolean)
```

### Task Hierarchy
- **All implementations:** ParentId + Children collection
- **Max depth:** Checked in WPF to prevent stack overflow (max 50 levels)

---

## Validation Rules

### Time Entry Validation (TaskProPro)
- ID1 required
- ID1 max 20 chars
- Daily hours 0-24
- No duplicate ID1/ID2 in same week
- Description max 200 chars

### Task Hierarchy (WPF)
- Depth limit 50 to prevent overflow
- Timeout protection 30 seconds for validation

### Fiscal Year (All time trackers)
- April 1 - March 31 fiscal year
- Auto-calculated from week ending Friday date
- Format: "2025-2026"

---

## Export Formats

### CSV Columns
**Tasks:**
```
Title, Status, Priority, Created, Due, Tags, Notes
```

**Commands:**
```
Title, CommandText, Description, Tags, IsGroup
```

**Time Entries:**
```
WeekEndingFriday, ID1, ID2, Monday-Friday, Total, Description
```

### Excel Mapping
- **Source:** Cell reference (W23, B15, etc.)
- **Destination:** Application field
- **Type:** Date/String/Number
- **Worksheet:** Look for specific sheet name, fallback to first
- **Usage:** Audit/complex project imports

---

## Key Implementation Differences

### C# (TaskProPro, WPF)
- Type safety via enums
- GUID identifiers (strings)
- Decimal for financial data (time)
- ObservableCollection (WPF binding)
- JSON serialization

### PowerShell (SpeedTUI, Alcar, R2)
- Class-based (modern PS5+)
- ArrayList for collections
- Hashtable for data structures
- COM objects for Excel (Windows-only)
- Direct class instantiation

---

## Recommended Unified Task Model

```csharp
public class UnifiedTask {
    // Core
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TaskStatus Status { get; set; }     // Pending, InProgress, Completed, Cancelled
    public TaskPriority Priority { get; set; } // Low, Medium, High
    
    // Dates
    public DateTime CreatedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? BringForwardDate { get; set; }
    
    // Organization
    public List<string> Tags { get; set; }
    public int Progress { get; set; }    // 0-100
    public string AssignedTo { get; set; }
    
    // Hierarchy & Project Links
    public string ParentId { get; set; }
    public List<UnifiedTask> Subtasks { get; set; }
    public string ProjectCode { get; set; }     // ID1
    public string TaskCode { get; set; }        // ID2
}

public class UnifiedTimeEntry {
    // Core
    public string Id { get; set; }
    public string WeekEndingFriday { get; set; }
    public string FiscalYear { get; set; }
    
    // Time Allocation
    public string CategoryCode { get; set; }    // ID1
    public string ProjectCode { get; set; }     // ID2
    public string TaskId { get; set; }
    public string Description { get; set; }
    
    // Daily Hours (Monday-Friday)
    public decimal Monday { get; set; }
    public decimal Tuesday { get; set; }
    public decimal Wednesday { get; set; }
    public decimal Thursday { get; set; }
    public decimal Friday { get; set; }
    public decimal Total { get; set; }
    
    // Metadata
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public bool IsLinkedToTask { get; set; }
}
```

---

## File Locations Summary

| Implementation | Location | Type | Primary Model |
|---|---|---|---|
| TaskProPro | `/praxis-main/TaskProPro/CSharp/Data/` | C# | `SimpleTask`, `SimpleTimeEntry` |
| SpeedTUI | `/praxis-main/SpeedTUI/_ProjectData/` | JSON data | Time entries + Commands |
| WPF | `/praxis-main/_wpf/Models/` | C# WPF | `TaskItem` |
| Alcar | `/alcar/Models/` | PowerShell | `Task` class |
| R2 | `/_R2/` | PowerShell SOA | Service architecture |

---

**Full analysis available in:** `/home/teej/supertui/TUI_DATA_MODELS_ANALYSIS.md`
