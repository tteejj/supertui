# SuperTUI Focus & Input System - Edge Cases & Potential Issues Analysis

**Analysis Date:** 2025-10-31
**Scope:** Complete focus management, keyboard input routing, and event handling
**Thoroughness Level:** Very Thorough (7 categories, 100+ code points examined)

---

## 1. RACE CONDITIONS & TIMING ISSUES

### 1.1 Focus Restoration Race During Workspace Switch (CRITICAL)

**Location:** `MainWindow.xaml.cs:390-398`

```csharp
// Restore focus to the previously focused pane
Dispatcher.BeginInvoke(new Action(() =>
{
    if (paneManager.FocusedPane != null)
    {
        var paneName = paneManager.FocusedPane.PaneName;
        focusHistory.RestorePaneFocus(paneName);
        logger.Log(LogLevel.Debug, "MainWindow", 
            $"Restored focus to {paneName} after workspace switch");
    }
}), System.Windows.Threading.DispatcherPriority.Loaded);
```

**Issue:** Race condition between pane restoration and focus restoration
- Panes are restored in `RestoreWorkspaceState()` line 382
- Focus restoration happens asynchronously at `Loaded` priority
- If pane finishes loading AFTER focus is restored, `paneManager.FocusedPane` might be null
- `focusHistory.RestorePaneFocus()` will try to focus a control in a pane that's still initializing

**Scenario:**
1. Workspace switch saves current state
2. New workspace starts restoring panes (line 382)
3. Focus restoration is queued at Loaded priority
4. If focus restoration executes BEFORE pane.Initialize() completes, it fails silently
5. Control in pane never gets focus

**Risk Level:** HIGH - Silent failure, user sees unfocused pane

---

### 1.2 Focus Dispatcher Priority Inversion (HIGH)

**Location:** `PaneManager.cs:155-171`

```csharp
pane.Dispatcher.BeginInvoke(new Action(() =>
{
    if (!pane.IsKeyboardFocusWithin)
    {
        if (pane.Focusable && pane.Focus())
        {
            System.Windows.Input.Keyboard.Focus(pane);
        }
        else
        {
            pane.MoveFocus(new System.Windows.Input.TraversalRequest(
                System.Windows.Input.FocusNavigationDirection.First));
        }
    }
}), System.Windows.Threading.DispatcherPriority.Input);
```

**Issue:** Focus set at `Input` priority, but many window events (activation, etc.) are at `Normal` or `Loaded` priority
- MainWindow_Activated (line 620) uses `Input` priority - OK
- MainWindow_Loaded (line 649) uses `Loaded` priority - CONFLICTS
- During window activation, there's a priority race: who wins between Input-priority focus and Loaded-priority focus restoration?

**Race Scenario:**
1. Window activates
2. Focus restoration queued at Loaded priority
3. PaneManager.FocusPane() queues at Input priority (higher)
4. Input fires FIRST, sets focus to pane X
5. Loaded fires AFTER, sets focus to pane Y (from history)
6. Final focus: wrong pane

**Risk Level:** HIGH - Silent precedence issue, user sees focus jump unexpectedly

---

### 1.3 FocusHistoryManager WeakReference & Garbage Collection Race (MEDIUM)

**Location:** `FocusHistoryManager.cs:86-98`

```csharp
public UIElement GetLastFocusedControl(string paneId)
{
    if (string.IsNullOrEmpty(paneId)) return null;

    if (paneFocusMap.TryGetValue(paneId, out var record))
    {
        if (record.Element.IsAlive)  // <-- RACE: Can be collected between check and use
        {
            return record.Element.Target as UIElement;
        }
    }
    return null;
}
```

**Issue:** TOCTOU (Time-Of-Check vs Time-Of-Use) race condition
- Line 92: `record.Element.IsAlive` returns true
- Garbage collection happens here (no lock!)
- Line 94: `.Target as UIElement` could return null or a different object

**Scenario:**
1. Control is focused but weakly referenced
2. Workspace switches
3. IsAlive check passes
4. GC runs (DispatcherPriority.Background can run during this)
5. .Target returns null
6. RestorePaneFocus() silently fails

