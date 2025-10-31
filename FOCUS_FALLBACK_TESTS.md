# Focus Fallback Chain - Test Cases and Scenarios

**Date:** 2025-10-31
**Status:** Implementation Complete - Ready for Testing
**Build:** ✅ 0 Errors, 0 Warnings

## Overview

This document outlines test cases for the focus fallback chain implementation. These tests verify that keyboard input never gets "lost" when UI elements become garbage collected or unavailable.

## Test Categories

### Category 1: WeakReference Death Detection

Tests verifying that the system detects when weak references to focused elements die.

#### Test 1.1: GC Collects Focused Element

**Setup:**
1. Open TaskListPane with focus on a TextBox
2. Record focus history (FocusHistoryManager tracks it)
3. Force garbage collection
4. Close and reopen the pane

**Action:**
Call `RestorePaneFocus("TaskListPane")`

**Expected Result:**
- Weak reference is dead (`IsAlive == false`)
- Fallback #1 is triggered
- System focuses first focusable child (ListBox)
- Logs show: `"[RestorePaneFocus] Focus fallback #1: Focused first child"`

**Verification:**
- TaskListPane has keyboard focus
- User can interact with ListBox

#### Test 1.2: Multiple Weak References in Pane

**Setup:**
1. Open NotesPane with multiple TextBoxes
2. Record focus on TextBox #1
3. Record focus on TextBox #2
4. GC collects both TextBox instances
5. Trigger workspace switch

**Action:**
Call `RestorePaneFocus("NotesPane")`

**Expected Result:**
- TextBox #2 WeakReference is dead
- Fallback #1 finds another focusable child (TextBox #3 or container)
- Focus is restored to available element

**Verification:**
- NotesPane is interactive
- No focus exceptions in logs

---

### Category 2: Loaded State Validation

Tests verifying that the system respects element loaded state.

#### Test 2.1: Element Not Loaded Yet

**Setup:**
1. Trigger pane creation but delay loading
2. Attempt to restore focus before IsLoaded == true

**Action:**
Call `RestorePaneFocus("SomepPane")` before pane IsLoaded event fires

**Expected Result:**
- Element check fails (IsLoaded == false)
- RoutedEventHandler is attached to Loaded event
- Focus restoration deferred until load completes
- Logs show: `"Element not loaded yet for..., deferring focus"`

**Verification:**
- No premature focus attempt
- Focus restored when element loads
- Clean event handler cleanup

#### Test 2.2: Element Becomes Unloaded After Initial Check

**Setup:**
1. Element is loaded and focused
2. Parent container is unloaded (e.g., pane hidden)
3. Attempt to restore focus

**Action:**
1. Get focus history for element
2. Unload parent pane
3. Call `RestorePaneFocus()`

**Expected Result:**
- Double-check catches unloaded state
- Fallback chain is triggered
- Focus moves to available alternative

**Verification:**
- No exception thrown
- Focus applied to fallback target
- Logs show: `"Element no longer valid..., using fallback chain"`

---

### Category 3: Pane-Level Fallback Chain

Tests verifying the PaneManager fallback chain works correctly.

#### Test 3.1: Focused Pane Becomes Unavailable

**Setup:**
1. Open TaskListPane and NotesPane
2. Focus on TaskListPane
3. Simulate TaskListPane becoming unavailable (GC, unload)

**Action:**
Call `FocusPane(taskListPane)` when pane is no longer valid

**Expected Result:**
- Double-check in Dispatcher.BeginInvoke catches unavailability
- FocusFallbackPane() is called
- Attempt 1: Try previously focused pane (fails - same pane)
- Attempt 2: Focus first available pane (NotesPane)
- Logs show: `"Focus fallback #2: Focusing first available pane"`

**Verification:**
- NotesPane receives keyboard focus
- Keyboard input works
- No focus exceptions

#### Test 3.2: Close Focused Pane

**Setup:**
1. Open three panes: NotesPane (focused), TaskListPane, FileBrowserPane
2. Record NotesPane as focused

