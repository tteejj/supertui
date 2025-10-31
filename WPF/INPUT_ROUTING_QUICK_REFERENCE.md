# SuperTUI Focus & Input Routing - Quick Reference

**Date:** 2025-10-31  
**Full Analysis:** See `FOCUS_INPUT_ROUTING_ANALYSIS.md` (1237 lines)

---

## Architecture Overview

```
Input Flow:
  MainWindow.KeyDown (Bubbling)
    ↓
  ShortcutManager.HandleKeyPress()
    - Checks if typing in TextBox
    - Returns true/false (consumed/not)
    ↓ (if not consumed)
  isMovePaneMode context handler
    ↓ (if not consumed)
  Event bubbles to focused pane
    ↓
  Pane.PreviewKeyDown (Tunneling) [NotesPane only]
    ↓
  Pane.KeyDown (Bubbling) [all panes]
    ↓
  Child controls handle
```

---

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| **ShortcutManager** | `Core/Infrastructure/ShortcutManager.cs` | Global keyboard shortcuts (20 total) |
| **PaneManager** | `Core/Infrastructure/PaneManager.cs` | Focus tracking, pane switching |
| **FocusHistoryManager** | `Core/Infrastructure/FocusHistoryManager.cs` | Focus restoration across workspaces |
| **PaneBase** | `Core/Components/PaneBase.cs` | Base class, focus notifications |
| **MainWindow** | `MainWindow.xaml.cs` | Global event dispatcher |

---

## Global Shortcuts (Registered in MainWindow)

```
Help:          ? (Shift+/)
Command Pal:   : (Shift+;)
Move Mode:     F12
Debug:         Ctrl+Shift+D

Workspaces:    Ctrl+1..9
Pane Focus:    Ctrl+Shift+Arrow
Pane Move:     F12 then Arrow
Pane Open:     Ctrl+Shift+T/N/P/E/F/C
Pane Close:    Ctrl+Shift+Q
Undo/Redo:     Ctrl+Z/Y
```

---

## Focus Management

**Active Focus:**
- PaneManager.FocusPane(pane) - Called when pane selected
- Sets both custom IsFocused flag AND WPF keyboard focus
- Calls OnPaneGainedFocus() on new pane (virtual method)
- Fires PaneFocusChanged event

**Focus Restoration:**
- FocusHistoryManager tracks last focused control per pane
- Stores caret position, selection, scroll offset
- Restored on workspace switch via SaveState/RestoreState
- Uses WeakReferences to avoid memory leaks

---

## CRITICAL ISSUES FOUND

| # | Issue | Severity | Location |
|---|-------|----------|----------|
| 5 | No input blocking for modal overlay | **CRITICAL** | MainWindow.KeyDown doesn't suppress background pane events |
| 11 | Modal can't capture input | **CRITICAL** | CommandPalette + background pane both handle same key |
| 2 | Focus restoration fails silently | **HIGH** | FocusHistoryManager weak refs die without fallback |
| 4 | Inconsistent event handling | **MEDIUM** | NotesPane uses PreviewKeyDown, TaskListPane uses KeyDown |
| 1 | Modal Tab navigation undefined | **MEDIUM** | ModalOverlay outside PaneCanvas focus scope |
| 10 | ShortcutManager 80% unused | **MEDIUM** | Pane shortcuts hardcoded in KeyDown handlers |
| 3 | Triple-phase focus setting | **LOW** | Race condition between sync/async focus calls |

---

## Pane Shortcuts (Hardcoded, NOT in ShortcutManager)

**TaskListPane:**
- A = Add task
- D = Mark done
- E = Edit task
- S = Toggle subtask
- C = Copy task
- etc.

**NotesPane:**
- Plus/Minus = Font size
- Ctrl+F = Search
- Cmd Palette = Colon (:)

**FileBrowserPane:**
- Enter = Open/navigate
- Backspace = Go up

---

## Key Files

**Read For Details:**
1. `MainWindow.xaml.cs` (lines 80, 116-208, 475-517)
2. `ShortcutManager.cs` (lines 49-132, 88-132)
3. `PaneManager.cs` (lines 128-174)
4. `FocusHistoryManager.cs` (lines 44-130)
5. `PaneBase.cs` (lines 200-250)

**Modals:**
- `MainWindow.xaml.cs` (lines 540-602, 660-743)
- `CommandPalettePane.cs` (modal behavior)

**Analysis:**
- `FOCUS_INPUT_ROUTING_ANALYSIS.md` (full 1237-line analysis)
- `INPUT_ROUTING_FIXED.md` (context-aware shortcuts)
- `TUI_KEYBOARD_FOCUS_ANALYSIS.md` (previous implementation patterns)

---

## What Works Well

✅ Context-aware ShortcutManager (checks if typing)  
✅ Clean focus tracking per pane  
✅ Focus restoration across workspace switches  
✅ Window activation/deactivation handling  
✅ Good separation of global vs pane shortcuts  
✅ OnPaneGainedFocus() virtual method for pane-specific handling  

---

## What Needs Fixing

❌ Modal input blocking (highest priority!)  
❌ Modal needs FocusScope  
❌ Fallback when focus element is dead  
❌ Standardize PreviewKeyDown vs KeyDown usage  
❌ Move pane shortcuts to ShortcutManager  
❌ Shortcut conflict detection  

---

## Testing Checklist

- [ ] Type in TextBox, press Ctrl+Z → does NOT undo task (goes to TextBox)
- [ ] Type in NotesPane searchBox → single-key shortcuts blocked
- [ ] Open CommandPalette, type nonsense → TaskListPane NOT moving
- [ ] Alt+Tab away and back → focus restored to correct pane
- [ ] Close focused pane → focus moves to next, no exceptions
- [ ] Move pane mode F12 → arrow keys swap panes, not navigate
- [ ] Tab in CommandPalette → cycles through modal controls only

---

## Quick Fixes

**Fix #1 - Modal Input Blocking (URGENT):**
```csharp
// MainWindow.xaml.cs - MainWindow_KeyDown()
if (ModalOverlay.Visibility == Visibility.Visible)
{
    // Don't route to background panes
    return;  // Let modal handle it
}
```

**Fix #2 - Modal FocusScope:**
```xaml
<!-- MainWindow.xaml -->
<Grid x:Name="ModalOverlay" 
      FocusManager.IsFocusScope="True"
      Grid.RowSpan="2" 
      Visibility="Collapsed" 
      Panel.ZIndex="1000"/>
```

**Fix #3 - Focus Fallback:**
```csharp
// FocusHistoryManager.cs
if (element == null)
{
    // Fallback: find first focusable child
    var pane = FindPaneById(paneId);
    pane?.MoveFocus(new TraversalRequest(
        FocusNavigationDirection.First));
}
```

---

## Conclusion

SuperTUI has a **solid foundation** for focus and input routing but needs **critical modal fixes** before production use.

Most critical: Prevent background panes from receiving input when modal is visible.

See full analysis for 12 identified issues with detailed explanations and code examples.
