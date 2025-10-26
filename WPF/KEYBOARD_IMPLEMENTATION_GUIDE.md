# SuperTUI Keyboard/Focus Implementation Guide

Based on analysis of previous TUI implementation patterns. This is a practical guide for implementing keyboard handling in the WPF-based SuperTUI.

---

## Quick Reference: Input Handling Flow

```
User presses key
    ↓
MainWindow receives KeyDown/PreviewKeyDown event
    ↓
Route to focused Widget/Control
    ↓
Check if widget IsFocused
    ↓ NO
    Return false (don't consume)
    ↓ YES
Check current mode (edit, search, normal)
    ↓
Match key against mode-specific handlers
    ↓ Match found
    Execute handler, return true (consume event)
    ↓ No match
    Return false (don't consume)
    ↓
Update Status Bar with mode/instructions
```

---

## Implementation Patterns

### 1. Focus System (Simple)

**Pattern:** Single boolean flag, checked first thing

```csharp
public class MyWidget : UserControl
{
    private bool isFocused = true;
    
    public bool IsFocused 
    { 
        get => isFocused;
        set
        {
            if (isFocused != value)
            {
                isFocused = value;
                UpdateVisualFeedback();  // Highlight, border, etc.
            }
        }
    }
    
    // Handle input from MainWindow or parent
    public bool HandleKeyDown(KeyEventArgs e)
    {
        if (!IsFocused) return false;  // First line of defense
        
        // Now we know we're focused, match keys
        switch (e.Key)
        {
            case Key.Up:
                MoveUp();
                e.Handled = true;
                return true;
            case Key.Down:
                MoveDown();
                e.Handled = true;
                return true;
            default:
                return false;
        }
    }
    
    private void UpdateVisualFeedback()
    {
        // Change border color, shadow, highlight, etc.
        if (isFocused)
        {
            BorderBrush = new SolidColorBrush(Colors.Cyan);
            Background = new SolidColorBrush(Color.FromRgb(0, 30, 60));
        }
        else
        {
            BorderBrush = new SolidColorBrush(Colors.DarkGray);
            Background = new SolidColorBrush(Colors.Black);
        }
    }
}
```

---

### 2. Input Mode System (State Machine)

**Pattern:** Mode flag gates all input, status bar shows instructions

```csharp
public class EditableWidget : UserControl
{
    // Mode state
    private bool isEditMode = false;
    private string editBuffer = "";
    private int cursorPosition = 0;
    private object originalValue = null;
    
    public bool IsEditMode => isEditMode;
    
    public void StartEdit(object currentValue)
    {
        isEditMode = true;
        editBuffer = currentValue?.ToString() ?? "";
        originalValue = currentValue;
        cursorPosition = editBuffer.Length;
        
        // Show mode instructions in status bar
        StatusService.ShowStatus(
            "EDIT MODE: Enter=Save, Esc=Cancel, Arrows=Navigate, Ctrl+A=Select All",
            StatusType.Info);
    }
    
    public bool HandleKeyDown(KeyEventArgs e)
    {
        if (!IsFocused) return false;
        
        // Mode check: different behavior based on mode
        if (isEditMode)
            return HandleEditModeInput(e);
        else
            return HandleNormalModeInput(e);
    }
    
    private bool HandleNormalModeInput(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.E:
                StartEdit(CurrentValue);
                e.Handled = true;
                return true;
            case Key.Up:
                MoveUp();
                e.Handled = true;
                return true;
            case Key.Down:
                MoveDown();
                e.Handled = true;
                return true;
            default:
                return false;
        }
    }
    
    private bool HandleEditModeInput(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                SaveEdit();
                e.Handled = true;
                return true;
            case Key.Escape:
                CancelEdit();
                e.Handled = true;
                return true;
            case Key.Left:
                if (cursorPosition > 0) cursorPosition--;
                e.Handled = true;
                return true;
            case Key.Right:
                if (cursorPosition < editBuffer.Length) cursorPosition++;
                e.Handled = true;
                return true;
            case Key.Home:
                cursorPosition = 0;
                e.Handled = true;
                return true;
            case Key.End:
                cursorPosition = editBuffer.Length;
                e.Handled = true;
                return true;
            case Key.Back:
                if (cursorPosition > 0)
                {
                    editBuffer = editBuffer.Remove(cursorPosition - 1, 1);
                    cursorPosition--;
                }
                e.Handled = true;
                return true;
            default:
                // Handle printable characters
                if (e.Key >= Key.A && e.Key <= Key.Z)
                {
                    editBuffer = editBuffer.Insert(cursorPosition, e.Key.ToString());
                    cursorPosition++;
                    e.Handled = true;
                    return true;
                }
                return false;
        }
    }
    
    private void SaveEdit()
    {
        // Validate and apply
        CurrentValue = editBuffer;
        isEditMode = false;
        StatusService.ShowStatus($"Saved: {editBuffer}", StatusType.Success);
    }
    
    private void CancelEdit()
    {
        isEditMode = false;
        editBuffer = "";
        StatusService.ShowStatus("Edit cancelled", StatusType.Info);
    }
}
```

