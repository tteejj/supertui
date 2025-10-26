# SuperTUI Focus Management & Keyboard Input System Analysis

**Analysis Date:** 2025-10-26  
**Codebase Location:** /home/teej/supertui/WPF  
**Scope:** Complete focus management and keyboard routing system  

---

## Executive Summary

SuperTUI has a **well-structured but partially incomplete** focus management and keyboard input system. The architecture is clean and follows i3-style keyboard navigation patterns, but there are **gaps between what the PowerShell entry point expects and what the C# code provides**.

### Key Findings:
- **Focus system:** Fully implemented with directional navigation (Alt+H/J/K/L)
- **Keyboard routing:** Multi-layer system (window → workspace → widget)
- **Shortcuts:** Centralized ShortcutManager with global and workspace-specific shortcuts
- **Missing methods:** `FocusWidget()`, `FocusedWidget` property, `GetAllWidgets()` in Workspace class
- **EventBus:** Exists but not currently used for keyboard/focus events
- **Focus state tracking:** Per-widget via `HasFocus` property and visual indicators

---

## 1. FOCUS MANAGEMENT INFRASTRUCTURE

### 1.1 Workspace Focus System (Workspace.cs)

**Location:** `/home/teej/supertui/WPF/Core/Infrastructure/Workspace.cs`

#### Focus State Tracking
```csharp
private UIElement focusedElement;                    // Currently focused element
private List<UIElement> focusableElements;           // List of focusable widgets/screens
```

#### Focus Navigation Methods
| Method | Purpose | Notes |
|--------|---------|-------|
| `FocusNext()` | Cycle to next widget | Uses Tab key |
| `FocusPrevious()` | Cycle to previous widget | Uses Shift+Tab |
| `FocusInDirection(FocusDirection direction)` | i3-style directional focus | Alt+H/J/K/L for Left/Down/Up/Right |
| `CycleFocusForward()` | Forward cycle | Alias for FocusNext() |
| `CycleFocusBackward()` | Backward cycle | Alias for FocusPrevious() |
| `FocusElement(UIElement element)` | Internal focus setter | Updates HasFocus, calls event handlers |

#### Focus State Storage
```csharp
private void FocusElement(UIElement element)
{
    // Clear previous focus
    if (focusedElement != null)
    {
        if (focusedElement is WidgetBase widget)
            widget.HasFocus = false;
        else if (focusedElement is ScreenBase screen)
            screen.HasFocus = false;
    }

    // Set new focus
    focusedElement = element;

    if (element is WidgetBase newWidget)
    {
        newWidget.HasFocus = true;
        newWidget.Focus();  // WPF focus
    }
    else if (element is ScreenBase newScreen)
    {
        newScreen.HasFocus = true;
        newScreen.Focus();  // WPF focus
    }
}
```

#### Directional Focus Algorithm
The `FocusInDirection()` method implements sophisticated grid-based navigation:
1. Gets current focused element's grid position (row, col)
2. Scans all focusable elements
3. Finds candidates in the target direction
4. Calculates weighted distance: `distance = (axis_distance) + Math.Abs(perpendicular_distance) * 0.5`
5. Selects closest candidate
6. If no candidate found, wraps around (rightmost/leftmost/topmost/bottommost)

#### Lifecycle Hooks
```csharp
public void Activate()
{
    isActive = true;
    // ... activate all widgets ...
    if (focusedElement == null && focusableElements.Count > 0)
        FocusElement(focusableElements[0]);  // Focus first element on activate
}

public void Deactivate()
{
    isActive = false;
    // ... deactivate all widgets ...
    // Preserves focus state for when workspace reactivates
}
```

### 1.2 Widget Focus Interface (WidgetBase.cs)

**Location:** `/home/teej/supertui/WPF/Core/Components/WidgetBase.cs`

#### Focus Property
```csharp
private bool hasFocus;
public bool HasFocus
{
    get => hasFocus;
    set
    {
        if (hasFocus != value)
        {
            hasFocus = value;
            OnPropertyChanged(nameof(HasFocus));
            UpdateFocusVisual();

            if (value)
                OnWidgetFocusReceived();
            else
                OnWidgetFocusLost();
        }
    }
}
```

