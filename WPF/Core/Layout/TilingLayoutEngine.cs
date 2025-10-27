using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    /// <summary>
    /// Tiling layout modes for automatic or manual layout selection (i3-style)
    /// </summary>
    public enum TilingMode
    {
        /// <summary>
        /// Automatically select layout based on widget count
        /// </summary>
        Auto,

        /// <summary>
        /// Master + Stack layout: main widget (60%) on left, others stacked on right (40%)
        /// </summary>
        MasterStack,

        /// <summary>
        /// Wide mode: All widgets stacked vertically (horizontal splits)
        /// </summary>
        Wide,

        /// <summary>
        /// Tall mode: All widgets arranged horizontally (vertical splits)
        /// </summary>
        Tall,

        /// <summary>
        /// Grid mode: Force 2x2 or NxN grid layout
        /// </summary>
        Grid
    }

    /// <summary>
    /// i3-style tiling layout engine with preset layouts
    /// Automatically adjusts layout based on widget count
    /// </summary>
    public class TilingLayoutEngine : LayoutEngine
    {
        private Grid grid;
        private TilingMode currentMode = TilingMode.Auto;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;

        // Track widget grid positions for directional navigation
        private Dictionary<UIElement, GridPosition> widgetPositions = new Dictionary<UIElement, GridPosition>();

        /// <summary>
        /// Gets or sets the current tiling mode
        /// </summary>
        public TilingMode Mode
        {
            get => currentMode;
            set
            {
                if (currentMode != value)
                {
                    currentMode = value;
                    logger?.Debug("TilingLayoutEngine", $"Mode changed to {value}");
                    Relayout();
                }
            }
        }

        /// <summary>
        /// Initializes a new TilingLayoutEngine with dependency injection
        /// </summary>
        /// <param name="logger">Logger instance for debug output</param>
        /// <param name="themeManager">Theme manager for styling</param>
        public TilingLayoutEngine(ILogger logger, IThemeManager themeManager)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            grid = new Grid();
            Container = grid;

            logger?.Debug("TilingLayoutEngine", "Initialized with Auto mode");
        }

        /// <summary>
        /// Sets the tiling mode
        /// </summary>
        /// <param name="mode">The tiling mode to apply</param>
        public void SetMode(TilingMode mode)
        {
            Mode = mode;
        }

        /// <summary>
        /// Adds a child widget and triggers auto-relayout
        /// </summary>
        public override void AddChild(UIElement child, LayoutParams lp)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            // Store the child and its layout params
            children.Add(child);
            layoutParams[child] = lp ?? new LayoutParams();

            logger?.Debug("TilingLayoutEngine", $"Added widget (total: {children.Count})");

            // Trigger relayout
            Relayout();
        }

        /// <summary>
        /// Removes a child widget and triggers auto-relayout
        /// </summary>
        public override void RemoveChild(UIElement child)
        {
            if (child == null)
                return;

            grid.Children.Remove(child);
            children.Remove(child);
            layoutParams.Remove(child);
            widgetPositions.Remove(child);

            logger?.Debug("TilingLayoutEngine", $"Removed widget (remaining: {children.Count})");

            // Trigger relayout
            Relayout();
        }

        /// <summary>
        /// Clears all children
        /// </summary>
        public override void Clear()
        {
            grid.Children.Clear();
            children.Clear();
            layoutParams.Clear();
            widgetPositions.Clear();

            logger?.Debug("TilingLayoutEngine", "Cleared all widgets");
        }

        /// <summary>
        /// Rebuilds the layout based on current mode and widget count
        /// </summary>
        public void Relayout()
        {
            // Clear grid
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();
            widgetPositions.Clear();

            if (children.Count == 0)
            {
                logger?.Debug("TilingLayoutEngine", "No widgets to layout");
                return;
            }

            // Determine effective mode
            TilingMode effectiveMode = DetermineEffectiveMode();
            logger?.Debug("TilingLayoutEngine", $"Relayout: {children.Count} widgets, mode={effectiveMode}");

            // Apply the appropriate layout
            switch (effectiveMode)
            {
                case TilingMode.MasterStack:
                    ApplyMasterStackLayout();
                    break;
                case TilingMode.Wide:
                    ApplyWideLayout();
                    break;
                case TilingMode.Tall:
                    ApplyTallLayout();
                    break;
                case TilingMode.Grid:
                    ApplyGridLayout();
                    break;
            }
        }

        /// <summary>
        /// Determines the effective mode based on Auto logic or manual mode
        /// </summary>
        private TilingMode DetermineEffectiveMode()
        {
            if (currentMode != TilingMode.Auto)
                return currentMode;

            // Auto-select based on widget count
            return children.Count switch
            {
                1 => TilingMode.Grid,         // 1 widget: fullscreen via grid
                2 => TilingMode.Tall,         // 2 widgets: side-by-side
                3 or 4 => TilingMode.Grid,    // 3-4 widgets: grid layout
                _ => TilingMode.MasterStack   // 5+ widgets: master+stack
            };
        }

        /// <summary>
        /// WIDE: All widgets stacked vertically (horizontal splits, like i3 splitv)
        /// ┌──────────┐
        /// │    W1    │
        /// ├──────────┤
        /// │    W2    │
        /// ├──────────┤
        /// │    W3    │
        /// └──────────┘
        /// </summary>
        private void ApplyWideLayout()
        {
            // All widgets in rows, 1 column
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < children.Count; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var widget = children[i];
                var lp = layoutParams[widget];

                Grid.SetRow(widget, i);
                Grid.SetColumn(widget, 0);
                ApplyCommonParams(widget, lp);
                grid.Children.Add(widget);

                widgetPositions[widget] = new GridPosition(i, 0, 1, 1);
            }
        }

        /// <summary>
        /// TALL: All widgets arranged horizontally (vertical splits, like i3 splith)
        /// ┌───┬───┬───┐
        /// │W1 │W2 │W3 │
        /// └───┴───┴───┘
        /// </summary>
        private void ApplyTallLayout()
        {
            // All widgets in columns, 1 row
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < children.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var widget = children[i];
                var lp = layoutParams[widget];

                Grid.SetRow(widget, 0);
                Grid.SetColumn(widget, i);
                ApplyCommonParams(widget, lp);
                grid.Children.Add(widget);

                widgetPositions[widget] = new GridPosition(0, i, 1, 1);
            }
        }

        /// <summary>
        /// GRID: Force NxN grid layout (like i3 tabbed/stacking view, but as grid)
        /// ┌──────┬──────┐
        /// │  W1  │  W2  │
        /// ├──────┼──────┤
        /// │  W3  │  W4  │
        /// └──────┴──────┘
        /// </summary>
        private void ApplyGridLayout()
        {
            if (children.Count == 0)
                return;

            // Calculate grid dimensions
            int cols = (int)Math.Ceiling(Math.Sqrt(children.Count));
            int rows = (int)Math.Ceiling((double)children.Count / cols);

            // Create grid structure
            for (int r = 0; r < rows; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (int c = 0; c < cols; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Place widgets
            for (int i = 0; i < children.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;

                var widget = children[i];
                var lp = layoutParams[widget];

                Grid.SetRow(widget, row);
                Grid.SetColumn(widget, col);
                ApplyCommonParams(widget, lp);
                grid.Children.Add(widget);

                widgetPositions[widget] = new GridPosition(row, col, 1, 1);
            }
        }

        /// <summary>
        /// MASTER_STACK: 6+ widgets - main (60%) left, stack (40%) right
        /// ┌──────────┬───┐
        /// │    W1    │W2 │
        /// │  (main)  │W3 │
        /// │   60%    │W4 │
        /// │          │...│
        /// └──────────┴───┘
        /// </summary>
        private void ApplyMasterStackLayout()
        {
            int stackCount = children.Count - 1;

            // Create rows for stack
            for (int i = 0; i < stackCount; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); // 60%
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // 40%

            // Main widget (left, spans all rows)
            var mainWidget = children[0];
            var mainLp = layoutParams[mainWidget];
            Grid.SetRow(mainWidget, 0);
            Grid.SetColumn(mainWidget, 0);
            Grid.SetRowSpan(mainWidget, stackCount);
            ApplyCommonParams(mainWidget, mainLp);
            grid.Children.Add(mainWidget);
            widgetPositions[mainWidget] = new GridPosition(0, 0, stackCount, 1);

            // Stack widgets (right)
            for (int i = 1; i < children.Count; i++)
            {
                var widget = children[i];
                var lp = layoutParams[widget];

                Grid.SetRow(widget, i - 1);
                Grid.SetColumn(widget, 1);
                ApplyCommonParams(widget, lp);
                grid.Children.Add(widget);

                widgetPositions[widget] = new GridPosition(i - 1, 1, 1, 1);
            }
        }

        /// <summary>
        /// Find widget in a specific direction from the given widget
        /// </summary>
        /// <param name="fromWidget">The widget to search from</param>
        /// <param name="direction">The direction to search</param>
        /// <returns>The nearest widget in that direction, or null if none found</returns>
        public UIElement FindWidgetInDirection(UIElement fromWidget, FocusDirection direction)
        {
            if (fromWidget == null || !widgetPositions.ContainsKey(fromWidget))
                return null;

            var fromPos = widgetPositions[fromWidget];
            UIElement bestMatch = null;
            double bestDistance = double.MaxValue;

            foreach (var kvp in widgetPositions)
            {
                if (kvp.Key == fromWidget)
                    continue;

                var toPos = kvp.Value;
                bool isInDirection = false;
                double distance = 0;

                // Calculate center points for more accurate distance
                double fromCenterRow = fromPos.Row + fromPos.RowSpan / 2.0;
                double fromCenterCol = fromPos.Column + fromPos.ColumnSpan / 2.0;
                double toCenterRow = toPos.Row + toPos.RowSpan / 2.0;
                double toCenterCol = toPos.Column + toPos.ColumnSpan / 2.0;

                switch (direction)
                {
                    case FocusDirection.Left:
                        isInDirection = toCenterCol < fromCenterCol;
                        distance = (fromCenterCol - toCenterCol) + Math.Abs(fromCenterRow - toCenterRow) * 0.5;
                        break;
                    case FocusDirection.Right:
                        isInDirection = toCenterCol > fromCenterCol;
                        distance = (toCenterCol - fromCenterCol) + Math.Abs(fromCenterRow - toCenterRow) * 0.5;
                        break;
                    case FocusDirection.Up:
                        isInDirection = toCenterRow < fromCenterRow;
                        distance = (fromCenterRow - toCenterRow) + Math.Abs(fromCenterCol - toCenterCol) * 0.5;
                        break;
                    case FocusDirection.Down:
                        isInDirection = toCenterRow > fromCenterRow;
                        distance = (toCenterRow - fromCenterRow) + Math.Abs(fromCenterCol - toCenterCol) * 0.5;
                        break;
                }

                if (isInDirection && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = kvp.Key;
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Swap two widgets and re-layout
        /// </summary>
        /// <param name="widget1">First widget</param>
        /// <param name="widget2">Second widget</param>
        public void SwapWidgets(UIElement widget1, UIElement widget2)
        {
            if (widget1 == null || widget2 == null)
                return;

            if (!children.Contains(widget1) || !children.Contains(widget2))
            {
                logger?.Warning("TilingLayoutEngine", "Cannot swap widgets: one or both widgets not found");
                return;
            }

            // Swap in children list
            int index1 = children.IndexOf(widget1);
            int index2 = children.IndexOf(widget2);

            children[index1] = widget2;
            children[index2] = widget1;

            logger?.Debug("TilingLayoutEngine", $"Swapped widgets at positions {index1} and {index2}");

            // Re-layout
            Relayout();
        }

        /// <summary>
        /// Internal structure to track widget grid positions
        /// </summary>
        private class GridPosition
        {
            public int Row { get; }
            public int Column { get; }
            public int RowSpan { get; }
            public int ColumnSpan { get; }

            public GridPosition(int row, int column, int rowSpan, int columnSpan)
            {
                Row = row;
                Column = column;
                RowSpan = rowSpan;
                ColumnSpan = columnSpan;
            }
        }
    }
}