**Action:**
Call `paneManager.ClosePane(notesPane)`

**Expected Result:**
- ClosePane detects pane was focused
- FocusPane(openPanes[0]) is called
- Fallback chain ensures focus is applied
- Logs show focus moving to TaskListPane

**Verification:**
- TaskListPane is visually active
- Keyboard input is accepted
- No lost focus state

#### Test 3.3: Close All Panes Then Open New One

**Setup:**
1. Open NotesPane (focused), then close it
2. No panes remain open
3. Open new FileBrowserPane

**Action:**
1. Call `paneManager.CloseAll()`
2. Call `paneManager.OpenPane(fileBrowserPane)`

**Expected Result:**
- CloseAll closes the last pane
- FocusPane(null) scenario triggers fallback
- MainWindow gets focus as last resort
- Opening new pane applies focus to it

**Verification:**
- FileBrowserPane receives focus
- No lost focus state during transition

---

### Category 4: First Focusable Child Discovery

Tests verifying the system can find and focus children.

#### Test 4.1: Find First Focusable Child in Simple Hierarchy

**Setup:**
1. Create pane with structure:
   - Pane (not focusable)
     - Grid (not focusable)
       - Label (not focusable)
       - TextBox (focusable) ← target
       - Button (focusable)

**Action:**
Call `FindFirstFocusableChild(pane)` where pane element was GC'd

**Expected Result:**
- BFS finds TextBox before Button
- TextBox is returned and focused
- Logs show: `"Focus fallback #1: Focused first child TextBox"`

**Verification:**
- TextBox has focus
- Caret visible and active

#### Test 4.2: No Focusable Children Found

**Setup:**
1. Create pane with only non-focusable elements:
   - Pane (not focusable)
     - Grid (not focusable)
       - Label (not focusable)
       - TextBlock (not focusable)

**Action:**
Call `FindFirstFocusableChild(pane)`

**Expected Result:**
- BFS exhausts all children
- Returns null
- Fallback continues to Attempt 3
- Tries to focus pane itself

**Verification:**
- Fallback #2 is attempted
- Pane receives focus if focusable
- Otherwise MainWindow gets focus

#### Test 4.3: Complex Nested Hierarchy

**Setup:**
1. Create deep pane hierarchy with scrollable content:
   ```
   Pane
   ├─ DockPanel
   │  ├─ StackPanel (Header)
   │  │  └─ Button (focusable)
   │  └─ ScrollViewer
   │     └─ ItemsControl
   │        ├─ TextBox (focusable) ← first in BFS
   │        ├─ TextBox (focusable)
   │        └─ TextBox (focusable)
   ```

**Action:**
Call `FindFirstFocusableChild(pane)` with pane element GC'd

**Expected Result:**
- BFS order finds elements level-by-level
- First focusable found (Button in header)
- Returns Button, focus applied
- Logs show: `"Focused first child of TaskListPane: Button"`

**Verification:**
- Button has focus
- User can interact immediately

---

### Category 5: Visual Tree Navigation

Tests verifying the system can locate panes by ID.

#### Test 5.1: Find Pane from Tracked List

**Setup:**
1. Create TaskListPane and register with FocusHistoryManager
2. Call `FocusHistoryManager.TrackPane(taskListPane)`
3. Store pane reference
4. Delete all other references (except WeakReference)

**Action:**
Call `FindPaneById("TaskListPane")`

**Expected Result:**
- Checks trackedPanes dictionary first
- Finds WeakReference that is alive
- Returns pane instance
- No visual tree search needed

**Verification:**
- Pane found quickly
- Minimal performance impact

#### Test 5.2: Find Pane from Visual Tree Fallback

**Setup:**
1. Create pane but don't track it
2. Pane is in visual tree

**Action:**
Call `FindPaneById("NotesPane")` when pane not in tracked list

