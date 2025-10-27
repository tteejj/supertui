# SuperTUI Windows Testing Checklist

**Created:** 2025-10-27
**Status:** Ready for Windows Testing
**Build:** 0 Errors, 4 Warnings (3.67s)

---

## Overview

This checklist covers comprehensive testing of SuperTUI on Windows after the Excel integration work. All widgets were developed and compiled successfully on Linux, but require Windows for runtime testing.

---

## Pre-Testing Setup

### Environment Requirements
- [ ] Windows 10/11 with .NET 8.0 SDK installed
- [ ] PowerShell 7+ available
- [ ] Git for Windows (if pulling from repository)
- [ ] Excel 2016+ (for testing Excel integration)
- [ ] Display resolution: 1920x1080 or higher recommended

### Build Verification
```powershell
cd /path/to/supertui/WPF
dotnet build SuperTUI.csproj
```
- [ ] Build succeeds (0 errors expected, 4 warnings acceptable)
- [ ] Output: `SuperTUI.dll` in `bin/Debug/net8.0-windows/`

### Launch Application
```powershell
pwsh SuperTUI.ps1
```
- [ ] Application launches without crashes
- [ ] Main window displays with 6 workspaces
- [ ] No console errors during startup

---

## Phase 1: Core Infrastructure Testing

### 1.1 Application Startup
- [ ] Application starts within 5 seconds
- [ ] All 6 workspaces load successfully
- [ ] No error dialogs appear
- [ ] Console shows "Workspaces created!" message
- [ ] State restoration runs (if previous state exists)

### 1.2 Workspace Navigation
- [ ] Press `Ctrl+1` → Switches to Workspace 1 (Dashboard)
- [ ] Press `Ctrl+2` → Switches to Workspace 2 (Coding)
- [ ] Press `Ctrl+3` → Switches to Workspace 3 (Tasks)
- [ ] Press `Ctrl+4` → Switches to Workspace 4 (Communication)
- [ ] Press `Ctrl+5` → Switches to Workspace 5 (Analytics)
- [ ] Press `Ctrl+6` → Switches to Workspace 6 (Excel) **[NEW]**
- [ ] Tab indicators update correctly
- [ ] Workspace content renders properly

### 1.3 Focus Management
- [ ] Click a widget → Widget gains focus (3px colored border)
- [ ] Press `Tab` → Focus moves to next widget
- [ ] Press `Shift+Tab` → Focus moves to previous widget
- [ ] Focused widget responds to keyboard input
- [ ] Focus border uses theme Primary color

### 1.4 Theme System
- [ ] Press `Ctrl+T` → Opens theme selector (if implemented)
- [ ] Change theme → All widgets update colors immediately
- [ ] Themes available: Dark, Light, Amber Terminal, Matrix, Synthwave
- [ ] No visual artifacts after theme change

---

## Phase 2: Excel Integration Testing (Workspace 6)

### 2.1 ExcelImportWidget (Top-Left)

#### Profile Management
- [ ] Widget displays profile dropdown
- [ ] Default profile "SVI-CAS Standard (48 Fields)" exists
- [ ] Profile dropdown shows profile name
- [ ] Selecting different profile updates field count

#### Data Import - Basic
1. Open Excel with sample data
2. Select cells (e.g., A1:C3)
3. Copy (Ctrl+C)
4. Switch to SuperTUI Workspace 6
5. Focus ExcelImportWidget
- [ ] Click clipboard text box
- [ ] Paste (Ctrl+V) → Data appears in text box
- [ ] Preview section shows parsed fields
- [ ] Import button becomes enabled
- [ ] Status text shows "✓ Data parsed successfully" (green)

#### Data Import - Execute
- [ ] Click "Import" button → Success message
- [ ] Status shows "✓ Imported: [Project Name]"
- [ ] Text box clears after import
- [ ] Preview clears after import
- [ ] Import button disables after import

#### Error Handling
- [ ] Paste invalid data → Import button stays disabled
- [ ] Status shows "✗ Error: [message]" (red)
- [ ] Clear button → Clears text box and preview

#### Keyboard Shortcuts
- [ ] Focus widget, press `Ctrl+V` → Pastes from clipboard
- [ ] With valid data, press `Ctrl+I` → Imports project
- [ ] Press `Escape` → Clears text box and preview

