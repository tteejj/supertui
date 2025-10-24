# SuperTUI WPF Framework Architecture

## Overview
A workspace/widget-based TUI framework in WPF with terminal aesthetic. Think i3 window manager meets desktop widgets, keyboard-driven, modular, and reactive.

## Core Concepts

### 1. Workspace
A "desktop" containing a layout of widgets and screens.
- Multiple workspaces (like i3 workspaces or virtual desktops)
- Switch with Ctrl+1, Ctrl+2, ... Ctrl+9
- Each workspace has independent layout configuration
- Persisted to config file

**Example:**
```powershell
# Workspace 1: Dashboard
Workspace "Dashboard" {
    Grid -Rows 2 -Columns 2 {
        Widget "Clock" -Position 0,0
        Widget "Calendar" -Position 0,1
        Widget "TaskSummary" -Position 1,0
        Widget "Weather" -Position 1,1
    }
}

# Workspace 2: Project Management
Workspace "Projects" {
    Dock {
        Screen "ProjectList" -Dock Left -Width 400
        Screen "TaskDetail" -Dock Fill
    }
}
```

### 2. Widget
Self-contained, reactive component that displays data.
- Small, focused (clock, calendar, task count, etc.)
- Read-only or minimal interaction
- Auto-updates via data binding
- Implemented as WPF UserControl

**Base Class:**
```csharp
public abstract class WidgetBase : UserControl
{
    public string WidgetName { get; set; }
    public abstract void Initialize();
    public abstract void Refresh();
    public virtual void OnActivated() { }
    public virtual void OnDeactivated() { }
}
```

**Examples:**
- ClockWidget - Current time
- CalendarWidget - Month view
- TaskSummaryWidget - Task count by status
- ProjectListWidget - Active projects
- WeatherWidget - Current weather
- QuoteWidget - Random quote
- SystemInfoWidget - CPU/Memory

### 3. Screen
Larger, interactive component for complex workflows.
- Full CRUD operations (tasks, projects, etc.)
- Keyboard navigation (arrow keys, tab, enter)
- Can spawn child screens/dialogs
- Implemented as WPF UserControl

**Base Class:**
```csharp
public abstract class ScreenBase : UserControl
{
    public string ScreenName { get; set; }
    public abstract void Initialize();
    public abstract void OnKeyDown(KeyEventArgs e);
    public abstract void OnFocusReceived();
    public abstract void OnFocusLost();
    public virtual bool CanClose() => true;
}
```

**Examples:**
- TaskScreen - Task list with CRUD
- ProjectScreen - Project management
- CalendarScreen - Full calendar with events
- FileExplorerScreen - File browser
- SettingsScreen - Configuration

### 4. Layout System
Defines how widgets/screens are arranged in a workspace.

**Layout Types:**
1. **GridLayout** - CSS-like grid (rows/columns)
2. **DockLayout** - Top/Bottom/Left/Right/Fill
3. **StackLayout** - Vertical or horizontal stack
4. **TileLayout** - i3-like binary tree tiling
5. **AbsoluteLayout** - Manual positioning (fallback)

**Layout Engine:**
```csharp
public abstract class LayoutEngine
{
    public abstract void AddWidget(WidgetBase widget, LayoutParams param);
    public abstract void RemoveWidget(WidgetBase widget);
    public abstract void Recalculate();
}
```

### 5. WorkspaceManager
Manages workspace switching and lifecycle.

```csharp
public class WorkspaceManager
{
    public ObservableCollection<Workspace> Workspaces { get; }
    public Workspace CurrentWorkspace { get; }

    public void SwitchToWorkspace(int index);
    public void AddWorkspace(Workspace workspace);
    public void RemoveWorkspace(int index);
    public void SaveConfiguration();
    public void LoadConfiguration();
}
```

## Data Flow

### Reactive Updates
```
Data Source (ObservableCollection, INotifyPropertyChanged)
    ↓
WPF Data Binding
    ↓
Widget/Screen UI Updates Automatically
```

