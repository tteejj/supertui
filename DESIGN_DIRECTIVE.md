# **SUPER-TUI DESIGN DIRECTIVE**

## **PROJECT VISION**

**Name:** SuperTUI (PowerShell Project Management Terminal UI)

**Mission:** Create a powerful, extensible, .NET-powered TUI framework focused on project management with a clean, declarative PowerShell API that reduces boilerplate by 70-80%.

**Core Principle:** Infrastructure in C#, Business Logic in PowerShell

---

## **I. OVERALL ARCHITECTURE**

### **Three-Layer Architecture**

```
┌─────────────────────────────────────────────────────────────┐
│  Layer 3: Application Layer (PowerShell)                    │
│  ├─ Screens/                                                │
│  │  ├─ TaskListScreen.ps1                                   │
│  │  ├─ ProjectFormScreen.ps1                                │
│  │  └─ KanbanScreen.ps1                                     │
│  ├─ Services/                                               │
│  │  ├─ TaskService.ps1                                      │
│  │  └─ ProjectService.ps1                                   │
│  └─ App.ps1 (Main entry point)                              │
├─────────────────────────────────────────────────────────────┤
│  Layer 2: PowerShell API (Module)                           │
│  ├─ SuperTUI.psm1                                           │
│  │  ├─ Fluent Builders (New-GridLayout, New-DataGrid)       │
│  │  ├─ Screen Templates (New-ListScreen, New-FormScreen)    │
│  │  ├─ Service Helpers (Register-Service, Get-Service)      │
│  │  └─ Navigation Helpers (Push-Screen, Pop-Screen)         │
│  └─ SuperTUI.psd1 (Module manifest)                         │
├─────────────────────────────────────────────────────────────┤
│  Layer 1: Core Engine (C# - Inline compiled)                │
│  ├─ SuperTUI.Core.cs (~1500 lines)                          │
│  │  ├─ Base Classes (UIElement, Screen, Component)          │
│  │  ├─ Layout System (GridLayout, StackLayout)              │
│  │  ├─ Components (Label, Button, TextBox, DataGrid, List)  │
│  │  ├─ Navigation (ScreenManager, Router)                   │
│  │  ├─ Events (EventBus with C# events)                     │
│  │  ├─ Data (ObservableCollection wrappers)                 │
│  │  ├─ Rendering (VT100, Terminal, RenderContext)           │
│  │  └─ Services (ServiceContainer)                          │
│  └─ Compiled on first module import via Add-Type            │
└─────────────────────────────────────────────────────────────┘
```

### **File Structure**

