# SuperTUI Architecture - Quick Reference

## Current System (2025-10-29)

### What is SuperTUI Now?
WPF application with a **pane-based UI** styled like a terminal. It's NOT a text-based TUI - it's a Windows desktop app that LOOKS like a terminal.

### Visual Layout
```
┌─ MainWindow (1400x900) ─────────────────────────────┐
│  RootContainer (Grid)                               │
│  ├─ PaneCanvas [Row 0: *]                           │
│  │  └─ TilingLayoutEngine (auto-tiles panes)        │
│  │     ├─ TaskListPane     (200px)                  │
│  │     │  ┌──────────────────┐                      │
│  │     │  │ Tasks   [Ctrl+T] │                      │
│  │     │  ├──────────────────┤                      │
│  │     │  │ Task 1           │ Focused (green)      │
│  │     │  │ Task 2           │                      │
│  │     │  └──────────────────┘                      │
│  │     │                                             │
│  │     ├─ NotesPane        (200px)                  │
│  │     │  ┌──────────────────┐                      │
│  │     │  │ Notes   [Ctrl+N] │                      │
│  │     │  ├──────────────────┤                      │
│  │     │  │ Note 1           │ Unfocused (gray)     │
│  │     │  │ Note 2           │                      │
│  │     │  └──────────────────┘                      │
│  │     │                                             │
│  │     └─ ProcessingPane  (remaining space)         │
│  │        ┌─────────────────────────┐               │
│  │        │ Processing [Ctrl+P]     │               │
│  │        ├─────────────────────────┤               │
│  │        │ Item 1                  │ Unfocused      │
│  │        └─────────────────────────┘               │
│  │                                                   │
│  └─ StatusBarContainer [Row 1: 30px] ────────────┐  │
│     StatusBarWidget: "Tasks + Notes + Processing"   │
│     ┌─────────────────────────────────────────────┐ │
│     │ Active: Tasks | Project: None | Time: 2:30p  │ │
│     └─────────────────────────────────────────────┘ │
│                                                     │
│  CommandPaletteOverlay (when visible):              │
│  ┌──────────────────────────────────────────────┐   │
│  │ > tasks                              [↓] Cmd │   │
│  │ ├─ tasks      Open tasks pane        [Ctrl+T]│   │
│  │ ├─ notes      Open notes pane        [Ctrl+N]│   │
│  │ ├─ processing Open processing...             │   │
│  │ └─ close      Close focused pane     [Ctrl+Q]│   │
│  └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

---

## Key Systems

### 1. PaneManager (Auto-Tiling)
Manages open panes with i3-style tiling layout.

**Open a pane:**
```
User: Ctrl+T
  ↓
MainWindow.OpenPane("tasks")
  ↓
PaneManager.OpenPane(TaskListPane)
  ↓
TilingLayoutEngine.AddChild() → pane auto-positioned
  ↓
Pane visible and interactive
```

**Close a pane:**
```
User: Ctrl+Shift+Q
  ↓
PaneManager.CloseFocusedPane()
  ↓
TilingLayoutEngine.RemoveChild() → remaining panes reflowed
  ↓
Canvas updated
```

**Navigate between panes:**
```
User: Alt+Left
  ↓
MainWindow_KeyDown detects Alt+Left
  ↓
PaneManager.NavigateFocus(FocusDirection.Left)
  ↓
TilingLayoutEngine.FindWidgetInDirection()
  ↓
PaneManager.FocusPane(targetPane)
  ↓
targetPane.SetActive(true) → green border
```

---

### 2. Workspace System (Alt+1-9)
9 independent workspaces, each with its own pane layout.

**Switch workspace:**
```
User: Alt+2
  ↓
MainWindow detects Alt+2
  ↓
workspaceManager.SwitchToWorkspace(1)
  ↓
WorkspaceChanged event fires
  ↓