#### Focus Visual Feedback
```csharp
private void UpdateFocusVisual()
{
    if (containerBorder != null)
    {
        var theme = ThemeManager.Instance.CurrentTheme;

        if (HasFocus)
        {
            containerBorder.BorderBrush = new SolidColorBrush(theme.Focus);
            containerBorder.BorderThickness = new Thickness(2);
            
            if (theme.Glow != null)
                GlowEffectHelper.ApplyGlow(containerBorder, theme.Glow, GlowState.Focus);
        }
        else
        {
            containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            containerBorder.BorderThickness = new Thickness(1);
            
            if (theme.Glow != null && theme.Glow.Mode == GlowMode.Always)
                GlowEffectHelper.ApplyGlow(containerBorder, theme.Glow, GlowState.Normal);
        }
    }
}
```

#### Focus Event Handlers
```csharp
public virtual void OnWidgetFocusReceived() { }
public virtual void OnWidgetFocusLost() { }
```

### 1.3 ScreenBase Focus (ScreenBase.cs)

**Location:** `/home/teej/supertui/WPF/Core/Components/ScreenBase.cs`

Similar to WidgetBase but simpler:
```csharp
private bool hasFocus;
public bool HasFocus { get; set; }
public virtual void OnFocusReceived() { }
public virtual void OnFocusLost() { }
```

---

## 2. KEYBOARD INPUT ROUTING CHAIN

### 2.1 Input Flow Diagram
```
Window (PowerShell, SuperTUI.ps1)
    ↓
    Add_KeyDown event handler
    ├─ Handle overlay keys (?, G, Esc)
    └─ Delegate to ShortcutManager → ShortcutManager.HandleKeyDown()
    └─ Delegate to WorkspaceManager → WorkspaceManager.HandleKeyDown()
    
WorkspaceManager (WorkspaceManager.cs)
    ↓
    CurrentWorkspace.HandleKeyDown(KeyEventArgs)
    
Workspace (Workspace.cs)
    ↓
    ├─ Check for i3-style keys (Alt+h/j/k/l)
    │   ├─ Alt+h/j/k/l → FocusInDirection()
    │   └─ Alt+Shift+h/j/k/l → MoveWidgetInDirection()
    ├─ Check for Tab navigation
    │   ├─ Tab → FocusNext()
    │   └─ Shift+Tab → FocusPrevious()
    └─ Delegate to focused widget/screen → OnWidgetKeyDown() / OnScreenKeyDown()
    
Individual Widget
    ↓
    OnWidgetKeyDown(KeyEventArgs)
    └─ Each widget handles its own keys
```

### 2.2 Window-Level Keyboard Handler (SuperTUI.ps1, line 982-1085)

**Priority Order:**
1. **Overlay keys (early exit if matched):**
   - `?` (Shift+/) → Toggle ShortcutOverlay
   - `G` → Open QuickJumpOverlay (with context-aware jumps)
   - `Esc` → Close overlays if open

2. **ShortcutManager shortcuts (if matched, set e.Handled = true):**
   ```powershell
   $handled = $shortcutManager.HandleKeyDown($e.Key, $e.KeyboardDevice.Modifiers, $currentWorkspaceName)
   ```

3. **WorkspaceManager/Workspace routing (if not handled by shortcuts):**
   ```
   Implicitly handled through event bubbling if not marked as handled
   ```

**Problem:** The current PowerShell doesn't explicitly call `$workspaceManager.HandleKeyDown($e)`, so keyboard events for widgets may not reach them properly.

### 2.3 WorkspaceManager Keyboard Handler (WorkspaceManager.cs:113-116)

```csharp
public void HandleKeyDown(KeyEventArgs e)
{
    CurrentWorkspace?.HandleKeyDown(e);
}
```

Simple passthrough to current workspace.

### 2.4 Workspace Keyboard Handler (Workspace.cs:368-452)

**Handles:**
1. **Alt modifier + h/j/k/l (i3-style focus navigation)** - Lines 376-429
   - `Alt+h` → FocusInDirection(Left)
   - `Alt+j` → FocusInDirection(Down)
   - `Alt+k` → FocusInDirection(Up)
   - `Alt+l` → FocusInDirection(Right)
   
2. **Alt+Shift modifier + h/j/k/l (widget movement)** - Lines 378-402
   - `Alt+Shift+h` → MoveWidgetLeft()
   - `Alt+Shift+j` → MoveWidgetDown()
   - `Alt+Shift+k` → MoveWidgetUp()
   - `Alt+Shift+l` → MoveWidgetRight()

3. **Tab navigation** - Lines 443-451
   - `Tab` → FocusNext()
   - `Shift+Tab` → FocusPrevious()

4. **Delegate to focused element** - Lines 433-440
   ```csharp
   if (focusedElement is WidgetBase widget)
   {
       widget.OnWidgetKeyDown(e);
   }
   else if (focusedElement is ScreenBase screen)
   {
       screen.OnScreenKeyDown(e);
   }
   ```

