# Critical Analysis: Pane Focus Management Infrastructure

**Finding:** CRITICAL DESIGN FLAWS creating race conditions between panes

## Four Conflicting Focus Models (Not Synchronized)

1. **IsActive property** - Custom flag, set by PaneManager
2. **IsKeyboardFocusWithin** - WPF native property, actual focus
3. **FocusHistoryManager** - Custom tracking with weak references
4. **EventBus** - Synchronous publishing that can change focus

## Critical Issue #1: Sync/Async Race Condition

PaneManager.FocusPane() split between sync and async:

- T0: SetActive(false) shows visual unfocused, but old pane still has actual focus
- T1: SetActive(true) shows visual focused, but new pane doesn't have actual focus
- T2: Dispatcher processes, actually sets focus
- Result: Visual state wrong during T0-T2 window, focus flickers

## Critical Issue #2: OnPaneGainedFocus Called Too Early

- Called synchronously during SetActive()
- Control might not be loaded
- Competes with FocusHistoryManager.ApplyFocusToPane() running async
- Race condition: last focus operation wins

## Critical Issue #3: IsActive Not Synced with WPF Focus

- PaneManager sets IsActive property
- ApplyTheme() uses IsKeyboardFocusWithin instead
- IsActive is dead weight, visual state ignores it

## Critical Issue #4: FocusHistoryManager Too Complex

- Four-level fallback chain (tries element, first child, pane, MainWindow)
- Weak references can be garbage collected
- FindFirstFocusableChild() O(n) tree walk
- Falls back to MainWindow when it should fail loudly

## Critical Issue #5: Workspace Switch Loses Focus

- focusHistory.SaveWorkspaceState() called
- focusHistory.RestoreWorkspaceState() then CLEARS all history
- New panes created with no history
- Falls back to MainWindow (wrong)

## FileBrowserPane ↔ NotesPane Issues

**Navigation (Ctrl+Shift+Right):**
- Visual shows wrong pane focused during transition
- Focus flickers on screen
- Actual keyboard input goes to wrong place temporarily

**Event-triggered (NotesPane requests FileBrowserPane):**
- New FileBrowserPane instance created
- No focus history for new instance
- Falls back to generic first-focusable-child
- Might focus wrong control

## Recommendations

1. Remove IsActive property, use ONLY IsKeyboardFocusWithin
2. Remove OnPaneGainedFocus/OnPaneLostFocus, handle in PaneManager
3. Simplify FocusHistoryManager or remove entirely
4. Don't clear focus history before restoring
5. Use consistent Dispatcher.Priority for all focus ops

## Severity: CRITICAL
- User-facing focus bugs
- Hard to debug
- Unpredictable behavior


