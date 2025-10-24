using System;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for components that respond to theme changes
    /// Widgets implementing this interface will automatically be notified when theme changes
    /// </summary>
    public interface IThemeable
    {
        /// <summary>
        /// Called when the theme changes - widget should update all theme-dependent colors and styles
        /// </summary>
        void ApplyTheme();
    }
}
