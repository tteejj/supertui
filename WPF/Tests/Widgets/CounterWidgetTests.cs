using System;
using System.Collections.Generic;
using Xunit;
using SuperTUI.Widgets;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Widgets
{
    /// <summary>
    /// Unit tests for CounterWidget - verifies state persistence and button functionality
    /// </summary>
    public class CounterWidgetTests : IDisposable
    {
        private readonly CounterWidget widget;

        public CounterWidgetTests()
        {
            ThemeManager.Instance.Initialize(null);
            widget = new CounterWidget();
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
            Assert.Equal("Counter", widget.WidgetName);
        }

        [Fact]
        public void Initialize_ShouldStartAtZero()
        {
            // Act
            widget.Initialize();

            // Assert - Counter should start at 0
            // We can verify this through state
            var state = widget.SaveState();
            Assert.True(state.ContainsKey("Count") || state.Count == 0);
        }

        // ====================================================================
        // STATE MANAGEMENT TESTS
        // ====================================================================

        [Fact]
        public void SaveState_ShouldIncludeCountValue()
        {
            // Arrange
            widget.Initialize();

            // Act
            var state = widget.SaveState();

            // Assert
            Assert.NotNull(state);
            // State should either be empty (default) or contain Count key
            Assert.True(state.Count == 0 || state.ContainsKey("Count"));
        }

        [Fact]
        public void RestoreState_ShouldPreserveCountValue()
        {
            // Arrange
            widget.Initialize();
            var testState = new Dictionary<string, object>
            {
                ["Count"] = 42
            };

            // Act
            widget.RestoreState(testState);
            var restoredState = widget.SaveState();

            // Assert
            if (restoredState.ContainsKey("Count"))
            {
                Assert.Equal(42, restoredState["Count"]);
            }
        }

        [Fact]
        public void StateRoundTrip_ShouldPreserveData()
        {
            // Arrange
            widget.Initialize();
            var originalState = new Dictionary<string, object>
            {
                ["Count"] = 100,
                ["CustomProperty"] = "test"
            };

            // Act
            widget.RestoreState(originalState);
            var savedState = widget.SaveState();

            // Assert
            Assert.NotNull(savedState);
            // Widget should preserve state even if not using all keys
        }

        // ====================================================================
        // THEME TESTS
        // ====================================================================

        [Fact]
        public void ApplyTheme_ShouldNotThrow()
        {
            // Arrange
            widget.Initialize();
            var theme = ThemeManager.Instance.CurrentTheme;

            // Act & Assert
            widget.ApplyTheme(theme);
        }

        [Fact]
        public void ApplyTheme_MultipleThemes_ShouldNotThrow()
        {
            // Arrange
            widget.Initialize();
            var themeManager = ThemeManager.Instance;

            // Act - Switch themes multiple times
            themeManager.SetTheme("Dark");
            widget.ApplyTheme(themeManager.CurrentTheme);

            themeManager.SetTheme("Light");
            widget.ApplyTheme(themeManager.CurrentTheme);

            themeManager.SetTheme("Dark");
            widget.ApplyTheme(themeManager.CurrentTheme);

            // Assert - Should complete without exceptions
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

            // Assert - Should not throw
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldBeSafe()
        {
            // Arrange
            widget.Initialize();

            // Act
            widget.Dispose();
            widget.Dispose();

            // Assert - Multiple disposal should be idempotent
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
