# Inline Editing Implementation Status

## Overview
Implementing inline field editing (from TodayViewScreen pattern) across all task list screens.

## Pattern Components
Each screen needs:
1. **Properties**: InputBuffer, InputMode, EditField, EditableFields, EditFieldIndex
2. **Methods**: _HandleInputMode(), _StartEditField(), _SubmitInput(), _CancelInput(), _CycleField(), _UpdateField()
3. **HandleInput Update**: Check InputMode first, call E key handler
4. **Rendering Update**: Show edit indicators and inline input UI

## Status

### COMPLETED
- ✅ **TodayViewScreen** - Already had full inline editing (reference implementation)
- ✅ **TomorrowViewScreen** - Already had full inline editing
- ✅ **WeekViewScreen** - JUST COMPLETED
  - Added all inline edit properties
  - Added all 6 helper methods
  - Updated HandleInput to check InputMode
  - Updated rendering to show edit UI with "E" cursor
  - Array: WeekTasks

### IN PROGRESS
- 🔄 **OverdueViewScreen** - NEEDS COMPLETION
  - Array: OverdueTasks
  - Similar structure to WeekViewScreen
  - Has due date column that needs inline edit support

### PENDING - Need Same Pattern Applied
- ⏳ **UpcomingViewScreen**
  - Array: UpcomingTasks
  - Has priority, due date, task text columns

- ⏳ **NextActionsViewScreen**
  - Array: NextActions
  - Has priority, due date, task text columns

- ⏳ **NoDueDateViewScreen**
  - Array: NoDueDateTasks
  - Has priority, ID, task text columns (no due date to edit)
  - Edit fields: priority, text (NOT due since these have no due date)

- ⏳ **BlockedTasksScreen**
  - Array: BlockedTasks
  - Simpler rendering - just task list
  - Edit fields: priority, text (status via toggle, not inline edit)

### SPECIAL CASES - SelectableTasks Array
- ⏳ **MonthViewScreen**
  - Array: SelectableTasks (combines OverdueTasks, ThisMonthTasks, NoDueDateTasks)
  - Multi-section rendering
  - Edit applies to selected task in SelectableTasks

- ⏳ **AgendaViewScreen**
  - Array: SelectableTasks (only shown tasks: first 5 overdue + 5 today + 3 tomorrow + 3 week)
  - Multi-section rendering
  - Edit applies to selected task in SelectableTasks

### OTHER ISSUES
- ⏳ **KanbanScreen** - Menu stubs need fixing (lines 57-81)
  - Replace `Write-Host "..."` with proper New-Object patterns like BlockedTasksScreen uses
  - Example: `{ Write-Host "Task List not implemented" }` →
    ```powershell
    {
        . "$PSScriptRoot/TaskListScreen.ps1"
        $global:PmcApp.PushScreen([TaskListScreen]::new())
    }
    ```

## Implementation Template

### 1. Add Properties (after existing properties)
```powershell
[string]$InputBuffer = ""
[string]$InputMode = ""  # "", "edit-field"
[string]$EditField = ""  # Which field being edited (due, priority, text)
[array]$EditableFields = @('priority', 'due', 'text')  # Adjust based on screen
[int]$EditFieldIndex = 0
```

### 2. Update Footer Shortcuts (in constructor)
```powershell
$this.Footer.AddShortcut("E", "Edit")
$this.Footer.AddShortcut("Tab", "Next Field")
$this.Footer.AddShortcut("Esc", "Cancel")
```

### 3. Update HandleInput (add at top of method)
```powershell
[bool] HandleInput([ConsoleKeyInfo]$keyInfo) {
    # Input mode handling
    if ($this.InputMode) {
        return $this._HandleInputMode($keyInfo)
    }

    # Normal mode handling...
    # Change 'Enter' case to 'E' case calling $this._StartEditField()
}
```

### 4. Update Rendering (in _RenderTaskList)
Add `$isEditing` check:
```powershell
$isEditing = ($i -eq $this.SelectedIndex) -and ($this.InputMode -eq 'edit-field')
```

Update cursor to show "E" when editing:
```powershell
$cursorChar = if ($isEditing) { "E" } else { ">" }
```

For each editable column, add inline edit check:
```powershell
if ($isEditing -and $this.EditField -eq 'priority') {
    # Show input buffer with cursor
    $sb.Append($cursorColor)
    $sb.Append(($this.InputBuffer + "_").PadRight($priorityWidth))
    $sb.Append($reset)
} elseif ($task.priority -gt 0) {
    # Normal display
    ...
}
```

### 5. Add Helper Methods
Copy ALL 6 methods from TodayViewScreen/WeekViewScreen:
- _HandleInputMode()
- _StartEditField()
- _SubmitInput()
- _CancelInput()
- _CycleField()
- _UpdateField()

**IMPORTANT**: Replace all array references:
- `$this.TodayTasks` → `$this.YOURARRAY`  (e.g., OverdueTasks, NextActions, SelectableTasks)

## Testing
After each screen update:
```bash
pwsh -c ". ./screens/ScreenName.ps1"
```
This will catch syntax errors (ignore PmcScreen type errors - those are expected).

## Files Modified
1. /home/teej/pmc/module/Pmc.Strict/consoleui/screens/WeekViewScreen.ps1 ✅

## Files Still Need Updates
2. /home/teej/pmc/module/Pmc.Strict/consoleui/screens/OverdueViewScreen.ps1
3. /home/teej/pmc/module/Pmc.Strict/consoleui/screens/UpcomingViewScreen.ps1
4. /home/teej/pmc/module/Pmc.Strict/consoleui/screens/NextActionsViewScreen.ps1
5. /home/teej/pmc/module/Pmc.Strict/consoleui/screens/NoDueDateViewScreen.ps1
6. /home/teej/pmc/module/Pmc.Strict/consoleui/screens/BlockedTasksScreen.ps1
7. /home/teej/pmc/module/Pmc.Strict/consoleui/screens/MonthViewScreen.ps1
8. /home/teej/pmc/module/Pmc.Strict/consoleui/screens/AgendaViewScreen.ps1
9. /home/teej/pmc/module/Pmc.Strict/consoleui/screens/KanbanScreen.ps1 (menu fix only)

## Next Steps
1. Apply pattern to remaining 7 screens (2-8 above)
2. Fix KanbanScreen menus (screen 9)
3. Test each screen loads without parse errors
4. Test in actual TUI to verify inline editing works

## Key Differences By Screen

### NoDueDateViewScreen
- EditableFields: @('priority', 'text') - NO 'due' field since these tasks have no due date
- No due date column to render inline edit for

### BlockedTasksScreen
- EditableFields: @('priority', 'text') - status is toggled via 'D' key, not inline edited
- Simpler rendering with less columns

### MonthViewScreen & AgendaViewScreen
- Use SelectableTasks array (not individual task arrays)
- Multi-section rendering with different task buckets
- Edit functionality works on currently selected task from SelectableTasks

### KanbanScreen
- Only needs menu stub fixes, NOT inline editing (has different navigation model)
