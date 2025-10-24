# SuperTUI - Quick Start Guide

## Run on Windows

```powershell
cd C:\path\to\supertui\WPF
.\SuperTUI.ps1
```

## Keyboard Shortcuts

### Global
- `Ctrl+1` to `Ctrl+9` - Switch workspaces
- `Ctrl+Left/Right` - Previous/Next workspace
- `Ctrl+Q` - Quit

### Navigation (within widgets/screens)
- `Arrow Keys` - Navigate up/down/left/right
- `Tab` - Next widget/field
- `Shift+Tab` - Previous widget/field
- `Enter` - Activate/Select
- `Escape` - Cancel/Close

## File Structure

```
WPF/
├── SuperTUI.ps1           ← RUN THIS
├── Core/Framework.cs      ← Core framework (don't modify)
├── Widgets/               ← Add your widgets here
│   ├── ClockWidget.cs
│   └── TaskSummaryWidget.cs
└── Screens/               ← Add your screens here
```

## Add a Widget - 3 Steps

### 1. Create `Widgets/YourWidget.cs`

```csharp
using SuperTUI.Core;

namespace SuperTUI.Widgets
{
    public class YourWidget : WidgetBase
    {
        public YourWidget() { WidgetType = "YourWidget"; BuildUI(); }
        private void BuildUI() { /* create UI */ }
        public override void Initialize() { /* init data */ }
    }
}
```

### 2. Add to compilation in `SuperTUI.ps1`

```powershell
$yourWidgetSource = Get-Content "$PSScriptRoot/Widgets/YourWidget.cs" -Raw

$combinedSource = @"
$frameworkSource
$yourWidgetSource
...
"@
```

### 3. Add to workspace in `SuperTUI.ps1`

```powershell
$widget = New-Object SuperTUI.Widgets.YourWidget
$widget.Initialize()

$params = New-Object SuperTUI.Core.LayoutParams
$params.Row = 0
$params.Column = 0

$workspace1.AddWidget($widget, $params)
```

## Terminal Colors

```csharp
Background:  #0C0C0C
Foreground:  #CCCCCC
Border:      #3A3A3A
Accent:      #4EC9B0
Blue:        #569CD6
Green:       #6A9955
Orange:      #CE9178
Red:         #F48771
```

## Layouts

### Grid
```csharp
new GridLayoutEngine(rows: 2, columns: 3)
new LayoutParams { Row = 0, Column = 1 }
```

### Dock
```csharp
new DockLayoutEngine()
new LayoutParams { Dock = Dock.Left, Width = 300 }
```

### Stack
```csharp
new StackLayoutEngine(Orientation.Vertical)
new LayoutParams { Height = 100 }
```

## Data Binding

```csharp
// In widget
private string myData;
public string MyData
{
    get => myData;
    set { myData = value; OnPropertyChanged(nameof(MyData)); }
}

// In UI
textBlock.SetBinding(TextBlock.TextProperty, new Binding("MyData") { Source = this });

// Change data - UI updates automatically
MyData = "New value";
```

## Services

```csharp
// Register
var service = new MyService();
ServiceContainer.Instance.Register(service);

// Use
var service = ServiceContainer.Instance.Get<MyService>();
```

## Events

```csharp
// Subscribe
EventBus.Instance.Subscribe("EventName", data => { /* handler */ });

// Publish
EventBus.Instance.Publish("EventName", data);
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Window won't launch | Run on Windows (WPF is Windows-only) |
| Compilation errors | Check C# syntax in widget files |
| Widget not showing | Verify added to workspace AND compiled |
| Shortcuts don't work | Window must have focus |

## Next Steps

1. Run the demo - see Clock and TaskSummary widgets
2. Try switching workspaces (Ctrl+1, Ctrl+2, Ctrl+3)
3. Create your first widget (copy ClockWidget.cs as template)
4. Build your own workspace layout

## Need Help?

- Read: `WPF_ARCHITECTURE.md` - Full design document
- Read: `README.md` - Detailed documentation
- Look at: `ClockWidget.cs` - Simple widget example
- Look at: `TaskSummaryWidget.cs` - Data-driven widget example