**Expected Result:**
- Tracked list lookup fails
- Falls back to visual tree search from MainWindow
- Recursively searches for PaneBase with matching name
- Finds and returns pane

**Verification:**
- Pane located despite not being tracked
- Search terminates when found
- No exception on failed search

#### Test 5.3: Pane Not Found

**Setup:**
1. Ask for non-existent pane ID
2. Pane not tracked and not in visual tree

**Action:**
Call `FindPaneById("NonExistentPane")`

**Expected Result:**
- Tracked list returns nothing
- Visual tree search returns nothing
- Function returns null gracefully
- Calling code handles null

**Verification:**
- No exception thrown
- Next fallback step is attempted
- Logs may show warning at attempt 4

---

### Category 6: Exception Recovery

Tests verifying the system handles unexpected exceptions.

#### Test 6.1: Focus() Throws Exception

**Setup:**
1. Create pane with custom Focus() that throws
2. Attempt to restore focus

**Action:**
Call `RestorePaneFocus("ProblematicPane")`

**Expected Result:**
- Try/catch in RestorePaneFocus catches exception
- Exception message logged
- Fallback chain triggered via catch block
- Source parameter is "RestorePaneFocus_Exception"

**Verification:**
- Exception is handled gracefully
- Focus applied to fallback target
- Logs show: `"[RestorePaneFocus_Exception]"`

#### Test 6.2: MoveFocus() Fails Silently

**Setup:**
1. Create pane where MoveFocus returns false
2. No focusable children available
3. Attempt to focus pane

**Action:**
Call `FocusPane(problematicPane)`

**Expected Result:**
- MoveFocus returns false
- FocusFallbackPane() is called
- Fallback attempts proceed

**Verification:**
- Focus fallback chain is triggered
- Log shows: `"Could not focus any child of..., using fallback chain"`
- Focus applied to alternative pane

#### Test 6.3: Dispatcher BeginInvoke Exception

**Setup:**
1. Main window or dispatcher is invalid
2. Focus operation scheduled via BeginInvoke
3. Exception occurs in dispatched action

**Action:**
1. Close main window while BeginInvoke is pending
2. Focus operation executes

**Expected Result:**
- Try/catch in dispatcher action catches exception
- FocusFallbackPane() is called as recovery
- Logs show exception and fallback

**Verification:**
- Application doesn't crash
- Focus is maintained via fallback
- Graceful degradation

---

### Category 7: Race Condition Scenarios

Tests verifying the system handles timing issues.

#### Test 7.1: Element Valid, Then Becomes Invalid

**Setup:**
1. Record element as focused
2. Element is valid when RestorePaneFocus starts
3. Element is GC'd during method execution
4. Double-check catches change

**Action:**
1. Get last focused control (succeeds, element exists)
2. Check IsLoaded (passes)
3. Trigger GC between checks
4. Try to focus (element is null now)

**Expected Result:**
- First check passes
- GC collects element
- Double-check catches null
- Fallback chain triggered
- Logs show: `"Element no longer valid..."`

**Verification:**
- No exception from focusing null element
- Fallback target receives focus
- Application remains responsive

#### Test 7.2: Pane Valid, Then Becomes Unloaded

**Setup:**
1. Open pane (IsLoaded == true)
2. Schedule focus via Dispatcher
3. Pane unloads before Dispatcher action executes

**Action:**
1. Call FocusPane(pane)
2. Pane unloads while BeginInvoke is queued
3. Dispatcher action executes

**Expected Result:**
- First check passes (pane is loaded)
4. FocusPane registers as deferred
5. Pane becomes unloaded
6. When dispatcher action runs, double-check catches it
7. FocusFallbackPane() is called

**Verification:**
- No exception when accessing unloaded pane
- Fallback pane receives focus
- Logs show deferred focus transition to fallback

#### Test 7.3: Concurrent Focus Requests

**Setup:**
1. Schedule two FocusPane calls rapidly
2. Both refer to different panes
3. Both execute via Dispatcher

