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
    /// Glow effect mode for UI elements
    /// </summary>
    public enum GlowMode
    {
        Always,
        OnFocus,
        OnHover,
        Never
    }

    /// <summary>
    /// Glow effect settings for widgets and UI elements
    /// </summary>
    public class GlowSettings
    {
        public GlowMode Mode { get; set; } = GlowMode.OnFocus;
        public Color GlowColor { get; set; } = Color.FromRgb(78, 201, 176);
        public double GlowRadius { get; set; } = 10.0;
        public double GlowOpacity { get; set; } = 0.8;
        public Color FocusGlowColor { get; set; } = Color.FromRgb(78, 201, 176);
        public Color HoverGlowColor { get; set; } = Color.FromRgb(51, 153, 255);
    }

    /// <summary>
    /// CRT/retro terminal effect settings
    /// </summary>
    public class CRTEffectSettings
    {
        public bool EnableScanlines { get; set; } = false;
        public double ScanlineOpacity { get; set; } = 0.1;
        public int ScanlineSpacing { get; set; } = 2;
        public Color ScanlineColor { get; set; } = Colors.Black;
        public bool EnableBloom { get; set; } = false;
        public double BloomIntensity { get; set; } = 0.3;
    }

    /// <summary>
    /// Opacity settings for various UI elements
    /// </summary>
    public class OpacitySettings
    {
        public double WindowOpacity { get; set; } = 1.0;
        public double BackgroundOpacity { get; set; } = 1.0;
        public double InactiveWidgetOpacity { get; set; } = 0.7;
    }

    /// <summary>
    /// Per-widget font settings
    /// </summary>
    public class FontSettings
    {
        public string FontFamily { get; set; } = "Consolas";
        public double FontSize { get; set; } = 12.0;
        public string FontWeight { get; set; } = "Normal";
    }

    /// <summary>
    /// Typography settings for the theme
    /// </summary>
    public class TypographySettings
    {
        public string FontFamily { get; set; } = "Consolas";
        public double FontSize { get; set; } = 12.0;
        public string FontWeight { get; set; } = "Normal";
        public Dictionary<string, FontSettings> PerWidgetFonts { get; set; } = new Dictionary<string, FontSettings>();
    }

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

        // Enhanced theme features
        public GlowSettings Glow { get; set; } = new GlowSettings();
        public CRTEffectSettings CRTEffects { get; set; } = new CRTEffectSettings();
        public OpacitySettings Opacity { get; set; } = new OpacitySettings();
        public TypographySettings Typography { get; set; } = new TypographySettings();
        public Dictionary<string, Color> ColorOverrides { get; set; } = new Dictionary<string, Color>();

        /// <summary>
        /// Helper method to get a color by name with override support
        /// </summary>
        public Color GetColor(string colorName)
        {
            if (ColorOverrides != null && ColorOverrides.TryGetValue(colorName, out var overrideColor))
                return overrideColor;

            // Use reflection to get the color property
            var prop = GetType().GetProperty(colorName);
            if (prop != null && prop.PropertyType == typeof(Color))
                return (Color)prop.GetValue(this);

            return Foreground; // Default fallback
        }

        /// <summary>
        /// Helper method to get background color with override support
        /// </summary>
        public Color GetBgColor(string colorName = "Background")
        {
            if (ColorOverrides != null && ColorOverrides.TryGetValue(colorName, out var overrideColor))
                return overrideColor;

            switch (colorName)
            {
                case "Background": return Background;
                case "BackgroundSecondary": return BackgroundSecondary;
                case "Surface": return Surface;
                case "SurfaceHighlight": return SurfaceHighlight;
                default: return Background;
            }
        }

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

        /// <summary>
        /// Creates an amber terminal theme - classic retro terminal look
        /// Amber phosphor on black with warm orange glow
        /// </summary>
        public static Theme CreateAmberTerminalTheme()
        {
            var amber = Color.FromRgb(255, 176, 0);      // #FFB000 - Amber phosphor
            var amberDim = Color.FromRgb(204, 140, 0);   // Dimmed amber
            var amberBright = Color.FromRgb(255, 200, 64); // Bright amber

            return new Theme
            {
                Name = "Amber Terminal",
                Description = "Classic amber phosphor terminal with warm glow",
                IsDark = true,

                Primary = amber,
                Secondary = amberDim,
                Success = amberBright,
                Warning = Color.FromRgb(255, 140, 0),
                Error = Color.FromRgb(255, 100, 0),
                Info = amber,

                Background = Color.FromRgb(0, 0, 0),
                BackgroundSecondary = Color.FromRgb(10, 8, 0),
                Surface = Color.FromRgb(15, 12, 0),
                SurfaceHighlight = Color.FromRgb(20, 16, 0),

                Foreground = amber,
                ForegroundSecondary = amberDim,
                ForegroundDisabled = Color.FromRgb(102, 70, 0),

                Border = amberDim,
                BorderActive = amberBright,
                Focus = amberBright,
                Selection = Color.FromArgb(100, 255, 176, 0),
                Hover = Color.FromRgb(20, 16, 0),
                Active = Color.FromRgb(30, 24, 0),

                SyntaxKeyword = amberBright,
                SyntaxString = amber,
                SyntaxComment = amberDim,
                SyntaxNumber = amberBright,
                SyntaxFunction = amber,
                SyntaxVariable = amber,

                Glow = new GlowSettings
                {
                    Mode = GlowMode.Always,
                    GlowColor = Color.FromRgb(255, 140, 0),
                    GlowRadius = 12.0,
                    GlowOpacity = 0.6,
                    FocusGlowColor = Color.FromRgb(255, 176, 0),
                    HoverGlowColor = Color.FromRgb(255, 200, 64)
                },
                CRTEffects = new CRTEffectSettings
                {
                    EnableScanlines = true,
                    ScanlineOpacity = 0.08,
                    ScanlineSpacing = 2,
                    ScanlineColor = Colors.Black,
                    EnableBloom = true,
                    BloomIntensity = 0.4
                },
                Opacity = new OpacitySettings
                {
                    WindowOpacity = 0.98,
                    BackgroundOpacity = 1.0,
                    InactiveWidgetOpacity = 0.7
                },
                Typography = new TypographySettings
                {
                    FontFamily = "Consolas",
                    FontSize = 12.0,
                    FontWeight = "Normal"
                }
            };
        }

        /// <summary>
        /// Creates a Matrix theme - green phosphor terminal
        /// Classic "Matrix" aesthetic with green glow and scanlines
        /// </summary>
        public static Theme CreateMatrixTheme()
        {
            var matrixGreen = Color.FromRgb(0, 255, 65);      // #00FF41 - Bright green
            var matrixDim = Color.FromRgb(0, 180, 45);        // Dimmed green
            var matrixDark = Color.FromRgb(0, 100, 25);       // Dark green

            return new Theme
            {
                Name = "Matrix",
                Description = "Green phosphor terminal with scanlines",
                IsDark = true,

                Primary = matrixGreen,
                Secondary = matrixDim,
                Success = matrixGreen,
                Warning = Color.FromRgb(150, 255, 0),
                Error = Color.FromRgb(255, 50, 50),
                Info = matrixGreen,

                Background = Color.FromRgb(0, 0, 0),
                BackgroundSecondary = Color.FromRgb(0, 10, 3),
                Surface = Color.FromRgb(0, 15, 4),
                SurfaceHighlight = Color.FromRgb(0, 20, 5),

                Foreground = matrixGreen,
                ForegroundSecondary = matrixDim,
                ForegroundDisabled = matrixDark,

                Border = matrixDim,
                BorderActive = matrixGreen,
                Focus = matrixGreen,
                Selection = Color.FromArgb(100, 0, 255, 65),
                Hover = Color.FromRgb(0, 20, 5),
                Active = Color.FromRgb(0, 30, 8),

                SyntaxKeyword = matrixGreen,
                SyntaxString = matrixDim,
                SyntaxComment = matrixDark,
                SyntaxNumber = matrixGreen,
                SyntaxFunction = matrixGreen,
                SyntaxVariable = matrixDim,

                Glow = new GlowSettings
                {
                    Mode = GlowMode.Always,
                    GlowColor = Color.FromRgb(0, 255, 65),
                    GlowRadius = 15.0,
                    GlowOpacity = 0.7,
                    FocusGlowColor = Color.FromRgb(0, 255, 100),
                    HoverGlowColor = Color.FromRgb(150, 255, 150)
                },
                CRTEffects = new CRTEffectSettings
                {
                    EnableScanlines = true,
                    ScanlineOpacity = 0.15,
                    ScanlineSpacing = 2,
                    ScanlineColor = Colors.Black,
                    EnableBloom = true,
                    BloomIntensity = 0.5
                },
                Opacity = new OpacitySettings
                {
                    WindowOpacity = 0.95,
                    BackgroundOpacity = 1.0,
                    InactiveWidgetOpacity = 0.6
                },
                Typography = new TypographySettings
                {
                    FontFamily = "Consolas",
                    FontSize = 12.0,
                    FontWeight = "Normal"
                }
            };
        }

        /// <summary>
        /// Creates a Synthwave theme - bright neon aesthetic
        /// Pink, purple, cyan with INTENSE glows and bloom effects
        /// </summary>
        public static Theme CreateSynthwaveTheme()
        {
            var neonPink = Color.FromRgb(255, 16, 240);      // #FF10F0
            var neonCyan = Color.FromRgb(0, 255, 255);       // #00FFFF
            var neonPurple = Color.FromRgb(138, 43, 226);    // #8A2BE2
            var darkPurple = Color.FromRgb(25, 10, 40);      // Deep purple background

            return new Theme
            {
                Name = "Synthwave",
                Description = "Neon synthwave aesthetic with intense glows",
                IsDark = true,

                Primary = neonPink,
                Secondary = neonPurple,
                Success = neonCyan,
                Warning = Color.FromRgb(255, 215, 0),
                Error = Color.FromRgb(255, 0, 100),
                Info = neonCyan,

                Background = Color.FromRgb(10, 5, 20),
                BackgroundSecondary = darkPurple,
                Surface = Color.FromRgb(30, 15, 50),
                SurfaceHighlight = Color.FromRgb(45, 25, 70),

                Foreground = neonCyan,
                ForegroundSecondary = neonPurple,
                ForegroundDisabled = Color.FromRgb(100, 50, 150),

                Border = neonPurple,
                BorderActive = neonPink,
                Focus = neonPink,
                Selection = Color.FromArgb(128, 255, 16, 240),
                Hover = Color.FromRgb(40, 20, 60),
                Active = Color.FromRgb(60, 30, 90),

                SyntaxKeyword = neonPink,
                SyntaxString = Color.FromRgb(255, 140, 255),
                SyntaxComment = Color.FromRgb(128, 100, 162),
                SyntaxNumber = neonCyan,
                SyntaxFunction = Color.FromRgb(255, 100, 255),
                SyntaxVariable = neonCyan,

                Glow = new GlowSettings
                {
                    Mode = GlowMode.Always,
                    GlowColor = neonPink,
                    GlowRadius = 20.0,
                    GlowOpacity = 0.9,
                    FocusGlowColor = neonPink,
                    HoverGlowColor = neonCyan
                },
                CRTEffects = new CRTEffectSettings
                {
                    EnableScanlines = true,
                    ScanlineOpacity = 0.05,
                    ScanlineSpacing = 3,
                    ScanlineColor = Color.FromRgb(138, 43, 226),
                    EnableBloom = true,
                    BloomIntensity = 0.8
                },
                Opacity = new OpacitySettings
                {
                    WindowOpacity = 0.98,
                    BackgroundOpacity = 0.95,
                    InactiveWidgetOpacity = 0.65
                },
                Typography = new TypographySettings
                {
                    FontFamily = "Consolas",
                    FontSize = 12.0,
                    FontWeight = "Normal"
                }
            };
        }

        /// <summary>
        /// Creates a Cyberpunk theme - blue/magenta with heavy effects
        /// High contrast cyberpunk aesthetic with strong visual effects
        /// </summary>
        public static Theme CreateCyberpunkTheme()
        {
            var cyberBlue = Color.FromRgb(0, 255, 255);       // Bright cyan
            var cyberMagenta = Color.FromRgb(255, 0, 255);    // Bright magenta
            var cyberPurple = Color.FromRgb(180, 0, 255);     // Purple
            var darkBlue = Color.FromRgb(5, 10, 25);          // Deep blue-black

            return new Theme
            {
                Name = "Cyberpunk",
                Description = "High-contrast cyberpunk with heavy effects",
                IsDark = true,

                Primary = cyberBlue,
                Secondary = cyberMagenta,
                Success = Color.FromRgb(0, 255, 128),
                Warning = Color.FromRgb(255, 200, 0),
                Error = Color.FromRgb(255, 0, 80),
                Info = cyberBlue,

                Background = Color.FromRgb(0, 0, 10),
                BackgroundSecondary = darkBlue,
                Surface = Color.FromRgb(10, 15, 30),
                SurfaceHighlight = Color.FromRgb(15, 25, 45),

                Foreground = cyberBlue,
                ForegroundSecondary = cyberPurple,
                ForegroundDisabled = Color.FromRgb(80, 80, 120),

                Border = cyberPurple,
                BorderActive = cyberMagenta,
                Focus = cyberMagenta,
                Selection = Color.FromArgb(128, 0, 255, 255),
                Hover = Color.FromRgb(15, 20, 40),
                Active = Color.FromRgb(25, 30, 60),

                SyntaxKeyword = cyberMagenta,
                SyntaxString = Color.FromRgb(255, 100, 255),
                SyntaxComment = Color.FromRgb(100, 100, 150),
                SyntaxNumber = cyberBlue,
                SyntaxFunction = cyberPurple,
                SyntaxVariable = cyberBlue,

                Glow = new GlowSettings
                {
                    Mode = GlowMode.Always,
                    GlowColor = cyberBlue,
                    GlowRadius = 18.0,
                    GlowOpacity = 0.85,
                    FocusGlowColor = cyberMagenta,
                    HoverGlowColor = cyberPurple
                },
                CRTEffects = new CRTEffectSettings
                {
                    EnableScanlines = true,
                    ScanlineOpacity = 0.12,
                    ScanlineSpacing = 2,
                    ScanlineColor = Color.FromRgb(0, 5, 15),
                    EnableBloom = true,
                    BloomIntensity = 0.7
                },
                Opacity = new OpacitySettings
                {
                    WindowOpacity = 0.96,
                    BackgroundOpacity = 0.98,
                    InactiveWidgetOpacity = 0.6
                },
                Typography = new TypographySettings
                {
                    FontFamily = "Consolas",
                    FontSize = 12.0,
                    FontWeight = "Bold"
                }
            };
        }

        /// <summary>
        /// Creates the Terminal theme - clean terminal aesthetic matching terminal.json design
        /// Dark background with neon green accents, minimal borders, terminal-focused design
        /// </summary>
        public static Theme CreateTerminalTheme()
        {
            var terminalGreen = Color.FromRgb(57, 255, 20);      // #39FF14 - Neon green accent
            var terminalCyan = Color.FromRgb(0, 217, 255);       // #00D9FF - Cyan secondary
            var terminalForeground = Color.FromRgb(184, 197, 219); // #B8C5DB - Foreground text
            var terminalBackground = Color.FromRgb(10, 14, 20);   // #0A0E14 - Dark background
            var paneBackground = Color.FromRgb(13, 17, 23);      // #0D1117 - Pane background
            var paneHeader = Color.FromRgb(22, 27, 34);          // #161B22 - Pane header

            return new Theme
            {
                Name = "Terminal",
                Description = "Clean terminal aesthetic matching mockup design - dark background, green accent, minimal borders",
                IsDark = true,

                Primary = terminalGreen,
                Secondary = terminalCyan,
                Success = terminalGreen,
                Warning = Color.FromRgb(255, 180, 84),            // #FFB454
                Error = Color.FromRgb(240, 113, 120),             // #F07178
                Info = terminalCyan,

                Background = terminalBackground,
                BackgroundSecondary = paneBackground,
                Surface = paneHeader,
                SurfaceHighlight = Color.FromRgb(37, 51, 64),    // #253340 - Selection

                Foreground = terminalForeground,
                ForegroundSecondary = Color.FromRgb(108, 122, 137), // #6C7A89 - Text secondary
                ForegroundDisabled = Color.FromRgb(77, 85, 102),    // #4D5566 - Muted

                Border = Color.FromRgb(31, 36, 48),              // #1F2430
                BorderActive = terminalGreen,
                Focus = terminalGreen,
                Selection = Color.FromRgb(37, 51, 64),           // #253340
                Hover = paneHeader,
                Active = Color.FromRgb(37, 51, 64),

                SyntaxKeyword = terminalGreen,
                SyntaxString = Color.FromRgb(255, 180, 84),
                SyntaxComment = Color.FromRgb(77, 85, 102),
                SyntaxNumber = terminalCyan,
                SyntaxFunction = terminalGreen,
                SyntaxVariable = terminalCyan,

                Glow = new GlowSettings
                {
                    Mode = GlowMode.OnFocus,
                    GlowColor = terminalGreen,
                    GlowRadius = 12.0,
                    GlowOpacity = 0.7,
                    FocusGlowColor = terminalGreen,
                    HoverGlowColor = terminalCyan
                },
                CRTEffects = new CRTEffectSettings
                {
                    EnableScanlines = false,
                    ScanlineOpacity = 0.0,
                    ScanlineSpacing = 0,
                    ScanlineColor = Colors.Black,
                    EnableBloom = false,
                    BloomIntensity = 0.0
                },
                Opacity = new OpacitySettings
                {
                    WindowOpacity = 1.0,
                    BackgroundOpacity = 1.0,
                    InactiveWidgetOpacity = 0.7
                },
                Typography = new TypographySettings
                {
                    FontFamily = "JetBrains Mono, Consolas",
                    FontSize = 11.0,
                    FontWeight = "Normal"
                }
            };
        }
    }

    /// <summary>
    /// Theme manager with hot-reloading support
    /// </summary>
    public class ThemeManager : IThemeManager
    {
        private static ThemeManager instance;

        /// <summary>
        /// Singleton instance for infrastructure use.
        /// Widgets should use injected IThemeManager. Infrastructure may use Instance when DI is unavailable.
        /// </summary>
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

            // Register built-in themes (Terminal is the primary default)
            RegisterTheme(Theme.CreateTerminalTheme());
            RegisterTheme(Theme.CreateDarkTheme());
            RegisterTheme(Theme.CreateLightTheme());

            // Register enhanced themes
            RegisterTheme(Theme.CreateAmberTerminalTheme());
            RegisterTheme(Theme.CreateMatrixTheme());
            RegisterTheme(Theme.CreateSynthwaveTheme());
            RegisterTheme(Theme.CreateCyberpunkTheme());

            // Load custom themes
            LoadCustomThemes();

            // Apply saved theme from config
            string savedTheme = ConfigurationManager.Instance.Get<string>("UI.Theme", "Terminal");
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
            // Truly synchronous implementation - avoids Task.Wait() deadlock issues
            // Note: This is safe because it doesn't use Dispatcher or WPF threading
            try
            {
                filename = filename ?? Path.Combine(themesDirectory, $"{theme.Name}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(filename));

                string json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filename, json, Encoding.UTF8);

                Logger.Instance.Info("Theme", $"Saved theme {theme.Name} to {filename}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Theme", $"Failed to save theme: {ex.Message}", ex);
            }
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
            // Truly synchronous implementation - avoids Task.Wait() deadlock issues
            // Note: This is safe because it doesn't use Dispatcher or WPF threading
            if (!Directory.Exists(themesDirectory))
                return;

            try
            {
                foreach (var file in Directory.GetFiles(themesDirectory, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file, Encoding.UTF8);
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

        /// <summary>
        /// Sets a color override for a specific element in the current theme
        /// </summary>
        /// <param name="element">The name of the color element to override</param>
        /// <param name="color">The new color value</param>
        public void SetColorOverride(string element, Color color)
        {
            if (currentTheme == null)
            {
                Logger.Instance.Warning("Theme", "Cannot set color override: no theme loaded");
                return;
            }

            if (currentTheme.ColorOverrides == null)
                currentTheme.ColorOverrides = new Dictionary<string, Color>();

            currentTheme.ColorOverrides[element] = color;
            Logger.Instance.Debug("Theme", $"Set color override: {element} = {color}");

            // Trigger theme changed event so UI can update
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs { OldTheme = currentTheme, NewTheme = currentTheme });
        }

        /// <summary>
        /// Resets a specific color override back to the theme default
        /// </summary>
        /// <param name="element">The name of the color element to reset</param>
        public void ResetOverride(string element)
        {
            if (currentTheme?.ColorOverrides == null)
                return;

            if (currentTheme.ColorOverrides.Remove(element))
            {
                Logger.Instance.Debug("Theme", $"Reset color override: {element}");
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs { OldTheme = currentTheme, NewTheme = currentTheme });
            }
        }

        /// <summary>
        /// Resets all color overrides back to theme defaults
        /// </summary>
        public void ResetAllOverrides()
        {
            if (currentTheme?.ColorOverrides == null)
                return;

            int count = currentTheme.ColorOverrides.Count;
            currentTheme.ColorOverrides.Clear();

            if (count > 0)
            {
                Logger.Instance.Debug("Theme", $"Reset {count} color overrides");
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs { OldTheme = currentTheme, NewTheme = currentTheme });
            }
        }

        /// <summary>
        /// Gets the effective color for an element, checking overrides first
        /// </summary>
        /// <param name="element">The name of the color element</param>
        /// <returns>The effective color (override if exists, otherwise theme default)</returns>
        public Color GetEffectiveColor(string element)
        {
            if (currentTheme == null)
                return Colors.White; // Safe fallback

            return currentTheme.GetColor(element);
        }
    }

    public class ThemeChangedEventArgs : EventArgs
    {
        public Theme OldTheme { get; set; }
        public Theme NewTheme { get; set; }
    }
}
