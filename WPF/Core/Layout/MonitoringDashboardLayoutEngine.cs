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
    /// Specialized 4-widget monitoring/dashboard layout.
    /// Optimized for: 3 equal widgets on top (metrics/monitors) + 1 wide widget on bottom (logs/stats)
    /// Layout:
    /// ┌──────┬──────┬──────┐
    /// │      │      │      │
    /// │  1   │  2   │  3   │
    /// │      │      │      │
    /// ├──────┴──────┴──────┤
    /// │         4          │
    /// │    (wide stats)    │
    /// └────────────────────┘
    /// </summary>
    public class MonitoringDashboardLayoutEngine : LayoutEngine
    {
        private Grid grid;
        private UIElement[] topWidgets = new UIElement[3]; // Top row: 3 equal widgets
        private UIElement bottomWidget = null; // Bottom row: 1 wide widget
        private bool[] hasTopWidget = new bool[3];

        public MonitoringDashboardLayoutEngine()
        {
            grid = new Grid();
            Container = grid;

            // 3 equal columns for top row
            for (int i = 0; i < 3; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                    MinWidth = 150
                });
            }

            // 2 rows: 60% top | 40% bottom
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(0.6, GridUnitType.Star),
                MinHeight = 150
            });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(0.4, GridUnitType.Star),
                MinHeight = 100
            });

            // Add splitters
            AddGridSplitters();
        }

        private void AddGridSplitters()
        {
            var theme = ThemeManager.Instance?.CurrentTheme ?? Theme.CreateDarkTheme();
            var splitterBrush = new SolidColorBrush(theme.Border);

            // Vertical splitter between column 0 and 1
            var splitter1 = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = splitterBrush,
                ResizeDirection = GridResizeDirection.Columns
            };
            Grid.SetColumn(splitter1, 0);
            Grid.SetRow(splitter1, 0);
            grid.Children.Add(splitter1);

            // Vertical splitter between column 1 and 2
            var splitter2 = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = splitterBrush,
                ResizeDirection = GridResizeDirection.Columns
            };
            Grid.SetColumn(splitter2, 1);
            Grid.SetRow(splitter2, 0);
            grid.Children.Add(splitter2);

            // Horizontal splitter between top and bottom rows
            var horizontalSplitter = new GridSplitter
            {
                Height = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = splitterBrush,
                ResizeDirection = GridResizeDirection.Rows
            };
            Grid.SetRow(horizontalSplitter, 0);
            Grid.SetColumnSpan(horizontalSplitter, 3);
            grid.Children.Add(horizontalSplitter);
        }

        /// <summary>
        /// Set widget in top row (position 0-2)
        /// </summary>
        public void SetTopWidget(int position, UIElement widget)
        {
            if (position < 0 || position >= 3)
                throw new ArgumentOutOfRangeException(nameof(position), "Position must be 0-2");

            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            // Remove existing widget at this position
            if (topWidgets[position] != null)
            {
                grid.Children.Remove(topWidgets[position]);
                children.Remove(topWidgets[position]);
            }

            topWidgets[position] = widget;
            hasTopWidget[position] = true;

            // Apply margin
            if (widget is FrameworkElement fe)
            {
                fe.Margin = new Thickness(5);
            }

            // Position in grid (top row)
            Grid.SetColumn(widget, position);
            Grid.SetRow(widget, 0);

            grid.Children.Add(widget);

            if (!children.Contains(widget))
            {
                children.Add(widget);
            }
        }

        /// <summary>
        /// Set bottom wide widget
        /// </summary>
        public void SetBottomWidget(UIElement widget)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            // Remove existing bottom widget
            if (bottomWidget != null)
            {
                grid.Children.Remove(bottomWidget);
                children.Remove(bottomWidget);
            }

            bottomWidget = widget;

            // Apply margin
            if (widget is FrameworkElement fe)
            {
                fe.Margin = new Thickness(5);
            }

            // Position in grid (bottom row, span all columns)
            Grid.SetColumn(widget, 0);
            Grid.SetRow(widget, 1);
            Grid.SetColumnSpan(widget, 3);

            grid.Children.Add(widget);

            if (!children.Contains(widget))
            {
                children.Add(widget);
            }
        }

        public UIElement GetTopWidget(int position)
        {
            if (position < 0 || position >= 3)
                throw new ArgumentOutOfRangeException(nameof(position));

            return topWidgets[position];
        }

        public UIElement GetBottomWidget() => bottomWidget;

        public void ClearTopPosition(int position)
        {
            if (position < 0 || position >= 3)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (topWidgets[position] != null)
            {
                grid.Children.Remove(topWidgets[position]);
                children.Remove(topWidgets[position]);
                topWidgets[position] = null;
                hasTopWidget[position] = false;
            }
        }

        public void ClearBottomWidget()
        {
            if (bottomWidget != null)
            {
                grid.Children.Remove(bottomWidget);
                children.Remove(bottomWidget);
                bottomWidget = null;
            }
        }

        public override void AddChild(UIElement child, LayoutParams layoutParams)
        {
            // For compatibility: Row 0-2 = top widgets, Row 3 = bottom widget
            int position = layoutParams?.Row ?? FindFirstEmptyPosition();

            if (position >= 0 && position < 3)
            {
                SetTopWidget(position, child);
            }
            else if (position == 3)
            {
                SetBottomWidget(child);
            }
            else
            {
                Logger.Instance?.Warning("MonitoringDashboardLayoutEngine", $"All positions full, cannot add widget");
                return;
            }

            layoutParams = layoutParams ?? new LayoutParams();
            layoutParams.Row = position;
            this.layoutParams[child] = layoutParams;
        }

        private int FindFirstEmptyPosition()
        {
            for (int i = 0; i < 3; i++)
            {
                if (!hasTopWidget[i])
                    return i;
            }
            if (bottomWidget == null)
                return 3;

            return -1; // All full
        }

        public override void RemoveChild(UIElement child)
        {
            for (int i = 0; i < 3; i++)
            {
                if (topWidgets[i] == child)
                {
                    ClearTopPosition(i);
                    layoutParams.Remove(child);
                    return;
                }
            }

            if (bottomWidget == child)
            {
                ClearBottomWidget();
                layoutParams.Remove(child);
            }
        }

        public override void Clear()
        {
            for (int i = 0; i < 3; i++)
            {
                ClearTopPosition(i);
            }
            ClearBottomWidget();
            layoutParams.Clear();
        }

        public override List<UIElement> GetChildren()
        {
            var result = new List<UIElement>();
            result.AddRange(topWidgets.Where(w => w != null));
            if (bottomWidget != null)
                result.Add(bottomWidget);
            return result;
        }

        /// <summary>
        /// Find position index for a widget (0-2 = top, 3 = bottom)
        /// </summary>
        private int FindPosition(UIElement widget)
        {
            for (int i = 0; i < 3; i++)
            {
                if (topWidgets[i] == widget)
                    return i;
            }
            if (bottomWidget == widget)
                return 3;

            return -1;
        }

        /// <summary>
        /// Swap two widgets between positions
        /// </summary>
        public void SwapWidgets(UIElement widget1, UIElement widget2)
        {
            if (widget1 == null || widget2 == null)
                return;

            int pos1 = FindPosition(widget1);
            int pos2 = FindPosition(widget2);

            if (pos1 < 0 || pos2 < 0)
            {
                Logger.Instance?.Warning("MonitoringDashboardLayoutEngine", "Cannot swap: one or both widgets not found");
                return;
            }

            // Remove both from grid
            grid.Children.Remove(widget1);
            grid.Children.Remove(widget2);

            // Swap references
            if (pos1 < 3)
                topWidgets[pos1] = widget2;
            else
                bottomWidget = widget2;

            if (pos2 < 3)
                topWidgets[pos2] = widget1;
            else
                bottomWidget = widget1;

            // Re-add with new positions
            SetWidgetGridPosition(widget1, pos2);
            SetWidgetGridPosition(widget2, pos1);

            grid.Children.Add(widget1);
            grid.Children.Add(widget2);
        }

        private void SetWidgetGridPosition(UIElement widget, int position)
        {
            if (position >= 0 && position < 3)
            {
                // Top row widget
                Grid.SetColumn(widget, position);
                Grid.SetRow(widget, 0);
                Grid.SetColumnSpan(widget, 1);
            }
            else if (position == 3)
            {
                // Bottom widget
                Grid.SetColumn(widget, 0);
                Grid.SetRow(widget, 1);
                Grid.SetColumnSpan(widget, 3);
            }
        }

        /// <summary>
        /// Find widget in a direction from the given widget
        /// </summary>
        public UIElement FindWidgetInDirection(UIElement fromWidget, FocusDirection direction)
        {
            if (fromWidget == null)
                return null;

            int fromPos = FindPosition(fromWidget);
            if (fromPos < 0)
                return null;

            // Map directions:
            // Positions: 0,1,2 = top row, 3 = bottom row
            switch (direction)
            {
                case FocusDirection.Left:
                    if (fromPos == 1) return topWidgets[0]; // Center -> Left
                    if (fromPos == 2) return topWidgets[1]; // Right -> Center
                    break;

                case FocusDirection.Right:
                    if (fromPos == 0 && hasTopWidget[1]) return topWidgets[1]; // Left -> Center
                    if (fromPos == 1 && hasTopWidget[2]) return topWidgets[2]; // Center -> Right
                    break;

                case FocusDirection.Up:
                    // Bottom -> corresponding top widget
                    if (fromPos == 3 && hasTopWidget[1])
                        return topWidgets[1]; // Prefer center
                    if (fromPos == 3 && hasTopWidget[0])
                        return topWidgets[0]; // Fall back to left
                    break;

                case FocusDirection.Down:
                    // Any top widget -> bottom
                    if (fromPos >= 0 && fromPos < 3)
                        return bottomWidget;
                    break;
            }

            return null;
        }
    }
}
