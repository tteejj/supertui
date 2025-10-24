using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SuperTUI.Infrastructure
{
    // ============================================================================
    // THEME SYSTEM
    // ============================================================================

    /// <summary>
    /// Complete theme definition with all UI colors and styles
    /// </summary>
    public class Theme
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsDark { get; set; }

        // Primary colors
        public Color Primary { get; set; }
        public Color Secondary { get; set; }
        public Color Success { get; set; }
        public Color Warning { get; set; }
        public Color Error { get; set; }
        public Color Info { get; set; }

        // Background colors
        public Color Background { get; set; }
        public Color BackgroundSecondary { get; set; }
        public Color Surface { get; set; }
        public Color SurfaceHighlight { get; set; }

        // Text colors
        public Color Foreground { get; set; }
        public Color ForegroundSecondary { get; set; }
        public Color ForegroundDisabled { get; set; }

        // UI element colors
        public Color Border { get; set; }
        public Color BorderActive { get; set; }
        public Color Focus { get; set; }
        public Color Selection { get; set; }
        public Color Hover { get; set; }
        public Color Active { get; set; }

        // Syntax highlighting colors (for code editors)
        public Color SyntaxKeyword { get; set; }
        public Color SyntaxString { get; set; }
        public Color SyntaxComment { get; set; }
        public Color SyntaxNumber { get; set; }
        public Color SyntaxFunction { get; set; }
        public Color SyntaxVariable { get; set; }

        public static Theme CreateDarkTheme()
        {
            return new Theme
            {
                Name = "Dark",
                Description = "Dark theme with high contrast",
                IsDark = true,

                Primary = Color.FromRgb(0, 120, 212),        // #0078D4
                Secondary = Color.FromRgb(107, 107, 107),     // #6B6B6B
                Success = Color.FromRgb(16, 124, 16),         // #107C10
                Warning = Color.FromRgb(255, 140, 0),         // #FF8C00
                Error = Color.FromRgb(232, 17, 35),           // #E81123
                Info = Color.FromRgb(78, 201, 176),           // #4EC9B0

                Background = Color.FromRgb(12, 12, 12),       // #0C0C0C
                BackgroundSecondary = Color.FromRgb(26, 26, 26), // #1A1A1A
                Surface = Color.FromRgb(30, 30, 30),          // #1E1E1E
                SurfaceHighlight = Color.FromRgb(45, 45, 45), // #2D2D2D

                Foreground = Color.FromRgb(204, 204, 204),    // #CCCCCC
                ForegroundSecondary = Color.FromRgb(136, 136, 136), // #888888
                ForegroundDisabled = Color.FromRgb(102, 102, 102),  // #666666

                Border = Color.FromRgb(58, 58, 58),           // #3A3A3A
                BorderActive = Color.FromRgb(78, 201, 176),   // #4EC9B0
                Focus = Color.FromRgb(78, 201, 176),          // #4EC9B0
                Selection = Color.FromRgb(51, 153, 255),      // #3399FF
                Hover = Color.FromRgb(45, 45, 48),            // #2D2D30
                Active = Color.FromRgb(62, 62, 64),           // #3E3E40

                SyntaxKeyword = Color.FromRgb(86, 156, 214),  // #569CD6
                SyntaxString = Color.FromRgb(206, 145, 120),  // #CE9178
                SyntaxComment = Color.FromRgb(106, 153, 85),  // #6A9955
                SyntaxNumber = Color.FromRgb(181, 206, 168),  // #B5CEA8
                SyntaxFunction = Color.FromRgb(220, 220, 170), // #DCDCAA
                SyntaxVariable = Color.FromRgb(156, 220, 254)  // #9CDCFE
            };
        }

        public static Theme CreateLightTheme()
        {
            return new Theme
            {
                Name = "Light",
                Description = "Light theme with clean aesthetics",
                IsDark = false,

                Primary = Color.FromRgb(0, 120, 212),
                Secondary = Color.FromRgb(150, 150, 150),
                Success = Color.FromRgb(16, 124, 16),
                Warning = Color.FromRgb(255, 140, 0),
                Error = Color.FromRgb(232, 17, 35),
                Info = Color.FromRgb(0, 120, 212),

                Background = Color.FromRgb(255, 255, 255),
                BackgroundSecondary = Color.FromRgb(245, 245, 245),
                Surface = Color.FromRgb(250, 250, 250),
                SurfaceHighlight = Color.FromRgb(240, 240, 240),

                Foreground = Color.FromRgb(0, 0, 0),
                ForegroundSecondary = Color.FromRgb(96, 96, 96),
                ForegroundDisabled = Color.FromRgb(168, 168, 168),

                Border = Color.FromRgb(204, 204, 204),
                BorderActive = Color.FromRgb(0, 120, 212),
                Focus = Color.FromRgb(0, 120, 212),
                Selection = Color.FromRgb(51, 153, 255),
                Hover = Color.FromRgb(229, 229, 229),
                Active = Color.FromRgb(204, 204, 204),

                SyntaxKeyword = Color.FromRgb(0, 0, 255),
                SyntaxString = Color.FromRgb(163, 21, 21),
                SyntaxComment = Color.FromRgb(0, 128, 0),
                SyntaxNumber = Color.FromRgb(9, 134, 88),
                SyntaxFunction = Color.FromRgb(121, 94, 38),
                SyntaxVariable = Color.FromRgb(0, 16, 128)
            };
        }
    }

    /// <summary>
    /// Theme manager with hot-reloading support
    /// </summary>
    public class ThemeManager : IThemeManager
    {
        private static ThemeManager instance;
        public static ThemeManager Instance => instance ??= new ThemeManager();

        private readonly Dictionary<string, Theme> themes = new Dictionary<string, Theme>();
        private Theme currentTheme;
        private string themesDirectory;

        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        public Theme CurrentTheme => currentTheme;

        public void Initialize(string themesDir = null)
        {
            themesDirectory = themesDir ?? Path.Combine(
                SuperTUI.Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(), "Themes");

            // Register built-in themes
            RegisterTheme(Theme.CreateDarkTheme());
            RegisterTheme(Theme.CreateLightTheme());

            // Load custom themes
            LoadCustomThemes();

            // Apply saved theme from config
            string savedTheme = ConfigurationManager.Instance.Get<string>("UI.Theme", "Dark");
            ApplyTheme(savedTheme);
        }

        public void RegisterTheme(Theme theme)
        {
            themes[theme.Name] = theme;
            Logger.Instance.Debug("Theme", $"Registered theme: {theme.Name}");
        }

        public void ApplyTheme(string themeName)
        {
            if (!themes.TryGetValue(themeName, out var theme))
            {
                Logger.Instance.Warning("Theme", $"Theme not found: {themeName}, falling back to Dark");
                theme = themes["Dark"];
            }

            var oldTheme = currentTheme;
            currentTheme = theme;

            Logger.Instance.Info("Theme", $"Applied theme: {theme.Name}");

            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs { OldTheme = oldTheme, NewTheme = theme });
        }

        public List<Theme> GetAvailableThemes()
        {
            return themes.Values.ToList();
        }

        public void SaveTheme(Theme theme, string filename = null)
        {
            // Synchronous wrapper for backward compatibility
            SaveThemeAsync(theme, filename).GetAwaiter().GetResult();
        }

        public async Task SaveThemeAsync(Theme theme, string filename = null)
        {
            try
            {
                filename = filename ?? Path.Combine(themesDirectory, $"{theme.Name}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(filename));

                string json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filename, json, Encoding.UTF8);

                Logger.Instance.Info("Theme", $"Saved theme {theme.Name} to {filename}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Theme", $"Failed to save theme: {ex.Message}", ex);
            }
        }

        private void LoadCustomThemes()
        {
            // Synchronous wrapper for backward compatibility
            LoadCustomThemesAsync().GetAwaiter().GetResult();
        }

        private async Task LoadCustomThemesAsync()
        {
            if (!Directory.Exists(themesDirectory))
                return;

            try
            {
                foreach (var file in Directory.GetFiles(themesDirectory, "*.json"))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(file, Encoding.UTF8);
                        var theme = JsonSerializer.Deserialize<Theme>(json);
                        RegisterTheme(theme);
                        Logger.Instance.Info("Theme", $"Loaded custom theme: {theme.Name}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning("Theme", $"Failed to load theme from {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Theme", $"Failed to load custom themes: {ex.Message}", ex);
            }
        }
    }

    public class ThemeChangedEventArgs : EventArgs
    {
        public Theme OldTheme { get; set; }
        public Theme NewTheme { get; set; }
    }
}
