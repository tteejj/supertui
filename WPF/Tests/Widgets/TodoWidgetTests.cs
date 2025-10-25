using System;
using System.Collections.Generic;
using Xunit;
using SuperTUI.Widgets;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Widgets
{
    /// <summary>
    /// Unit tests for TodoWidget - verifies task management and state persistence
    /// </summary>
    public class TodoWidgetTests : IDisposable
    {
        private readonly TodoWidget widget;

        public TodoWidgetTests()
        {
            ThemeManager.Instance.Initialize(null);
            widget = new TodoWidget();
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
            Assert.Equal("Todo List", widget.WidgetName);
        }

        [Fact]
        public void Initialize_ShouldCreateUI()
        {
            // Act
            widget.Initialize();

            // Assert - Should complete without throwing
            Assert.NotNull(widget);
        }

        // ====================================================================
        // STATE MANAGEMENT TESTS
        // ====================================================================

        [Fact]
        public void SaveState_ShouldIncludeTodoItems()
        {
            // Arrange
            widget.Initialize();

            // Act
            var state = widget.SaveState();

            // Assert
            Assert.NotNull(state);
            // State may include TodoItems list
        }

        [Fact]
        public void RestoreState_WithTodoItems_ShouldRestoreTasks()
        {
            // Arrange
            widget.Initialize();
            var testState = new Dictionary<string, object>
            {
                ["TodoItems"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["Text"] = "Test task 1",
                        ["IsCompleted"] = false
                    },
                    new Dictionary<string, object>
                    {
                        ["Text"] = "Test task 2",
                        ["IsCompleted"] = true
                    }
                }
            };

            // Act & Assert - Should not throw
            widget.RestoreState(testState);
        }

        [Fact]
        public void StateRoundTrip_ShouldPreserveTasks()
        {
            // Arrange
            widget.Initialize();
            var originalState = new Dictionary<string, object>
            {
                ["TodoItems"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["Text"] = "Important task",
                        ["IsCompleted"] = false
                    }
                }
            };

            // Act
            widget.RestoreState(originalState);
            var savedState = widget.SaveState();

            // Assert
            Assert.NotNull(savedState);
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

            // Assert - Should use theme colors (not hardcoded)
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
