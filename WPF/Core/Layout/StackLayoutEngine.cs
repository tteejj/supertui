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
    public class StackLayoutEngine : LayoutEngine
    {
        private StackPanel stackPanel;

        public StackLayoutEngine(Orientation orientation = Orientation.Vertical)
        {
            stackPanel = new StackPanel();
            stackPanel.Orientation = orientation;
            Container = stackPanel;
        }

        public override void AddChild(UIElement child, LayoutParams lp)
        {
            ApplyCommonParams(child, lp);
            children.Add(child);
            layoutParams[child] = lp;
            stackPanel.Children.Add(child);
        }

        public override void RemoveChild(UIElement child)
        {
            stackPanel.Children.Remove(child);
            children.Remove(child);
            layoutParams.Remove(child);
        }

        public override void Clear()
        {
            stackPanel.Children.Clear();
            children.Clear();
            layoutParams.Clear();
        }
    }

    // ============================================================================
    // WORKSPACE SYSTEM
    // ============================================================================

    /// <summary>
    /// Represents a workspace (desktop) containing widgets and screens
    /// Each workspace maintains independent state for all its widgets/screens
}
