using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    /// <summary>
    /// Base class for all widgets - small, focused, self-contained components
    /// Each widget maintains its own state independently
    /// </summary>
    public abstract class WidgetBase : UserControl, IWidget
    {
        public string WidgetName { get; set; }
        public string WidgetType { get; set; }
        public Guid WidgetId { get; private set; } = Guid.NewGuid();

        // Focus management
        private bool hasFocus;
        public bool HasFocus
        {
            get => hasFocus;
            set
            {
                if (hasFocus != value)
                {
                    hasFocus = value;
                    OnPropertyChanged(nameof(HasFocus));
                    UpdateFocusVisual();

                    if (value)
                        OnWidgetFocusReceived();
                    else
                        OnWidgetFocusLost();
                }
            }
        }

        // Container wrapper for focus visual
        private Border containerBorder;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public WidgetBase()
        {
            // Wrap widget content in a border for focus indication
            this.Loaded += (s, e) => WrapInFocusBorder();
            this.Focusable = true;
            this.GotFocus += (s, e) => HasFocus = true;
            this.LostFocus += (s, e) => HasFocus = false;
        }

        private void WrapInFocusBorder()
        {
            if (this.Content != null && containerBorder == null)
            {
                var originalContent = this.Content;
                this.Content = null;

                containerBorder = new Border
                {
                    Child = originalContent as UIElement,
                    BorderThickness = new Thickness(2),
                    BorderBrush = Brushes.Transparent
                };

                this.Content = containerBorder;
                UpdateFocusVisual();
            }
        }

        private void UpdateFocusVisual()
        {
            if (containerBorder != null)
            {
                var theme = ThemeManager.Instance.CurrentTheme;
                containerBorder.BorderBrush = HasFocus
                    ? new SolidColorBrush(theme.Focus)
                    : Brushes.Transparent;
            }
        }

        /// <summary>
        /// Initialize widget - called once when widget is created
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Refresh widget data - can be called manually or on timer
        /// </summary>
        public virtual void Refresh() { }

        /// <summary>
        /// Called when widget becomes visible (workspace switched to)
        /// </summary>
        public virtual void OnActivated() { }

        /// <summary>
        /// Called when widget becomes hidden (workspace switched away)
        /// Widget state is preserved, just hidden
        /// </summary>
        public virtual void OnDeactivated() { }

        /// <summary>
        /// Handle keyboard input when widget has focus
        /// </summary>
        public virtual void OnWidgetKeyDown(KeyEventArgs e) { }

        /// <summary>
        /// Called when widget receives focus
        /// </summary>
        public virtual void OnWidgetFocusReceived() { }

        /// <summary>
        /// Called when widget loses focus
        /// </summary>
        public virtual void OnWidgetFocusLost() { }

        /// <summary>
        /// Save widget state (for persistence)
        /// </summary>
        public virtual Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["WidgetName"] = WidgetName,
                ["WidgetType"] = WidgetType,
                ["WidgetId"] = WidgetId
            };
        }

        /// <summary>
        /// Restore widget state (from persistence)
        /// </summary>
        public virtual void RestoreState(Dictionary<string, object> state)
        {
            // Override in derived classes to restore specific state
        }

        /// <summary>
        /// Dispose pattern implementation
        /// </summary>
        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    OnDispose();
                }

                disposed = true;
            }
        }

        /// <summary>
        /// Override in derived classes to dispose widget-specific resources
        /// (timers, event subscriptions, etc.)
        /// </summary>
        protected virtual void OnDispose()
        {
            // Override in derived classes
        }
    }
}
