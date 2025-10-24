using System;
using System.IO;
using System.Linq;
using Xunit;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Infrastructure
{
    public class ThemeManagerTests : IDisposable
    {
        private readonly string testThemesDir;
        private readonly ThemeManager themeManager;

        public ThemeManagerTests()
        {
            testThemesDir = Path.Combine(Path.GetTempPath(), $"themes_{Guid.NewGuid()}");
            Directory.CreateDirectory(testThemesDir);

            themeManager = new ThemeManager();
            themeManager.Initialize(testThemesDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(testThemesDir))
            {
                Directory.Delete(testThemesDir, recursive: true);
            }
        }

        [Fact]
        public void Initialize_ShouldLoadDefaultTheme()
        {
            // Assert
            Assert.NotNull(themeManager.CurrentTheme);
            Assert.Equal("Dark", themeManager.CurrentTheme.Name);
        }

        [Fact]
        public void RegisterTheme_ShouldAddThemeToAvailableThemes()
        {
            // Arrange
            var customTheme = new Theme
            {
                Name = "CustomTheme",
                Background = System.Windows.Media.Colors.Black,
                Foreground = System.Windows.Media.Colors.White
            };

            // Act
            themeManager.RegisterTheme(customTheme);
            var themes = themeManager.GetAvailableThemes();

            // Assert
            Assert.Contains(themes, t => t.Name == "CustomTheme");
        }

        [Fact]
        public void ApplyTheme_ShouldChangeCurrentTheme()
        {
            // Arrange
            var customTheme = new Theme
            {
                Name = "TestTheme",
                Background = System.Windows.Media.Colors.Navy,
                Foreground = System.Windows.Media.Colors.Yellow
            };
            themeManager.RegisterTheme(customTheme);

            // Act
            themeManager.ApplyTheme("TestTheme");

            // Assert
            Assert.Equal("TestTheme", themeManager.CurrentTheme.Name);
            Assert.Equal(System.Windows.Media.Colors.Navy, themeManager.CurrentTheme.Background);
        }

        [Fact]
        public void ApplyTheme_WithNonExistentTheme_ShouldKeepCurrentTheme()
        {
            // Arrange
            var originalTheme = themeManager.CurrentTheme.Name;

            // Act
            themeManager.ApplyTheme("NonExistentTheme");

            // Assert
            Assert.Equal(originalTheme, themeManager.CurrentTheme.Name);
        }

        [Fact]
        public void SaveTheme_ShouldPersistToFile()
        {
            // Arrange
            var customTheme = new Theme
            {
                Name = "SaveTest",
                Background = System.Windows.Media.Colors.DarkSlateGray,
                Foreground = System.Windows.Media.Colors.LightGray
            };

            // Act
            themeManager.SaveTheme(customTheme);

            // Assert
            var themeFile = Path.Combine(testThemesDir, "SaveTest.json");
            Assert.True(File.Exists(themeFile));
        }

        [Fact]
        public void LoadThemeFromFile_ShouldDeserializeTheme()
        {
            // Arrange
            var customTheme = new Theme
            {
                Name = "LoadTest",
                Background = System.Windows.Media.Colors.MidnightBlue,
                Foreground = System.Windows.Media.Colors.Cyan
            };
            themeManager.SaveTheme(customTheme);
            var themeFile = Path.Combine(testThemesDir, "LoadTest.json");

            // Act
            var loadedTheme = themeManager.LoadThemeFromFile(themeFile);

            // Assert
            Assert.NotNull(loadedTheme);
            Assert.Equal("LoadTest", loadedTheme.Name);
            Assert.Equal(System.Windows.Media.Colors.MidnightBlue, loadedTheme.Background);
            Assert.Equal(System.Windows.Media.Colors.Cyan, loadedTheme.Foreground);
        }

        [Fact]
        public void GetAvailableThemes_ShouldIncludeBuiltInThemes()
        {
            // Act
            var themes = themeManager.GetAvailableThemes();

            // Assert
            Assert.Contains(themes, t => t.Name == "Dark");
            Assert.Contains(themes, t => t.Name == "Light");
            Assert.True(themes.Count >= 2);
        }

        [Fact]
        public void Theme_ShouldHaveAllRequiredColors()
        {
            // Arrange
            var theme = themeManager.CurrentTheme;

            // Assert - Verify all essential color properties are set
            Assert.NotEqual(default, theme.Background);
            Assert.NotEqual(default, theme.Foreground);
            Assert.NotEqual(default, theme.Primary);
            Assert.NotEqual(default, theme.Secondary);
            Assert.NotEqual(default, theme.Success);
            Assert.NotEqual(default, theme.Warning);
            Assert.NotEqual(default, theme.Error);
            Assert.NotEqual(default, theme.Info);
        }
    }
}
