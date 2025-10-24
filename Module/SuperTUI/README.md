# SuperTUI PowerShell Module

Create beautiful terminal-style desktop UIs with workspaces, widgets, and layouts using fluent PowerShell syntax.

## Installation

```powershell
# Clone the repository
git clone https://github.com/yourusername/supertui.git
cd supertui

# Import the module
Import-Module .\Module\SuperTUI\SuperTUI.psd1
```

## Quick Start

```powershell
# Initialize SuperTUI
Initialize-SuperTUI

# Create a workspace with fluent API
$workspace = New-SuperTUIWorkspace "Dashboard" -Index 1 |
    Use-GridLayout -Rows 2 -Columns 2 -Splitters |
    Add-ClockWidget -Row 0 -Column 0 |
    Add-CounterWidget -Row 0 -Column 1 |
    Add-NotesWidget -Row 1 -Column 0 -ColumnSpan 2

# Build the workspace
$built = $workspace.Build()
```

## Features

- ✅ **Fluent API** - Chain commands with pipelines
- ✅ **Multiple Layouts** - Grid, Dock, and Stack layouts
- ✅ **Built-in Widgets** - Clock, Counter, Notes, TaskSummary
- ✅ **Resizable Panels** - Grid splitters for dynamic sizing
- ✅ **Theme Support** - Dark themes with customization
- ✅ **State Persistence** - Widget state survives workspace switches
- ✅ **Keyboard Shortcuts** - Ctrl+1-9 for workspace switching
- ✅ **Event System** - Widgets communicate via EventBus

## Core Commands

### Initialize-SuperTUI
Set up the SuperTUI environment and load the framework.

```powershell
# Use defaults (%LOCALAPPDATA%\SuperTUI)
Initialize-SuperTUI

# Custom paths
Initialize-SuperTUI -ConfigPath "C:\MyConfig\supertui.json" `
                    -ThemesPath "C:\MyThemes" `
                    -StatePath "C:\MyState"
```

### New-SuperTUIWorkspace
Create a new workspace builder. Alias: `New-Workspace`

```powershell
$workspace = New-SuperTUIWorkspace "MyWorkspace" -Index 1
```

**Parameters:**
- `Name` - Workspace display name
- `Index` - Number for Ctrl+N shortcut (1-9)

## Layout Commands

### Use-GridLayout
Configure a grid layout with rows and columns.

```powershell
$workspace | Use-GridLayout -Rows 2 -Columns 3 -Splitters
```

**Parameters:**
- `Rows` - Number of rows
- `Columns` (alias: `Cols`) - Number of columns
- `Splitters` - Enable resizable splitters

### Use-DockLayout
Configure a dock layout (Top/Bottom/Left/Right/Fill).

```powershell
$workspace | Use-DockLayout
```

### Use-StackLayout
Configure a stack layout (Vertical or Horizontal).

```powershell
$workspace | Use-StackLayout -Orientation Vertical
$workspace | Use-StackLayout -Orientation Horizontal
```

## Widget Commands

All widget commands support these common parameters:

**Grid Layout Parameters:**
- `Row` - Row position (0-based)
- `Column` - Column position (0-based)
- `RowSpan` - Span multiple rows
- `ColumnSpan` - Span multiple columns

**Dock Layout Parameters:**
- `Dock` - Dock position: Top, Bottom, Left, Right
- `Width` - Fixed width
- `Height` - Fixed height

### Add-ClockWidget
Add a real-time clock widget.

```powershell
# In grid
$workspace | Add-ClockWidget -Row 0 -Column 0

# In dock layout
$workspace | Add-ClockWidget -Dock Top -Height 150
```

### Add-CounterWidget
Add an interactive counter widget (Up/Down arrows to increment/decrement).

```powershell
$workspace | Add-CounterWidget -Name "My Counter" -Row 0 -Column 1
```

### Add-NotesWidget
Add a text notes widget.

```powershell
$workspace | Add-NotesWidget -Name "Notes" -Row 1 -Column 0 -ColumnSpan 2
```

### Add-TaskSummaryWidget
Add a task summary display widget.

```powershell
$workspace | Add-TaskSummaryWidget -Row 0 -Column 2
```

### Add-SystemMonitorWidget
Add a system monitor widget that displays real-time CPU, RAM, and Network statistics.

```powershell
# In grid
$workspace | Add-SystemMonitorWidget -Row 1 -Column 1

# In dock layout
$workspace | Add-SystemMonitorWidget -Dock Right -Width 300
```

**Features:**
- Real-time CPU usage percentage
- RAM usage (percentage and MB)
- Network activity (bytes sent/received per second)
- Color-coded progress bars (green < 75%, yellow < 90%, red >= 90%)
- Updates every second
- Publishes `SystemResourcesChangedEvent` for other widgets

### Add-GitStatusWidget
Add a Git repository status widget that displays branch, commit, and file information.