MainWindow.OnWorkspaceChanged:
  ├─ SaveCurrentWorkspaceState()
  │  └─ paneManager.GetState() → ["tasks", "notes"]
  └─ RestoreWorkspaceState()
     ├─ paneManager.CloseAll()
     ├─ For each saved pane type:
     │  ├─ paneFactory.CreatePane(type)
     │  └─ paneManager.OpenPane(pane)
     └─ Restore focus
  ↓
New workspace displayed with saved layout
```

**Persistence:**
- Location: `%LOCALAPPDATA%\SuperTUI\workspaces.json`
- Contains: pane list, focused pane, project context for each workspace
- Loaded on app startup, saved on app close

---

### 3. Command Palette (: or Ctrl+Space)
Fuzzy-searchable command overlay.

**Command types:**
| Category | Commands | Effect |
|----------|----------|--------|
| **Pane** | tasks, notes, processing, close | OpenPane() or CloseFocusedPane() |
| **Project** | project {name}, project clear | SetProject() for filtering |
| **Workspace** | workspace 1-9 | SwitchToWorkspace() |
| **Task** | create task, filter active, etc. | Future implementation |
| **System** | help, settings, theme | Future implementation |

**Example flow:**
```
User: `:notes<Enter>`
  ↓
CommandPalette fuzzy matches "notes"
  ↓
CommandExecuted event: Command(name="notes", category=Pane)
  ↓
MainWindow.OnCommandExecuted()
  ├─ Check category == Pane
  ├─ Call OpenPane("notes")
  └─ NotesPane created and displayed
```

---

### 4. Project Context (Global Filter)
Panes can filter their content to a specific project.

**Set project context:**
```
User: `:project tasks<Enter>`
  ↓
CommandPalette.OnCommandExecuted()
  ↓
MainWindow.OnCommandExecuted()
  ├─ Find project named "tasks"
  └─ projectContext.SetProject(project)
    ↓
    ProjectContextChanged event fires
    ↓
    All open panes receive notification
    ├─ pane.OnProjectContextChanged(e)
    └─ Each pane filters to project
      ↓
      All panes now show only "tasks" project items
```

**Save with workspace:**
When switching workspaces, the project context is also saved/restored.

---

## Keyboard Shortcuts

### Pane Management
| Shortcut | Action |
|----------|--------|
| `Alt+Left/Right/Up/Down` | Navigate focus between panes |
| `Alt+Shift+Arrows` | Move (swap) focused pane |
| `Ctrl+Shift+Q` | Close focused pane |

### Quick Pane Open
| Shortcut | Action |
|----------|--------|
| `Ctrl+T` | Open Tasks pane |
| `Ctrl+N` | Open Notes pane |
| `Ctrl+P` | Open Processing pane |

### Workspace
| Shortcut | Action |
|----------|--------|
| `Alt+1` to `Alt+9` | Switch to workspace 1-9 |

### Command Palette
| Shortcut | Action |
|----------|--------|
| `:` (Shift+;) | Open command palette |
| `Ctrl+Space` | Open command palette |
| `Down` | Next command |
| `Up` | Previous command |
| `Tab` | Autocomplete |
| `Enter` | Execute command |
| `Esc` | Cancel |

---

## Component Map

### UI Hierarchy
```
MainWindow
├─ ServiceContainer (DI)
├─ PaneWorkspaceManager (manages 9 workspaces)
├─ PaneManager (auto-tiling engine)
│  └─ TilingLayoutEngine
│     ├─ TaskListPane
│     ├─ NotesPane
│     ├─ ProcessingPane
│     └─ ...
├─ PaneFactory (creates panes by name)
├─ StatusBarWidget (bottom status bar)
├─ CommandPaletteOverlay (fuzzy search)
├─ OverlayManager (shows/hides overlays)
└─ ProjectContextManager (global project filter)
```

### File Locations
```
/WPF/
├─ MainWindow.xaml               - UI layout (simple 2-row grid)
├─ MainWindow.xaml.cs            - Orchestrator (init, event handlers)
├─ Core/
│  ├─ Components/
│  │  └─ PaneBase.cs             - Base class for all panes
│  ├─ Infrastructure/
│  │  ├─ PaneManager.cs          - Auto-tiling engine
│  │  ├─ PaneFactory.cs          - Creates panes by name
│  │  ├─ ProjectContextManager.cs - Global project filter
│  │  └─ WorkspaceState.cs       - Workspace persistence (9 workspaces)
│  ├─ DI/
│  │  ├─ ServiceContainer.cs     - DI container
│  │  └─ ServiceRegistration.cs  - Register all services
│  └─ Interfaces/
│     ├─ IProjectContextManager.cs
│     └─ ...
├─ Panes/                         - Concrete pane implementations
│  ├─ TaskListPane.cs
│  ├─ NotesPane.cs
│  ├─ ProcessingPane.cs
│  └─ ...
└─ Widgets/
   └─ Overlays/
      └─ CommandPaletteOverlay.cs - Fuzzy search command palette
