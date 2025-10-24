using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for widgets - enables testing and mocking
    /// </summary>
    public interface IWidget : INotifyPropertyChanged, IDisposable
    {
        string WidgetName { get; set; }
        string WidgetType { get; set; }
        Guid WidgetId { get; }
        bool HasFocus { get; set; }

        void Initialize();
        void Refresh();
        void OnActivated();
        void OnDeactivated();
        void OnWidgetKeyDown(KeyEventArgs e);
        void OnWidgetFocusReceived();
        void OnWidgetFocusLost();

        Dictionary<string, object> SaveState();
        void RestoreState(Dictionary<string, object> state);
    }
}
