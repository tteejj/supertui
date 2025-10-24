# How To Test SuperTUI Fixes

## Quick Start (Windows)

```powershell
cd WPF
./SuperTUI_TestFixes.ps1
```

**That's it.** The test demo will:
- ✅ Compile all code (including new widgets)
- ✅ Initialize logging
- ✅ Show SettingsWidget and ShortcutHelpWidget
- ✅ Display testing instructions
- ✅ Tell you where the log file is

---

## What You'll See

### Workspace 1: "Test Fixes"

**Left Panel - ShortcutHelpWidget (Fix #6)**
- Shows all keyboard shortcuts
- Search bar at top
- Category grouping (Navigation, Workspace, Application, etc.)

**Right Top - SettingsWidget (Fix #5)**
- Shows configuration settings
- Editable fields with validation
- Save/Reload buttons

**Right Bottom - ClockWidget**
- Just a clock for reference

### Workspace 2: "More Widgets"
- Counter, Notes, Task Summary widgets

---

## Testing Fix #5: Settings Validation

### What to Test
SettingsWidget should enforce validation rules and prevent invalid values from being saved.

### Steps

1. **Launch test demo:**
   ```powershell
   ./SuperTUI_TestFixes.ps1
   ```

2. **Find SettingsWidget** (right panel, top)

3. **Test Invalid Value:**
   - Find "Performance → MaxFPS" setting
   - Current value should be 60
   - Change it to: **-1000**
   - **Expected:** Border turns RED, tooltip shows error
   - **Expected:** "Save Settings" does NOT save this value

4. **Test Valid Value:**
   - Change it to: **120**
   - **Expected:** Border stays normal (white/gray)
   - **Expected:** "Save Settings" works

5. **Test Other Validations:**
   - MaxThreads: Try 100 (max is 32) → should fail
   - MaxThreads: Try 8 → should work
   - FontSize: Try 50 (max is 32) → should fail
   - FontSize: Try 14 → should work

### What Should Happen
- ✅ Invalid values show red border
- ✅ Invalid values show tooltip with error
- ✅ Invalid values are NOT saved
- ✅ Valid values show normal border
- ✅ Valid values can be saved
- ✅ Validation hints show acceptable ranges

### If It Fails
Check log file (path shown at startup):
```powershell
notepad "$env:TEMP/supertui_test_*.log"
```

Look for validation-related errors.

---

## Testing Fix #6: Dynamic Shortcut Loading

### What to Test
ShortcutHelpWidget should load shortcuts from ShortcutManager dynamically, not from hardcoded list.

### Steps

1. **Launch test demo:**
   ```powershell
   ./SuperTUI_TestFixes.ps1
   ```

2. **Find ShortcutHelpWidget** (left panel)

3. **Verify Tab Shortcuts Appear:**
   - Look for "Navigation" category
   - Should see: **"Tab - Focus next widget"**
   - Should see: **"Shift+Tab - Focus previous widget"**

   **Why this matters:** In previous version, these were hardcoded. Now they're loaded from ShortcutManager.

4. **Verify Workspace Shortcuts:**
   - Look for "Workspace" category
   - Should see: **"Ctrl+1 - Switch to workspace 1"**
   - Should see: **"Ctrl+2 - Switch to workspace 2"**
   - etc.

5. **Test Search:**
   - Type "focus" in search box
   - Should find Tab shortcuts
   - Type "workspace" in search box
   - Should find Ctrl+1-9 shortcuts

6. **Verify Widget-Specific Shortcuts:**
   - Look for "Counter Widget" category
   - Should see: Up, Down, R keys
   - These are still hardcoded (widget-specific, not global)

### What Should Happen
- ✅ Tab/Shift+Tab appear under "Navigation"
- ✅ Ctrl+1-9 appear under "Workspace"
- ✅ Search works
- ✅ Categories are correctly assigned
- ✅ No duplicate shortcuts

### If It Fails
Check if:
- Shortcuts are missing → ShortcutManager registration failed
- Duplicates appear → Hardcoded list wasn't properly removed
- Wrong categories → InferCategory() logic has issues

Check log file for errors.

---

## Testing Fix #7: Thread-Safety

### What to Test
ShortcutManager singleton is now thread-safe using Lazy<T>.

### Steps

**Unfortunately, you can't easily test this in the demo.**

This fix prevents race conditions when multiple threads access ShortcutManager simultaneously. You'd need a multithreaded stress test to verify.

### Verification
- Code inspection: Check `WPF/Core/Infrastructure/ShortcutManager.cs` lines 31-33
- Should see: `private static readonly Lazy<ShortcutManager> instance = ...`
- Should NOT see: `instance ??= new ShortcutManager()`

### What Should Happen
- ✅ Application doesn't crash
- ✅ Shortcuts work normally
- ✅ No threading errors in log

---

## Testing Fix #8: State Migration

### What to Test
State migration infrastructure works correctly.

### Steps

1. **Run migration test script:**
   ```powershell
   cd WPF
   ./Test_StateMigration.ps1
   ```

2. **Expected output:**
   ```
   === SuperTUI State Migration Test ===
   ✓ Test migration class compiled
   ✓ Created test state file
   ✓ Registered test migration: 1.0 -> 1.1

   Loading old state (version 1.0)...
   ✓ Loaded state

   Applying migration...
   ✓ Migration completed

   Post-migration verification:
     ✓ Version updated to 1.1
     ✓ MigrationTestField added
     ✓ MigrationTestField value correct
     ✓ Workspace 1 has timestamp
     ✓ Workspace 2 has timestamp
     ✓ Original data preserved

   === ALL TESTS PASSED ===
   ```

3. **If any test fails:**
   - Check error message
   - Review `WPF/Core/Extensions.cs` (migration code)
   - Check `WPF/STATE_MIGRATION_GUIDE.md` for troubleshooting

### What Should Happen
- ✅ All 6 tests pass
- ✅ State is migrated from 1.0 to 1.1
- ✅ New fields are added
- ✅ Original data is preserved

---

## General Testing

### Basic Functionality

1. **Application Launch:**
   ```powershell
   ./SuperTUI_TestFixes.ps1
   ```
   - Should compile without errors
   - Should show window
   - Should show 2 workspaces

2. **Keyboard Navigation:**
   - Press **Tab** → Focus moves to next widget (cyan border)
   - Press **Shift+Tab** → Focus moves to previous widget
   - Press **Ctrl+1** → Switch to Workspace 1
   - Press **Ctrl+2** → Switch to Workspace 2
   - Press **Ctrl+Q** → Quit application

3. **Window Controls:**
   - Click **Minimize** → Window minimizes
   - Click **Maximize** → Window maximizes
   - Click **Close** or press **Ctrl+Q** → Window closes

4. **Logging:**
   - Check log file location (shown at startup)
   - Open log file: `notepad "$env:TEMP/supertui_test_*.log"`
   - Should see initialization messages, widget activations, etc.

### If Compilation Fails

**Error:** "Add-Type: Cannot add type..."

**Solutions:**
1. Make sure you're on Windows
2. Make sure you're in the WPF directory
3. Check for syntax errors in C# files
4. Look at the error message - it tells you which file/line failed

**Common Issues:**
- Missing semicolon
- Undefined type (wrong namespace)
- Circular dependency
- Missing using statement

### If Application Crashes

1. **Check log file** (path shown at startup)
2. **Look for exceptions** in the log
3. **Try original demo** to see if basic framework works:
   ```powershell
   ./SuperTUI_Demo.ps1
   ```
4. **If original demo works but test demo doesn't:**
   - Problem is in new widgets (SettingsWidget, ShortcutHelpWidget)
   - Check log for which widget initialization failed

### If Widgets Don't Respond

1. **Check focus:** Widget must have cyan border to receive input
2. **Press Tab** to cycle focus
3. **Check log** for keyboard event handling errors

---

## Interpreting Results

### All Tests Pass ✅
- Fix #5: Validation works
- Fix #6: Shortcuts load dynamically
- Fix #7: No threading issues (implicit)
- Fix #8: Migration works

**Conclusion:** Fixes are working as intended.

### Some Tests Fail ❌

**Settings validation doesn't work:**
- Check `WPF/Widgets/SettingsWidget.cs` lines 300-550 (validation logic)
- Check if ConfigValue.Validator is null
- Check log for validation errors

**Shortcuts don't appear:**
- Check if ShortcutManager.GetAllShortcuts() returns empty list
- Check if Tab/Shift+Tab were registered in demo
- Check log for shortcut registration errors

**Migration test fails:**
- Check `WPF/Core/Extensions.cs` (StateMigrationManager)
- Check if IStateMigration interface is implemented correctly
- Check log for migration errors

---

## Cleanup

After testing, you can delete:
- Log files: `$env:TEMP/supertui_test_*.log`
- Config files: `$env:TEMP/supertui_test_config.json`
- Migration test files: (automatically cleaned up by test script)

---

## Next Steps

### If Everything Works:
1. ✅ Fixes are validated
2. ✅ Code is production-ready (for these specific fixes)
3. ✅ Can integrate into main demo if desired

### If Issues Found:
1. Document the failures
2. Check log files for errors
3. Report specific issues with:
   - What you did
   - What you expected
   - What actually happened
   - Log file contents

---

## Summary

**To test everything:**
```powershell
cd WPF

# Test fixes #5 and #6
./SuperTUI_TestFixes.ps1

# Test fix #8
./Test_StateMigration.ps1

# Check logs
notepad "$env:TEMP/supertui_test_*.log"
```

**Expected time:** 5-10 minutes

**Expected outcome:** All fixes work as documented.
