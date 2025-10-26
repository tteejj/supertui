# SuperTUI UX/DX Integration Plan

**Status**: In Progress
**Started**: 2025-10-25
**Goal**: Homogenize widgets, improve integration, enhance UX/DX

---

## COMPLETED ✅

### Phase 1: Foundation (Critical Fixes)

#### 1.1 Demo Script Bugs ✅
- **Fixed**: SuperTUI.ps1:305 - Changed `ProjectManagementWidget` → `TaskManagementWidget`
- **Fixed**: DashboardLayoutEngine empty slot text - Now shows "Press Ctrl+N to add widget"
- **Fixed**: Status bar text - Simplified from 138 chars to 62 chars: "Ctrl+1-6: Workspaces | Tab: Next Widget | ?: Help | Ctrl+Q: Quit"
- **Impact**: Demo no longer crashes on Workspace 2 switch, UI guidance is accurate

#### 1.2 Real Data Connection ✅
- **Fixed**: TaskSummaryWidget now connects to actual TaskService
  - Removed hardcoded fake data (TotalTasks=15, etc.)
  - Added real-time event subscriptions (TaskAdded, TaskUpdated, TaskDeleted, TasksReloaded)
  - Implemented RefreshData() to pull live counts from TaskService
  - Added proper disposal (unsubscribe from events)
- **Impact**: Widget shows actual task counts, updates in real-time when tasks change

#### 1.3 Application Context ✅
- **Created**: `/WPF/Core/Infrastructure/ApplicationContext.cs` (112 lines)
  - Singleton pattern matching infrastructure style
  - Tracks: CurrentProject, CurrentFilter, CurrentWorkspace
  - Events: ProjectChanged, FilterChanged, WorkspaceChanged, NavigationRequested
  - Methods: RequestNavigation(), Reset()
- **Created**: TaskFilter enum (All, Active, Completed, Overdue, DueToday, DueThisWeek, NoDueDate, HighPriority)
- **Impact**: Widgets can now share context and coordinate behavior

#### 1.4 Event Classes ✅
- **Created**: `/WPF/Core/Events/WidgetEvents.cs` (86 lines)
  - Task events: TaskSelectedEvent, TaskCreatedEvent, TaskUpdatedEvent, TaskDeletedEvent
  - Project events: ProjectSelectedEvent, ProjectCreatedEvent, ProjectUpdatedEvent
  - Navigation: NavigationRequestedEvent, WorkspaceSwitchRequestedEvent
  - Filters: FilterChangedEvent
  - State: RefreshRequestedEvent, DataChangedEvent
- **Impact**: Typed events for EventBus communication between widgets

---

## IN PROGRESS ⏳

### Phase 2: Integration (Widget Communication)

#### 2.1 EventBus Integration (Current)
**Status**: EventBus infrastructure exists, need to wire up widgets

**Verified Existing**:
- EventBus singleton implementation exists ✅
- IEventBus interface exists ✅
- Supports typed pub/sub with priorities ✅
- Weak reference support ✅

**Next Steps**:
1. Initialize EventBus in SuperTUI.ps1 (add to infrastructure initialization)
2. Update KanbanBoardWidget to publish TaskSelectedEvent
3. Update AgendaWidget to publish TaskSelectedEvent
4. Update TaskManagementWidget to subscribe to TaskSelectedEvent and navigate
5. Add EventBus helper property to WidgetBase for easy access
6. Test cross-widget communication

#### 2.2 State Persistence
**Status**: Code exists, needs wiring

**To Do**:
1. Initialize StatePersistenceManager in SuperTUI.ps1
2. Hook up Window.Closing event to save all workspace states
3. Add auto-save on data changes (not cursor movement)
4. Test state restore on app restart

#### 2.3 Auto-Navigation System
**Status**: Design complete, ready to implement

**Design**:
- Widgets publish NavigationRequestedEvent via EventBus
- ApplicationContext.RequestNavigation() helper method
- WorkspaceManager subscribes and switches workspaces
- Target widget subscribes and selects appropriate item

**Examples**:
- KanbanWidget: Press Enter on task → Navigate to TaskManagementWidget with task selected
- AgendaWidget: Press P on task → Navigate to ProjectStatsWidget for task's project
- TaskManagementWidget: Press K → Navigate to KanbanWidget

---

## PLANNED 📝

### Phase 3: Visual System

#### 3.1 Focus Visual System
**Design**: WPF borders with glow effects (user preference)

**Implementation**:
- Update WidgetBase.OnGotFocus/OnLostFocus
- Change border thickness 1px → 2px on focus
- Change border color to theme.Accent on focus
- Add DropShadowEffect for glow (ShadowDepth=0, BlurRadius=10, Color=Accent)
- Update all widgets to inherit behavior

#### 3.2 StandardWidgetFrame
**Design**: Consistent widget structure

```
╔═══ [WIDGET TITLE] ═══════════════╗
║ [Optional toolbar/controls]      ║
╠═══════════════════════════════════╣
║                                   ║
║  Main Content Area                ║
║                                   ║
╠═══════════════════════════════════╣
║ Status | Shortcuts                ║
╚═══════════════════════════════════╝
```

