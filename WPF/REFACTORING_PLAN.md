# Framework.cs Refactoring Plan

## Current State

`WPF/Core/Framework.cs` is a **monolithic 1067-line file** containing 13 classes:

1. `WidgetBase` (lines 23-189) - 167 lines
2. `ScreenBase` (lines 194-296) - 103 lines
3. `LayoutParams` (lines 298-330) - 33 lines
4. `LayoutEngine` (lines 332-389) - 58 lines
5. `GridLayoutEngine` (lines 391-561) - 171 lines
6. `DockLayoutEngine` (lines 563-601) - 39 lines
7. `StackLayoutEngine` (lines 603-643) - 41 lines
8. `Workspace` (lines 645-808) - 164 lines
9. `WorkspaceManager` (lines 810-896) - 87 lines
10. `ServiceContainer` (lines 898-942) - 45 lines
11. `EventBus` (lines 944-984) - 41 lines
12. `KeyboardShortcut` (lines 986-997) - 12 lines
13. `ShortcutManager` (lines 999-1067) - 69 lines

## Proposed File Structure

```
WPF/Core/
├── Components/
│   ├── WidgetBase.cs          ✅ CREATED
│   └── ScreenBase.cs
├── Layout/
│   ├── LayoutEngine.cs        (includes LayoutParams + abstract LayoutEngine)
│   ├── GridLayoutEngine.cs
│   ├── DockLayoutEngine.cs
│   └── StackLayoutEngine.cs
├── Infrastructure/
│   ├── Workspace.cs
│   ├── WorkspaceManager.cs
│   ├── ServiceContainer.cs
│   ├── EventBus.cs
│   └── ShortcutManager.cs     (includes KeyboardShortcut struct)
└── Framework.cs               (DEPRECATED - kept for reference, will be removed)
```

## Why This Refactoring?

### Problems with Current Monolithic File

1. **Hard to navigate** - 1067 lines, hard to find specific class
2. **Merge conflicts** - Multiple developers editing same file
3. **Violates Single Responsibility** - File contains 13 unrelated concerns
4. **Poor discoverability** - Can't tell what classes exist without reading entire file
5. **Harder to test** - All classes in one file makes mocking difficult

### Benefits of Split Files

1. **Clear organization** - Logical grouping (Components, Layout, Infrastructure)
2. **Easier navigation** - Jump to specific file/class
3. **Better IDE support** - Find references, rename refactoring works better
4. **Testability** - Each class can be tested independently
5. **Maintainability** - Clear boundaries between concerns

## Migration Strategy

### Phase 1: Extract Classes (CURRENT)

Create new files with classes extracted from Framework.cs:

**Status:**
- ✅ Created directory structure: `Components/`, `Layout/`, `Infrastructure/`
- ✅ Created `Components/WidgetBase.cs` (167 lines)
- ⏳ TODO: Extract remaining 12 classes

### Phase 2: Update Imports

All files that reference these classes need to be updated:

**Files to Update:**
- `/WPF/Widgets/*.cs` - All widgets inherit from `WidgetBase`
- `/WPF/Core/Extensions.cs` - References `Workspace`, `WorkspaceManager`
- `/WPF/SuperTUI_Demo.ps1` - Creates workspaces and layouts
- Any test files (once created)

**Import Changes:**
```csharp
// OLD (implicit - same namespace)
var widget = new WidgetBase();

// NEW (may need explicit using if in different namespace)
using SuperTUI.Core;  // Still works - all classes stay in SuperTUI.Core namespace
```

**Good news:** Since all classes stay in `namespace SuperTUI.Core`, existing code should continue to work without changes!

### Phase 3: Deprecate Framework.cs

1. Rename `Framework.cs` → `Framework.cs.deprecated`
2. Add comment at top: "This file has been split - see Components/, Layout/, Infrastructure/"
3. Test that everything still compiles

### Phase 4: Remove Framework.cs

Once confirmed everything works:
1. Delete `Framework.cs.deprecated`
2. Update `.claude/CLAUDE.md` with new file structure

## Implementation Script