```
SuperTUI/
├── Core/
│   └── SuperTUI.Core.cs           # C# engine (inline Add-Type)
├── Module/
│   ├── SuperTUI.psm1              # PowerShell API layer
│   ├── SuperTUI.psd1              # Module manifest
│   └── Builders/
│       ├── LayoutBuilders.ps1     # New-GridLayout, New-StackLayout
│       ├── ComponentBuilders.ps1  # New-DataGrid, New-Button, etc.
│       └── ScreenBuilders.ps1     # New-ListScreen, New-FormScreen
├── App/
│   ├── App.ps1                    # Main entry point
│   ├── Screens/
│   │   ├── Tasks/
│   │   │   ├── TaskListScreen.ps1
│   │   │   ├── TaskFormScreen.ps1
│   │   │   └── TaskDetailScreen.ps1
│   │   ├── Projects/
│   │   │   ├── ProjectListScreen.ps1
│   │   │   ├── ProjectFormScreen.ps1
│   │   │   └── ProjectStatsScreen.ps1
│   │   ├── Views/
│   │   │   ├── TodayScreen.ps1
│   │   │   ├── WeekScreen.ps1
│   │   │   ├── MonthScreen.ps1
│   │   │   ├── AgendaScreen.ps1
│   │   │   ├── KanbanScreen.ps1
│   │   │   ├── CalendarScreen.ps1
│   │   │   ├── OverdueScreen.ps1
│   │   │   ├── UpcomingScreen.ps1
│   │   │   ├── TomorrowScreen.ps1
│   │   │   ├── NextActionsScreen.ps1
│   │   │   ├── BlockedScreen.ps1
│   │   │   ├── NoDueDateScreen.ps1
│   │   │   └── BurndownScreen.ps1
│   │   ├── Tools/
│   │   │   ├── FileExplorerScreen.ps1
│   │   │   ├── NotesScreen.ps1
│   │   │   ├── CommandLibraryScreen.ps1
│   │   │   ├── TimerScreen.ps1
│   │   │   ├── SearchScreen.ps1
│   │   │   ├── MultiSelectScreen.ps1
│   │   │   └── HelpScreen.ps1
│   │   ├── Time/
│   │   │   ├── TimeListScreen.ps1
│   │   │   ├── TimeAddScreen.ps1
│   │   │   ├── TimeEditScreen.ps1
│   │   │   ├── TimeDeleteScreen.ps1
│   │   │   └── TimeReportScreen.ps1
│   │   ├── Dependencies/
│   │   │   ├── DependencyAddScreen.ps1
│   │   │   ├── DependencyRemoveScreen.ps1
│   │   │   └── DependencyViewScreen.ps1
│   │   ├── Focus/
│   │   │   ├── FocusSetScreen.ps1
│   │   │   ├── FocusClearScreen.ps1
│   │   │   └── FocusStatusScreen.ps1
│   │   ├── Backup/
│   │   │   ├── BackupViewScreen.ps1
│   │   │   ├── BackupRestoreScreen.ps1
│   │   │   └── BackupClearScreen.ps1
│   │   └── System/
│   │       ├── MainMenuScreen.ps1
│   │       ├── ThemeScreen.ps1
│   │       ├── ThemeEditorScreen.ps1
│   │       ├── SettingsScreen.ps1
│   │       ├── UndoScreen.ps1
│   │       └── RedoScreen.ps1
│   └── Services/
│       ├── DataService.ps1        # Task/Project data management
│       ├── ThemeService.ps1       # Theme management
│       └── FileService.ps1        # File I/O operations
└── README.md
```

**NOTE:** The screens listed above are based on the current ConsoleUI implementation (58 screens). Additional screens will be needed as the project evolves and new features are identified. The architecture is designed to be extensible to accommodate future screen types.

---

## **II. CORE ENGINE (Layer 1) - C# FOUNDATION**

### **Design Goals**
- **Compile on load** via `Add-Type` (simplicity over DLL management)
- **No external dependencies** (pure .NET framework types)
- **Thin but complete** (~1500 lines covers all infrastructure)
- **PowerShell-friendly** (scriptblock callbacks, dynamic typing support)

### **Core Classes Overview**

```csharp
// === CORE HIERARCHY ===

UIElement (abstract base)
├── Component (abstract - interactive elements)
│   ├── Label
│   ├── Button
│   ├── TextBox
│   ├── DataGrid
│   ├── ListView
│   ├── TreeView
│   └── Panel
└── Screen (abstract - full-screen views)
    ├── ContentScreen (single content area)
    └── DialogScreen (modal dialogs)

// === LAYOUT CLASSES ===

Layout (abstract)
├── GridLayout (row/column grid)
├── StackLayout (vertical/horizontal stack)
└── DockLayout (top/bottom/left/right/fill)

// === SUPPORT CLASSES ===

ScreenManager (navigation stack)
Router (URL-like routing)
EventBus (pub/sub events)
ServiceContainer (dependency injection)
Terminal (VT100 rendering)
Theme (color schemes)
```

### **Key C# Code Structure**