### 2.2 ExcelExportWidget (Top-Right)

#### Profile and Format Selection
- [ ] Profile dropdown displays available profiles
- [ ] Format dropdown shows: CSV, TSV (Excel Paste), JSON, XML
- [ ] Default format is "TSV (Excel Paste)"
- [ ] Field count displays "Export fields: [N]"

#### Project Selection
- [ ] Project list loads all projects from ProjectService
- [ ] Projects display as: "Name [Status]"
- [ ] Multi-select enabled (Ctrl+Click, Shift+Click)
- [ ] "All" button → Selects all projects
- [ ] "Clear" button → Deselects all projects
- [ ] "↻" (refresh) button → Reloads project list

#### Export Preview
- [ ] Select 1+ projects → Preview updates automatically
- [ ] Preview shows first 50 lines
- [ ] Preview shows "... (N more lines)" if > 50 lines
- [ ] Export buttons become enabled
- [ ] Status shows "Ready to export N project(s) as [format]" (green)

#### Export to Clipboard
- [ ] Click "Copy to Clipboard" → Success message
- [ ] Status shows "✓ Copied N project(s) to clipboard" (green)
- [ ] Open Excel, paste → Data appears in correct format
- [ ] TSV format: Tab-separated, ready for Excel
- [ ] CSV format: Comma-separated with proper escaping
- [ ] JSON format: Valid JSON array
- [ ] XML format: Valid XML structure

#### Export to File
- [ ] Click "Save to File" → Save dialog opens
- [ ] Default filename: `projects_export_YYYYMMDD_HHMMSS.[ext]`
- [ ] Select location, save → Success message
- [ ] Status shows "✓ Exported N project(s) to [filename]" (green)
- [ ] File exists at specified location
- [ ] File content matches preview

#### Keyboard Shortcuts
- [ ] Focus widget, press `Ctrl+A` → Selects all projects
- [ ] With selection, press `Ctrl+E` → Exports to clipboard
- [ ] With selection, press `Ctrl+S` → Opens save dialog
- [ ] Press `Escape` → Clears selection

#### Error Handling
- [ ] Select 0 projects → Export buttons disabled
- [ ] Status shows "Select at least one project"
- [ ] Export with 0 export fields → Error message

### 2.3 ExcelMappingEditorWidget (Bottom, Full Width)

#### Profile Management
- [ ] Profile dropdown displays all profiles
- [ ] Select profile → Loads profile details
- [ ] Profile name editable in text box
- [ ] Profile description editable (multiline)
- [ ] Profile name/description change → "Save Profile" button enables

#### Profile CRUD Operations
**Create Profile:**
- [ ] Click "+" (new profile) button
- [ ] New profile created with timestamp name
- [ ] Profile appears in dropdown
- [ ] Status shows "Created new profile: [name]" (green)

**Edit Profile:**
- [ ] Change profile name
- [ ] Change profile description
- [ ] Click "Save Profile" → Success message
- [ ] Profile updates in dropdown
- [ ] Status shows "Profile saved" (green)

**Delete Profile:**
- [ ] Click "✕" (delete profile) button
- [ ] Confirmation dialog appears
- [ ] Click "Yes" → Profile deleted
- [ ] Profile removed from dropdown
- [ ] Status shows "Profile deleted" (red)
- [ ] Attempting to delete last profile → Error message

#### Mapping List
- [ ] Mapping list displays all field mappings
- [ ] Format: "Display Name (Cell → Property) [Export]"
- [ ] Mappings ordered by SortOrder
- [ ] Select mapping → Edit/Delete buttons enable
- [ ] Select first mapping → Move Up disabled
- [ ] Select last mapping → Move Down disabled

#### Mapping CRUD Operations
**Create Mapping:**
- [ ] Click "+" (add mapping) button
- [ ] Editor panel enables
- [ ] All fields clear/reset
- [ ] Status shows "Creating new mapping..." (blue)
- [ ] Fill required fields: Display Name, Cell Ref, Property Name
- [ ] Check "Required" checkbox
- [ ] Check "Include in Export" checkbox
- [ ] Click "Save" → Mapping added to list
- [ ] Status shows "Saved: [Display Name]" (green)

