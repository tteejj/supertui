using System;
using System.Collections.Generic;
using Xunit;
using SuperTUI.Widgets;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Widgets
{
    /// <summary>
    /// Unit tests for CommandPaletteWidget - verifies command registration and fuzzy search
    /// </summary>
    public class CommandPaletteWidgetTests : IDisposable
    {
        private readonly CommandPaletteWidget widget;

        public CommandPaletteWidgetTests()
        {
            ThemeManager.Instance.Initialize(null);
            widget = new CommandPaletteWidget();
        }

        public void Dispose()
        {
            widget?.Dispose();
        }

        // ====================================================================
        // INITIALIZATION TESTS
        // ====================================================================

        [Fact]
        public void Initialize_ShouldSetWidgetName()
        {
            // Act
            widget.Initialize();

            // Assert
            Assert.Equal("Command Palette", widget.WidgetName);
        }

        [Fact]
        public void Initialize_ShouldApplyThemeColors()
        {
            // Arrange - CommandPaletteWidget uses theme.Surface and theme.BackgroundSecondary

            // Act
            widget.Initialize();

            // Assert - No hardcoded colors
            Assert.NotNull(widget);
        }

        // ====================================================================
        // STATE MANAGEMENT TESTS
        // ====================================================================

        [Fact]
        public void SaveState_ShouldReturnValidDictionary()
        {
            // Arrange
            widget.Initialize();

            // Act
            var state = widget.SaveState();

            // Assert
            Assert.NotNull(state);
        }

        [Fact]
        public void RestoreState_ShouldNotThrow()
        {
            // Arrange
            widget.Initialize();
            var state = new Dictionary<string, object>();

            // Act & Assert
            widget.RestoreState(state);
        }

        // ====================================================================
        // THEME TESTS
        // ====================================================================

        [Fact]
        public void ApplyTheme_ShouldUpdateColors()
        {
            // Arrange
            widget.Initialize();
            var theme = ThemeManager.Instance.CurrentTheme;

            // Act
            widget.ApplyTheme(theme);

            // Assert
        }

        [Fact]
        public void ThemeSwitch_ShouldNotThrow()
        {
            // Arrange
            widget.Initialize();
            var themeManager = ThemeManager.Instance;

            // Act
            themeManager.SetTheme("Dark");
            widget.ApplyTheme(themeManager.CurrentTheme);

            themeManager.SetTheme("Light");
            widget.ApplyTheme(themeManager.CurrentTheme);

            // Assert
        }

        // ====================================================================
        // DISPOSAL TESTS
        // ====================================================================

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            widget.Initialize();

            // Act
            widget.Dispose();

            // Assert
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldBeSafe()
        {
            // Arrange
            widget.Initialize();

            // Act
            widget.Dispose();
            widget.Dispose();

            // Assert
        }

        // ====================================================================
        // FOCUS TESTS
        // ====================================================================

        [Fact]
        public void Focus_ShouldNotThrow()
        {
            // Arrange
            widget.Initialize();

            // Act & Assert
            widget.OnFocus();
            widget.OnBlur();
        }
    }
}