```csharp
namespace SuperTUI {
    // === BASE ELEMENT ===
    public abstract class UIElement : INotifyPropertyChanged {
        // Position & Size
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        // Visibility & Focus
        public bool Visible { get; set; } = true;
        public bool CanFocus { get; set; } = false;
        public bool IsFocused { get; private set; }

        // Rendering
        public bool IsDirty { get; private set; } = true;
        public void Invalidate() {
            IsDirty = true;
            OnPropertyChanged("IsDirty");
        }

        // Abstract methods
        public abstract string Render(RenderContext ctx);

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // === SCREEN BASE ===
    public abstract class Screen : UIElement {
        public string Title { get; set; }
        public List<UIElement> Children { get; } = new List<UIElement>();
        public Dictionary<string, Action<object>> KeyBindings { get; } = new Dictionary<string, Action<object>>();

        // Lifecycle
        public virtual void OnActivate() { }
        public virtual void OnDeactivate() { }
        public virtual void OnResize(int width, int height) { }

        // Input handling
        public virtual bool HandleKey(ConsoleKeyInfo key) {
            string keyStr = KeyToString(key);
            if (KeyBindings.ContainsKey(keyStr)) {
                KeyBindings[keyStr]?.Invoke(null);
                return true;
            }
            return false;
        }

        // Rendering
        public override string Render(RenderContext ctx) {
            var sb = new StringBuilder();
            foreach (var child in Children) {
                if (child.Visible) {
                    sb.Append(child.Render(ctx));
                }
            }
            return sb.ToString();
        }
    }

    // === GRID LAYOUT ===
    public class GridLayout : UIElement {
        public List<RowDefinition> Rows { get; } = new List<RowDefinition>();
        public List<ColumnDefinition> Columns { get; } = new List<ColumnDefinition>();
        private List<GridChild> _children = new List<GridChild>();

        public void AddChild(UIElement element, int row, int col, int rowSpan = 1, int colSpan = 1) {
            _children.Add(new GridChild { Element = element, Row = row, Column = col, RowSpan = rowSpan, ColSpan = colSpan });
            element.PropertyChanged += (s, e) => { if (e.PropertyName == "IsDirty") Invalidate(); };
        }

        public override string Render(RenderContext ctx) {
            // Calculate cell sizes
            CalculateLayout();

            // Position and render children
            var sb = new StringBuilder();
            foreach (var child in _children) {
                var bounds = GetCellBounds(child.Row, child.Column, child.RowSpan, child.ColSpan);
                child.Element.X = this.X + bounds.X;
                child.Element.Y = this.Y + bounds.Y;
                child.Element.Width = bounds.Width;
                child.Element.Height = bounds.Height;
                sb.Append(child.Element.Render(ctx));
            }
            return sb.ToString();
        }
    }

    // === DATAGRID (with auto-binding) ===
    public class DataGrid : UIElement {
        private INotifyCollectionChanged _itemsSource;
        public IEnumerable ItemsSource {
            get => _itemsSource as IEnumerable;
            set {
                if (_itemsSource != null)
                    _itemsSource.CollectionChanged -= OnCollectionChanged;
                _itemsSource = value as INotifyCollectionChanged;
                if (_itemsSource != null)
                    _itemsSource.CollectionChanged += OnCollectionChanged;
                Invalidate();
            }
        }

        public List<GridColumn> Columns { get; } = new List<GridColumn>();
        public int SelectedIndex { get; set; } = 0;
        public event EventHandler<ItemSelectedEventArgs> ItemSelected;

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            Invalidate();  // Auto-refresh!
        }

        public override string Render(RenderContext ctx) {
            // Render table with VT100 box-drawing
        }
    }

    // === SCREEN MANAGER ===
    public class ScreenManager {
        private static ScreenManager _instance = new ScreenManager();
        public static ScreenManager Instance => _instance;

        private Stack<Screen> _stack = new Stack<Screen>();
        public Screen Current => _stack.Count > 0 ? _stack.Peek() : null;

        public void Push(Screen screen) {
            if (_stack.Count > 0) _stack.Peek().OnDeactivate();
            _stack.Push(screen);
            screen.OnActivate();
            Terminal.Instance.Invalidate();
        }

        public void Pop() {
            if (_stack.Count > 0) {
                _stack.Pop().OnDeactivate();
                if (_stack.Count > 0) _stack.Peek().OnActivate();
                Terminal.Instance.Invalidate();
            }
        }

        public void Replace(Screen screen) {
            if (_stack.Count > 0) _stack.Pop().OnDeactivate();
            _stack.Push(screen);
            screen.OnActivate();
            Terminal.Instance.Invalidate();
        }
    }

    // === EVENT BUS ===
    public class EventBus {
        private static EventBus _instance = new EventBus();
        public static EventBus Instance => _instance;

        // Typed events
        public event EventHandler<DataEventArgs> TaskCreated;
        public event EventHandler<DataEventArgs> TaskUpdated;
        public event EventHandler<DataEventArgs> TaskDeleted;
        public event EventHandler<DataEventArgs> ProjectCreated;
        public event EventHandler<DataEventArgs> ProjectUpdated;

        public void PublishTaskCreated(object data) => TaskCreated?.Invoke(this, new DataEventArgs(data));
        public void PublishTaskUpdated(object data) => TaskUpdated?.Invoke(this, new DataEventArgs(data));
        // ... etc
    }
}
```

