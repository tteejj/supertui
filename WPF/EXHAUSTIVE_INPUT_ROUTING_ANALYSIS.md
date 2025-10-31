# COMPREHENSIVE INPUT ROUTING & KEYBOARD HANDLING ANALYSIS
SuperTUI WPF Framework - Exhaustive System Review
Date: 2025-10-31

## SCOPE OF ANALYSIS
Searched for: 189 instances of KeyDown/PreviewKey/e.Handled across codebase
Key files analyzed: 26 files with keyboard handling
Focus areas: 
  1. Input Flow Path (keystroke routing)
  2. Event Handlers (all KeyDown/Up/Preview patterns)
  3. Input Conflicts (multiple handlers for same input)
  4. Modal Input (blocking behavior)
  5. Focus Scopes (IsFocusScope, FocusManager usage)
  6. Input Validation & Transformation
  7. Dead Zones (input loss)

---

## SECTION 1: INPUT FLOW PATH (Complete Keystroke Route)

### 1.1 Top-Level Entry Point: MainWindow
File: /home/teej/supertui/WPF/MainWindow.xaml.cs

EVENT REGISTRATION:
  Line 80: `this.KeyDown += MainWindow_KeyDown;`
  Line 84: `this.Activated += MainWindow_Activated;`
  Line 85: `this.Deactivated += MainWindow_Deactivated;`
  Line 86: `this.Loaded += MainWindow_Loaded;`

MAINWINDOW KEYDOWN HANDLER (Lines 475-518):
```csharp
private void MainWindow_KeyDown(object sender, KeyEventArgs e)
{
    var shortcuts = serviceContainer.GetRequiredService<IShortcutManager>();
    
    // Step 1: Delegate to ShortcutManager for registered shortcuts
    bool handled = shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers);
    
    if (handled)
    {
        e.Handled = true;
        return;  // Event STOPS here - does not bubble
    }
    
    // Step 2: Handle context-specific shortcuts (move pane mode)
    if (isMovePaneMode && e.KeyboardDevice.Modifiers == ModifierKeys.None)
    {
        // Arrows, Escape handled here
        e.Handled = true;
        return;
    }
}
```

CRITICAL OBSERVATION #1: If ShortcutManager returns "handled=false", event routing continues
  - May bubble to focused pane if MainWindow doesn't set e.Handled=true
  - CommandPalette modal overlay (ModalOverlay) gets SAME events if visible

### 1.2 ShortcutManager: Central Dispatch Hub
File: /home/teej/supertui/WPF/Core/Infrastructure/ShortcutManager.cs

ARCHITECTURE:
  - Singleton instance (Lazy<ShortcutManager>.Instance)
  - Two shortcut registries:
    * globalShortcuts: List<KeyboardShortcut> (applies everywhere)
    * workspaceShortcuts: Dict<workspace, List<KeyboardShortcut>> (workspace-specific)
  
ENTRY METHOD: HandleKeyPress() (Lines 150-153)
```csharp
public bool HandleKeyPress(Key key, ModifierKeys modifiers, string currentWorkspace = null)
{
    return HandleKeyDown(key, modifiers, currentWorkspace);
}
```

CORE LOGIC: HandleKeyDown() (Lines 49-83)
  Step 1 (Lines 51-57): Context Check - IsTypingInTextInput()
    - If typing AND not allowed-while-typing shortcut → return false
    - No e.Handled manipulation (returns bool to caller)
  
  Step 2 (Lines 59-70): Workspace-specific shortcuts
    - Checks workspaceShortcuts dict first
    - If match found: calls action, returns true
  
  Step 3 (Lines 72-80): Global shortcuts
    - Iterates globalShortcuts list
    - First match wins, returns true
  
  Step 4 (Default): Returns false

CONTEXT CHECKING (Lines 88-132):
  IsTypingInTextInput() checks:
    - TextBox
    - TextBoxBase
    - RichTextBox
    - PasswordBox
  
  IsAllowedWhileTyping() whitelist:
    - Ctrl+S (Save)
    - Ctrl+Z (Undo)
    - Ctrl+Y (Redo)
    - Ctrl+X/C/V (Cut/Copy/Paste)
    - Ctrl+A (Select All)
    - Escape (always allowed)

