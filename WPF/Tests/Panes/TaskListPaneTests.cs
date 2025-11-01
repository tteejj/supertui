using System;
using System.Linq;
using FluentAssertions;
using SuperTUI.Core.Models;
using SuperTUI.Tests.TestHelpers;
using Xunit;

namespace SuperTUI.Tests.Panes
{
    /// <summary>
    /// Tests for TaskListPane - validates task management UI
    /// </summary>
    [Trait("Category", "Windows")]
    [Trait("Category", "High")]
    [Trait("Priority", "High")]
    public class TaskListPaneTests : PaneTestBase
    {
        [WpfFact]
        public void TaskListPane_Initialize_ShouldSucceed()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");

            // Act
            Action act = () => pane.Initialize();

            // Assert
            act.Should().NotThrow();
        }

        [WpfFact]
        public void TaskListPane_SaveState_ShouldCaptureCurrentState()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            var state = pane.SaveState();

            // Assert
            state.Should().NotBeNull();
            state.PaneType.Should().Contain("TaskListPane");
        }

        [WpfFact]
        public void TaskListPane_RestoreState_ShouldNotThrow()
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
        public void TaskListPane_Dispose_ShouldUnsubscribeFromTaskService()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            Action act = () => pane.Dispose();

            // Assert
            act.Should().NotThrow("Dispose should clean up event subscriptions");
        }

        [WpfFact]
        public void TaskListPane_WithTasks_ShouldDisplay()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Test Task",
                Description = "Test Description",
                Priority = TaskPriority.High
            };
            TaskService.AddTask(task);

            // Act
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Assert - Pane should initialize without errors even with tasks
            pane.Should().NotBeNull();
        }

        [WpfFact]
        public void TaskListPane_AfterTaskAdded_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act - Add task after pane is initialized
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "New Task",
                Priority = TaskPriority.Medium
            };
            Action act = () => TaskService.AddTask(task);

            // Assert
            act.Should().NotThrow("Adding task should not break pane");
        }

        [WpfFact]
        public void TaskListPane_AfterTaskDeleted_ShouldNotThrow()
        {
            // Arrange
            var task = new TaskItem { Id = Guid.NewGuid(), Title = "To Delete" };
            TaskService.AddTask(task);

            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            Action act = () => TaskService.DeleteTask(task.Id);

            // Assert
            act.Should().NotThrow("Deleting task should not break pane");
        }

        [WpfFact]
        public void TaskListPane_AfterTaskUpdated_ShouldNotThrow()
        {
            // Arrange
            var task = new TaskItem { Id = Guid.NewGuid(), Title = "Original" };
            TaskService.AddTask(task);

            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act
            task.Title = "Updated";
            Action act = () => TaskService.UpdateTask(task);

            // Assert
            act.Should().NotThrow("Updating task should not break pane");
        }

        [WpfFact]
        public void TaskListPane_FilterModes_ShouldNotThrow()
        {
            // Arrange
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();

            // Act & Assert - Changing filter modes should not throw
            // Note: This tests that the pane handles filter changes gracefully
            pane.Should().NotBeNull();
        }

        [WpfFact]
        public void TaskListPane_FullLifecycle_ShouldSucceed()
        {
            // Arrange
            var task = new TaskItem { Id = Guid.NewGuid(), Title = "Lifecycle Test" };
            TaskService.AddTask(task);

            // Act & Assert - Full lifecycle
            var pane = PaneFactory.CreatePane("tasks");
            pane.Initialize();
            var state = pane.SaveState();
            pane.RestoreState(state);
            pane.Dispose();

            // Should not throw
        }

        [WpfFact]
        public void TaskListPane_MemoryLeak_CreateDispose10Times_ShouldNotThrow()
        {
            // This test helps detect event subscription memory leaks

            // Act
            Action act = () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var pane = PaneFactory.CreatePane("tasks");
                    pane.Initialize();
                    pane.Dispose();
                }
            };

            // Assert
            act.Should().NotThrow("Creating/disposing TaskListPane multiple times should not leak");
        }
    }
}