---

## **III. NAVIGATION SYSTEM**

### **Design: Stack-Based with Routing**

The navigation system combines **alcar's stack-based ScreenManager** with **_R2's routing** for the best of both worlds.

### **Navigation Methods**

```csharp
// C# ScreenManager (Layer 1)
ScreenManager.Instance.Push(screen)      // Navigate to new screen
ScreenManager.Instance.Pop()             // Go back
ScreenManager.Instance.Replace(screen)   // Replace current screen
ScreenManager.Instance.Current           // Get current screen
```

```powershell
# PowerShell API (Layer 2) - Simplified
Push-Screen TaskListScreen
Push-Screen (New-TaskFormScreen -TaskId 42)
Pop-Screen
```

### **Routing System (Optional URL-style)**

```csharp
// C# Router
public class Router {
    private Dictionary<string, Func<Dictionary<string, string>, Screen>> _routes = new Dictionary<string, Func<Dictionary<string, string>, Screen>>();

    public void Register(string pattern, Func<Dictionary<string, string>, Screen> factory) {
        _routes[pattern] = factory;
    }

    public Screen Navigate(string url) {
        // Parse URL: "/tasks/42/edit"
        // Match pattern: "/tasks/:id/edit"
        // Extract params: { "id": "42" }
        // Call factory(params)
        // Return screen instance
    }
}
```

```powershell
# PowerShell usage
Register-Route "/tasks" { New-TaskListScreen }
Register-Route "/tasks/:id" { param($params) New-TaskDetailScreen -TaskId $params.id }
Register-Route "/tasks/:id/edit" { param($params) New-TaskFormScreen -TaskId $params.id }

# Navigate
Navigate-To "/tasks/42/edit"
```

### **Screen Lifecycle**

```
1. Screen Created (Constructor)
   ↓
2. Screen.OnActivate()    [Screen pushed to stack]
   ↓
3. Screen.Render()        [Every frame while active]
   ↓
4. Screen.HandleKey()     [On user input]
   ↓
5. Screen.OnDeactivate()  [New screen pushed or screen popped]
   ↓
6. Disposed               [If popped from stack]
```

---

## **IV. LAYOUT SYSTEM**

### **Design Philosophy: Declarative CSS-like Layouts**

**Goal:** Zero manual position calculations. All layouts are declarative.

### **Three Layout Types**

#### **1. GridLayout** (Most Flexible)