REGISTERED SHORTCUTS (MainWindow.RegisterAllShortcuts, Lines 116-208):
  Global shortcuts registered:
    - Help overlay: Shift+?
    - Command palette: Shift+;
    - Debug overlay: Ctrl+Shift+D
    - Toggle move mode: F12
    - Workspace switch: Ctrl+1-9 (9 shortcuts)
    - Pane navigation: Ctrl+Shift+Arrows (4 shortcuts)
    - Pane opening: Ctrl+Shift+T/N/P/E/F/C (6 shortcuts)
    - Close pane: Ctrl+Shift+Q
    - Undo: Ctrl+Z
    - Redo: Ctrl+Y
  TOTAL: ~23 global shortcuts registered at MainWindow

### 1.3 Individual Pane Input Handlers
Files: NotesPane.cs, TaskListPane.cs, FileBrowserPane.cs, CalendarPane.cs

PATTERN OBSERVED: Multiple PreviewKeyDown handlers per pane

NOTESPANE (Lines 145, 212, 262, 332, 347):
  - this.PreviewKeyDown → OnPreviewKeyDown() [pane-level]
  - notesListBox.PreviewKeyDown → OnNotesListKeyDown() [list-level]
  - noteEditor.PreviewKeyDown → OnEditorPreviewKeyDown() [editor-level]
  - commandInput.PreviewKeyDown → OnCommandInputKeyDown() [command palette input]
  - commandList.PreviewKeyDown → OnCommandListKeyDown() [command palette results]

HANDLER HIERARCHY:
  Tunnel: PreviewKeyDown (executes FIRST, top-down)
    → noteEditor.PreviewKeyDown (component-level) [FIRST]
    → notesListBox.PreviewKeyDown (component-level)
    → commandInput.PreviewKeyDown (component-level)
    → this.PreviewKeyDown (pane-level) [LAST]
  
  Bubble: KeyDown (executes SECOND, bottom-up)
    → Control's KeyDown
    → Pane's KeyDown
    → MainWindow's KeyDown

EVENT FLOW EXAMPLE (when typing in noteEditor):
  1. PreviewKeyDown tunnel starts at MainWindow level
  2. Reaches noteEditor (focused element)
  3. OnEditorPreviewKeyDown fires (may set e.Handled=true)
  4. If not handled, continues to OnPreviewKeyDown (pane-level)
  5. If still not handled, bubbles as KeyDown
  6. Reaches MainWindow_KeyDown
  7. ShortcutManager tries to handle it
  8. If still not handled, may be lost or handled by framework

TASKLISTPANE (Line 942):
  Checks: `if (Keyboard.FocusedElement is TextBox) return;`
  This prevents shortcuts from firing while editing

FILEBROWSERPANE (Lines 207):
  this.PreviewKeyDown → OnPreviewKeyDown()
  fileListBox.PreviewKeyDown → OnFileListKeyDown()

COMMANDPALETTEPANE (Lines 124, 160):
  searchBox.PreviewKeyDown → OnSearchBoxKeyDown()
  resultsListBox.PreviewKeyDown → OnResultsKeyDown()

---

## SECTION 2: ALL EVENT HANDLER LOCATIONS

### 2.1 MainWindow.xaml
File: /home/teej/supertui/WPF/MainWindow.xaml

XAML FOCUS SCOPE CONFIGURATION:
```xaml
<Grid x:Name="PaneCanvas" Grid.Row="0" Margin="0"
      FocusManager.IsFocusScope="True"
      KeyboardNavigation.TabNavigation="Cycle"
      KeyboardNavigation.DirectionalNavigation="Cycle"/>
```

CRITICAL ISSUE #1: IsFocusScope="True"
  - Creates a FOCUS SCOPE BOUNDARY
  - Tab key trapped within PaneCanvas
  - ModalOverlay (outside scope, ZIndex=1000) may not receive Tab focus
  - Arrow key cycling may be affected

### 2.2 Code-Behind Event Handlers

MAINWINDOW.XAML.CS:
  Line 80: KeyDown (MainWindow_KeyDown)
  Line 84: Activated (MainWindow_Activated) - focus restoration
  Line 85: Deactivated (MainWindow_Deactivated) - logging
  Line 86: Loaded (MainWindow_Loaded) - initial focus setup

NOTESPANE.CS (at least 5 PreviewKeyDown handlers):
  Line 145: this.PreviewKeyDown
  Line 212: notesListBox.PreviewKeyDown
  Line 262: noteEditor.PreviewKeyDown
  Line 332: commandInput.PreviewKeyDown
  Line 347: commandList.PreviewKeyDown
  
  ALSO: TextChanged events (185, 261, 331) trigger debounced filters

