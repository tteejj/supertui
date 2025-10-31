# SuperTUI Pane Navigation & Focus System - Comprehensive Analysis

## Executive Summary

The SuperTUI pane navigation and focus system is **well-architected with solid fundamentals** but has several **critical edge cases and potential dead-ends** that require attention. The navigation model is based on i3-style directional movement, but the implementation has gaps in handling edge cases, modal interactions, and wraparound behavior.

---

## 1. PANE NAVIGATION LOGIC

### 1.1 Navigation Flow (Ctrl+Shift+Arrows)

**File**: `/home/teej/supertui/WPF/MainWindow.xaml.cs`

```csharp
// Keyboard shortcut registration (lines 149-165):
shortcuts.RegisterGlobal(Key.Left, ModifierKeys.Control | ModifierKeys.Shift,
    () => paneManager.NavigateFocus(FocusDirection.Left),
    "Focus pane left");
// ... similar for Right, Up, Down
```

**File**: `/home/teej/supertui/WPF/Core/Infrastructure/PaneManager.cs`

```csharp
public void NavigateFocus(FocusDirection direction)
{
    if (focusedPane == null || openPanes.Count <= 1)
        return;  // EDGE CASE 1: No navigation if <= 1 pane

    var targetPane = tilingEngine.FindWidgetInDirection(focusedPane, direction) as PaneBase;
    if (targetPane != null)
    {
        FocusPane(targetPane);
        logger.Log(LogLevel.Debug, "PaneManager", $"Focus moved {direction} to {targetPane.PaneName}");
    }
    // EDGE CASE 2: Silent failure if no pane found in direction
}
```

### 1.2 Directional Finding Algorithm

**File**: `/home/teej/supertui/WPF/Core/Layout/TilingLayoutEngine.cs` (lines 465-517)

**Algorithm**:
```csharp
public UIElement FindWidgetInDirection(UIElement fromWidget, FocusDirection direction)
{
    // 1. Iterate all widgets (O(n))
    // 2. Calculate center points of each widget
    // 3. Filter by direction (e.g., for Left: toCenterCol < fromCenterCol)
    // 4. Calculate weighted distance: 
    //    - Primary: distance in direction (e.g., column distance for Left)
    //    - Secondary: perpendicular distance * 0.5 (e.g., row distance)
    // 5. Return widget with lowest weighted distance
    
    double distance = (fromCenterCol - toCenterCol) + Math.Abs(fromCenterRow - toCenterRow) * 0.5;
}
```

**Key Properties**:
- Uses grid position tracking (`widgetPositions` dictionary)
- Weighted distance metric (primary direction is more important)
- Returns `null` if no widget found in direction

### 1.3 Grid Position Tracking

**Problem**: Widget positions are recomputed on every `Relayout()` call, which occurs:
- On pane creation
- On pane destruction
- When tiling mode changes
- When layout mode auto-switches based on pane count

**This is correct behavior** - positions must track the current layout.

### 1.4 Tiling Layout Modes

The system supports 5 modes (defined in `TilingLayoutEngine.cs`):

```
Auto (intelligent selection):
  1 pane   → Grid (fullscreen)
  2 panes  → Tall (side-by-side)
  3-4      → Grid (2x2)
  5+       → MasterStack (main + stack)

Manual modes:
  MasterStack: Main (60%) left, Stack (40%) right
  Wide:        All stacked vertically
  Tall:        All arranged horizontally
  Grid:        NxN grid layout
```

**Navigation Impact**: Different layouts have different widget connectivity patterns:
- **Wide/Tall**: Linear navigation (only Up/Down or Left/Right works well)
- **Grid**: Allows navigation in all 4 directions
- **MasterStack**: Left/Right changes main↔stack; Up/Down navigates within stack

---

## 2. FOCUS TRANSFER MECHANISMS

### 2.1 Focus Transfer Flow

**File**: `/home/teej/supertui/WPF/Core/Infrastructure/PaneManager.cs` (lines 128-174)

