using System;
using System.Linq;
using Xunit;
using SuperTUI.Infrastructure;
using SuperTUI.Components;
using Moq;

namespace SuperTUI.Tests.Components
{
    public class WorkspaceTests
    {
        [Fact]
        public void Constructor_ShouldInitializeWithName()
        {
            // Act
            var workspace = new Workspace("TestWorkspace");

            // Assert
            Assert.Equal("TestWorkspace", workspace.Name);
            Assert.NotEqual(Guid.Empty, workspace.WorkspaceId);
            Assert.Empty(workspace.Widgets);
        }

        [Fact]
        public void AddWidget_ShouldAddToCollection()
        {
            // Arrange
            var workspace = new Workspace("Test");
            var mockWidget = new Mock<IWidget>();
            mockWidget.Setup(w => w.WidgetId).Returns(Guid.NewGuid());
            mockWidget.Setup(w => w.WidgetName).Returns("TestWidget");

            // Act
            workspace.AddWidget(mockWidget.Object);

            // Assert
            Assert.Single(workspace.Widgets);
            Assert.Equal(mockWidget.Object, workspace.Widgets[0]);
        }

        [Fact]
        public void AddWidget_ShouldInitializeWidget()
        {
            // Arrange
            var workspace = new Workspace("Test");
            var mockWidget = new Mock<IWidget>();
            mockWidget.Setup(w => w.WidgetId).Returns(Guid.NewGuid());

            // Act
            workspace.AddWidget(mockWidget.Object);

            // Assert
            mockWidget.Verify(w => w.Initialize(), Times.Once);
        }

        [Fact]
        public void RemoveWidget_ShouldRemoveFromCollection()
        {
            // Arrange
            var workspace = new Workspace("Test");
            var mockWidget = new Mock<IWidget>();
            var widgetId = Guid.NewGuid();
            mockWidget.Setup(w => w.WidgetId).Returns(widgetId);
            workspace.AddWidget(mockWidget.Object);

            // Act
            workspace.RemoveWidget(widgetId);

            // Assert
            Assert.Empty(workspace.Widgets);
        }

        [Fact]
        public void RemoveWidget_ShouldDisposeWidget()
        {
            // Arrange
            var workspace = new Workspace("Test");
            var mockWidget = new Mock<IWidget>();
            var widgetId = Guid.NewGuid();
            mockWidget.Setup(w => w.WidgetId).Returns(widgetId);
            workspace.AddWidget(mockWidget.Object);

            // Act
            workspace.RemoveWidget(widgetId);

            // Assert
            mockWidget.Verify(w => w.Dispose(), Times.Once);
        }

        [Fact]
        public void FocusNext_WithNoWidgets_ShouldNotThrow()
        {
            // Arrange
            var workspace = new Workspace("Test");

            // Act & Assert - Should not throw
            workspace.FocusNext();
        }

        [Fact]
        public void FocusNext_ShouldCycleThroughWidgets()
        {
            // Arrange
            var workspace = new Workspace("Test");
            var widget1 = new Mock<IWidget>();
            widget1.Setup(w => w.WidgetId).Returns(Guid.NewGuid());
            var widget2 = new Mock<IWidget>();
            widget2.Setup(w => w.WidgetId).Returns(Guid.NewGuid());
            var widget3 = new Mock<IWidget>();
            widget3.Setup(w => w.WidgetId).Returns(Guid.NewGuid());

            workspace.AddWidget(widget1.Object);
            workspace.AddWidget(widget2.Object);
            workspace.AddWidget(widget3.Object);

            // Act - Focus next 4 times (should cycle back to first)
            workspace.FocusNext();
            workspace.FocusNext();
            workspace.FocusNext();
            workspace.FocusNext();

            // Assert - Each widget should have received focus at least once
            widget1.VerifySet(w => w.HasFocus = true, Times.AtLeastOnce);
            widget2.VerifySet(w => w.HasFocus = true, Times.AtLeastOnce);
            widget3.VerifySet(w => w.HasFocus = true, Times.AtLeastOnce);
        }

