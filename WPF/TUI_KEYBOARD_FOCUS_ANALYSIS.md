# Previous TUI Implementation Analysis - Keyboard/Focus Patterns

## Overview
Analyzed the previous TUI implementation in `/home/teej/_tui/praxis-main` to understand keyboard input handling, focus management, and input modes. This project transitioned from console-based TUI to WPF while maintaining terminal-like aesthetics.

---

## 1. Global Keyboard Input Architecture

### Console-Based (TaskProPro)
**File:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/Core/InputManager.cs`

```csharp
public static class InputManager
{
    public static InputEvent ReadInput()
    {
        var keyInfo = Console.ReadKey(true);  // true = don't echo
        return InputEvent.FromConsoleKeyInfo(keyInfo);
    }
    
    public static bool IsInputAvailable()
    {
        return Console.KeyAvailable;  // Non-blocking check
    }
}
```

**Key Pattern:** 
- Blocking read loop using `Console.ReadKey(true)` with intercept flag
- Non-blocking availability check with `Console.KeyAvailable`
- Delegates input handling to component handlers
- **Critical learning:** Don't try to intercept at application level in console—delegate to focused widget

### WPF Version (PraxisWpf)
**File:** `/home/teej/_tui/praxis-main/_wpf/MainWindow.xaml`

```xaml
<!-- NO Window.InputBindings - all key handling at data level -->
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    
    <Border Grid.Row="0">...</Border>
    <taskViewer:TaskView Grid.Row="1" DataContext="{Binding TaskViewModel}"/>
    <controls:StatusBar Grid.Row="2" DataContext="{Binding StatusService}"/>
</Grid>
```

**Key Pattern:**
- **NO global input bindings in MainWindow**
- Input handling pushed to individual controls (TaskView, StatusBar)
- Focus managed by WPF's natural keyboard navigation
- **Critical learning:** WPF's default Tab navigation is powerful—extend it rather than replace it

---

## 2. Focus Management System

### Console-Based Focus Tracking
**File:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/UI/ListWidget.cs`

```csharp
public class ListWidget<T>
{
    public bool IsFocused { get; set; } = true;
    
    public bool HandleInput(InputEvent input)
    {
        if (!IsFocused) return false;  // First check: are we focused?
        
        if (input.IsArrowUp)
        {
            MoveTo(selectedIndex - 1);
            return true;
        }
        // ... more handlers
    }
    
    public virtual void Render(ScreenBuffer screen, Rectangle bounds)
    {
        var isSelected = (itemIndex == selectedIndex) && IsFocused;  // Use focus in rendering
        RenderItem(screen, item, bounds.X, y, bounds.Width, isSelected, itemIndex);
    }
}
```

**Pattern:**
- Simple boolean flag `IsFocused`
- **Input handler returns immediately if not focused** (prevents consuming input)
- Focused widget visually highlighted (color change, border change)
- No complex focus stack—single focused widget at a time

### WPF Behavior-Based Navigation
**File:** `/home/teej/_tui/praxis-main/_wpf/Behaviors/KeyboardNavigationBehavior.cs`

```csharp
public static class KeyboardNavigationBehavior
{
    public static readonly DependencyProperty EnableEnhancedNavigationProperty =
        DependencyProperty.RegisterAttached(
            "EnableEnhancedNavigation",
            typeof(bool),
            typeof(KeyboardNavigationBehavior),
            new PropertyMetadata(false, OnEnableEnhancedNavigationChanged));

    private static void OnEnhancedNavigationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TreeView treeView)
        {
            if ((bool)e.NewValue)
            {
                treeView.PreviewKeyDown += OnTreeViewPreviewKeyDown;  // Preview phase
                treeView.KeyDown += OnTreeViewKeyDown;  // Bubble phase
            }
        }
    }
    
    private static void OnTreeViewPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Home:
                NavigateToFirst(treeView);
                e.Handled = true;  // Prevent WPF default handling
                break;
            // ...
        }
    }
}
```