```csharp
public void FocusPane(PaneBase pane)
{
    // 1. Validate pane exists
    if (pane == null || !openPanes.Contains(pane))
        return;

    // 2. Unfocus previous
    if (focusedPane != null && focusedPane != pane)
    {
        focusedPane.SetActive(false);      // Sets IsActive=false
        focusedPane.IsFocused = false;      // Sets IsFocused=false
        focusedPane.OnFocusChanged();       // Triggers visual update
    }

    // 3. Focus new
    focusedPane = pane;
    pane.SetActive(true);                   // Sets IsActive=true
    pane.IsFocused = true;                  // Sets IsFocused=true
    pane.OnFocusChanged();                  // Triggers visual update

    // 4. CRITICAL: Set WPF keyboard focus
    if (!pane.IsFocused)  // Redundant check?
    {
        pane.Focus();  // Request logical focus
    }

    // 5. Asynchronous focus enforcement (delayed to dispatch priority)
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
                // Focus first focusable child
                pane.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
        }
    }), DispatcherPriority.Input);

    PaneFocusChanged?.Invoke(this, new PaneEventArgs(pane));
}
```

### 2.2 Pane Focus Lifecycle

**File**: `/home/teej/supertui/WPF/Core/Components/PaneBase.cs`

```csharp
// When pane gains focus
protected virtual void OnPaneGainedFocus()
{
    // Default behavior: move focus to first focusable child
    this.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
}

// When pane loses focus
protected virtual void OnPaneLostFocus()
{
    // Default: do nothing
}

// When pane's active state changes
protected virtual void OnActiveChanged(bool isActive)
{
    if (isActive)
        OnPaneGainedFocus();
    else
        OnPaneLostFocus();
}
```

### 2.3 Visual Feedback on Focus Change

**File**: `/home/teej/supertui/WPF/Core/Components/PaneBase.cs` (lines 258-300)

```csharp
public void ApplyTheme()
{
    // When focused:
    containerBorder.BorderThickness = new Thickness(3);  // 3px vs 1px
    containerBorder.BorderBrush = theme.BorderActive;
    headerBorder.Background = theme.BorderActive;
    
    // Add drop shadow when focused
    if (IsFocused)
    {
        containerBorder.Effect = new DropShadowEffect
        {
            BlurRadius = 12,
            Opacity = 0.8
        };
    }
}
```

---

## 3. WORKSPACE SWITCHING

### 3.1 Workspace Management

**File**: `/home/teej/supertui/WPF/Core/Infrastructure/WorkspaceState.cs`

```csharp
public class PaneWorkspaceManager
{
    private List<PaneWorkspaceState> workspaceStates;
    private int currentWorkspaceIndex;

    public void SwitchToWorkspace(int index)
    {
        var oldIndex = currentWorkspaceIndex;
        currentWorkspaceIndex = index;
        WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs(oldIndex, index));
    }
}

public class PaneWorkspaceState
{
    public List<string> OpenPaneTypes { get; set; }     // Pane types to recreate
    public int FocusedPaneIndex { get; set; }           // Which pane was focused
    public Dictionary<string, object> FocusState { get; set; }  // Internal focus history
    public Guid? CurrentProjectId { get; set; }         // Project context
}
```

### 3.2 Focus Restoration on Workspace Switch

**File**: `/home/teej/supertui/WPF/MainWindow.xaml.cs` (lines 336-404)

```csharp
private void RestoreWorkspaceState()
{
    var state = workspaceManager.CurrentWorkspace;

    // 1. Close all panes
    paneManager.CloseAll();

    // 2. Restore project context
    if (state.CurrentProjectId.HasValue)
    {
        projectContext.SetProject(project);
    }

    // 3. Recreate panes from stored types
    var panesToRestore = new List<PaneBase>();
    foreach (var paneTypeName in state.OpenPaneTypes)
    {
        try
        {
            var pane = paneFactory.CreatePane(paneTypeName);
            panesToRestore.Add(pane);
        }
        catch { /* log warning */ }
    }

    // 4. Restore pane manager state
    var paneState = new PaneManagerState
    {
        OpenPaneTypes = state.OpenPaneTypes,
        FocusedPaneIndex = state.FocusedPaneIndex
    };
    paneManager.RestoreState(paneState, panesToRestore);

    // 5. Restore focus history
    if (state.FocusState != null)
    {
        focusHistory.RestoreWorkspaceState(state.FocusState);
        
        // CRITICAL: Restore focus to previously focused pane
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (paneManager.FocusedPane != null)
            {
                var paneName = paneManager.FocusedPane.PaneName;
                focusHistory.RestorePaneFocus(paneName);  // Restore to specific control in pane
            }
        }), DispatcherPriority.Loaded);
    }
}
```

### 3.3 Focus History Management

**File**: `/home/teej/supertui/WPF/Core/Infrastructure/FocusHistoryManager.cs`

The `FocusHistoryManager` maintains:
- **Global focus stack** (Last 50 focus changes)
- **Per-pane focus map** (Last focused control in each pane)
- **Control state** (TextBox caret position, ListBox scroll position, etc.)

