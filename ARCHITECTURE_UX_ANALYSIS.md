# SuperTUI Architecture & Implementation Report

## Executive Summary

SuperTUI is a WPF-based desktop framework styled to resemble terminal aesthetics. It features a sophisticated widget/workspace system with i3-style tiling layouts, terminal-like input modes, keyboard-driven navigation, and a comprehensive event bus for inter-widget communication. The architecture emphasizes modularity, extensibility, and resilience through error boundaries and dependency injection.

---

## 1. Navigation System

### 1.1 Workspace/Tab Navigation

**Location**: `/home/teej/supertui/WPF/Core/Infrastructure/WorkspaceManager.cs`

**Architecture**:
- Multiple independent desktops (workspaces) each with isolated widget state
- Workspaces stored in `ObservableCollection<Workspace>` for reactive updates
- Each workspace has:
  - Unique index and name
  - List of widgets with their layout parameters
  - LayoutEngine (Grid, Tiling, Dashboard, etc.)
  - Focus tracking and keyboard input handling
  - Fullscreen state management

**Key Methods**:
- `SwitchToWorkspace(int index)` - Transitions between workspaces
- `SwitchToNext()` / `SwitchToPrevious()` - Cycling through workspaces
- `ExitFullscreen()` - Exit fullscreen mode before switching
- `Activate()` / `Deactivate()` - Preserve state during transitions

**Features**:
- Workspaces are deactivated (not destroyed) when switching away
- State is preserved across workspace switches
- Event: `WorkspaceChanged` fired on switch
- Focus management: First element receives focus on activation

**Code Example**:
```csharp
// Switch to workspace by index
workspaceManager.SwitchToWorkspace(0);

// Navigate with keyboard shortcuts
workspaceManager.SwitchToNext();      // Cycle forward
workspaceManager.SwitchToPrevious();  // Cycle backward

// Current workspace always accessible
var activeWs = workspaceManager.CurrentWorkspace;
```

---

### 1.2 Widget Focus Navigation

**Location**: `/home/teej/supertui/WPF/Core/Infrastructure/Workspace.cs`

**Architecture**:
- Each workspace maintains `List<UIElement> focusableElements`
- Sequential focus cycling via `FocusNext()` / `FocusPrevious()`
- Directional focus (arrow keys) via layout engine's `FindWidgetInDirection()`

**Key Methods**:
- `FocusElement(UIElement)` - Set focus on specific element
- `FocusNext()` - Tab to next focusable element
- `FocusPrevious()` - Shift+Tab to previous element
- `HandleKeyDown(KeyEventArgs)` - Delegate to layout engine for directional navigation

**Focus Direction Algorithm** (from GridLayoutEngine):
```
- Maintains widget positions in grid coordinates (row, col)
- Direction.Left/Right/Up/Down searches by adjacency
- Uses distance calculation: manhattan distance + 0.5x perpendicular distance
- Returns closest widget in requested direction
```

**Code Example**:
```csharp
// In workspace
FocusNext();  // Tab to next widget

// In layout engine (supports arrow keys)
var nextWidget = gridLayoutEngine.FindWidgetInDirection(currentWidget, FocusDirection.Right);
if (nextWidget != null)
    workspace.FocusElement(nextWidget);
```

---

### 1.3 Modal Dialog System

**Location**: `/home/teej/supertui/WPF/Core/Dialogs/`

**Modal Dialogs Implemented**:

1. **FilePickerDialog** (`FilePickerDialog.cs`)
   - Three modes: Open, Save, SelectFolder
   - Modal window with terminal aesthetic
   - Keyboard-driven file/folder navigation
   - Returns `SelectedPath` property on OK

2. **TagEditorDialog** (`TagEditorDialog.cs`)
   - In-widget modal for editing task tags
   - Shows available tags with selection UI
   - Returns selected tags on OK

**FilePickerDialog Key Features**:
- `PickerMode enum`: Open, Save, SelectFolder
- Supports file extension filtering
- Starts at configurable initial path
- Terminal-styled UI with keyboard navigation
- Returns result via `SelectedPath` property
- Safe file type handling (dangerous file warnings)

**Usage Pattern**:
```csharp
var picker = new FilePickerDialog(
    FilePickerDialog.PickerMode.Open,
    ".txt",  // extension filter
    initialPath: @"C:\Users\...",
    logger: logger,
    themeManager: themeManager);

if (picker.ShowDialog() == true)
{
    string selectedPath = picker.SelectedPath;
    // Use selected file
}
```

---

### 1.4 Screen/View Switching

**Location**: `/home/teej/supertui/WPF/Core/Components/ScreenBase.cs`

**Architecture**:
- Screens are alternative UI containers (less common than widgets)
- Similar lifecycle to widgets but distinct role
- Can exist alongside widgets in a workspace