**Edit Mapping:**
- [ ] Select mapping from list
- [ ] Click "✎" (edit) button
- [ ] Editor panel enables
- [ ] Fields populate with mapping data
- [ ] Status shows "Editing: [Display Name]" (blue)
- [ ] Modify fields
- [ ] Click "Save" → Mapping updated
- [ ] List refreshes with changes

**Delete Mapping:**
- [ ] Select mapping from list
- [ ] Click "✕" (delete) button
- [ ] Confirmation dialog appears
- [ ] Click "Yes" → Mapping removed
- [ ] List refreshes
- [ ] Status shows "Deleted: [Display Name]" (red)

**Reorder Mappings:**
- [ ] Select mapping (not first)
- [ ] Click "↑" (move up) → Mapping moves up in list
- [ ] Select mapping (not last)
- [ ] Click "↓" (move down) → Mapping moves down in list
- [ ] Order persists after save

#### Field Validation
- [ ] Try to save with empty Display Name → Error dialog
- [ ] Try to save with empty Cell Ref → Error dialog
- [ ] Try to save with empty Property Name → Error dialog
- [ ] Try to save with invalid cell ref (e.g., "XYZ") → Accepts but warns
- [ ] Default values: Category="General", DataType="String"

#### Keyboard Shortcuts
- [ ] Focus widget, press `Ctrl+N` → Creates new mapping
- [ ] With profile changes, press `Ctrl+S` → Saves profile
- [ ] With editor open, press `Ctrl+S` → Saves mapping
- [ ] With editor open, press `Escape` → Cancels and closes editor

#### Integration Testing
1. Create new profile "Test Profile"
2. Add mapping: "Test Field" (W1 → TestProperty)
3. Mark "Include in Export"
4. Save profile
5. Switch to ExcelExportWidget
6. Select "Test Profile" → Field count updates
7. Export 1 project → "Test Field" appears in output

---

## Phase 3: Existing Widgets Testing

### 3.1 Workspace 1: Dashboard
- [ ] ClockWidget displays current time
- [ ] Clock updates every second
- [ ] Time format: HH:mm:ss or HH:mm
- [ ] Date displays below time

### 3.2 Workspace 2: Coding (Focus Layout)
- [ ] FileExplorerWidget displays directory tree
- [ ] Click folder → Expands/collapses
- [ ] Click file → Opens in editor (if implemented)
- [ ] Keyboard navigation: Arrow keys, Enter
- [ ] Security warning for dangerous files (.exe, .bat, etc.)

### 3.3 Workspace 3: Tasks (Grid 2x2)
- [ ] TaskManagementWidget displays task list
- [ ] Tasks grouped by status/priority
- [ ] Press `F2` on selected task → Edit mode
- [ ] Press `Delete` on selected task → Confirmation → Deletes
- [ ] Press `Ctrl+N` → Creates new task
- [ ] TaskSummaryWidget shows stats (total, completed, pending, overdue)
- [ ] Stats update when tasks change

### 3.4 Workspace 4: Communication
- [ ] CommandPaletteWidget displays command search
- [ ] Type command name → Filters list
- [ ] Select command, press Enter → Executes

### 3.5 Workspace 5: Analytics
- [ ] ProjectStatsWidget displays metrics
- [ ] Charts render correctly (if charts implemented)
- [ ] Recent activity shows project changes

---

## Phase 4: State Persistence Testing