---

### 3. Preventing Controls from Stealing Focus

**Pattern 1: PreviewKeyDown with e.Handled = true**

```csharp
// In MainWindow.xaml.cs or behavior
private void OnPreviewKeyDown(object sender, KeyEventArgs e)
{
    // Handle critical navigation keys BEFORE controls see them
    switch (e.Key)
    {
        case Key.Home:
            NavigateToPreviousWidget();
            e.Handled = true;  // Consumed - control won't see it
            return;
        case Key.End:
            NavigateToNextWidget();
            e.Handled = true;
            return;
        case Key.Tab:
            // Tab navigation is yours to control
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                NavigateToPreviousWidget();
            else
                NavigateToNextWidget();
            e.Handled = true;
            return;
    }
}
```

**Pattern 2: Don't use Tab for field cycling in edit mode**

```csharp
// In edit mode, Tab means "next field" not "next control"
if (isEditMode && e.Key == Key.Tab)
{
    MoveToNextField();
    e.Handled = true;  // Prevent WPF's tab navigation
    return true;
}
```

**Pattern 3: XAML - Make control not focusable**

```xaml
<TreeView Focusable="False" IsTabStop="False" />
```

---

### 4. Status Bar with Mode Indicators

**Pattern:** Always show current mode and instructions

```csharp
public class StatusBarControl : UserControl
{
    public static readonly DependencyProperty CurrentModeProperty =
        DependencyProperty.Register("CurrentMode", typeof(string), typeof(StatusBarControl),
            new PropertyMetadata("Normal"));
    
    public string CurrentMode
    {
        get => (string)GetValue(CurrentModeProperty);
        set => SetValue(CurrentModeProperty, value);
    }
    
    public void ShowModeInstructions(string mode, string instructions)
    {
        CurrentMode = mode;
        InstructionText.Text = instructions;
    }
}

// Usage:
StatusBar.ShowModeInstructions("EDIT", 
    "Enter=Save, Esc=Cancel, Arrows=Move cursor, Ctrl+A=Select All");

StatusBar.ShowModeInstructions("SEARCH",
    "Type to search, Enter=Jump, Esc=Cancel, Up/Down=Previous/Next match");

StatusBar.ShowModeInstructions("NORMAL",
    "J/K=Down/Up, H/L=Expand/Collapse, E=Edit, D=Delete, Q=Quit");
```

---

### 5. Navigation with Focus Tracking

**Pattern:** Explicitly set focus and scroll into view

```csharp
public void FocusWidget(int widgetIndex)
{
    // Remove focus from current
    if (focusedWidget != null)
    {
        focusedWidget.IsFocused = false;
    }
    
    // Set focus to new widget
    focusedWidget = widgets[widgetIndex];
    focusedWidget.IsFocused = true;
    
    // Ensure it's visible (WPF TreeView equivalent)
    focusedWidget.BringIntoView();
    
    // Update status bar
    UpdateStatusBar();
}

public void NavigateToNextWidget()
{
    int nextIndex = (currentFocusIndex + 1) % widgets.Count;
    FocusWidget(nextIndex);
}

public void NavigateToPreviousWidget()
{
    int prevIndex = (currentFocusIndex - 1 + widgets.Count) % widgets.Count;
    FocusWidget(prevIndex);
}
```

---

### 6. Keyboard Shortcuts Map

**Recommended Pattern:**

```
NORMAL MODE
-----------
Navigation:
  J/K         - Down/Up (Vim-like)
  H/L         - Collapse/Expand
  G/Shift+G   - First/Last
  Ctrl+Home   - Top
  Ctrl+End    - Bottom
  Page Up/Dn  - Page navigation
  
Commands:
  N           - New item
  E           - Edit selected
  D           - Delete selected
  /           - Search
  ?           - Help
  Q           - Quit
  
Window:
  Tab         - Next widget/window
  Shift+Tab   - Previous widget/window
  Alt+N       - Switch to window N (1-9)
  
EDIT MODE
---------
  Enter       - Save
  Esc         - Cancel
  Tab         - Next field
  Shift+Tab   - Previous field
  Arrows      - Move cursor
  Ctrl+A      - Select all
  Ctrl+C/V    - Copy/Paste (if supported)
  
SEARCH MODE
-----------
  Type        - Add to search
  Enter       - Jump to match
  Up/Down     - Previous/Next match
  Esc         - Cancel search
```