**ScreenBase Interface**:
- `OnFocusReceived()` - Called when screen gets focus
- `OnFocusLost()` - Called when screen loses focus
- Add to workspace via: `workspace.AddScreen(screen, layoutParams)`

---

## 2. Layout System

### 2.1 Core Layout Architecture

**Base Class**: `LayoutEngine` (`/home/teej/supertui/WPF/Core/Layout/LayoutEngine.cs`)

**Common Properties**:
- `Container: Panel` - WPF container (Grid, StackPanel, Canvas)
- `List<UIElement> children` - Tracked child elements
- `Dictionary<UIElement, LayoutParams> layoutParams` - Position/size data

**LayoutParams Configuration**:
```csharp
public class LayoutParams
{
    // Grid positioning
    public int? Row { get; set; }
    public int? Column { get; set; }
    public int? RowSpan { get; set; }
    public int? ColumnSpan { get; set; }
    
    // Dock positioning
    public Dock? Dock { get; set; }
    
    // Sizing modes
    public double? Width { get; set; }
    public double? Height { get; set; }
    public double? MinWidth { get; set; }
    public double? MinHeight { get; set; }
    public double? MaxWidth { get; set; }
    public double? MaxHeight { get; set; }
    
    // Star sizing (proportional)
    public double StarWidth { get; set; }   // 2.0 = 2*
    public double StarHeight { get; set; }  // 1.0 = 1*
    
    // Alignment
    public HorizontalAlignment? HorizontalAlignment { get; set; }
    public VerticalAlignment? VerticalAlignment { get; set; }
    public Thickness? Margin { get; set; }
}
```

---

### 2.2 Built-In Layout Engines

**1. GridLayoutEngine** (`GridLayoutEngine.cs`)
- **Purpose**: Traditional 2D grid layout with resizable columns/rows
- **Features**:
  - N x M grid with dynamic row/column definitions
  - GridSplitters with drag-to-resize (enforces min sizes)
  - Visual feedback on splitter hover
  - Directional widget navigation via `FindWidgetInDirection()`
  - Widget swapping via `SwapWidgets()`
- **Min/Max Constraints**:
  - Default: 50px min row height, 100px min column width
  - Enforced on drag completion
- **Key Methods**:
  - `SetColumnWidth(int, GridLength)` - Adjust column sizing
  - `SetRowHeight(int, GridLength)` - Adjust row sizing
  - `SwapWidgets(UIElement, UIElement)` - Exchange positions

**2. TilingLayoutEngine** (`TilingLayoutEngine.cs`)
- **Purpose**: i3-style automatic tiling with multiple preset layouts
- **Tiling Modes** (enum `TilingMode`):
  - **Auto**: Automatically select based on widget count
    - 1 widget: Grid (fullscreen)
    - 2 widgets: Tall (side-by-side)
    - 3-4 widgets: Grid (square layout)
    - 5+ widgets: MasterStack
  - **MasterStack**: 60/40 split (main widget left, others stacked right)
  - **Wide**: All widgets stacked vertically (horizontal splits)
  - **Tall**: All widgets arranged horizontally (vertical splits)
  - **Grid**: NxN square grid layout

- **Visual Layouts**:
```
WIDE (splitv):          TALL (splith):        GRID (2x2):
┌──────────┐            ┌───┬───┬───┐         ┌──────┬──────┐
│    W1    │            │W1 │W2 │W3 │         │  W1  │  W2  │
├──────────┤            └───┴───┴───┘         ├──────┼──────┤
│    W2    │                                  │  W3  │  W4  │
├──────────┤            MASTER_STACK:         └──────┴──────┘
│    W3    │            ┌──────────┬───┐
└──────────┘            │    W1    │W2 │
                        │  (main)  │W3 │
                        │   60%    │W4 │
                        │          │...│
                        └──────────┴───┘
```

- **Key Methods**:
  - `SetMode(TilingMode)` - Change layout mode
  - `Relayout()` - Rebuild layout after mode change
  - `FindWidgetInDirection()` - Directional navigation
  - `SwapWidgets()` - Exchange positions

**3. DashboardLayoutEngine** (`DashboardLayoutEngine.cs`)
- **Purpose**: Fixed 2x2 dashboard with 4 slot layout
- **Features**:
  - Exactly 4 slots: TopLeft, TopRight, BottomLeft, BottomRight
  - Can add/remove widgets to slots at runtime
  - Shows "Empty Slot" placeholder when unoccupied
  - Equal-sized cells (proportional 1:1:1:1)
- **Key Methods**:
  - `SetWidget(int slotIndex, UIElement widget)` - Place in slot (0-3)
  - `GetWidget(int slotIndex)` - Get current widget
  - `ClearSlot(int slotIndex)` - Remove widget