```csharp
public void RecordFocus(UIElement element, string paneId = null)
{
    var record = new FocusRecord
    {
        Element = new WeakReference(element),  // Weak ref (garbage collection safe)
        ElementType = element.GetType().Name,
        PaneId = paneId,
        ControlState = CaptureControlState(element)  // Save state
    };
    
    // Add to stack and per-pane map
    focusHistory.Push(record);
    paneFocusMap[paneId] = record;
}

public bool RestorePaneFocus(string paneId)
{
    var element = GetLastFocusedControl(paneId);
    if (element == null) return false;
    
    element.Focus();
    Keyboard.Focus(element);
    
    // Restore control state (text, selection, scroll position)
    RestoreControlState(element, record.ControlState);
    return true;
}
```

---

## 4. PANE LIFECYCLE

### 4.1 Pane Opening

**File**: `/home/teej/supertui/WPF/Core/Infrastructure/PaneManager.cs` (lines 47-71)

```csharp
public void OpenPane(PaneBase pane)
{
    // 1. Prevent duplicates
    if (openPanes.Contains(pane))
    {
        FocusPane(pane);  // Just focus existing
        return;
    }

    // 2. Initialize pane
    pane.Initialize();

    // 3. Add to tiling engine (auto-tiles based on layout mode)
    tilingEngine.AddChild(pane, new LayoutParams());
    openPanes.Add(pane);

    // 4. Set as focused
    FocusPane(pane);

    PaneOpened?.Invoke(this, new PaneEventArgs(pane));
}
```

### 4.2 Pane Closing

**File**: `/home/teej/supertui/WPF/Core/Infrastructure/PaneManager.cs` (lines 76-100)

```csharp
public void ClosePane(PaneBase pane)
{
    if (pane == null || !openPanes.Contains(pane))
        return;

    // 1. Remove from layout
    tilingEngine.RemoveChild(pane);
    openPanes.Remove(pane);

    // 2. Dispose pane
    pane.Dispose();

    // 3. Handle focus transfer
    if (focusedPane == pane)  // ✓ Correct check
    {
        focusedPane = null;
        if (openPanes.Count > 0)
        {
            FocusPane(openPanes[0]);  // Focus first remaining pane
        }
    }

    PaneClosed?.Invoke(this, new PaneEventArgs(pane));
}
```

**Focus Transfer on Close**:
- If closed pane was NOT focused → no focus transfer (correct)
- If closed pane WAS focused → focus `openPanes[0]` (always first pane)

**ISSUE 1: Deterministic but arbitrary focus**
- The focus goes to the first pane in the list, which may not be spatially adjacent
- Could be improved: find nearest adjacent pane in direction

### 4.3 Pane Initialization Order

When opening a pane:
1. `Initialize()` - called by PaneManager
2. `BuildContent()` - subclass builds UI
3. `ApplyTheme()` - colors applied
4. `Loaded` event - pane added to visual tree
5. `FocusPane()` - focus transferred

**ISSUE 2: Race condition potential**
- `FocusPane()` uses `Dispatcher.BeginInvoke()` with `DispatcherPriority.Input`
- But pane may not be fully rendered yet
- Mitigated by: priority queue ensures it runs after layout pass

---

## 5. EDGE CASES & DEAD-ENDS

### 5.1 Wraparound Behavior (MISSING)

**Current behavior**: Navigation stops at edges
```
Example: 2 panes side-by-side
[Pane A] [Pane B]
  ↑        ↑
 In A, pressing Ctrl+Shift+Left → NO NAVIGATION (returns null from FindWidgetInDirection)
 In B, pressing Ctrl+Shift+Right → NO NAVIGATION
```

**Expected in i3**: Wraparound to opposite pane
**Actual in SuperTUI**: Silent failure (debug log only)

### 5.2 No Navigation Available (POTENTIAL DEAD-END)

**Scenarios where navigation silently fails**:

1. **In Wide layout** (vertically stacked):
   - Pressing Left/Right has no effect (no widgets to left/right)
   
2. **In Tall layout** (horizontally arranged):
   - Pressing Up/Down has no effect (no widgets above/below)

3. **In Master-Stack with 2 panes**:
   - Can navigate main ↔ stack (Left/Right)
   - Cannot navigate up/down within main
   - Only stack widgets can navigate up/down

4. **With 1 pane**: Navigation does nothing (caught by `openPanes.Count <= 1`)

