# Framework.cs Has Been Split

This file (`Framework.cs`) has been **deprecated** and split into multiple files for better organization.

## New File Structure

The original 1067-line monolithic file has been split into:

### Components (Base Classes)
- `Components/WidgetBase.cs` - Base class for widgets
- `Components/ScreenBase.cs` - Base class for screens

### Layout System
- `Layout/LayoutEngine.cs` - LayoutParams, SizeMode enum, abstract LayoutEngine
- `Layout/GridLayoutEngine.cs` - Grid-based layout with validation
- `Layout/DockLayoutEngine.cs` - Dock-based layout
- `Layout/StackLayoutEngine.cs` - Stack-based layout

### Infrastructure
- `Infrastructure/Workspace.cs` - Workspace class (virtual desktops)
- `Infrastructure/WorkspaceManager.cs` - Manages multiple workspaces
- `Infrastructure/ServiceContainer.cs` - Simple DI container
- `Infrastructure/EventBus.cs` - Event pub/sub system
- `Infrastructure/ShortcutManager.cs` - Keyboard shortcuts (includes KeyboardShortcut struct)

## Migration Guide

### If you're importing classes:

**No changes needed!** All classes remain in the `SuperTUI.Core` namespace. Your existing code will continue to work:

```csharp
using SuperTUI.Core;

// This still works
public class MyWidget : WidgetBase { ... }
public class MyScreen : ScreenBase { ... }
var workspace = new Workspace(...);
var layout = new GridLayoutEngine(...);
```

### If you're modifying the framework:

Navigate to the specific file instead of searching through Framework.cs:

- **Adding widget features?** → `Components/WidgetBase.cs`
- **Fixing grid layout bug?** → `Layout/GridLayoutEngine.cs`
- **Adding workspace feature?** → `Infrastructure/Workspace.cs`

## Benefits

1. ✅ **Easier navigation** - Jump directly to the class you need
2. ✅ **Better git diff** - Changes to one class don't affect others
3. ✅ **Clearer organization** - Logical grouping by responsibility
4. ✅ **Smaller files** - Each file is 40-170 lines instead of 1067
5. ✅ **Testability** - Each class can be tested in isolation

## Rollback

If you need to revert (e.g., for compatibility):

```bash
cd /home/teej/supertui/WPF/Core
mv Framework.cs.deprecated Framework.cs
rm -rf Components/ Layout/ Infrastructure/
```

---

**Date Split:** 2025-10-24
**Reason:** Medium-priority refactoring (task #11)
**Impact:** None - backward compatible
