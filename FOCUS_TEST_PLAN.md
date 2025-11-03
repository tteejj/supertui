# SuperTUI Focus Management Test Plan

**Version:** 1.0
**Date:** 2025-11-02
**Target:** Focus Management Refactoring (Phase 1-3)
**Status:** Ready for Execution

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Test Environment Setup](#test-environment-setup)
3. [Test Execution Guidelines](#test-execution-guidelines)
4. [Test Categories](#test-categories)
   - [A. Basic Focus Navigation](#a-basic-focus-navigation)
   - [B. Command Palette Focus](#b-command-palette-focus)
   - [C. NotesPane Keyboard Input](#c-notespane-keyboard-input)
   - [D. Workspace Switching](#d-workspace-switching)
   - [E. Window Activation](#e-window-activation)
   - [F. State Persistence](#f-state-persistence)
   - [G. Edge Cases](#g-edge-cases)
5. [Visual Verification](#visual-verification)
6. [Performance Testing](#performance-testing)
7. [Regression Testing](#regression-testing)
8. [Test Results Summary](#test-results-summary)

---

## Prerequisites

### Build Verification

**Before starting tests, verify clean build:**

```powershell
cd C:\Users\jhnhe\OneDrive\Documents\GitHub\supertui\WPF
dotnet build SuperTUI.csproj
```

**Expected Results:**
- ✅ Build Succeeded
- ✅ 0 Errors
- ✅ 0 Warnings
- ⏱️ Build time: ~2-3 seconds

**If build fails:** Stop testing and fix build errors first.

### System Requirements

- Windows 10/11
- .NET 8.0 SDK installed
- PowerShell 7+ (for demo script)
- Keyboard with full modifier keys (Ctrl, Shift, Alt)

---

## Test Environment Setup

### 1. Clean Application State

**Delete existing state files to start fresh:**

```powershell
Remove-Item -Path "C:\Users\jhnhe\OneDrive\Documents\GitHub\supertui\WPF\.data\SuperTUI\State\*" -Force -ErrorAction SilentlyContinue
```

### 2. Launch Application

**Method 1: PowerShell Demo Script**
```powershell
cd C:\Users\jhnhe\OneDrive\Documents\GitHub\supertui\WPF
pwsh SuperTUI.ps1
```

**Method 2: Direct Execution**
```powershell
cd C:\Users\jhnhe\OneDrive\Documents\GitHub\supertui\WPF
dotnet run --project SuperTUI.csproj
```

### 3. Verify Initial State

- [ ] Application launches without errors
- [ ] MainWindow visible
- [ ] Status bar visible at bottom
- [ ] At least one pane visible
- [ ] No exception dialogs

---

## Test Execution Guidelines

### How to Use This Test Plan

1. **Execute tests in order** - Some tests depend on earlier setup
2. **Check all verification points** - Don't skip visual checks
3. **Record failures immediately** - Note exact steps to reproduce
4. **Use the Results Table** - Mark Pass/Fail/Skip for each test
5. **Take screenshots** - Capture visual bugs if found

### Recording Results

For each test case, record:
- **Status**: Pass / Fail / Skip / Blocked
- **Notes**: Any observations, even if test passes
- **Screenshots**: If visual issues found
- **Reproducibility**: Always / Sometimes / Once

### Severity Levels

- **Critical**: App crash, data loss, complete feature failure
- **High**: Major functionality broken, workaround difficult
- **Medium**: Feature partially works, workaround available
- **Low**: Minor visual issue, no functional impact

---

## Test Categories

## A. Basic Focus Navigation

### A1. Directional Navigation (Ctrl+Shift+Arrows)

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| A1.1 | 1. Launch app<br>2. Press `Ctrl+Shift+Right` | Focus moves to pane on the right | | |
| A1.2 | 1. Press `Ctrl+Shift+Left` | Focus moves to pane on the left | | |
| A1.3 | 1. Press `Ctrl+Shift+Down` | Focus moves to pane below | | |
| A1.4 | 1. Press `Ctrl+Shift+Up` | Focus moves to pane above | | |
| A1.5 | 1. Navigate to edge pane<br>2. Press direction toward edge | Focus wraps to opposite side OR stays at edge | | |

**Visual Verification for A1:**
- [ ] Active border (3px, colored) appears on focused pane
- [ ] Inactive border (1px, dimmed) on non-focused panes
- [ ] "►" prefix appears in focused pane header
- [ ] Drop shadow visible on focused pane
- [ ] No flicker during transition

### A2. Tab Navigation Within Panes

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| A2.1 | 1. Focus FileBrowserPane<br>2. Press `Tab` | Focus cycles through file list items | | |
| A2.2 | 1. Focus NotesPane<br>2. Press `Tab` | Focus cycles: Search → Note list → Editor | | |
| A2.3 | 1. Focus TaskListPane<br>2. Press `Tab` | Focus cycles through task items | | |
| A2.4 | 1. In any pane<br>2. Press `Shift+Tab` | Focus cycles backward through controls | | |

**Notes:**
- Tab navigation should stay WITHIN the focused pane
- Tab should NOT jump between panes (use Ctrl+Shift+Arrows for that)

### A3. Mouse Click Focus

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| A3.1 | 1. Click on inactive pane border | Pane receives focus | | |
| A3.2 | 1. Click on inactive pane header | Pane receives focus | | |
| A3.3 | 1. Click on inactive pane content area | Pane receives focus AND control clicked | | |
| A3.4 | 1. Click rapidly between 3 different panes | Each click focuses correct pane, no lag | | |

**Visual Verification for A3:**
- [ ] Focus changes immediately on click
- [ ] Visual feedback updates within 100ms
- [ ] No "stuck" focus states

---

## B. Command Palette Focus

### B1. Opening Command Palette

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| B1.1 | 1. Focus FileBrowserPane<br>2. Press `Ctrl+P` | Command palette opens, search box focused | | |
| B1.2 | 1. Focus NotesPane editor<br>2. Press `Ctrl+P` | Command palette opens, search box focused | | |
| B1.3 | 1. No pane focused<br>2. Press `Ctrl+P` | Command palette opens, search box focused | | |

**Verification:**
- [ ] Search box has blinking text cursor
- [ ] Can type immediately without clicking
- [ ] Command palette appears as modal overlay

### B2. Command Palette Focus Restoration (CRITICAL)

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| B2.1 | 1. Focus FileBrowserPane<br>2. Select a file<br>3. Press `Ctrl+P`<br>4. Press `Escape` | Focus returns to FileBrowserPane, same file selected | | |
| B2.2 | 1. Focus NotesPane editor<br>2. Place cursor mid-text<br>3. Press `Ctrl+P`<br>4. Press `Escape` | Focus returns to editor, cursor at same position | | |
| B2.3 | 1. Focus TaskListPane<br>2. Select a task<br>3. Press `Ctrl+P`<br>4. Press `Escape` | Focus returns to TaskListPane, same task selected | | |
| B2.4 | 1. Focus any pane<br>2. Press `Ctrl+P`<br>3. Type "notes"<br>4. Press `Enter` | NotesPane opens AND receives focus | | |

**Common Bug Patterns to Watch For:**
- [ ] Focus lost after closing palette (nothing focused)
- [ ] Focus goes to wrong pane
- [ ] Focus goes to correct pane but wrong control
- [ ] Cursor position lost in text editors

### B3. Command Palette Repeated Use

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| B3.1 | 1. Press `Ctrl+P`<br>2. Press `Escape`<br>3. Press `Ctrl+P` again | Opens successfully second time | | |
| B3.2 | 1. Open/close command palette 10 times rapidly | Works every time, no degradation | | |
| B3.3 | 1. Press `Ctrl+P`<br>2. Execute command<br>3. Press `Ctrl+P` again | Opens successfully after command execution | | |

**Known Issue to Verify Fixed:**
- ❌ Command palette only works on FIRST open (should be fixed)

---

## C. NotesPane Keyboard Input

### C1. Typing in Editor

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| C1.1 | 1. Open NotesPane (`Ctrl+P` → "notes")<br>2. Type "Hello World" in editor | Text appears, no interference | | |
| C1.2 | 1. In editor, type "Ctrl+1" | Character "1" typed (NOT workspace switch) | | |
| C1.3 | 1. In editor, type "Ctrl+Shift+Right" | Text selection (NOT pane navigation) | | |
| C1.4 | 1. Type multiple lines quickly | All characters appear, no dropped input | | |

**Known Issue to Verify Fixed:**
- ❌ Typing intercepted by global shortcuts (should be fixed)

### C2. Priority Shortcuts in Editor

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| C2.1 | 1. Type text in editor<br>2. Press `Ctrl+S` | Note saves (priority shortcut works) | | |
| C2.2 | 1. Have unsaved changes<br>2. Press `Escape` | Editor closes, prompts to save | | |
| C2.3 | 1. Type text<br>2. Press `Ctrl+P` | Command palette opens (priority shortcut) | | |

**Priority Shortcuts (should work even while typing):**
- `Ctrl+S` - Save note
- `Ctrl+P` - Command palette
- `Escape` - Close editor
- `Ctrl+N` - New note

**Regular Shortcuts (should NOT interfere):**
- `Ctrl+1-9` - Workspace switching
- `Ctrl+Shift+Arrows` - Pane navigation

### C3. Editor Focus After Commands

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| C3.1 | 1. Edit note<br>2. Press `Ctrl+S` (save)<br>3. Continue typing | Can type immediately after save | | |
| C3.2 | 1. Edit note<br>2. Press `Ctrl+P`, then `Escape`<br>3. Continue typing | Editor still focused, can type | | |
| C3.3 | 1. Edit note<br>2. Switch workspace (`Ctrl+2`)<br>3. Switch back (`Ctrl+1`) | Editor still focused, cursor position preserved | | |

---

## D. Workspace Switching

### D1. Basic Workspace Operations

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| D1.1 | 1. Press `Ctrl+1` | Workspace 1 active | | |
| D1.2 | 1. Press `Ctrl+2` | Workspace 2 active | | |
| D1.3 | 1. Add pane to Workspace 1<br>2. Press `Ctrl+2`<br>3. Press `Ctrl+1` | Workspace 1 panes restored | | |

### D2. Focus Preservation During Workspace Switch

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| D2.1 | 1. In Workspace 1, focus FileBrowserPane<br>2. Press `Ctrl+2`<br>3. Press `Ctrl+1` | FileBrowserPane focused again | | |
| D2.2 | 1. In Workspace 1, focus NotesPane editor<br>2. Press `Ctrl+2`<br>3. Press `Ctrl+1` | NotesPane editor focused, cursor position preserved | | |
| D2.3 | 1. Select file in FileBrowserPane<br>2. Switch workspace<br>3. Switch back | Same file still selected | | |

**Critical Verification:**
- [ ] Focus restored to EXACT control (not just pane)
- [ ] Selection states preserved
- [ ] Scroll positions preserved
- [ ] Text cursor positions preserved

### D3. State Preservation in NotesPane

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| D3.1 | 1. In Workspace 1, edit note "Test Note"<br>2. Type "Hello"<br>3. Don't save<br>4. Switch to Workspace 2<br>5. Switch back to Workspace 1 | "Hello" still in editor (unsaved) | | |
| D3.2 | 1. Edit note, place cursor at position 10<br>2. Switch workspace<br>3. Switch back | Cursor at position 10 | | |
| D3.3 | 1. Scroll editor to line 50<br>2. Switch workspace<br>3. Switch back | Editor scrolled to line 50 | | |

**Known Issue to Verify Fixed:**
- ❌ NotesPane content lost on workspace switch (should be fixed)

### D4. Rapid Workspace Switching (Stress Test)

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| D4.1 | 1. Quickly press: `Ctrl+1`, `Ctrl+2`, `Ctrl+3`, `Ctrl+1` | No crashes, correct workspace shown | | |
| D4.2 | 1. Switch workspaces 20 times rapidly | App remains responsive | | |
| D4.3 | 1. Switch workspace while pane is loading | No race conditions, no crashes | | |

---

## E. Window Activation

### E1. Alt+Tab Behavior

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| E1.1 | 1. Focus FileBrowserPane<br>2. Alt+Tab to another app<br>3. Alt+Tab back | FileBrowserPane focused again | | |
| E1.2 | 1. Focus NotesPane editor, cursor at position 5<br>2. Alt+Tab away<br>3. Alt+Tab back | Editor focused, cursor at position 5 | | |
| E1.3 | 1. Command palette open<br>2. Alt+Tab away<br>3. Alt+Tab back | Command palette still open, search box focused | | |

### E2. Window Minimize/Restore

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| E2.1 | 1. Focus any pane<br>2. Minimize window<br>3. Restore window | Same pane focused | | |
| E2.2 | 1. Edit note<br>2. Minimize<br>3. Restore | Editor focused, content preserved | | |

### E3. Window Click Activation

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| E3.1 | 1. Click window title bar | Focus restored to last focused pane | | |
| E3.2 | 1. Click window border | Focus restored correctly | | |

---

## F. State Persistence

### F1. Application Restart

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| F1.1 | 1. Focus FileBrowserPane<br>2. Select file "test.txt"<br>3. Exit app (`Alt+F4`)<br>4. Restart app | FileBrowserPane focused, "test.txt" selected | | |
| F1.2 | 1. Open 3 panes in Workspace 1<br>2. Exit app<br>3. Restart | All 3 panes restored | | |
| F1.3 | 1. Switch to Workspace 2<br>2. Exit app<br>3. Restart | Workspace 2 active on startup | | |

### F2. Multi-Workspace Persistence

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| F2.1 | 1. Setup Workspace 1 with FileBrowserPane<br>2. Setup Workspace 2 with NotesPane<br>3. Exit<br>4. Restart | Both workspaces restored correctly | | |
| F2.2 | 1. Focus different panes in Workspaces 1-3<br>2. Exit<br>3. Restart<br>4. Visit each workspace | Each workspace has correct focus | | |

### F3. State Corruption Recovery

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| F3.1 | 1. Manually corrupt state file (invalid JSON)<br>2. Restart app | App starts with default state, no crash | | |
| F3.2 | 1. Delete state file<br>2. Restart app | App starts fresh, no crash | | |

**State File Location:**
```
C:\Users\jhnhe\OneDrive\Documents\GitHub\supertui\WPF\.data\SuperTUI\State\workspace_state.json
```

---

## G. Edge Cases

### G1. Pane Lifecycle

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| G1.1 | 1. Focus PaneA<br>2. Open PaneB (`Ctrl+P` → command)<br>3. Note focus behavior | Focus moves to newly opened pane | | |
| G1.2 | 1. Open 2 panes<br>2. Focus PaneA<br>3. Close PaneA (if closeable) | Focus moves to PaneB | | |
| G1.3 | 1. Open pane while command palette open | Pane opens, command palette closes, new pane focused | | |

### G2. Error Dialog Focus

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| G2.1 | 1. Trigger error (e.g., invalid file operation)<br>2. MessageBox appears<br>3. Click OK | Focus returns to original pane | | |
| G2.2 | 1. Error during workspace switch<br>2. Dismiss error<br>3. Try workspace switch again | Workspace switch works | | |

### G3. No Panes Scenario

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| G3.1 | 1. Close all closeable panes<br>2. Press `Ctrl+Shift+Right` | No crash, focus stays on remaining UI | | |
| G3.2 | 1. Empty workspace<br>2. Press `Ctrl+P` | Command palette opens | | |

### G4. Race Conditions

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| G4.1 | 1. Press `Ctrl+P`<br>2. Immediately press `Escape`<br>3. Repeat 5 times rapidly | No crashes, focus restored correctly | | |
| G4.2 | 1. Switch workspace<br>2. Immediately press `Ctrl+P` | No crashes, command palette opens | | |
| G4.3 | 1. Open command palette<br>2. Start typing command<br>3. Press `Ctrl+1` (workspace switch)<br>4. Finish command | No crashes, state consistent | | |

### G5. Long-Running Operations

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| G5.1 | 1. Start long file operation in FileBrowserPane<br>2. Switch focus during operation | Focus switches, operation continues | | |
| G5.2 | 1. NotesPane auto-saving<br>2. Switch focus during save | No focus disruption | | |

---

## Visual Verification

### Visual Checklist

For EVERY test case above, verify these visual indicators:

#### Focused Pane Appearance
- [ ] **Border**: 3px width, colored (theme accent color)
- [ ] **Header**: "►" prefix before pane name
- [ ] **Shadow**: Drop shadow visible around pane
- [ ] **Title**: Text in theme accent color

#### Unfocused Pane Appearance
- [ ] **Border**: 1px width, dimmed color
- [ ] **Header**: No "►" prefix
- [ ] **Shadow**: No drop shadow
- [ ] **Title**: Text in muted color

#### Transition Quality
- [ ] **No flicker**: Smooth transition between states
- [ ] **No lag**: Visual update within 100ms of focus change
- [ ] **Consistent**: Same appearance across all pane types

### Visual Bug Patterns

Watch for these common visual issues:

| Bug Pattern | Description | Severity |
|-------------|-------------|----------|
| Double focus indicators | Two panes showing focused state | High |
| No focus indicators | No pane showing focused state | High |
| Stuck focus glow | Border/shadow not clearing from old pane | Medium |
| Flicker on transition | Brief flash of wrong state | Medium |
| Delayed update | Visual lags 500ms+ behind actual focus | High |
| Wrong colors | Using hardcoded colors instead of theme | Low |

### Screenshot Checklist

**Take screenshots for:**
1. Typical focused/unfocused pane comparison
2. Any visual bugs discovered
3. Before/after workspace switch
4. Command palette open state
5. Each pane type when focused

**Save screenshots to:**
```
C:\Users\jhnhe\OneDrive\Documents\GitHub\supertui\test-screenshots\
```

---

## Performance Testing

### Performance Benchmarks

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Focus switch time | < 100ms | Stopwatch, user perception |
| Workspace switch time | < 500ms | Stopwatch from keypress to UI ready |
| Command palette open time | < 200ms | Stopwatch from Ctrl+P to input ready |
| Memory leak rate | 0 MB/hour | Task Manager during extended session |

### P1. Focus Performance

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| P1.1 | 1. Switch focus between 2 panes 50 times<br>2. Note any lag | Each switch < 100ms, no degradation | | |
| P1.2 | 1. Switch focus while CPU busy (compile, etc.) | Still responsive, no blocking | | |

### P2. Workspace Switch Performance

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| P2.1 | 1. Workspace 1: 1 pane<br>2. Switch to Workspace 2<br>3. Measure time | < 500ms | | |
| P2.2 | 1. Workspace 1: 5 panes<br>2. Switch to Workspace 2<br>3. Measure time | < 500ms | | |
| P2.3 | 1. Switch between workspaces 100 times<br>2. Note any degradation | Consistent performance | | |

### P3. Memory Leak Detection

| Test Case | Steps | Expected Result | Actual Result | Status |
|-----------|-------|-----------------|---------------|--------|
| P3.1 | 1. Open Task Manager<br>2. Note baseline memory<br>3. Switch focus 100 times<br>4. Check memory | < 5MB increase | | |
| P3.2 | 1. Note baseline<br>2. Open/close command palette 50 times<br>3. Check memory | < 5MB increase | | |
| P3.3 | 1. Note baseline<br>2. Switch workspaces 50 times<br>3. Check memory | < 10MB increase | | |
| P3.4 | 1. Note baseline<br>2. Run app for 30 minutes, normal use<br>3. Check memory | < 50MB increase | | |

**Task Manager Monitoring:**
1. Press `Ctrl+Shift+Esc`
2. Find "SuperTUI.exe" process
3. Note "Memory (Private Working Set)" column
4. Monitor during test execution

---

## Regression Testing

### R1. Existing Functionality

These features should still work after focus refactoring:

| Feature | Test | Expected | Actual | Status |
|---------|------|----------|--------|--------|
| i3 Navigation | Ctrl+Shift+Arrows | Panes navigate correctly | | |
| Theme Switch | Ctrl+T (if bound) | Theme changes, focus preserved | | |
| Pane Opening | Ctrl+P → command | Pane opens | | |
| Pane Closing | Close button (if exists) | Pane closes | | |
| Status Bar | Always visible | Shows correct info | | |
| Task Operations | Add/edit/complete tasks | Works correctly | | |
| Note Operations | Create/edit/save notes | Works correctly | | |
| File Browser | Browse files | Selection works | | |
| Project Context | Switch projects | Updates correctly | | |

### R2. Keyboard Shortcuts

Verify all shortcuts still work:

| Shortcut | Expected Action | Verified |
|----------|-----------------|----------|
| `Ctrl+P` | Open command palette | [ ] |
| `Ctrl+1-9` | Switch workspace | [ ] |
| `Ctrl+Shift+Arrows` | Navigate panes | [ ] |
| `Ctrl+N` | New item (context-dependent) | [ ] |
| `Ctrl+S` | Save (context-dependent) | [ ] |
| `Escape` | Close/cancel | [ ] |
| `Tab` | Cycle within pane | [ ] |
| `Shift+Tab` | Cycle backward | [ ] |

### R3. UI Responsiveness

| Test | Expected Behavior | Verified |
|------|-------------------|----------|
| Window resize | Panes reflow, no crashes | [ ] |
| Long text in notes | Scrolling works | [ ] |
| Many tasks | List scrolls, no lag | [ ] |
| Many files | Browser scrolls, no lag | [ ] |
| Rapid commands | No command queue backup | [ ] |

---

## Test Results Summary

### Summary Template

```markdown
## Test Execution Summary

**Date:** [DATE]
**Tester:** [NAME]
**Build Version:** [COMMIT HASH]
**Duration:** [TIME]

### Results Overview

| Category | Total | Passed | Failed | Skipped | Pass Rate |
|----------|-------|--------|--------|---------|-----------|
| Basic Focus Navigation | 14 | | | | |
| Command Palette Focus | 13 | | | | |
| NotesPane Keyboard Input | 10 | | | | |
| Workspace Switching | 13 | | | | |
| Window Activation | 8 | | | | |
| State Persistence | 8 | | | | |
| Edge Cases | 13 | | | | |
| Visual Verification | 1 | | | | |
| Performance Testing | 10 | | | | |
| Regression Testing | 20 | | | | |
| **TOTAL** | **110** | | | | |

### Critical Issues Found

| ID | Severity | Description | Reproducibility | Test Case |
|----|----------|-------------|-----------------|-----------|
| | | | | |

### Non-Critical Issues Found

| ID | Severity | Description | Reproducibility | Test Case |
|----|----------|-------------|-----------------|-----------|
| | | | | |

### Performance Results

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Focus switch time | < 100ms | | |
| Workspace switch time | < 500ms | | |
| Command palette open | < 200ms | | |
| Memory leak (30min) | < 50MB | | |

### Known Issues Status

| Issue | Expected Fix Status | Actual Status | Verified |
|-------|---------------------|---------------|----------|
| Command palette first-open-only | Fixed | | [ ] |
| NotesPane typing intercepted | Fixed | | [ ] |
| Focus lost on workspace switch | Fixed | | [ ] |
| Content lost on workspace switch | Fixed | | [ ] |
| Circular focus loops | Fixed | | [ ] |
| Race conditions | Fixed | | [ ] |

### Recommendations

1. [Recommendation 1]
2. [Recommendation 2]
3. [Recommendation 3]

### Sign-Off

- [ ] All critical tests passed
- [ ] No critical bugs found
- [ ] Performance within targets
- [ ] Ready for production

**Tester Signature:** _______________
**Date:** _______________
```

---

## Appendix A: Quick Reference

### Focus State Debug Checklist

If focus seems wrong, check:

1. **Visual Indicators**
   - Border width/color correct?
   - Header prefix "►" present?
   - Drop shadow visible?

2. **Keyboard Focus**
   - Press Tab - does focus move within pane?
   - Type in editor - does text appear?
   - Press shortcut - does correct pane respond?

3. **WPF Focus Tree** (for developers)
   - Check `Keyboard.FocusedElement`
   - Check `FocusManager.GetFocusedElement()`
   - Check `IsKeyboardFocusWithin` property

### Common Test Scenarios

**Scenario 1: "Command Palette Not Working"**
1. Check if focus actually in search box (Tab key test)
2. Try closing and reopening
3. Check for error in logs
4. Verify no modal dialog blocking input

**Scenario 2: "Can't Type in NotesPane"**
1. Check if editor actually focused (caret visible?)
2. Try clicking in editor first
3. Check if global shortcuts intercepting
4. Verify NotesPane is active workspace pane

**Scenario 3: "Focus Lost After Operation"**
1. Note what operation was performed
2. Check if any pane is focused (visual indicators)
3. Try manually focusing a pane (mouse click)
4. Check if workspace switch occurred unintentionally

### Log File Locations

**Windows:**
```
C:\Users\jhnhe\OneDrive\Documents\GitHub\supertui\WPF\.data\SuperTUI\Logs\
```

**Look for:**
- `focus_*.log` - Focus-related events
- `error_*.log` - Errors during focus operations
- `performance_*.log` - Timing metrics

---

## Appendix B: Bug Report Template

When filing a bug, include:

```markdown
### Bug Report

**ID:** [UNIQUE-ID]
**Date:** [DATE]
**Severity:** Critical / High / Medium / Low

**Title:** [Short description]

**Test Case:** [Test case ID from test plan]

**Environment:**
- OS: Windows [version]
- .NET: 8.0
- Build: [commit hash]

**Steps to Reproduce:**
1. [Step 1]
2. [Step 2]
3. [Step 3]

**Expected Result:**
[What should happen]

**Actual Result:**
[What actually happened]

**Reproducibility:**
- Always (100%)
- Sometimes (50-99%)
- Rarely (<50%)
- Once (can't reproduce)

**Screenshots:**
[Attach screenshots]

**Logs:**
[Attach relevant log excerpts]

**Workaround:**
[If any workaround exists]

**Impact:**
[How does this affect users?]

**Notes:**
[Any additional observations]
```

---

## Appendix C: Testing Tools

### Recommended Tools

1. **Stopwatch** (Windows built-in)
   - Win+S → "Stopwatch"
   - Use for timing tests

2. **Task Manager** (Ctrl+Shift+Esc)
   - Monitor memory usage
   - Check CPU during performance tests

3. **ScreenToGif** (optional)
   - Record animated GIFs of bugs
   - Download: https://www.screentogif.com/

4. **Notepad++** (optional)
   - View log files with syntax highlighting
   - Download: https://notepad-plus-plus.org/

### Test Data Setup

**Sample Notes for Testing:**
```
Note 1: Short note (10 words)
Note 2: Medium note (100 words)
Note 3: Long note (1000 words, requires scrolling)
Note 4: Unicode test - 日本語 テスト עברית 🎉
Note 5: Special chars - "quotes" 'apostrophes' <brackets>
```

**Sample File Structure:**
```
C:\temp\test-files\
  ├── file1.txt
  ├── file2.txt
  ├── folder1\
  │   ├── nested1.txt
  │   └── nested2.txt
  └── folder2\
      └── deep\
          └── deeper\
              └── deepest.txt
```

---

## Appendix D: Test Plan Maintenance

### When to Update This Test Plan

- [ ] New focus-related feature added
- [ ] Focus bug discovered not covered by existing tests
- [ ] Test procedure found to be unclear/incorrect
- [ ] Performance targets change
- [ ] New pane type added to application

### Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-11-02 | Initial | Comprehensive test plan for focus refactoring |

---

## Document Statistics

- **Total Test Cases:** 110+
- **Test Categories:** 10
- **Visual Checks:** 15+
- **Performance Metrics:** 4
- **Regression Tests:** 20+
- **Appendices:** 4

---

**END OF TEST PLAN**
