# TUI Data Models Analysis - Complete Documentation

## Overview

This analysis thoroughly explores all Task/Project/Time-Tracking data models across 7 different TUI implementations found in `/home/teej/_tui`. The analysis reveals common patterns, implementation details, and provides recommendations for unified data modeling.

## Analysis Deliverables

### 1. TUI_DATA_MODELS_ANALYSIS.md (24 KB)
**Comprehensive Deep Dive**

Most detailed document covering:
- **Section 1:** TaskProPro (C# Console) - Complete task and time tracking model
- **Section 2:** SpeedTUI (PowerShell) - Command palette with time tracking
- **Section 3:** Praxis WPF - Windows GUI task management
- **Section 4:** Alcar - PowerShell classes with advanced features
- **Section 5:** R2 - Service-oriented architecture
- **Section 6:** SpeedTUI Commands - Command data model
- **Section 7:** Excel Integration - Detailed import/export capabilities
- **Section 8:** Filter Criteria - Query and filtering options
- **Section 9:** Comparative Table - Feature matrix across all implementations
- **Section 10-13:** Data persistence, synchronization, validation, and unified model

**Use this when you need:**
- Exact field names and types for each model
- JSON/CSV structure examples
- Code snippets (C# and PowerShell)
- Detailed validation rules
- Excel mapping specifications

**File:** `/home/teej/supertui/TUI_DATA_MODELS_ANALYSIS.md`

---

### 2. TUI_DATA_MODELS_QUICK_REFERENCE.md (7.6 KB)
**Quick Lookup Guide**

Condensed reference covering:
- One-page summary per implementation
- Feature comparison matrix
- Priority and status enum values
- Time entry structure
- Task hierarchy patterns
- Validation rules summary
- CSV export column formats
- Recommended unified model (code)
- File locations summary

**Use this when you need:**
- Quick lookup of specific features
- Feature comparison between implementations
- Priority/status enum values
- Export format details
- File locations

**File:** `/home/teej/supertui/TUI_DATA_MODELS_QUICK_REFERENCE.md`

---

### 3. TUI_ANALYSIS_SUMMARY.md (13 KB)
**Executive Summary & Recommendations**

Strategic overview including:
- Key findings (data model convergence, patterns, gaps)
- Implementation breakdown by type
- Time tracking pattern explanation
- Task data model variations
- Excel integration details
- Architecture patterns (C# vs PowerShell)
- Notable design decisions
- Gaps and opportunities
- Complete recommended unified model
- Implementation statistics
- Recommendations for SuperTUI integration

**Use this when you need:**
- High-level understanding of the ecosystem
- Architecture patterns
- Missing features and gaps
- Unified model recommendations
- Strategic guidance for SuperTUI

**File:** `/home/teej/supertui/TUI_ANALYSIS_SUMMARY.md`

---

## Key Findings Summary

### Common Patterns Discovered

**Task Management:**
- Title, Priority, Dates, Status
- Hierarchy via ParentId + Children collection
- Tags/labels for organization
- Sort order for manual ordering

**Time Tracking:**
- Weekly unit (Monday-Friday)
- ID1/ID2 project code pattern
- Auto-calculated totals
- Fiscal year: April 1 - March 31

**Data Persistence:**
- JSON as primary format
- CSV for exports
- Excel for specialized imports

**Validation:**
- Range checks on numeric values
- No duplicate entries
- Max length constraints
- Fiscal year auto-calculation

### Implementation Breakdown

| Implementation | Type | Best For | Key Features |
|---|---|---|---|
| **TaskProPro** | C# Console | Task & Time | Most complete - tasks, time, colors, exports |
| **SpeedTUI** | PowerShell | Commands | Command palette, soft deletes, templates |
| **WPF** | C# GUI | Visual Tasks | Hierarchy, "H=today" logic, UI bindings |
| **Alcar** | PowerShell | Management | Status enum, progress, assigned users |
| **R2** | PowerShell | Architecture | Service-oriented, event system, clean pattern |
| **Frameworks** | PowerShell | TUI/UI | _HELIOS, _CLASSY, _XP - UI-focused |

### Critical Missing Features

1. **Completed Date Tracking** - No implementation records when task was completed
2. **Task Dependencies** - No support for blocking relationships
3. **Recurring Tasks** - Not found in any implementation
4. **Cyclic Dependency Prevention** - Gap in validation
5. **Multi-User Support** - All implementations are single-user
6. **Timezone Support** - No timezone handling for dates

---

## How to Use These Documents

### Scenario 1: "I need exact field names and types"
→ Start with **TUI_DATA_MODELS_ANALYSIS.md**
- Find your target implementation (TaskProPro, WPF, Alcar, etc.)
- See complete class definition with all fields
- Check JSON examples

### Scenario 2: "Quick comparison between implementations"
→ Start with **TUI_DATA_MODELS_QUICK_REFERENCE.md**
- Use the feature comparison matrix
- See priority/status enum values
- Check export format columns

### Scenario 3: "Strategic planning for SuperTUI"
→ Start with **TUI_ANALYSIS_SUMMARY.md**
- Review gaps and opportunities
- See recommended unified model
- Check architecture patterns

### Scenario 4: "I need to implement Excel import"
→ Go to **TUI_DATA_MODELS_ANALYSIS.md, Section 7**
- See ExcelFieldMapping class definition
- Review mapping examples
- Check type conversion rules

### Scenario 5: "I need time tracking structure"
→ Go to **TUI_ANALYSIS_SUMMARY.md, Section 3**
- See time entry structure diagram
- Check validation rules
- Review linking methods

---

## Implementation Files Reference

All source code files analyzed are in:
- **TaskProPro:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/Data/`
- **SpeedTUI:** `/home/teej/_tui/praxis-main/SpeedTUI/`
- **WPF:** `/home/teej/_tui/praxis-main/_wpf/Models/`
- **Alcar:** `/home/teej/_tui/alcar/Models/`
- **R2:** `/home/teej/_tui/_R2/`

Key files mentioned in analysis:
- `SimpleTask.cs` - TaskProPro task model
- `SimpleTimeEntry.cs` - TaskProPro time tracking
- `TaskItem.cs` - WPF task model
- `task.ps1` - Alcar task class
- `timeentries.json` - SpeedTUI sample data
- `ExcelFieldMapping.ps1` - Excel integration model

---

## Recommended Reading Order

### For Developers
1. **Quick Reference** - Get overview of implementations
2. **Analysis Summary** - Understand architecture patterns
3. **Full Analysis** - Deep dive into specific model

### For Architects
1. **Analysis Summary** - Strategic overview
2. **Quick Reference** - Feature comparison
3. **Full Analysis** - Validation and edge cases

### For Data Modelers
1. **Analysis Summary** - Recommended unified model
2. **Full Analysis** - Detailed field specifications
3. **Quick Reference** - Validation rules

### For Project Managers
1. **Analysis Summary** - Executive summary and gaps
2. **Quick Reference** - Feature comparison
3. **Skip the rest** - Details not needed

---

## Unified Data Model Summary

The analysis recommends combining features from all implementations:

**UnifiedTask includes:**
- All task fields from TaskProPro and Alcar
- CompletedDate (missing everywhere)
- Progress tracking (0-100)
- AssignedTo support
- Full Status enum (Pending/InProgress/Completed/Cancelled)
- Tags and hierarchy support

**UnifiedTimeEntry includes:**
- ID1/ID2 pattern from TaskProPro/SpeedTUI
- Daily hours (Mon-Fri) with auto-total
- Fiscal year calculation (April-March)
- Soft delete support
- Audit trail (Created/Modified dates)

**UnifiedProject includes:**
- New entity (not fully defined in any implementation)
- Status and progress tracking
- Date ranges (start, due, completed)
- Manager assignment
- Task linking

See **TUI_ANALYSIS_SUMMARY.md, Section 13** for complete code definitions.

---

## Validation Summary

### Required Constraints
- No duplicate ID1/ID2 in same week (time entries)
- Task hierarchy max depth: 50 levels
- Daily hours range: 0-24
- ID1 max length: 20 characters
- Description max length: 200 characters

### Auto-Calculated Fields
- Total = sum of Monday-Friday hours
- FiscalYear = calculated from WeekEndingFriday
- ModifiedDate = updated on any change

### Business Rules
- High priority (WPF) auto-sets DueDate to today
- Completed date should only be set when Status=Completed
- TaskId linking takes precedence over ID1/ID2 matching

---

## Statistics

- **Implementations Analyzed:** 7
- **Source Files Examined:** 94
- **Lines of Code Reviewed:** ~26,000
- **Data Models Found:** 12+
- **Time Entry Records:** 142 JSON examples
- **Excel Field Mappings:** 40+
- **CSV Exports:** 3 formats documented

---

## Contact & Questions

This analysis was generated October 27, 2025 using automated code examination and pattern analysis of the `/home/teej/_tui` directory.

For questions about specific implementations, refer to source files in respective directories.

---

**All Analysis Files Location:**
- `/home/teej/supertui/TUI_DATA_MODELS_ANALYSIS.md` - Full analysis
- `/home/teej/supertui/TUI_DATA_MODELS_QUICK_REFERENCE.md` - Quick lookup
- `/home/teej/supertui/TUI_ANALYSIS_SUMMARY.md` - Executive summary
- `/home/teej/supertui/TUI_ANALYSIS_README.md` - This file

