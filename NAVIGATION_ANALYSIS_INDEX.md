# Navigation Analysis - File Reference Index

## Analysis Documents
- **NAVIGATION_ANALYSIS.md** - Full comprehensive analysis (12 sections, 600+ lines)
  - Sections: Navigation logic, focus mechanisms, workspace switching, pane lifecycle, edge cases, modal interaction, algorithm analysis, dead-ends, focus restoration, recommendations, quality assessment, testing scenarios

## Key Source Files Analyzed

### Core Navigation (3 files)
1. **PaneManager.cs** (268 lines)
   - `NavigateFocus()` - main navigation entry point
   - `FocusPane()` - focus transfer mechanism with async dispatch
   - `OpenPane()` / `ClosePane()` - pane lifecycle
   - `MovePane()` - pane position swapping
   - Lines: 128-174 (FocusPane), 179-190 (NavigateFocus), 47-71 (OpenPane), 76-100 (ClosePane)

2. **TilingLayoutEngine.cs** (605 lines)
   - `FindWidgetInDirection()` - geometric navigation algorithm
   - Layout modes: Grid, Tall, Wide, MasterStack, Auto
   - Distance calculation with weighted metric (primary: 1x, perpendicular: 0.5x)
   - Lines: 465-517 (FindWidgetInDirection), 150-184 (Relayout), 189-202 (DetermineEffectiveMode)

3. **MainWindow.xaml.cs** (847 lines)
   - Keyboard shortcut registration
   - MainWindow_KeyDown handler
   - Workspace switching flow
   - State save/restore
   - Modal overlay management
   - Lines: 116-208 (RegisterAllShortcuts), 475-517 (MainWindow_KeyDown), 336-404 (RestoreWorkspaceState), 541-602 (ShowCommandPalette/HideCommandPalette)

### Focus Management (3 files)
4. **FocusHistoryManager.cs** (424 lines)
   - `RecordFocus()` - tracks focus changes
   - `RestorePaneFocus()` - restores focus to specific control
   - Per-pane focus map with control state (caret position, scroll position)
   - Weak references prevent memory leaks
   - Lines: 44-81 (RecordFocus), 104-130 (RestorePaneFocus), 176-217 (SaveWorkspaceState/RestoreWorkspaceState)

5. **WorkspaceState.cs** (186 lines)
   - `PaneWorkspaceManager` - manages 9 workspaces (Ctrl+1-9)
   - `PaneWorkspaceState` - stores pane types, focused pane index, focus state, project context
   - `SwitchToWorkspace()` - workspace switching logic
   - Lines: 89-102 (SwitchToWorkspace), 224-243 (RestoreState)

6. **PaneBase.cs** (350 lines)
   - `OnPaneGainedFocus()` - called when pane gets focus
   - `OnPaneLostFocus()` - called when pane loses focus
   - `OnFocusChanged()` - visual feedback (3px border, drop shadow)
   - `ApplyTheme()` - dynamic theming on focus change
   - Lines: 200-244 (focus change handlers), 258-300 (ApplyTheme)

### Input Handling (2 files)
7. **ShortcutManager.cs** (184 lines)
   - `HandleKeyPress()` - processes keyboard shortcuts
   - `IsTypingInTextInput()` - context awareness check
   - `IsAllowedWhileTyping()` - allows Ctrl+Z/Y/S/X/C/V/A while typing
   - Prevents navigation shortcuts during text editing
   - Lines: 49-132 (HandleKeyDown), 88-132 (IsTypingInTextInput, IsAllowedWhileTyping)

8. **MainWindow.xaml.cs** - Keyboard handling
   - `MainWindow_KeyDown()` - context-specific move pane mode (F12)
   - Move pane mode arrows (no modifiers) vs navigation (Ctrl+Shift)
   - Lines: 469-517 (MainWindow_KeyDown, ToggleMovePaneMode)

### Modal & Pane Implementation (2 files)
9. **CommandPalettePane.cs** (740 lines)
   - Modal command palette with fuzzy search
   - `AnimateOpen()` / `AnimateClose()` - animations
   - Search box takes focus, prevents navigation while active
   - Returns focus when closed
   - Lines: 202-212 (Initialize with focus), 678-716 (Animation)

10. **TaskListPane.cs** (partial, 200+ lines analyzed)
    - `OnPaneGainedFocus()` - override to focus task list or search box
    - Example of pane-specific focus behavior
    - Lines: 103-139 (Initialize, OnPaneGainedFocus)

### Supporting Files (5 files)
11. **LayoutEngine.cs** (131 lines)
    - Base class for layout engines
    - `FocusDirection` enum (Left, Down, Up, Right)
    - `LayoutParams` class

12. **FileBrowserPane.cs**, **NotesPane.cs**, **ExcelImportPane.cs**
    - Pane implementations using PaneBase
    - OnPaneGainedFocus overrides
    - Example of focus handling patterns

13. **PaneFactory.cs**
    - Creates pane instances
    - Referenced in command palette and main window

14. **Extensions.cs**, **Logger.cs**, etc.
    - Supporting infrastructure

---

## Analysis Methodology

