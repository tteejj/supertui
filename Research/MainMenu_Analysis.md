# MainMenu/Dashboard Screen Analysis

**Date:** 2025-10-23
**Purpose:** Research best UI patterns from existing TUIs to design superior MainMenuScreen for SuperTUI

---

## Research Summary

### TUIs Analyzed

1. **praxis-main (SpeedTUI)** - Dashboard-focused, performance-oriented
2. **alcar (BOLT-AXIOM)** - Clean menu with ASCII art, icon-based
3. **_R2 (Helios)** - Service-oriented with routing
4. **ConsoleUI (PMC)** - Legacy monolithic approach

---

## Key Features Discovered

### 1. SpeedTUI DashboardScreen (/home/teej/_tui/praxis-main/SpeedTUI/Screens/DashboardScreen.ps1)

**Strengths:**
- **Multi-section layout**: Header, Quick Stats, Main Menu, Recent Activity, Status Bar
- **Real-time statistics**: Shows commands, projects, time entries, memory usage, uptime
- **Performance monitoring**: Integration with PerformanceMonitor service
- **Two-column menu layout**: Efficient use of space
- **ASCII art branding**: Large "SPEEDTUI" logo
- **Rich status bar**: Shows debug mode, auto-save, theme, help keys
- **Multiple navigation methods**: Number keys, arrow keys, function keys
- **Activity feed**: Shows recent actions with timestamps

**UI Components Used:**
```powershell
- BorderHelper for consistent borders
- Quick stats with emoji icons (📊, 🎯, ⏱️, 📅, 💾, ⏰)
- Two-column menu (5 items left, 6 items right)
- Recent activity feed (last 3 items)
- Function key bindings (F1, F5, F11, Ctrl+Q, Ctrl+,)
```

**Menu Options:**
1. Time Tracking
2. Command Library
3. Projects
4. Tasks
5. File Browser
6. Text Editor
7. System Monitoring
8. Settings
9. Import/Export
10. Help & About
0. Exit

---

### 2. BOLT-AXIOM MainMenuScreen (/home/teej/_tui/alcar/Screens/MainMenuScreen.ps1)

**Strengths:**
- **Icon-based menu items**: Each item has emoji icon
- **Descriptive menu structure**: Title + Description for each item
- **ASCII art logo**: "BOLT-AXIOM" branding
- **Selection highlighting**: Clear visual feedback with ">"
- **Quick key shortcuts**: Single letter shortcuts (t, p, d, s, q)
- **Action closures**: Menu items have attached scriptblocks
- **Status bar integration**: Shows all available keys
- **Clean, centered layout**: Professional appearance

**UI Pattern:**
```powershell
MenuItems = @(
    @{
        Title = "Task Manager"
        Description = "Manage tasks and subtasks with tree view"
        Icon = "📋"
        Action = { Push-Screen }
    }
)
```

**Quick Keys:**
- `t` - Tasks
- `p` - Projects
- `d` - Dashboard
- `s` - Settings
- `q` - Quit

---

### 3. BOLT-AXIOM DashboardScreen (/home/teej/_tui/alcar/Screens/DashboardScreen.ps1)

**Strengths:**
- **Widget-based layout**: 4 separate widgets in 2x2 grid
- **Task Summary Widget**: Shows total, completed, in-progress, pending, overdue
- **Progress Widget**: Large percentage display with progress bar
- **Timeline Widget**: Today/week stats with mini calendar
- **Activity Widget**: Recent activity feed with timestamps
- **Color coding**: Green for success, yellow for warning, red for error, dim for inactive
- **Refresh capability**: Press 'r' to refresh stats

**Widgets:**
1. **Task Summary** (top-left): Counts with status indicators (●, ◐, ○, ✗)
2. **Completion** (top-right): Big percentage with block character art + progress bar
3. **Timeline** (bottom-left): Today/week/next week + mini calendar
4. **Activity** (bottom-right): Last 4 activities with timestamps

---

### 4. Helios (_R2) Architecture

**Strengths:**
- **Service-oriented**: DataManager, ThemeManager, NavigationService, KeybindingService
- **Module-based loading**: Lazy loading of screen modules
- **Routing system**: Screen registration with routes
- **Event system integration**: EventBus for decoupled communication
- **Dependency injection**: Services injected into screens
- **Focus management**: Dedicated FocusManager utility

**Architecture Pattern:**
```
Core Modules → Services → Layout System → Components → Screens
```

---

## What SuperTUI Can Do Better

### SuperTUI Capabilities NOT in Old TUIs

