# TUI Data Models Analysis - Comprehensive Report

## Executive Summary

This document provides a detailed analysis of all Task/Project/Time-Tracking data models found across 7 different TUI implementations in `/home/teej/_tui`. Each implementation uses different technologies (C#, PowerShell, WPF) but shares common patterns for task management and time tracking.

---

## 1. TASKPROPRO (C# Console TUI)

### Technology: C# .NET Console Application
**Location:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/`

### 1.1 TASK DATA MODEL

**Class:** `SimpleTask`
**File:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/Data/SimpleTask.cs`

#### Fields and Data Types:

```csharp
public class SimpleTask {
    // Core Properties
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool Completed { get; set; } = false;
    
    // Metadata
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public DateTime DueDate { get; set; } = DateTime.MinValue;
    
    // Organization
    public Priority Priority { get; set; } = Priority.Medium;
    public List<string> Tags { get; set; } = new List<string>();
    public string ColorTheme { get; set; } = "default";
    public string CustomColor { get; set; } = "";  // RGB hex color
    
    // Hierarchy
    public string ParentId { get; set; } = "";
    public List<SimpleTask> Subtasks { get; set; } = new List<SimpleTask>();
    public bool SubtasksCollapsed { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    
    // Project Integration
    public string ID1 { get; set; } = "";  // Project code
    public string ID2 { get; set; } = "";  // Secondary project code
}
```

#### Status Values:
**Not implemented in SimpleTask directly** - Only `Completed` boolean flag.
However, the TaskProPro UI supports:
- Pending (implicit - not completed)
- Completed (Completed = true)

#### Priority Levels:
```csharp
public enum Priority {
    Today = 0,    // Highest priority
    High = 1,     
    Medium = 2,   
    Low = 3       // Lowest priority
}
```

#### Features:
- ✅ **Subtasks/Hierarchy:** Yes - `ParentId`, `Subtasks` collection
- ✅ **Tags/Labels:** Yes - `List<string> Tags`
- ❌ **Dependencies:** No
- ✅ **Notes/Comments:** Yes - `Notes` field
- ✅ **Created/Modified dates:** Yes - `CreatedDate`, `ModifiedDate`
- ❌ **Completed date:** No
- ❌ **Estimated vs Actual time:** No
- ✅ **Color customization:** Yes - `ColorTheme`, `CustomColor`
- ✅ **Sort order:** Yes - `SortOrder` for manual ordering

---

### 1.2 PROJECT DATA MODEL

**Not Implemented** - TaskProPro uses ID1/ID2 codes instead of formal Project objects.
- **ID1:** Generic project/time code (e.g., "PROJ", "MEET", "TRAIN")
- **ID2:** Specific project identifier (e.g., task ID or project name)

---

### 1.3 TIME TRACKING DATA MODEL

**Class:** `SimpleTimeEntry`
**File:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/Data/SimpleTimeEntry.cs`

#### Fields and Data Types:

```csharp
public class SimpleTimeEntry {
    // Core Properties
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WeekEndingFriday { get; set; } = "";  // Format: yyyyMMdd
    public string ID1 { get; set; } = "";               // Generic time code allocation (MEET, TRAIN, PROJ, etc.)
    public string ID2 { get; set; } = "";               // Specific project identifier (links to tasks)
    public string Description { get; set; } = "";       // Auto-filled from task or manual entry
    
    // Daily Hours (Monday to Friday)
    public decimal Monday { get; set; } = 0m;
    public decimal Tuesday { get; set; } = 0m;
    public decimal Wednesday { get; set; } = 0m;
    public decimal Thursday { get; set; } = 0m;
    public decimal Friday { get; set; } = 0m;
    public decimal Total { get; set; } = 0m;
    
    // Metadata
    public string FiscalYear { get; set; } = "";
    public string TaskId { get; set; } = "";         // Links to specific task if selected
    public bool IsLinkedToTask { get; set; } = false; // true if linked to a task, false for manual entry
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime Modified { get; set; } = DateTime.Now;
}
```

#### Time Tracking Features:
- ✅ **Time tracked per:** Task, Project, or Generic Time Code
- ✅ **Manual time entry:** Yes - Daily hours for each weekday
- ❌ **Timer/stopwatch:** No
- ❌ **Pomodoro technique:** No
- ✅ **Time categorization:**
  - **ID1:** Generic time code category (MEET, TRAIN, PROJ)
  - **ID2:** Specific project/task identifier
  - **IsLinkedToTask:** Boolean flag for task linkage
- ✅ **Time aggregations:**
  - **Daily:** Monday-Friday breakdown
  - **Weekly:** Total field (sum of daily hours)
  - **Fiscal Year:** April 1 - March 31
  - **By Category (ID1):** Grouped totals
  - **By Project (ID1/ID2):** Grouped totals

#### Sample Data:

```json
{
  "WeekEndingFriday": "20250801",
  "ID1": "PROJ",
  "ID2": "050",
  "Description": "TaskProPro Development",
  "Monday": 8.0,
  "Tuesday": 7.5,
  "Wednesday": 8.0,
  "Thursday": 0.0,
  "Friday": 0.0,
  "Total": 23.5,
  "FiscalYear": "2025-2026",
  "IsLinkedToTask": true,
  "TaskId": "task-guid-here"
}
```

---

## 2. SPEEDTUI (PowerShell Terminal)

### Technology: PowerShell with Custom TUI Framework
**Location:** `/home/teej/_tui/praxis-main/SpeedTUI/`

### 2.1 TIME TRACKING MODEL

**Source:** `/home/teej/_tui/praxis-main/SpeedTUI/_ProjectData/timeentries.json`

```json
{
  "Id": "6c92a4cc-6799-480d-959b-e32fcc365b52",
  "WeekEndingFriday": "20250801",
  "ID1": "",
  "ID2": "",
  "Monday": 0.0,
  "Tuesday": 0.0,
  "Wednesday": 0.0,
  "Thursday": 0.0,
  "Friday": 0.0,
  "Total": 0.0,
  "FiscalYear": "2025-2026",
  "Name": "",
  "TimeCodeID": "",
  "Description": "",
  "IsProjectEntry": true,
  "CreatedAt": "2025-07-31T07:57:56.8786738-06:00",
  "UpdatedAt": "2025-07-31T07:57:57.0103784-06:00",
  "Deleted": false
}
```

#### Fields:
| Field | Type | Description |
|-------|------|-------------|
| `Id` | string (GUID) | Unique identifier |
| `WeekEndingFriday` | string (yyyyMMdd) | Week reference date |
| `ID1` | string | Generic time code |
| `ID2` | string | Project/task identifier |
| `Monday-Friday` | decimal | Daily hours |
| `Total` | decimal | Weekly total hours |
| `FiscalYear` | string | Fiscal year (e.g., "2025-2026") |
| `Name` | string | Optional name/title |
| `Description` | string | Entry description |
| `IsProjectEntry` | bool | True if linked to project |
| `CreatedAt` | DateTime | Creation timestamp |
| `UpdatedAt` | DateTime | Last modification timestamp |
| `Deleted` | bool | Soft delete flag |

#### Configuration:
**File:** `/home/teej/_tui/praxis-main/SpeedTUI/_ProjectData/config.json`

```json
"TimeTracking": {
  "ValidateTimeEntries": true,
  "AutoCalculateTotal": true,
  "RoundingMinutes": 15,
  "FiscalYearStart": "April",
  "WeekStartDay": "Monday",
  "RequireProjectCode": true,
  "ShowWeekends": false,
  "DefaultHoursPerDay": 8.0
}
```

---

## 3. PRAXIS WPF (Windows Presentation Foundation)

### Technology: C# WPF (.NET 8.0-windows)
**Location:** `/home/teej/_tui/praxis-main/_wpf/`

### 3.1 TASK DATA MODEL

**Class:** `TaskItem`
**File:** `/home/teej/_tui/praxis-main/_wpf/Models/TaskItem.cs`

```csharp
public class TaskItem : IDisplayableItem, INotifyPropertyChanged {
    public int Id1 { get; set; }
    public int Id2 { get; set; }
    
    public string Name {
        get => _name;
        set {
            if (_name != value) {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
    
    public DateTime AssignedDate { get; set; } = DateTime.Now;
    public DateTime? DueDate { get; set; }
    public DateTime? BringForwardDate { get; set; }
    
    public PriorityType Priority {
        get => _priority;
        set {
            if (_priority != value) {
                _priority = value;
                // H=today logic: If priority is High and no due date, set to today
                if (value == PriorityType.High && !DueDate.HasValue) {
                    DueDate = DateTime.Today;
                }
                OnPropertyChanged(nameof(Priority));
            }
        }
    }
    
    public bool IsExpanded { get; set; }
    public bool IsInEditMode { get; set; }
    public ObservableCollection<TaskItem> Children { get; set; }
    
    [JsonIgnore]
    public string DisplayName => Name;
    
    [JsonIgnore]
    public bool IsHighPriorityToday => 
        Priority == PriorityType.High && 
        DueDate.HasValue && 
        DueDate.Value.Date == DateTime.Today;
}
```

#### Priority Types:
```csharp
public enum PriorityType {
    Low,
    Medium,
    High
}
```

#### Sample Data:
```json
{
  "id1": 1,
  "id2": 1,
  "name": "Sample Task 1",
  "assignedDate": "2025-08-18T00:00:00",
  "dueDate": "2025-08-25T00:00:00",
  "bringForwardDate": "2025-08-20T00:00:00",
  "priority": "High",
  "isExpanded": true,
  "children": [
    {
      "id1": 1,
      "id2": 2,
      "name": "Sub-task A",
      "assignedDate": "2025-08-18T00:00:00",
      "dueDate": "2025-08-22T00:00:00",
      "priority": "Medium",
      "children": []
    }
  ]
}
```

#### Features:
- ✅ **Subtasks:** Yes - `Children` ObservableCollection
- ❌ **Tags:** No
- ❌ **Notes:** No
- ✅ **Dates:**
  - `AssignedDate` - When task was assigned
  - `DueDate` - When task is due
  - `BringForwardDate` - When to re-surface task
- ✅ **Hierarchy:** Yes - recursive Children collection
- ✅ **Special Logic:** "H=today" - High priority tasks default DueDate to today

---

## 4. ALCAR (PowerShell Classes)

### Technology: PowerShell Classes
**Location:** `/home/teej/_tui/alcar/`

### 4.1 TASK DATA MODEL

**Class:** `Task`
**File:** `/home/teej/_tui/alcar/Models/task.ps1`

```powershell
class Task {
    [string]$Id
    [string]$Title
    [string]$Description
    [string]$Status        # Pending, InProgress, Completed, Cancelled
    [string]$Priority      # Low, Medium, High
    [int]$Progress         # 0-100
    [string]$ProjectId
    [datetime]$CreatedDate
    [datetime]$ModifiedDate
    [datetime]$DueDate
    [string]$AssignedTo
    [System.Collections.ArrayList]$Tags
    [string]$ParentId      # For subtasks
    [System.Collections.ArrayList]$SubtaskIds  # Children
    [bool]$IsExpanded = $true
    [int]$Level = 0        # Nesting level for display
    
    # Constructor
    Task() {
        $this.Id = [Guid]::NewGuid().ToString()
        $this.CreatedDate = [datetime]::Now
        $this.ModifiedDate = [datetime]::Now
        $this.Status = "Pending"
        $this.Priority = "Medium"
        $this.Progress = 0
        $this.Tags = [System.Collections.ArrayList]::new()
        $this.SubtaskIds = [System.Collections.ArrayList]::new()
    }
}
```

#### Status Values:
- **Pending** - Default, not started
- **InProgress** - Currently being worked on
- **Completed** - Finished
- **Cancelled** - No longer needed

#### Priority Levels:
- Low
- Medium
- High

#### Features:
- ✅ **Subtasks:** Yes - `ParentId`, `SubtaskIds`
- ✅ **Tags:** Yes - `ArrayList` of tag strings
- ✅ **Progress tracking:** Yes - 0-100 progress integer
- ✅ **Assigned to:** Yes - `AssignedTo` user field
- ✅ **Full status tracking:** Yes - Pending/InProgress/Completed/Cancelled
- ✅ **Dates:** CreatedDate, ModifiedDate, DueDate
- ✅ **Tree view support:** Yes - `IsExpanded`, `Level` for display

---

## 5. R2 (PowerShell Class Migration - PMC Terminal v5)

### Technology: PowerShell Classes with Service-Oriented Architecture
**Location:** `/home/teej/_tui/_R2/`

### 5.1 TASK DATA MODEL

From the README and implementation, R2 uses similar patterns to Alcar but with a more formal service architecture.

#### Key Classes:
- `Task` - Core task model (similar to Alcar)
- `Project` - Project management (separate from tasks)
- `Settings` - Application configuration

#### Architecture:
- **DataManager:** Centralized data management service
- **NavigationService:** Screen navigation stack
- **ScreenFactory:** Factory pattern for creating screens
- **Event System:** Loose coupling between components
- **Error Handling:** Standardized error handling with logging

---

## 6. SPEEDTUI COMMANDS MODEL

### 6.1 COMMAND DATA MODEL

**File:** `/home/teej/_tui/praxis-main/SpeedTUI/_ProjectData/commands.json`

```json
{
  "Id": "288ecf45-3240-4c1d-b68f-140506513296",
  "Title": "Get System Information",
  "Category": "Administration",
  "Group": "System",
  "Description": "Display basic system information including OS, memory, and CPU",
  "CommandText": "Get-ComputerInfo | Select-Object WindowsProductName, TotalPhysicalMemoryMB, CsProcessors",
  "Notes": "",
  "Language": "PowerShell",
  "Tags": ["system", "info", "diagnostics"],
  "UseCount": 0,
  "LastUsed": "0001-01-01T00:00:00",
  "IsTemplate": false,
  "Parameters": {},
  "CreatedAt": "2025-07-31T06:22:16.6712252-06:00",
  "UpdatedAt": "2025-07-31T06:22:16.6772546-06:00",
  "Deleted": false
}
```

#### Fields:
| Field | Type | Purpose |
|-------|------|---------|
| `Id` | string | Unique identifier |
| `Title` | string | Command name |
| `Category` | string | Organization (Administration, Development, Utilities) |
| `Group` | string | Sub-category (System, Git, Docker, File Management) |
| `Description` | string | User-friendly description |
| `CommandText` | string | Actual command to execute |
| `Notes` | string | Additional notes |
| `Language` | string | Command language (PowerShell, Git, Docker) |
| `Tags` | string[] | Search tags |
| `UseCount` | int | Number of times executed |
| `LastUsed` | DateTime | Last execution time |
| `IsTemplate` | bool | Is this a template? |
| `Parameters` | object | Template parameters |
| `CreatedAt` | DateTime | Creation timestamp |
| `UpdatedAt` | DateTime | Last modification timestamp |
| `Deleted` | bool | Soft delete flag |

---

## 7. EXCEL INTEGRATION

### 7.1 EXCEL IMPORT/EXPORT FEATURES

**Excel Field Mapping Class:**
**File:** `/home/teej/_tui/praxis-main/simpletaskpro/Models/ExcelFieldMapping.ps1`

```powershell
class ExcelFieldMapping {
    [string]$Id
    [string]$DisplayName      # User-friendly name shown in screen
    [string]$SourceCell       # Excel cell reference (W23, B15, etc.)
    [string]$DestinationCell  # Target Excel cell (A1, A2, etc.)
    [string]$T2020Name        # Field name for T2020 text export
    [bool]$IncludeInT2020     # X mark - include in T2020 export
    [string]$Category         # Project Info, Contact, Site Info, etc.
    [int]$SortOrder           # Order for display and export
    [datetime]$CreatedDate
    [datetime]$ModifiedDate
}
```

#### Excel Import Service:
**File:** `/home/teej/_tui/praxis-main/Services/ExcelImportService.ps1`

```powershell
class ExcelImportService {
    [void] InitializeFieldMappings() {
        $this.FieldMappings = @{
            'RequestDate' = @{ Cell = 'W23'; Type = 'Date' }
            'AuditType' = @{ Cell = 'W78'; Type = 'String' }
            'AuditorName' = @{ Cell = 'W10'; Type = 'String' }
            'TPName' = @{ Cell = 'W3'; Type = 'String' }
            'Address' = @{ Cell = 'W5'; Type = 'String' }
            # ... many more field mappings
        }
    }
    
    [hashtable] ImportFromExcel([string]$FilePath) {
        # Opens Excel COM object, reads mapped cells
        # Returns hashtable with extracted data
    }
    
    [object] CreateProjectFromImport([hashtable]$ImportedData) {
        # Maps Excel data to Project object
    }
}
```

#### Excel Mapping Examples:
- **Source:** Excel workbook cells (e.g., 'W23' for RequestDate)
- **Destination:** Application fields (e.g., Project.RequestDate)
- **Types:** Date, String, Number
- **Worksheet:** Looks for 'SVI-CAS' sheet, falls back to first sheet

---

### 7.2 CSV EXPORT FORMAT

**Tasks CSV Export:**
```csv
Title,Status,Priority,Created,Due,Tags,Notes
"a1",Pending,Medium,2025-08-06,,"",""
"Complete quarterly report",Pending,Today,2025-08-02,2025-08-05,"today",""
"Revenue analysis",Completed,Medium,2025-08-02,,"",""
```

**Commands CSV Export:**
```csv
Title,CommandText,Description,Tags,IsGroup
"Git","","Git version control commands","",True
"Git Log","git log --oneline -10","Show recent commits","git;log;history",False
"Git Status","git status","Show working tree status","git;status",False
```

**Time Entries CSV Export:**
```csv
WeekEndingFriday,ID1,ID2,Monday,Tuesday,Wednesday,Thursday,Friday,Total,Description
20250801,PROJ,050,1.0,2.0,0.0,0.0,0.0,3.0,"Project work"
```

#### Export Format Details:
- **Delimiter:** Comma (CSV)
- **Quote Character:** Double quotes for fields with special characters
- **Date Format:** ISO 8601 (YYYY-MM-DD)
- **Decimal:** Period separator for time entries
- **Tags:** Semicolon-separated within field
- **Empty values:** Represented as empty quoted strings or blank

---

## 8. FILTER CRITERIA

**File:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/Data/FilterCriteria.cs`

```csharp
public class FilterCriteria {
    public Priority Priority { get; set; } = Priority.Medium;
    public string TagFilter { get; set; } = "";
    public string SearchText { get; set; } = "";
    public bool ShowOnlyToday { get; set; } = false;
    public bool ShowCompleted { get; set; } = true;
}
```

#### Filtering Capabilities:
- ✅ Filter by Priority
- ✅ Filter by Tag (single tag)
- ✅ Search by Title and Notes
- ✅ Show only "Today" (Priority=Today OR DueDate=Today)
- ✅ Show/Hide Completed tasks

---

## 9. COMPARATIVE TABLE - ALL TUI IMPLEMENTATIONS

| Feature | TaskProPro | SpeedTUI | WPF | Alcar | R2 |
|---------|-----------|----------|-----|-------|-----|
| **Task Title** | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Description/Notes** | ✅ | ❌ | ❌ | ✅ | ✅ |
| **Status (Enum)** | ❌ Bool | ❌ | ❌ | ✅ Enum | ✅ Enum |
| **Priority (Enum)** | ✅ 4 levels | ❌ | ✅ 3 levels | ✅ 3 levels | ✅ 3 levels |
| **Subtasks** | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Tags** | ✅ | ✅ | ❌ | ✅ | ✅ |
| **Progress %** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **Assigned To** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **Created Date** | ✅ | ✅ | ❌ | ✅ | ✅ |
| **Modified Date** | ✅ | ✅ | ❌ | ✅ | ✅ |
| **Due Date** | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Bring Forward Date** | ❌ | ❌ | ✅ | ❌ | ❌ |
| **Completed Date** | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Time Tracking** | ✅ Weekly | ✅ Weekly | ❌ | ❌ | ❌ |
| **Time Linking** | ✅ Task Link | ✅ Project Link | ❌ | ❌ | ❌ |
| **Color Customization** | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Excel Import/Export** | ✅ | ❌ | ❌ | ✅ | ❌ |
| **CSV Export** | ✅ | ✅ | ❌ | ❌ | ❌ |

---

## 10. DATA PERSISTENCE

### Storage Formats:
1. **JSON** - TaskProPro, SpeedTUI, WPF
   - Human readable
   - Easy to parse
   - Version control friendly
   
2. **CSV** - Export only (all TUIs that support export)
   - Excel compatible
   - Easy to share
   
3. **Excel/COM** - Custom mapping system
   - Complex field mapping
   - Cell-level references
   - Type conversion (Date, String, Number)

### Backup Strategies:
- **SpeedTUI:** Automatic backups with timestamp (config: `AutoBackup: true`, `BackupInterval: 24` hours)
- **TaskProPro:** Backup on save with retention policy
- **WPF:** Configurable data file path with optional logging

---

## 11. SYNCHRONIZATION BETWEEN TASK AND TIME TRACKING

### TaskProPro Model:
```
SimpleTask
├── ID1 (Generic code)
└── ID2 (Project/task code)

SimpleTimeEntry
├── ID1 (Generic code) ← matches Task.ID1
├── ID2 (Project/task code) ← matches Task.ID2
├── TaskId (Direct link to Task.Id)
└── IsLinkedToTask (Boolean connection)
```

### Linking Method:
1. **Direct:** By `TaskId` field
2. **Indirect:** By ID1/ID2 combination
3. **Type:** Both task-specific and generic time codes

### Time Allocation Codes:
- **Generic Codes (ID1 only):**
  - MEET - Meetings
  - TRAIN - Training
  - ADMIN - Administration
  - Etc.

- **Project Codes (ID1/ID2 combination):**
  - PROJ/taskid - Task-specific time
  - PROJ/project-name - Project-general time

---

## 12. VALIDATION RULES FOUND

### TaskProPro Time Entry Validation:
```csharp
public bool ValidateTimeEntry(SimpleTimeEntry entry, out List<string> errors) {
    // ID1 required
    // ID1 max 20 chars
    // ID2 max 20 chars
    // Description max 200 chars
    // Daily hours 0-24
    // No duplicate ID1/ID2 in same week
}
```

### WPF Task Validation:
- Task hierarchy depth check (prevent stack overflow)
- High priority auto-sets due date to today
- Children validation before adding
- Timeout protection (30 second limit)

### TimeEntry Fiscal Year Rules:
- April 1 - March 31 fiscal year
- Automatically calculated from week date
- Stored as "2025-2026" format

---

## 13. RECOMMENDED UNIFIED DATA MODEL

Based on analysis of all 7 TUI implementations, here's a recommended unified model:

```csharp
public class UnifiedTask {
    // Core
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TaskStatus Status { get; set; }  // Pending, InProgress, Completed, Cancelled
    public TaskPriority Priority { get; set; }  // Low, Medium, High, Today
    
    // Dates
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? BringForwardDate { get; set; }
    
    // Organization
    public List<string> Tags { get; set; }
    public int Progress { get; set; }  // 0-100
    public string AssignedTo { get; set; }
    
    // Hierarchy
    public string ParentId { get; set; }
    public List<UnifiedTask> Subtasks { get; set; }
    
    // Time Tracking Links
    public string ProjectCode { get; set; }  // ID1
    public string TaskCode { get; set; }     // ID2
}

public class UnifiedTimeEntry {
    // Core
    public string Id { get; set; }
    public string WeekEndingFriday { get; set; }
    public string FiscalYear { get; set; }
    
    // Time Allocation
    public string CategoryCode { get; set; }  // ID1
    public string ProjectCode { get; set; }   // ID2
    public string TaskId { get; set; }
    public string Description { get; set; }
    
    // Daily Hours
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

## Summary

All 7 TUI implementations converge on similar patterns:

1. **Task Management:** Title, dates, priority, status, hierarchy
2. **Organization:** Tags, categories, project codes
3. **Time Tracking:** Weekly hours by day, category/project codes, task linking
4. **Persistence:** JSON primary, CSV exports, Excel import/export optional
5. **Validation:** Data integrity checks, no duplicate entries, range validation
6. **UI Models:** Support for expanded/collapsed state, progress visualization, color coding

The main differentiators are:
- **PowerShell versions:** More focus on scripting and command automation
- **C# versions:** More robust type safety and OOP architecture
- **WPF version:** GUI-focused with binding support
- **Excel integration:** Only in audit/complex project variants