```powershell
# Monitor current directory
$workspace | Add-GitStatusWidget -Row 0 -Column 1

# Monitor specific repository
$workspace | Add-GitStatusWidget -RepositoryPath "C:\Projects\MyRepo" -Row 0 -Column 1

# In dock layout
$workspace | Add-GitStatusWidget -Dock Left -Width 300
```

**Features:**
- Current branch name
- Last commit (hash + message)
- Repository status (clean/changes/staged)
- File counts: modified, staged, untracked
- Color-coded status indicators
- Updates every 5 seconds
- Publishes `BranchChangedEvent` and `RepositoryStatusChangedEvent`

## Configuration Commands

### Get-SuperTUIConfig
Get a configuration value.

```powershell
# Get with default
$logLevel = Get-SuperTUIConfig -Key "Logging.MinLevel" -DefaultValue "Info"

# Get existing
$theme = Get-SuperTUIConfig -Key "UI.DefaultTheme"
```

### Set-SuperTUIConfig
Set a configuration value.

```powershell
Set-SuperTUIConfig -Key "Logging.MinLevel" -Value "Debug"
Set-SuperTUIConfig -Key "UI.DefaultTheme" -Value "Dark"
```

## Theme Commands

### Get-SuperTUITheme
Get the current theme or list available themes.

```powershell
# Get current theme
$currentTheme = Get-SuperTUITheme

# List available themes
$themes = Get-SuperTUITheme -ListAvailable
$themes | ForEach-Object { Write-Host $_.Name }
```

### Set-SuperTUITheme
Change the active theme.

```powershell
Set-SuperTUITheme -ThemeName "Dark"
Set-SuperTUITheme -ThemeName "Monokai"
```

## Utility Commands

### Get-SuperTUIStatistics
Get EventBus statistics.

```powershell
$stats = Get-SuperTUIStatistics

Write-Host "Events Published: $($stats.EventsPublished)"
Write-Host "Events Delivered: $($stats.EventsDelivered)"
Write-Host "Typed Subscribers: $($stats.TypedSubscribers)"
```

## Examples

### Example 1: Simple Dashboard

```powershell
Import-Module SuperTUI
Initialize-SuperTUI

$workspace = New-SuperTUIWorkspace "Dashboard" -Index 1 |
    Use-GridLayout -Rows 1 -Columns 2 |
    Add-ClockWidget -Row 0 -Column 0 |
    Add-NotesWidget -Row 0 -Column 1

$workspace.Build()
```

### Example 2: Complex Grid Layout

```powershell
$workspace = New-Workspace "Dev" -Index 1 |
    Use-GridLayout -Rows 3 -Cols 3 -Splitters |
    Add-ClockWidget -Row 0 -Column 0 |
    Add-TaskSummaryWidget -Row 0 -Column 1 -ColumnSpan 2 |
    Add-CounterWidget -Name "Builds" -Row 1 -Column 0 |
    Add-CounterWidget -Name "Tests" -Row 1 -Column 1 |
    Add-CounterWidget -Name "Errors" -Row 1 -Column 2 |
    Add-NotesWidget -Row 2 -Column 0 -ColumnSpan 3

$workspace.Build()
```

### Example 3: Dock Layout

```powershell
$workspace = New-Workspace "Dock Demo" -Index 1 |
    Use-DockLayout |
    Add-ClockWidget -Dock Top -Height 100 |
    Add-TaskSummaryWidget -Dock Left -Width 250 |
    Add-CounterWidget -Dock Right -Width 150 |
    Add-NotesWidget  # Fills remaining space

$workspace.Build()
```

### Example 4: Multiple Workspaces

```powershell
# Workspace 1: Dashboard
$ws1 = New-Workspace "Dashboard" -Index 1 |
    Use-GridLayout -Rows 2 -Cols 2 |
    Add-ClockWidget -Row 0 -Column 0 |
    Add-TaskSummaryWidget -Row 0 -Column 1 |
    Add-NotesWidget -Row 1 -Column 0 -ColumnSpan 2

# Workspace 2: Counters
$ws2 = New-Workspace "Counters" -Index 2 |
    Use-GridLayout -Rows 2 -Cols 2 -Splitters |
    Add-CounterWidget -Name "A" -Row 0 -Column 0 |
    Add-CounterWidget -Name "B" -Row 0 -Column 1 |
    Add-CounterWidget -Name "C" -Row 1 -Column 0 |
    Add-CounterWidget -Name "D" -Row 1 -Column 1

# Build both
$workspace1 = $ws1.Build()
$workspace2 = $ws2.Build()

# Switch with Ctrl+1 and Ctrl+2
```

### Example 5: System Monitor Workspace

```powershell
$workspace = New-Workspace "System" -Index 1 |
    Use-GridLayout -Rows 2 -Cols 2 -Splitters |
    Add-SystemMonitorWidget -Row 0 -Column 0 |
    Add-ClockWidget -Row 0 -Column 1 |
    Add-TaskSummaryWidget -Row 1 -Column 0 |
    Add-NotesWidget -Row 1 -Column 1

$workspace.Build()

# System Monitor displays real-time:
# - CPU usage with color-coded bar
# - RAM usage (percentage and MB)
# - Network activity (upload/download)
```