**Pattern:**
- **Attached behavior** pattern (don't modify XAML for each control)
- **PreviewKeyDown (tunneling)** for critical keys (Home, End, PageUp, PageDown)
- **KeyDown (bubbling)** for application-specific shortcuts
- **e.Handled = true** to prevent WPF default handling
- Vim-like navigation: Ctrl+J (down), Ctrl+K (up), Ctrl+H (parent), Ctrl+L (child)

**Navigation State Tracking:**
```csharp
private static readonly Dictionary<TreeView, NavigationState> _navigationStates = new();

private class NavigationState
{
    public DateTime LastNavigationTime { get; set; }
    public Key LastNavigationKey { get; set; }
    public int RepeatCount { get; set; }
    public List<object> VisitedItems { get; set; } = new();  // For back navigation
}
```

---

## 3. Input Modes & Command Systems

### Edit Mode State Machine
**File:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/UI/TaskListEditMode.cs`

```csharp
public class TaskListEditMode
{
    private bool isEditMode = false;
    private string editingTaskId = "";
    private EditField currentField = EditField.None;
    private string editBuffer = "";
    
    public bool IsEditMode => isEditMode;
    
    public bool StartEdit(SimpleTask task, EditField field)
    {
        isEditMode = true;
        editingTaskId = task.Id;
        currentField = field;
        editBuffer = GetFieldValue(task, field);
        
        StatusBar?.ShowMessage($"Editing {field} - Enter=Save, Esc=Cancel, Tab=Next Field");
        return true;
    }
    
    public bool HandleEditInput(InputEvent input)
    {
        if (!isEditMode) return false;
        
        if (input.IsEnter)
            return SaveEdit();
        if (input.IsEscape)
            return CancelEdit();
        if (input.IsTab)
        {
            SaveCurrentField();
            MoveToNextField();
            return true;
        }
        if (input.IsBackspace && editBuffer.Length > 0)
        {
            editBuffer = editBuffer.Substring(0, editBuffer.Length - 1);
            return true;
        }
        if (input.IsPrintableChar)
        {
            editBuffer += input.Char;
            return true;
        }
        return false;
    }
    
    private void ExitEditMode()
    {
        isEditMode = false;
        editingTaskId = "";
        currentField = EditField.None;
        editBuffer = "";
    }
}
```

**Pattern:**
- **Mode flag** (`isEditMode`) gates all input handling
- **Input handlers return early if not in mode**
- **Tab/Shift+Tab cycles through fields** (not WPF Tab navigation)
- **Mode indicator in status bar** shows user they're in edit mode
- **Backup of original values** for Escape cancellation

### Inline Editor with Visual Cursor
**File:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/UI/InlineEditor.cs`

```csharp
public class InlineEditor
{
    private bool isActive = false;
    private EditMode currentMode = EditMode.None;
    private string editBuffer = "";
    private int cursorPosition = 0;
    
    // Visual cursor blinking
    private bool cursorVisible = true;
    private const int CursorBlinkInterval = 500;
    
    public bool HandleInput(InputEvent input)
    {
        if (!isActive) return false;
        
        if (input.IsEscape)
        {
            CancelEdit();
            return true;
        }
        
        if (input.IsEnter)
        {
            SaveEdit();
            return true;
        }
        
        // Text editing with cursor tracking
        if (input.IsBackspace && cursorPosition > 0)
        {
            editBuffer = editBuffer.Remove(cursorPosition - 1, 1);
            cursorPosition--;
            return true;
        }
        
        if (input.IsArrowLeft && cursorPosition > 0)
        {
            cursorPosition--;
            return true;
        }
        
        if (input.IsArrowRight && cursorPosition < editBuffer.Length)
        {
            cursorPosition++;
            return true;
        }
        
        // Word movement
        if (input.IsCtrlArrowLeft)
            cursorPosition = FindPreviousWordBoundary(editBuffer, cursorPosition);
        
        if (input.IsPrintableChar)
        {
            editBuffer = editBuffer.Insert(cursorPosition, input.Char.ToString());
            cursorPosition++;
            return true;
        }
        
        return false;
    }
}
```

**Pattern:**
- **Separate cursor position** from buffer
- **Word navigation with Ctrl+Arrow**
- **Blinking cursor** for visual feedback
- **Insert vs overstrike** modes possible
- **All input consumed** during edit (returns true)

### WPF Command Pattern
**File:** `/home/teej/_tui/praxis-main/_wpf/Services/RelayCommand.cs`

```csharp
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

// Usage in ViewModel:
public MainWindowViewModel(...)
{
    NewCommand = CreateCommand(ExecuteNew, CanExecuteNew);
    EditCommand = CreateCommand(ExecuteEdit, CanExecuteEdit);
    DeleteCommand = CreateCommand(ExecuteDelete, CanExecuteDelete);
}

private void ExecuteEdit()
{
    if (SelectedItem != null)
    {
        SelectedItem.IsInEditMode = !SelectedItem.IsInEditMode;
        _statusService.ShowStatus($"Switched to {mode} mode", StatusType.Info);
    }
}
```

**Pattern:**
- **Commands encapsulate actions** (New, Edit, Delete, Save, Expand, Collapse)
- **CanExecute predicate** determines if command is available
- **Command binding in XAML** (not code-behind keyboard handling)
- **Status service feedback** shows user the mode changed

---

## 4. Preventing WPF Controls from Stealing Focus

### Strategy 1: Attached Behavior with PreviewKeyDown
```csharp
private static void OnTreeViewPreviewKeyDown(object sender, KeyEventArgs e)
{
    // Handle in preview phase (tunneling) BEFORE control's default behavior
    switch (e.Key)
    {
        case Key.Home:
            NavigateToFirst(treeView);
            e.Handled = true;  // Consume the event
            break;
    }
}
```

**Why:** 
- PreviewKeyDown fires BEFORE control's KeyDown
- Setting `e.Handled = true` prevents further routing
- Control never sees the key

### Strategy 2: Don't Use Default Navigation Keys in Edit Mode
```csharp
public bool HandleInput(InputEvent input)
{
    if (!isEditMode) return false;  // Only consume in edit mode
    
    // Tab doesn't navigate to next control—cycles fields
    if (input.IsTab)
    {
        MoveToNextField();
        e.Handled = true;
        return true;
    }
}
```

### Strategy 3: Mark Control as Non-Focusable
In XAML:
```xaml
<TreeView Focusable="False" />  <!-- Prevents focus from shifting -->
```

Or in code:
```csharp
treeView.Focusable = false;
treeView.IsTabStop = false;
```

---

## 5. Workspace/Window Navigation System

### Console-Based: Simple Screen Buffer Model
The console implementation didn't have complex workspace navigation—it was single-screen focused.

### WPF Tab Navigation with Shortcuts
**File:** `/home/teej/_tui/praxis-main/_wpf/MainWindow.xaml`

```xaml
<!-- Keyboard shortcuts help shown in status bar -->
<StatusBar Grid.Row="2" Visibility="...">
    <StatusBarItem>
        <TextBlock Text="N: New | E: Edit | Del: Delete | +/-: Expand/Collapse | Ctrl+S: Save | Ctrl+J/K: Navigate | Alt+B: Back"/>
    </StatusBarItem>
</StatusBar>
```

**Navigation Shortcuts:**
- **Ctrl+J/K:** Up/Down navigation (Vim-like)
- **Alt+B:** Back navigation (using visited items history)
- **Alt+1..9:** Quick jump to index
- **Home/End:** First/last item
- **PageUp/PageDown:** Page navigation
- **N, E, D:** Quick commands (New, Edit, Delete)

**Focus Management:**
```csharp
private static void SelectTreeViewItem(TreeView treeView, object item)
{
    var treeViewItem = treeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
    if (treeViewItem != null)
    {
        treeViewItem.IsSelected = true;
        treeViewItem.Focus();           // Explicitly set focus
        treeViewItem.BringIntoView();   // Scroll into view
    }
}
```

---

## 6. Status Bar & Mode Indicators

### Console-Based Status Bar
**File:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/UI/StatusBar.cs`

```csharp
public class StatusBar
{
    private string currentMessage = "";
    private DateTime messageExpiry = DateTime.MinValue;
    private const int MESSAGE_DURATION_MS = 3000;
    
    public void ShowMessage(string message, MessageType type = MessageType.Info)
    {
        currentMessage = message;
        currentMessageColor = type switch
        {
            MessageType.Success => SuccessColor,
            MessageType.Error => ErrorColor,
            MessageType.Warning => HighlightColor,
            _ => ForegroundColor
        };
        messageExpiry = DateTime.Now.AddMilliseconds(MESSAGE_DURATION_MS);
    }
    
    public void Render(ScreenBuffer screen, Rectangle bounds, StatusInfo status)
    {
        // Check for expired messages
        if (DateTime.Now > messageExpiry)
        {
            currentMessage = "";
        }
        
        // Adaptive layout based on width
        if (bounds.Width >= MIN_WIDTH_FULL)
            RenderFullLayout(screen, bounds, status);
        else if (bounds.Width >= MIN_WIDTH_MEDIUM)
            RenderMediumLayout(screen, bounds, status);
        else
            RenderCompactLayout(screen, bounds, status);
    }
}
```

**Patterns:**
- **Auto-expiring messages** (3 seconds default)
- **Message type coloring** (Success=Green, Error=Red, Warning=Yellow)
- **Adaptive layout** for responsive design
- **Priority rendering:** Messages > shortcuts > status info
- **Essential vs full shortcuts** based on screen width

### WPF Status Service
**File:** `/home/teej/_tui/praxis-main/_wpf/Services/StatusService.cs`

```csharp
public class StatusService : IStatusService
{
    public ObservableCollection<StatusMessage> Messages { get; } = new();
    public StatusMessage? CurrentMessage { get; private set; }
    
    public StatusMessage ShowStatus(string message, StatusType type = StatusType.Info, bool autoExpire = true)
    {
        var statusMessage = new StatusMessage
        {
            Message = message,
            Type = type,
            AutoExpireAfter = type switch
            {
                StatusType.Error => TimeSpan.FromSeconds(10),
                StatusType.Warning => TimeSpan.FromSeconds(7),
                StatusType.Success => TimeSpan.FromSeconds(3),
                _ => TimeSpan.FromSeconds(5)
            }
        };
        
        Messages.Add(statusMessage);
        CurrentMessage = statusMessage;
        return statusMessage;
    }
    
    public IDisposable CreateProgressScope(string message)
    {
        return new ProgressScope(this, message);
    }
    
    public void ClearExpiredMessages()
    {
        var now = DateTime.Now;
        var expiredMessages = Messages
            .Where(m => m.IsAutoExpiring && (now - m.Timestamp) > m.AutoExpireAfter)
            .ToList();
        
        foreach (var message in expiredMessages)
        {
            Messages.Remove(message);
        }
    }
}
```

**Patterns:**
- **INotifyPropertyChanged** for binding
- **ObservableCollection** for multiple messages
- **Message types** with different auto-expire times
- **Progress scopes** for long operations (using pattern)
- **Automatic cleanup timer** (runs every 2 seconds)

---

## 7. Input Event Abstraction

**File:** `/home/teej/_tui/praxis-main/TaskProPro/CSharp/Core/InputEvent.cs`

```csharp
public class InputEvent
{
    public ConsoleKey Key { get; set; }
    public char Char { get; set; }
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    
    // Arrow keys
    public bool IsArrowUp => Key == ConsoleKey.UpArrow;
    public bool IsArrowDown => Key == ConsoleKey.DownArrow;
    public bool IsArrowLeft => Key == ConsoleKey.LeftArrow;
    public bool IsArrowRight => Key == ConsoleKey.RightArrow;
    
    // Navigation
    public bool IsHome => Key == ConsoleKey.Home;
    public bool IsEnd => Key == ConsoleKey.End;
    public bool IsPageUp => Key == ConsoleKey.PageUp;
    public bool IsPageDown => Key == ConsoleKey.PageDown;
    
    // Action keys
    public bool IsEnter => Key == ConsoleKey.Enter;
    public bool IsEscape => Key == ConsoleKey.Escape;
    public bool IsTab => Key == ConsoleKey.Tab;
    
    // Professional Ctrl shortcuts
    public bool IsCtrlA => Ctrl && Key == ConsoleKey.A;
    public bool IsCtrlC => Ctrl && Key == ConsoleKey.C;
    public bool IsCtrlV => Ctrl && Key == ConsoleKey.V;
    public bool IsCtrlZ => Ctrl && Key == ConsoleKey.Z;
    
    // Character input
    public bool IsPrintableChar => !char.IsControl(Char) && Char != '\0';
    
    // Function keys
    public bool IsFunction => Key >= ConsoleKey.F1 && Key <= ConsoleKey.F24;
    public int FunctionKey => IsFunction ? (int)(Key - ConsoleKey.F1) + 1 : 0;
    
    public static InputEvent FromConsoleKeyInfo(ConsoleKeyInfo keyInfo)
    {
        return new InputEvent
        {
            Key = keyInfo.Key,
            Char = keyInfo.KeyChar,
            Ctrl = (keyInfo.Modifiers & ConsoleModifiers.Control) != 0,
            Alt = (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
            Shift = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0
        };
    }
}
```

**Pattern:**
- **Properties for common key combinations** (IsArrowUp, IsCtrlA, etc.)
- **Clean API** for checking modifiers + key
- **Printable char detection** built-in
- **Factory method** for creating from ConsoleKeyInfo
- **Extensible** for adding more key combination properties

---

## Key Learnings & Recommendations for SuperTUI

### 1. Input Handling Architecture
- **Don't build global keyboard interceptor**
- **Delegate to focused widget/component**
- **Use PreviewKeyDown (tunneling) for critical keys** that shouldn't reach controls
- **Return true/consume event** only when input is handled

### 2. Focus Management
- **Simple boolean `IsFocused` flag** is sufficient
- **Check focus in input handler first line** (early return)
- **Visual feedback** (highlight, color change, border) required
- **Single focused widget** at a time (not a stack)

### 3. Mode System
- **Explicit mode flag** (isEditMode, isSearchMode, etc.)
- **Status bar shows current mode** with instructions
- **Input handler gates on mode** (return false if not in mode)
- **Clear exit/cancel** with state restoration

### 4. Status Bar
- **Always visible with contextual information**
- **Auto-expiring messages** (different times by type)
- **Mode instructions** ("ESC=Cancel, Enter=Save, Tab=Next Field")
- **Adaptive layout** for different screen sizes
- **Shortcut cheat sheet** when no messages

### 5. Navigation
- **Keyboard shortcuts documented in status bar**
- **Vim-like bindings** (Ctrl+J/K for up/down, Ctrl+H/L for left/right)
- **Home/End for first/last**
- **Alt+B for back** (requires visited items history)
- **Explicit focus setting** in WPF (`treeViewItem.Focus()` + `BringIntoView()`)

### 6. WPF-Specific Patterns
- **Attached Behaviors** for reusable keyboard handling
- **PreviewKeyDown for tunneling phase**
- **CommandManager.RequerySuggested** for command availability
- **MVVM with binding** instead of code-behind
- **INotifyPropertyChanged** for status updates

### 7. Text Editing
- **Separate cursor position from buffer**
- **Blinking cursor** for visual feedback
- **Word navigation** with Ctrl+Arrow
- **Selection** with Shift+Arrow
- **Home/End for line edges**

### 8. Input Event Abstraction
- **Property-based API** (IsArrowUp, IsCtrlA) instead of switch statements
- **Consistent modifier checking** (Ctrl, Alt, Shift flags)
- **Factory method** for creating from platform events
- **Type safety** over strings

---

## Files to Review
1. `/home/teej/_tui/praxis-main/TaskProPro/CSharp/Core/InputEvent.cs` - Input abstraction
2. `/home/teej/_tui/praxis-main/TaskProPro/CSharp/Core/InputManager.cs` - Input reading
3. `/home/teej/_tui/praxis-main/TaskProPro/CSharp/UI/ListWidget.cs` - Focus & navigation
4. `/home/teej/_tui/praxis-main/TaskProPro/CSharp/UI/InlineEditor.cs` - Edit mode & cursor
5. `/home/teej/_tui/praxis-main/TaskProPro/CSharp/UI/TaskListEditMode.cs` - State machine
6. `/home/teej/_tui/praxis-main/TaskProPro/CSharp/UI/StatusBar.cs` - Status display
7. `/home/teej/_tui/praxis-main/_wpf/Behaviors/KeyboardNavigationBehavior.cs` - WPF patterns
8. `/home/teej/_tui/praxis-main/_wpf/Services/StatusService.cs` - Status service
9. `/home/teej/_tui/praxis-main/_wpf/MainWindow.xaml` - No global input bindings
10. `/home/teej/_tui/praxis-main/_wpf/Services/RelayCommand.cs` - Command pattern