**User Experience**: No visual feedback, no error message, user doesn't know navigation is impossible

### 5.3 Non-Focusable Pane (POTENTIAL CRASH)

If a pane has `Focusable = false`:

```csharp
public void FocusPane(PaneBase pane)
{
    // ... SetActive, SetIsFocused ...
    
    pane.Dispatcher.BeginInvoke(new Action(() =>
    {
        if (!pane.IsKeyboardFocusWithin)
        {
            if (pane.Focusable && pane.Focus())  // ✓ Guards against non-focusable
            {
                Keyboard.Focus(pane);
            }
            else
            {
                pane.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));  // Fall back to children
            }
        }
    }), DispatcherPriority.Input);
}
```

**Good**: Code guards against non-focusable panes by attempting child focus
**Issue**: If pane has NO focusable children, focus is lost (WPF default)

### 5.4 Invisible/Collapsed Panes (NOT HANDLED)

The navigation system has no check for `Visibility` or `IsEnabled`:

```csharp
public UIElement FindWidgetInDirection(UIElement fromWidget, FocusDirection direction)
{
    foreach (var kvp in widgetPositions)
    {
        if (kvp.Key == fromWidget) continue;
        
        // NO CHECK FOR: Visibility.Collapsed, IsEnabled, or Focusable
        
        // Include in navigation even if invisible/disabled
        var toPos = kvp.Value;
        // ... calculate distance ...
    }
}
```

**Consequence**: If you navigate to an invisible pane, focus is transferred to UI that can't be seen or interacted with

### 5.5 Focus Loops (PREVENTED)

The `FocusPane()` method prevents infinite loops:
```csharp
if (focusedPane != null && focusedPane != pane)  // Guard against same pane
{
    // Unfocus previous
}
```
✓ **Good**: Prevents double-triggering focus events

### 5.6 Window Deactivation/Reactivation

**File**: `/home/teej/supertui/WPF/MainWindow.xaml.cs` (lines 612-625)

```csharp
private void MainWindow_Activated(object sender, EventArgs e)
{
    // When window gets focus (Alt+Tab back, click on window)
    if (paneManager?.FocusedPane != null)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            paneManager.FocusPane(paneManager.FocusedPane);
        }), DispatcherPriority.Input);
    }
}
```

✓ **Good**: Restores keyboard focus when window reactivates
✓ **Good**: Uses deferred execution to ensure pane is ready

---

## 6. MODAL INTERACTION

### 6.1 Command Palette Modal Behavior

**File**: `/home/teej/supertui/MainWindow.xaml.cs` (lines 541-602)

When Command Palette opens:
1. Palette is added to `ModalOverlay`
2. Palette gets focus
3. Navigation shortcuts (Ctrl+Shift+Arrows) still registered BUT:
   - `ShortcutManager.HandleKeyPress()` checks for typing context
   - If palette's search box has focus → no navigation (correct)

When Command Palette closes:
```csharp
private void HideCommandPalette()
{
    commandPalette.AnimateClose(() =>
    {
        Dispatcher.Invoke(() =>
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            ModalOverlay.Children.Clear();
            
            // CRITICAL FIX: Return focus to previously focused pane
            if (paneManager?.FocusedPane != null)
            {
                paneManager.FocusPane(paneManager.FocusedPane);
            }
        });
    });
}
```

✓ **Good**: Focus is explicitly restored after modal closes

### 6.2 Keyboard Context Awareness

**File**: `/home/teej/supertui/WPF/Core/Infrastructure/ShortcutManager.cs` (lines 49-132)

The `ShortcutManager` prevents navigation shortcuts while typing:

```csharp
public bool HandleKeyDown(Key key, ModifierKeys modifiers, string currentWorkspace)
{
    // Check if user is typing in text input control
    if (IsTypingInTextInput() && !IsAllowedWhileTyping(key, modifiers))
    {
        return false;  // Block shortcut
    }
    
    // Process shortcuts...
}

private bool IsTypingInTextInput()
{
    var focused = Keyboard.FocusedElement;
    return focused is TextBox ||
           focused is RichTextBox ||
           focused is PasswordBox;
}

private bool IsAllowedWhileTyping(Key key, ModifierKeys modifiers)
{
    // Ctrl+Z/Y/S/X/C/V/A allowed while typing
    // Escape allowed while typing
    // Everything else blocked
    return (modifiers == ModifierKeys.Control && key is Key.Z/Y/S/X/C/V/A) ||
           (key == Key.Escape);
}
```