### Example 6: Developer Workspace

```powershell
$workspace = New-Workspace "Development" -Index 1 |
    Use-GridLayout -Rows 2 -Cols 3 -Splitters |
    Add-GitStatusWidget -Row 0 -Column 0 |
    Add-SystemMonitorWidget -Row 0 -Column 1 |
    Add-ClockWidget -Row 0 -Column 2 |
    Add-NotesWidget -Row 1 -Column 0 -ColumnSpan 3

$workspace.Build()

# Perfect for developers:
# - Git status shows branch and file changes
# - System monitor shows resource usage
# - Clock keeps time visible
# - Notes for quick reminders
```

### Example 7: Theme Customization

```powershell
Initialize-SuperTUI

# List available themes
Get-SuperTUITheme -ListAvailable | Format-Table Name, Author

# Set theme
Set-SuperTUITheme -ThemeName "Dracula"

# Get current theme details
$theme = Get-SuperTUITheme
Write-Host "Current theme: $($theme.Name)"
Write-Host "Background: $($theme.Background)"
```

## Pipeline Chaining

The fluent API uses PowerShell pipelines for clean, readable code:

```powershell
# Each command returns the workspace builder
$workspace = New-Workspace "MyApp" |           # Returns WorkspaceBuilder
    Use-GridLayout -Rows 2 -Cols 2 |           # Returns WorkspaceBuilder
    Add-ClockWidget -Row 0 -Column 0 |         # Returns WorkspaceBuilder
    Add-CounterWidget -Row 0 -Column 1 |       # Returns WorkspaceBuilder
    Add-NotesWidget -Row 1 -Column 0           # Returns WorkspaceBuilder

# Finally, build to get the actual workspace object
$built = $workspace.Build()                    # Returns Workspace
```

## Keyboard Shortcuts

When displayed in a window:

| Shortcut | Action |
|----------|--------|
| `Ctrl+1` to `Ctrl+9` | Switch to workspace 1-9 |
| `Ctrl+Left` / `Ctrl+Right` | Previous/Next workspace |
| `Tab` / `Shift+Tab` | Switch focus between widgets |
| `Ctrl+Q` | Quit |

Widget-specific:
- **Counter:** Up/Down arrows to increment/decrement, R to reset
- **Notes:** Type freely, text persists

## Advanced Usage

### EventBus Integration

```powershell
# Subscribe to events
$eventBus = [SuperTUI.Core.EventBus]::Instance

$handler = {
    param($evt)
    Write-Host "Theme changed to: $($evt.NewThemeName)" -ForegroundColor Cyan
}

$eventBus.Subscribe([SuperTUI.Core.Events.ThemeChangedEvent], $handler)

# Publish events
$evt = New-Object SuperTUI.Core.Events.ThemeChangedEvent
$evt.NewThemeName = "Dark"
$evt.OldThemeName = "Light"
$eventBus.Publish($evt)
```

### Dependency Injection

```powershell
# Access the DI container
$container = [SuperTUI.Core.ServiceContainer]::Instance

# Resolve services
$logger = $container.Resolve([SuperTUI.Infrastructure.ILogger])
$themeManager = $container.Resolve([SuperTUI.Infrastructure.IThemeManager])

# Use services
$logger.Info("PowerShell", "Hello from PowerShell!")
```

### State Persistence

Widget state (counter values, notes text, etc.) is automatically persisted:

```powershell
# State is saved when switching workspaces
# State is loaded when returning to a workspace
# No manual save/load needed!
```

## Troubleshooting

### Module Not Found
```powershell
# Ensure you're in the correct directory
Import-Module .\Module\SuperTUI\SuperTUI.psd1 -Force
```

### Framework Compilation Errors
```powershell
# The module automatically compiles C# sources
# Ensure all source files exist in WPF/Core and WPF/Widgets
```

### WPF Not Available
```powershell
# SuperTUI requires Windows with WPF support
# Check PowerShell version
$PSVersionTable.PSVersion  # Should be 5.1 or higher
```

## Module Structure

```
Module/SuperTUI/
├── SuperTUI.psd1       # Module manifest
├── SuperTUI.psm1       # Module implementation
└── README.md           # This file
```

## Related Documentation

- `DEPENDENCY_INJECTION.md` - DI container usage
- `EVENTBUS.md` - Event system documentation
- `WPF/FEATURES.md` - Complete feature list
- `Examples/FluentAPI_Demo.ps1` - Runnable example

## Requirements

- Windows with WPF support
- PowerShell 5.1 or higher
- .NET Framework 4.7.2 or higher

## License

MIT License - See LICENSE file for details

## Contributing

Contributions welcome! Please see CONTRIBUTING.md

## Future Widgets

Coming soon:
- `Add-TerminalWidget` - Embedded PowerShell terminal
- `Add-TodoWidget` - Task management
- `Add-FileExplorerWidget` - Directory navigation
- `Add-CommandPaletteWidget` - Ctrl+P fuzzy search

---

**Made with ❤️ for PowerShell users who love terminal UIs**
