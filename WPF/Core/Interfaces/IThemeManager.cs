using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for theme management - enables testing and mocking
    /// </summary>
    public interface IThemeManager
    {
        Theme CurrentTheme { get; }
        event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        void Initialize(string themesDirectory = null);
        void ApplyTheme(string themeName);
        void RegisterTheme(Theme theme);
        void SaveTheme(Theme theme, string filename = null);

        List<Theme> GetAvailableThemes();
    }
}
