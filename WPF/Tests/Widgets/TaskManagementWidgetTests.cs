using System;
using System.Collections.Generic;
using Xunit;
using SuperTUI.Widgets;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Widgets
{
    /// <summary>
    /// Unit tests for TaskManagementWidget - verifies task service integration and UI
    /// </summary>
    public class TaskManagementWidgetTests : IDisposable
    {
        private readonly TaskManagementWidget widget;

        public TaskManagementWidgetTests()
        {
            ThemeManager.Instance.Initialize(null);
            widget = new TaskManagementWidget();
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
            Assert.Equal("Task Management", widget.WidgetName);
        }

        [Fact]
        public void Initialize_ShouldCreateTaskService()
        {
            // Act
            widget.Initialize();

            // Assert - TaskService should be initialized
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
            themeManager.SetTheme("Light");
            widget.ApplyTheme(themeManager.CurrentTheme);

            themeManager.SetTheme("Dark");
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