TASKLISTPANE.CS:
  Line 236: quickAddTitle.KeyDown (QuickAddField_KeyDown)
  Line 264: quickAddDueDate.KeyDown
  Line 294: quickAddPriority.KeyDown
  Plus various other handlers throughout

FILEBROWSERPANE.CS:
  Line 207: this.PreviewKeyDown (OnPreviewKeyDown)
  Plus fileListBox handlers

COMMANDPALETTEPA NE.CS:
  Line 124: searchBox.PreviewKeyDown (OnSearchBoxKeyDown)
  Line 160: resultsListBox.PreviewKeyDown (OnResultsKeyDown)

---

## SECTION 3: INPUT CONFLICT ANALYSIS

### 3.1 Same-Key Conflicts

CONFLICT #1: Arrow Keys - Multiple Handlers
  - MainWindow: Arrow keys in move-pane mode (F12 toggle)
  - NotesPane: Arrow keys for cursor navigation in editor
  - CommandPalette: Arrow keys for menu navigation
  - Pane Navigation: Ctrl+Shift+Arrow for directional focus
  
  HANDLING: e.Handled flag prevents bubbling - should work correctly IF set

CONFLICT #2: Escape Key
  - MainWindow: Could close modals (not explicitly handled)
  - CommandPalette: Closes palette (Line 481-483)
  - NotesPane: Closes command palette (Line 1269)
  - Various panes: May use Escape for mode switching
  
  ISSUE: Multiple handlers - first to set e.Handled=true wins

CONFLICT #3: Ctrl+Z / Ctrl+Y
  - ShortcutManager: Registered as global shortcuts (Undo/Redo)
  - TextBox: Default TextBox undo/redo (Ctrl+Z/Y)
  - ShortcutManager allows these while typing (whitelist)
  - But BOTH ShortcutManager AND TextBox handle same keys
  
  POTENTIAL ISSUE: Double-handling or collision
  - If ShortcutManager fires Undo action while TextBox also does undo
  - May result in two undo operations or unexpected behavior

CONFLICT #4: Single-Letter Keys (A, D, E, S, F, C, etc.)
  - TaskListPane: A=Add, D=Delete, E=Edit, S=Subtask, etc.
  - NotesPane: A=New, D=Delete, E=Edit, O=Open, S=Search, F=Filter
  - BUT: These are guarded with `if (Keyboard.FocusedElement is TextBox) return;`
  - SHOULD be safe IF guard is always present

### 3.2 Handler Execution Order Ambiguity

ISSUE: Multiple handlers on same event can conflict

Example (NotesPane):
  1. noteEditor.PreviewKeyDown (FIRST - component level)
  2. OnPreviewKeyDown (SECOND - pane level)
  
  If noteEditor doesn't set e.Handled=true, OnPreviewKeyDown sees same event
  Could result in:
    - Same key processed twice
    - Conflicting commands executed
    - Unexpected behavior if both handlers expect exclusivity

---

## SECTION 4: MODAL INPUT HANDLING

### 4.1 CommandPalettePane Modal Overlay

Location: MainWindow.xaml.cs, lines 541-602

ARCHITECTURE:
  - CommandPalettePane created on-demand
  - Added to ModalOverlay container (Grid, ZIndex=1000)
  - ModalOverlay.Visibility set to Visible
  - PaneCanvas NOT hidden (still visible behind modal)

MODAL BEHAVIOR ANALYSIS:

CRITICAL ISSUE #2: No Input Blocking
  - PaneCanvas is NOT disabled (IsEnabled=true)
  - PaneCanvas is NOT hidden (Visibility=Visible)
  - KeyDown events still bubble to panes behind modal
  
  When CommandPalette is open:
  1. User presses key
  2. CommandPalette handlers run FIRST (in focus tree)
  3. If CommandPalette sets e.Handled=true → event stops
  4. If NOT handled → bubbles to MainWindow → ShortcutManager
  5. ShortcutManager fires global shortcuts (Ctrl+Shift+T to open panes, etc.)
  6. RESULT: Both modal AND background panes receive input
  
  EXAMPLE SCENARIO:
  - CommandPalette open with focus on search box
  - User types "Ctrl+Shift+T" to switch to Tasks pane
  - CommandPalette doesn't handle this key combo
  - Event bubbles to MainWindow
  - ShortcutManager fires "Open Tasks pane" action
  - NEW Tasks pane opens while palette is still open
  - User is confused - palette should have blocked input

