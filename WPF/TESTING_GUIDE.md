# SuperTUI Testing Guide

## Prerequisites

**Platform:** Windows with PowerShell 5.1+
**Location:** You must run this from the WPF directory

---

## Quick Start

```powershell
cd WPF
./SuperTUI_Demo.ps1
```

**Expected:** Application launches without errors.

---

## Current Demo Status

### ❌ Problem: New Widgets Not Included

The following widgets were created but are **NOT compiled or included in SuperTUI_Demo.ps1:**

- ❌ SettingsWidget.cs (Fix #5 validation testing)
- ❌ ShortcutHelpWidget.cs (Fix #6 dynamic shortcuts)
- ❌ SystemMonitorWidget.cs (previous session)
- ❌ GitStatusWidget.cs (previous session)
- ❌ FileExplorerWidget.cs (previous session)
- ❌ TerminalWidget.cs (previous session)
- ❌ TodoWidget.cs (previous session)
- ❌ CommandPaletteWidget.cs (previous session)

**Currently Compiled Widgets:**
- ✅ ClockWidget.cs
- ✅ TaskSummaryWidget.cs
- ✅ CounterWidget.cs
- ✅ NotesWidget.cs

### ❌ Problem: No Logging Initialization

The demo doesn't initialize the Logger, so:
- No log file is created
- No way to verify fixes are working
- No debugging information

### ❌ Problem: No Way to Test New Features

You cannot test:
- Settings Widget validation (widget not included)
- Shortcut Help Widget dynamic loading (widget not included)
- State migration (no test in demo)
- ShortcutManager thread-safety (no visible way to test)

---

## What You CAN Test Right Now

### Test #1: Basic Application Launch

```powershell
cd WPF
./SuperTUI_Demo.ps1
```

**Verify:**
- ✅ Window opens
- ✅ 3 workspaces with widgets visible
- ✅ No compilation errors

**Expected Widgets:**
- Workspace 1: Clock, Task Summary, 2 Counters, Notes
- Workspace 2: 6 Counter widgets in grid
- Workspace 3: Clock (top), Task Summary (left), Notes (fill)

### Test #2: Focus Management

**Actions:**
1. Press Tab → focus moves to next widget (cyan border)
2. Press Shift+Tab → focus moves to previous widget
3. Press Up/Down in focused Counter → increment/decrement
4. Press R in focused Counter → reset to 0

**Verify:**
- ✅ Focus border appears on correct widget
- ✅ Counter responds to keyboard

### Test #3: Workspace Switching

**Actions:**
1. Press Ctrl+1 → switch to Workspace 1
2. Press Ctrl+2 → switch to Workspace 2
3. Press Ctrl+3 → switch to Workspace 3
4. Press Ctrl+Left → previous workspace
5. Press Ctrl+Right → next workspace

**Verify:**
- ✅ Workspaces switch
- ✅ Title bar updates
- ✅ Status bar shows correct workspace name

### Test #4: State Persistence

**Actions:**
1. Launch app
2. Change Counter value with Up/Down
3. Type text in Notes widget
4. Switch to Workspace 2
5. Switch back to Workspace 1

**Verify:**
- ✅ Counter value preserved
- ✅ Notes text preserved

### Test #5: Window Controls

**Actions:**
1. Click Minimize button
2. Click Maximize button
3. Click Close button (or Ctrl+Q)

**Verify:**
- ✅ Window minimizes/maximizes/closes

---

## What You CANNOT Test

### ❌ Cannot Test Fix #5: Settings Validation

**Reason:** SettingsWidget not compiled or added to demo

**To Enable:**
1. Add `"Widgets/SettingsWidget.cs"` to `$widgetFiles` in SuperTUI_Demo.ps1
2. Add SettingsWidget to a workspace
3. Test validation by entering invalid values

### ❌ Cannot Test Fix #6: Dynamic Shortcuts

**Reason:** ShortcutHelpWidget not compiled or added to demo

**To Enable:**
1. Add `"Widgets/ShortcutHelpWidget.cs"` to `$widgetFiles` in SuperTUI_Demo.ps1
2. Add ShortcutHelpWidget to a workspace
3. Verify shortcuts loaded from ShortcutManager

### ❌ Cannot Test Fix #7: Thread-Safety

**Reason:** No visible way to test singleton thread-safety

**To Test:** Would need multithreaded stress test (not in demo)

### ❌ Cannot Test Fix #8: State Migration

**Reason:** Test script exists but is separate

**To Test:**
```powershell
cd WPF
./Test_StateMigration.ps1
```

---

## How to Enable Full Testing

I need to create an updated demo that includes:

1. **Logger Initialization**
   ```powershell
   $logger = [SuperTUI.Infrastructure.Logger]::Instance
   $logger.Initialize("$env:TEMP/supertui_test.log")
   ```

2. **Compile New Widgets**
   ```powershell
   $widgetFiles = @(
       "Widgets/ClockWidget.cs"
       "Widgets/TaskSummaryWidget.cs"
       "Widgets/CounterWidget.cs"
       "Widgets/NotesWidget.cs"
       "Widgets/SettingsWidget.cs"        # ADD
       "Widgets/ShortcutHelpWidget.cs"     # ADD
       "Widgets/SystemMonitorWidget.cs"    # ADD
       # ... etc
   )
   ```

3. **Add Widgets to Workspace**
   ```powershell
   # Add Settings widget
   $settings = New-Object SuperTUI.Widgets.SettingsWidget
   $settingsParams = @{ Row=0; Column=0 }
   $workspace1.AddWidget($settings, $settingsParams)

   # Add Shortcut Help widget
   $shortcuts = New-Object SuperTUI.Widgets.ShortcutHelpWidget
   $shortcutsParams = @{ Row=1; Column=0 }
   $workspace1.AddWidget($shortcuts, $shortcutsParams)
   ```

4. **Add Logging to Verify Fixes**
   ```powershell
   Write-Host "Check log file: $env:TEMP/supertui_test.log"
   ```

---

## Should I Create This?

**Question for you:**

Do you want me to:

1. ✅ **Create a new test demo** (`SuperTUI_TestFixes.ps1`) that:
   - Initializes logging
   - Compiles ALL widgets (including new ones)
   - Adds SettingsWidget and ShortcutHelpWidget to workspaces
   - Includes instructions for testing each fix
   - Shows log file location

2. ✅ **Update existing demo** (`SuperTUI_Demo.ps1`) to include everything above

3. ✅ **Create minimal test scripts** for each fix:
   - `Test_SettingsValidation.ps1` → Test Settings validation
   - `Test_ShortcutLoading.ps1` → Test dynamic shortcuts
   - `Test_StateMigration.ps1` → Already exists
   - `Test_AllFixes.ps1` → Run all tests

**My Recommendation:** Create `SuperTUI_TestFixes.ps1` so you have:
- **SuperTUI_Demo.ps1** → Original clean demo with 4 basic widgets
- **SuperTUI_TestFixes.ps1** → Test harness with all new widgets + logging

This way you can:
- Test the 4 basic widgets work (current demo)
- Test the new fixes separately (test demo)
- Not break the existing demo

---

## Manual Compilation Test

If you want to test compilation before running the full demo:

```powershell
cd WPF

# Test compile just the core framework
Add-Type -Path "Core/Infrastructure/Logger.cs" -ReferencedAssemblies @(
    "PresentationFramework",
    "PresentationCore",
    "WindowsBase"
)

# Test compile SettingsWidget
Add-Type -Path "Widgets/SettingsWidget.cs" -ReferencedAssemblies @(
    "PresentationFramework",
    "PresentationCore",
    "WindowsBase",
    "System.Xaml"
)

Write-Host "✓ Compilation successful" -ForegroundColor Green
```

**If this fails:** There are compilation errors that need fixing.

---

## Current Reality

**What works right now:**
- ✅ SuperTUI_Demo.ps1 should launch and work (with 4 basic widgets)
- ✅ Test_StateMigration.ps1 might work (untested)

**What doesn't work:**
- ❌ Cannot test Settings validation (widget not in demo)
- ❌ Cannot test Shortcut Help (widget not in demo)
- ❌ No logging to verify anything
- ❌ 8 widgets created but never compiled

**Bottom line:** I wrote a bunch of code but didn't integrate it into anything runnable.

---

## What Do You Want Me To Do?

**Option A:** "Create SuperTUI_TestFixes.ps1 with everything integrated so I can test"

**Option B:** "Update SuperTUI_Demo.ps1 to include new widgets"

**Option C:** "Create individual test scripts for each fix"

**Option D:** "Just tell me how to manually test - I'll figure it out"

**Option E:** Something else

Let me know and I'll create the appropriate testing infrastructure.