**4. FocusLayoutEngine** (`FocusLayoutEngine.cs`)
- **Purpose**: Distraction-free 2-pane layout
- **Layout**: 80% main | 20% sidebar
- **Constraints**: Main min 400px, sidebar 150-300px
- **Use Case**: Focus on primary content with quick reference sidebar
- **Key Methods**:
  - `SetMainWidget()` - 80% area
  - `SetSidebarWidget()` - 20% area

**5. Other Specialized Engines**:
- **CodingLayoutEngine** - Optimized for development (code editor + panels)
- **MonitoringDashboardLayoutEngine** - Real-time system metrics display
- **CommunicationLayoutEngine** - Chat/messaging interface layout
- **DockLayoutEngine** - Simple docking (Top/Bottom/Left/Right)
- **StackLayoutEngine** - Simple vertical/horizontal stacking

---

### 2.3 Multi-Pane Widget Examples

**TaskManagementWidget** (3-pane layout):
- Left pane: Filter list (All, Today, Overdue, etc.)
- Center pane: Tree task list with hierarchy
- Right pane: Task details panel (editable)
- Connected via event system for cross-pane updates

**FileExplorerWidget** (2-pane layout):
- Top/Full: Path bar and file list
- Navigation updates file display
- Uses ListBox with keyboard shortcuts (Enter, Backspace, Del)

**KanbanBoardWidget** (3-pane layout):
- Column 1: TODO tasks
- Column 2: IN PROGRESS tasks
- Column 3: DONE tasks
- Keyboard navigation between columns (Left/Right arrows)

---

### 2.4 Resizable Components

**GridSplitter Implementation**:
```csharp
var splitter = new GridSplitter
{
    Width = 5,  // or Height for horizontal
    HorizontalAlignment = HorizontalAlignment.Right,
    VerticalAlignment = VerticalAlignment.Stretch,
    Background = splitterBrush,
    ResizeDirection = GridResizeDirection.Columns,  // or Rows
    ShowsPreview = false,
    Cursor = Cursors.SizeWE  // or SizeNS
};

// Enforce constraints on drag completion
splitter.DragCompleted += (sender, e) =>
{
    EnforceColumnConstraints(columnIndex);
};

// Visual feedback
splitter.MouseEnter += (sender, e) =>
{
    splitter.Background = splitterHoverBrush;
};
```

**Constraint Enforcement**:
- Min sizes enforced after drag completes
- If resized below minimum, snaps to minimum
- Deficit distributed to adjacent cell if possible
- After snapping, reverts to proportional (star) sizing

---

## 3. Input Handling

### 3.1 Keyboard Shortcuts System

**Location**: `/home/teej/supertui/WPF/Core/Infrastructure/ShortcutManager.cs`

**Architecture**:
- Central singleton registry for global and workspace-specific shortcuts
- Two-tier lookup: workspace-specific → global
- Supports modifier keys (Ctrl, Alt, Shift, Windows)

**Key Features**:
```csharp
public class ShortcutManager : IShortcutManager
{
    // Global shortcuts (all workspaces)
    public void RegisterGlobal(Key key, ModifierKeys modifiers, Action action, string description);
    
    // Workspace-specific shortcuts
    public void RegisterForWorkspace(string workspaceName, Key key, ModifierKeys modifiers, Action action, string description);
    
    // Handle input
    public bool HandleKeyDown(Key key, ModifierKeys modifiers, string currentWorkspace);
    
    // Query
    public List<KeyboardShortcut> GetAllShortcuts();
    public IReadOnlyList<KeyboardShortcut> GetGlobalShortcuts();
    public IReadOnlyList<KeyboardShortcut> GetWorkspaceShortcuts(string workspaceName);
}
```

**KeyboardShortcut Model**:
```csharp
public class KeyboardShortcut
{
    public Key Key { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public Action Action { get; set; }
    public string Description { get; set; }  // Human-readable for help display
    
    public bool Matches(Key key, ModifierKeys modifiers);
    public override string ToString();  // Format: "Ctrl+Alt+S"
}
```

**Widget Input Mode System**:

**WidgetInputMode Enum**:
```csharp
public enum WidgetInputMode
{
    Normal,   // Navigation and commands (default)
    Insert,   // Text input active (textarea, input box)
    Command   // Command palette or special mode
}
```

**Usage in WidgetBase**:
```csharp
public class WidgetBase : UserControl, IWidget
{
    private WidgetInputMode inputMode = WidgetInputMode.Normal;
    
    public WidgetInputMode InputMode
    {
        get => inputMode;
        protected set
        {
            if (inputMode != value)
            {
                inputMode = value;
                OnPropertyChanged(nameof(InputMode));
                OnInputModeChanged(value);  // Virtual for subclasses
            }
        }
    }
    
    // Override to handle mode-specific behavior
    protected virtual void OnInputModeChanged(WidgetInputMode newMode) { }
}
```