```powershell
# Define grid
$layout = New-GridLayout -Rows "Auto", "*", "40" -Columns "200", "*", "200"
#                               ↑      ↑    ↑       ↑      ↑    ↑
#                               │      │    │       │      │    └─ Fixed 200px
#                               │      │    │       │      └────── Fill remaining
#                               │      │    │       └───────────── Fixed 200px
#                               │      │    └───────────────────── Fixed 40px height
#                               │      └────────────────────────── Fill remaining
#                               └───────────────────────────────── Auto-size to content

# Add children to cells
$layout.AddChild($header, 0, 0, 1, 3)  # row=0, col=0, rowspan=1, colspan=3 (spans all columns)
$layout.AddChild($sidebar, 1, 0)       # row=1, col=0 (left sidebar)
$layout.AddChild($content, 1, 1)       # row=1, col=1 (main content)
$layout.AddChild($info, 1, 2)          # row=1, col=2 (right info panel)
$layout.AddChild($footer, 2, 0, 1, 3)  # row=2, col=0, colspan=3 (full width footer)
```

**Visual Result:**
```
┌────────────────────────────────────────────────┐
│              Header (spans all 3 cols)         │  ← Row 0 (Auto)
├─────────┬───────────────────────┬──────────────┤
│         │                       │              │
│ Sidebar │   Main Content        │  Info Panel  │  ← Row 1 (Fill *)
│ (200px) │   (Fill *)            │  (200px)     │
│         │                       │              │
├─────────┴───────────────────────┴──────────────┤
│              Footer (40px height)              │  ← Row 2 (40px)
└────────────────────────────────────────────────┘
```

#### **2. StackLayout** (Simpler)

```powershell
# Vertical stack
$stack = New-StackLayout -Orientation Vertical -Spacing 1

$stack.AddChild($title)
$stack.AddChild($description)
$stack.AddChild($buttons)
# Children auto-arranged top-to-bottom with 1-line spacing

# Horizontal stack
$hstack = New-StackLayout -Orientation Horizontal -Spacing 2
$hstack.AddChild($label)
$hstack.AddChild($textbox)
# Children auto-arranged left-to-right with 2-char spacing
```

#### **3. DockLayout** (Edges + Fill)

```powershell
$dock = New-DockLayout

$dock.AddChild($toolbar, "Top")     # Docked to top edge
$dock.AddChild($statusBar, "Bottom") # Docked to bottom
$dock.AddChild($tree, "Left")        # Docked to left
$dock.AddChild($props, "Right")      # Docked to right
$dock.AddChild($editor, "Fill")      # Fills remaining center
```

### **Layout Calculation**

C# handles all math automatically:

```csharp
public class GridLayout : UIElement {
    private void CalculateLayout() {
        // 1. Calculate Auto rows (measure content)
        // 2. Allocate Fixed rows (exact heights)
        // 3. Distribute remaining space to * rows
        // 4. Same for columns
        // 5. Position all children
    }
}
```

**PowerShell never does layout math!**

---

## **V. SCREEN/VIEW IMPLEMENTATION**

### **How a Screen Works: Step-by-Step**

#### **Example: TaskListScreen**