✓ **Good**: Prevents Ctrl+Shift+Arrows while editing text fields
✓ **Good**: Allows Ctrl+Z (Undo) to work during editing

### 6.3 Move Pane Mode (F12)

**File**: `/home/teej/supertui/WPF/MainWindow.xaml.cs` (lines 469-517)

When F12 toggles move mode:
```csharp
private void MainWindow_KeyDown(object sender, KeyEventArgs e)
{
    // ... Let ShortcutManager handle registered shortcuts ...
    
    // Context-specific shortcuts (can't be pre-registered):
    if (isMovePaneMode && e.KeyboardDevice.Modifiers == ModifierKeys.None)
    {
        switch (e.Key)
        {
            case Key.Left:
                paneManager.MovePane(FocusDirection.Left);  // Swap pane positions
                return;
            case Key.Right/Up/Down: // Similar
            case Key.Escape:
                isMovePaneMode = false;
                return;
        }
    }
}
```

**Behavior**: 
- Normal Ctrl+Shift+Arrows → Navigate focus
- F12 → Enable move mode
- Arrow keys alone → Swap panes (no modifiers)
- Esc → Disable move mode

✓ **Good**: Clear mode switching
✓ **Issue**: No visual cue that navigation vs move modes have different semantics

---

## 7. DETAILED NAVIGATION ALGORITHM ANALYSIS

### 7.1 Distance Calculation (TilingLayoutEngine.cs:489-514)

For each direction, algorithm:

1. **LEFT**: 
   - Filter: `toCenterCol < fromCenterCol` (target left of source)
   - Distance: `(fromCenterCol - toCenterCol) + Abs(fromCenterRow - toCenterRow) * 0.5`
   - Best match: closest in column distance, tiebreak by row

2. **RIGHT**: 
   - Filter: `toCenterCol > fromCenterCol` (target right of source)
   - Distance: `(toCenterCol - fromCenterCol) + Abs(fromCenterRow - toCenterRow) * 0.5`

3. **UP**:
   - Filter: `toCenterRow < fromCenterRow` (target above source)
   - Distance: `(fromCenterRow - toCenterRow) + Abs(fromCenterCol - toCenterCol) * 0.5`

4. **DOWN**:
   - Filter: `toCenterRow > fromCenterRow` (target below source)
   - Distance: `(toCenterRow - fromCenterRow) + Abs(fromCenterCol - toCenterCol) * 0.5`

### 7.2 Algorithm Strengths

✓ **Geometric accuracy**: Uses center points (accounts for pane size)
✓ **Weighted metric**: Direction distance is 2x more important than perpendicular distance
✓ **Directional filtering**: Only considers panes in the intended direction
✓ **Handles grid layouts**: Works with 2x2, 3x3, or irregular grids

### 7.3 Algorithm Weaknesses

1. **No visibility check**: Includes invisible panes
2. **No focusability check**: Can navigate to non-focusable panes
3. **No wraparound**: Returns `null` at edges
4. **Perpendicular bias unclear**: Why 0.5 weight? Seems arbitrary

---

## 8. POTENTIAL NAVIGATION DEAD-ENDS SUMMARY

### Critical Issues

| Issue | Severity | Scenario | Impact |
|-------|----------|----------|--------|
| **No wraparound** | HIGH | User at edge pane navigates outward | Silent failure, no feedback |
| **Layout-specific navigation limits** | HIGH | Wide/Tall layouts with partial directions | Navigation disabled for 2+ directions |
| **Invisible pane focus** | HIGH | Pane with Visibility.Collapsed gets focused | Focus transferred to hidden UI |
| **No focusable children fallback** | MEDIUM | Pane with no focusable children | Focus lost (WPF default) |
| **Arbitrary focus on close** | MEDIUM | Close focused pane with multiple options | Focus goes to openPanes[0], not nearest |
| **No error feedback** | MEDIUM | Navigation fails silently | User doesn't know navigation is blocked |

### Design Questions

1. **Should navigation wrap around?**
   - Current: No (returns null, does nothing)
   - i3 style: Yes (wraps to opposite edge)
   - Vim style: No (stays at edge)

2. **Should all directions work in all layouts?**
   - Current: No (only available directions in current layout)
   - Alternative: Rotate layout to enable requested direction?

3. **Should closed pane focus to nearest?**
   - Current: No (always first pane)
   - Alternative: Find spatially adjacent pane?

---

## 9. FOCUS RESTORATION FLOW (Summary)

