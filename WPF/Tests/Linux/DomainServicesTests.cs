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
            container = DI.ServiceRegistration.RegisterAllServices();

            taskService = container.GetService<ITaskService>();
            projectService = container.GetService<IProjectService>();
            timeTrackingService = container.GetService<ITimeTrackingService>();
        }

        public void Dispose()
        {
            // Container disposal handled by test fixture;
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
        public void ProjectService_DeleteProject_ShouldDelete()
        {
            // Arrange
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "To Be Deleted"
            };
            projectService.AddProject(project);

            // Act
            projectService.DeleteProject(project.Id);
            var retrieved = projectService.GetProject(project.Id);

            // Assert
            retrieved.Should().BeNull();
        }

        #endregion

        #region TimeTrackingService Tests

        [Fact]
        public void TimeTrackingService_AddEntry_ShouldSucceed()
        {
            // Arrange
            var entry = new TimeEntry
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                WeekEnding = DateTime.Now,
                Hours = 8.5m
            };

            // Act
            var result = timeTrackingService.AddEntry(entry);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(entry.Id);
        }

        [Fact]
        public void TimeTrackingService_GetAllEntries_ShouldReturnList()
        {
            // Act
            var entries = timeTrackingService.GetAllEntries();

            // Assert
            entries.Should().NotBeNull();
            entries.Should().BeOfType<System.Collections.Generic.List<TimeEntry>>();
        }

        [Fact]
        public void TimeTrackingService_AddAndRetrieve_ShouldPersist()
        {
            // Arrange
            var entry = new TimeEntry
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                WeekEnding = DateTime.Now,
                Hours = 5.0m
            };

            // Act
            timeTrackingService.AddEntry(entry);
            var retrieved = timeTrackingService.GetEntry(entry.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Id.Should().Be(entry.Id);
            retrieved.Hours.Should().Be(5.0m);
        }

        [Fact]
        public void TimeTrackingService_GetEntriesForProject_ShouldFilter()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var entry1 = new TimeEntry { Id = Guid.NewGuid(), ProjectId = projectId, Hours = 8.0m };
            var entry2 = new TimeEntry { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Hours = 4.0m };

            timeTrackingService.AddEntry(entry1);
            timeTrackingService.AddEntry(entry2);

            // Act
            var projectEntries = timeTrackingService.GetEntriesForProject(projectId);

            // Assert
            projectEntries.Should().Contain(e => e.Id == entry1.Id);
            projectEntries.Should().NotContain(e => e.Id == entry2.Id);
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
            var project = new Project { Id = Guid.NewGuid(), Name = "Test Project" };
            projectService.AddProject(project);

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Tracked Task",
                ProjectId = project.Id
            };
            taskService.AddTask(task);

            // Act
            var entry = new TimeEntry
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Hours = 8.0m,
                WeekEnding = DateTime.Now
            };
            timeTrackingService.AddEntry(entry);

            var projectEntries = timeTrackingService.GetEntriesForProject(project.Id);

            // Assert
            projectEntries.Should().Contain(e => e.Id == entry.Id);
        }

        #endregion
    }
}