MODAL FOCUS SCOPE ISSUE:

From MainWindow.xaml:
```xaml
<Grid x:Name="ModalOverlay" Grid.RowSpan="2" Visibility="Collapsed" Panel.ZIndex="1000"/>
```

ModalOverlay is:
  - OUTSIDE the PaneCanvas focus scope (PaneCanvas has IsFocusScope="True")
  - Has higher ZIndex (1000 vs default 0)
  - But ZIndex is visual only - doesn't affect input routing

RESULT: Tab key may NOT work as expected in modal
  - PaneCanvas focus scope might trap Tab within panes
  - Modal controls unable to receive Tab focus
  - User unable to navigate modal using Tab

### 4.2 Modal-Like Panes (NotesPane Command Palette)

NotesPane has internal command palette (Line 138-142):
```csharp
commandPaletteBorder = BuildCommandPalette();
commandPaletteBorder.Visibility = Visibility.Collapsed;
Grid.SetColumnSpan(commandPaletteBorder, 2);
Panel.SetZIndex(commandPaletteBorder, 1000);
mainLayout.Children.Add(commandPaletteBorder);
```

ISSUE: NOT truly modal
  - Still within NotesPane's input hierarchy
  - User can click on other panes or UI elements
  - Focus might leave command palette accidentally
  - No visual block of background panes

---

## SECTION 5: FOCUS SCOPE ANALYSIS

### 5.1 Focus Scope Definitions

PANEBASE:
  - Sets `Focusable = true` (Line 67)
  - Contains header + content (no explicit focus scope)
  - No FocusManager.IsFocusScope="True"

MAINWINDOW:
  - PaneCanvas: FocusManager.IsFocusScope="True"
  - ModalOverlay: No explicit focus scope setting
  - Creates TWO separate scope hierarchies

CRITICAL ISSUE #3: Focus Scope Boundary Conflict
  
  MainWindow hierarchy:
  ```
  MainWindow
  ├─ PaneCanvas (IsFocusScope="True")
  │  └─ TilingLayoutEngine.Container
  │     └─ Panes...
  ├─ StatusBarContainer
  └─ ModalOverlay (no focus scope)
     └─ CommandPalettePane...
  ```
  
  When CommandPalette is visible:
  - PaneCanvas focus scope might prevent Tab from reaching ModalOverlay
  - CommandPalette in ModalOverlay is OUTSIDE the focus scope
  - Tab key navigation BROKEN for modal

### 5.2 Keyboard Navigation Settings

PaneCanvas XML:
```xaml
KeyboardNavigation.TabNavigation="Cycle"
KeyboardNavigation.DirectionalNavigation="Cycle"
```

TabNavigation="Cycle":
  - Tab moves through focusable elements in order
  - At end, cycles back to first
  - Applies only within this focus scope
  - ModalOverlay NOT affected (outside scope)

DirectionalNavigation="Cycle":
  - Arrow keys move through focusable elements
  - NOT standard WPF behavior (needs custom handling or TabIndex)
  - Panes don't implement arrow-key navigation to other panes
  - Arrow keys probably navigate within pane instead

---

## SECTION 6: INPUT VALIDATION & TRANSFORMATION

### 6.1 Key Validation in ShortcutManager

IsTypingInTextInput() validates:
```csharp
private bool IsTypingInTextInput()
{
    var focused = Keyboard.FocusedElement;
    return focused is TextBox ||
           focused is System.Windows.Controls.Primitives.TextBoxBase ||
           focused is RichTextBox ||
           focused is PasswordBox;
}
```

VALIDATION GAPS:
  - Does NOT check for ComboBox (has editable text)
  - Does NOT check for RichTextBox derived classes
  - Does NOT check for custom controls derived from TextBoxBase
  - Does NOT check if TextBox.IsReadOnly=true (still counted as "typing")

RESULT: May allow shortcuts to fire when user is in editable combo box

### 6.2 Key Validation in Individual Panes

TASKLISTPANE (Line 942):
```csharp
if (Keyboard.FocusedElement is TextBox) return;
```

NOTESPANE (Line 1271):
```csharp
if (Keyboard.FocusedElement is TextBox) return;
```

