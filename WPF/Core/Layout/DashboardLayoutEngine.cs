using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SuperTUI.Core
{
    /// <summary>
    /// Flexible 2x2 dashboard layout (i3-inspired).
    /// Up to 4 slots, any widget in any position, runtime add/remove.
    /// </summary>
    public class DashboardLayoutEngine : LayoutEngine
    {
        private Grid grid;
        private Border[] slots = new Border[4]; // 4 slots: [0]=TL, [1]=TR, [2]=BL, [3]=BR
        private UIElement[] widgets = new UIElement[4]; // Current widget in each slot

        public DashboardLayoutEngine()
        {
            grid = new Grid();
            Container = grid; // Set base class Container property

            // 2x2 grid
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Create empty slots
            for (int i = 0; i < 4; i++)
            {
                slots[i] = CreateEmptySlot(i);
                int row = i / 2;
                int col = i % 2;
                Grid.SetRow(slots[i], row);
                Grid.SetColumn(slots[i], col);
                grid.Children.Add(slots[i]);
            }
        }

        private Border CreateEmptySlot(int slotIndex)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5)
            };

            var textBlock = new TextBlock
            {
                Text = $"Empty Slot {slotIndex + 1}\n\nPress Ctrl+{slotIndex + 1} to add widget",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            border.Child = textBlock;
            return border;
        }

        /// <summary>
        /// Add widget to specific slot (0-3).
        /// </summary>
        public void SetWidget(int slotIndex, UIElement widget)
        {
            if (slotIndex < 0 || slotIndex >= 4)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Slot must be 0-3");

            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            // Remove existing widget from this slot
            if (widgets[slotIndex] != null)
            {
                grid.Children.Remove(widgets[slotIndex]);
            }

            // Remove empty slot placeholder
            if (slots[slotIndex] != null && grid.Children.Contains(slots[slotIndex]))
            {
                grid.Children.Remove(slots[slotIndex]);
            }

            // Add new widget
            widgets[slotIndex] = widget;
            int row = slotIndex / 2;
            int col = slotIndex % 2;
            Grid.SetRow(widget, row);
            Grid.SetColumn(widget, col);

            // Add margin
            if (widget is FrameworkElement fe)
            {
                fe.Margin = new Thickness(5);
            }

            grid.Children.Add(widget);
        }

        /// <summary>
        /// Remove widget from slot, show empty placeholder.
        /// </summary>
        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 4)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Slot must be 0-3");

            // Remove widget if exists
            if (widgets[slotIndex] != null)
            {
                grid.Children.Remove(widgets[slotIndex]);
                widgets[slotIndex] = null;
            }

            // Show empty slot placeholder
            if (!grid.Children.Contains(slots[slotIndex]))
            {
                grid.Children.Add(slots[slotIndex]);
            }
        }

        /// <summary>
        /// Get widget currently in slot (or null if empty).
        /// </summary>
        public UIElement GetWidget(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 4)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            return widgets[slotIndex];
        }

        /// <summary>
        /// Get all non-null widgets.
        /// </summary>
        public IEnumerable<UIElement> GetAllWidgets()
        {
            return widgets.Where(w => w != null);
        }

        public override void AddChild(UIElement child, LayoutParams layoutParams)
        {
            // For compatibility with existing API
            // Default: add to first empty slot
            int slotIndex = layoutParams?.Row ?? FindFirstEmptySlot();
            if (slotIndex >= 0 && slotIndex < 4)
            {
                SetWidget(slotIndex, child);
                if (!children.Contains(child))
                {
                    children.Add(child);
                }
            }
        }

        private int FindFirstEmptySlot()
        {
            for (int i = 0; i < 4; i++)
            {
                if (widgets[i] == null)
                    return i;
            }
            return -1; // All slots full
        }

        public override void RemoveChild(UIElement child)
        {
            for (int i = 0; i < 4; i++)
            {
                if (widgets[i] == child)
                {
                    ClearSlot(i);
                    children.Remove(child);
                    return;
                }
            }
        }

        public override void Clear()
        {
            for (int i = 0; i < 4; i++)
            {
                ClearSlot(i);
            }
            children.Clear();
        }

        public override List<UIElement> GetChildren()
        {
            return widgets.Where(w => w != null).ToList();
        }

        /// <summary>
        /// Swap two widgets between slots
        /// </summary>
        public void SwapWidgets(int slot1, int slot2)
        {
            if (slot1 < 0 || slot1 >= 4 || slot2 < 0 || slot2 >= 4)
                throw new ArgumentOutOfRangeException("Slots must be 0-3");

            var widget1 = widgets[slot1];
            var widget2 = widgets[slot2];

            // Remove both from grid
            if (widget1 != null) grid.Children.Remove(widget1);
            if (widget2 != null) grid.Children.Remove(widget2);

            // Swap in array
            widgets[slot1] = widget2;
            widgets[slot2] = widget1;

            // Re-add at new positions
            if (widget1 != null)
            {
                int row = slot2 / 2;
                int col = slot2 % 2;
                Grid.SetRow(widget1, row);
                Grid.SetColumn(widget1, col);
                grid.Children.Add(widget1);
            }
            else
            {
                // Show empty slot at slot2
                if (!grid.Children.Contains(slots[slot2]))
                {
                    grid.Children.Add(slots[slot2]);
                }
            }

            if (widget2 != null)
            {
                int row = slot1 / 2;
                int col = slot1 % 2;
                Grid.SetRow(widget2, row);
                Grid.SetColumn(widget2, col);
                grid.Children.Add(widget2);
            }
            else
            {
                // Show empty slot at slot1
                if (!grid.Children.Contains(slots[slot1]))
                {
                    grid.Children.Add(slots[slot1]);
                }
            }
        }

        /// <summary>
        /// Find which slot contains the given widget
        /// </summary>
        public int FindSlotIndex(UIElement widget)
        {
            for (int i = 0; i < 4; i++)
            {
                if (widgets[i] == widget)
                    return i;
            }
            return -1;
        }
    }
}
