# SuperTUI Quick Reference

## Project Location
`/home/teej/supertui/WPF`

## Key Files
- `.claude/CLAUDE.md` - **AUTHORITATIVE** project documentation
- `PROJECT_STATUS.md` - Current status and metrics
- `SECURITY.md` - Security model
- `PLUGIN_GUIDE.md` - Plugin development
- `MainWindow.xaml.cs` - Application entry point
- `Core/Infrastructure/PaneFactory.cs` - Pane creation and DI

## Architecture

**WPF Pane-Based System:**
- **Panes**: Main UI components (TaskListPane, NotesPane, FileBrowserPane, CommandPalettePane)
- **Workspaces**: Multiple desktops with state preservation (Ctrl+1-9)
- **TilingLayoutEngine**: Automatic pane arrangement (5 modes)
- **Services**: Infrastructure (Logger, Config, Theme, Security) + Domain (Task, Project, TimeTracking, Tag)

## Core Components

```
PaneBase (abstract)
├── TaskListPane - Task management with filtering, sorting, subtasks
├── NotesPane - Note editor with auto-save and fuzzy search
├── FileBrowserPane - Secure file browser
└── CommandPalettePane - Command palette (Shift+:)

StatusBarWidget - Legacy widget showing time/tasks/project
```

## Dependency Injection Pattern

```csharp
public class MyPane : PaneBase
{
    private readonly ILogger logger;
    private readonly ITaskService taskService;

    public MyPane(
        ILogger logger,
        IThemeManager themeManager,
        IConfigurationManager config,
        ITaskService taskService,
        IProjectContext projectContext)
        : base(logger, themeManager, config, projectContext)
    {
        this.logger = logger;
        this.taskService = taskService;
        PaneName = "MyPane";
    }

    protected override void OnDispose()
    {
        taskService.TaskAdded -= OnTaskAdded;
        base.OnDispose();
    }
}
```

## Global Shortcuts

| Shortcut | Action |
|----------|--------|
| **Shift+:** | Command palette |
| **Ctrl+1-9** | Switch workspace |
| **Ctrl+Shift+←↑↓→** | Navigate panes |
| **Ctrl+Shift+T** | Open tasks pane |
| **Ctrl+Shift+N** | Open notes pane |
| **Ctrl+Shift+F** | Open files pane |
| **Ctrl+Shift+Q** | Close focused pane |
| **F12** | Toggle move pane mode |

## Pane-Specific Shortcuts

**TaskListPane:**
- A: Add task
- E / Enter: Edit task
- D: Delete task
- C: Toggle complete
- Tab: Indent task
- Ctrl+:: Command mode

**FileBrowserPane:**
- Enter: Select
- Backspace: Parent directory
- ~: Home directory
- /: Jump to path
- Ctrl+H: Toggle hidden files
- Ctrl+F: Focus search

**NotesPane:**
- Ctrl+N: New note
- Ctrl+S: Save note
- Ctrl+F: Search notes
- Delete: Delete note
- F2: Rename note

## Services

**Infrastructure Services:**
```csharp
ILogger - Dual-queue async logging
IConfigurationManager - Type-safe config
IThemeManager - Hot-reload themes
ISecurityManager - Path validation
IErrorHandler - Retry logic
IEventBus - Pub/sub events
IShortcutManager - Key bindings (unused - shortcuts hardcoded)
```

**Domain Services:**
```csharp
ITaskService - Task CRUD + events
IProjectService - Project management
ITimeTrackingService - Time tracking
ITagService - Tag management
```

## Building & Running

```bash
cd /home/teej/supertui/WPF
dotnet build SuperTUI.csproj
```

## Testing

```bash
cd /home/teej/supertui/WPF
dotnet test  # Requires Windows
```

## Known Limitations

- **Windows-only** (WPF requirement)
- **Tests not run** (require Windows, excluded from Linux build)
- **ShortcutManager unused** (shortcuts hardcoded in event handlers)
- **StatePersistence disabled** (CaptureState/RestoreState commented out during migration)
- **No resizable splitters** (TilingLayoutEngine has no GridSplitter)
- **EventBus memory leak risk** (strong references by default, widgets don't unsubscribe)

## Development Guidelines

1. **All new panes** inherit from PaneBase
2. **Use DI constructors** with interface parameters
3. **Implement OnDispose()** for cleanup
4. **Use ErrorHandlingPolicy** for all errors
5. **Never hardcode colors** - use ThemeManager
6. **Always use interfaces** (ILogger, ITaskService, etc.)
7. **Implement SaveState/RestoreState** for workspace persistence

## Commands

- `/implement` - Implementation guidelines
- `/status` - Project status check
- `/test` - Run tests

## Tools

- **TodoWrite** - Track tasks during work
- **Task tool** - Launch sub-agents for complex analysis
- **Explore agent** - Quick codebase searches

## File Structure

```
/home/teej/supertui/WPF/
├── Core/
│   ├── Infrastructure/    - Services (Logger, Config, PaneFactory, etc.)
│   ├── DI/               - ServiceContainer, ServiceRegistration
│   ├── Components/       - PaneBase, WidgetBase, ErrorBoundary
│   ├── Layout/           - TilingLayoutEngine
│   ├── Services/         - TaskService, ProjectService, etc.
│   ├── Models/           - TaskModels, ProjectModels, etc.
│   └── Interfaces/       - Service interfaces
├── Panes/                - 4 production panes
├── Widgets/              - StatusBarWidget (legacy)
└── MainWindow.xaml.cs    - Entry point
```

## Remember

- **CLAUDE.md is the source of truth** - always check there first
- Update CLAUDE.md when making architectural decisions
- Use sub-agents for complex codebase analysis
- Test on Windows before claiming "tests pass"
- Document known gaps honestly