**Example:**
```csharp
// In PowerShell or C#
public class TaskService
{
    public ObservableCollection<Task> Tasks { get; } = new();

    public void AddTask(Task task)
    {
        Tasks.Add(task);  // UI updates automatically
    }
}
```

**In Widget XAML:**
```xml
<ListBox ItemsSource="{Binding TaskService.Tasks}" />
```

### Event System
```
User Input (Keyboard/Mouse)
    ↓
WorkspaceManager dispatches to Current Workspace
    ↓
Workspace dispatches to Focused Widget/Screen
    ↓
Widget/Screen handles input
    ↓
Optional: Publish event to EventBus
    ↓
Other Widgets subscribe and react
```

## PowerShell API

### Defining Workspaces
```powershell
# Import framework
Import-Module SuperTUI

# Define workspace 1
New-Workspace -Name "Dashboard" -Index 1 {
    New-GridLayout -Rows 2 -Columns 2 {
        New-Widget -Type "Clock" -Row 0 -Column 0
        New-Widget -Type "Calendar" -Row 0 -Column 1 -RowSpan 2
        New-Widget -Type "TaskSummary" -Row 1 -Column 0
    }
}

# Define workspace 2
New-Workspace -Name "Tasks" -Index 2 {
    New-DockLayout {
        New-Widget -Type "ProjectList" -Dock Left -Width 300
        New-Screen -Type "TaskScreen" -Dock Fill
    }
}

# Start framework
Start-SuperTUI
```

### Keyboard Shortcuts
```powershell
# Global shortcuts
Register-Shortcut -Key "Ctrl+Q" -Action { Stop-SuperTUI }
Register-Shortcut -Key "Ctrl+1" -Action { Switch-Workspace 1 }
Register-Shortcut -Key "Ctrl+2" -Action { Switch-Workspace 2 }

# Workspace-specific shortcuts
Register-Shortcut -Workspace "Tasks" -Key "Ctrl+N" -Action {
    New-Task
}
```

## Widget Development

### Creating a Custom Widget
```powershell
# 1. Create XAML (ClockWidget.xaml)
<UserControl x:Class="SuperTUI.Widgets.ClockWidget" ...>
    <Border Style="{StaticResource WidgetBorder}">
        <StackPanel>
            <TextBlock Text="{Binding CurrentTime}"
                       Style="{StaticResource ClockStyle}" />
        </StackPanel>
    </Border>
</UserControl>

# 2. Create Code-Behind (ClockWidget.xaml.cs)
public class ClockWidget : WidgetBase
{
    public string CurrentTime { get; set; }
    private DispatcherTimer timer;

    public override void Initialize()
    {
        timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (s, e) => {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            OnPropertyChanged(nameof(CurrentTime));
        };
        timer.Start();
    }
}

# 3. Register Widget
Register-Widget -Name "Clock" -Type "SuperTUI.Widgets.ClockWidget"

# 4. Use in Workspace
New-Widget -Type "Clock" -Row 0 -Column 0
```

## Configuration

### Workspace Config (JSON)
```json
{
  "workspaces": [
    {
      "name": "Dashboard",
      "index": 1,
      "layout": {
        "type": "Grid",
        "rows": 2,
        "columns": 2,
        "widgets": [
          { "type": "Clock", "row": 0, "column": 0 },
          { "type": "Calendar", "row": 0, "column": 1, "rowSpan": 2 },
          { "type": "TaskSummary", "row": 1, "column": 0 }
        ]
      }
    },
    {
      "name": "Tasks",
      "index": 2,
      "layout": {
        "type": "Dock",
        "children": [
          { "type": "Widget", "name": "ProjectList", "dock": "Left", "width": 300 },
          { "type": "Screen", "name": "TaskScreen", "dock": "Fill" }
        ]
      }
    }
  ],
  "theme": "Terminal",
  "shortcuts": [
    { "key": "Ctrl+Q", "action": "Exit" },
    { "key": "Ctrl+1", "action": "SwitchWorkspace", "parameter": 1 }
  ]
}
```

