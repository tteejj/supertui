# Specialized Layout Presets Guide

## Overview

SuperTUI now includes 4 specialized layout engines optimized for specific use cases. These complement the existing `DashboardLayoutEngine`, `GridLayoutEngine`, `StackLayoutEngine`, and `DockLayoutEngine`.

## Available Specialized Layouts

### 1. CodingLayoutEngine

**Purpose:** Development/coding workflows
**File:** `/home/teej/supertui/WPF/Core/Layout/CodingLayoutEngine.cs`

```
┌────┬──────────┬────┐
│Tree│  Editor  │ Git│
│30% │   40%    │30% │
│    │          │    │
│    ├──────────┤    │
│    │ Terminal │    │
│    │   60%    │    │
└────┴──────────┴────┘
```

**Widget Positions:**
- Position 0: Left column (30%, full height) - File tree/explorer
- Position 1: Center-top (40%, 40% height) - Editor/Notes
- Position 2: Center-bottom (40%, 60% height) - Terminal/Console
- Position 3: Right column (30%, full height) - Git status/Output

**Usage Example:**
```csharp
var workspace = new Workspace("Dev", 1, new CodingLayoutEngine());

var fileExplorer = new FileExplorerWidget();
var notes = new NotesWidget();
var terminal = new SystemMonitorWidget(); // Or custom terminal widget
var gitStatus = new GitStatusWidget();

workspace.AddWidget(fileExplorer, new LayoutParams { Row = 0 }); // Left
workspace.AddWidget(notes, new LayoutParams { Row = 1 });        // Center-top
workspace.AddWidget(terminal, new LayoutParams { Row = 2 });     // Center-bottom
workspace.AddWidget(gitStatus, new LayoutParams { Row = 3 });    // Right
```

**Best For:**
- Software development
- Code editing + terminal
- Git workflows
- Project management

---

### 2. FocusLayoutEngine

**Purpose:** Distraction-free single-task work
**File:** `/home/teej/supertui/WPF/Core/Layout/FocusLayoutEngine.cs`

```
┌──────────────┬──┐
│              │T │
│     Main     │o │
│     80%      │d│
│              │o │
│              │  │
└──────────────┴──┘
```

**Widget Positions:**
- Column 0: Main area (80%) - Primary content
- Column 1: Sidebar (20%, max 300px) - Reference/todos

**Usage Example:**
```csharp
var workspace = new Workspace("Focus", 2, new FocusLayoutEngine());

var mainEditor = new NotesWidget();
var todoSidebar = new TodoWidget();

workspace.AddWidget(mainEditor, new LayoutParams { Column = 0 });   // Main
workspace.AddWidget(todoSidebar, new LayoutParams { Column = 1 });  // Sidebar
```

**Best For:**
- Writing/documentation
- Deep focus work
- Reading with notes
- Single-task concentration

---

### 3. CommunicationLayoutEngine

**Purpose:** Messaging/email/list-detail workflows
**File:** `/home/teej/supertui/WPF/Core/Layout/CommunicationLayoutEngine.cs`

```
┌─────────┬───────────┐
│Contacts │  Thread   │
│  List   │  60%      │
│  40%    ├───────────┤
│         │  Reply    │
│         │  40%      │
└─────────┴───────────┘
```

**Widget Positions:**
- Position 0: Left panel (40%, full height) - List view
- Position 1: Right-top (60%, 60% height) - Detail/thread view
- Position 2: Right-bottom (60%, 40% height) - Compose/reply area

**Usage Example:**
```csharp
var workspace = new Workspace("Inbox", 3, new CommunicationLayoutEngine());

var contactList = new TaskManagementWidget();  // Use as contact list
var threadView = new NotesWidget();            // Use as message thread
var replyArea = new NotesWidget();             // Use as compose area

workspace.AddWidget(contactList, new LayoutParams { Row = 0 });  // List
workspace.AddWidget(threadView, new LayoutParams { Row = 1 });   // Detail
workspace.AddWidget(replyArea, new LayoutParams { Row = 2 });    // Compose
```

**Best For:**
- Email clients
- Messaging apps
- Master-detail interfaces
- List + preview patterns

---

### 4. MonitoringDashboardLayoutEngine

**Purpose:** System monitoring and metrics
**File:** `/home/teej/supertui/WPF/Core/Layout/MonitoringDashboardLayoutEngine.cs`

```
┌──────┬──────┬──────┐
│      │      │      │
│  1   │  2   │  3   │
│      │      │      │
├──────┴──────┴──────┤
│         4          │
│    (wide stats)    │
└────────────────────┘
```

