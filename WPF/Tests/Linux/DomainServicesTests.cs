using System;
using System.Linq;
using FluentAssertions;
using SuperTUI.Infrastructure;
using SuperTUI.Core.Services;
using SuperTUI.Core.Models;
using Xunit;
using DI = SuperTUI.DI;

namespace SuperTUI.Tests.Linux
{
    /// <summary>
    /// Tests for domain services - uses ACTUAL interface methods that exist
    /// These tests run on Linux WITHOUT WPF dependencies
    /// </summary>
    [Trait("Category", "Linux")]
    [Trait("Category", "Critical")]
    public class DomainServicesTests : IDisposable
    {
        private readonly DI.ServiceContainer container;
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly ITimeTrackingService timeTrackingService;

        public DomainServicesTests()
        {
            container = new DI.ServiceContainer();
            DI.ServiceRegistration.RegisterServices(container);

            taskService = container.Resolve<ITaskService>();
            projectService = container.Resolve<IProjectService>();
            timeTrackingService = container.Resolve<ITimeTrackingService>();
        }

        public void Dispose()
        {
            container.Clear();
        }

        #region TaskService Tests

        [Fact]
        public void TaskService_AddTask_ShouldSucceed()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Test Task",
                Description = "Test Description",
                Priority = TaskPriority.Medium
            };