## Services

### ServiceContainer
Central registry for services (like task storage, project management, etc.)

```csharp
public class ServiceContainer
{
    private Dictionary<Type, object> services = new();

    public void Register<T>(T service) => services[typeof(T)] = service;
    public T Get<T>() => (T)services[typeof(T)];
}
```

**Usage:**
```powershell
# Register services
$taskService = New-Object TaskService
Register-Service $taskService

# Widgets/Screens access via dependency injection
$widget.TaskService = Get-Service "TaskService"
```

## Theming

### Terminal Theme (ResourceDictionary)
```xml
<ResourceDictionary>
    <!-- Colors -->
    <SolidColorBrush x:Key="Background">#0C0C0C</SolidColorBrush>
    <SolidColorBrush x:Key="Foreground">#CCCCCC</SolidColorBrush>
    <SolidColorBrush x:Key="Border">#3A3A3A</SolidColorBrush>
    <SolidColorBrush x:Key="Accent">#4EC9B0</SolidColorBrush>

    <!-- Fonts -->
    <FontFamily x:Key="MonoFont">Cascadia Mono, Consolas</FontFamily>

    <!-- Styles -->
    <Style x:Key="WidgetBorder" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource Background}"/>
        <Setter Property="BorderBrush" Value="{StaticResource Border}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="10"/>
    </Style>
</ResourceDictionary>
```

## Implementation Phases

### Phase 1: Core Framework ✅ (Next)
- WorkspaceManager
- WidgetBase, ScreenBase
- GridLayout, DockLayout
- Theme system
- Basic keyboard routing

### Phase 2: Essential Widgets
- ClockWidget
- CalendarWidget
- TaskSummaryWidget
- ProjectListWidget

### Phase 3: Essential Screens
- TaskScreen (CRUD tasks)
- ProjectScreen (CRUD projects)
- SettingsScreen

### Phase 4: PowerShell API
- Workspace DSL
- Widget registration
- Shortcut registration
- Configuration persistence

### Phase 5: Advanced Features
- TileLayout (i3-style)
- Widget marketplace/plugins
- Mouse support
- Split panes within widgets
- Inter-widget communication

## Technical Decisions

**Language:** C# for framework, PowerShell for configuration/scripting
**UI Framework:** WPF (Windows-only, GPU-accelerated)
**Data Binding:** Native WPF binding with INotifyPropertyChanged
**Compilation:** Compile C# via Add-Type, load at runtime
**Configuration:** JSON for persistence, PowerShell DSL for authoring
**Modularity:** Widgets/Screens as separate UserControl files
**Keyboard:** Arrow keys, Tab, Enter, Escape + global shortcuts

## Success Metrics

- ✅ Create new workspace in <10 lines PowerShell
- ✅ Create new widget in <50 lines C#/XAML
- ✅ Switch workspaces instantly (< 100ms)
- ✅ Zero manual UI refresh calls (data binding)
- ✅ Modular - add/remove widgets without touching framework
- ✅ Keyboard-first - every action has shortcut
- ✅ Terminal aesthetic - looks like TUI, performs like GUI

## Open Questions

1. **Persistence:** Save workspace state (widget data) between sessions?
2. **Widget Communication:** Pub/sub event bus or direct references?
3. **Dynamic Layouts:** Allow runtime layout changes (drag/drop)?
4. **Multi-Monitor:** Support multiple physical monitors?
5. **Hotkey Conflicts:** How to handle global vs local shortcuts?

## Next Steps

1. Implement core framework (WorkspaceManager, WidgetBase, ScreenBase)
2. Build GridLayout and DockLayout engines
3. Create Terminal theme ResourceDictionary
4. Build 2-3 sample widgets (Clock, TaskSummary, Calendar)
5. Create simple PowerShell API
6. Test end-to-end workspace switching