**Risk Level:** MEDIUM - Low probability but catastrophic when happens

---

### 1.4 EventBus Unsubscribe Race (MEDIUM)

**Location:** `EventBus.cs:156-167` and `NotesPane.cs:2040-2044`

```csharp
// In NotesPane disposal:
if (taskSelectedHandler != null)
{
    eventBus.Unsubscribe(taskSelectedHandler);
}
```

```csharp
// In EventBus.Unsubscribe<TEvent>:
typedSubscriptions[eventType].RemoveAll(s =>
{
    if (s.IsWeak)
    {
        var target = s.HandlerReference?.Target as Action<TEvent>;
        return target == handler || !s.HandlerReference.IsAlive;
    }
    // ...
});
```

**Issue:** No synchronization between publishing and unsubscribing
- Publisher holds lock during Publish() (line 177)
- Subscriber holds lock during Unsubscribe() (line 150)
- But: A handler could be invoked AFTER unsubscribe if GC collects the weak reference during iteration

**Scenario:**
1. NotesPane.Dispose() calls eventBus.Unsubscribe()
2. During Unsubscribe(), handler is being removed
3. Meanwhile, publisher calls Publish() on another thread (unlikely but possible)
4. Handler gets invoked after pane is disposed
5. Null reference error in disposed pane

**Risk Level:** MEDIUM - Requires specific threading model (unlikely in UI, but possible)

---

## 2. NULL REFERENCE RISKS

### 2.1 WeakReference.Target Null Returns (MEDIUM)

**Multiple Locations:**
- `FocusHistoryManager.cs:92-94` - Already discussed above
- `FocusHistoryManager.cs:145-149` - NavigateBack() method

```csharp
if (focusHistory.TryPeek(out var previous))
{
    if (previous.Element.IsAlive)
    {
        var element = previous.Element.Target as UIElement;
        element?.Focus();  // <-- Could be null if GC runs
        Keyboard.Focus(element);  // <-- Will throw if element is null!
    }
}
```

**Issue:** `Keyboard.Focus(null)` throws ArgumentNullException
- Line 149: No null check after `.Target as UIElement`
- If weak reference object is collected between IsAlive check and .Target call, this fails

**Risk Level:** MEDIUM - Causes unhandled exception

---

### 2.2 Pane.Focus() Returns False Without Null Checks (MEDIUM)

**Location:** `PaneManager.cs:149-169`

```csharp
// CRITICAL FIX: Actually set WPF keyboard focus
if (!pane.IsFocused)  // <-- Check WPF's actual IsFocused
{
    pane.Focus();  // <-- Returns false if can't focus, no check
}

// Force keyboard focus to the pane or its first focusable child
pane.Dispatcher.BeginInvoke(new Action(() =>
{
    if (!pane.IsKeyboardFocusWithin)
    {
        if (pane.Focusable && pane.Focus())  // <-- Line 160: what if this is false?
        {
            System.Windows.Input.Keyboard.Focus(pane);
        }
        else
        {
            pane.MoveFocus(new System.Windows.Input.TraversalRequest(...));
        }
    }
}), System.Windows.Threading.DispatcherPriority.Input);
```

**Issue:** Multiple focus attempts with no visibility into why they fail
- Line 151: `pane.Focus()` called but result discarded
- Line 160: `pane.Focus()` checked, but if false, falls back to MoveFocus()
- No logging of why focus failed - silent failure

**Risk Level:** MEDIUM - Silent failures make debugging impossible

---

### 2.3 Null Checks in Focus Restoration (MEDIUM-HIGH)

**Location:** `MainWindow.xaml.cs:614-623`

```csharp
private void MainWindow_Activated(object sender, EventArgs e)
{
    // Restore focus to the currently focused pane
    if (paneManager?.FocusedPane != null)
    {
        logger.Log(LogLevel.Debug, "MainWindow", 
            $"Window activated, restoring focus to {paneManager.FocusedPane.PaneName}");

        // Use dispatcher to ensure focus is set after activation completes
        Dispatcher.BeginInvoke(new Action(() =>
        {
            paneManager.FocusPane(paneManager.FocusedPane);  // <-- What if FocusedPane became null?
        }), System.Windows.Threading.DispatcherPriority.Input);
    }
}
```