**Widget Keyboard Handler**:
```csharp
// Called by workspace on key press
public virtual void OnWidgetKeyDown(KeyEventArgs e) { }
```

---

### 3.2 Text Input Components

**EditableListControl<T>** (`/home/teej/supertui/WPF/Core/Components/EditableListControl.cs`)
- Generic reusable control for displaying/editing lists
- Supports Add, Edit (inline), Delete, Selection
- Fully keyboard-driven (no mouse required)
- Configuration via delegates:
  - `Func<T, string> DisplayFormatter` - How to display items
  - `Func<string, T> ItemCreator` - Parse input to create item
  - `Func<T, string, T> ItemUpdater` - Update item from text
  - `Func<T, bool> ItemValidator` - Validate before saving

- **Events**:
  - `OnItemAdded`
  - `OnItemDeleted`
  - `OnItemUpdated`
  - `OnSelectionChanged`

**TaskListControl** (`/home/teej/supertui/WPF/Core/Components/TaskListControl.cs`)
- Specialized ListBox for task display
- Supports multi-selection and filtering
- Custom item templates with task properties

**TreeTaskListControl** (`/home/teej/supertui/WPF/Core/Components/TreeTaskListControl.cs`)
- Hierarchical task display with expand/collapse
- Maintains visual tree structure
- Flattened display (no TreeView complexity)
- Events: TaskSelected, TaskActivated, CreateSubtask, DeleteTask

---

### 3.3 Autocomplete & Smart Parsing

**CommandPaletteWidget** (`/home/teej/supertui/WPF/Widgets/CommandPaletteWidget.cs`)
- Implements fuzzy search/autocomplete pattern
- Real-time filtering of command list

**Implementation**:
```csharp
// Search box
searchBox = new TextBox { ... };
searchBox.TextChanged += (s, e) =>
{
    RefreshResults();  // Filter and display
};

// Filtering logic (fuzzy search)
private void RefreshResults()
{
    string query = searchBox.Text.ToLower();
    filteredItems = allItems.Where(item =>
        FuzzyMatch(item.Name, query) ||
        FuzzyMatch(item.Description, query)
    ).ToList();
    
    resultsBox.ItemsSource = filteredItems;
}
```

---

### 3.4 Inline Editing Patterns

**TaskManagementWidget - Inline Task Editing**:
- Click task to select
- Right pane shows details
- TextBox for description (editable)
- Buttons for status/priority/tags updates
- Direct property editing (no separate dialog)

**NotesWidget - Tab-Based Editor**:
- Multiple TextBox instances (one per file)
- Tabs for switching between open files
- Dirty flag tracking (`IsDirty`)
- Atomic saves with error handling

**Pattern Used Across Widgets**:
```csharp
// Example from TaskManagementWidget
detailDescriptionBox = new TextBox
{
    Text = selectedTask?.Description ?? "",
    AcceptsReturn = true,
    TextWrapping = TextWrapping.Wrap
};

// On text changed
detailDescriptionBox.TextChanged += (s, e) =>
{
    if (selectedTask != null)
    {
        selectedTask.Description = detailDescriptionBox.Text;
        // Optional: Auto-save on change
    }
};

// Save button
saveDescButton.Click += (s, e) =>
{
    if (selectedTask != null)
    {
        taskService.UpdateTask(selectedTask);
    }
};
```

---

## 4. Visual Feedback & UI/UX Patterns

### 4.1 Status Bar Implementation

**StandardWidgetFrame** (`/home/teej/supertui/WPF/Core/Components/StandardWidgetFrame.cs`)
- Three-zone layout: Header, Content, Footer
- Consistent frame around all widgets

**Structure**:
```
┌─ HEADER (Title + Context) ─────────────┐
│ [Widget Title]          [Context Info]  │
├─────────────────────────────────────────┤
│                                         │
│            Content Area                 │
│         (filled by widget)              │
│                                         │
├─ FOOTER (Shortcuts) ───────────────────┤
│ Enter: Execute | Esc: Close | ?: Help  │
└─────────────────────────────────────────┘
```

**Properties**:
```csharp
public string Title { get; set; }
public string ContextInfo { get; set; }  // Right-aligned, auto-hidden if empty
public string FooterInfo { get; set; }   // Keyboard shortcuts
public UIElement Content { get; set; }   // Content area
```

**Keyboard Shortcuts Format**:
```csharp
frame.SetStandardShortcuts(
    "Enter: Open",
    "Backspace: Up",
    "Del: Delete",
    "F5: Refresh",
    "?: Help"
);
```

