using System;
using System.Windows;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// WeakEventManager for ThemeChanged events to prevent memory leaks
    ///
    /// PROBLEM:
    /// ThemeManager is a singleton with a static ThemeChanged event. When widgets subscribe directly:
    ///   ThemeManager.Instance.ThemeChanged += OnThemeChanged;
    ///
    /// The static ThemeManager holds strong references to all subscribed widgets, preventing
    /// garbage collection even after the widget is no longer in use. This causes memory leaks.
    ///
    /// SOLUTION:
    /// WeakEventManager uses weak references internally, allowing widgets to be garbage collected
    /// when they're no longer referenced elsewhere. The weak reference allows GC to reclaim the
    /// widget's memory while the static ThemeManager is still alive.
    ///
    /// USAGE:
    /// Instead of:  ThemeManager.Instance.ThemeChanged += OnThemeChanged;
    /// Use:         ThemeChangedWeakEventManager.AddHandler(ThemeManager.Instance, OnThemeChanged);
    ///
    /// Cleanup (optional but recommended):
    ///              ThemeChangedWeakEventManager.RemoveHandler(ThemeManager.Instance, OnThemeChanged);
    /// </summary>
    public class ThemeChangedWeakEventManager : WeakEventManager
    {
        private ThemeChangedWeakEventManager() { }

        /// <summary>
        /// Add a weak event handler for ThemeChanged events
        /// </summary>
        public static void AddHandler(IThemeManager source, EventHandler<ThemeChangedEventArgs> handler)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            CurrentManager.ProtectedAddHandler(source, handler);
        }

        /// <summary>
        /// Remove a weak event handler for ThemeChanged events
        /// </summary>
        public static void RemoveHandler(IThemeManager source, EventHandler<ThemeChangedEventArgs> handler)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            CurrentManager.ProtectedRemoveHandler(source, handler);
        }

        /// <summary>
        /// Start listening to ThemeChanged events on the source
        /// </summary>
        protected override void StartListening(object source)
        {
            if (source is IThemeManager themeManager)
            {
                themeManager.ThemeChanged += DeliverEvent;
            }
        }

        /// <summary>
        /// Stop listening to ThemeChanged events on the source
        /// </summary>
        protected override void StopListening(object source)
        {
            if (source is IThemeManager themeManager)
            {
                themeManager.ThemeChanged -= DeliverEvent;
            }
        }

        /// <summary>
        /// Get the current manager instance (creates one if needed)
        /// </summary>
        private static ThemeChangedWeakEventManager CurrentManager
        {
            get
            {
                var managerType = typeof(ThemeChangedWeakEventManager);
                var manager = (ThemeChangedWeakEventManager)GetCurrentManager(managerType);
                if (manager == null)
                {
                    manager = new ThemeChangedWeakEventManager();
                    SetCurrentManager(managerType, manager);
                }
                return manager;
            }
        }
    }
}