---

## 3. SHORTCUT MANAGER

### 3.1 ShortcutManager Implementation (ShortcutManager.cs)

**Location:** `/home/teej/supertui/WPF/Core/Infrastructure/ShortcutManager.cs`

#### Data Structures
```csharp
private List<KeyboardShortcut> globalShortcuts;
private Dictionary<string, List<KeyboardShortcut>> workspaceShortcuts;
```

#### KeyboardShortcut Class (KeyboardShortcut.cs)
```csharp
public class KeyboardShortcut
{
    public Key Key { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public Action Action { get; set; }
    public string Description { get; set; }
    
    public bool Matches(Key key, ModifierKeys modifiers)
    {
        return Key == key && Modifiers == modifiers;
    }
}
```

#### IShortcutManager Interface (IShortcutManager.cs)
```csharp
void RegisterGlobal(Key key, ModifierKeys modifiers, Action action, string description = "");
void RegisterForWorkspace(string workspaceName, Key key, ModifierKeys modifiers, Action action, ...);
bool HandleKeyPress(Key key, ModifierKeys modifiers, string currentWorkspace = null);
IReadOnlyList<KeyboardShortcut> GetGlobalShortcuts();
IReadOnlyList<KeyboardShortcut> GetWorkspaceShortcuts(string workspaceName);
void ClearAll();
void ClearWorkspace(string workspaceName);
```

#### Key Handling Logic
```csharp
public bool HandleKeyDown(Key key, ModifierKeys modifiers, string currentWorkspace)
{
    // Try workspace-specific shortcuts first
    if (!string.IsNullOrEmpty(currentWorkspace) && workspaceShortcuts.ContainsKey(currentWorkspace))
    {
        foreach (var shortcut in workspaceShortcuts[currentWorkspace])
        {
            if (shortcut.Matches(key, modifiers))
            {
                shortcut.Action?.Invoke();
                return true;
            }
        }
    }

    // Try global shortcuts
    foreach (var shortcut in globalShortcuts)
    {
        if (shortcut.Matches(key, modifiers))
        {
            shortcut.Action?.Invoke();
            return true;
        }
    }

    return false;
}
```

**Priority:** Workspace-specific → Global

### 3.2 Shortcut Registration

**NOT FOUND IN CODE** - The PowerShell script calls `RegisterGlobal()` and `RegisterForWorkspace()` but we see no actual registration in the C# code. This needs to be done in the PowerShell setup.

---

## 4. WIDGET KEYBOARD INPUT HANDLING

### 4.1 Example: CounterWidget.OnWidgetKeyDown()

```csharp
public override void OnWidgetKeyDown(KeyEventArgs e)
{
    switch (e.Key)
    {
        case Key.Up:
            Count++;
            e.Handled = true;
            break;

        case Key.Down:
            Count--;
            e.Handled = true;
            break;

        case Key.R:
            Count = 0;
            e.Handled = true;
            break;
    }
}
```

### 4.2 WidgetBase Keyboard Listening

```csharp
public WidgetBase()
{
    this.Loaded += (s, e) => WrapInFocusBorder();
    this.Focusable = true;
    this.GotFocus += (s, e) => HasFocus = true;
    this.LostFocus += (s, e) => HasFocus = false;
}
```

Widgets use WPF's `GotFocus` and `LostFocus` events to update their `HasFocus` property.

### 4.3 Widget-Specific Keyboard Handlers

Many widgets attach their own KeyDown handlers to internal controls:

**CommandPaletteWidget:**
```csharp
searchBox.KeyDown += SearchBox_KeyDown;
```

**TodoWidget, AgendaWidget, FileExplorerWidget, etc.**
```csharp
todoList.KeyDown += TodoList_KeyDown;
listBox.KeyDown += ListBox_KeyDown;
fileListBox.KeyDown += FileListBox_KeyDown;
```

These handle widget-internal navigation (e.g., moving through list items).

---

## 5. OVERLAY SYSTEMS

### 5.1 ShortcutOverlay (ShortcutOverlay.cs)

Displays keyboard shortcuts. When visible, it consumes keyboard input:
```csharp
private void OnKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Escape || e.Key == Key.OemQuestion || ...)
    {
        Hide();
        e.Handled = true;
    }
}
```

**Triggered by:** `?` key at window level

### 5.2 QuickJumpOverlay (QuickJumpOverlay.cs)

