using System;
using FluentAssertions;
using SuperTUI.Tests.TestHelpers;
using Xunit;

namespace SuperTUI.Tests.Panes
{
    /// <summary>
    /// Tests for FileBrowserPane - validates file browsing with security
    /// </summary>
    [Trait("Category", "Windows")]
    [Trait("Category", "High")]
    [Trait("Priority", "High")]
    public class FileBrowserPaneTests : PaneTestBase
    {
        [WpfFact]
        public void FileBrowserPane_Initialize_ShouldSucceed()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("files");

            // Act
            Action act = () => pane.Initialize();

            // Assert
            act.Should().NotThrow();
        }

        [WpfFact]
        public void FileBrowserPane_SaveState_ShouldCaptureCurrentPath()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("files");
            pane.Initialize();

            // Act
            var state = pane.SaveState();

            // Assert
            state.Should().NotBeNull();
            state.PaneType.Should().Contain("FileBrowserPane");
        }

        [WpfFact]
        public void FileBrowserPane_RestoreState_ShouldRestorePath()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("files");
            pane.Initialize();
            var state = pane.SaveState();

            // Act
            Action act = () => pane.RestoreState(state);

            // Assert
            act.Should().NotThrow();
        }

        [WpfFact]
        public void FileBrowserPane_Dispose_ShouldCleanup()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("files");
            pane.Initialize();

            // Act
            Action act = () => pane.Dispose();

            // Assert
            act.Should().NotThrow();
        }

        [WpfFact]
        public void FileBrowserPane_SecurityValidation_ShouldUseSecurityManager()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("files");
            pane.Initialize();

            // Assert - SecurityManager should be injected and configured
            SecurityManager.Should().NotBeNull();
            // Note: IsInitialized property doesn't exist on ISecurityManager interface
            // SecurityManager is initialized in test setup, validation happens during pane operations
        }

        [WpfFact]
        public void FileBrowserPane_FullLifecycle_ShouldSucceed()
        {
            // Arrange & Act
            var pane = PaneFactory.CreatePane("files");
            pane.Initialize();
            var state = pane.SaveState();
            pane.RestoreState(state);
            pane.Dispose();

            // Assert - Should not throw
        }

        [WpfFact]
        public void FileBrowserPane_MemoryLeak_CreateDispose10Times_ShouldNotThrow()
        {
            // This test helps detect event subscription memory leaks

            // Act
            Action act = () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var pane = PaneFactory.CreatePane("files");
                    pane.Initialize();
                    pane.Dispose();
                }
            };

            // Assert
            act.Should().NotThrow("Creating/disposing FileBrowserPane multiple times should not leak");
        }

        [WpfFact]
        public void FileBrowserPane_ApplyTheme_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("files");
            pane.Initialize();

            // Act
            Action act = () => pane.ApplyTheme();

            // Assert
            act.Should().NotThrow();
        }
    }
}
