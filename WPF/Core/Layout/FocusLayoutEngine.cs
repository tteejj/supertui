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
    /// Specialized 2-pane focus layout for distraction-free work.
    /// Optimized for: Main content area (80%) + thin sidebar (20%) for quick reference/todos
    /// Layout:
    /// ┌──────────────┬──┐
    /// │              │T │
    /// │     Main     │o │
    /// │     80%      │d│
    /// │              │o │
    /// │              │  │
    /// └──────────────┴──┘
    /// </summary>
    public class FocusLayoutEngine : LayoutEngine
    {
        private Grid grid;
        private UIElement mainWidget = null;
        private UIElement sidebarWidget = null;

        public FocusLayoutEngine()
        {
            grid = new Grid();
            Container = grid;

            // 2 columns: 80% main | 20% sidebar
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(0.8, GridUnitType.Star),
                MinWidth = 400
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(0.2, GridUnitType.Star),
                MinWidth = 150,
                MaxWidth = 300
            });

            // Single row
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

            // Add vertical splitter
            AddGridSplitter();
        }

        private void AddGridSplitter()
        {
            var theme = ThemeManager.Instance?.CurrentTheme ?? Theme.CreateDarkTheme();
            var splitterBrush = new SolidColorBrush(theme.Border);

            var splitter = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = splitterBrush,
                ResizeDirection = GridResizeDirection.Columns
            };
            Grid.SetColumn(splitter, 0);
            Grid.SetRow(splitter, 0);
            grid.Children.Add(splitter);
        }

        /// <summary>
        /// Set main widget (80% area)
        /// </summary>
        public void SetMainWidget(UIElement widget)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            // Remove existing main widget
            if (mainWidget != null)
            {
                grid.Children.Remove(mainWidget);
                children.Remove(mainWidget);
            }

            mainWidget = widget;

            // Apply margin
            if (widget is FrameworkElement fe)
            {
                fe.Margin = new Thickness(5);
            }

            // Position in grid
            Grid.SetColumn(widget, 0);
            Grid.SetRow(widget, 0);

            grid.Children.Add(widget);

            if (!children.Contains(widget))
            {
                children.Add(widget);
            }
        }

        /// <summary>
        /// Set sidebar widget (20% area)
        /// </summary>
        public void SetSidebarWidget(UIElement widget)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            // Remove existing sidebar widget
            if (sidebarWidget != null)
            {
                grid.Children.Remove(sidebarWidget);
                children.Remove(sidebarWidget);
            }

            sidebarWidget = widget;

            // Apply margin
            if (widget is FrameworkElement fe)
            {
                fe.Margin = new Thickness(5);
            }

            // Position in grid
            Grid.SetColumn(widget, 1);
            Grid.SetRow(widget, 0);

            grid.Children.Add(widget);

            if (!children.Contains(widget))
            {
                children.Add(widget);
            }
        }

        public UIElement GetMainWidget() => mainWidget;
        public UIElement GetSidebarWidget() => sidebarWidget;

        public override void AddChild(UIElement child, LayoutParams layoutParams)
        {
            // For compatibility: Column 0 = main, Column 1 = sidebar
            int position = layoutParams?.Column ?? (mainWidget == null ? 0 : 1);

            if (position == 0)
            {
                SetMainWidget(child);
            }
            else if (position == 1)
            {
                SetSidebarWidget(child);
            }
            else
            {
                Logger.Instance?.Warning("FocusLayoutEngine", $"Invalid position {position}, using sidebar");
                SetSidebarWidget(child);
            }

            layoutParams = layoutParams ?? new LayoutParams();
            layoutParams.Column = position;
            this.layoutParams[child] = layoutParams;
        }

        public override void RemoveChild(UIElement child)
        {
            if (child == mainWidget)
            {
                grid.Children.Remove(mainWidget);
                children.Remove(mainWidget);
                mainWidget = null;
            }
            else if (child == sidebarWidget)
            {
                grid.Children.Remove(sidebarWidget);
                children.Remove(sidebarWidget);
                sidebarWidget = null;
            }

            layoutParams.Remove(child);
        }

        public override void Clear()
        {
            if (mainWidget != null)
            {
                grid.Children.Remove(mainWidget);
                children.Remove(mainWidget);
                mainWidget = null;
            }

            if (sidebarWidget != null)
            {
                grid.Children.Remove(sidebarWidget);
                children.Remove(sidebarWidget);
                sidebarWidget = null;
            }

            layoutParams.Clear();
        }

        public override List<UIElement> GetChildren()
        {
            var result = new List<UIElement>();
            if (mainWidget != null) result.Add(mainWidget);
            if (sidebarWidget != null) result.Add(sidebarWidget);
            return result;
        }

        /// <summary>
        /// Swap main and sidebar widgets
        /// </summary>
        public void SwapWidgets(UIElement widget1, UIElement widget2)
        {
            if (widget1 == null || widget2 == null)
                return;

            if ((widget1 != mainWidget && widget1 != sidebarWidget) ||
                (widget2 != mainWidget && widget2 != sidebarWidget))
            {
                Logger.Instance?.Warning("FocusLayoutEngine", "Cannot swap: one or both widgets not found");
                return;
            }

            // Remove both
            grid.Children.Remove(widget1);
            grid.Children.Remove(widget2);

            // Swap references
            if (widget1 == mainWidget)
            {
                mainWidget = widget2;
                sidebarWidget = widget1;
            }
            else
            {
                mainWidget = widget1;
                sidebarWidget = widget2;
            }

            // Re-add with new positions
            Grid.SetColumn(mainWidget, 0);
            Grid.SetRow(mainWidget, 0);
            grid.Children.Add(mainWidget);

            Grid.SetColumn(sidebarWidget, 1);
            Grid.SetRow(sidebarWidget, 0);
            grid.Children.Add(sidebarWidget);
        }

        /// <summary>
        /// Find widget in a direction from the given widget
        /// </summary>
        public UIElement FindWidgetInDirection(UIElement fromWidget, FocusDirection direction)
        {
            if (fromWidget == null)
                return null;

            // Simple 2-widget layout
            switch (direction)
            {
                case FocusDirection.Left:
                    // Sidebar -> Main
                    return (fromWidget == sidebarWidget) ? mainWidget : null;

                case FocusDirection.Right:
                    // Main -> Sidebar
                    return (fromWidget == mainWidget) ? sidebarWidget : null;

                case FocusDirection.Up:
                case FocusDirection.Down:
                    // No vertical navigation in single-row layout
                    return null;

                default:
                    return null;
            }
        }
    }
}