**Issue:** Deferred focus restoration without null check
- Line 615: Check `paneManager?.FocusedPane != null`
- Line 622: Deferred call to `paneManager.FocusPane(paneManager.FocusedPane)`
- Between check and execution, FocusedPane could become null if:
  - User closes the pane (line 89-96 in PaneManager.cs sets focusedPane to null)
  - New workspace switches
  - Pane is disposed

**Scenario:**
1. Window activates
2. Check passes: paneManager.FocusedPane is not null
3. User presses Ctrl+Shift+Q to close focused pane
4. Dispatcher executes queued focus restoration
5. paneManager.FocusedPane is now null
6. FocusPane(null) is called - fails in line 130 check

**Risk Level:** HIGH - Causes crash in FocusPane() guard clause

---

### 2.4 Application.Current Null Coalescing (MEDIUM)

**Multiple Locations:**
- `NotesPane.cs:536, 648, 1529, 1585, etc.`
- `TaskListPane.cs` - Many uses
- `FileBrowserPane.cs` - Many uses

```csharp
Application.Current?.Dispatcher.Invoke(() => { ... });
```

**Issue:** Silently fails if Application.Current is null
- Could happen during shutdown
- Could happen in test environments
- No error indication

**Risk Level:** MEDIUM - Silent failures during shutdown or testing

---

## 3. STATE INCONSISTENCIES

### 3.1 FocusedPane vs IsFocused Desynchronization (HIGH)

**Location:** `PaneManager.cs:128-174` and `PaneBase.cs:202-210`

Two sources of truth for focus state:
1. `PaneManager.focusedPane` (line 24)
2. `PaneBase.IsFocused` (line 45)

```csharp
// PaneManager sets both:
focusedPane = pane;  // Line 142
focusedPane.SetActive(true);  // Line 143
focusedPane.IsFocused = true;  // Line 144
focusedPane.OnFocusChanged();  // Line 145
```

```csharp
// But pane can lose focus via WPF events without notifying PaneManager
// PaneBase has no LostFocus handler to update PaneManager!
```

**Issue:** PaneManager.focusedPane can desync from actual WPF keyboard focus
- WPF can move focus (e.g., via Tab key) without calling PaneManager.FocusPane()
- PaneManager doesn't subscribe to pane.LostFocus events
- User presses Tab → focus moves to different control → PaneManager is unaware

**Scenario:**
1. Pane A has focus (PaneManager.focusedPane = A, A.IsFocused = true)
2. User presses Tab
3. WPF moves focus to first focusable control in Pane B
4. PaneManager.focusedPane is still A
5. Navigate focus command uses stale data
6. Ctrl+Shift+Right tries to find pane to right of "A", not "B"

**Risk Level:** HIGH - Navigation becomes unpredictable

---

### 3.2 FocusHistoryManager Not Tracking Tab Navigation (HIGH)

**Location:** `FocusHistoryManager.cs:230-238`

```csharp
private void OnElementGotFocus(object sender, RoutedEventArgs e)
{
    if (!isTrackingEnabled) return;

    var element = e.OriginalSource as UIElement;
    if (element != null && ShouldTrackElement(element))
    {
        RecordFocus(element);
    }
}
```

**Issue:** Only tracks specific element types (line 246-257)
- TextBox ✓
- ListBox ✓
- Button ✓
- **But NOT custom controls or deep-nested controls**
- If pane contains nested controls, only top-level ones are tracked

**Example Problem:**
1. NotesPane has searchBox (TextBox)
2. User types in searchBox → focus tracked ✓
3. User presses Escape → focus moves to first focusable child (nested Label?) → NOT tracked ✗
4. Workspace switch → Tries to restore focus to "first focusable child" → fails ✗

**Risk Level:** MEDIUM - Workspace focus restoration becomes unreliable

---

### 3.3 FocusHistoryManager SaveWorkspaceState Missing Pane (MEDIUM)

**Location:** `FocusHistoryManager.cs:176-197`

