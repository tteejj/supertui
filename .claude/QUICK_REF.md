# Quick Reference

## Project Location
`/home/teej/supertui`

## Key Files
- `DESIGN_DIRECTIVE.md` - Architecture spec
- `TASKS.md` - Task list
- `PROJECT_STATUS.md` - Current status
- `.claude/CLAUDE.md` - Memory/decisions
- `.claude/WORKFLOW.md` - Development guide

## Architecture

**Three Layers:**
1. C# Core (`Core/SuperTUI.Core.cs`) - Infrastructure
2. PowerShell API (`Module/SuperTUI.psm1`) - Builders
3. Application (`App/`) - Screens & services

## Three Rules
1. Infrastructure in C#, Logic in PowerShell
2. Declarative over Imperative
3. Convention over Configuration

## Success Metrics
- 70-80% code reduction
- New screen <50 lines
- Zero manual positioning
- Zero manual refresh
- <2s compile time
- 30+ FPS rendering

## Current Phase
**Phase 1: Core Engine** - Ready to start

## Next Task
Implement SuperTUI.Core.cs skeleton

## References
- `/home/teej/_tui/praxis-main/` - Patterns
- `/home/teej/_tui/alcar/` - Architecture
- `/home/teej/_tui/_R2/` - Services
- `/home/teej/pmc/ConsoleUI.Core.ps1` - VT100

## Commands
- `/implement` - Guidelines
- `/status` - Status check
- `/test` - Run tests

## Tools
- TodoWrite - Track tasks
- Task tool - Sub-agents
- Explore agent - Codebase analysis

## Core Classes (C#)
```
UIElement (base)
├── Component
│   ├── Label, Button, TextBox
│   ├── DataGrid, ListView, TreeView
│   └── Panel
└── Screen
    ├── ContentScreen
    └── DialogScreen

Layout
├── GridLayout
├── StackLayout
└── DockLayout

ScreenManager (singleton)
EventBus (singleton)
ServiceContainer
Terminal
Theme
```

## Typical Screen (PowerShell)
```powershell
class MyScreen : SuperTUI.Screen {
    MyScreen() {
        $this.Title = "My Screen"
        $layout = New-GridLayout -Rows "*" -Columns "*"
        # Add components...
        $this.Children.Add($layout)
        $this.RegisterKey("Escape", { Pop-Screen })
    }
}
```

## Layout Patterns
```powershell
# Grid
New-GridLayout -Rows "Auto","*","40" -Columns "200","*","200"

# Stack
New-StackLayout -Orientation Vertical -Spacing 1

# Dock
New-DockLayout
```

## Component Patterns
```powershell
# Label
New-Label -Text "Hello" -Style "Header"

# Button
New-Button -Label "Click" -OnClick { }

# DataGrid (auto-binding!)
New-DataGrid -ItemsSource $service.Items -Columns @(...)

# TextBox
New-TextBox -Label "Name:" -Value "" -Required $true
```

## Navigation
```powershell
Push-Screen $screen    # Go to screen
Pop-Screen             # Go back
Navigate-To "/path"    # Route navigation
```

## Services
```powershell
Register-Service "TaskService" { [TaskService]::new() }
$svc = Get-Service "TaskService"
```

## Events
```powershell
[EventBus]::Instance.add_TaskCreated({ ... })
[EventBus]::Instance.PublishTaskCreated($data)
```

## Testing
```powershell
Invoke-Pester ./Tests/
```

## Remember
- Use sub-agents for complex tasks
- Update .claude/CLAUDE.md with decisions
- Keep PROJECT_STATUS.md current
- Reference existing TUI code
- Test as you go
