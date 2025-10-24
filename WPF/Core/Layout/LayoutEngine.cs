using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SuperTUI.Core
{
    /// <summary>
    /// Size constraint types
    /// </summary>
    public enum SizeMode
    {
        Auto,           // Size to content
        Star,           // Proportional (e.g., 1*, 2*)
        Pixels,         // Fixed pixels
        Percentage      // Percentage of available space
    }

    /// <summary>
    /// Layout parameters for positioning widgets/screens
    /// </summary>
    public class LayoutParams
    {
        // Grid layout
        public int? Row { get; set; }
        public int? Column { get; set; }
        public int? RowSpan { get; set; } = 1;
        public int? ColumnSpan { get; set; } = 1;

        // Dock layout
        public Dock? Dock { get; set; }

        // Size constraints
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? MinWidth { get; set; }
        public double? MinHeight { get; set; }
        public double? MaxWidth { get; set; }
        public double? MaxHeight { get; set; }

        // Proportional sizing (for Grid)
        public double StarWidth { get; set; } = 1.0;  // e.g., 2.0 = 2*, 0.5 = 0.5*
        public double StarHeight { get; set; } = 1.0;

        // Margin
        public Thickness? Margin { get; set; }

        // Alignment
        public HorizontalAlignment? HorizontalAlignment { get; set; }
        public VerticalAlignment? VerticalAlignment { get; set; }
    }

    /// <summary>
    /// Base class for layout engines
    /// </summary>
    public abstract class LayoutEngine
    {
        public Panel Container { get; protected set; }
        protected List<UIElement> children = new List<UIElement>();
        protected Dictionary<UIElement, LayoutParams> layoutParams = new Dictionary<UIElement, LayoutParams>();

        public abstract void AddChild(UIElement child, LayoutParams layoutParams);
        public abstract void RemoveChild(UIElement child);
        public abstract void Clear();

        public virtual List<UIElement> GetChildren() => new List<UIElement>(children);

        protected void ApplyCommonParams(UIElement child, LayoutParams lp)
        {
            if (child is FrameworkElement fe)
            {
                // Size
                if (lp.Width.HasValue)
                    fe.Width = lp.Width.Value;

                if (lp.Height.HasValue)
                    fe.Height = lp.Height.Value;

                // Min/Max size
                if (lp.MinWidth.HasValue)
                    fe.MinWidth = lp.MinWidth.Value;

                if (lp.MinHeight.HasValue)
                    fe.MinHeight = lp.MinHeight.Value;

                if (lp.MaxWidth.HasValue)
                    fe.MaxWidth = lp.MaxWidth.Value;

                if (lp.MaxHeight.HasValue)
                    fe.MaxHeight = lp.MaxHeight.Value;

                // Margin
                if (lp.Margin.HasValue)
                    fe.Margin = lp.Margin.Value;
                else
                    fe.Margin = new Thickness(5); // Default margin

                // Alignment
                if (lp.HorizontalAlignment.HasValue)
                    fe.HorizontalAlignment = lp.HorizontalAlignment.Value;
                else
                    fe.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;

                if (lp.VerticalAlignment.HasValue)
                    fe.VerticalAlignment = lp.VerticalAlignment.Value;
                else
                    fe.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            }
        }
    }
}