```powershell
# Step 1: Extract all classes (automated approach)
$frameworkFile = "WPF/Core/Framework.cs"
$content = Get-Content $frameworkFile -Raw

# Define class boundaries (line numbers from grep output)
$classes = @(
    @{ Name = "WidgetBase"; Start = 23; End = 189; Dir = "Components" }
    @{ Name = "ScreenBase"; Start = 194; End = 296; Dir = "Components" }
    @{ Name = "LayoutEngine"; Start = 298; End = 389; Dir = "Layout"; Includes = @("LayoutParams") }
    @{ Name = "GridLayoutEngine"; Start = 391; End = 561; Dir = "Layout" }
    @{ Name = "DockLayoutEngine"; Start = 563; End = 601; Dir = "Layout" }
    @{ Name = "StackLayoutEngine"; Start = 603; End = 643; Dir = "Layout" }
    @{ Name = "Workspace"; Start = 645; End = 808; Dir = "Infrastructure" }
    @{ Name = "WorkspaceManager"; Start = 810; End = 896; Dir = "Infrastructure" }
    @{ Name = "ServiceContainer"; Start = 898; End = 942; Dir = "Infrastructure" }
    @{ Name = "EventBus"; Start = 944; End = 984; Dir = "Infrastructure" }
    @{ Name = "ShortcutManager"; Start = 999; End = 1067; Dir = "Infrastructure"; Includes = @("KeyboardShortcut") }
)

# For each class, extract and create new file
foreach ($class in $classes) {
    $lines = (Get-Content $frameworkFile)[($class.Start - 1)..($class.End - 1)]

    # Add using statements
    $header = @"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
"@

    $footer = "}"

    $fullContent = $header + "`n" + ($lines -join "`n") + "`n" + $footer

    $targetFile = "WPF/Core/$($class.Dir)/$($class.Name).cs"
    Set-Content -Path $targetFile -Value $fullContent

    Write-Host "Created: $targetFile"
}

# Step 2: Verify compilation
dotnet build  # Or however you compile

# Step 3: If successful, deprecate Framework.cs
Move-Item $frameworkFile "$frameworkFile.deprecated"
```

## Testing Plan

After refactoring:

1. **Compile test**: Ensure project still builds
2. **Widget test**: Create a widget, verify it works
3. **Layout test**: Create workspace with grid/dock/stack layouts
4. **State test**: Save/restore workspace state
5. **Focus test**: Tab navigation between widgets
6. **Disposal test**: Dispose widgets, verify no leaks

## Rollback Plan

If something breaks:
```powershell
# Restore original Framework.cs
Move-Item "WPF/Core/Framework.cs.deprecated" "WPF/Core/Framework.cs"

# Remove split files
Remove-Item -Recurse "WPF/Core/Components"
Remove-Item -Recurse "WPF/Core/Layout"
Remove-Item -Recurse "WPF/Core/Infrastructure"
```

## Estimated Effort

- **Extract classes**: ~1 hour (automated script + verification)
- **Test compilation**: ~15 minutes
- **Manual testing**: ~30 minutes
- **Documentation update**: ~15 minutes
- **Total**: ~2 hours

## Risks

| Risk | Mitigation |
|------|------------|
| Circular dependencies | Use interfaces (next task #12) |
| Missing using statements | All stay in SuperTUI.Core namespace |
| Broken references | Test compilation after each file |
| Lost git history | Use `git mv` to preserve history |

## Next Steps

After completing this refactoring:
1. ✅ Split Framework.cs → DONE (this task)
2. 🔄 Add interfaces (IWidget, IWorkspace, etc.) → Task #12
3. 🔄 Replace singletons with DI → Task #13
4. 🔄 Add unit tests → Task #14
5. 🔄 State versioning → Task #15

---

## Alternative: Keep Framework.cs

**Pros:**
- Zero risk - no refactoring needed
- Simpler mental model (one file = one place)
- No import changes needed

**Cons:**
- Still have all the problems listed above
- Technical debt accumulates
- Hard to maintain as project grows

**Recommendation:** **Do the refactoring.** The benefits far outweigh the effort, and it's a one-time cost that pays dividends forever.

---

**Status:** ✅ Plan complete, ready for execution
**Blocker:** None
**Dependencies:** None (can proceed immediately)