**Styling**:
- Header: Surface background with primary foreground
- Content: Main background
- Footer: Surface background with disabled foreground
- All borders use theme.Border color

---

### 4.2 Visual Symbols & Icons

**Unicode Symbols Used**:
```
Navigation/UI:
🔍 Search/Find
✓ Check/Done
✗ Close/Cancel
← → ↑ ↓ Direction arrows
⚙ Settings
🔧 Tools
⌨ Keyboard
↔ Resize handle
├─ ┬─ ├─ Tree structure lines

Status/Feedback:
⚡ Quick action
→ Navigation/Link
• Bullet point
│ Divider
─ Horizontal line
```

**Glyph Implementation**:
- Direct Unicode in TextBlock text properties
- No custom font glyphs required
- Monospace fonts (Cascadia Mono, Consolas) support symbols

**Example**:
```csharp
titleText = new TextBlock
{
    Text = "⌨  KEYBOARD SHORTCUTS",  // Keyboard symbol + title
    FontSize = 18,
    FontWeight = FontWeights.Bold
};

footerText = new TextBlock
{
    Text = "↑/↓: Navigate  |  Enter: Select  |  Esc: Cancel",
    FontSize = 11
};
```

---

### 4.3 Theme System Capabilities

**Location**: `/home/teej/supertui/WPF/Core/Infrastructure/ThemeManager.cs`

**Complete Theme Definition**:
```csharp
public class Theme
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsDark { get; set; }
    
    // Primary colors
    public Color Primary { get; set; }           // Main accent
    public Color Secondary { get; set; }         // Alternative accent
    public Color Success { get; set; }           // Green for positive
    public Color Warning { get; set; }           // Yellow for warnings
    public Color Error { get; set; }             // Red for errors
    public Color Info { get; set; }              // Blue for info
    
    // UI colors
    public Color Background { get; set; }
    public Color BackgroundSecondary { get; set; }
    public Color Surface { get; set; }           // Slightly raised
    public Color Foreground { get; set; }
    public Color ForegroundSecondary { get; set; }
    public Color ForegroundDisabled { get; set; }
    public Color Border { get; set; }
    public Color Focus { get; set; }             // Focus ring color
    public Color Hover { get; set; }             // Hover highlight
    public Color Selection { get; set; }         // Selection highlight
    
    // Effects
    public GlowSettings Glow { get; set; }       // Neon glow effect
    public CRTEffectSettings CRTEffects { get; set; }  // Scanlines, bloom
    public OpacitySettings Opacity { get; set; }      // Element opacity
    
    // Typography
    public TypographySettings Typography { get; set; }
}
```

**Glow Settings**:
```csharp
public class GlowSettings
{
    public GlowMode Mode { get; set; }       // Always, OnFocus, OnHover, Never
    public Color GlowColor { get; set; }
    public double GlowRadius { get; set; }
    public double GlowOpacity { get; set; }
    public Color FocusGlowColor { get; set; }
    public Color HoverGlowColor { get; set; }
}
```

**CRT Effects**:
```csharp
public class CRTEffectSettings
{
    public bool EnableScanlines { get; set; }
    public double ScanlineOpacity { get; set; }
    public int ScanlineSpacing { get; set; }
    public Color ScanlineColor { get; set; }
    public bool EnableBloom { get; set; }
    public double BloomIntensity { get; set; }
}
```

**Theme Switching**:
- Hot-reload capability (themes apply immediately)
- Per-widget font overrides via `PerWidgetFonts` dictionary
- Theme changes broadcast via `ThemeChangedEvent`
- WeakEventManager prevents memory leaks during subscription

---

### 4.4 Focus Ring & Visual Indication

**WidgetBase Focus Border**:
- Wraps widget content in Border with dynamic styling
- Focus state: 3px colored border (theme.Focus)
- Unfocused state: 1px subtle border (theme.Border)
- Optional glow effect based on theme settings

**Implementation**:
```csharp
// In WidgetBase
private void UpdateFocusVisual()
{
    if (HasFocus)
    {
        containerBorder.BorderBrush = new SolidColorBrush(theme.Focus);
        containerBorder.BorderThickness = new Thickness(3);
        
        if (theme.Glow != null)
        {
            GlowEffectHelper.ApplyGlow(containerBorder, theme.Glow, GlowState.Focus);
        }
    }
    else
    {
        containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
        containerBorder.BorderThickness = new Thickness(1);
        
        if (theme.Glow?.Mode == GlowMode.Always)
        {
            GlowEffectHelper.ApplyGlow(containerBorder, theme.Glow, GlowState.Normal);
        }
    }
}
```

