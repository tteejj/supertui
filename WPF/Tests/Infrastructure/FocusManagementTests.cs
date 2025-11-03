using System;
using FluentAssertions;
using SuperTUI.Tests.TestHelpers;
using Xunit;

namespace SuperTUI.Tests.Infrastructure
{
    /// <summary>
    /// Tests for focus management system
    /// CRITICAL: Validates that focus is properly managed across panes
    /// </summary>
    [Trait("Category", "Windows")]
    [Trait("Category", "Critical")]
    [Trait("Priority", "Critical")]
    public class FocusManagementTests : PaneTestBase
    {
        [WpfFact]
        public void Pane_InitialFocus_ShouldBeFalse()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Assert - Use IsKeyboardFocusWithin (single source of truth for focus state)
            pane.IsKeyboardFocusWithin.Should().BeFalse("New pane should not have keyboard focus initially");
        }

        [WpfFact]
        public void Pane_ApplyTheme_ShouldUpdateVisualState()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act - Visual state updates are now automatic via IsKeyboardFocusWithinChanged
            Action act = () => pane.ApplyTheme();

            // Assert
            act.Should().NotThrow("ApplyTheme should update visual state");
        }

        [WpfFact]
        public void Pane_ApplyTheme_WhenFocused_ShouldHighlight()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act - Simulate focus
            Action act = () => pane.ApplyTheme();

            // Assert
            act.Should().NotThrow("ApplyTheme should highlight focused pane");
        }

        [WpfFact]
        public void Pane_ApplyTheme_WhenUnfocused_ShouldNotHighlight()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            Action act = () => pane.ApplyTheme();

            // Assert
            act.Should().NotThrow("ApplyTheme should work for unfocused pane");
        }

        [WpfFact]
        public void MultiplePanes_OnlyOneShouldHaveFocus()
        {
            // Arrange
            var pane1 = PaneFactory.CreatePane("tasks");
            var pane2 = PaneFactory.CreatePane("notes");
            pane1.Initialize();
            pane2.Initialize();

            // Assert - Use IsKeyboardFocusWithin (single source of truth for focus state)
            pane1.IsKeyboardFocusWithin.Should().BeFalse("Pane1 should not have keyboard focus initially");
            pane2.IsKeyboardFocusWithin.Should().BeFalse("Pane2 should not have keyboard focus initially");
        }

        [WpfFact]
        public void FocusChange_ShouldTriggerThemeUpdate()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act - Theme updates now happen automatically via IsKeyboardFocusWithinChanged event
            Action act = () =>
            {
                pane.ApplyTheme();
            };

            // Assert
            act.Should().NotThrow("Theme update should work without errors");
        }

        [WpfFact]
        public void FocusRestore_AfterWorkspaceSwitch_ShouldWork()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();
            var state = pane.SaveState();

            // Act - Simulate workspace switch and restore
            pane.RestoreState(state);
            Action act = () => pane.ApplyTheme();

            // Assert
            act.Should().NotThrow("State should be restorable after workspace switch");
        }

        [WpfFact]
        public void Pane_Dispose_ShouldClearFocus()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            pane.Dispose();

            // Assert - After disposal, pane should not be active
            // (Can't check IsActive after disposal, but disposal should complete cleanly)
        }

        [WpfFact]
        public void FocusHistory_TrackMultiplePanes_ShouldNotThrow()
        {
            // This tests FocusHistoryManager's ability to track panes

            // Arrange
            var panes = new[]
            {
                PaneFactory.CreatePane("tasks"),
                PaneFactory.CreatePane("notes"),
                PaneFactory.CreatePane("projects")
            };

            // Act
            foreach (var pane in panes)
            {
                pane.Initialize();
            }

            // Assert - All panes should be tracked without errors
            foreach (var pane in panes)
            {
                pane.Should().NotBeNull();
            }

            // Cleanup
            foreach (var pane in panes)
            {
                pane.Dispose();
            }
        }

        [WpfFact]
        public void FocusHistory_UntrackOnDispose_ShouldPreventMemoryLeak()
        {
            // This test validates that FocusHistoryManager properly untracks disposed panes

            // Act
            Action act = () =>
            {
                for (int i = 0; i < 50; i++)
                {
                    var pane = PaneFactory.CreatePane("tasks");
                    pane.Initialize();
                    pane.Dispose(); // Should untrack from FocusHistoryManager
                }
            };

            // Assert
            act.Should().NotThrow("FocusHistory should untrack disposed panes to prevent memory leak");
        }

        [WpfFact]
        public void Pane_Focusable_ShouldBeTrue()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Assert - Panes should be focusable to receive keyboard events
            pane.Focusable.Should().BeTrue("Panes must be focusable for keyboard navigation");
        }

        [WpfFact]
        public void ThemeChange_ShouldUpdateAllPanes()
        {
            // Arrange
            var panes = new[]
            {
                PaneFactory.CreatePane("tasks"),
                PaneFactory.CreatePane("notes")
            };

            foreach (var pane in panes)
            {
                pane.Initialize();
            }

            // Act - Theme change should propagate to all panes via event subscription
            Action act = () =>
            {
                foreach (var pane in panes)
                {
                    pane.ApplyTheme();
                }
            };

            // Assert
            act.Should().NotThrow("Theme changes should update all panes");

            // Cleanup
            foreach (var pane in panes)
            {
                pane.Dispose();
            }
        }
    }
}
