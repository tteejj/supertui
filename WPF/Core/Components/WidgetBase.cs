using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Infrastructure;
using SuperTUI.Core.Effects;
using System.Windows.Threading;

namespace SuperTUI.Core
{
    /// <summary>
    /// Widget input modes for terminal-like keyboard handling
    /// </summary>
    public enum WidgetInputMode
    {
        /// <summary>Normal mode - navigation and commands</summary>
        Normal,
        /// <summary>Insert/Edit mode - text input active</summary>
        Insert,
        /// <summary>Command mode - accepting command input</summary>
        Command
    }

    /// <summary>
    /// Base class for all widgets - small, focused, self-contained components
    /// Each widget maintains its own state independently
    /// </summary>
    public abstract class WidgetBase : UserControl, IWidget
    {
        public string WidgetName { get; set; }
        public string WidgetType { get; set; }
        public Guid WidgetId { get; private set; } = Guid.NewGuid();

        // Infrastructure helpers for easy access
        protected IEventBus EventBus => SuperTUI.Core.EventBus.Instance;
        protected ApplicationContext AppContext => ApplicationContext.Instance;

        // Input mode management (terminal-like)
        private WidgetInputMode inputMode = WidgetInputMode.Normal;
        public WidgetInputMode InputMode
        {
            get => inputMode;
            protected set
            {
                if (inputMode != value)
                {
                    inputMode = value;
                    OnPropertyChanged(nameof(InputMode));
                    OnInputModeChanged(value);
                }
            }
        }

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

            // Subscribe to theme changes using WeakEventManager to prevent memory leaks
            // This prevents the static ThemeManager from holding strong references to widget instances
            if (this is IThemeable)
            {
                ThemeChangedWeakEventManager.AddHandler(ThemeManager.Instance, OnThemeChanged);
            }
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

                if (HasFocus)
                {
                    // 3px colored border for better visibility
                    containerBorder.BorderBrush = new SolidColorBrush(theme.Focus);
                    containerBorder.BorderThickness = new Thickness(3);

                    // Apply glow effect from theme settings
                    if (theme.Glow != null)
                    {
                        GlowEffectHelper.ApplyGlow(containerBorder, theme.Glow, GlowState.Focus);
                    }
                }
                else
                {
                    // 1px subtle border
                    containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
                    containerBorder.BorderThickness = new Thickness(1);

                    // Apply always-on glow or remove glow based on theme settings
                    if (theme.Glow != null && theme.Glow.Mode == GlowMode.Always)
                    {
                        GlowEffectHelper.ApplyGlow(containerBorder, theme.Glow, GlowState.Normal);
                    }
                    else
                    {
                        GlowEffectHelper.RemoveGlow(containerBorder);
                    }
                }
            }
        }

        /// <summary>
        /// Handler for theme changes - calls ApplyTheme() if widget implements IThemeable
        /// </summary>
        private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (this is IThemeable themeable)
            {
                themeable.ApplyTheme();
            }

            // Always update focus visual (uses theme colors)
            UpdateFocusVisual();
        }

        /// <summary>
        /// Apply typography settings from theme to this widget
        /// Call this in Initialize() or ApplyTheme() to use themed fonts
        /// </summary>
        protected void ApplyTypography()
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            if (theme?.Typography == null) return;

            // Check for widget-specific font override
            if (theme.Typography.PerWidgetFonts != null &&
                theme.Typography.PerWidgetFonts.TryGetValue(WidgetType ?? WidgetName, out var widgetFont))
            {
                FontFamily = new FontFamily(widgetFont.FontFamily);
                FontSize = widgetFont.FontSize;
                FontWeight = ParseFontWeight(widgetFont.FontWeight);
            }
            else
            {
                // Apply global typography settings
                FontFamily = new FontFamily(theme.Typography.FontFamily);
                FontSize = theme.Typography.FontSize;
                FontWeight = ParseFontWeight(theme.Typography.FontWeight);
            }
        }

        /// <summary>
        /// Parse font weight string to FontWeight
        /// </summary>
        private FontWeight ParseFontWeight(string weight)
        {
            return weight?.ToLower() switch
            {
                "thin" => FontWeights.Thin,
                "extralight" => FontWeights.ExtraLight,
                "light" => FontWeights.Light,
                "normal" => FontWeights.Normal,
                "medium" => FontWeights.Medium,
                "semibold" => FontWeights.SemiBold,
                "bold" => FontWeights.Bold,
                "extrabold" => FontWeights.ExtraBold,
                "black" => FontWeights.Black,
                _ => FontWeights.Normal
            };
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
        /// Called when input mode changes (Normal/Insert/Command)
        /// Override to handle mode-specific behavior
        /// </summary>
        protected virtual void OnInputModeChanged(WidgetInputMode newMode) { }

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
            // Unsubscribe from theme changes using WeakEventManager
            // Note: With weak references, this is technically optional (GC will clean up)
            // but we do it anyway for completeness and to free resources immediately
            if (this is IThemeable)
            {
                ThemeChangedWeakEventManager.RemoveHandler(ThemeManager.Instance, OnThemeChanged);
            }

            // Override in derived classes
        }
    }
}