**Glow Effect Helper** (`/home/teej/supertui/WPF/Core/Effects/GlowEffectHelper.cs`):
- Applies DropShadowEffect with theme colors
- Weak reference event handlers prevent memory leaks
- Supports focus/hover/theme change events

---

### 4.5 Overlay & Modal Presentation

**ShortcutOverlay** (`/home/teej/supertui/WPF/Core/Components/ShortcutOverlay.cs`)
- Full-screen semi-transparent background
- Centered content box with drop shadow
- Displays global + widget-specific shortcuts
- Context-aware (shows current widget shortcuts)
- Dismissible with Esc

**QuickJumpOverlay** (`/home/teej/supertui/WPF/Core/Components/QuickJumpOverlay.cs`)
- Quick widget jumping with single-key access
- "G" key activates quick jump menu
- Shows available jump targets with key bindings
- Modal-style presentation

**CRTEffectsOverlay** (`/home/teej/supertui/WPF/Core/Components/CRTEffectsOverlay.cs`)
- Canvas-based retro visual effects
- Animated scanlines overlay
- Bloom effect for glow enhancement
- Configurable opacity and spacing
- Layered on top of main UI

---

## 5. Widget Architecture

### 5.1 Widget Communication

**Location**: `/home/teej/supertui/WPF/Core/Infrastructure/EventBus.cs`

**Event Bus Design**:
- Singleton pattern (one global channel)
- Strongly-typed pub/sub via generics
- Supports weak references (for memory-sensitive cases)
- Priority-based delivery (High → Low)
- Thread-safe with lock-based synchronization

**Usage Pattern**:
```csharp
// Subscribe to event
EventBus.Subscribe<TaskSelectedEvent>(OnTaskSelected);

// Publish event
EventBus.Publish(new TaskSelectedEvent
{
    Task = selectedTask,
    SourceWidget = WidgetType
});

// Unsubscribe (for cleanup)
EventBus.Unsubscribe<TaskSelectedEvent>(OnTaskSelected);
```

**Available Events** (42 total):
- Workspace: WorkspaceChanged, WorkspaceCreated, WorkspaceRemoved
- Widget: WidgetActivated, WidgetDeactivated, WidgetFocusReceived/Lost
- Task: TaskCreated, TaskCompleted, TaskDeleted, TaskSelected, TaskUpdated, TaskStatusChanged
- Theme: ThemeChanged, ThemeLoaded
- File: DirectoryChanged, FileSelected, FileCreated, FileDeleted
- Git: BranchChanged, CommitCreated, RepositoryStatusChanged
- System: SystemResourcesChanged, NetworkActivity
- ... and more

**Inter-Widget Communication Example** (TaskManagementWidget):
```csharp
public override void Initialize()
{
    // Subscribe to events
    EventBus.Subscribe<TaskSelectedEvent>(OnTaskSelectedFromOtherWidget);
    EventBus.Subscribe<NavigationRequestedEvent>(OnNavigationRequested);
}

private void OnTaskSelectedFromOtherWidget(TaskSelectedEvent evt)
{
    if (evt.SourceWidget == WidgetType) return;  // Ignore our own events
    
    // Try to select the task if it's in current view
    if (evt.Task != null && treeTaskListControl != null)
    {
        treeTaskListControl.SelectTask(evt.Task);
    }
}
```

---

### 5.2 Common UI Patterns

**Pattern 1: List + Details (TaskManagementWidget)**
```
┌─ LIST PANE ────────┬─ DETAIL PANE ────────┐
│ Filter buttons     │ Task title           │
├────────────────────│                      │
│ Task list (tree)   │ Description (edit)   │
│                    │ Status, Priority     │
│                    │ Due date, Tags       │
│                    │ Notes list           │
│                    │ Subtasks panel       │
└────────────────────┴──────────────────────┘
```

**Pattern 2: Three-Column Kanban (KanbanBoardWidget)**
```
┌──────────────┬──────────────┬──────────────┐
│ TODO         │ IN PROGRESS  │ DONE         │
│ (Column 1)   │ (Column 2)   │ (Column 3)   │
│              │              │              │
│ [Task]       │ [Task]       │ [Task] ✓     │
│ [Task]       │ [Task]       │ [Task] ✓     │
└──────────────┴──────────────┴──────────────┘
```

**Pattern 3: Editor with Tabs (NotesWidget)**
```
┌─ [Untitled*] ─ [notes.txt] ─ [readme.md] ┐
├────────────────────────────────────────────┤
│                                            │
│ Text editor content (TextBox)              │
│                                            │
│                                            │
├─ Ln 45, Col 12 ──────────────────────────┤
│ Ctrl+S: Save | Ctrl+N: New | ?: Help      │
└────────────────────────────────────────────┘
```

