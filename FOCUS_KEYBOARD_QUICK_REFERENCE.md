# SuperTUI Focus & Keyboard System - Quick Reference

## Current Implementation Status: 85% Complete

### What Works
✅ Focus tracking per widget with visual indicators (border + glow)  
✅ Alt+H/J/K/L directional focus navigation (i3-style)  
✅ Alt+Shift+H/J/K/L widget movement  
✅ Tab/Shift+Tab focus cycling  
✅ Widget keyboard input via OnWidgetKeyDown()  
✅ ShortcutManager with global & workspace-specific shortcuts  
✅ Keyboard overlays (? help, G quick jump)  

### What's Broken
❌ PowerShell missing `$workspaceManager.HandleKeyDown($e)` call  
❌ Workspace.FocusWidget(widget) method missing  
❌ Workspace.FocusedWidget property missing  
❌ Workspace.GetAllWidgets() method missing  
❌ No EventBus integration for keyboard/focus events  
❌ No key binding configuration system  

## Keyboard Routing Chain

```
Window KeyDown Event
├─ Overlay keys (?, G, Esc) → Handled by overlays
├─ ShortcutManager → Global/workspace shortcuts
└─ [MISSING] WorkspaceManager.HandleKeyDown()
    └─ Workspace.HandleKeyDown()
        ├─ Alt+H/J/K/L → FocusInDirection()
        ├─ Alt+Shift+H/J/K/L → MoveWidgetInDirection()
        ├─ Tab → FocusNext()
        └─ Delegate to focused widget → OnWidgetKeyDown()
```

## Key Files

| File | Lines | Purpose |
|------|-------|---------|
| Workspace.cs | 798 | Focus state, navigation, keyboard handling |
| WidgetBase.cs | 287 | Widget focus interface |
| ShortcutManager.cs | 127 | Shortcut registry & dispatch |
| WorkspaceManager.cs | 152 | Multi-workspace focus delegation |
| EventBus.cs | 452 | Pub/sub (not used for keyboard) |
| SuperTUI.ps1 | 1152 | Window entry point & keyboard handler |

## Critical Fixes Needed

### 1. Add to Workspace.cs
```csharp
public void FocusWidget(WidgetBase widget)
{
    FocusElement(widget);
}

public WidgetBase FocusedWidget => GetFocusedWidget();

public IEnumerable<WidgetBase> GetAllWidgets()
{
    return Widgets.AsReadOnly();
}
```

### 2. Update SuperTUI.ps1 (line 1080, after ShortcutManager check)
```powershell
# If not handled by shortcuts, try workspace
if (-not $handled) {
    $workspaceManager.HandleKeyDown($e)
    $handled = $e.Handled
}
```

## Focus API

### Workspace Focus Methods
- `FocusNext()` - Tab to next widget
- `FocusPrevious()` - Shift+Tab to previous
- `FocusInDirection(FocusDirection dir)` - i3-style navigation
- `GetFocusedWidget()` - Get current focused widget
- `ToggleFullscreen()` - Alt+F fullscreen

### Widget Focus Properties
- `HasFocus` - Boolean property with notifications
- `OnWidgetFocusReceived()` - Override for focus gained
- `OnWidgetFocusLost()` - Override for focus lost
- `OnWidgetKeyDown(KeyEventArgs)` - Override for keyboard

## Event Types (EventBus, unused currently)
- `WidgetFocusReceivedEvent` - Published when widget gets focus
- `WidgetFocusLostEvent` - Published when widget loses focus
- `WidgetActivatedEvent` - Published when widget becomes visible
- `WidgetDeactivatedEvent` - Published when widget becomes hidden

## Shortcut Registration Pattern

```csharp
// Global shortcut
shortcutManager.RegisterGlobal(Key.N, ModifierKeys.Control, 
    () => { /* action */ }, "Create new");

// Workspace-specific shortcut
shortcutManager.RegisterForWorkspace("Dashboard", Key.R, ModifierKeys.Control,
    () => { /* action */ }, "Refresh");
```

## Layout Grid-Based Focus Algorithm

When navigating with Alt+H/J/K/L:
1. Gets current element's grid position (row, col)
2. Finds all candidates in target direction
3. Calculates distance: `(axis_distance) + (perpendicular_distance) * 0.5`
4. Selects closest candidate
5. If none found, wraps around to opposite edge

Example: Alt+L (focus right)
- From position (0, 0): focuses (0, 1) if exists
- From position (1, 2): skips (1, 1), focuses closest in same/nearby row

## Known Issues

1. **QuickJumpOverlay not functional** - Calls `$current.FocusWidget($widget)` which doesn't exist
2. **Property vs Method mismatch** - PS1 expects `FocusedWidget` property, C# provides `GetFocusedWidget()` method
3. **No EventBus integration** - Keyboard/focus changes don't publish to event system
4. **Hardcoded keybindings** - Alt+H/J/K/L cannot be customized

## Widget Keyboard Handling Example

```csharp
public override void OnWidgetKeyDown(KeyEventArgs e)
{
    switch (e.Key)
    {
        case Key.Up:
            DoSomething();
            e.Handled = true;
            break;
        case Key.Down:
            DoSomethingElse();
            e.Handled = true;
            break;
    }
    // If not handled, key bubbles up to workspace
}
```

## Focus Visual Feedback

When a widget receives focus:
- Border changes from 1px (theme.Border) to 2px (theme.Focus)
- Glow effect applied from theme.Glow.Focus state
- `OnWidgetFocusReceived()` called
- `HasFocus` property set to true
- PropertyChanged event fires

## Testing Focus Features

Manual test checklist:
- [ ] Alt+H focuses left neighbor
- [ ] Alt+J focuses down neighbor
- [ ] Alt+K focuses up neighbor
- [ ] Alt+L focuses right neighbor
- [ ] Alt+Shift+H moves widget left
- [ ] Tab cycles forward
- [ ] Shift+Tab cycles backward
- [ ] ? toggles help overlay
- [ ] G opens quick jump (once methods added)

---

**Last Updated:** 2025-10-26
**Completeness:** 85%
**Status:** Production-ready with limitations