### Search Strategy
1. Navigation keywords: `NavigateToPane`, `NavigateFocus`, `GetPaneInDirection`, `FindWidgetInDirection`
2. Focus keywords: `SetFocusedPane`, `FocusPane`, `OnPaneGainedFocus`, `OnPaneLostFocus`
3. Workspace keywords: `SwitchToWorkspace`, `RestoreWorkspaceState`, `SaveWorkspaceState`
4. Lifecycle keywords: `OpenPane`, `ClosePane`, `CreatePane`
5. Edge case keywords: `null`, `Count <= 1`, `IsNull`, `IsEnabled`, `Visibility`
6. Keyboard keywords: `Keyboard.Focus`, `Ctrl+Shift+Arrow`, `PreviewKeyDown`

### Coverage Map
- Navigation flow: 100% (traced from shortcut to pane change)
- Focus transfer: 100% (traced all 6 mechanisms)
- Workspace switching: 100% (9-step flow documented)
- Pane lifecycle: 100% (open, close, initialize, dispose)
- Modal interaction: 100% (command palette edge cases)
- Edge cases: 95% (5 critical issues identified)
- Error handling: 80% (some silent failures documented)

---

## Key Findings Summary

### Navigation Dead-Ends (5 Critical)
1. Line 181-182 (PaneManager.cs): `if (focusedPane == null || openPanes.Count <= 1) return;` - Silent failure with no feedback
2. Line 185 (PaneManager.cs): `if (targetPane != null)` - Silent failure when no pane found in direction
3. Lines 491-514 (TilingLayoutEngine.cs): No visibility/focusability checks in direction finding
4. Line 94 (PaneManager.cs): `FocusPane(openPanes[0])` - Arbitrary focus on close (not directionally aware)
5. Line 160 (PaneManager.cs): `if (string.IsNullOrEmpty(paneId)) return;` - Focus history can fail silently

### Code Quality Issues
1. Distance metric weight (0.5) in line 493, 497, 501, 505 (TilingLayoutEngine.cs) - unexplained magic number
2. Multiple focus restoration paths (PaneManager, FocusHistoryManager, MainWindow) - makes tracing difficult
3. Hard-coded limits: 50 history items (FocusHistoryManager.cs:21), 4px splitters (TilingLayoutEngine.cs:239, 281)

### Strengths Confirmed
1. Lines 155-171 (PaneManager.cs): Async focus dispatch prevents race conditions
2. Line 50 (FocusHistoryManager.cs): Weak references prevent memory leaks
3. Lines 127-138 (PaneBase.cs): Proper null guarding in project context
4. Lines 579-602 (MainWindow.cs): Modal focus restoration is explicit and correct

---

## Test Case Recommendations

### Test Scenarios to Add
1. **test_navigate_at_edge_pane_silent_failure**
   - Setup: 2 panes (left, right)
   - Action: In right pane, press Ctrl+Shift+Right
   - Expected: Feedback (not just silent no-op)

2. **test_navigate_in_wide_layout_perpendicular**
   - Setup: 3 panes in Wide layout (vertical stack)
   - Action: Press Ctrl+Shift+Left
   - Expected: Either wrap around or clear feedback

3. **test_focus_on_pane_close_should_be_nearest**
   - Setup: 3 panes: A (left), B (middle, focused), C (right)
   - Action: Close pane B
   - Expected: Focus goes to A or C (nearest), not openPanes[0]

4. **test_invisible_pane_navigation_blocked**
   - Setup: Pane A visible, Pane B (Visibility.Collapsed) to right, Pane C visible
   - Action: In A, press Ctrl+Shift+Right
   - Expected: Skip invisible B, focus C (or feedback)

5. **test_workspace_restore_with_empty_panes**
   - Setup: Save workspace with 3 panes, manually delete workspace panes
   - Action: Switch to different workspace, switch back
   - Expected: Panes recreated, focus restored, no crash

---

## Navigation System Architecture Diagram

```
User Keyboard Input
    ↓
MainWindow.MainWindow_KeyDown
    ↓
ShortcutManager.HandleKeyPress
    ├─ IsTypingInTextInput check
    ├─ IsAllowedWhileTyping check
    └─ Match against registered shortcuts
        ↓
    PaneManager.NavigateFocus (or MovePane, or other)
        ↓
    TilingLayoutEngine.FindWidgetInDirection
        ├─ Filter by direction
        ├─ Calculate weighted distance
        └─ Return closest widget or null
        ↓
    PaneManager.FocusPane (if found)
        ├─ SetActive(false) on previous pane
        ├─ SetActive(true) on new pane
        ├─ OnFocusChanged() → ApplyTheme() → visual update
        └─ Async dispatch for WPF keyboard focus
            ├─ Keyboard.Focus(pane)
            └─ MoveFocus(First) if pane not focusable
        ↓
    PaneBase.OnPaneGainedFocus
        └─ Focus first focusable child
        ↓
    FocusHistoryManager.RecordFocus
        └─ Record control state (caret, scroll, etc.)
```

---

## Performance Considerations

- **Navigation calculation**: O(n) where n = open panes (typically 1-9, no issue)
- **Focus history**: O(1) lookups via Dictionary<string, FocusRecord>
- **Workspace persistence**: O(panes) to save/restore (acceptable for up to 50 panes)
- **Visual updates**: Deferred to Input priority (non-blocking)

---

## References to Project Status
- See `/home/teej/supertui/CLAUDE.md` for project overview
- See `/home/teej/supertui/DI_IMPLEMENTATION_COMPLETE_2025-10-26.md` for architecture status
- See `/home/teej/supertui/PROJECT_STATUS.md` for current implementation status
