# SuperTUI Project - Claude Code Memory

## Project Overview

**Project:** SuperTUI - WPF-Based PowerShell Widget Framework
**Location:** /home/teej/supertui
**Goal:** Build a desktop GUI framework with terminal aesthetics, featuring a workspace/widget system with declarative layouts

## Architecture Summary

**Technology Stack:** WPF (Windows Presentation Foundation) + PowerShell
**Not Terminal-Based:** This is a WPF desktop application styled to look like a terminal (monospace fonts, dark theme, ANSI colors), NOT a true terminal TUI using VT100 escape sequences.

**Three Layers:**
1. **C# Core Engine (WPF)** - Infrastructure (workspaces, widgets, layouts, rendering via WPF)
2. **PowerShell API** - Builders and helpers (future)
3. **Application** - Widget implementations and demo scripts

**Key Design Decisions:**
- WPF-based GUI, not terminal-based
- Windows-only (WPF limitation)
- Compile C# on load via Add-Type
- Workspace system: Multiple independent desktops with widget state preservation
- Declarative layouts: Grid, Stack, Dock
- Focus management with Tab navigation
- Resizable grid splitters

## Current Status

**Phase:** Early WPF prototype - Core framework exists, quality issues identified

**Completed:**
1. ✅ C# core framework (Framework.cs, Infrastructure.cs, Extensions.cs)
2. ✅ Base classes (WidgetBase, ScreenBase)
3. ✅ Layout engines (GridLayout, StackLayout, DockLayout)
4. ✅ Workspace management with state preservation
5. ✅ Focus management and keyboard shortcuts
6. ✅ Demo widgets (Clock, Counter, TaskSummary, Notes)
7. ✅ Grid splitters (resizable panels)
8. ✅ Demo application (SuperTUI_Demo.ps1)

**Infrastructure Components (Implemented but with quality issues):**
- Logging system (file + memory sinks)
- Configuration management
- Theme system
- Security/validation layer
- State persistence with undo/redo
- Plugin system
- Performance monitoring
- Error handling

**Next Steps:**
- Address critical code quality issues (see CRITICAL_ISSUES.md if created)
- Refactor hardcoded theme colors
- Implement proper widget ID system
- Add unit tests
- Create actual PowerShell API module

## Key Files

- `/home/teej/supertui/WPF/Core/Framework.cs` - Core WPF framework (1000 lines)
- `/home/teej/supertui/WPF/Core/Infrastructure.cs` - Infrastructure systems (1051 lines)
- `/home/teej/supertui/WPF/Core/Extensions.cs` - State persistence, plugins, perf monitoring (698 lines)
- `/home/teej/supertui/WPF/Widgets/*.cs` - Demo widget implementations
- `/home/teej/supertui/WPF/SuperTUI_Demo.ps1` - Main demo application
- `/home/teej/supertui/WPF/INFRASTRUCTURE_GUIDE.md` - Infrastructure documentation
- `/home/teej/supertui/DESIGN_DIRECTIVE.md` - Original terminal-based design (OUTDATED)

## Design Principles

1. **WPF Infrastructure in C#, Logic in PowerShell**
   - C# = Layouts, rendering, widgets, infrastructure
   - PowerShell = Application logic, services, business rules

2. **Declarative Over Imperative**
   - Use layouts, not manual positioning
   - Use data binding where possible
   - Use key bindings, not switch statements

3. **Workspace/Widget Architecture**
   - Multiple workspaces (virtual desktops)
   - Independent widget instances with state preservation
   - Focus management across widgets

## Known Issues & Technical Debt

### Critical Issues
1. **Security**: Path validation vulnerable to traversal attacks (Infrastructure.cs:921-927)
2. **Performance**: FileLogSink uses AutoFlush=true, blocking I/O on every log
3. **Memory Leak**: Plugin system cannot unload assemblies (Extensions.cs:467)
4. **Performance**: Undo stack uses inefficient clear/rebuild pattern (Extensions.cs:206-219)
5. **Type Safety**: ConfigurationManager.Get<T>() crashes on complex types like List<string>

### High Priority Issues
6. **Theme Integration**: All widgets have hardcoded colors instead of using ThemeManager
7. **Widget Identity**: Widgets identified by name, not unique IDs - causes state restoration bugs
8. **Threading**: ErrorHandler.ExecuteWithRetry uses Thread.Sleep, blocks UI thread
9. **Resource Leaks**: ClockWidget timer never disposed, other widgets missing cleanup
10. **Validation**: Layout engines don't validate row/col bounds

### Medium Priority Issues
11. **Architecture**: Framework.cs is monolithic (should split into multiple files)
12. **Testability**: No interfaces, everything is abstract classes or singletons
13. **Dependencies**: No proper DI container, everything uses singleton pattern
14. **Testing**: Zero unit test coverage
15. **State Persistence**: No versioning, no migration support, fragile JSON serialization