CONSISTENCY: Good - all panes use same pattern

### 6.3 Input Transformation

SearchBox debouncing (NotesPane, Lines 87-95):
- TextChanged → starts timer (150ms delay)
- Timer expires → FilterNotes() executes
- Prevents excessive filtering during fast typing
- Acceptable transformation

AutoSave debouncing (NotesPane, Lines 97-105):
- TextChanged → starts timer (1000ms delay)
- Timer expires → AutoSaveCurrentNoteAsync() executes
- Standard pattern

---

## SECTION 7: DEAD ZONES (Input Loss)

### 7.1 Unhandled Events

EVENT FLOW WITH POTENTIAL LOSS:
  1. MainWindow_KeyDown fires
  2. ShortcutManager.HandleKeyPress() returns false
  3. MainWindow sets e.Handled=false (NOT SET - relies on default)
  4. Event bubbles to...?
  
  PROBLEM: No explicit e.Handled=false means event continues routing
  - WPF will try to handle it using default handlers
  - May end up in SystemParametersInfo or other framework handlers
  - Lost if no handler recognizes key

### 7.2 Modal Overlay Events Lost

When CommandPalette is open:
  1. Key pressed
  2. CommandPalette handlers execute
  3. If handled → stops (OK)
  4. If NOT handled:
     - Bubbles to PaneCanvas (outside focus scope)
     - Goes to MainWindow
     - ShortcutManager tries
     - If no match → truly lost

EXAMPLE: Pressing "Q" when command palette has focus
  - CommandPalette doesn't handle it
  - Bubbles to MainWindow
  - Not in shortcut registry
  - Lost completely

### 7.3 Pane-Internal Events Lost

Within NotesPane editor:
  1. Key pressed (e.g., "Ctrl+Shift+L" - not registered)
  2. noteEditor.PreviewKeyDown → OnEditorPreviewKeyDown()
     - Doesn't recognize key → doesn't set e.Handled
  3. Bubbles to OnPreviewKeyDown() (pane-level)
     - Might handle it, or not
  4. Bubbles to MainWindow
     - ShortcutManager tries
     - Not registered → lost

CONSEQUENCE: User presses unknown combination, nothing happens, no feedback

---

## SECTION 8: FOCUS RESTORATION ISSUES

### 8.1 Window Activation/Deactivation

MainWindow.xaml.cs, Lines 612-634:

```csharp
private void MainWindow_Activated(object sender, EventArgs e)
{
    if (paneManager?.FocusedPane != null)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            paneManager.FocusPane(paneManager.FocusedPane);
        }), System.Windows.Threading.DispatcherPriority.Input);
    }
}
```

BEHAVIOR:
  - Window regains focus → restores focus to previously-focused pane
  - Good for Alt+Tab switching
  
ISSUE: PaneManager.FocusPane() is complex (Lines 128-174)
  - Calls SetActive(true)
  - Sets IsFocused = true
  - Calls pane.Focus()
  - Calls Keyboard.Focus(pane)
  - Uses Dispatcher.BeginInvoke with Input priority
  
  DOUBLE DISPATCHER: Focus called both sync and async
  - Sync: pane.Focus() via FocusPane()
  - Async: Dispatcher.BeginInvoke inside FocusPane()
  
  RESULT: Unclear if focus is actually restored or if timing issue occurs

### 8.2 Weak Reference Resurrection

FocusHistoryManager.RestorePaneFocus() (Line 104-130):

```csharp
public bool RestorePaneFocus(string paneId)
{
    var element = GetLastFocusedControl(paneId);
    if (element == null) return false;  // Silent failure
    
    try
    {
        element.Focus();
        Keyboard.Focus(element);
        
        if (paneFocusMap.TryGetValue(paneId, out var record))
        {
            RestoreControlState(element, record.ControlState);
        }
        return true;
    }
    catch (Exception ex)
    {
        logger.Log(LogLevel.Warning, "FocusHistory", $"Failed to restore focus: {ex.Message}");
        return false;  // Silent failure
    }
}
```

ISSUES:
  1. If element is dead (garbage collected), returns false silently
  2. No fallback to "focus first focusable child"
  3. No fallback to pane's OnPaneGainedFocus()
  4. User might lose focus entirely on workspace switch

