# SuperTUI WPF Framework

A workspace/widget-based desktop framework with terminal aesthetics. Think **i3 window manager meets desktop widgets**, keyboard-driven, modular, and reactive.

## Features

✅ **Multiple Workspaces** - Switch between desktops like i3 (Ctrl+1-9)
✅ **Widget System** - Modular, self-contained components (Clock, TaskSummary, etc.)
✅ **Screen System** - Larger interactive panels (Task Manager, Project Manager, etc.)
✅ **Flexible Layouts** - Grid, Dock, Stack layouts for arranging widgets
✅ **Terminal Aesthetic** - Dark theme, monospace fonts, minimal borders
✅ **Keyboard-First** - Arrow keys, Tab, Enter, Escape (NO VIM BINDINGS)
✅ **Reactive** - WPF data binding, zero manual UI refresh calls
✅ **Modular** - Add widgets/screens without touching framework code

## Quick Start

### On Windows (Your Work Machine)

1. Copy the `WPF/` folder to your Windows machine
2. Open PowerShell
3. Run:
   ```powershell
   cd path\to\supertui\WPF
   .\SuperTUI.ps1
   ```

That's it! The framework will compile and launch.

## Project Structure

```
WPF/
├── Core/
│   └── Framework.cs          # Core classes (Workspace, Widget, Layout, etc.)
├── Widgets/
│   ├── ClockWidget.cs        # Clock widget (time/date display)
│   └── TaskSummaryWidget.cs  # Task summary widget
├── Screens/
│   └── (empty - create your screens here)
├── Themes/
│   └── (empty - future theme support)
├── SuperTUI.ps1              # Main entry point
└── README.md                 # This file
```

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+1` to `Ctrl+9` | Switch to workspace 1-9 |
| `Ctrl+Left` / `Ctrl+Right` | Previous/Next workspace |
| `Ctrl+Q` | Quit |
| `Tab` | Switch focus between widgets/screens |
| `Arrow Keys` | Navigate within widgets/screens |
| `Enter` | Activate selected item |
| `Escape` | Cancel/Close |

## Current Workspaces

The demo includes 3 workspaces:

### Workspace 1: Dashboard
- **Layout:** 2x2 Grid
- **Widgets:**
  - Clock (top-left) - Live time/date
  - Task Summary (top-right) - Task counts
  - Placeholder widgets (bottom) - For future widgets

### Workspace 2: Projects
- **Layout:** Dock (left/fill)
- **Content:**
  - Project List (left panel)
  - Task Detail (right panel)

### Workspace 3: Empty
- Placeholder for your custom layout

## Creating a Custom Widget

### 1. Create the Widget Class

Create `Widgets/MyWidget.cs`:

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core;

namespace SuperTUI.Widgets
{
    public class MyWidget : WidgetBase
    {
        private TextBlock textBlock;

        public MyWidget()
        {
            WidgetType = "MyWidget";
            BuildUI();
        }

        private void BuildUI()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };

            textBlock = new TextBlock
            {
                Text = "Hello from MyWidget!",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176))
            };

            border.Child = textBlock;
            this.Content = border;
        }

        public override void Initialize()
        {
            // Initialize widget data
        }

        public override void Refresh()
        {
            // Update widget data
        }
    }
}
```

### 2. Add Widget to Compilation

Edit `SuperTUI.ps1`, add to compilation section:

```powershell
$myWidgetSource = Get-Content "$PSScriptRoot/Widgets/MyWidget.cs" -Raw

$combinedSource = @"
$frameworkSource
$clockWidgetSource
$taskSummarySource
$myWidgetSource
"@
```

### 3. Add Widget to Workspace

In `SuperTUI.ps1`, add to workspace definition:

```powershell
$myWidget = New-Object SuperTUI.Widgets.MyWidget
$myWidget.WidgetName = "MyWidget"
$myWidget.Initialize()

$params = New-Object SuperTUI.Core.LayoutParams
$params.Row = 1
$params.Column = 0

$workspace1.AddWidget($myWidget, $params)
```

## Creating a Custom Screen

Same process as widgets, but inherit from `ScreenBase` instead of `WidgetBase`:

```csharp
public class MyScreen : ScreenBase
{
    public MyScreen()
    {
        ScreenType = "MyScreen";
        BuildUI();
    }

    private void BuildUI()
    {
        // Build your screen UI
    }

    public override void Initialize()
    {
        // Initialize screen
    }

    public override void OnScreenKeyDown(KeyEventArgs e)
    {
        // Handle keyboard input
    }
}
```

## Layout Types

### GridLayoutEngine
```csharp
var layout = new GridLayoutEngine(rows: 2, columns: 3);
var params = new LayoutParams { Row = 0, Column = 1, RowSpan = 2 };
```