```powershell
# File: App/Screens/Tasks/TaskListScreen.ps1

using namespace SuperTUI

class TaskListScreen : Screen {
    [object]$_taskService
    [object]$_grid

    TaskListScreen() {
        # 1. Set screen properties
        $this.Title = "Tasks"
        $this.CanFocus = $false  # Grid handles focus

        # 2. Get services (dependency injection)
        $this._taskService = Get-Service "TaskService"

        # 3. Create layout
        $layout = New-GridLayout -Rows "Auto", "*", "Auto" -Columns "*"

        # 4. Create header
        $header = New-Label -Text "My Tasks" -Style "Header"
        $layout.AddChild($header, 0, 0)

        # 5. Create data grid with auto-binding
        $this._grid = New-DataGrid `
            -ItemsSource $this._taskService.Tasks `  # ObservableCollection!
            -Columns @(
                @{ Header = "ID"; Property = "Id"; Width = 5 }
                @{ Header = "Title"; Property = "Title"; Width = "*" }
                @{ Header = "Due"; Property = "DueDate"; Width = 12 }
                @{ Header = "Priority"; Property = "Priority"; Width = 10 }
            ) `
            -CanFocus $true `
            -OnItemSelected { param($item) $this.ViewTask($item) }

        $layout.AddChild($this._grid, 1, 0)

        # 6. Create footer with instructions
        $footer = New-Label -Text "N:New  E:Edit  D:Delete  Enter:View  Esc:Back" -Style "Footer"
        $layout.AddChild($footer, 2, 0)

        # 7. Add layout to screen
        $this.Children.Add($layout)

        # 8. Register key bindings
        $this.RegisterKey("N", { $this.NewTask() })
        $this.RegisterKey("E", { $this.EditTask() })
        $this.RegisterKey("D", { $this.DeleteTask() })
        $this.RegisterKey("Enter", { $this.ViewTask($this._grid.SelectedItem) })
        $this.RegisterKey("Escape", { Pop-Screen })

        # 9. Subscribe to events (auto-cleanup on dispose!)
        [EventBus]::Instance.add_TaskCreated({
            # Grid auto-refreshes because bound to ObservableCollection
            # No code needed here! Just for additional logic if wanted
        })
    }

    # Action methods
    [void] NewTask() {
        Push-Screen (New-TaskFormScreen)
    }

    [void] EditTask() {
        $item = $this._grid.SelectedItem
        if ($item) {
            Push-Screen (New-TaskFormScreen -TaskId $item.Id)
        }
    }

    [void] DeleteTask() {
        $item = $this._grid.SelectedItem
        if ($item) {
            $confirmed = Show-ConfirmDialog "Delete task '$($item.Title)'?"
            if ($confirmed) {
                $this._taskService.DeleteTask($item.Id)
                # Grid auto-updates!
            }
        }
    }

    [void] ViewTask($item) {
        if ($item) {
            Push-Screen (New-TaskDetailScreen -TaskId $item.Id)
        }
    }
}
```

**Key Points:**
1. **No manual rendering** - Layout handles positioning
2. **No manual data refresh** - ObservableCollection auto-updates grid
3. **Declarative key bindings** - Clean, readable
4. **Services injected** - Testable, decoupled
5. **~50 lines** vs ~200+ in current ConsoleUI

---

### **Example: TaskFormScreen (Dialog)**