        [Fact]
        public void FocusPrevious_ShouldCycleBackwards()
        {
            // Arrange
            var workspace = new Workspace("Test");
            var widget1 = new Mock<IWidget>();
            widget1.Setup(w => w.WidgetId).Returns(Guid.NewGuid());
            var widget2 = new Mock<IWidget>();
            widget2.Setup(w => w.WidgetId).Returns(Guid.NewGuid());

            workspace.AddWidget(widget1.Object);
            workspace.AddWidget(widget2.Object);

            // Act - Start at 0, go back to last
            workspace.FocusPrevious();

            // Assert - Should focus the last widget
            widget2.VerifySet(w => w.HasFocus = true, Times.Once);
        }

        [Fact]
        public void SaveState_ShouldCaptureAllWidgetStates()
        {
            // Arrange
            var workspace = new Workspace("Test");
            var widget1 = new Mock<IWidget>();
            widget1.Setup(w => w.WidgetId).Returns(Guid.NewGuid());
            widget1.Setup(w => w.SaveState()).Returns(new System.Collections.Generic.Dictionary<string, object>
            {
                { "key1", "value1" }
            });

            var widget2 = new Mock<IWidget>();
            widget2.Setup(w => w.WidgetId).Returns(Guid.NewGuid());
            widget2.Setup(w => w.SaveState()).Returns(new System.Collections.Generic.Dictionary<string, object>
            {
                { "key2", "value2" }
            });

            workspace.AddWidget(widget1.Object);
            workspace.AddWidget(widget2.Object);

            // Act
            var state = workspace.SaveState();

            // Assert
            Assert.Equal("Test", state.WorkspaceName);
            Assert.Equal(2, state.WidgetStates.Count);
            widget1.Verify(w => w.SaveState(), Times.Once);
            widget2.Verify(w => w.SaveState(), Times.Once);
        }

        [Fact]
        public void RestoreState_ShouldRestoreWidgetStates()
        {
            // Arrange
            var workspace = new Workspace("Test");
            var widgetId1 = Guid.NewGuid();
            var widgetId2 = Guid.NewGuid();

            var widget1 = new Mock<IWidget>();
            widget1.Setup(w => w.WidgetId).Returns(widgetId1);
            widget1.Setup(w => w.SaveState()).Returns(new System.Collections.Generic.Dictionary<string, object>
            {
                { "WidgetId", widgetId1 }
            });

            var widget2 = new Mock<IWidget>();
            widget2.Setup(w => w.WidgetId).Returns(widgetId2);
            widget2.Setup(w => w.SaveState()).Returns(new System.Collections.Generic.Dictionary<string, object>
            {
                { "WidgetId", widgetId2 }
            });

            workspace.AddWidget(widget1.Object);
            workspace.AddWidget(widget2.Object);

            var state = workspace.SaveState();

            // Act
            workspace.RestoreState(state);

            // Assert
            widget1.Verify(w => w.RestoreState(It.IsAny<System.Collections.Generic.Dictionary<string, object>>()), Times.Once);
            widget2.Verify(w => w.RestoreState(It.IsAny<System.Collections.Generic.Dictionary<string, object>>()), Times.Once);
        }

        [Fact]
        public void Dispose_ShouldDisposeAllWidgets()
        {
            // Arrange
            var workspace = new Workspace("Test");
            var widget1 = new Mock<IWidget>();
            widget1.Setup(w => w.WidgetId).Returns(Guid.NewGuid());
            var widget2 = new Mock<IWidget>();
            widget2.Setup(w => w.WidgetId).Returns(Guid.NewGuid());

            workspace.AddWidget(widget1.Object);
            workspace.AddWidget(widget2.Object);

            // Act
            workspace.Dispose();

            // Assert
            widget1.Verify(w => w.Dispose(), Times.Once);
            widget2.Verify(w => w.Dispose(), Times.Once);
        }
    }
}