**Widget Positions:**
- Positions 0-2: Top row (equal thirds) - Metrics/monitors
- Position 3: Bottom row (full width) - Logs/timeline/stats

**Usage Example:**
```csharp
var workspace = new Workspace("Monitor", 4, new MonitoringDashboardLayoutEngine());

// Note: Use WidgetFactory for proper dependency injection
var cpuMonitor = widgetFactory.CreateWidget<SystemMonitorWidget>();
var clockWidget = widgetFactory.CreateWidget<ClockWidget>();
var agendaWidget = widgetFactory.CreateWidget<AgendaWidget>();
var logViewer = widgetFactory.CreateWidget<TaskSummaryWidget>();  // Or custom log viewer

workspace.AddWidget(cpuMonitor, new LayoutParams { Row = 0 });   // Top-left
workspace.AddWidget(clockWidget, new LayoutParams { Row = 1 });  // Top-center
workspace.AddWidget(agendaWidget, new LayoutParams { Row = 2 }); // Top-right
workspace.AddWidget(logViewer, new LayoutParams { Row = 3 });    // Bottom-wide
```

**Best For:**
- System monitoring dashboards
- DevOps consoles
- Metrics visualization
- Server monitoring
- Real-time data displays

---

## Common Features

All specialized layouts include:

### Resizable Splitters
- Visual borders between panes
- Mouse drag to resize
- Minimum size constraints
- Theme-aware colors

### Directional Navigation
All layouts support i3-style directional navigation:
- `Alt+H` - Focus left
- `Alt+J` - Focus down
- `Alt+K` - Focus up
- `Alt+L` - Focus right

### Widget Movement
Move focused widget in direction:
- `Alt+Shift+H` - Move left
- `Alt+Shift+J` - Move down
- `Alt+Shift+K` - Move up
- `Alt+Shift+L` - Move right

### Widget Swapping
```csharp
// All layouts implement SwapWidgets
layout.SwapWidgets(widget1, widget2);
```

---

## Integration with Workspace System

Specialized layouts work seamlessly with the existing Workspace system:

```csharp
// Create workspace with specialized layout
var devWorkspace = new Workspace("Dev", 0, new CodingLayoutEngine());
var focusWorkspace = new Workspace("Focus", 1, new FocusLayoutEngine());
var inboxWorkspace = new Workspace("Inbox", 2, new CommunicationLayoutEngine());
var monitorWorkspace = new Workspace("Monitor", 3, new MonitoringDashboardLayoutEngine());

// Add to WorkspaceManager
workspaceManager.AddWorkspace(devWorkspace);
workspaceManager.AddWorkspace(focusWorkspace);
workspaceManager.AddWorkspace(inboxWorkspace);
workspaceManager.AddWorkspace(monitorWorkspace);

// Switch between workspaces (Win+1, Win+2, etc.)
```

---

## Graceful Degradation

All layouts handle fewer widgets than expected:

**CodingLayoutEngine** (expects 4 widgets):
- 1 widget: Shows in left position
- 2 widgets: Shows left + center-top
- 3 widgets: Shows left + center-top + center-bottom
- 4 widgets: All positions filled

**FocusLayoutEngine** (expects 2 widgets):
- 1 widget: Shows in main area
- 2 widgets: Main + sidebar

**CommunicationLayoutEngine** (expects 3 widgets):
- 1 widget: Shows in list position
- 2 widgets: List + detail
- 3 widgets: List + detail + compose

**MonitoringDashboardLayoutEngine** (expects 4 widgets):
- 1 widget: Shows top-left
- 2 widgets: Top-left + top-center
- 3 widgets: Top row filled
- 4 widgets: Top row + bottom widget

---

## Comparison with Existing Layouts

| Layout | Use Case | Flexibility | Preset Config |
|--------|----------|-------------|---------------|
| **GridLayoutEngine** | Generic N×M grid | High | None |
| **DashboardLayoutEngine** | 2×2 balanced view | Medium | 4 equal slots |
| **StackLayoutEngine** | Linear vertical/horizontal | Low | Sequential |
| **DockLayoutEngine** | Edge-docked panels | Medium | Edge positions |
| **CodingLayoutEngine** | Development | Low | 4-pane dev setup |
| **FocusLayoutEngine** | Single task | Low | 80/20 split |
| **CommunicationLayoutEngine** | List+detail | Low | 40/60 split |
| **MonitoringDashboardLayoutEngine** | Monitoring | Low | 3-top + bottom |