### Workspace Switch Sequence

```
User presses Ctrl+1 (switch workspace)
    ↓
PaneWorkspaceManager.SwitchToWorkspace(0)
    ↓
MainWindow.OnWorkspaceChanged()
    ↓
SaveCurrentWorkspaceState()
    ├─ paneManager.GetState() → {OpenPaneTypes, FocusedPaneIndex}
    ├─ focusHistory.SaveWorkspaceState() → {Focus_PaneName, ControlState}
    └─ projectContext current project
    ↓
RestoreWorkspaceState()
    ├─ paneManager.CloseAll()
    ├─ Recreate panes from OpenPaneTypes
    ├─ paneManager.RestoreState() → sets focusedPane
    ├─ focusHistory.RestoreWorkspaceState()
    └─ focusHistory.RestorePaneFocus(paneName)
        ├─ GetLastFocusedControl(paneName)
        ├─ element.Focus()
        └─ RestoreControlState() → caret/scroll position
```

### Focus Consistency Mechanisms

1. **PaneManager.focusedPane**: Tracks which pane should have focus
2. **FocusHistoryManager.paneFocusMap**: Maps pane → last focused control
3. **WPF's Keyboard.FocusedElement**: Actual keyboard focus
4. **Dispatcher.BeginInvoke()**: Deferred execution ensures safe timing

**Consistency**: Maintained via event sequence and deferred dispatch

---

## 10. RECOMMENDATIONS

### High Priority (Navigation Dead-Ends)

1. **Add wraparound navigation** OR document that it's not supported
2. **Add visibility/focusability checks** to FindWidgetInDirection()
3. **Add user feedback** when navigation fails (status message or beep)
4. **Improve focus transfer on pane close** (find nearest adjacent pane)

### Medium Priority (Robustness)

1. **Add navigation tests** for all layout modes
2. **Document layout-specific navigation limitations**
3. **Add fallback behavior** for non-focusable panes
4. **Test edge case**: All panes closed, then open first pane

### Low Priority (UX Refinement)

1. **Consider directional focus history** (remember which direction user came from)
2. **Visual feedback on navigation failure** (highlight attempted direction)
3. **Keyboard overlay** showing available navigation directions
4. **Configurable distance weights** (currently hardcoded 0.5)

---

## 11. CODE QUALITY ASSESSMENT

### Strengths
✓ Clear separation: PaneManager (focus logic) vs TilingLayoutEngine (geometry)
✓ Proper null checking in critical paths
✓ Asynchronous focus setting prevents race conditions
✓ Event-driven architecture (PaneFocusChanged, PaneOpened, etc.)
✓ Weak references in focus history prevent memory leaks

### Concerns
⚠️ Silent failure on no navigation target (debug log only)
⚠️ No validation of layout consistency
⚠️ Focus restoration happens in multiple places (PaneManager, FocusHistoryManager, MainWindow)
⚠️ Hard-coded magic numbers (0.5 weight, 50 history limit)
⚠️ Limited error handling in navigation code

---

## 12. Testing Recommendations

### Test Scenarios

```
1. Single pane:
   - Navigate in all 4 directions → should not crash
   - Expected: No-op or feedback

2. Two panes (side-by-side):
   - Navigate Left/Right → works
   - Navigate Up/Down → should not work
   - Expected: No navigation or feedback

3. Grid layout (2x2):
   - Navigate in all 4 directions → should work
   - From corners vs edges vs center → different results
   - Expected: Correct pane selection

4. Modal interaction:
   - Open command palette
   - Try to navigate with Ctrl+Shift+Arrows → should not work
   - Close palette
   - Navigate again → should work
   - Expected: Focus restored correctly

5. Edge detection:
   - Pane at grid edge, navigate away → should fail gracefully
   - Pane with no focusable children → should not crash
   - Invisible pane in layout → should not focus

6. Workspace persistence:
   - Create layout in workspace 1
   - Switch to workspace 2
   - Return to workspace 1
   - Expected: Same layout, same focus position
```

---

## Conclusion

The SuperTUI navigation system is **architecturally sound** with **well-structured components**, but **lacks comprehensive edge case handling** and **provides no feedback on failed navigation**. The primary dead-end risks are:

1. **Silent navigation failures** at layout boundaries
2. **Layout-specific limitations** not documented
3. **No user feedback** when navigation is impossible
4. **Focus transfer on pane close** is arbitrary, not directionally aware

These are **correctable issues** but should be addressed before considering this production-ready for keyboard-intensive workflows.