### DockLayoutEngine
```csharp
var layout = new DockLayoutEngine();
var params = new LayoutParams { Dock = Dock.Left, Width = 300 };
```

### StackLayoutEngine
```csharp
var layout = new StackLayoutEngine(Orientation.Vertical);
var params = new LayoutParams { Height = 100 };
```

## Services & Data Binding

### Register a Service

```csharp
// In your initialization code
var taskService = new TaskService();
ServiceContainer.Instance.Register(taskService);
```

### Use Service in Widget

```csharp
public override void Initialize()
{
    var taskService = ServiceContainer.Instance.Get<TaskService>();
    // Bind to service data
    this.DataContext = taskService;
}
```

### WPF Data Binding

```csharp
// In your widget's BuildUI()
textBlock.SetBinding(
    TextBlock.TextProperty,
    new Binding("PropertyName") { Source = this }
);
```

When the property changes, call `OnPropertyChanged("PropertyName")` and the UI updates automatically.

## Event Bus (Inter-Widget Communication)

### Subscribe to Event

```csharp
public override void Initialize()
{
    EventBus.Instance.Subscribe("TaskAdded", OnTaskAdded);
}

private void OnTaskAdded(object data)
{
    Refresh(); // Update widget
}
```

### Publish Event

```csharp
EventBus.Instance.Publish("TaskAdded", newTask);
```

## Terminal Aesthetic Colors

Standard color palette (from Windows Terminal):

```csharp
Background:     #0C0C0C  (black)
Foreground:     #CCCCCC  (light gray)
Border:         #3A3A3A  (dark gray)
Accent:         #4EC9B0  (cyan/teal)
Selection:      #264F78  (blue)

// Code highlighting colors
Green:          #6A9955
Blue:           #569CD6
Orange:         #CE9178
Red:            #F48771
```

## Next Steps

### Immediate Additions
1. **CalendarWidget** - Month view calendar
2. **TaskListScreen** - Interactive task list with CRUD
3. **ProjectListScreen** - Project management
4. **SettingsScreen** - Configuration UI

### Advanced Features
1. **Configuration Persistence** - Save/load workspace layouts to JSON
2. **Dynamic Widget Loading** - Load widgets from DLLs at runtime
3. **Mouse Support** - Drag to resize panels
4. **Split Panes** - Subdivide widget areas
5. **Widget Marketplace** - Share custom widgets

### PowerShell API (Future)

Vision for declarative workspace definition:

```powershell
Import-Module SuperTUI

New-Workspace "Dashboard" -Index 1 {
    Grid -Rows 2 -Columns 2 {
        Widget "Clock" -Position 0,0
        Widget "Calendar" -Position 0,1 -RowSpan 2
        Widget "TaskSummary" -Position 1,0
        Screen "TaskList" -Position 1,1
    }
}

Start-SuperTUI
```

## Architecture

### Core Components

1. **WidgetBase** - Base class for widgets (small, focused components)
2. **ScreenBase** - Base class for screens (larger, interactive panels)
3. **LayoutEngine** - Abstract layout system (Grid, Dock, Stack)
4. **Workspace** - Container for widgets/screens with layout
5. **WorkspaceManager** - Manages workspace switching
6. **ServiceContainer** - Simple dependency injection
7. **EventBus** - Pub/sub for inter-widget communication
8. **ShortcutManager** - Global and workspace-specific keyboard shortcuts

### Data Flow

```
User Input → ShortcutManager → Action
                              ↓
                         Workspace
                              ↓
                    Widget/Screen Handlers
                              ↓
                         Service Layer
                              ↓
                    ObservableCollection
                              ↓
                         WPF Binding
                              ↓
                         UI Updates
```

## Philosophy

1. **Terminal Aesthetic, GUI Power** - Looks like a TUI, performs like a GUI
2. **Keyboard-First** - Every action has a keyboard shortcut (NO VIM BINDINGS)
3. **Modular** - Widgets/screens are independent, reusable components
4. **Reactive** - Data binding eliminates manual UI updates
5. **Workspace-Oriented** - Multiple desktops for different contexts
6. **Extensible** - Framework doesn't need modification to add features

## Troubleshooting

### "Cannot find type [SuperTUI.Widgets.ClockWidget]"
- Make sure you're running on Windows (WPF is Windows-only)
- Check that the C# compilation succeeded (no errors in console)

### Window appears but is blank
- Check that workspaces were created successfully
- Verify `WorkspaceManager.SwitchToWorkspace(1)` was called

### Keyboard shortcuts don't work
- Make sure window has focus
- Check that shortcuts are registered before ShowDialog()
- Verify KeyDown event handler is attached to window

## License

Do whatever you want with this. It's your project.

## Credits

Built by Claude Code for teej's i3-like workspace vision.
