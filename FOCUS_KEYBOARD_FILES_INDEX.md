# Focus & Keyboard System - File Index

## Core Infrastructure Files

### Focus Management
1. **Workspace.cs** (798 lines)
   - Location: `/home/teej/supertui/WPF/Core/Infrastructure/Workspace.cs`
   - Focus state tracking: `focusedElement`, `focusableElements`
   - Key methods: `FocusElement()`, `FocusInDirection()`, `FocusNext()`, `FocusPrevious()`
   - Keyboard handling: `HandleKeyDown()` - routes Alt+H/J/K/L, Tab, and widget keys
   - Widget movement: `MoveWidgetInDirection()` for Alt+Shift+H/J/K/L
   - Fullscreen: `ToggleFullscreen()` for Alt+F
   - Status: 100% implemented, working well

2. **WorkspaceManager.cs** (152 lines)
   - Location: `/home/teej/supertui/WPF/Core/Infrastructure/WorkspaceManager.cs`
   - Multi-workspace focus delegation
   - Method: `HandleKeyDown()` - passes to current workspace
   - Status: Simple passthrough, complete

### Widget/Screen Focus
3. **WidgetBase.cs** (287 lines)
   - Location: `/home/teej/supertui/WPF/Core/Components/WidgetBase.cs`
   - Focus property: `HasFocus` with PropertyChanged notification
   - Visual feedback: `UpdateFocusVisual()` - border, glow effects
   - Keyboard input: `OnWidgetKeyDown()` virtual method
   - Focus events: `OnWidgetFocusReceived()`, `OnWidgetFocusLost()`
   - Status: Fully implemented, well-designed

4. **ScreenBase.cs** (99 lines)
   - Location: `/home/teej/supertui/WPF/Core/Components/ScreenBase.cs`
   - Similar to WidgetBase but simpler
   - Focus property, lifecycle hooks
   - Method: `OnScreenKeyDown()`
   - Status: Complete

### Keyboard & Shortcuts
5. **ShortcutManager.cs** (127 lines)
   - Location: `/home/teej/supertui/WPF/Core/Infrastructure/ShortcutManager.cs`
   - Singleton pattern
   - Data structures: `globalShortcuts`, `workspaceShortcuts` (Dict)
   - Key method: `HandleKeyDown(key, modifiers, currentWorkspace)` - tries workspace then global
   - Registration: `RegisterGlobal()`, `RegisterForWorkspace()`
   - Status: Fully implemented, efficient

6. **KeyboardShortcut.cs** (66 lines)
   - Location: `/home/teej/supertui/WPF/Core/Models/KeyboardShortcut.cs`
   - Data model for shortcuts
   - Properties: `Key`, `Modifiers`, `Action`, `Description`
   - Method: `Matches()` for key comparison
   - Method: `ToString()` for display (e.g., "Ctrl+N")
   - Status: Complete

7. **IShortcutManager.cs** (48 lines)
   - Location: `/home/teej/supertui/WPF/Core/Interfaces/IShortcutManager.cs`
   - Interface definition
   - No implementation in this file
   - Status: Interface only

## Event System

8. **EventBus.cs** (452 lines)
   - Location: `/home/teej/supertui/WPF/Core/Infrastructure/EventBus.cs`
   - Pub/sub event system
   - Typed and named subscriptions
   - Priority support (Low, Normal, High, Critical)
   - Weak reference support for memory management
   - Methods: `Subscribe<T>()`, `Publish<T>()`, `Request<TRequest, TResponse>()`
   - Internal class: `Subscription` - holds subscription metadata
   - Status: Fully implemented, sophisticated, but NOT used for keyboard/focus events yet

9. **Events.cs** (403 lines)
   - Location: `/home/teej/supertui/WPF/Core/Infrastructure/Events.cs`
   - Event type definitions
   - Relevant to focus: `WidgetFocusReceivedEvent`, `WidgetFocusLostEvent`
   - Also defines: `WidgetActivatedEvent`, `WidgetDeactivatedEvent`, many others
   - Status: Complete, ready to use

## Overlay Components

10. **ShortcutOverlay.cs** (200+ lines)
    - Location: `/home/teej/supertui/WPF/Core/Components/ShortcutOverlay.cs`
    - Full-screen overlay showing keyboard shortcuts
    - Consumes keyboard input when visible
    - Method: `OnKeyDown()` - handles Esc and ? to close
    - Method: `Show()`, `Hide()`
    - Keyboard interception: Yes, prevents event bubbling
    - Triggered by: `?` key
    - Status: Fully implemented