**Pattern 4: Tree View + Selection (TaskManagementWidget)**
```
┌─────────────────────────────────────┐
│ Project 1                   [+] [-] │
│ ├─ Feature: Login          [E] [D]  │
│ │  ├─ UI mockup           [#1]      │
│ │  └─ Backend API         [#2]      │
│ ├─ Bug: Crash on save                │
│ └─ Chore: Update deps                │
└─────────────────────────────────────┘
E=Edit, D=Delete, # = associated ID
```

---

### 5.3 Three-Pane Widget Examples

**TaskManagementWidget Structure**:
1. **Left Pane**: Filter list
   - Clickable filter options
   - Updates center pane
   
2. **Center Pane**: TreeTaskListControl
   - Hierarchical task display
   - Expand/collapse nodes
   - Keyboard selection
   
3. **Right Pane**: Task details
   - Read-only display of selected task
   - Editable TextBox for description
   - Status/Priority buttons
   - Tags editor button
   - Notes and subtasks sections

**FileExplorerWidget Structure**:
1. **Path Bar**: Current directory
2. **List**: Files and folders
3. **Status Bar**: File count, selected size
4. **Keyboard**: Enter (open), Backspace (up), Del (delete)

**KanbanBoardWidget Structure**:
1. **Column 1**: TODO tasks (ListBox)
2. **Column 2**: IN PROGRESS tasks (ListBox)
3. **Column 3**: DONE tasks (ListBox)
4. **Navigation**: Left/Right arrows to switch columns

---

### 5.4 Focus Management

**WidgetBase Focus Properties**:
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
            UpdateFocusVisual();  // Update border
            
            if (value)
                OnWidgetFocusReceived();
            else
                OnWidgetFocusLost();
        }
    }
}
```

**Lifecycle**:
1. Workspace sets `HasFocus = true` on widget
2. WidgetBase updates border styling
3. `OnWidgetFocusReceived()` virtual method called
4. Widget can prepare UI (e.g., select first item in list)

**Nested Focus**:
- Widgets can contain focusable sub-elements (TextBox, ListBox)
- WPF focus tree handles nested focus
- Widget's outer HasFocus indicates widget-level focus

---

## 6. Advanced Features & Patterns

### 6.1 Error Boundary Pattern

**Location**: `/home/teej/supertui/WPF/Core/Components/ErrorBoundary.cs`

**Purpose**: Isolate widget crashes (one widget failure doesn't crash app)

**Architecture**:
```csharp
public class ErrorBoundary : ContentControl
{
    private WidgetBase widget;
    private bool hasError = false;
    
    public void SafeInitialize()    // Wraps widget.Initialize()
    public void SafeActivate()      // Wraps widget.OnActivated()
    public void SafeDeactivate()    // Wraps widget.OnDeactivated()
    public void SafeHandleKeyDown() // Wraps widget.OnWidgetKeyDown()
    
    private void HandleError(Exception ex, string context)
    {
        hasError = true;
        // Log error
        // Display error UI
        // Widget becomes inert but workspace continues
    }
}
```

**Usage**:
```csharp
// In Workspace.AddWidget()
var errorBoundary = new ErrorBoundary(widget, logger, themeManager);
Layout.AddChild(errorBoundary, layoutParams);
errorBoundary.SafeInitialize();  // Protected call
```

---

### 6.2 Dependency Injection

**Location**: `/home/teej/supertui/WPF/Core/DI/WidgetFactory.cs`

**WidgetFactory Pattern**:
```csharp
public class WidgetFactory
{
    // Create widget with automatic dependency resolution
    public TWidget CreateWidget<TWidget>() where TWidget : WidgetBase
    {
        var constructor = GetBestConstructor(typeof(TWidget));
        var parameters = ResolveParameters(constructor);
        return (TWidget)constructor.Invoke(parameters);
    }
    
    // Widget DI constructor pattern
    public class MyWidget : WidgetBase
    {
        public MyWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config,
            ITaskService taskService)
        {
            // DI constructor - used by WidgetFactory
        }
    }
}
```

**Service Container** (14 services with interfaces):
- ILogger
- IThemeManager
- IConfigurationManager
- ISecurityManager
- IErrorHandler
- IStatePersistenceManager
- IPerformanceMonitor
- IPluginManager
- IEventBus
- IShortcutManager
- ITaskService (domain)
- IProjectService (domain)
- ITimeTrackingService (domain)
- ITagService (domain)

---

### 6.3 State Persistence

**StateSnapshot Pattern**:
```csharp
public class StateSnapshot
{
    public string WidgetName { get; set; }
    public Dictionary<string, object> State { get; set; }
    public string Checksum { get; set; }  // SHA256 for corruption detection
}
```

**Widget State Management**:
```csharp
// Save state (called on app close)
public virtual Dictionary<string, object> SaveState()
{
    return new Dictionary<string, object>
    {
        ["SelectedTaskId"] = selectedTask?.Id,
        ["FilterType"] = currentFilter.Type,
        ["ScrollPosition"] = listBox.SelectedIndex
    };
}