```csharp
public Dictionary<string, object> SaveWorkspaceState()
{
    var state = new Dictionary<string, object>();

    foreach (var kvp in paneFocusMap)
    {
        if (kvp.Value.Element.IsAlive)
        {
            state[$"Focus_{kvp.Key}"] = new
            {
                ElementType = kvp.Value.ElementType,
                ControlState = kvp.Value.ControlState,
                Timestamp = kvp.Value.Timestamp
            };
        }
    }

    state["CurrentFocus"] = currentFocus?.PaneId;  // <-- What if CurrentFocus is not in paneFocusMap?
    // ...
}
```

**Issue:** currentFocus can reference pane not in paneFocusMap
- currentFocus set whenever focus changes (line 58)
- But if pane never had a control focused (Pane itself has focus), no entry in paneFocusMap
- RestoreWorkspaceState() tries to restore focus to pane, but no saved state exists

**Risk Level:** MEDIUM - Focus restoration falls back to default (first pane)

---

### 3.4 IsCommandPaletteVisible Flag Without Event (MEDIUM)

**Location:** `NotesPane.cs:55`

```csharp
private bool isCommandPaletteVisible;  // Line 55

private void ShowCommandPalette()
{
    isCommandPaletteVisible = true;
    commandPaletteBorder.Visibility = Visibility.Visible;
}

private void HideCommandPalette()
{
    isCommandPaletteVisible = false;
    commandPaletteBorder.Visibility = Visibility.Collapsed;
}
```

**Issue:** Flag can desync from actual visibility
- If HideCommandPalette() is called but Border.Visibility is already Hidden (e.g., by parent), flag doesn't match
- If Border is hidden externally (e.g., workspace switch disposes pane), flag remains true
- OnPaneGainedFocus() (line 1692) checks flag to restore focus to command input, but if flag is wrong, focus goes to wrong control

**Risk Level:** LOW-MEDIUM - Affects focus restoration logic

---

## 4. RESOURCE CLEANUP ISSUES

### 4.1 FocusHistoryManager EventManager Not Cleaned (MEDIUM-HIGH)

**Location:** `FocusHistoryManager.cs:32-38`

```csharp
// Hook into global focus changes
EventManager.RegisterClassHandler(typeof(UIElement),
    UIElement.GotFocusEvent,
    new RoutedEventHandler(OnElementGotFocus));

EventManager.RegisterClassHandler(typeof(UIElement),
    UIElement.LostFocusEvent,
    new RoutedEventHandler(OnElementLostFocus));
```

**Issue:** Event handlers registered but NEVER unregistered!
- FocusHistoryManager doesn't implement IDisposable
- Constructor registers class-level event handlers
- When FocusHistoryManager is disposed/replaced, handlers remain active
- Every UI element focus change still triggers these handlers for disposed manager
- Memory leak + potential crashes

**Risk Level:** HIGH - Memory leak, especially during development with hot reload

---

### 4.2 DispatcherTimer Not Stopped on Exception (MEDIUM)

**Location:** `NotesPane.cs:86-105`

```csharp
searchDebounceTimer = new DispatcherTimer
{
    Interval = TimeSpan.FromMilliseconds(SEARCH_DEBOUNCE_MS)
};
searchDebounceTimer.Tick += (s, e) =>
{
    searchDebounceTimer.Stop();
    FilterNotes();  // <-- What if FilterNotes() throws?
};
```

**Issue:** If FilterNotes() throws, timer.Stop() never called
- Timer continues firing (default: every 150ms)
- Each execution throws exception
- Repeated exceptions in logs
- Timer fires after pane is disposed

**Scenario:**
1. User searches with invalid input
2. FilterNotes() throws
3. Timer not stopped
4. Timer fires every 150ms
5. Repeated exceptions
6. User closes NotesPane
7. Timer still exists in memory, still firing on disposed pane → crash

**Risk Level:** MEDIUM - Cascading failures

---

### 4.3 FileSystemWatcher Not Disposed on Error (MEDIUM)

**Location:** `NotesPane.cs:1494-1523`

