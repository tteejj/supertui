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
            // Add vertical splitters between columns
            for (int col = 0; col < columns - 1; col++)
            {
                var splitter = new GridSplitter
                {
                    Width = 5,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
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
                    Background = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
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
    }
}
