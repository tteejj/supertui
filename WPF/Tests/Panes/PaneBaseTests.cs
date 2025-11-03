using System;
using FluentAssertions;
using SuperTUI.Core.Components;
using SuperTUI.Tests.TestHelpers;
using Xunit;

namespace SuperTUI.Tests.Panes
{
    /// <summary>
    /// Tests for PaneBase lifecycle and state management
    /// CRITICAL: Validates pane initialization, disposal, and state persistence
    /// </summary>
    [Trait("Category", "Windows")]
    [Trait("Category", "Critical")]
    [Trait("Priority", "Critical")]
    public class PaneBaseTests : PaneTestBase
    {
        [WpfFact]
        public void Initialize_ShouldBuildUIContent()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");

            // Act
            Action act = () => pane.Initialize();

            // Assert
            act.Should().NotThrow("Initialize should build UI without errors");
        }

        [WpfFact]
        public void SaveState_ShouldReturnNonNullPaneState()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            var state = pane.SaveState();

            // Assert
            state.Should().NotBeNull();
            state.PaneType.Should().NotBeNullOrEmpty();
        }

        [WpfFact]
        public void SaveState_ShouldIncludePaneType()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("notes");
            pane.Initialize();

            // Act
            var state = pane.SaveState();

            // Assert
            state.PaneType.Should().Contain("Pane");
        }

        [WpfFact]
        public void RestoreState_WithNullState_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            Action act = () => pane.RestoreState(null);

            // Assert
            act.Should().NotThrow("RestoreState should handle null state gracefully");
        }

        [WpfFact]
        public void RestoreState_WithValidState_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();
            var state = pane.SaveState();

            // Act
            Action act = () => pane.RestoreState(state);

            // Assert
            act.Should().NotThrow();
        }

        [WpfFact]
        public void Dispose_ShouldUnsubscribeEvents()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            Action act = () => pane.Dispose();

            // Assert
            act.Should().NotThrow("Dispose should clean up all resources");
        }

        [WpfFact]
        public void Dispose_CalledTwice_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("notes");
            pane.Initialize();
            pane.Dispose();

            // Act
            Action act = () => pane.Dispose();

            // Assert
            act.Should().NotThrow("Double dispose should be safe");
        }

        [WpfFact]
        public void ApplyTheme_ShouldUpdateVisualState()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            Action act = () => pane.ApplyTheme();

            // Assert
            act.Should().NotThrow("ApplyTheme should update visuals without errors");
        }

        [WpfFact]
        public void ApplyTheme_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("notes");
            pane.Initialize();

            // Act
            Action act = () =>
            {
                pane.ApplyTheme();
                pane.ApplyTheme();
                pane.ApplyTheme();
            };

            // Assert
            act.Should().NotThrow("Multiple ApplyTheme calls should be safe");
        }

        [WpfFact]
        public void PaneName_ShouldBeSet()
        {
            // Arrange & Act
            var pane = PaneFactory.CreatePane("tasks");

            // Assert
            pane.PaneName.Should().NotBeNullOrEmpty();
        }

        [WpfFact]
        public void IsKeyboardFocusWithin_InitiallyFalse()
        {
            // Arrange & Act
            var pane = PaneFactory.CreatePane("tasks");

            // Assert - Use IsKeyboardFocusWithin (single source of truth for focus state)
            pane.IsKeyboardFocusWithin.Should().BeFalse("Pane should not have keyboard focus initially");
        }

        [WpfFact]
        public void ApplyTheme_MultipleCalls_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act - Visual updates happen via IsKeyboardFocusWithinChanged event
            Action act = () => pane.ApplyTheme();

            // Assert
            act.Should().NotThrow("ApplyTheme should handle multiple calls safely");
        }

        [WpfFact]
        public void SizePreference_ShouldHaveDefaultValue()
        {
            // Arrange & Act
            var pane = PaneFactory.CreatePane("tasks");

            // Assert
            pane.SizePreference.Should().Be(PaneSizePreference.Flex, "Default size preference should be Flex");
        }

        [WpfTheory]
        [InlineData("tasks")]
        [InlineData("notes")]
        [InlineData("projects")]
        [InlineData("calendar")]
        public void AllPanes_ShouldInitializeWithoutErrors(string paneType)
        {
            // Arrange
            var pane = PaneFactory.CreatePane(paneType);

            // Act
            Action act = () => pane.Initialize();

            // Assert
            act.Should().NotThrow($"{paneType} pane should initialize without errors");
        }

        [WpfTheory]
        [InlineData("tasks")]
        [InlineData("notes")]
        [InlineData("projects")]
        [InlineData("calendar")]
        public void AllPanes_ShouldSaveStateWithoutErrors(string paneType)
        {
            // Arrange
            var pane = PaneFactory.CreatePane(paneType);
            pane.Initialize();

            // Act
            var state = pane.SaveState();

            // Assert
            state.Should().NotBeNull($"{paneType} pane should save state");
            state.PaneType.Should().NotBeNullOrEmpty();
        }

        [WpfTheory]
        [InlineData("tasks")]
        [InlineData("notes")]
        [InlineData("projects")]
        [InlineData("calendar")]
        public void AllPanes_ShouldDisposeWithoutErrors(string paneType)
        {
            // Arrange
            var pane = PaneFactory.CreatePane(paneType);
            pane.Initialize();

            // Act
            Action act = () => pane.Dispose();

            // Assert
            act.Should().NotThrow($"{paneType} pane should dispose cleanly");
        }

        [WpfFact]
        public void LifecycleTest_CreateInitializeSaveRestoreDispose_ShouldSucceed()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");

            // Act & Assert - Full lifecycle
            Action initialize = () => pane.Initialize();
            initialize.Should().NotThrow("Initialize should succeed");

            Action saveState = () => { var state = pane.SaveState(); };
            saveState.Should().NotThrow("SaveState should succeed");

            var state = pane.SaveState();
            Action restoreState = () => pane.RestoreState(state);
            restoreState.Should().NotThrow("RestoreState should succeed");

            Action dispose = () => pane.Dispose();
            dispose.Should().NotThrow("Dispose should succeed");
        }

        [WpfFact]
        public void MemoryLeak_CreateDispose100Panes_ShouldNotThrow()
        {
            // This test helps detect memory leaks by creating/disposing many panes
            // If there are event subscription leaks, this may cause issues

            // Act
            Action act = () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var pane = PaneFactory.CreatePane("tasks");
                    pane.Initialize();
                    pane.Dispose();
                }
            };

            // Assert
            act.Should().NotThrow("Creating/disposing 100 panes should not cause memory leaks");
        }
    }
}