**Components**:
- Title bar (widget name + context info)
- Optional control bar
- Scrollable content area
- Status bar (widget-specific info + shortcuts)
- Focus indicator (border + glow)

#### 3.3 Keyboard Shortcut Overlay
**Design**: Full-screen help overlay (press `?`)

**Features**:
- Global shortcuts section
- Current widget shortcuts section
- Context-sensitive (shows different shortcuts per widget)
- Clean WPF overlay with semi-transparent background
- Press Esc to close

---

### Phase 4: Widget Enhancements

#### 4.1 Per-Widget Improvements
**TaskManagementWidget**:
- Add project filter dropdown in toolbar
- Show current project name in title
- Add quick-add task button (Ctrl+N)
- Publish TaskSelectedEvent on selection change

**KanbanBoardWidget**:
- Subscribe to TaskSelectedEvent (select task if present)
- Publish TaskSelectedEvent on selection change
- Add navigation shortcut (E = Edit in TaskManagement, Enter = Edit in place)
- Real-time column count updates (already done via TaskService events)

**AgendaWidget**:
- Subscribe to TaskSelectedEvent
- Publish TaskSelectedEvent on selection change
- Add navigation shortcuts (E = Edit task, P = View project stats)
- Add collapse/expand sections

**TaskSummaryWidget** (Already done! ✅):
- Real data connection ✅
- Real-time updates ✅
- Could rename to "DashboardWidget" for clarity

**ProjectStatsWidget**:
- Subscribe to ProjectSelectedEvent
- Show current project stats
- Add time tracking integration
- Add completion metrics

---

## DESIGN DECISIONS (User Confirmed)

1. **Visual Style**: WPF borders (not ASCII box-drawing)
   - Easier theming
   - Can add glow effects
   - Better performance

2. **Navigation**: Automatic
   - Widgets auto-navigate on action (e.g., press Enter)
   - Seamless UX without extra keystrokes

3. **Data Scope**: Show all data
   - No global project filtering
   - Widgets show all tasks/projects
   - Individual widgets can have local filters

4. **Persistence**: Save on app close + data changes
   - Auto-save when user creates/updates/deletes data
   - Auto-save on app close
   - No save on cursor movement or focus changes

5. **Implementation Priority**: Foundation → Integration → Visual → Polish
   - Fix critical bugs first ✅
   - Wire up communication next ⏳
   - Polish visuals after functionality works
   - Enhance individual widgets last

---

## METRICS

### Code Changes So Far
- **Files Modified**: 3 (SuperTUI.ps1, DashboardLayoutEngine.cs, TaskSummaryWidget.cs)
- **Files Created**: 2 (ApplicationContext.cs, WidgetEvents.cs)
- **Lines Changed**: ~150
- **Bugs Fixed**: 3 critical (demo crash, wrong instructions, fake data)
- **New Features**: 2 (ApplicationContext, WidgetEvents)

### Remaining Work
- **Files to Modify**: ~15 (all interactive widgets + SuperTUI.ps1)
- **Files to Create**: ~3 (StandardWidgetFrame, FocusVisualHelper, ShortcutOverlay)
- **Estimated Lines**: ~800-1000
- **Estimated Time**: 4-6 hours of focused work

---

## NEXT ACTIONS

### Immediate (Next Session)
1. Initialize EventBus in SuperTUI.ps1
2. Add EventBus property to WidgetBase
3. Update KanbanBoardWidget to publish/subscribe events
4. Update AgendaWidget to publish/subscribe events
5. Test inter-widget communication (Kanban → TaskManagement)

### Short-term (This Week)
1. Wire up StatePersistenceManager
2. Implement focus visual system
3. Create StandardWidgetFrame
4. Build keyboard shortcut overlay

### Medium-term (Next Week)
1. Enhance all widgets with consistent UX
2. Add comprehensive testing
3. Update documentation
4. Create user guide

---

## TESTING CHECKLIST

### Functionality
- [ ] Demo script runs without crashes
- [ ] Workspace switching works (Ctrl+1-6)
- [ ] Widget focus navigation works (Tab, Ctrl+Up/Down)
- [ ] Task creation/edit/delete works
- [ ] Real-time updates work across widgets
- [ ] Inter-widget navigation works
- [ ] State persistence works (app close/reopen)

### Visual
- [ ] Focus indicators are visible and clear
- [ ] All widgets have consistent structure
- [ ] Empty slots show correct instructions
- [ ] Status bar is readable
- [ ] Keyboard shortcut overlay works
- [ ] Glow effects work on focus

### UX
- [ ] Keyboard shortcuts are discoverable
- [ ] Navigation is intuitive
- [ ] No duplicate shortcuts
- [ ] Error messages are clear
- [ ] State is preserved correctly

---

**Last Updated**: 2025-10-25
**Status**: Phase 1 Complete ✅, Phase 2 In Progress ⏳