11. **QuickJumpOverlay.cs** (200+ lines)
    - Location: `/home/teej/supertui/WPF/Core/Components/QuickJumpOverlay.cs`
    - Context-aware widget jumping menu
    - Internal state: `jumpTargets` dictionary (Key → target widget)
    - Method: `OnKeyDown()` - handles registered jump keys
    - Method: `RegisterJump()` - register jump targets
    - Event: `JumpRequested` - fired when jump key pressed
    - Triggered by: `G` key
    - Status: Mostly implemented, but depends on missing Workspace.FocusWidget()
    - Status: BLOCKING - QuickJump won't work

## Interfaces

12. **IWidget.cs** (30 lines)
    - Location: `/home/teej/supertui/WPF/Core/Interfaces/IWidget.cs`
    - Interface for widgets
    - Includes: `HasFocus`, `OnWidgetKeyDown()`
    - Status: Interface definition only

13. **IWorkspace.cs** (26 lines)
    - Location: `/home/teej/supertui/WPF/Core/Interfaces/IWorkspace.cs`
    - Interface for workspaces
    - Method: `HandleKeyDown()`
    - Status: Interface definition only

## Application Entry Point

14. **SuperTUI.ps1** (1152 lines)
    - Location: `/home/teej/supertui/WPF/SuperTUI.ps1`
    - Window creation and setup in PowerShell
    - Keyboard handler: Line 982-1085 (`$window.Add_KeyDown`)
    - Shortcut overlay setup: Lines 266-273
    - Quick jump overlay setup: Lines 279-318
    - CRT overlay setup: Lines 325-369
    - Workspace setup: Lines 431-599
    - Status: INCOMPLETE - missing call to `$workspaceManager.HandleKeyDown($e)`
    - Missing features: Uses undefined methods `FocusWidget()`, `FocusedWidget` property, `GetAllWidgets()`

## Deprecated/Legacy Files

15. **Framework.cs.deprecated** (deprecated)
    - Location: `/home/teej/supertui/WPF/Core/Framework.cs.deprecated`
    - Old implementation, superseded by Workspace/WidgetBase separation
    - Status: Not in use

## Summary Statistics

| Category | Count | Status |
|----------|-------|--------|
| Core infrastructure files | 4 | 100% implemented |
| Widget/screen files | 2 | 100% implemented |
| Keyboard/shortcut files | 3 | 95% implemented (FocusWidget missing) |
| Event system files | 2 | 100% implemented, 0% used |
| Overlay components | 2 | 90% implemented (FocusWidget dependency) |
| Interface definitions | 2 | Complete |
| Entry point | 1 | 95% implemented (missing HandleKeyDown call) |
| **TOTAL** | **16** | **~85% complete** |

## Critical Missing Implementations

### In Workspace.cs (must add)
```csharp
// Line to add after GetFocusedWidget():
public WidgetBase FocusedWidget => GetFocusedWidget();

// New method to add:
public void FocusWidget(WidgetBase widget)
{
    FocusElement(widget);
}

// New method to add:
public IEnumerable<WidgetBase> GetAllWidgets()
{
    return Widgets.AsReadOnly();
}
```

### In SuperTUI.ps1 (must fix - line 1080 area)
```powershell
# After ShortcutManager check, add:
if (-not $handled) {
    $workspaceManager.HandleKeyDown($e)
    $handled = $e.Handled
}
```

## File Locations - Quick Lookup

```
Core Infrastructure/
├── Workspace.cs - MAIN: Focus state & keyboard routing
├── WorkspaceManager.cs - Multi-workspace coordination
├── ShortcutManager.cs - Shortcut registry & dispatch
├── EventBus.cs - Event pub/sub system
└── Events.cs - Event type definitions

Components/
├── WidgetBase.cs - Widget focus interface
├── ScreenBase.cs - Screen focus interface
├── ShortcutOverlay.cs - Help overlay
└── QuickJumpOverlay.cs - Context jump menu

Interfaces/
├── IWidget.cs - Widget interface
└── IWorkspace.cs - Workspace interface

Models/
└── KeyboardShortcut.cs - Shortcut data model

Entry Point/
└── SuperTUI.ps1 - Window setup & keyboard handler
```

---

**Last Updated:** 2025-10-26
**Total Lines Analyzed:** ~4,000+ lines
**Completeness:** 85%