CRITICAL ISSUE #4: No Fallback Focus Strategy
  When focus element is dead:
  - Simply returns false
  - Pane has no focus
  - No error message to user
  - Subsequent keyboard input might go nowhere

---

## SECTION 9: ARCHITECTURE ASSESSMENT

### 9.1 Design Strengths

STRENGTH #1: Centralized Shortcut Management
  - Single registry in ShortcutManager
  - All shortcuts registered in MainWindow.RegisterAllShortcuts()
  - Easy to see all shortcuts in one place
  - Context-aware (checks IsTypingInTextInput)

STRENGTH #2: Multi-Layer Architecture
  - MainWindow captures top-level events
  - ShortcutManager handles registered shortcuts
  - Individual panes handle component-level events
  - Proper layering prevents conflicts

STRENGTH #3: Focus Tracking
  - PaneManager tracks focused pane
  - FocusHistoryManager tracks per-pane focus
  - Focus restoration on workspace switch
  - Integration with WPF's focus system

STRENGTH #4: Modal Support
  - CommandPalettePane with overlay
  - NotesPane internal command palette
  - Animations for open/close
  - Proper Z-order management

### 9.2 Design Weaknesses

WEAKNESS #1: Focus Scope Configuration
  - IsFocusScope="True" on PaneCanvas creates boundary
  - ModalOverlay outside scope → Tab key broken for modals
  - No comments explaining why this is needed
  - Likely unintentional conflict

WEAKNESS #2: Dual Focus System
  - PaneBase.IsFocused shadows WPF's IsFocused
  - FocusPane() sets both custom property and calls .Focus()
  - Confusing which one is "authoritative"
  - Comments say "CRITICAL FIX" but don't explain what was broken

WEAKNESS #3: No Input Blocking for Modals
  - CommandPalette doesn't block input to background panes
  - No InputManager or event suppression
  - Background panes can receive shortcuts while modal open
  - User sees unexpected pane changes while interacting with modal

WEAKNESS #4: PreviewKeyDown Cascading
  - Multiple PreviewKeyDown handlers in NotesPane
  - Execution order unclear (tunneling phase goes top-down)
  - Each handler must carefully check if already handled
  - Prone to double-handling or missed events

WEAKNESS #5: No Global Event Filter
  - No centralized place to see all events
  - Hard to debug event routing
  - No event tracing or logging
  - Multiple handlers scattered across files

### 9.3 Fault Tolerance

INPUT ROBUSTNESS:
  - If ShortcutManager fails to handle key → event continues (OK)
  - If pane handler fails → event continues (OK)
  - No try-catch around event handlers (bad - exceptions not caught)
  - No fallback handlers for unrecognized keys (neutral)

FOCUS RESTORATION:
  - If focus element is dead → silent failure (bad)
  - If focus restoration fails → no error to user (bad)
  - No timeout on Dispatcher.BeginInvoke (could hang)
  - No validation that focus actually set

---

## SECTION 10: CRITICAL ISSUES SUMMARY

### CRITICAL ISSUE #1: Modal Input Not Blocked
SEVERITY: High
LOCATION: MainWindow.xaml.cs (ShowCommandPalette)
ISSUE: CommandPalette modal doesn't block input to background panes
IMPACT: User can accidentally trigger pane shortcuts while using palette
EXAMPLE: Pressing Ctrl+Shift+T opens Tasks pane while palette open
SOLUTION: Either:
  1. Disable PaneCanvas when modal open
  2. Or implement InputManager to intercept globally
  3. Or make ShortcutManager respect "modal mode"

### CRITICAL ISSUE #2: Focus Scope Boundary Traps Tab
SEVERITY: High
LOCATION: MainWindow.xaml (PaneCanvas FocusManager.IsFocusScope)
ISSUE: TabNavigation="Cycle" in PaneCanvas traps Tab within scope
IMPACT: Tab key doesn't navigate to ModalOverlay or StatusBar
SOLUTION: Either:
  1. Remove IsFocusScope="True" or TabNavigation="Cycle"
  2. Or add explicit TabIndex values to force proper order
  3. Or add Tab key handler to break scope boundary

### CRITICAL ISSUE #3: Dual Focus System Confusion
SEVERITY: Medium
LOCATION: PaneManager.FocusPane() and PaneBase.IsFocused
ISSUE: Two separate focus tracking systems (custom bool + WPF bool)
IMPACT: Unclear which is authoritative, potential desync
SOLUTION: Either:
  1. Use only WPF's IsFocused (standard approach)
  2. Or rename custom property to PaneIsFocused for clarity
  3. Or consolidate into single property