```csharp
private void SetupFileWatcher()
{
    if (!Directory.Exists(currentNotesFolder)) return;

    try
    {
        fileWatcher?.Dispose();

        fileWatcher = new FileSystemWatcher(currentNotesFolder)
        {
            // ...
            EnableRaisingEvents = true
        };
        // ...
    }
    catch (Exception ex)
    {
        ErrorHandlingPolicy.Handle(
            ErrorCategory.Internal,
            ex,
            $"Setting up file watcher for '{currentNotesFolder}'",
            logger);
        // <-- fileWatcher might be partially initialized!
    }
}
```

**Issue:** If exception occurs during setup, fileWatcher is left in inconsistent state
- May be non-null but not properly initialized
- OnDispose() tries to dispose it (line 2071): `fileWatcher.Dispose()`
- If already disposed or half-initialized, could throw

**Risk Level:** MEDIUM - Cascading disposal failures

---

### 4.4 CommandPalette Animation Callback Not Cleaned (MEDIUM)

**Location:** `MainWindow.xaml.cs:584-598`

```csharp
commandPalette.AnimateClose(() =>
{
    Dispatcher.Invoke(() =>
    {
        ModalOverlay.Visibility = Visibility.Collapsed;
        ModalOverlay.Children.Clear();

        // CRITICAL FIX: Return focus to the previously focused pane
        if (paneManager?.FocusedPane != null)
        {
            logger.Log(LogLevel.Debug, "MainWindow", 
                $"Returning focus to {paneManager.FocusedPane.PaneName}...");
            paneManager.FocusPane(paneManager.FocusedPane);
        }
    });
});
```

**Issue:** Callback captures `this` (MainWindow) - but what if MainWindow is closed?
- AnimateClose() stores callback in command palette
- CommandPalette is not disposed until animation completes
- If MainWindow closes during animation, callback still tries to access Dispatcher
- Null reference or use-after-dispose

**Risk Level:** LOW-MEDIUM - Requires specific timing (close while animating)

---

## 5. INPUT EDGE CASES

### 5.1 Rapid Key Presses - Focus Restoration Queue Overflow (MEDIUM)

**Location:** Multiple Dispatcher.BeginInvoke() calls in focus restoration paths

**Scenario:**
1. User mashes Ctrl+Shift+T, Ctrl+Shift+N rapidly (opens multiple panes)
2. Each OpenPane() calls FocusPane()
3. Each FocusPane() queues focus restoration at Input priority
4. Dispatcher queue fills up with hundreds of focus commands
5. UI becomes sluggish
6. Wrong pane gets final focus (last queued command wins)

**Risk Level:** MEDIUM - Performance issue, incorrect focus

---

### 5.2 Auto-Repeat Keys During Input Parsing (MEDIUM)

**Location:** `SmartInputParser.cs` - vulnerable to rapid input

```csharp
// "in N days/weeks/months"
var inMatch = Regex.Match(input, @"^in\s+(\d+)\s+(day|week|month|year)s?$");
if (inMatch.Success)
{
    var amount = int.Parse(inMatch.Groups[1].Value);  // <-- No bounds check!
    // ...
}
```

**Issue:** int.Parse() doesn't validate range
- User holds "5" key → input becomes "55555555555555555555"
- int.Parse() succeeds (int max is ~2.1B)
- AddMonths(2147483647) might throw OverflowException

**Risk Level:** MEDIUM - Unhandled exception

---

### 5.3 Focus Lost During Input Processing (MEDIUM)

**Location:** NotesPane, TaskListPane, FileBrowserPane - KeyDown handlers

```csharp
// In OnPreviewKeyDown:
if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
{
    switch (e.Key)
    {
        case Key.A:
            CreateNewNote();  // <-- What if this takes time?
            e.Handled = true;
            break;
    }
}
```

**Issue:** Long operation during key handling blocks UI
- CreateNewNote() might open dialog
- User's keyboard focus is in limbo
- If operation fails, focus state unclear

**Risk Level:** LOW - Better to use async, but not critical

---

### 5.4 Input During Async Operations (HIGH)

**Location:** `NotesPane.cs:596-604`

