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
    public class DockLayoutEngine : LayoutEngine
    {
        private DockPanel dockPanel;

        public DockLayoutEngine()
        {
            dockPanel = new DockPanel();
            dockPanel.LastChildFill = true;
            Container = dockPanel;
        }

        public override void AddChild(UIElement child, LayoutParams lp)
        {
            if (lp.Dock.HasValue)
                DockPanel.SetDock(child, lp.Dock.Value);

            ApplyCommonParams(child, lp);
            children.Add(child);
            layoutParams[child] = lp;
            dockPanel.Children.Add(child);
        }

        public override void RemoveChild(UIElement child)
        {
            dockPanel.Children.Remove(child);
            children.Remove(child);
            layoutParams.Remove(child);
        }

        public override void Clear()
        {
            dockPanel.Children.Clear();
            children.Clear();
            layoutParams.Clear();
        }
    }

    /// <summary>
    /// Stack-based layout engine
}
