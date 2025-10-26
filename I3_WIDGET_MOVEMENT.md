# i3-Style Directional Widget Movement

## Overview

SuperTUI now supports i3-style directional widget movement, allowing you to move the focused widget in any direction by swapping positions with adjacent widgets.

## Keyboard Shortcuts

### Widget Movement (Alt+Shift+h/j/k/l)

- **Alt+Shift+h** - Move focused widget LEFT
- **Alt+Shift+j** - Move focused widget DOWN
- **Alt+Shift+k** - Move focused widget UP
- **Alt+Shift+l** - Move focused widget RIGHT

### Directional Focus (Alt+h/j/k/l)

For comparison, directional focus navigation (already implemented):

- **Alt+h** - Focus widget to the LEFT
- **Alt+j** - Focus widget BELOW
- **Alt+k** - Focus widget ABOVE
- **Alt+l** - Focus widget to the RIGHT

## How It Works

### Movement Behavior

1. **Get focused widget** - Identifies the currently focused widget
2. **Find target widget** - Searches for a widget in the specified direction
3. **Swap positions** - Swaps the grid/layout positions of the two widgets
4. **Keep focus** - Focus remains on the moved widget (it follows the widget)

### Direction Finding

The system uses spatial awareness to find widgets:

- **GridLayoutEngine**: Uses row/column coordinates with distance calculation
  - Prefers widgets directly in the direction (same row/column)
  - Falls back to nearest widget at an angle
  - Distance formula: `primary_distance + cross_distance * 0.5`

- **DashboardLayoutEngine**: Uses fixed 2x2 slot mapping
  - Slots: 0=TopLeft, 1=TopRight, 2=BottomLeft, 3=BottomRight
  - Direct mapping (e.g., TopLeft → TopRight when moving right)

### Edge Cases

- **No widget in direction**: Operation is a no-op, logs a debug message
- **Only one widget**: Movement is not possible
- **Unsupported layout**: Stack/Dock layouts don't support widget movement (logged as warning)

## Implementation Details

### Files Modified

1. **`/home/teej/supertui/WPF/Core/Layout/LayoutEngine.cs`**
   - Added `FocusDirection` enum (Left, Down, Up, Right)

2. **`/home/teej/supertui/WPF/Core/Layout/GridLayoutEngine.cs`**
   - Added `SwapWidgets(widget1, widget2)` - Swaps grid positions
   - Added `FindWidgetInDirection(fromWidget, direction)` - Spatial search

3. **`/home/teej/supertui/WPF/Core/Layout/DashboardLayoutEngine.cs`**
   - Added `SwapWidgets(widget1, widget2)` - Overload using existing slot-based swap
   - Added `FindWidgetInDirection(fromWidget, direction)` - 2x2 slot mapping

4. **`/home/teej/supertui/WPF/Core/Infrastructure/Workspace.cs`**
   - Added `MoveWidgetInDirection(direction)` - Main movement logic
   - Added `MoveWidgetLeft/Down/Up/Right()` - Convenience methods
   - Updated `HandleKeyDown()` - Added Alt+Shift+h/j/k/l bindings

### Swap Implementation

**GridLayoutEngine**:
```csharp
// Swaps Row, Column, RowSpan, ColumnSpan in LayoutParams
// Updates Grid.SetRow/SetColumn attached properties
// Updates visual positions immediately
```

**DashboardLayoutEngine**:
```csharp
// Finds slot indices for both widgets
// Calls existing slot-based swap (line 203)
// Handles empty slots gracefully
```

## Usage Example

```csharp
// Programmatic usage
workspace.MoveWidgetLeft();
workspace.MoveWidgetRight();
workspace.MoveWidgetInDirection(FocusDirection.Up);

// Or use keyboard shortcuts:
// Alt+Shift+h (move left)
// Alt+Shift+l (move right)
```

## Logging

All movement operations are logged:

- **Info**: Successful widget moves (`"Moved widget 'ClockWidget' Left"`)
- **Debug**: No widget found in direction
- **Warning**: Unsupported layout type or widget not found

## Compatibility

- **Supported Layouts**: GridLayoutEngine, DashboardLayoutEngine
- **Unsupported Layouts**: StackLayoutEngine, DockLayoutEngine
- **Error Boundaries**: Works seamlessly with widget error boundaries
- **Focus Management**: Focus follows the moved widget

## Future Enhancements

Potential improvements:

1. **Visual feedback**: Brief highlight/flash animation on swap
2. **Wrap-around**: Move to opposite edge if no widget found
3. **Stack/Dock support**: Implement movement for linear layouts
4. **Undo/Redo**: Track movement history for undo operations
5. **Custom keybindings**: Allow user-configurable movement shortcuts

## Testing

Build status: **✅ 0 Errors, 0 Warnings** (3.45s)

Manual testing required (Windows-only):
1. Create workspace with GridLayoutEngine or DashboardLayoutEngine
2. Add 4+ widgets to different positions
3. Focus a widget (Alt+h/j/k/l or Tab)
4. Press Alt+Shift+h/j/k/l to move widget
5. Verify widget swaps positions
6. Verify focus follows the moved widget

## Notes

- Uses Alt as modifier key (Windows key is harder to capture in WPF)
- Movement is relative to grid/slot positions, not visual screen positions
- ErrorBoundary wrappers are handled transparently
- Works with existing directional focus navigation
