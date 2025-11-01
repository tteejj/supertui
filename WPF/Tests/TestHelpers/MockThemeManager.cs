using System;
using System.Collections.Generic;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.TestHelpers
{
    /// <summary>
    /// Mock ThemeManager for tests - provides valid theme data without UI thread requirements.
    /// Does NOT apply themes to UI elements, avoiding cross-thread exceptions in tests.
    /// </summary>
    public class MockThemeManager : IThemeManager
    {
        private Theme currentTheme;
        private readonly Dictionary<string, Theme> themes = new Dictionary<string, Theme>();

        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        public Theme CurrentTheme => currentTheme;

        public void Initialize(string themesDirectory = null)
        {
            // Register built-in themes (same as real ThemeManager)
            RegisterTheme(Theme.CreateDarkTheme());
            RegisterTheme(Theme.CreateLightTheme());
            RegisterTheme(Theme.CreateTerminalTheme());

            // Set default theme WITHOUT triggering UI updates
            currentTheme = themes["Terminal"];

            // NOTE: We do NOT call ApplyTheme() to avoid UI thread issues in tests
        }

        public void RegisterTheme(Theme theme)
        {
            themes[theme.Name] = theme;
        }

        public void ApplyTheme(string themeName)
        {
            if (!themes.TryGetValue(themeName, out var theme))
            {
                theme = themes["Dark"];
            }

            var oldTheme = currentTheme;
            currentTheme = theme;

            // NOTE: We do NOT raise ThemeChanged event to avoid triggering
            // PaneBase.ApplyTheme() which requires UI thread
            // This is intentional for tests - UI rendering is tested separately
        }

        public List<Theme> GetAvailableThemes()
        {
            return new List<Theme>(themes.Values);
        }

        public void SaveTheme(Theme theme, string filename = null)
        {
            // No-op for tests - we don't need to persist themes during testing
        }
    }
}
