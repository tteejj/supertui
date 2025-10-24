# SuperTUI Project - Claude Code Memory

## Project Overview

**Project:** SuperTUI - .NET-Powered PowerShell TUI Framework
**Location:** /home/teej/supertui
**Goal:** Build a declarative, high-performance terminal UI framework with 70-80% code reduction vs existing ConsoleUI

## Architecture Summary

**Three Layers:**
1. **C# Core Engine** - Infrastructure (layouts, components, rendering, events)
2. **PowerShell API** - Fluent builders and helpers
3. **Application** - Screens and services

**Key Design Decisions:**
- Compile C# on load via Add-Type (simplicity)
- Use ObservableCollection for auto-binding UI updates
- Stack-based navigation with optional routing
- CSS-like declarative layouts (GridLayout, StackLayout, DockLayout)
- Event-driven architecture with C# events

## Current Status

**Phase:** Phase 1 COMPLETE ✅ - Ready for Phase 2
**Completed:**
1. ✅ C# core engine (SuperTUI.Core.cs - 2,247 lines)
2. ✅ All base classes and layouts
3. ✅ All core components
4. ✅ VT100 rendering, Terminal, ScreenManager
5. ✅ EventBus, ServiceContainer, Theme
6. ✅ Compilation tested (0.9s compile time)
7. ✅ Component tests (all passed)

**Next Steps:**
1. Create PowerShell API module (SuperTUI.psm1)
2. Implement fluent builders
3. Create first real screen
4. Begin porting from existing ConsoleUI

## Key Files

- `/home/teej/supertui/DESIGN_DIRECTIVE.md` - Complete architectural specification
- `/home/teej/pmc/ConsoleUI.Core.ps1` - Existing implementation (14,599 lines, 58 screens)

## TUI Research Summary

**Implementations Analyzed:**
- **praxis-main** - Most advanced, Redux-inspired, SpeedTUI vision
- **alcar** - Clean architecture, excellent base classes
- **_R2** - Service-oriented with routing
- **_HELIOS** - Redux state management, time-travel debugging
- **_NEW_HELIOS** - Pure PowerShell approach
- **_CLASSY** - OOP class hierarchy
- **_XP** - Monolithic with decomposer tooling

**Best Patterns Extracted:**
- Screen base class from alcar (template methods, key bindings)
- NavigationService with routing from _R2
- Redux state store from _HELIOS (optional)
- Focus Manager from _NEW_HELIOS (BFS traversal)
- VT100 renderer from current ConsoleUI (proven, performant)
- StringCache optimization from current ConsoleUI

## Design Principles

1. **Infrastructure in C#, Logic in PowerShell**
2. **Declarative Over Imperative**
3. **Convention Over Configuration**

## Success Metrics

- ✅ 70-80% code reduction vs current ConsoleUI
- ✅ New screen in <50 lines
- ✅ Zero manual position calculations
- ✅ Zero manual data refresh calls
- ✅ Compile time < 2 seconds
- ✅ Smooth 30+ FPS rendering

## Screen Inventory (from existing ConsoleUI)

**Total:** 58 screens to port

**Categories:**
- Tasks (3): List, Form, Detail
- Projects (3): List, Form, Stats
- Views (13): Today, Week, Month, Agenda, Kanban, Calendar, Overdue, Upcoming, Tomorrow, NextActions, Blocked, NoDueDate, Burndown
- Tools (7): FileExplorer, Notes, CommandLibrary, Timer, Search, MultiSelect, Help
- Time (5): List, Add, Edit, Delete, Report
- Dependencies (3): Add, Remove, View
- Focus (3): Set, Clear, Status
- Backup (3): View, Restore, Clear
- System (6): MainMenu, Theme, ThemeEditor, Settings, Undo, Redo

**Note:** Additional screens will be added as features are identified

## Development Workflow

### Using Sub-Agents
- Use general-purpose agents for complex multi-file refactoring
- Use Explore agents for codebase investigation
- Launch parallel agents for independent tasks

### Project Organization
- All C# code in `Core/`
- PowerShell module in `Module/`
- Application screens in `App/Screens/`
- Services in `App/Services/`

## Technical Decisions

**C# Compilation:** Inline Add-Type (not pre-compiled DLL)
**Backward Compatibility:** Clean break, no migration layer
**Feature Priorities:**
1. Core framework (layouts, components, navigation)
2. Essential screens (tasks, projects, views)
3. Advanced features (file explorer, notes, calendar, command library)
4. Future: Mouse support, split panes, inline editing

**Scope:** Somewhat general-purpose but focused on project management

## Open Questions

None currently - design is approved and ready for implementation.

## Implementation Notes

### Phase 1: Core Engine (Current)
Starting with C# core engine implementation. Focus on:
- UIElement, Screen, Component base classes
- GridLayout, StackLayout implementations
- Basic components (Label, Button, TextBox, DataGrid)
- ScreenManager for navigation
- Terminal/VT100 rendering
- EventBus for pub/sub

### Testing Strategy
- Create simple test screen after each component
- Validate layout calculations visually
- Test data binding with ObservableCollection
- Verify key handling and navigation

## Resources

**Reference Implementations:**
- `/home/teej/_tui/praxis-main/` - Advanced patterns
- `/home/teej/_tui/alcar/` - Clean architecture
- `/home/teej/_tui/_R2/` - Service patterns
- `/home/teej/pmc/ConsoleUI.Core.ps1` - VT100 rendering, current screens

**Documentation:**
- PowerShell Add-Type: https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/add-type
- INotifyPropertyChanged: https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.inotifypropertychanged
- ObservableCollection: https://docs.microsoft.com/en-us/dotnet/api/system.collections.objectmodel.observablecollection-1

## Notes for Claude Code

- Prioritize using sub-agents for exploration and complex refactoring
- Keep design directive updated as decisions are made
- Document all architectural changes here
- Track implementation progress with TodoWrite
- Use parallel tool calls when tasks are independent
- Reference existing TUI implementations when solving problems