// Restore state (called on app start)
public virtual void RestoreState(Dictionary<string, object> state)
{
    if (state.TryGetValue("SelectedTaskId", out var taskId))
    {
        selectedTask = taskService.GetTaskById((Guid)taskId);
    }
}
```

---

## 7. Key Files Reference

| File | Purpose | Key Classes |
|------|---------|-------------|
| `/Core/Infrastructure/WorkspaceManager.cs` | Manage multiple workspaces | WorkspaceManager |
| `/Core/Infrastructure/Workspace.cs` | Individual workspace state | Workspace |
| `/Core/Layout/GridLayoutEngine.cs` | 2D grid with resizable cells | GridLayoutEngine |
| `/Core/Layout/TilingLayoutEngine.cs` | i3-style tiling modes | TilingLayoutEngine |
| `/Core/Components/WidgetBase.cs` | Base class for all widgets | WidgetBase, WidgetInputMode |
| `/Core/Components/StandardWidgetFrame.cs` | Consistent widget frame | StandardWidgetFrame |
| `/Core/Infrastructure/ShortcutManager.cs` | Keyboard shortcuts | ShortcutManager, KeyboardShortcut |
| `/Core/Infrastructure/EventBus.cs` | Inter-widget pub/sub | EventBus |
| `/Core/Infrastructure/Events.cs` | Event definitions (42 types) | TaskSelectedEvent, etc. |
| `/Core/Infrastructure/ThemeManager.cs` | Theme system | Theme, GlowSettings, CRTEffectSettings |
| `/Core/Effects/GlowEffectHelper.cs` | Neon glow effects | GlowEffectHelper, GlowState |
| `/Core/Components/CRTEffectsOverlay.cs` | Retro scanline effects | CRTEffectsOverlay |
| `/Core/Dialogs/FilePickerDialog.cs` | File/folder selection | FilePickerDialog |
| `/Core/Components/ErrorBoundary.cs` | Exception isolation | ErrorBoundary |
| `/Core/DI/WidgetFactory.cs` | Widget creation with DI | WidgetFactory |
| `/Widgets/TaskManagementWidget.cs` | 3-pane task manager | TaskManagementWidget |
| `/Widgets/KanbanBoardWidget.cs` | 3-column kanban board | KanbanBoardWidget |
| `/Widgets/FileExplorerWidget.cs` | File navigator | FileExplorerWidget |
| `/Widgets/NotesWidget.cs` | Text editor with tabs | NotesWidget |

---

## 8. Innovation Opportunities

Based on the current architecture, here are areas ripe for UX innovation:

### 8.1 Navigation Enhancements
- Command palette improvements (fuzzy search scoring, command grouping)
- Breadcrumb navigation for deep widget hierarchies
- Widget history/back button navigation
- Customizable workspace switcher UI (not just index-based)

### 8.2 Layout Improvements
- Dynamic layout switching per workspace (save/restore layouts)
- Gesture-based navigation (swiping between workspaces)
- Persistent splitter positions (remember column widths)
- Nested container support (widgets containing sub-widgets)

### 8.3 Input/UX
- Smart autocomplete based on usage patterns
- Voice command support (via speech-to-text)
- Context-sensitive help tooltips
- Macro recording and playback
- Vim-mode bindings (hjkl navigation, command sequences)

### 8.4 Visual Feedback
- Animated transitions between workspaces
- Notification badges on widgets with updates
- Mini-map for large widget content
- Progress indicators for long operations
- Toast notifications for status updates

### 8.5 Accessibility
- High-contrast theme options
- Larger font/icon scaling
- Screen reader support
- Keyboard-only navigation certification
- Customizable key bindings per user profile

---

## Conclusion

SuperTUI presents a sophisticated, extensible framework for building terminal-aesthetic desktop applications. The separation of concerns (navigation, layout, input, visual feedback, widget communication) creates a flexible system that can accommodate diverse UI patterns while maintaining a cohesive design language.

The key architectural strengths are:
1. **Modular Layout Engine System** - Multiple layout strategies with consistent API
2. **Event-Driven Communication** - Loosely-coupled inter-widget updates
3. **Theme System** - Comprehensive visual customization
4. **Error Isolation** - Widget crashes don't affect the application
5. **Keyboard-First Design** - All interactions accessible without mouse
6. **Dependency Injection** - Clean testability and dependency management

These foundations enable sophisticated UX innovations while maintaining stability and extensibility.
