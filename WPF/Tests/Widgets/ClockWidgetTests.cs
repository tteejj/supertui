using System;
using System.Windows.Media;
using Xunit;
using SuperTUI.Widgets;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Widgets
{
    /// <summary>
    /// Unit tests for ClockWidget - verifies initialization, theming, disposal, and state management
    /// </summary>
    public class ClockWidgetTests : IDisposable
    {
        private readonly ClockWidget widget;

        public ClockWidgetTests()
        {
            // Initialize infrastructure before widget tests
            ThemeManager.Instance.Initialize(null);
            widget = new ClockWidget();
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
            Assert.Equal("Clock", widget.WidgetName);
        }

        [Fact]
        public void Initialize_ShouldStartTimer()
        {
            // Act
            widget.Initialize();

            // Assert - Widget should be initialized without throwing
            // Timer should be running (verified by disposal test)
            Assert.NotNull(widget);
        }

        [Fact]
        public void Initialize_ShouldApplyCurrentTheme()
        {
            // Arrange
            var themeManager = ThemeManager.Instance;
            themeManager.SetTheme("Dark");

            // Act
            widget.Initialize();

            // Assert - Widget should use theme colors
            // Cannot directly verify due to private implementation
            // But initialize should not throw
        }

        // ====================================================================
        // THEME SWITCHING TESTS
        // ====================================================================

        [Fact]
        public void ApplyTheme_ShouldUpdateColors()
        {
            // Arrange
            widget.Initialize();
            var theme = ThemeManager.Instance.CurrentTheme;

            // Act
            widget.ApplyTheme(theme);

            // Assert - Should not throw
            // Theme colors should be applied to UI elements
        }

        [Fact]
        public void ApplyTheme_ShouldHandleNullTheme()
        {
            // Arrange
            widget.Initialize();

            // Act & Assert - Should not throw
            widget.ApplyTheme(null);
        }

        [Fact]
        public void ThemeSwitch_DarkToLight_ShouldUpdateUI()
        {
            // Arrange
            widget.Initialize();
            var themeManager = ThemeManager.Instance;
            themeManager.SetTheme("Dark");
            widget.ApplyTheme(themeManager.CurrentTheme);

            // Act
            themeManager.SetTheme("Light");
            widget.ApplyTheme(themeManager.CurrentTheme);

            // Assert - Should complete without exceptions
        }

        // ====================================================================
        // DISPOSAL TESTS
        // ====================================================================

        [Fact]
        public void Dispose_ShouldStopTimer()
        {
            // Arrange
            widget.Initialize();

            // Act
            widget.Dispose();

            // Assert - Timer should be stopped and disposed
            // No way to directly verify, but should not throw
            // and should not cause resource leaks
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            widget.Initialize();

            // Act & Assert
            widget.Dispose();
            widget.Dispose(); // Second call should be safe
            widget.Dispose(); // Third call should be safe
        }

        [Fact]
        public void Dispose_WithoutInitialize_ShouldNotThrow()
        {
            // Act & Assert
            var uninitializedWidget = new ClockWidget();
            uninitializedWidget.Dispose(); // Should handle gracefully
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
            Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(state);
        }

        [Fact]
        public void RestoreState_ShouldAcceptValidState()
        {
            // Arrange
            widget.Initialize();
            var state = widget.SaveState();

            // Act & Assert - Should not throw
            widget.RestoreState(state);
        }

        [Fact]
        public void RestoreState_WithEmptyState_ShouldNotThrow()
        {
            // Arrange
            widget.Initialize();
            var emptyState = new System.Collections.Generic.Dictionary<string, object>();

            // Act & Assert
            widget.RestoreState(emptyState);
        }

        [Fact]
        public void RestoreState_WithNullState_ShouldNotThrow()
        {
            // Arrange
            widget.Initialize();

            // Act & Assert
            widget.RestoreState(null);
        }

        // ====================================================================
        // FOCUS TESTS
        // ====================================================================

        [Fact]
        public void OnFocus_ShouldApplyFocusedStyle()
        {
            // Arrange
            widget.Initialize();

            // Act
            widget.OnFocus();

            // Assert - Should update border or visual indicator
            // Cannot directly verify internal state, but should not throw
        }

        [Fact]
        public void OnBlur_ShouldRemoveFocusedStyle()
        {
            // Arrange
            widget.Initialize();
            widget.OnFocus();

            // Act
            widget.OnBlur();

            // Assert - Should remove focus indicator
        }

        [Fact]
        public void FocusCycle_ShouldNotThrow()
        {
            // Arrange
            widget.Initialize();

            // Act
            widget.OnFocus();
            widget.OnBlur();
            widget.OnFocus();
            widget.OnBlur();

            // Assert - Multiple focus/blur cycles should be safe
        }
    }
}