1. **C# Data Binding**
   - ObservableCollection auto-updates
   - INotifyPropertyChanged for reactive UI
   - No manual refresh needed

2. **Declarative Layout System**
   - GridLayout with CSS Grid-like syntax
   - Automatic sizing ("*", "Auto", pixels)
   - Proper component hierarchy

3. **True Event System**
   - C# events (not PowerShell scriptblocks)
   - Event bubbling/propagation
   - Strongly-typed event args

4. **Real Components**
   - DataGrid with auto-binding
   - ListView with selection
   - Button with Click events
   - TextBox with validation
   - Label with text alignment

5. **Theme System**
   - RGB color support
   - Theme inheritance
   - Component-level theming

6. **Service Container**
   - Proper DI container
   - Singleton/transient support
   - Type-safe service resolution

---

## Proposed SuperTUI MainMenuScreen Design

### Enhanced Features

#### 1. **Dashboard + Menu Hybrid**
Combine the best of both worlds:
- Top section: Quick stats (auto-updating via ObservableCollection)
- Middle section: Menu with icons and descriptions
- Bottom section: Recent activity feed (auto-updating)
- Persistent status bar

#### 2. **Auto-Updating Dashboard**
```powershell
# Services automatically update ObservableCollections
$stats = [DashboardStats]::new()  # INotifyPropertyChanged
$stats.TotalTasks = 35  # UI auto-updates!
```

#### 3. **Rich Menu Items**
```powershell
class MenuItem {
    [string]$Title
    [string]$Description
    [string]$Icon
    [string]$ShortcutKey
    [ConsoleKey]$QuickKey
    [EventHandler]$OnSelect
    [bool]$IsEnabled
    [string]$Badge  # For notifications (e.g., "5 new")
}
```

#### 4. **Widget-Based Layout**
Use GridLayout for perfect positioning:
```powershell
$layout = New-GridLayout -Rows "Auto","Auto","*","Auto","Auto" -Columns "*"

# Row 0: Header with ASCII art + clock
# Row 1: Quick stats (4-column grid inside)
# Row 2: Main menu (scrollable if needed)
# Row 3: Activity feed (last 5 items)
# Row 4: Status bar with keybindings
```

#### 5. **Live Data Integration**
```powershell
# TaskService updates stats automatically
$taskSvc = Get-Service "TaskService"
$statsWidget.ItemsSource = $taskSvc.Statistics  # ObservableCollection

# When tasks change, stats update automatically
$taskSvc.AddTask($newTask)  # Dashboard updates without refresh!
```

#### 6. **Advanced Navigation**
- Number keys for direct access (1-9, 0)
- Letter keys for quick shortcuts (t, p, w, etc.)
- Arrow keys for visual selection
- Function keys for system actions (F1=Help, F5=Refresh, F11=Monitor)
- Ctrl combinations for power users
- Tab for cycling through widgets

#### 7. **Notification System**
- Badge counters on menu items ("Tasks" shows "5 new")
- Activity feed shows real-time updates
- Toast notifications for important events

#### 8. **Responsive Layout**
- Adapts to terminal size changes
- Collapses widgets on small screens
- Maintains functionality at 80x24 minimum

---

## Comparison: Old vs SuperTUI

| Feature | ConsoleUI | SpeedTUI | BOLT-AXIOM | **SuperTUI** |
|---------|-----------|----------|------------|-------------|
| **Layout System** | Manual strings | BorderHelper | VT positioning | **GridLayout (declarative)** |
| **Data Updates** | Manual refresh | Manual refresh | Manual refresh | **Auto (ObservableCollection)** |
| **Menu Structure** | Simple list | Two-column | Icon + description | **Rich MenuItem objects** |
| **Statistics** | None | Service calls | Hardcoded | **Live bindings** |
| **Activity Feed** | None | Performance metrics | Hardcoded | **Event-driven** |
| **Theming** | None | Config-based | VT colors | **True theme system** |
| **Navigation** | Limited | Number + arrows | Letters + arrows | **Multi-modal (all methods)** |
| **Widgets** | None | Sections | 4 widgets | **Composable components** |
| **Events** | Manual | Manual | Manual | **C# events** |
| **Code Size** | ~200 lines | ~440 lines | ~230 lines | **~80 lines (PowerShell API)** |

---

## Recommended Implementation Strategy

### Phase 1: Core MainMenuScreen (Simple)
- Basic menu with navigation
- Number keys + arrow keys
- Push-Screen for navigation
- No widgets yet