```powershell
class TaskFormScreen : DialogScreen {
    [int]$_taskId = 0
    [object]$_taskService
    [hashtable]$_fields = @{}

    TaskFormScreen([int]$taskId = 0) {
        $this._taskId = $taskId
        $this._taskService = Get-Service "TaskService"
        $this.Title = if ($taskId) { "Edit Task" } else { "New Task" }

        # Load existing task if editing
        $task = if ($taskId) { $this._taskService.GetTask($taskId) } else { $null }

        # Create form layout
        $form = New-StackLayout -Orientation Vertical -Spacing 1

        # Title field
        $this._fields.Title = New-TextBox `
            -Label "Title:" `
            -Value $(if ($task) { $task.Title } else { "" }) `
            -Required $true
        $form.AddChild($this._fields.Title)

        # Due date field
        $this._fields.DueDate = New-TextBox `
            -Label "Due Date:" `
            -Value $(if ($task) { $task.DueDate } else { "" }) `
            -Placeholder "yyyy-MM-dd or 'today', 'tomorrow', '+3'"
        $form.AddChild($this._fields.DueDate)

        # Priority dropdown
        $this._fields.Priority = New-SelectList `
            -Label "Priority:" `
            -Items @("Low", "Normal", "High", "Critical") `
            -SelectedValue $(if ($task) { $task.Priority } else { "Normal" })
        $form.AddChild($this._fields.Priority)

        # Project selector
        $projects = $this._taskService.GetAllProjects()
        $this._fields.Project = New-SelectList `
            -Label "Project:" `
            -Items $projects `
            -DisplayProperty "Name" `
            -ValueProperty "Id" `
            -SelectedValue $(if ($task) { $task.ProjectId } else { $null })
        $form.AddChild($this._fields.Project)

        # Buttons
        $buttons = New-StackLayout -Orientation Horizontal -Spacing 2
        $buttons.AddChild((New-Button -Label "Save" -OnClick { $this.Save() }))
        $buttons.AddChild((New-Button -Label "Cancel" -OnClick { Pop-Screen }))
        $form.AddChild($buttons)

        $this.Children.Add($form)

        # Key bindings
        $this.RegisterKey("Ctrl+S", { $this.Save() })
        $this.RegisterKey("Escape", { Pop-Screen })
    }

    [void] Save() {
        # Validate
        if ([string]::IsNullOrWhiteSpace($this._fields.Title.Value)) {
            Show-ErrorDialog "Title is required"
            return
        }

        # Create/update task
        $taskData = @{
            Id = $this._taskId
            Title = $this._fields.Title.Value
            DueDate = $this._fields.DueDate.Value
            Priority = $this._fields.Priority.SelectedValue
            ProjectId = $this._fields.Project.SelectedValue
        }

        if ($this._taskId) {
            $this._taskService.UpdateTask($taskData)
        } else {
            $this._taskService.CreateTask($taskData)
        }

        Pop-Screen  # Return to list
        # List auto-refreshes via ObservableCollection!
    }
}
```

---

## **VI. COMPONENT LIBRARY**

### **Core Components (All in C#)**

| Component | Purpose | Auto-Binding | Key Features |
|-----------|---------|--------------|--------------|
| **Label** | Text display | No | Styles, wrapping, alignment |
| **Button** | Clickable action | No | OnClick event, hotkeys |
| **TextBox** | Single-line input | Yes (Value property) | Validation, placeholder, mask |
| **TextArea** | Multi-line input | Yes (Value property) | Scrolling, selection, undo/redo |
| **DataGrid** | Table view | Yes (ItemsSource) | Sorting, filtering, selection |
| **ListView** | Simple list | Yes (ItemsSource) | Icons, multi-select |
| **TreeView** | Hierarchical data | Yes (ItemsSource) | Expand/collapse, lazy load |
| **SelectList** | Dropdown | No | Searchable, custom display |
| **CheckBox** | Boolean toggle | Yes (Checked property) | Tri-state support |
| **RadioGroup** | Single selection | Yes (SelectedValue) | Auto-layout |
| **ProgressBar** | Visual progress | Yes (Value property) | Indeterminate mode |
| **TabControl** | Multiple pages | No | Tab switching, lazy load |
| **SplitPane** | Resizable splits | No | Horizontal/vertical, drag |
| **FileExplorer** | File browser | Yes (CurrentPath) | Tree + list, multi-select |
| **Calendar** | Date picker | Yes (SelectedDate) | Month view, navigation |

---

## **VII. DATA FLOW & EVENT SYSTEM**

### **Data Flow Pattern**

```
User Action (Key press)
    ↓
Screen.HandleKey() or Component.OnClick()
    ↓
Service Method (CreateTask, UpdateTask, etc.)
    ↓
Service Updates ObservableCollection
    ↓
ObservableCollection fires CollectionChanged event
    ↓
DataGrid receives event, calls Invalidate()
    ↓
Next frame: DataGrid.Render() reads updated collection
    ↓
UI automatically updated!
```

**No manual LoadData() calls needed!**

### **Event Bus Usage**