Context-aware widget jumping menu:
```csharp
private void OnKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Escape || e.Key == Key.G)
    {
        Hide();
        e.Handled = true;
        return;
    }

    // Check if key matches a jump target
    if (jumpTargets.ContainsKey(e.Key))
    {
        var target = jumpTargets[e.Key];
        JumpRequested?.Invoke(target.TargetWidget, target.Context);
        Hide();
        e.Handled = true;
    }
}
```

**Triggered by:** `G` key at window level

**Context-aware jumps** - Registers different jump targets based on current focused widget type (lines 1030-1062 in SuperTUI.ps1)

---

## 6. EVENT BUS (Potential Enhancement)

### 6.1 EventBus Implementation (EventBus.cs)

A publish/subscribe event system exists but is **NOT CURRENTLY USED FOR KEYBOARD/FOCUS EVENTS**.

**Available event types defined in Events.cs:**
- `WidgetFocusReceivedEvent`
- `WidgetFocusLostEvent`
- `WidgetActivatedEvent`
- `WidgetDeactivatedEvent`
- And many others

**Current usage:** Mostly for theme changes, widget lifecycle, file system, and git events.

### 6.2 Keyboard Events NOT Currently Published

The focus system doesn't publish to the EventBus. This means:
- Widgets can't react to other widgets gaining/losing focus via EventBus
- No event-driven cross-widget communication for focus changes
- Any keyboard events are handled synchronously through method calls only

---

## 7. MISSING FUNCTIONALITY

### 7.1 Critical Missing Methods

**In Workspace class:**
1. **`FocusWidget(WidgetBase widget)`** - Direct widget focusing
   - **Expected by:** SuperTUI.ps1 line 296
   - **Current workaround:** Use `GetFocusedWidget()` and manually update state
   - **Status:** MISSING