### Documentation Issues
- DESIGN_DIRECTIVE.md still describes terminal-based VT100 architecture (outdated)
- INFRASTRUCTURE_GUIDE.md claims features that aren't fully implemented
- No actual API documentation for C# classes
- No PowerShell module documentation (module doesn't exist yet)

## Development Workflow

### File Organization
- `WPF/Core/` - C# core framework files
- `WPF/Widgets/` - Widget implementations (C#)
- `WPF/` - PowerShell demo and test scripts
- `Core/` - OLD terminal-based core (archived, non-functional)

### Building & Running
```powershell
# Run demo
./WPF/SuperTUI_Demo.ps1

# Or enhanced demo
./WPF/SuperTUI_Enhanced_Demo.ps1
```

### Current Demo Features
- 3 workspaces with different layouts
- Widget focus indication (cyan border)
- Tab/Shift+Tab to cycle focus
- Ctrl+1/2/3 to switch workspaces
- Ctrl+Left/Right for prev/next workspace
- Resizable grid splitters
- State preservation across workspace switches

## Technical Constraints

**Platform:** Windows-only (WPF requirement)
**PowerShell:** Requires PowerShell 5.1+ with WPF support
**Not SSH-compatible:** Cannot run over SSH (requires display)
**Deployment:** Requires WPF runtime libraries

## Future Roadmap

### Phase 2: Quality & Refactoring
- Fix all critical security/performance issues
- Refactor to proper DI pattern
- Add comprehensive unit tests
- Split monolithic files
- Implement theme system integration
- Add widget disposal/cleanup

### Phase 3: PowerShell API
- Create SuperTUI.psm1 module
- Fluent builders for widgets and layouts
- Helper functions for common patterns
- Service registration API
- Event subscription helpers

### Phase 4: Real Applications
- Actual TaskService implementation
- File explorer widget
- Data grid with real data
- Settings screen
- Plugin examples

### Phase 5: Advanced Features
- Theme editor
- Hot-reload for widgets
- Plugin marketplace
- Telemetry/diagnostics
- Mouse interaction improvements

## Open Questions

1. Should we maintain both terminal and WPF versions? (Currently WPF only)
2. Do we need cross-platform support via Avalonia instead of WPF?
3. Should plugins be sandboxed in separate AppDomains for security?
4. What's the migration path for the old terminal-based code?

## Notes for Claude Code

- This is a WPF application, not a terminal TUI
- Code quality needs significant improvement before production use
- Many infrastructure features are partially implemented
- Testing is completely absent
- Don't assume features work just because they're in INFRASTRUCTURE_GUIDE.md
- When implementing fixes, prioritize critical issues first
- Always add disposal/cleanup to new widgets
- Use ThemeManager colors, never hardcode
- Consider testability when refactoring

## Code Review Summary (2025-10-24)

A comprehensive code review identified significant quality issues across all modules:

- **Infrastructure.cs**: Logging, config, theme, and security systems have critical bugs
- **Extensions.cs**: State persistence, plugin system, and perf monitoring are incomplete
- **Framework.cs**: Core is functional but has architectural issues
- **Widgets**: Hardcoded colors, missing disposal, no error handling

**Overall Assessment:**
- Architecture: 6/10 - Decent design, incomplete execution
- Implementation: 5/10 - Many stub implementations masquerading as features
- Production Ready: 3/10 - Needs significant work
- Code Consistency: 4/10 - Inconsistent patterns throughout

See critical issues section above for prioritized fix list.

## Success Criteria (Revised)

- ✅ Workspace system with state preservation (Working)
- ✅ Widget focus management (Working)
- ✅ Resizable layouts (Working)
- ❌ Clean, bug-free core infrastructure (Many bugs)
- ❌ Proper theme integration (Hardcoded colors everywhere)
- ❌ Production-ready code quality (Not even close)
- ❌ PowerShell API module (Doesn't exist)
- ❌ Unit test coverage (0%)

## Resources

**Current Implementation:**
- `/home/teej/supertui/WPF/` - Active WPF implementation

**Archived/Deprecated:**
- `/home/teej/supertui/Core/SuperTUI.Core.cs` - Original terminal-based engine (incomplete, archived)
- `/home/teej/supertui/DESIGN_DIRECTIVE.md` - Terminal-based design spec (outdated)

**Reference (Old TUI implementations):**
- `/home/teej/_tui/praxis-main/` - Redux-inspired terminal TUI
- `/home/teej/_tui/alcar/` - Clean architecture terminal TUI
- `/home/teej/pmc/ConsoleUI.Core.ps1` - Existing PowerShell terminal UI (14,599 lines)
