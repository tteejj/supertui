using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for layout engines - enables testing and mocking
    /// </summary>
    public interface ILayoutEngine
    {
        Panel Container { get; }

        void AddChild(UIElement child, LayoutParams layoutParams);
        void RemoveChild(UIElement child);
        void Clear();
        List<UIElement> GetChildren();
    }
}