**Implementation:**

```csharp
public class KeyboardShortcuts
{
    private readonly IStatusService statusService;
    
    public Dictionary<Key, string> NormalModeHelp = new()
    {
        { Key.J, "Down" },
        { Key.K, "Up" },
        { Key.H, "Collapse" },
        { Key.L, "Expand" },
        { Key.N, "New" },
        { Key.E, "Edit" },
        { Key.D, "Delete" },
        { Key.Oem2, "Search" },  // / key
    };
    
    public void ShowHelp()
    {
        var help = string.Join(" | ", 
            NormalModeHelp.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        statusService.ShowStatus(help, StatusType.Info);
    }
}
```

---

### 7. Widget Container (Parent-Child Focus)

**Pattern:** Parent routes keys to focused child

```csharp
public class WidgetContainer : UserControl
{
    private List<MyWidget> widgets = new();
    private int focusedIndex = 0;
    
    public void Initialize()
    {
        // Create widgets
        widgets.Add(new FileExplorerWidget());
        widgets.Add(new TaskListWidget());
        widgets.Add(new NotesWidget());
        
        // Set initial focus
        widgets[0].IsFocused = true;
    }
    
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Inter-widget navigation (before delegating to widget)
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Tab:
                    FocusNextWidget();
                    e.Handled = true;
                    return;
                case Key.J:
                    FocusNextWidget();
                    e.Handled = true;
                    return;
                case Key.K:
                    FocusPreviousWidget();
                    e.Handled = true;
                    return;
            }
        }
        
        // Delegate to focused widget
        if (focusedIndex >= 0 && focusedIndex < widgets.Count)
        {
            if (widgets[focusedIndex].HandleKeyDown(e))
            {
                e.Handled = true;
                return;
            }
        }
        
        base.OnPreviewKeyDown(e);
    }
    
    private void FocusNextWidget()
    {
        widgets[focusedIndex].IsFocused = false;
        focusedIndex = (focusedIndex + 1) % widgets.Count;
        widgets[focusedIndex].IsFocused = true;
        StatusService.ShowStatus($"Focused: {widgets[focusedIndex].Name}", StatusType.Info);
    }
    
    private void FocusPreviousWidget()
    {
        widgets[focusedIndex].IsFocused = false;
        focusedIndex = (focusedIndex - 1 + widgets.Count) % widgets.Count;
        widgets[focusedIndex].IsFocused = true;
        StatusService.ShowStatus($"Focused: {widgets[focusedIndex].Name}", StatusType.Info);
    }
}
```

---

## Checklist for Implementation

- [ ] All widgets have `IsFocused` boolean property
- [ ] All widgets have `HandleKeyDown(KeyEventArgs)` method
- [ ] First line of HandleKeyDown checks `if (!IsFocused) return false;`
- [ ] Mode flags gate input handling (edit, search, normal)
- [ ] Status bar always shows current mode and instructions
- [ ] PreviewKeyDown used for critical keys (Home, End, Tab, etc.)
- [ ] `e.Handled = true` set when input is consumed
- [ ] Visual feedback for focus state (color, border, highlight)
- [ ] Keyboard shortcuts documented in status bar
- [ ] No global input bindings in MainWindow
- [ ] Focus explicitly set when navigating between widgets
- [ ] BringIntoView() called when focus changes
- [ ] Edit mode shows different instructions than normal mode
- [ ] Escape cancels edit mode and restores original values
- [ ] Enter saves edit mode changes

---

## Common Pitfalls to Avoid

1. **Don't consume input if not focused**
   - Bad: Always return true
   - Good: `if (!IsFocused) return false;`

2. **Don't nest mode checks too deep**
   - Bad: `if (isEditMode) { if (isSearchMode) { ... } }`
   - Good: One mode flag, switch between them

3. **Don't forget e.Handled = true**
   - Bad: Process key but don't mark as handled
   - Good: Always set `e.Handled = true` when consuming

4. **Don't use Tab for navigation in edit mode**
   - Bad: Tab moves to next control
   - Good: Tab moves to next field, consume with `e.Handled = true`

5. **Don't forget status bar instructions**
   - Bad: User doesn't know what mode they're in
   - Good: Status bar always shows mode and available keys

6. **Don't create complex focus stacks**
   - Bad: Focus history, focus stack, focus navigation tree
   - Good: Single `IsFocused` flag, change it when navigating

7. **Don't handle all keys in MainWindow**
   - Bad: One giant MainWindow keyboard handler
   - Good: Route to focused widget, let widget decide

