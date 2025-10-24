using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for workspaces - enables testing and mocking
    /// </summary>
    public interface IWorkspace
    {
        string Name { get; set; }
        int Index { get; set; }
        bool IsActive { get; set; }
        ObservableCollection<WidgetBase> Widgets { get; }
        Panel Container { get; }

        void AddWidget(WidgetBase widget, LayoutParams layoutParams);
        void RemoveWidget(WidgetBase widget);
        void ClearWidgets();
        void Activate();
        void Deactivate();
        bool HandleKeyDown(System.Windows.Input.KeyEventArgs e);
    }
}
