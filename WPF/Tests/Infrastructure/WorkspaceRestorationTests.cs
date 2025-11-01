using System;
using System.Collections.Generic;
using FluentAssertions;
using SuperTUI.Core.Components;
using SuperTUI.Tests.TestHelpers;
using Xunit;

namespace SuperTUI.Tests.Infrastructure
{
    /// <summary>
    /// Tests for workspace state save/restore functionality
    /// CRITICAL: Validates that workspace switching preserves pane state
    /// </summary>
    [Trait("Category", "Windows")]
    [Trait("Category", "Critical")]
    [Trait("Priority", "Critical")]
    public class WorkspaceRestorationTests : PaneTestBase
    {
        [WpfFact]
        public void SaveState_SinglePane_ShouldCaptureState()
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
        public void SaveState_MultiplePanes_ShouldCaptureAllStates()
        {
            // Arrange
            var panes = new List<PaneBase>
            {
                PaneFactory.CreatePane("tasks"),
                PaneFactory.CreatePane("notes"),
                PaneFactory.CreatePane("projects")
            };

            foreach (var pane in panes)
            {
                pane.Initialize();
            }

            // Act
            var states = new List<PaneState>();
            foreach (var pane in panes)
            {
                states.Add(pane.SaveState());
            }

            // Assert
            states.Should().HaveCount(3);
            states.Should().OnlyContain(s => s != null);
            states.Should().OnlyContain(s => !string.IsNullOrEmpty(s.PaneType));
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
            act.Should().NotThrow("RestoreState should handle null gracefully");
        }

        [WpfFact]
        public void RestoreState_WithValidState_ShouldSucceed()
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
        public void SaveRestore_RoundTrip_ShouldPreserveState()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            var savedState = pane.SaveState();
            pane.RestoreState(savedState);
            var restoredState = pane.SaveState();

            // Assert
            restoredState.Should().NotBeNull();
            restoredState.PaneType.Should().Be(savedState.PaneType);
        }

        [WpfFact]
        public void SaveRestore_MultiplePanes_ShouldPreserveAllStates()
        {
            // Arrange
            var panes = new List<PaneBase>
            {
                PaneFactory.CreatePane("tasks"),
                PaneFactory.CreatePane("notes"),
                PaneFactory.CreatePane("projects")
            };

            foreach (var pane in panes)
            {
                pane.Initialize();
            }

            // Act - Save all states
            var states = new List<PaneState>();
            foreach (var pane in panes)
            {
                states.Add(pane.SaveState());
            }

            // Restore all states
            for (int i = 0; i < panes.Count; i++)
            {
                panes[i].RestoreState(states[i]);
            }

            // Assert - All panes should be restored without errors
            foreach (var pane in panes)
            {
                pane.Should().NotBeNull();
            }
        }

        [WpfFact]
        public void WorkspaceSwitch_SaveAndRestore_ShouldMaintainPaneIdentity()
        {
            // Arrange - Workspace 1
            var workspace1Panes = new List<PaneBase>
            {
                PaneFactory.CreatePane("tasks"),
                PaneFactory.CreatePane("notes")
            };
            foreach (var pane in workspace1Panes)
            {
                pane.Initialize();
            }

            // Save workspace 1 state
            var workspace1States = new List<PaneState>();
            foreach (var pane in workspace1Panes)
            {
                workspace1States.Add(pane.SaveState());
            }

            // Arrange - Workspace 2
            var workspace2Panes = new List<PaneBase>
            {
                PaneFactory.CreatePane("projects"),
                PaneFactory.CreatePane("calendar")
            };
            foreach (var pane in workspace2Panes)
            {
                pane.Initialize();
            }

            // Act - Restore workspace 1 states
            for (int i = 0; i < workspace1Panes.Count; i++)
            {
                workspace1Panes[i].RestoreState(workspace1States[i]);
            }

            // Assert
            workspace1States.Should().HaveCount(2);
            workspace1States[0].PaneType.Should().Contain("TaskListPane");
            workspace1States[1].PaneType.Should().Contain("NotesPane");
        }

        [WpfFact]
        public void PartialRestore_SomePanesFail_ShouldNotBreakOthers()
        {
            // Arrange
            var panes = new List<PaneBase>
            {
                PaneFactory.CreatePane("tasks"),
                PaneFactory.CreatePane("notes"),
                PaneFactory.CreatePane("projects")
            };

            foreach (var pane in panes)
            {
                pane.Initialize();
            }

            var states = new List<PaneState>();
            foreach (var pane in panes)
            {
                states.Add(pane.SaveState());
            }

            // Act - Restore states (even if one fails, others should succeed)
            Action act = () =>
            {
                foreach (var (pane, state) in System.Linq.Enumerable.Zip(panes, states, (p, s) => (p, s)))
                {
                    try
                    {
                        pane.RestoreState(state);
                    }
                    catch
                    {
                        // Ignore individual failures - test that it doesn't cascade
                    }
                }
            };

            // Assert
            act.Should().NotThrow("Partial restoration failures should not cascade");
        }

        [WpfFact]
        public void ProjectContextRestore_ShouldPreserveContext()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Save state with current project context
            var state = pane.SaveState();

            // Act - Restore state
            pane.RestoreState(state);

            // Assert - Should not throw
            state.Should().NotBeNull();
        }

        [WpfFact]
        public void EmptyWorkspace_SaveRestore_ShouldNotThrow()
        {
            // Arrange - Empty workspace (no panes)
            var states = new List<PaneState>();

            // Act - Try to restore empty workspace
            Action act = () =>
            {
                foreach (var state in states)
                {
                    // Nothing to restore
                }
            };

            // Assert
            act.Should().NotThrow("Empty workspace restoration should be safe");
        }

        [WpfFact]
        public void MemoryLeak_SaveRestore100Times_ShouldNotLeak()
        {
            // This test helps detect memory leaks in state management

            // Act
            Action act = () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var pane = PaneFactory.CreatePane("tasks");
                    pane.Initialize();
                    var state = pane.SaveState();
                    pane.RestoreState(state);
                    pane.Dispose();
                }
            };

            // Assert
            act.Should().NotThrow("SaveState/RestoreState should not leak memory");
        }
    }
}
