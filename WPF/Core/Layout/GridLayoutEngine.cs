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
    public class GridLayoutEngine : LayoutEngine
    {
        private Grid grid;
        private bool enableSplitters;

        public GridLayoutEngine(int rows, int columns, bool enableSplitters = false)
        {
            grid = new Grid();
            Container = grid;
            this.enableSplitters = enableSplitters;

            // Create row definitions with star sizing
            for (int i = 0; i < rows; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Star),
                    MinHeight = 50 // Minimum row height
                });
            }

            // Create column definitions with star sizing
            for (int i = 0; i < columns; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                    MinWidth = 100 // Minimum column width
                });
            }

            // Add splitters if enabled
            if (enableSplitters)
            {
                AddGridSplitters(rows, columns);
            }
        }

        private void AddGridSplitters(int rows, int columns)
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            var splitterBrush = new SolidColorBrush(theme.Border);

            // Add vertical splitters between columns
            for (int col = 0; col < columns - 1; col++)
            {
                var splitter = new GridSplitter
                {
                    Width = 5,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Background = splitterBrush,
                    ResizeDirection = GridResizeDirection.Columns
                };

                Grid.SetColumn(splitter, col);
                Grid.SetRowSpan(splitter, rows);
                grid.Children.Add(splitter);
            }

            // Add horizontal splitters between rows
            for (int row = 0; row < rows - 1; row++)
            {
                var splitter = new GridSplitter
                {
                    Height = 5,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    Background = splitterBrush,
                    ResizeDirection = GridResizeDirection.Rows
                };

                Grid.SetRow(splitter, row);
                Grid.SetColumnSpan(splitter, columns);
                grid.Children.Add(splitter);
            }
        }

        public void SetColumnWidth(int column, GridLength width)
        {
            if (column >= 0 && column < grid.ColumnDefinitions.Count)
            {
                grid.ColumnDefinitions[column].Width = width;
            }
        }

        public void SetRowHeight(int row, GridLength height)
        {
            if (row >= 0 && row < grid.RowDefinitions.Count)
            {
                grid.RowDefinitions[row].Height = height;
            }
        }

        public override void AddChild(UIElement child, LayoutParams lp)
        {
            // Validate row/column references
            if (lp.Row.HasValue)
            {
                if (lp.Row.Value < 0 || lp.Row.Value >= grid.RowDefinitions.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(lp.Row),
                        $"Row {lp.Row.Value} is invalid. Grid has {grid.RowDefinitions.Count} rows (0-{grid.RowDefinitions.Count - 1})");
                }
                Grid.SetRow(child, lp.Row.Value);
            }

            if (lp.Column.HasValue)
            {
                if (lp.Column.Value < 0 || lp.Column.Value >= grid.ColumnDefinitions.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(lp.Column),
                        $"Column {lp.Column.Value} is invalid. Grid has {grid.ColumnDefinitions.Count} columns (0-{grid.ColumnDefinitions.Count - 1})");
                }
                Grid.SetColumn(child, lp.Column.Value);
            }

            // Validate span doesn't exceed grid bounds
            if (lp.RowSpan.HasValue)
            {
                int row = lp.Row ?? 0;
                if (row + lp.RowSpan.Value > grid.RowDefinitions.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(lp.RowSpan),
                        $"RowSpan {lp.RowSpan.Value} starting at row {row} exceeds grid bounds ({grid.RowDefinitions.Count} rows)");
                }
                Grid.SetRowSpan(child, lp.RowSpan.Value);
            }

            if (lp.ColumnSpan.HasValue)
            {
                int col = lp.Column ?? 0;
                if (col + lp.ColumnSpan.Value > grid.ColumnDefinitions.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(lp.ColumnSpan),
                        $"ColumnSpan {lp.ColumnSpan.Value} starting at column {col} exceeds grid bounds ({grid.ColumnDefinitions.Count} columns)");
                }
                Grid.SetColumnSpan(child, lp.ColumnSpan.Value);
            }

            // Apply star sizing to grid definitions if specified
            if (lp.Row.HasValue && lp.StarHeight != 1.0)
            {
                SetRowHeight(lp.Row.Value, new GridLength(lp.StarHeight, GridUnitType.Star));
            }

            if (lp.Column.HasValue && lp.StarWidth != 1.0)
            {
                SetColumnWidth(lp.Column.Value, new GridLength(lp.StarWidth, GridUnitType.Star));
            }

            ApplyCommonParams(child, lp);
            children.Add(child);
            layoutParams[child] = lp;
            grid.Children.Add(child);
        }

        public override void RemoveChild(UIElement child)
        {
            grid.Children.Remove(child);
            children.Remove(child);
            layoutParams.Remove(child);
        }

        public override void Clear()
        {
            grid.Children.Clear();
            children.Clear();
            layoutParams.Clear();
        }

        /// <summary>
        /// Swap the positions of two widgets in the grid
        /// </summary>
        public void SwapWidgets(UIElement widget1, UIElement widget2)
        {
            if (widget1 == null || widget2 == null)
                return;

            if (!layoutParams.ContainsKey(widget1) || !layoutParams.ContainsKey(widget2))
            {
                Logger.Instance?.Warning("GridLayoutEngine", "Cannot swap widgets: one or both widgets not found in layout");
                return;
            }

            // Get current layout params
            var params1 = layoutParams[widget1];
            var params2 = layoutParams[widget2];

            // Swap Row/Column values
            int? tempRow = params1.Row;
            int? tempCol = params1.Column;
            int? tempRowSpan = params1.RowSpan;
            int? tempColSpan = params1.ColumnSpan;

            params1.Row = params2.Row;
            params1.Column = params2.Column;
            params1.RowSpan = params2.RowSpan;
            params1.ColumnSpan = params2.ColumnSpan;

            params2.Row = tempRow;
            params2.Column = tempCol;
            params2.RowSpan = tempRowSpan;
            params2.ColumnSpan = tempColSpan;

            // Update visual positions
            if (params1.Row.HasValue)
                Grid.SetRow(widget1, params1.Row.Value);
            if (params1.Column.HasValue)
                Grid.SetColumn(widget1, params1.Column.Value);
            if (params1.RowSpan.HasValue)
                Grid.SetRowSpan(widget1, params1.RowSpan.Value);
            if (params1.ColumnSpan.HasValue)
                Grid.SetColumnSpan(widget1, params1.ColumnSpan.Value);

            if (params2.Row.HasValue)
                Grid.SetRow(widget2, params2.Row.Value);
            if (params2.Column.HasValue)
                Grid.SetColumn(widget2, params2.Column.Value);
            if (params2.RowSpan.HasValue)
                Grid.SetRowSpan(widget2, params2.RowSpan.Value);
            if (params2.ColumnSpan.HasValue)
                Grid.SetColumnSpan(widget2, params2.ColumnSpan.Value);

            Logger.Instance?.Debug("GridLayoutEngine",
                $"Swapped widgets: ({params2.Row},{params2.Column}) <-> ({params1.Row},{params1.Column})");
        }

        /// <summary>
        /// Find widget in a specific direction from the given widget
        /// Returns null if no widget found in that direction
        /// </summary>
        public UIElement FindWidgetInDirection(UIElement fromWidget, FocusDirection direction)
        {
            if (fromWidget == null || !layoutParams.ContainsKey(fromWidget))
                return null;

            var fromParams = layoutParams[fromWidget];
            if (!fromParams.Row.HasValue || !fromParams.Column.HasValue)
                return null;

            int fromRow = fromParams.Row.Value;
            int fromCol = fromParams.Column.Value;

            UIElement bestMatch = null;
            double bestDistance = double.MaxValue;

            foreach (var kvp in layoutParams)
            {
                if (kvp.Key == fromWidget)
                    continue;

                var toParams = kvp.Value;
                if (!toParams.Row.HasValue || !toParams.Column.HasValue)
                    continue;

                int toRow = toParams.Row.Value;
                int toCol = toParams.Column.Value;

                bool isInDirection = false;
                double distance = 0;

                switch (direction)
                {
                    case FocusDirection.Left:
                        isInDirection = toCol < fromCol;
                        distance = (fromCol - toCol) + Math.Abs(fromRow - toRow) * 0.5;
                        break;
                    case FocusDirection.Right:
                        isInDirection = toCol > fromCol;
                        distance = (toCol - fromCol) + Math.Abs(fromRow - toRow) * 0.5;
                        break;
                    case FocusDirection.Up:
                        isInDirection = toRow < fromRow;
                        distance = (fromRow - toRow) + Math.Abs(fromCol - toCol) * 0.5;
                        break;
                    case FocusDirection.Down:
                        isInDirection = toRow > fromRow;
                        distance = (toRow - fromRow) + Math.Abs(fromCol - toCol) * 0.5;
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
    }
}