### 4.1 State Save
1. Add several widgets to workspaces
2. Move/resize widgets
3. Switch between workspaces
4. Close application
- [ ] Application exits cleanly
- [ ] No crash or error dialogs
- [ ] State files created in `%LOCALAPPDATA%\SuperTUI\state\`
- [ ] Files: `workspace_1.json` through `workspace_6.json`

### 4.2 State Restore
1. Relaunch application
- [ ] Workspaces restore widget presence
- [ ] Widget positions approximately correct (Level 1-2 restoration)
- [ ] All widgets initialize successfully
- [ ] No errors if state file missing/corrupted

### 4.3 State Corruption Handling
1. Manually corrupt a state file (invalid JSON)
2. Relaunch application
- [ ] Application continues to launch
- [ ] Error logged but not fatal
- [ ] Corrupted workspace loads default/empty

---

## Phase 5: Performance & Stability

### 5.1 Startup Performance
- [ ] Application starts in < 5 seconds
- [ ] No long hangs during initialization
- [ ] Progress messages visible in console

### 5.2 Memory Usage
1. Launch application
2. Let run for 5 minutes
3. Switch workspaces repeatedly (Ctrl+1 through Ctrl+6)
- [ ] Memory usage stable (check Task Manager)
- [ ] No memory leaks visible
- [ ] Application remains responsive

### 5.3 Widget Disposal
1. Close application
- [ ] All widgets dispose cleanly (OnDispose() called)
- [ ] No "object in use" errors
- [ ] No unhandled exceptions

### 5.4 Error Handling
- [ ] Invalid Excel data → Graceful error message
- [ ] Missing profile file → Creates default
- [ ] Invalid cell reference → Warning but continues
- [ ] Export with 0 projects → Clear message

---

## Phase 6: Excel Integration End-to-End

### Scenario 1: Import Government Audit Data
1. Open Excel with SVI-CAS audit request (48 fields, W3:W130)
2. Select cell range W3:W50
3. Copy to clipboard
4. Switch to SuperTUI, Workspace 6, ExcelImportWidget
5. Paste data
- [ ] Preview shows correct field mappings
- [ ] All 48 fields parse correctly
- [ ] Status shows success
6. Click Import
- [ ] Project created in ProjectService
- [ ] Confirmation message displayed
7. Switch to Workspace 3 (Tasks)
- [ ] Imported project visible in TaskManagementWidget (if integrated)
8. Switch to Workspace 5 (Analytics)
- [ ] Project stats updated

### Scenario 2: Export Projects for Reporting
1. Create 3 test projects with various fields
2. Switch to Workspace 6, ExcelExportWidget
3. Select all 3 projects
4. Select format: TSV
5. Click "Copy to Clipboard"
6. Open Excel
7. Paste
- [ ] Data appears in correct cells
- [ ] Headers in first row
- [ ] Data rows follow
- [ ] Tab-separated format intact
8. Verify data matches project properties

### Scenario 3: Create Custom Mapping Profile
1. Switch to Workspace 6, ExcelMappingEditorWidget
2. Click "+" to create new profile
3. Name: "Custom Project Import"
4. Description: "Simplified 10-field import"
5. Click "Save Profile"
6. Add 10 mappings:
   - Name (W3 → Name)
   - Status (W4 → Status)
   - Priority (W5 → Priority)
   - Assigned (W6 → AssignedTo)
   - Due Date (W7 → DueDate)
   - Description (W8 → Description)
   - Category (W9 → Category)
   - Tags (W10 → Tags)
   - Budget (W11 → Budget)
   - Notes (W12 → Notes)
7. Mark first 5 for export
8. Save profile
9. Switch to ExcelImportWidget
10. Select "Custom Project Import" profile
- [ ] Field count shows 10
11. Import test data
- [ ] All 10 fields populate correctly
12. Switch to ExcelExportWidget
13. Select profile
- [ ] Export field count shows 5
14. Export project
- [ ] Only 5 fields appear in output

---

## Phase 7: Keyboard-Centric Workflow

### Global Shortcuts
- [ ] `Ctrl+1` through `Ctrl+6` → Workspace switching
- [ ] `Tab` / `Shift+Tab` → Widget focus navigation
- [ ] `Ctrl+Q` → Quit application (if implemented)

### Excel Widget Shortcuts
**ExcelImportWidget:**
- [ ] `Ctrl+V` → Paste from clipboard
- [ ] `Ctrl+I` → Import project
- [ ] `Escape` → Clear

**ExcelExportWidget:**
- [ ] `Ctrl+A` → Select all projects
- [ ] `Ctrl+E` → Export to clipboard
- [ ] `Ctrl+S` → Export to file
- [ ] `Escape` → Clear selection

**ExcelMappingEditorWidget:**
- [ ] `Ctrl+N` → New mapping
- [ ] `Ctrl+S` → Save (profile or mapping)
- [ ] `Escape` → Cancel editing

### Task Management Shortcuts
- [ ] `F2` → Edit selected task
- [ ] `Delete` → Delete selected task
- [ ] `Ctrl+N` → New task

---

## Phase 8: Visual & Aesthetic Testing

### Terminal Aesthetic
- [ ] Monospace fonts render correctly (Cascadia Mono, Consolas)
- [ ] ANSI-style colors visible
- [ ] Borders use theme colors
- [ ] Focus indicator (3px border) clearly visible
- [ ] Status text colors: Green (success), Red (error), Blue (info)

### Theme Consistency
For each theme (Dark, Light, Amber, Matrix, Synthwave):
- [ ] All widgets update immediately
- [ ] No "flash" of old theme
- [ ] Text remains readable
- [ ] Buttons clearly visible
- [ ] Focus indicator contrasts with background

### Layout Correctness
**Workspace 6 (Excel):**
- [ ] Top-left: ExcelImportWidget (50% width, 50% height)
- [ ] Top-right: ExcelExportWidget (50% width, 50% height)
- [ ] Bottom: ExcelMappingEditorWidget (100% width, 50% height)
- [ ] Splitters functional (drag to resize)
- [ ] No overlapping widgets
- [ ] No clipped content

---

## Phase 9: Documentation & Logging

### Console Output
- [ ] Startup messages clear and informative
- [ ] No error messages during normal operation
- [ ] Excel operations logged: "Imported project: [name]"
- [ ] Profile changes logged: "Saved profile: [name]"

### Error Messages
- [ ] User-facing errors clear and actionable
- [ ] No stack traces visible to user
- [ ] Error dialogs dismissible
- [ ] Application continues after error

### Log Files
- [ ] Check `%LOCALAPPDATA%\SuperTUI\logs\` for log files
- [ ] Log entries timestamped
- [ ] Critical errors logged with stack traces
- [ ] Info/Warning/Error levels appropriate

---

## Phase 10: Edge Cases & Stress Testing

### Large Data Sets
1. Import Excel data with 100+ rows
- [ ] Preview renders without lag
- [ ] Import completes in < 2 seconds
- [ ] Application remains responsive

2. Export 50+ projects
- [ ] Preview shows first 50 lines
- [ ] Export completes in < 3 seconds
- [ ] Clipboard handles large data

### Special Characters
1. Import data with special characters: Unicode, emojis, quotes
- [ ] Characters preserved correctly
- [ ] No encoding errors
- [ ] Display renders correctly

2. Export data with special characters
- [ ] CSV: Properly escaped quotes and commas
- [ ] TSV: Tab characters handled
- [ ] JSON: Valid escaping
- [ ] XML: Entity encoding correct

### Empty/Null Data
1. Import empty clipboard
- [ ] Error message: "Paste Excel data to preview"
- [ ] Import button disabled

2. Import data with null/empty fields
- [ ] Null fields handled gracefully
- [ ] Default values applied (if configured)
- [ ] No crashes

3. Export project with null fields
- [ ] Empty strings in output
- [ ] No "null" text literals
- [ ] Format remains valid

### Profile Management Edge Cases
1. Delete all profiles → Try to add mapping
- [ ] Prevented or gracefully handled
- [ ] Default profile auto-created

2. Create profile with duplicate name
- [ ] Allowed (profiles identified by GUID)
- [ ] Both profiles visible in dropdown

3. Corrupt profile JSON file
- [ ] Profile skipped during load
- [ ] Error logged
- [ ] Other profiles load successfully

---

## Phase 11: Integration with Other Widgets

### Cross-Workspace Communication
1. Import project in Workspace 6
2. Switch to Workspace 3 (Tasks)
- [ ] If TaskManagementWidget uses ProjectService, new project visible
- [ ] Stats update in TaskSummaryWidget

3. Create project in Workspace 3
4. Switch to Workspace 6, ExcelExportWidget
5. Refresh project list
- [ ] New project appears in list
- [ ] Can export new project

### EventBus Integration
- [ ] Check console for event bus messages (if logging enabled)
- [ ] No "EventBus error" messages
- [ ] Events delivered to subscribers

---

## Phase 12: Final Acceptance

### Functionality Checklist
- [ ] All 3 Excel widgets functional
- [ ] Import/Export round-trip works
- [ ] Profile management complete
- [ ] Mapping editor fully functional
- [ ] Keyboard shortcuts working
- [ ] State persistence working
- [ ] No crashes during normal use
- [ ] Error handling graceful

### Performance Checklist
- [ ] Startup < 5 seconds
- [ ] Widget operations < 1 second
- [ ] Import/Export < 3 seconds
- [ ] No UI freezing
- [ ] Memory usage stable

### Code Quality Checklist
- [ ] Build: 0 errors, 4 warnings (acceptable)
- [ ] No deprecation warnings for new code
- [ ] All widgets implement IDisposable/OnDispose()
- [ ] All widgets use dependency injection
- [ ] No hardcoded colors (use ThemeManager)

---

## Known Issues / Acceptable Warnings

### Build Warnings (4 total)
1. **ExcelExportWidget.cs:437** - `ProjectStatus` comparison always true
   - **Reason:** ProjectStatus is non-nullable enum
   - **Impact:** None, defensive check
   - **Fix:** Low priority, change to enum pattern matching

2. **ExcelImportWidget.cs:339** - `ProjectStatus` comparison always true
   - **Reason:** Same as above
   - **Impact:** None
   - **Fix:** Low priority

3. **CommunicationLayoutEngine.cs:337** - Unused variable `temp`
   - **Reason:** Legacy code
   - **Impact:** None
   - **Fix:** Low priority, remove variable

4. **NotesWidget.cs:37** - Unused field `helpLabel`
   - **Reason:** Incomplete feature
   - **Impact:** None
   - **Fix:** Low priority, implement or remove

---

## Test Results Template

```markdown
## Test Execution Results