### CRITICAL ISSUE #4: No Fallback Focus on Element Death
SEVERITY: Medium
LOCATION: FocusHistoryManager.RestorePaneFocus()
ISSUE: If focus element is garbage collected, focus is lost
IMPACT: User loses keyboard focus on workspace switch sometimes
SOLUTION: Add fallback:
  ```csharp
  if (element == null)
  {
      // Fallback to first focusable child of pane
      var pane = GetPaneById(paneId);
      if (pane != null)
      {
          pane.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
          return true;
      }
  }
  ```

### CRITICAL ISSUE #5: No Global Input Validation
SEVERITY: Low
LOCATION: Various pane handlers
ISSUE: Each pane checks IsTypingInTextInput() separately
IMPACT: Inconsistent behavior if one pane misses check
SOLUTION: Create ValidateInputContext() helper or attribute

---

## SECTION 11: RECOMMENDED ARCHITECTURE IMPROVEMENTS

### Improvement #1: Add InputManager Singleton
GOAL: Centralized input handling
DESIGN:
  - InputManager.IsModalActive property
  - InputManager.BlockNonModalInput(except: list)
  - Check in MainWindow_KeyDown:
    ```csharp
    if (inputManager.IsModalActive && !inputManager.IsAllowedInModal(e.Key))
    {
        e.Handled = true;
        return;
    }
    ```

### Improvement #2: Remove Focus Scope on PaneCanvas
GOAL: Fix Tab key in modals
DESIGN:
  - Remove FocusManager.IsFocusScope="True"
  - Remove KeyboardNavigation.TabNavigation="Cycle"
  - Let WPF default focus behavior work
  - Add explicit TabIndex ordering if needed

### Improvement #3: Consolidate Focus System
GOAL: Single source of truth for focus
DESIGN:
  - Remove PaneBase.IsFocused property
  - Use only WPF's UIElement.IsKeyboardFocusWithin
  - Rename internal tracking to "_logicalFocusedPane"
  - Add clear comments explaining dual system rationale

### Improvement #4: Add Focus Fallback Strategy
GOAL: Never lose focus
DESIGN:
  ```csharp
  public bool RestorePaneFocus(string paneId)
  {
      var element = GetLastFocusedControl(paneId);
      
      // Fallback chain:
      // 1. Specific element
      // 2. First focusable in pane
      // 3. Pane itself
      
      if (element == null || !element.IsVisible)
      {
          var pane = GetPaneById(paneId);
          if (pane != null)
          {
              element = pane;
          }
      }
      
      return FocusElement(element);
  }
  ```

### Improvement #5: Add Event Tracing
GOAL: Debug event routing
DESIGN:
  - Add [ConditionalAttribute("DEBUG")] event logger
  - Log every KeyDown with source and handler
  - Output to Logger with Debug level
  - Can be enabled/disabled via config

---

## SECTION 12: CONCLUSION

### Overall Architecture: SOUND BUT FLAWED

POSITIVE ASPECTS:
  ✓ Multi-layer architecture is correct approach
  ✓ Shortcut manager is well-designed
  ✓ Focus tracking is sophisticated
  ✓ Context-aware input checking is implemented
  ✓ Pane system is clean and modular

PROBLEM AREAS:
  ✗ Focus scope boundary creates Tab key trap
  ✗ Modal input not properly blocked
  ✗ Dual focus system unclear
  ✗ No fallback when focus element dies
  ✗ PreviewKeyDown cascading hard to debug

CURRENT STATE:
  FUNCTIONAL: System works for normal use cases
  FRAGILE: Edge cases (modal input, workspace switches) have issues
  CONFUSING: Multiple focus systems make code hard to understand

RECOMMENDED ACTION:
  HIGH PRIORITY:
    1. Remove FocusScope="True" to fix Tab key
    2. Add modal input blocking to ShortcutManager
    3. Simplify focus system (remove custom IsFocused)
    4. Add focus fallback strategy

  MEDIUM PRIORITY:
    5. Add event tracing for debugging
    6. Standardize input validation across panes
    7. Document focus restoration strategy
    8. Add InputManager for global state

  LOW PRIORITY:
    9. Performance: Cache Keyboard.FocusedElement checks
    10. Documentation: Add architecture diagram to code