```

---

## Data Models

### PaneBase (Abstract Base)
```csharp
public abstract class PaneBase
{
    public string PaneName { get; }           // "Tasks", "Notes", etc.
    public string PaneIcon { get; }           // Optional emoji
    public bool IsActive { get; }             // True if focused
    
    protected abstract UIElement BuildContent();
    protected virtual void OnProjectContextChanged(e) { }
    protected virtual void OnDispose() { }
}
```

### PaneWorkspaceState (Persistence)
```csharp
public class PaneWorkspaceState
{
    public string Name { get; set; }          // "Main", "Tasks"
    public int Index { get; set; }            // 0-8
    public List<string> OpenPaneTypes { get; set; }  // ["tasks", "notes"]
    public int FocusedPaneIndex { get; set; }        // Which is focused
    public Guid? CurrentProjectId { get; set; }      // Project filter
    public DateTime LastModified { get; set; }
}
```

### Command (Command Palette)
```csharp
public class Command
{
    public string Name { get; set; }          // "tasks", "notes", "close"
    public string Description { get; set; }   // "Open tasks pane"
    public string Shortcut { get; set; }      // "Ctrl+T"
    public CommandCategory Category { get; set; }  // Pane, Project, Task, etc.
}
```

---

## Integration Points

### To Add a New Pane
1. Create class inheriting `PaneBase`
2. Implement `BuildContent()` to return UI element
3. Register in `PaneFactory.paneCreators`
4. Add command to `CommandPaletteOverlay.BuildCommandList()`
5. User can now open it with `:panename<Enter>`

### To Add a New Command
1. Add to appropriate category in `CommandPaletteOverlay.BuildCommandList()`
2. Handle in `MainWindow.OnCommandExecuted()`

### To Filter Pane by Project
1. Override `OnProjectContextChanged()` in pane
2. Check `projectContext.CurrentProject`
3. Filter UI content based on project

---

## Current Status

### Working Now
- Pane system fully functional
- Workspace persistence (save/restore)
- Command palette with fuzzy search
- Project context filtering (global)
- Status bar showing context
- All keyboard shortcuts mapped

### Recent Changes (in this branch)
1. **MainWindow.xaml.cs**: Refactored to pane-based architecture
2. **ServiceRegistration.cs**: Added ProjectContextManager
3. **TaskManagementWidget_TUI.cs**: Added error handling in Initialize()

### Not Yet Implemented
- Actual pane implementations (TaskListPane, NotesPane, etc. are stubs)
- Task creation/management via command palette
- Advanced filtering options
- Settings dialog
- Help dialog

---

## Testing Checklist

- [ ] Open pane with Ctrl+T (Tasks)
- [ ] Open pane with `:notes<Enter>`
- [ ] Close pane with Ctrl+Shift+Q
- [ ] Navigate panes with Alt+Arrows
- [ ] Switch workspace with Alt+2
- [ ] Verify panes persist after workspace switch
- [ ] Change project context
- [ ] Verify all panes update when project changes
- [ ] Check status bar shows correct context
- [ ] Verify workspaces.json is created
- [ ] Restart app and verify workspace state restored