            // Act
            var result = taskService.AddTask(task);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(task.Id);
        }

        [Fact]
        public void TaskService_GetAllTasks_ShouldReturnList()
        {
            // Act
            var tasks = taskService.GetAllTasks();

            // Assert
            tasks.Should().NotBeNull();
            tasks.Should().BeOfType<System.Collections.Generic.List<TaskItem>>();
        }

        [Fact]
        public void TaskService_AddAndRetrieve_ShouldPersist()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Persistent Task",
                Priority = TaskPriority.High
            };

            // Act
            taskService.AddTask(task);
            var retrieved = taskService.GetTask(task.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Id.Should().Be(task.Id);
            retrieved.Title.Should().Be(task.Title);
        }

        [Fact]
        public void TaskService_UpdateTask_ShouldModify()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Priority = TaskPriority.Low
            };
            taskService.AddTask(task);

            // Act
            task.Title = "Updated Title";
            task.Priority = TaskPriority.High;
            var success = taskService.UpdateTask(task);

            var updated = taskService.GetTask(task.Id);

            // Assert
            success.Should().BeTrue();
            updated.Title.Should().Be("Updated Title");
            updated.Priority.Should().Be(TaskPriority.High);
        }

        [Fact]
        public void TaskService_DeleteTask_ShouldRemove()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "To Be Deleted"
            };
            taskService.AddTask(task);

            // Act
            var success = taskService.DeleteTask(task.Id);
            var retrieved = taskService.GetTask(task.Id);

            // Assert
            success.Should().BeTrue();
            retrieved.Should().BeNull();
        }

        [Fact]
        public void TaskService_GetTasksForProject_ShouldFilter()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var task1 = new TaskItem { Id = Guid.NewGuid(), Title = "Task 1", ProjectId = projectId };
            var task2 = new TaskItem { Id = Guid.NewGuid(), Title = "Task 2", ProjectId = projectId };
            var task3 = new TaskItem { Id = Guid.NewGuid(), Title = "Task 3", ProjectId = Guid.NewGuid() };

            taskService.AddTask(task1);
            taskService.AddTask(task2);
            taskService.AddTask(task3);

            // Act
            var projectTasks = taskService.GetTasksForProject(projectId);

            // Assert
            projectTasks.Should().HaveCount(2);
            projectTasks.Should().Contain(t => t.Id == task1.Id);
            projectTasks.Should().Contain(t => t.Id == task2.Id);
            projectTasks.Should().NotContain(t => t.Id == task3.Id);
        }

        [Fact]
        public void TaskService_GetTasks_WithPredicate_ShouldFilter()
        {
            // Arrange
            var highTask = new TaskItem { Id = Guid.NewGuid(), Title = "High", Priority = TaskPriority.High };
            var medTask = new TaskItem { Id = Guid.NewGuid(), Title = "Med", Priority = TaskPriority.Medium };

            taskService.AddTask(highTask);
            taskService.AddTask(medTask);

            // Act
            var highTasks = taskService.GetTasks(t => t.Priority == TaskPriority.High);

            // Assert
            highTasks.Should().Contain(t => t.Id == highTask.Id);
            highTasks.Should().NotContain(t => t.Id == medTask.Id);
        }

        [Fact]
        public void TaskService_ToggleCompletion_ShouldWork()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Toggle Me",
                Status = TaskStatus.InProgress
            };
            taskService.AddTask(task);

            // Act
            taskService.ToggleTaskCompletion(task.Id);
            var updated = taskService.GetTask(task.Id);

            // Assert
            updated.Status.Should().Be(TaskStatus.Completed);
        }

        #endregion

        #region ProjectService Tests

        [Fact]
        public void ProjectService_AddProject_ShouldSucceed()
        {
            // Arrange
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Test Project",
                Description = "Test Description"
            };

            // Act
            projectService.AddProject(project);

            // Assert - method is void, so just ensure no exception
        }

        [Fact]
        public void ProjectService_GetAllProjects_ShouldReturnList()
        {
            // Act
            var projects = projectService.GetAllProjects();

            // Assert
            projects.Should().NotBeNull();
            projects.Should().BeOfType<System.Collections.Generic.List<Project>>();
        }

        [Fact]
        public void ProjectService_AddAndRetrieve_ShouldPersist()
        {
            // Arrange
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Persistent Project"
            };

            // Act
            projectService.AddProject(project);
            var retrieved = projectService.GetProject(project.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Id.Should().Be(project.Id);
            retrieved.Name.Should().Be(project.Name);
        }

        [Fact]
        public void ProjectService_UpdateProject_ShouldModify()
        {
            // Arrange
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Original Name"
            };
            projectService.AddProject(project);

            // Act
            project.Name = "Updated Name";
            projectService.UpdateProject(project);

            var updated = projectService.GetProject(project.Id);

            // Assert
            updated.Name.Should().Be("Updated Name");
        }

        [Fact]
        public void ProjectService_RemoveProject_ShouldDelete()
        {
            // Arrange
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "To Be Deleted"
            };
            projectService.AddProject(project);

            // Act
            projectService.RemoveProject(project.Id);
            var retrieved = projectService.GetProject(project.Id);

            // Assert
            retrieved.Should().BeNull();
        }

        #endregion

        #region TimeTrackingService Tests

        [Fact]
        public void TimeTrackingService_Start_ShouldSucceed()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var description = "Testing time tracking";

            // Act
            Action act = () => timeTrackingService.Start(taskId, description);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void TimeTrackingService_Stop_ShouldSucceed()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            timeTrackingService.Start(taskId, "Test");

            // Act
            Action act = () => timeTrackingService.Stop();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void TimeTrackingService_GetCurrent_ShouldReturnNullWhenNotTracking()
        {
            // Act
            var active = timeTrackingService.GetCurrent();

            // Assert
            active.Should().BeNull();
        }

        [Fact]
        public void TimeTrackingService_GetCurrent_ShouldReturnEntryWhenTracking()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            timeTrackingService.Start(taskId, "Test tracking");

            // Act
            var active = timeTrackingService.GetCurrent();

            // Assert
            active.Should().NotBeNull();
            active.TaskId.Should().Be(taskId);
        }

        [Fact]
        public void TimeTrackingService_GetEntriesForTask_ShouldReturnList()
        {
            // Arrange
            var taskId = Guid.NewGuid();

            // Act
            var entries = timeTrackingService.GetEntriesForTask(taskId);

            // Assert
            entries.Should().NotBeNull();
            entries.Should().BeOfType<System.Collections.Generic.List<TimeEntry>>();
        }

        [Fact]
        public void TimeTrackingService_GetAllEntries_ShouldReturnList()
        {
            // Act
            var entries = timeTrackingService.GetAllEntries();

            // Assert
            entries.Should().NotBeNull();
        }

        #endregion

        #region Cross-Service Integration Tests

        [Fact]
        public void TaskAndProject_Integration_ShouldWork()
        {
            // Arrange
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Integration Project"
            };
            projectService.AddProject(project);

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Integration Task",
                ProjectId = project.Id
            };

            // Act
            taskService.AddTask(task);
            var projectTasks = taskService.GetTasksForProject(project.Id);

            // Assert
            projectTasks.Should().Contain(t => t.Id == task.Id);
        }

        [Fact]
        public void TaskAndTimeTracking_Integration_ShouldWork()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Tracked Task"
            };
            taskService.AddTask(task);

            // Act
            timeTrackingService.Start(task.Id, "Working on task");
            var active = timeTrackingService.GetCurrent();

            // Assert
            active.Should().NotBeNull();
            active.TaskId.Should().Be(task.Id);
        }

        #endregion
    }
}