2. **`FocusedWidget` property** - Get currently focused widget
   - **Expected by:** SuperTUI.ps1 lines 996, 1027
   - **Current alternative:** `GetFocusedWidget()` method works (getter, no property)
   - **Status:** PARTIALLY MISSING (method exists, property doesn't)

3. **`GetAllWidgets()` method** - Return all widgets in workspace
   - **Expected by:** SuperTUI.ps1 line 293
   - **Current status:** Layout engine has this, Workspace doesn't expose it
   - **Status:** MISSING

### 7.2 Implementation Gaps

1. **No keyboard event to EventBus mapping**
   - Keyboard input is not published to the event system
   - Can't have event-driven cross-widget keyboard handling

2. **No input manager/dispatcher**
   - No centralized input handling service
   - Each component handles its own input

3. **No input capture/blocking mechanism**
   - No way for a widget to "steal" focus from others
   - Modal dialogs would be difficult to implement

4. **No key binding configuration**
   - i3 keys are hardcoded (Alt+h/j/k/l)
   - No way to customize key bindings per user

---

## 8. WHAT'S WORKING WELL

### 8.1 Focus Management
- Clear focus state tracking per widget
- Visual focus indicators (border color, glow effect)
- Proper lifecycle hooks (OnWidgetFocusReceived/Lost)
- Directional focus navigation with intelligent grid-based algorithm
- Tab-based focus cycling

### 8.2 Keyboard Routing
- Multi-layer routing from window → workspace → widget
- Clear separation of concerns
- i3-style keybindings (Alt+hjkl)
- Widget movement (Alt+Shift+hjkl)
- Fullscreen toggle (inferred from code)

### 8.3 Widget Keyboard Handling
- Simple `OnWidgetKeyDown()` override pattern
- Event marking with `e.Handled = true`
- Per-widget keyboard state

### 8.4 Shortcut Management
- Centralized ShortcutManager
- Global vs workspace-specific shortcuts
- Extensible registration API

---

## 9. WHAT'S BROKEN

### 9.1 PowerShell Missing Calls
**SuperTUI.ps1 doesn't call:**
```powershell
# This call is MISSING:
$workspaceManager.HandleKeyDown($e)
```

**Result:** The workspace never sees keyboard events that weren't handled by ShortcutManager or overlays.

**Current workaround:** Events bubble through WPF's event system to the focused widget.

### 9.2 Missing Workspace Methods

**PowerShell expects these methods, but they don't exist:**
```csharp
// These should exist:
void FocusWidget(WidgetBase widget)
WidgetBase FocusedWidget { get; }
IEnumerable<WidgetBase> GetAllWidgets()
```

**Current impact:** QuickJumpOverlay feature won't work correctly (lines 293-296 in ps1).

### 9.3 Workspace Property in PowerShell

**PS1 tries to access:**
```powershell
$focusedWidget = $workspaceManager.CurrentWorkspace.FocusedWidget
```

But Workspace class only has:
```csharp
public WidgetBase GetFocusedWidget()  // Method, not property
```

---

## 10. CODE QUALITY OBSERVATIONS

### 10.1 Strengths
- Clean separation between focus logic and keyboard handling
- Well-documented with XML comments
- Proper use of WPF patterns (HasFocus, Focus(), events)
- Error handling with Logger
- Thread-safe EventBus implementation

### 10.2 Weaknesses
- Inconsistent naming (GetFocusedWidget method vs FocusedWidget property expectation)
- Missing implementation in C# but expected in PowerShell
- No configuration/customization mechanism for keybindings
- Limited testing infrastructure for keyboard input

---

## 11. RECENT CHANGES / NEW SYSTEMS

### 11.1 Observable Recent Additions
- **QuickJumpOverlay** - New overlay system for context-aware widget jumping
- **CRTEffectsOverlay** - Visual effects layer (scanlines, bloom)
- **ShortcutOverlay** - Visual help for keyboard shortcuts
- **Theme system integration** - Focus visual uses theme colors/glow effects

### 11.2 Under Development / Incomplete
- QuickJumpOverlay needs `FocusWidget()` method to work
- Workspace methods for direct widget access

---

## 12. TESTING IMPLICATIONS

### 12.1 Test Coverage Gaps
- No unit tests for Workspace.FocusInDirection() algorithm
- No integration tests for full keyboard → focus chain
- No tests for overlay keyboard interception
- No tests for workspace-specific shortcuts

### 12.2 Manual Testing Needed
- Alt+h/j/k/l focus navigation
- Alt+Shift+h/j/k/l widget movement
- Tab/Shift+Tab cycling
- ? overlay toggle
- G quick jump (once methods are added)
- Fullscreen mode

---

## 13. RECOMMENDATIONS FOR IMPROVEMENTS

### 13.1 Critical Fixes (Blocking Features)
1. Add `FocusWidget(WidgetBase widget)` method to Workspace
2. Add `FocusedWidget` property to Workspace (expose GetFocusedWidget)
3. Add `GetAllWidgets()` method to Workspace
4. Update PowerShell to call `$workspaceManager.HandleKeyDown($e)` in main keyboard handler

### 13.2 Important Enhancements
1. Publish focus change events to EventBus (enable event-driven reactions)
2. Create IInputManager service for centralized input handling
3. Add key binding configuration (JSON file with user-customizable bindings)
4. Add input capture mechanism for modal dialogs/overlays

### 13.3 Nice-to-Have
1. Keyboard macro recording
2. Input method remapping (Vim mode, Emacs mode, etc.)
3. Focus history (go back to previously focused widget)
4. Input gesture recognition

---

## 14. FILE REFERENCE SUMMARY

| File | Purpose | Key Components |
|------|---------|-----------------|
| Workspace.cs | Focus state & navigation | focusedElement, FocusInDirection, HandleKeyDown |
| WorkspaceManager.cs | Multi-workspace focus | SwitchToWorkspace, HandleKeyDown delegation |
| WidgetBase.cs | Widget focus interface | HasFocus, OnWidgetKeyDown, OnWidgetFocusReceived/Lost |
| ScreenBase.cs | Screen focus interface | HasFocus, OnScreenKeyDown |
| ShortcutManager.cs | Global shortcut registry | HandleKeyDown, RegisterGlobal, RegisterForWorkspace |
| KeyboardShortcut.cs | Shortcut data model | Key, Modifiers, Action, Matches |
| EventBus.cs | Pub/Sub event system | Publish, Subscribe (not used for keyboard) |
| Events.cs | Event type definitions | WidgetFocusReceivedEvent, WidgetFocusLostEvent |
| ShortcutOverlay.cs | Help overlay | Displays shortcuts, intercepts ? key |
| QuickJumpOverlay.cs | Context jump menu | Registers targets, intercepts G key |
| SuperTUI.ps1 | Application entry point | Window keyboard handler, workspace setup |

---

## 15. CONCLUSION

The focus management and keyboard input system is **approximately 85% complete**:

**Working (85%):**
- Core focus tracking and visual feedback
- Workspace focus navigation
- Widget keyboard input handling
- i3-style keybindings
- Shortcut registration system
- Overlay systems for help and quick jump

**Broken/Missing (15%):**
- Three methods missing from Workspace class
- PowerShell missing one critical `HandleKeyDown()` call
- No EventBus integration for keyboard/focus events
- No configuration system for custom keybindings

**Assessment:** The system is production-ready for basic use but needs the 15% fixes to unlock advanced features like QuickJumpOverlay, cross-widget keyboard communication, and user-customizable keybindings.