### Phase 2: Dashboard Integration
- Add quick stats widget
- Bind to TaskService.Statistics (ObservableCollection)
- Auto-updating counts

### Phase 3: Activity Feed
- Subscribe to EventBus events
- Show last 5 activities
- Auto-scroll on new items

### Phase 4: Polish
- ASCII art header
- Status bar with keybindings
- Notification badges
- Responsive layout

---

## Key Decisions

### 1. **Main Menu vs Dashboard?**
**Decision:** Hybrid - "DashboardScreen" that includes menu

**Rationale:**
- SpeedTUI shows value of dashboard approach
- Users want info at-a-glance, not just a menu
- SuperTUI's auto-binding makes live dashboard trivial
- Can toggle between "menu mode" and "dashboard mode" if needed

### 2. **Widget Layout?**
**Decision:** Flexible GridLayout with optional widgets

**Rationale:**
- GridLayout handles positioning perfectly
- Widgets can be enabled/disabled via config
- Easy to add new widgets later
- Responsive to terminal size

### 3. **Menu Item Structure?**
**Decision:** Rich object model with icons, descriptions, badges

**Rationale:**
- BOLT-AXIOM showed value of descriptions
- SpeedTUI showed value of multiple nav methods
- Badges enable notification system
- Extensible for future features

### 4. **Data Binding Strategy?**
**Decision:** Full ObservableCollection bindings from services

**Rationale:**
- This is SuperTUI's killer feature
- No manual refresh = better UX
- Demonstrates framework capabilities
- Enables real-time updates

---

## Proposed Menu Structure

```powershell
@(
    @{ Icon="📋"; Title="Tasks"; Description="View and manage all tasks"; Key="t"; Badge="" }
    @{ Icon="📁"; Title="Projects"; Description="Organize tasks by project"; Key="p"; Badge="" }
    @{ Icon="📅"; Title="Today"; Description="Tasks due today"; Key="w"; Badge="3" }
    @{ Icon="📆"; Title="Week"; Description="This week's schedule"; Key="k"; Badge="" }
    @{ Icon="⏱️"; Title="Time Tracking"; Description="Log and review time entries"; Key="m"; Badge="" }
    @{ Icon="💼"; Title="Command Library"; Description="Saved commands and scripts"; Key="c"; Badge="" }
    @{ Icon="📂"; Title="File Browser"; Description="Navigate filesystem"; Key="f"; Badge="" }
    @{ Icon="📊"; Title="Reports"; Description="Analytics and statistics"; Key="r"; Badge="" }
    @{ Icon="⚙️"; Title="Settings"; Description="Configure application"; Key="s"; Badge="" }
    @{ Icon="❓"; Title="Help"; Description="Documentation and support"; Key="h"; Badge="" }
    @{ Icon="🚪"; Title="Exit"; Description="Close application"; Key="q"; Badge="" }
)
```

**Quick Keys Mapping:**
- `t` - Tasks
- `p` - Projects
- `w` - Today (w for "what's today")
- `k` - Week (k is near w)
- `m` - Time (m for "minutes")
- `c` - Commands
- `f` - Files
- `r` - Reports
- `s` - Settings
- `h` - Help
- `q` - Quit

**Number Keys (for those who prefer):**
- 1-9, 0 map to menu items in order

---

## Next Steps

1. **Design** the screen layout (GridLayout structure)
2. **Create** menu data structure
3. **Implement** basic navigation
4. **Add** dashboard widgets
5. **Integrate** services
6. **Test** and refine

---

## Files to Reference

- `/home/teej/_tui/praxis-main/SpeedTUI/Screens/DashboardScreen.ps1`
- `/home/teej/_tui/alcar/Screens/MainMenuScreen.ps1`
- `/home/teej/_tui/alcar/Screens/DashboardScreen.ps1`
- `/home/teej/_tui/_R2/main.ps1` (architecture)
- `/home/teej/supertui/Core/SuperTUI.Core.cs` (capabilities)
- `/home/teej/supertui/Module/SuperTUI.psm1` (API)

---

## Conclusion

SuperTUI can create a **significantly better** main screen by:

1. Combining dashboard stats with menu navigation
2. Using ObservableCollection for auto-updating data
3. Leveraging GridLayout for perfect positioning
4. Supporting multiple navigation paradigms
5. Providing rich menu items with icons, descriptions, and badges
6. Enabling real-time activity feed via EventBus
7. All in **~80 lines** of PowerShell (vs 200-440 in old TUIs)

The key is **not to copy** the old implementations, but to **leverage SuperTUI's superior architecture** to create something that was impossible before.
