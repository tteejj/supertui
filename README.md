# SuperTUI

A workspace/widget-based desktop framework with terminal aesthetics. Think **i3 window manager meets desktop widgets**, keyboard-driven, modular, and reactive.

Built with WPF + PowerShell for Windows.

## Features

- ✅ **Multiple Workspaces** - Switch between desktops like i3 (Ctrl+1-9)
- ✅ **Widget System** - Modular, self-contained components (Clock, TaskSummary, Notes, etc.)
- ✅ **Independent State** - Each widget maintains its own state, preserved across workspace switches
- ✅ **Flexible Layouts** - Grid, Dock, Stack layouts with resizable panels
- ✅ **Terminal Aesthetic** - Dark theme, monospace fonts, minimal borders
- ✅ **Keyboard-First** - Arrow keys, Tab, Enter, Escape navigation
- ✅ **Focus Management** - Visual indicators and Tab-based focus switching
- ✅ **Reactive** - WPF data binding, zero manual UI refresh calls

## Quick Start

**Requirements:** Windows with PowerShell

```powershell
cd WPF
.\SuperTUI_Demo.ps1
```

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+1` to `Ctrl+9` | Switch to workspace 1-9 |
| `Ctrl+Left` / `Ctrl+Right` | Previous/Next workspace |
| `Tab` / `Shift+Tab` | Switch focus between widgets |
| `Ctrl+Q` | Quit |

## Project Structure

```
supertui/
├── WPF/                       # Main WPF framework
│   ├── Core/
│   │   └── Framework.cs       # Core framework (999 lines)
│   ├── Widgets/
│   │   ├── ClockWidget.cs
│   │   ├── TaskSummaryWidget.cs
│   │   ├── CounterWidget.cs
│   │   └── NotesWidget.cs
│   ├── SuperTUI_Demo.ps1      # Enhanced demo
│   ├── WpfSpike.ps1           # Original proof-of-concept
│   ├── README.md              # Full documentation
│   ├── QUICKSTART.md          # Quick reference
│   ├── FEATURES.md            # Feature documentation
│   └── WPF_ARCHITECTURE.md    # Design document
├── WPF_COMPLETE.md            # Complete implementation summary
└── WPF_ARCHITECTURE.md        # Architecture overview
```

## Documentation

- **[QUICKSTART.md](WPF/QUICKSTART.md)** - Quick reference guide
- **[FEATURES.md](WPF/FEATURES.md)** - Complete feature list
- **[WPF_ARCHITECTURE.md](WPF/WPF_ARCHITECTURE.md)** - Design documentation
- **[WPF_COMPLETE.md](WPF_COMPLETE.md)** - Implementation summary

## Creating Custom Widgets

```csharp
using SuperTUI.Core;

public class MyWidget : WidgetBase
{
    public MyWidget()
    {
        WidgetType = "MyWidget";
        BuildUI();
    }

    private void BuildUI()
    {
        // Create your widget UI
    }

    public override void Initialize()
    {
        // Initialize widget data
    }

    public override void OnWidgetKeyDown(KeyEventArgs e)
    {
        // Handle keyboard input
    }
}
```

## Demo Workspaces

### Workspace 1: Dashboard
- 2x3 grid with resizable splitters
- Clock, Task Summary, Notes, and 2 Counters
- Demonstrates state preservation

### Workspace 2: Focus Test
- 2x2 grid with 4 counter widgets
- Test Tab focus switching
- Each counter has independent state

### Workspace 3: Mixed Layout
- Dock layout (Top + Left + Fill)
- Clock (top), Task Summary (left), Notes (fill)
- Demonstrates flexible sizing

## Architecture

- **WidgetBase** - Base class for widgets
- **ScreenBase** - Base class for larger interactive panels
- **Workspace** - Container for widgets with layout
- **WorkspaceManager** - Handles workspace switching
- **LayoutEngine** - Grid/Dock/Stack layout engines
- **ServiceContainer** - Dependency injection
- **EventBus** - Inter-widget communication

## License

MIT - Do whatever you want with this.

## Credits

Built as a proof-of-concept for an i3-style workspace system in WPF.