**When to use specialized vs. generic:**
- Use **specialized layouts** when your workflow matches the preset exactly
- Use **GridLayoutEngine** when you need custom grid dimensions
- Use **DashboardLayoutEngine** when you want balanced 2×2 layout

---

## Implementation Details

### Architecture
All layouts inherit from `LayoutEngine` base class:
```csharp
public abstract class LayoutEngine
{
    public Panel Container { get; protected set; }
    public abstract void AddChild(UIElement child, LayoutParams layoutParams);
    public abstract void RemoveChild(UIElement child);
    public abstract void Clear();
    public virtual List<UIElement> GetChildren();
    public virtual LayoutParams GetLayoutParams(UIElement element);
}
```

### Directional Navigation
Layouts implement `FindWidgetInDirection(UIElement fromWidget, FocusDirection direction)`:
- Returns next widget in specified direction
- Returns `null` if no widget exists in that direction
- Used by Workspace for `Alt+H/J/K/L` navigation

### Widget Swapping
Layouts implement `SwapWidgets(UIElement widget1, UIElement widget2)`:
- Exchanges positions of two widgets
- Updates internal tracking arrays
- Re-applies Grid attached properties
- Used by Workspace for `Alt+Shift+H/J/K/L` movement

---

## Performance

All specialized layouts:
- **Zero overhead** compared to manual GridLayoutEngine setup
- **Minimal memory** (4-element arrays, no dynamic allocation)
- **Fast rendering** (WPF Grid-based, hardware accelerated)
- **Efficient navigation** (O(1) position lookups)

---

## Customization

### Adjusting Proportions

Edit the layout constructor to change proportions:

```csharp
// Example: Change FocusLayoutEngine from 80/20 to 70/30
grid.ColumnDefinitions.Add(new ColumnDefinition
{
    Width = new GridLength(0.7, GridUnitType.Star),  // Changed from 0.8
    MinWidth = 400
});
grid.ColumnDefinitions.Add(new ColumnDefinition
{
    Width = new GridLength(0.3, GridUnitType.Star),  // Changed from 0.2
    MinWidth = 150,
    MaxWidth = 400  // Increased from 300
});
```

### Creating Custom Layouts

1. Inherit from `LayoutEngine`
2. Create `Grid` with desired structure
3. Implement position mapping (0-N)
4. Implement required abstract methods
5. Add `FindWidgetInDirection()` for navigation
6. Add `SwapWidgets()` for movement

See existing layouts for reference patterns.

---

## Future Enhancements (Optional)

**Not currently implemented, but possible:**

1. **Keyboard shortcuts for layout switching:**
   ```csharp
   // Win+Alt+C → Coding layout
   // Win+Alt+F → Focus layout
   // Win+Alt+M → Monitoring layout
   // Win+Alt+I → Communication (Inbox) layout
   ```

2. **Layout picker widget:**
   - Visual thumbnails of each layout
   - Click to apply to current workspace
   - Preview before applying

3. **Saved layout preferences:**
   - Per-workspace layout persistence
   - Auto-apply layout on workspace creation
   - Layout templates library

4. **Dynamic layout adjustment:**
   - Auto-switch based on widget count
   - Responsive layouts for different screen sizes
   - Custom breakpoints

---

## Troubleshooting

### Widget not showing
- Check position parameter (Row or Column)
- Verify position is in valid range (0-3 or 0-2)
- Ensure widget is not null

### Navigation not working
- Verify layout implements `FindWidgetInDirection()`
- Check that widgets are wrapped in ErrorBoundary (automatic in Workspace)
- Ensure widgets are in focusableElements list

### Splitters not visible
- Check ThemeManager provides valid Border color
- Verify splitters are added after grid definitions
- Ensure splitters have non-zero width/height

### Layout proportions wrong
- Check GridLength values in constructor
- Verify StarWidth values are proportional
- Test with different window sizes

---

## Credits

**Created:** 2025-10-26
**Author:** Claude Code (Sonnet 4.5)
**Project:** SuperTUI - WPF Widget Framework
**License:** Same as SuperTUI project

---

## See Also

- `/home/teej/supertui/WPF/Core/Layout/LayoutEngine.cs` - Base class
- `/home/teej/supertui/WPF/Core/Layout/GridLayoutEngine.cs` - Generic grid
- `/home/teej/supertui/WPF/Core/Layout/DashboardLayoutEngine.cs` - 2×2 dashboard
- `/home/teej/supertui/WPF/Core/Infrastructure/Workspace.cs` - Layout integration
- `/home/teej/supertui/WPF/ARCHITECTURE.md` - Overall architecture
