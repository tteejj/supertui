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
    /// Specialized 3-pane communication/messaging layout.
    /// Optimized for: List view (40%) + Thread/Detail view (60% top) + Reply/Compose (60% bottom)
    /// Layout:
    /// ┌─────────┬───────────┐
    /// │Contacts │  Thread   │
    /// │  List   │  60%      │
    /// │  40%    ├───────────┤
    /// │         │  Reply    │
    /// │         │  40%      │
    /// └─────────┴───────────┘
    /// </summary>
    public class CommunicationLayoutEngine : LayoutEngine
    {
        private Grid grid;
        private UIElement listWidget = null;
        private UIElement detailWidget = null;
        private UIElement composeWidget = null;

        public CommunicationLayoutEngine()
        {
            grid = new Grid();
            Container = grid;

            // 2 columns: 40% list | 60% detail
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(0.4, GridUnitType.Star),
                MinWidth = 200
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(0.6, GridUnitType.Star),
                MinWidth = 300
            });

            // 2 rows in right column: 60% detail | 40% compose
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

            // Vertical splitter between list and detail
            var verticalSplitter = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = splitterBrush,
                ResizeDirection = GridResizeDirection.Columns
            };
            Grid.SetColumn(verticalSplitter, 0);
            Grid.SetRowSpan(verticalSplitter, 2);
            grid.Children.Add(verticalSplitter);

            // Horizontal splitter in right column
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
        /// Set list widget (left panel, full height)
        /// </summary>
        public void SetListWidget(UIElement widget)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            // Remove existing list widget
            if (listWidget != null)
            {
                grid.Children.Remove(listWidget);
                children.Remove(listWidget);
            }

            listWidget = widget;

            // Apply margin
            if (widget is FrameworkElement fe)
            {
                fe.Margin = new Thickness(5);
            }

            // Position in grid (left column, span both rows)
            Grid.SetColumn(widget, 0);
            Grid.SetRow(widget, 0);
            Grid.SetRowSpan(widget, 2);

            grid.Children.Add(widget);

            if (!children.Contains(widget))
            {
                children.Add(widget);
            }
        }

        /// <summary>
        /// Set detail widget (right-top panel)
        /// </summary>
        public void SetDetailWidget(UIElement widget)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            // Remove existing detail widget
            if (detailWidget != null)
            {
                grid.Children.Remove(detailWidget);
                children.Remove(detailWidget);
            }

            detailWidget = widget;

            // Apply margin
            if (widget is FrameworkElement fe)
            {
                fe.Margin = new Thickness(5);
            }

            // Position in grid (right column, top row)
            Grid.SetColumn(widget, 1);
            Grid.SetRow(widget, 0);

            grid.Children.Add(widget);

            if (!children.Contains(widget))
            {
                children.Add(widget);
            }
        }

        /// <summary>
        /// Set compose widget (right-bottom panel)
        /// </summary>
        public void SetComposeWidget(UIElement widget)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            // Remove existing compose widget
            if (composeWidget != null)
            {
                grid.Children.Remove(composeWidget);
                children.Remove(composeWidget);
            }

            composeWidget = widget;

            // Apply margin
            if (widget is FrameworkElement fe)
            {
                fe.Margin = new Thickness(5);
            }

            // Position in grid (right column, bottom row)
            Grid.SetColumn(widget, 1);
            Grid.SetRow(widget, 1);

            grid.Children.Add(widget);

            if (!children.Contains(widget))
            {
                children.Add(widget);
            }
        }

        public UIElement GetListWidget() => listWidget;
        public UIElement GetDetailWidget() => detailWidget;
        public UIElement GetComposeWidget() => composeWidget;

        public override void AddChild(UIElement child, LayoutParams layoutParams)
        {
            // For compatibility: Row 0 = list, Row 1 = detail, Row 2 = compose
            int position = layoutParams?.Row ?? FindFirstEmptyPosition();

            if (position == 0)
            {
                SetListWidget(child);
            }
            else if (position == 1)
            {
                SetDetailWidget(child);
            }
            else if (position == 2)
            {
                SetComposeWidget(child);
            }
            else
            {
                Logger.Instance?.Warning("CommunicationLayoutEngine", $"Invalid position {position}, all slots full");
                return;
            }

            layoutParams = layoutParams ?? new LayoutParams();
            layoutParams.Row = position;
            this.layoutParams[child] = layoutParams;
        }

        private int FindFirstEmptyPosition()
        {
            if (listWidget == null) return 0;
            if (detailWidget == null) return 1;
            if (composeWidget == null) return 2;
            return -1; // All full
        }

        public override void RemoveChild(UIElement child)
        {
            if (child == listWidget)
            {
                grid.Children.Remove(listWidget);
                children.Remove(listWidget);
                listWidget = null;
            }
            else if (child == detailWidget)
            {
                grid.Children.Remove(detailWidget);
                children.Remove(detailWidget);
                detailWidget = null;
            }
            else if (child == composeWidget)
            {
                grid.Children.Remove(composeWidget);
                children.Remove(composeWidget);
                composeWidget = null;
            }

            layoutParams.Remove(child);
        }

        public override void Clear()
        {
            if (listWidget != null)
            {
                grid.Children.Remove(listWidget);
                children.Remove(listWidget);
                listWidget = null;
            }

            if (detailWidget != null)
            {
                grid.Children.Remove(detailWidget);
                children.Remove(detailWidget);
                detailWidget = null;
            }

            if (composeWidget != null)
            {
                grid.Children.Remove(composeWidget);
                children.Remove(composeWidget);
                composeWidget = null;
            }

            layoutParams.Clear();
        }

        public override List<UIElement> GetChildren()
        {
            var result = new List<UIElement>();
            if (listWidget != null) result.Add(listWidget);
            if (detailWidget != null) result.Add(detailWidget);
            if (composeWidget != null) result.Add(composeWidget);
            return result;
        }

        /// <summary>
        /// Find position index for a widget
        /// </summary>
        private int FindPosition(UIElement widget)
        {
            if (widget == listWidget) return 0;
            if (widget == detailWidget) return 1;
            if (widget == composeWidget) return 2;
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
                Logger.Instance?.Warning("CommunicationLayoutEngine", "Cannot swap: one or both widgets not found");
                return;
            }

            // Remove both from grid
            grid.Children.Remove(widget1);
            grid.Children.Remove(widget2);

            // Swap references
            UIElement temp;
            if (pos1 == 0) listWidget = widget2;
            else if (pos1 == 1) detailWidget = widget2;
            else if (pos1 == 2) composeWidget = widget2;

            if (pos2 == 0) listWidget = widget1;
            else if (pos2 == 1) detailWidget = widget1;
            else if (pos2 == 2) composeWidget = widget1;

            // Re-add with new positions
            SetWidgetGridPosition(widget1, pos2);
            SetWidgetGridPosition(widget2, pos1);

            grid.Children.Add(widget1);
            grid.Children.Add(widget2);
        }

        private void SetWidgetGridPosition(UIElement widget, int position)
        {
            switch (position)
            {
                case 0: // List (left, full height)
                    Grid.SetColumn(widget, 0);
                    Grid.SetRow(widget, 0);
                    Grid.SetRowSpan(widget, 2);
                    break;

                case 1: // Detail (right-top)
                    Grid.SetColumn(widget, 1);
                    Grid.SetRow(widget, 0);
                    Grid.SetRowSpan(widget, 1);
                    break;

                case 2: // Compose (right-bottom)
                    Grid.SetColumn(widget, 1);
                    Grid.SetRow(widget, 1);
                    Grid.SetRowSpan(widget, 1);
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

            // Map directions:
            // Positions: 0=list, 1=detail (right-top), 2=compose (right-bottom)
            switch (direction)
            {
                case FocusDirection.Left:
                    // Detail or Compose -> List
                    if (fromPos == 1 || fromPos == 2)
                        return listWidget;
                    break;

                case FocusDirection.Right:
                    // List -> Detail
                    if (fromPos == 0)
                        return detailWidget;
                    break;

                case FocusDirection.Up:
                    // Compose -> Detail
                    if (fromPos == 2)
                        return detailWidget;
                    break;

                case FocusDirection.Down:
                    // Detail -> Compose
                    if (fromPos == 1)
                        return composeWidget;
                    // List -> Detail (no direct down, go right first)
                    if (fromPos == 0)
                        return detailWidget;
                    break;
            }

            return null;
        }
    }
}
