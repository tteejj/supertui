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
    /// Specialized 4-pane coding/development layout.
    /// Optimized for: FileExplorer (left) + Editor/Notes (center-top) + Terminal (center-bottom) + Git/Output (right)
    /// Layout:
    /// ┌────┬──────────┬────┐
    /// │Tree│  Editor  │ Git│
    /// │30% │   40%    │30% │
    /// │    │          │    │
    /// │    ├──────────┤    │
    /// │    │ Terminal │    │
    /// │    │   60%    │    │
    /// └────┴──────────┴────┘
    /// </summary>
    public class CodingLayoutEngine : LayoutEngine
    {
        private Grid grid;
        private UIElement[] widgets = new UIElement[4]; // [0]=left, [1]=center-top, [2]=center-bottom, [3]=right
        private bool[] hasWidget = new bool[4];

        public CodingLayoutEngine()
        {
            grid = new Grid();
            Container = grid;

            // 3 columns: 30% | 40% | 30%
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(0.3, GridUnitType.Star),
                MinWidth = 150
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(0.4, GridUnitType.Star),
                MinWidth = 200
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(0.3, GridUnitType.Star),
                MinWidth = 150
            });

            // 2 rows in center column: 40% | 60%
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(0.4, GridUnitType.Star),
                MinHeight = 100
            });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(0.6, GridUnitType.Star),
                MinHeight = 100
            });

            // Add GridSplitters for resizing
            AddGridSplitters();
        }

        private void AddGridSplitters()
        {
            var theme = ThemeManager.Instance?.CurrentTheme ?? Theme.CreateDarkTheme();
            var splitterBrush = new SolidColorBrush(theme.Border);

            // Vertical splitter between left and center (col 0|1)
            var leftSplitter = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = splitterBrush,
                ResizeDirection = GridResizeDirection.Columns
            };
            Grid.SetColumn(leftSplitter, 0);
            Grid.SetRowSpan(leftSplitter, 2);
            grid.Children.Add(leftSplitter);

            // Vertical splitter between center and right (col 1|2)
            var rightSplitter = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = splitterBrush,
                ResizeDirection = GridResizeDirection.Columns
            };
            Grid.SetColumn(rightSplitter, 1);
            Grid.SetRowSpan(rightSplitter, 2);
            grid.Children.Add(rightSplitter);

            // Horizontal splitter in center column (row 0|1)
            var horizontalSplitter = new GridSplitter
            {
                Height = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = splitterBrush,
                ResizeDirection = GridResizeDirection.Rows
            };
            Grid.SetColumn(horizontalSplitter, 1);
            Grid.SetRow(horizontalSplitter, 0);
            grid.Children.Add(horizontalSplitter);
        }

        /// <summary>
        /// Set widget in specific position:
        /// 0 = Left column (file tree)
        /// 1 = Center-top (editor)
        /// 2 = Center-bottom (terminal)
        /// 3 = Right column (git/output)
        /// </summary>
        public void SetWidget(int position, UIElement widget)
        {
            if (position < 0 || position >= 4)
                throw new ArgumentOutOfRangeException(nameof(position), "Position must be 0-3");

            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            // Remove existing widget at this position
            if (widgets[position] != null)
            {
                grid.Children.Remove(widgets[position]);
            }

            // Add new widget
            widgets[position] = widget;
            hasWidget[position] = true;

            // Apply margin
            if (widget is FrameworkElement fe)
            {
                fe.Margin = new Thickness(5);
            }

            // Position in grid
            switch (position)
            {
                case 0: // Left column (full height)
                    Grid.SetColumn(widget, 0);
                    Grid.SetRow(widget, 0);
                    Grid.SetRowSpan(widget, 2);
                    break;

                case 1: // Center-top
                    Grid.SetColumn(widget, 1);
                    Grid.SetRow(widget, 0);
                    break;

                case 2: // Center-bottom
                    Grid.SetColumn(widget, 1);
                    Grid.SetRow(widget, 1);
                    break;

                case 3: // Right column (full height)
                    Grid.SetColumn(widget, 2);
                    Grid.SetRow(widget, 0);
                    Grid.SetRowSpan(widget, 2);
                    break;
            }

            grid.Children.Add(widget);

            if (!children.Contains(widget))
            {
                children.Add(widget);
            }
        }

        public UIElement GetWidget(int position)
        {
            if (position < 0 || position >= 4)
                throw new ArgumentOutOfRangeException(nameof(position));

            return widgets[position];
        }

        public void ClearPosition(int position)
        {
            if (position < 0 || position >= 4)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (widgets[position] != null)
            {
                grid.Children.Remove(widgets[position]);
                children.Remove(widgets[position]);
                widgets[position] = null;
                hasWidget[position] = false;
            }
        }

        public override void AddChild(UIElement child, LayoutParams layoutParams)
        {
            // For compatibility: use Row parameter as position (0-3)
            int position = layoutParams?.Row ?? FindFirstEmptyPosition();

            if (position >= 0 && position < 4)
            {
                SetWidget(position, child);
                layoutParams = layoutParams ?? new LayoutParams();
                layoutParams.Row = position;
                this.layoutParams[child] = layoutParams;
            }
            else
            {
                Logger.Instance?.Warning("CodingLayoutEngine", $"All positions full, cannot add widget");
            }
        }

        private int FindFirstEmptyPosition()
        {
            for (int i = 0; i < 4; i++)
            {
                if (!hasWidget[i])
                    return i;
            }
            return -1;
        }

        public override void RemoveChild(UIElement child)
        {
            for (int i = 0; i < 4; i++)
            {
                if (widgets[i] == child)
                {
                    ClearPosition(i);
                    layoutParams.Remove(child);
                    return;
                }
            }
        }

        public override void Clear()
        {
            for (int i = 0; i < 4; i++)
            {
                ClearPosition(i);
            }
            layoutParams.Clear();
        }

        public override List<UIElement> GetChildren()
        {
            return widgets.Where(w => w != null).ToList();
        }

        /// <summary>
        /// Find position index for a widget
        /// </summary>
        public int FindPosition(UIElement widget)
        {
            for (int i = 0; i < 4; i++)
            {
                if (widgets[i] == widget)
                    return i;
            }
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
                Logger.Instance?.Warning("CodingLayoutEngine", "Cannot swap: one or both widgets not found");
                return;
            }

            // Remove both
            grid.Children.Remove(widget1);
            grid.Children.Remove(widget2);

            // Swap in array
            widgets[pos1] = widget2;
            widgets[pos2] = widget1;

            // Re-add at new positions
            SetWidgetGridPosition(widget2, pos1);
            SetWidgetGridPosition(widget1, pos2);

            grid.Children.Add(widget1);
            grid.Children.Add(widget2);
        }

        private void SetWidgetGridPosition(UIElement widget, int position)
        {
            switch (position)
            {
                case 0: // Left
                    Grid.SetColumn(widget, 0);
                    Grid.SetRow(widget, 0);
                    Grid.SetRowSpan(widget, 2);
                    break;

                case 1: // Center-top
                    Grid.SetColumn(widget, 1);
                    Grid.SetRow(widget, 0);
                    Grid.SetRowSpan(widget, 1);
                    break;

                case 2: // Center-bottom
                    Grid.SetColumn(widget, 1);
                    Grid.SetRow(widget, 1);
                    Grid.SetRowSpan(widget, 1);
                    break;

                case 3: // Right
                    Grid.SetColumn(widget, 2);
                    Grid.SetRow(widget, 0);
                    Grid.SetRowSpan(widget, 2);
                    break;
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

            int targetPos = -1;

            // Map directions:
            // Positions: 0=left, 1=center-top, 2=center-bottom, 3=right
            switch (direction)
            {
                case FocusDirection.Left:
                    if (fromPos == 1 || fromPos == 2) targetPos = 0; // Center -> Left
                    else if (fromPos == 3) targetPos = 1; // Right -> Center-top
                    break;

                case FocusDirection.Right:
                    if (fromPos == 0) targetPos = 1; // Left -> Center-top
                    else if (fromPos == 1 || fromPos == 2) targetPos = 3; // Center -> Right
                    break;

                case FocusDirection.Up:
                    if (fromPos == 2) targetPos = 1; // Center-bottom -> Center-top
                    break;

                case FocusDirection.Down:
                    if (fromPos == 1) targetPos = 2; // Center-top -> Center-bottom
                    break;
            }

            return (targetPos >= 0 && targetPos < 4 && hasWidget[targetPos]) ? widgets[targetPos] : null;
        }
    }
}