```powershell
# Service publishes events
class TaskService {
    [System.Collections.ObjectModel.ObservableCollection[object]]$Tasks

    [void] CreateTask($data) {
        $task = # ... create task
        $this.Tasks.Add($task)  # Auto-triggers UI update
        [EventBus]::Instance.PublishTaskCreated($task)  # For other screens
    }
}

# Other screens subscribe
class DashboardScreen : Screen {
    DashboardScreen() {
        # ...
        [EventBus]::Instance.add_TaskCreated({
            param($sender, $args)
            $this.RefreshStats()  # Update dashboard stats
        })
    }
}
```

---

## **VIII. VISUAL DESIGN SYSTEM**

### **Theme Structure**

```csharp
public class Theme {
    // Primary colors
    public Color Primary { get; set; }      // #0078D4 (blue)
    public Color Secondary { get; set; }    // #6B6B6B (gray)
    public Color Success { get; set; }      // #107C10 (green)
    public Color Warning { get; set; }      // #FF8C00 (orange)
    public Color Error { get; set; }        // #E81123 (red)

    // UI elements
    public Color Background { get; set; }
    public Color Foreground { get; set; }
    public Color Border { get; set; }
    public Color Focus { get; set; }
    public Color Selection { get; set; }

    // Text styles
    public TextStyle Header { get; set; }   // Bold, larger
    public TextStyle Footer { get; set; }   // Dim
    public TextStyle Emphasis { get; set; } // Bold
}
```

### **Box-Drawing Style**

```
┌─────────────────────────────────────────┐
│ Header                                  │
├─────────────────────────────────────────┤
│ Content area                            │
│                                         │
│ ▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░░░░░░ 50%        │
└─────────────────────────────────────────┘
```

---

## **IX. IMPLEMENTATION PRIORITY**

### **Phase 1: Core Foundation** (Week 1)
- [ ] Write `SuperTUI.Core.cs` (C# engine)
- [ ] Implement base classes (UIElement, Screen, Component)
- [ ] Implement layouts (GridLayout, StackLayout)
- [ ] Implement basic components (Label, Button, TextBox, DataGrid)
- [ ] Implement navigation (ScreenManager)
- [ ] Implement rendering (Terminal, VT100)
- [ ] Test with simple screen

### **Phase 2: PowerShell API** (Week 2)
- [ ] Create `SuperTUI.psm1` module
- [ ] Implement fluent builders
- [ ] Implement service registration
- [ ] Create screen templates
- [ ] Port theme system
- [ ] Test with real data

### **Phase 3: Essential Screens** (Week 3)
- [ ] MainMenuScreen
- [ ] TaskListScreen
- [ ] TaskFormScreen
- [ ] ProjectListScreen
- [ ] ProjectFormScreen
- [ ] TodayScreen
- [ ] WeekScreen

### **Phase 4: Advanced Features** (Week 4)
- [ ] FileExplorerScreen
- [ ] CalendarScreen
- [ ] NotesScreen
- [ ] CommandLibraryScreen
- [ ] KanbanScreen
- [ ] TreeView component
- [ ] TabControl component

---

## **X. DESIGN COHESION PRINCIPLES**

### **The Three Rules**

1. **Infrastructure in C#, Logic in PowerShell**
   - C# = Layout, rendering, events, components
   - PowerShell = Screens, services, business rules

2. **Declarative Over Imperative**
   - Use layouts, not manual positioning
   - Use data binding, not manual refresh
   - Use key bindings, not switch statements

3. **Convention Over Configuration**
   - Standard patterns for screens
   - Standard service names
   - Standard event names

### **Success Metrics**

- ✅ 70-80% code reduction vs current ConsoleUI
- ✅ New screen in <50 lines
- ✅ Zero manual position calculations
- ✅ Zero manual data refresh calls
- ✅ Compile time < 2 seconds (inline Add-Type)
- ✅ Smooth 30+ FPS rendering

---

## **XI. PROJECT SETUP & TOOLING**

This section will be populated with project configuration, build scripts, and development workflow documentation as the project progresses.
