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

        void Initialize(string themesDirectory);
        void ApplyTheme(string themeName);
        void RegisterTheme(Theme theme);
        void SaveTheme(Theme theme);

        List<Theme> GetAvailableThemes();
        Theme LoadThemeFromFile(string filePath);
    }
}