**Date:** YYYY-MM-DD
**Tester:** [Name]
**Environment:** Windows [version], .NET [version]
**Build:** [commit hash or version]

### Summary
- Total Tests: [N]
- Passed: [N]
- Failed: [N]
- Skipped: [N]

### Critical Failures
[List any blocking issues]

### Non-Critical Issues
[List minor issues that don't block usage]

### Notes
[Any additional observations]

### Next Steps
[Recommendations for fixes or additional testing]
```

---

## Appendix: Test Data

### Sample Excel Data (SVI-CAS Format)

```
W3: ABC Corporation
W4: 123 Main St
W5: Building 5
W6: Springfield
W7: IL
W8: 62701
W17: CAS-2025-001
W23: Financial Audit
W24: Complete Financial Review
W30: In Progress
W40: 2025-01-15
W41: 2025-03-15
W42: 2025-04-01
W50: John Smith
W60: 120
W61: 95
W70: 3 findings
W80: Approve with conditions
```

### Sample Projects for Export Testing

**Project 1:**
- Name: "Website Redesign"
- Status: InProgress
- Priority: High
- DueDate: 2025-11-30
- Description: "Complete redesign of company website"

**Project 2:**
- Name: "Database Migration"
- Status: Pending
- Priority: Medium
- DueDate: 2025-12-15
- Description: "Migrate from SQL Server to PostgreSQL"

**Project 3:**
- Name: "Security Audit"
- Status: Completed
- Priority: Critical
- DueDate: 2025-10-20
- Description: "Annual security compliance audit"

---

## Success Criteria

### Minimum Viable (Must Pass)
- [ ] All 3 Excel widgets load without crashes
- [ ] Import single project from clipboard
- [ ] Export single project to clipboard
- [ ] Create/Edit/Delete profile
- [ ] Create/Edit/Delete mapping
- [ ] Application doesn't crash during normal use

### Full Acceptance (Should Pass)
- [ ] All keyboard shortcuts work
- [ ] State persistence works
- [ ] All themes render correctly
- [ ] Error handling graceful
- [ ] Performance acceptable (< 5s startup, < 3s operations)
- [ ] Integration with existing widgets works
- [ ] Round-trip import/export preserves data

### Excellence (Nice to Have)
- [ ] No warnings during build (4 acceptable warnings resolved)
- [ ] Memory usage optimal (no leaks)
- [ ] Visual polish (animations, transitions)
- [ ] Advanced features (bulk import, data validation)

---

**End of Checklist**
