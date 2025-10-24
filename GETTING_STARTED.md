# Getting Started with SuperTUI Development

## Project Setup Complete! ✅

The SuperTUI project is fully configured and ready for implementation.

## What We Have

### Documentation
- ✅ **DESIGN_DIRECTIVE.md** - Complete architectural specification (32KB)
- ✅ **README.md** - Project overview and quick start
- ✅ **TASKS.md** - Detailed task breakdown for all phases
- ✅ **PROJECT_STATUS.md** - Current status and progress tracking

### Project Structure
```
supertui/
├── .claude/                    # Claude Code configuration
│   ├── CLAUDE.md              # Project memory and decisions
│   ├── WORKFLOW.md            # Development workflow guide
│   ├── QUICK_REF.md           # Quick reference
│   └── commands/              # Custom slash commands
│       ├── implement.md       # /implement command
│       ├── status.md          # /status command
│       └── test.md            # /test command
├── Core/                      # C# engine (to be implemented)
├── Module/                    # PowerShell API (to be implemented)
│   └── Builders/             # Component builders
├── App/                      # Application layer
│   ├── Screens/              # Screen implementations
│   │   ├── Tasks/
│   │   ├── Projects/
│   │   ├── Views/
│   │   ├── Tools/
│   │   ├── Time/
│   │   ├── Dependencies/
│   │   ├── Focus/
│   │   ├── Backup/
│   │   └── System/
│   └── Services/             # Business logic
├── Tests/                    # Unit and integration tests
│   ├── Unit/
│   └── Integration/
├── Examples/                 # Example screens and usage
├── .gitignore               # Git ignore patterns
└── README.md                # Project documentation
```

### Tooling & Configuration
- ✅ Custom slash commands for workflow
- ✅ Project memory system (.claude/CLAUDE.md)
- ✅ Task tracking system (TASKS.md)
- ✅ Development workflow documentation
- ✅ Quick reference guide
- ✅ Git configuration (.gitignore)

## What's Next

### Immediate Next Step: Implement C# Core Engine

**File to create:** `Core/SuperTUI.Core.cs`

**What it needs:**
1. Base classes (UIElement, Screen, Component)
2. Layout system (GridLayout, StackLayout, DockLayout)
3. Components (Label, Button, TextBox, DataGrid, ListView)
4. Navigation (ScreenManager, Router)
5. Rendering (Terminal, VT100, RenderContext)
6. Events (EventBus)
7. Services (ServiceContainer)
8. Theme system

**Estimated size:** ~1500 lines of C#

**Reference implementations:**
- `/home/teej/_tui/praxis-main/` - Advanced patterns
- `/home/teej/_tui/alcar/` - Clean architecture
- `/home/teej/pmc/ConsoleUI.Core.ps1` - VT100 rendering

## For Claude Code

When you're ready to start implementation:

1. **Review the design:**
   ```bash
   cat DESIGN_DIRECTIVE.md
   ```

2. **Check current status:**
   ```bash
   cat PROJECT_STATUS.md
   cat TASKS.md
   ```

3. **Load memory:**
   ```bash
   cat .claude/CLAUDE.md
   ```

4. **Use slash commands:**
   - `/implement` - Get implementation guidelines
   - `/status` - Check project status
   - `/test` - Run tests

5. **Start with core engine:**
   Begin implementing `Core/SuperTUI.Core.cs` following the design directive.

## Key Architectural Principles

Remember the **Three Rules:**
1. **Infrastructure in C#, Logic in PowerShell**
2. **Declarative over Imperative**
3. **Convention over Configuration**

## Success Metrics

We're aiming for:
- ✅ 70-80% code reduction vs traditional PowerShell TUI
- ✅ New screen in <50 lines
- ✅ Zero manual positioning (declarative layouts)
- ✅ Zero manual data refresh (auto-binding)
- ✅ <2 second compile time
- ✅ 30+ FPS rendering

## Design Highlights

### Layer 1: C# Core (Infrastructure)
- High-performance layout calculations
- VT100 rendering engine
- Event-driven architecture with C# events
- ObservableCollection for auto-binding
- Component library with data binding support

### Layer 2: PowerShell API (Helpers)
- Fluent builders (New-GridLayout, New-DataGrid, etc.)
- Navigation helpers (Push-Screen, Pop-Screen)
- Service helpers (Register-Service, Get-Service)
- Screen templates

### Layer 3: Application (Business Logic)
- Screen implementations (58+ screens to port)
- Services (TaskService, ProjectService, etc.)
- Business rules and workflows

## Example: Simple Screen

Here's what a screen will look like when done:

```powershell
class TaskListScreen : SuperTUI.Screen {
    [object]$_grid

    TaskListScreen() {
        $this.Title = "Tasks"

        # Get service (dependency injection)
        $taskService = Get-Service "TaskService"

        # Create layout (declarative, no manual positioning!)
        $layout = New-GridLayout -Rows "Auto","*","Auto" -Columns "*"

        # Add header
        $header = New-Label -Text "My Tasks" -Style "Header"
        $layout.AddChild($header, 0, 0)

        # Add grid with auto-binding (updates automatically!)
        $this._grid = New-DataGrid `
            -ItemsSource $taskService.Tasks `
            -Columns @(
                @{ Header = "Title"; Property = "Title"; Width = "*" }
                @{ Header = "Due"; Property = "DueDate"; Width = 12 }
            )
        $layout.AddChild($this._grid, 1, 0)

        # Add footer
        $footer = New-Label -Text "N:New  E:Edit  Esc:Back" -Style "Footer"
        $layout.AddChild($footer, 2, 0)

        $this.Children.Add($layout)

        # Register keys (declarative!)
        $this.RegisterKey("N", { Push-Screen (New-TaskFormScreen) })
        $this.RegisterKey("E", { $this.EditTask() })
        $this.RegisterKey("Escape", { Pop-Screen })
    }

    [void] EditTask() {
        if ($this._grid.SelectedItem) {
            Push-Screen (New-TaskFormScreen -TaskId $this._grid.SelectedItem.Id)
        }
    }
}
```

**Compare to current implementation:** ~200+ lines → ~50 lines (75% reduction!)

## Resources

**Project Documentation:**
- Design: `DESIGN_DIRECTIVE.md`
- Tasks: `TASKS.md`
- Status: `PROJECT_STATUS.md`
- Workflow: `.claude/WORKFLOW.md`
- Quick Ref: `.claude/QUICK_REF.md`

**Reference Code:**
- TUI implementations: `/home/teej/_tui/`
- Current ConsoleUI: `/home/teej/pmc/ConsoleUI.Core.ps1`

## Questions?

Add questions to `.claude/CLAUDE.md` under "Open Questions" section.

## Ready to Code!

The project is fully set up. Start with Phase 1: Core Engine implementation.

Good luck! 🚀