```csharp
private async void OnNoteSelected(object sender, SelectionChangedEventArgs e)
{
    if (isLoadingNote) return;  // <-- Flag prevents re-entrance

    if (notesListBox.SelectedItem is ListBoxItem item && item.Tag is NoteMetadata note)
    {
        await LoadNoteAsync(note);  // <-- What if user clicks another note while loading?
    }
}
```

**Issue:** Flag-based re-entrance protection, but race condition possible
- Line 598: Check isLoadingNote
- Line 602: Start LoadNoteAsync()
- User clicks another note
- New call checks isLoadingNote (still false, async hasn't set it yet)
- Both loads run concurrently
- EditorText gets overwritten mid-edit

**Scenario:**
1. Click Note A (loading starts)
2. Immediately click Note B (while A still loading)
3. isLoadingNote flag is false (A's async hasn't set it)
4. B's LoadNoteAsync starts
5. Both A and B load concurrently
6. Editor text gets corrupted with mixed content

**Risk Level:** MEDIUM - Data corruption possible

---

## 6. ERROR HANDLING GAPS

### 6.1 Focus Operations Without Exception Handling (MEDIUM)

**Location:** `FocusHistoryManager.cs:104-130`

```csharp
public bool RestorePaneFocus(string paneId)
{
    var element = GetLastFocusedControl(paneId);
    if (element == null) return false;

    try
    {
        element.Focus();  // <-- What exceptions can this throw?
        Keyboard.Focus(element);  // <-- What exceptions can this throw?

        // Restore control state if possible
        if (paneFocusMap.TryGetValue(paneId, out var record))
        {
            RestoreControlState(element, record.ControlState);
        }

        logger.Log(LogLevel.Debug, "FocusHistory",
            $"Focus restored to {element.GetType().Name} in {paneId}");
        return true;
    }
    catch (Exception ex)
    {
        logger.Log(LogLevel.Warning, "FocusHistory",
            $"Failed to restore focus: {ex.Message}");
        return false;
    }
}
```

**Good exception handling here, but...**

---

### 6.2 FocusPane Called Without Null Check (Caller Side) (MEDIUM)

**Location:** `MainWindow.xaml.cs:622`, `PaneManager.cs:187`, multiple other places

```csharp
paneManager.FocusPane(paneManager.FocusedPane);  // <-- paneManager could be null!
```

**Issue:** Callers don't null-check before calling FocusPane()
- paneManager could be disposed
- paneManager could be null during shutdown

**Risk Level:** MEDIUM - NullReferenceException

---

### 6.3 Keyboard.Focus() No Null Check (MEDIUM)

**Location:** `FocusHistoryManager.cs:149`, `PaneManager.cs:162`, `NotesPane.cs:1699`

```csharp
Keyboard.Focus(element);  // <-- What if element is null?
```

**Issue:** Keyboard.Focus(null) throws ArgumentNullException
- Can be null if weak reference collected between checks
- Can be null if returned from Focus() path without validation

**Risk Level:** MEDIUM - Unhandled exception

---

## 7. FOCUS DURING SPECIAL SCENARIOS

### 7.1 Focus During Pane Disposal (HIGH)

**Location:** `PaneManager.cs:76-100`

```csharp
public void ClosePane(PaneBase pane)
{
    if (pane == null || !openPanes.Contains(pane))
        return;

    // Remove from tiling engine (auto-reflows)
    tilingEngine.RemoveChild(pane);
    openPanes.Remove(pane);

    // Dispose pane
    pane.Dispose();  // <-- Line 86: Dispose happens here

    // Focus next pane if this was focused
    if (focusedPane == pane)  // <-- Line 89: But check happens AFTER disposal!
    {
        focusedPane = null;
        if (openPanes.Count > 0)
        {
            FocusPane(openPanes[0]);  // <-- Focus restoration after disposal!
        }
    }
}
```

**Issue:** Focus restoration happens after pane disposal
- If disposed pane has pending focus restoration in dispatcher queue, it executes on disposed pane
- OnPaneGainedFocus() or other handlers might try to access disposed resources

**Scenario:**
1. NotesPane has pending "restore focus to editor" in dispatcher queue
2. User presses Ctrl+Shift+Q to close NotesPane
3. ClosePane() → Dispose() → Dispatcher clears pending actions? (It doesn't!)
4. Focus restoration executes on disposed pane
5. Accessing disposed noteEditor control → NullReferenceException

**Risk Level:** HIGH - Crash after pane closure

---

### 7.2 Focus Lost During Theme Change (MEDIUM)

**Location:** `PaneBase.cs:161-167`

```csharp
private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
{
    // Re-apply theme on theme change
    ApplyTheme();  // <-- Recreates UI elements?

    // Notify subclasses of theme change
    OnThemeChangedOverride(e);
}
```

**Issue:** If theme application recreates UI, focus might be lost
- ApplyTheme() at line 258 modifies borders/brushes
- If dynamic theme update recreates elements, old focused element is gone
- Focus needs to be restored but code doesn't do this

**Risk Level:** LOW-MEDIUM - Depends on theme implementation

---

### 7.3 Focus During Window Deactivation (MEDIUM)

**Location:** `MainWindow.xaml.cs:630-634`

```csharp
private void MainWindow_Deactivated(object sender, EventArgs e)
{
    // Log but don't change anything - we'll restore focus when activated
    logger.Log(LogLevel.Debug, "MainWindow", "Window deactivated");
}
```

**Issue:** Does NOT save focus state on deactivation
- Focus context is lost when window loses activation
- On reactivation, tries to restore from FocusHistoryManager
- But FocusHistoryManager might have recorded focus changes in other apps

**Scenario:**
1. NotesPane has focus
2. Alt+Tab to another app
3. MainWindow_Deactivated() - but focus isn't saved
4. Alt+Tab back
5. MainWindow_Activated() tries to restore focus
6. FocusHistoryManager has stale data from app-switching
7. Focus restored incorrectly

**Risk Level:** MEDIUM - Focus confusion during app switching

---

### 7.4 Focus With Disabled Controls (LOW-MEDIUM)

**Location:** `NotesPane.cs:259, 652-653`

```csharp
noteEditor = new TextBox
{
    // ...
    IsEnabled = false  // Line 263
};

// Later in LoadNoteAsync:
Application.Current?.Dispatcher.Invoke(() =>
{
    currentNote = note;
    noteEditor.Text = content;
    noteEditor.IsEnabled = true;  // Line 652
    noteEditor.IsReadOnly = false;
    hasUnsavedChanges = false;
    UpdateStatusBar();
});
```

**Issue:** If focus is set to disabled control, focus moves to next focusable
- No explicit focus restoration after enabling
- User might be confused where focus goes

**Risk Level:** LOW - UX issue more than crash

---

## SUMMARY: RISK MATRIX

| Category | Risk Level | Severity | Likelihood | Issue Count |
|----------|-----------|----------|-----------|------------|
| Race Conditions | HIGH | Crash/Data Corrupt | Medium | 4 |
| Null References | MEDIUM | Crash | High | 4 |
| State Desync | HIGH | Wrong Behavior | Medium | 4 |
| Resource Leaks | MEDIUM | Memory/Crash | High | 4 |
| Input Edge Cases | MEDIUM | Crash/Corrupt | Low-Med | 4 |
| Error Handling | MEDIUM | Crash | Medium | 3 |
| Special Scenarios | HIGH | Crash | Medium | 4 |
| **TOTAL** | | | | **27 Issues** |

---

## CRITICAL PRIORITIES (Must Fix)

1. **FocusHistoryManager EventManager cleanup** - Memory leak
2. **Focus restoration on null focusedPane** - Crash risk
3. **Race condition: focus during workspace switch** - Silent failures
4. **Weak reference TOCTOU race** - Garbage collection timing bug
5. **Pane focus during disposal** - Use-after-free crashes

---

## RECOMMENDATIONS

1. **Add try-catch around all focus operations**
2. **Use single source of truth for focus state** (remove IsFocused duplication)
3. **Implement proper IDisposable in FocusHistoryManager**
4. **Add null checks in all deferred focus restoration calls**
5. **Synchronize focus state with WPF events** (subscribe to LostFocus)
6. **Stop pending dispatcher actions when pane closes**
7. **Add focus state validation in assertions/debug mode**