**Action:**
1. Call `FocusPane(pane1)`
2. Immediately call `FocusPane(pane2)` (before first completes)
3. Let both dispatcher actions execute

**Expected Result:**
- First focus request queued to Dispatcher
- Second focus request queued to Dispatcher
- First executes, focuses pane1
- Second executes, focuses pane2
- Both succeed without interference

**Verification:**
- Final focused pane is pane2
- No focus loss
- Both panes attempted focus correctly

---

### Category 8: Integration Scenarios

Tests verifying the fallback chain works with other systems.

#### Test 8.1: Workspace Switch with Focus Restoration

**Setup:**
1. Workspace A: TaskListPane (focus on TextBox)
2. Workspace B: NotesPane (focus on different TextBox)
3. Switch to Workspace A

**Action:**
1. Save workspace A state (focus recorded)
2. Switch to Workspace B
3. Switch back to Workspace A
4. Call `RestorePaneFocus("TaskListPane")`

**Expected Result:**
- Fallback chain attempts to restore original TextBox
- If GC'd, falls back to first focusable child
- Focus restored to TaskListPane content
- Workspace A is interactive

**Verification:**
- Workspace A is fully functional
- Focus is on correct pane
- Content is preserved

#### Test 8.2: Pane Closed and Recreated

**Setup:**
1. Open TaskListPane
2. Focus on element inside pane
3. Close pane (dispose, unload)
4. Recreate TaskListPane fresh
5. Switch focus to it

**Action:**
1. Record focus in original pane
2. Call ClosePane(taskListPane)
3. Call OpenPane(new TaskListPane())
4. Call RestorePaneFocus("TaskListPane")

**Expected Result:**
- Old element is GC'd (no longer exists)
- New pane has different element instances
- Fallback chain focuses new pane's content
- User sees fresh pane, focused on first control

**Verification:**
- New pane is interactive
- No references to old pane elements
- Keyboard focus works

#### Test 8.3: Multi-Pane Navigation During Updates

**Setup:**
1. Open 4 panes in grid layout
2. Navigate between panes (Ctrl+Shift+Arrow)
3. Some panes are updating UI during navigation

**Action:**
1. Start rapid pane navigation
2. Panes are being redrawn/updated
3. Some elements are being recreated
4. Continue navigation

**Expected Result:**
- Fallback chain handles focus transitions
- Navigation works even with UI updates
- No focus loss during navigation
- Logs show fallback usage if any panes are rebuilding

**Verification:**
- All panes remain interactive
- Keyboard navigation succeeds
- No stuck/lost focus state

---

## Manual Testing Checklist

Use this checklist for manual testing in the actual application.

- [ ] **Open single pane, verify focus works**
  - [ ] Keyboard input is received by pane
  - [ ] Ctrl+W closes pane without crash
  - [ ] Focus doesn't appear to be lost

- [ ] **Open two panes, switch focus**
  - [ ] Ctrl+Shift+Right/Left switches panes
  - [ ] Keyboard input goes to correct pane
  - [ ] Visual focus indicator updates

- [ ] **Open three panes, close middle pane**
  - [ ] Pane closes successfully
  - [ ] Remaining panes re-tile
  - [ ] Focus moves to adjacent pane
  - [ ] Keyboard works in new focused pane

- [ ] **Workspace switching**
  - [ ] Ctrl+1 and Ctrl+2 switches workspaces
  - [ ] Focus is preserved in each workspace
  - [ ] UI elements aren't duplicated
  - [ ] No lag during switch

- [ ] **Rapid navigation**
  - [ ] Hold down Ctrl+Shift+Arrow to navigate rapidly
  - [ ] All panes stay responsive
  - [ ] No exceptions in debug output
  - [ ] Focus doesn't get stuck

- [ ] **Close all panes**
  - [ ] Ctrl+W multiple times closes all panes
  - [ ] Application doesn't crash
  - [ ] MainWindow still has focus
  - [ ] Opening new pane works

- [ ] **Long running operations**
  - [ ] Perform search/calculation in pane
  - [ ] Navigate away while operation running
  - [ ] Navigate back
  - [ ] Focus works after operation completes

- [ ] **Debug output verification**
  - [ ] Open debug console (if available)
  - [ ] Verify logs show focus operations
  - [ ] Check for any fallback chain logs
  - [ ] No exception logs during normal use

---

## Performance Considerations

### Fallback Chain Performance Metrics

**Acceptable Ranges:**

1. **Single Fallback Operation:** < 5ms
   - Finding first focusable child
   - Visual tree search for pane
   - Total focus apply time

2. **Full Fallback Sequence:** < 20ms
   - All four attempts in sequence
   - Logging included
   - Worst case scenario

3. **Normal Focus Operation:** < 1ms
   - Primary target is available
   - No fallback needed
   - Just apply focus

### Optimization Strategies

If performance testing shows slowness:

1. **Cache Pane Lookups** - Store FindPaneById results for 100ms
2. **Lazy Child Search** - Only search children if absolutely needed
3. **Index Focusable Elements** - Pre-compute focusable child lists
4. **Batch Logging** - Aggregate log messages in fallback chain

---

## Expected Log Output Examples

### Successful Focus Restore (No Fallback Needed)

```
[FocusHistory] Focus recorded: TextBox in TaskListPane
[FocusHistory] [RestorePaneFocus] Focus applied to requested element: TextBox
```

### Fallback #1 (Child Element)

```
[FocusHistory] Element no longer valid for pane TaskListPane, using fallback chain
[FocusHistory] [RestorePaneFocus_GCRecovery] Focus fallback #1: Focused first child of TaskListPane: ListBox
```

### Fallback #2 (Pane Itself)

```
[FocusHistory] [RestorePaneFocus] Focus fallback #2: Focused pane itself: NotesPane
```

### Fallback #3 (MainWindow)

```
[PaneManager] [Focus fallback #3: Focused MainWindow (all other attempts failed)
```

### Exhausted Fallback Chain

```
[FocusHistory] [RestorePaneFocus] Focus fallback exhausted: Could not focus any element for UnknownPane
```

---

## Test Automation

### Recommended Test Framework

**NUnit + WPF Testing Libraries**

```csharp
[TestFixture]
public class FocusFallbackChainTests
{
    [Test]
    public void RestorePaneFocus_WithGCdElement_UsesFallbackChain()
    {
        // Arrange
        var manager = new FocusHistoryManager(mockLogger);
        var pane = CreateTestPane();
        var textBox = pane.GetFirstChild<TextBox>();

        manager.RecordFocus(textBox, "TestPane");
        GC.Collect(); // Collect element

        // Act
        var result = manager.RestorePaneFocus("TestPane");

        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(pane.IsKeyboardFocusWithin);
    }

    [Test]
    public void FocusFallbackPane_WithUnavailablePane_FocusesAlternative()
    {
        // Arrange
        var paneManager = new PaneManager(mockLogger, mockThemeManager);
        var pane1 = CreateTestPane("Pane1");
        var pane2 = CreateTestPane("Pane2");

        paneManager.OpenPane(pane1);
        paneManager.OpenPane(pane2);
        paneManager.FocusPane(pane1);

        // Act
        paneManager.FocusPane(null); // Triggers fallback

        // Assert
        Assert.IsNotNull(paneManager.FocusedPane);
        Assert.IsTrue(pane2.IsKeyboardFocusWithin);
    }
}
```

---

## Summary

The focus fallback chain has four layers of protection:

1. **Primary Target** - Try exact element that had focus
2. **Child Element** - Try first focusable child of pane
3. **Pane Container** - Try the pane itself
4. **Application Window** - Fall back to MainWindow

Each layer is tested independently, then in combination with other systems to ensure robust keyboard input handling across all UI states.

All tests should pass with the current implementation, confirming that focus is never lost due to garbage collection or UI state changes.
