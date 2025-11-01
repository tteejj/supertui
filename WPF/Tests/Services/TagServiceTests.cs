using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;
using SuperTUI.Core.Services;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Services
{
    /// <summary>
    /// Comprehensive tests for TagService
    /// Tests cover: CRUD operations, validation, normalization, search, thread-safety
    /// </summary>
    [Trait("Category", "Critical")]
    [Trait("Priority", "High")]
    [Collection("SingletonTests")] // Shared collection to prevent parallel execution with other singleton tests
    public class TagServiceTests : IDisposable
    {
        private TagService tagService;
        private TaskService taskService;
        private Logger logger;
        private ConfigurationManager config;

        public TagServiceTests()
        {
            // Use singleton instances
            logger = Logger.Instance;
            config = ConfigurationManager.Instance;

            // Initialize config with temp path
            var tempConfigPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
            config.Initialize(tempConfigPath);

            // Use singleton services
            taskService = TaskService.Instance;
            tagService = TagService.Instance;

            // Clear state from previous tests
            taskService.Clear();
            tagService.Clear();
        }

        public void Dispose()
        {
            // Cleanup
            taskService?.Clear();
        }

        // ====================================================================
        // TAG CRUD TESTS
        // ====================================================================

        [Fact]
        public void AddTagToTask_ValidTag_ShouldAddSuccessfully()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });

            // Act
            var result = tagService.AddTagToTask(task.Id, "important");

            // Assert
            result.Should().BeTrue();
            var updatedTask = taskService.GetTask(task.Id);
            updatedTask.Tags.Should().Contain("important");
        }

        [Fact]
        public void AddTagToTask_NormalizedTag_ShouldStoreTrimmed()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });

            // Act
            tagService.AddTagToTask(task.Id, "  urgent  ");

            // Assert
            var updatedTask = taskService.GetTask(task.Id);
            updatedTask.Tags.Should().Contain("urgent");
            updatedTask.Tags.Should().NotContain("  urgent  ");
        }

        [Fact]
        public void AddTagToTask_DuplicateTag_ShouldReturnFalse()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });
            tagService.AddTagToTask(task.Id, "bug");

            // Act
            var result = tagService.AddTagToTask(task.Id, "bug");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void AddTagToTask_CaseInsensitiveDuplicate_ShouldReturnFalse()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });
            tagService.AddTagToTask(task.Id, "BUG");

            // Act
            var result = tagService.AddTagToTask(task.Id, "bug");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void AddTagToTask_NonExistentTask_ShouldReturnFalse()
        {
            // Act
            var result = tagService.AddTagToTask(Guid.NewGuid(), "tag");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void AddTagToTask_MaxTagsExceeded_ShouldReturnFalse()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });

            // Add 10 tags (max)
            for (int i = 0; i < 10; i++)
            {
                tagService.AddTagToTask(task.Id, $"tag{i}");
            }

            // Act - Try to add 11th tag
            var result = tagService.AddTagToTask(task.Id, "tag11");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void RemoveTagFromTask_ExistingTag_ShouldRemoveSuccessfully()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });
            tagService.AddTagToTask(task.Id, "bug");

            // Act
            var result = tagService.RemoveTagFromTask(task.Id, "bug");

            // Assert
            result.Should().BeTrue();
            var updatedTask = taskService.GetTask(task.Id);
            updatedTask.Tags.Should().NotContain("bug");
        }

        [Fact]
        public void RemoveTagFromTask_CaseInsensitive_ShouldRemove()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });
            tagService.AddTagToTask(task.Id, "BUG");

            // Act
            var result = tagService.RemoveTagFromTask(task.Id, "bug");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void RemoveTagFromTask_NonExistentTag_ShouldReturnFalse()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });

            // Act
            var result = tagService.RemoveTagFromTask(task.Id, "nonexistent");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void SetTaskTags_ValidTags_ShouldReplaceAll()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });
            tagService.AddTagToTask(task.Id, "old1");
            tagService.AddTagToTask(task.Id, "old2");

            // Act
            var result = tagService.SetTaskTags(task.Id, new List<string> { "new1", "new2", "new3" });

            // Assert
            result.Should().BeTrue();
            var updatedTask = taskService.GetTask(task.Id);
            updatedTask.Tags.Should().HaveCount(3);
            updatedTask.Tags.Should().Contain("new1");
            updatedTask.Tags.Should().NotContain("old1");
        }

        [Fact]
        public void SetTaskTags_TooManyTags_ShouldTruncate()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });
            var tags = Enumerable.Range(0, 15).Select(i => $"tag{i}").ToList();

            // Act
            tagService.SetTaskTags(task.Id, tags);

            // Assert
            var updatedTask = taskService.GetTask(task.Id);
            updatedTask.Tags.Count.Should().BeLessOrEqualTo(10);
        }

        // ====================================================================
        // TAG VALIDATION TESTS
        // ====================================================================

        [Fact]
        public void ValidateTag_EmptyTag_ShouldReturnError()
        {
            // Act
            var error = tagService.ValidateTag("");

            // Assert
            error.Should().NotBeNullOrEmpty();
            error.Should().Contain("empty");
        }

        [Fact]
        public void ValidateTag_WhitespaceOnlyTag_ShouldReturnError()
        {
            // Act
            var error = tagService.ValidateTag("   ");

            // Assert
            error.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ValidateTag_TooLongTag_ShouldReturnError()
        {
            // Arrange
            var longTag = new string('x', 51);

            // Act
            var error = tagService.ValidateTag(longTag);

            // Assert
            error.Should().NotBeNullOrEmpty();
            error.Should().Contain("too long");
        }

        [Fact]
        public void ValidateTag_WithSpaces_ShouldReturnError()
        {
            // Act
            var error = tagService.ValidateTag("invalid tag");

            // Assert
            error.Should().NotBeNullOrEmpty();
            error.Should().Contain("invalid characters");
        }

        [Fact]
        public void ValidateTag_WithComma_ShouldReturnError()
        {
            // Act
            var error = tagService.ValidateTag("tag,name");

            // Assert
            error.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ValidateTag_WithSemicolon_ShouldReturnError()
        {
            // Act
            var error = tagService.ValidateTag("tag;name");

            // Assert
            error.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ValidateTag_WithPipe_ShouldReturnError()
        {
            // Act
            var error = tagService.ValidateTag("tag|name");

            // Assert
            error.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ValidateTag_ValidTag_ShouldReturnNull()
        {
            // Act
            var error = tagService.ValidateTag("valid-tag");

            // Assert
            error.Should().BeNull();
        }

        [Fact]
        public void AddTagToTask_InvalidTag_ShouldReturnFalse()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Test Task" });

            // Act
            var result = tagService.AddTagToTask(task.Id, "invalid tag");

            // Assert
            result.Should().BeFalse();
        }

        // ====================================================================
        // TAG QUERY TESTS
        // ====================================================================

        [Fact]
        public void GetAllTags_WithMultipleTags_ShouldReturnAll()
        {
            // Arrange
            var task1 = taskService.AddTask(new TaskItem { Title = "Task 1" });
            var task2 = taskService.AddTask(new TaskItem { Title = "Task 2" });
            tagService.AddTagToTask(task1.Id, "bug");
            tagService.AddTagToTask(task2.Id, "feature");
            tagService.AddTagToTask(task2.Id, "urgent");

            // Act
            var allTags = tagService.GetAllTags();

            // Assert
            allTags.Should().HaveCount(3);
            allTags.Should().Contain("bug");
            allTags.Should().Contain("feature");
            allTags.Should().Contain("urgent");
        }

        [Fact]
        public void GetTagsByUsage_ShouldReturnMostUsedFirst()
        {
            // Arrange
            var task1 = taskService.AddTask(new TaskItem { Title = "Task 1" });
            var task2 = taskService.AddTask(new TaskItem { Title = "Task 2" });
            var task3 = taskService.AddTask(new TaskItem { Title = "Task 3" });

            tagService.AddTagToTask(task1.Id, "bug");
            tagService.AddTagToTask(task2.Id, "bug");
            tagService.AddTagToTask(task3.Id, "bug");
            tagService.AddTagToTask(task1.Id, "feature");

            // Act
            var tagsByUsage = tagService.GetTagsByUsage();

            // Assert
            tagsByUsage.Should().NotBeEmpty();
            tagsByUsage[0].Name.Should().Be("bug");
            tagsByUsage[0].UsageCount.Should().Be(3);
        }

        [Fact]
        public void GetRecentTags_ShouldReturnLatestUsed()
        {
            // Arrange
            var task1 = taskService.AddTask(new TaskItem { Title = "Task 1" });
            var task2 = taskService.AddTask(new TaskItem { Title = "Task 2" });

            tagService.AddTagToTask(task1.Id, "old-tag");
            System.Threading.Thread.Sleep(500); // Increased from 100ms to ensure proper ordering even when tests run together
            tagService.AddTagToTask(task2.Id, "new-tag");

            // Act
            var recentTags = tagService.GetRecentTags();

            // Assert
            recentTags.Should().NotBeEmpty();
            // Both tags should be present
            var oldTagIndex = recentTags.FindIndex(t => t.Name == "old-tag");
            var newTagIndex = recentTags.FindIndex(t => t.Name == "new-tag");

            oldTagIndex.Should().BeGreaterOrEqualTo(0, "old-tag should be in recent tags");
            newTagIndex.Should().BeGreaterOrEqualTo(0, "new-tag should be in recent tags");

            // new-tag should come BEFORE old-tag (lower index = more recent)
            newTagIndex.Should().BeLessThan(oldTagIndex, "new-tag should be more recent than old-tag");
        }

        [Fact]
        public void GetTagSuggestions_WithPrefix_ShouldReturnMatches()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "bug-fix");
            tagService.AddTagToTask(task.Id, "bug-report");
            tagService.AddTagToTask(task.Id, "feature");

            // Act
            var suggestions = tagService.GetTagSuggestions("bug");

            // Assert
            suggestions.Should().HaveCount(2);
            suggestions.Should().Contain("bug-fix");
            suggestions.Should().Contain("bug-report");
        }

        [Fact]
        public void GetTagSuggestions_EmptyPrefix_ShouldReturnRecentTags()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "tag1");
            tagService.AddTagToTask(task.Id, "tag2");

            // Act
            var suggestions = tagService.GetTagSuggestions("");

            // Assert
            suggestions.Should().NotBeEmpty();
        }

        [Fact]
        public void GetTasksByTag_ExistingTag_ShouldReturnMatchingTasks()
        {
            // Arrange
            var task1 = taskService.AddTask(new TaskItem { Title = "Task 1" });
            var task2 = taskService.AddTask(new TaskItem { Title = "Task 2" });
            var task3 = taskService.AddTask(new TaskItem { Title = "Task 3" });

            tagService.AddTagToTask(task1.Id, "bug");
            tagService.AddTagToTask(task2.Id, "bug");
            tagService.AddTagToTask(task3.Id, "feature");

            // Act
            var tasks = tagService.GetTasksByTag("bug");

            // Assert
            tasks.Should().HaveCount(2);
            tasks.Should().Contain(t => t.Id == task1.Id);
            tasks.Should().Contain(t => t.Id == task2.Id);
        }

        [Fact]
        public void GetTasksByTag_CaseInsensitive_ShouldReturnMatches()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "BUG");

            // Act
            var tasks = tagService.GetTasksByTag("bug");

            // Assert
            tasks.Should().ContainSingle();
        }

        [Fact]
        public void GetTasksByTags_RequireAll_ShouldReturnOnlyTasksWithAllTags()
        {
            // Arrange
            var task1 = taskService.AddTask(new TaskItem { Title = "Task 1" });
            var task2 = taskService.AddTask(new TaskItem { Title = "Task 2" });

            tagService.AddTagToTask(task1.Id, "bug");
            tagService.AddTagToTask(task1.Id, "urgent");
            tagService.AddTagToTask(task2.Id, "bug");

            // Act
            var tasks = tagService.GetTasksByTags(new List<string> { "bug", "urgent" }, requireAll: true);

            // Assert
            tasks.Should().ContainSingle();
            tasks[0].Id.Should().Be(task1.Id);
        }

        [Fact]
        public void GetTasksByTags_RequireAny_ShouldReturnTasksWithAnyTag()
        {
            // Arrange
            var task1 = taskService.AddTask(new TaskItem { Title = "Task 1" });
            var task2 = taskService.AddTask(new TaskItem { Title = "Task 2" });

            tagService.AddTagToTask(task1.Id, "bug");
            tagService.AddTagToTask(task2.Id, "feature");

            // Act
            var tasks = tagService.GetTasksByTags(new List<string> { "bug", "feature" }, requireAll: false);

            // Assert
            tasks.Should().HaveCount(2);
        }

        [Fact]
        public void GetTagInfo_ExistingTag_ShouldReturnInfo()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "important");

            // Act
            var info = tagService.GetTagInfo("important");

            // Assert
            info.Should().NotBeNull();
            info.Name.Should().Be("important");
            info.UsageCount.Should().Be(1);
        }

        [Fact]
        public void GetTagInfo_NonExistentTag_ShouldReturnNull()
        {
            // Act
            var info = tagService.GetTagInfo("nonexistent");

            // Assert
            info.Should().BeNull();
        }

        // ====================================================================
        // TAG MANAGEMENT TESTS
        // ====================================================================

        [Fact]
        public void RenameTag_ExistingTag_ShouldRenameAcrossAllTasks()
        {
            // Arrange
            var task1 = taskService.AddTask(new TaskItem { Title = "Task 1" });
            var task2 = taskService.AddTask(new TaskItem { Title = "Task 2" });
            tagService.AddTagToTask(task1.Id, "typo");
            tagService.AddTagToTask(task2.Id, "typo");

            // Act
            var count = tagService.RenameTag("typo", "fixed");

            // Assert
            count.Should().Be(2);
            var tasks = tagService.GetTasksByTag("fixed");
            tasks.Should().HaveCount(2);
            tagService.GetTasksByTag("typo").Should().BeEmpty();
        }

        [Fact]
        public void RenameTag_SameName_ShouldReturnZero()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "tag");

            // Act
            var count = tagService.RenameTag("tag", "tag");

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public void RenameTag_InvalidNewTag_ShouldReturnZero()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "old");

            // Act
            var count = tagService.RenameTag("old", "invalid tag");

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public void DeleteTag_ExistingTag_ShouldRemoveFromAllTasks()
        {
            // Arrange
            var task1 = taskService.AddTask(new TaskItem { Title = "Task 1" });
            var task2 = taskService.AddTask(new TaskItem { Title = "Task 2" });
            tagService.AddTagToTask(task1.Id, "obsolete");
            tagService.AddTagToTask(task2.Id, "obsolete");

            // Act
            var count = tagService.DeleteTag("obsolete");

            // Assert
            count.Should().Be(2);
            tagService.GetTasksByTag("obsolete").Should().BeEmpty();
        }

        [Fact]
        public void MergeTags_ShouldReplaceSourceWithTarget()
        {
            // Arrange
            var task1 = taskService.AddTask(new TaskItem { Title = "Task 1" });
            var task2 = taskService.AddTask(new TaskItem { Title = "Task 2" });
            tagService.AddTagToTask(task1.Id, "defect");
            tagService.AddTagToTask(task2.Id, "bug");

            // Act
            var count = tagService.MergeTags("defect", "bug");

            // Assert
            count.Should().Be(1);
            tagService.GetTasksByTag("bug").Should().HaveCount(2);
            tagService.GetTasksByTag("defect").Should().BeEmpty();
        }

        // ====================================================================
        // INDEX MANAGEMENT TESTS
        // ====================================================================

        [Fact]
        public void RebuildTagIndex_ShouldRecreateIndex()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "tag1");
            tagService.AddTagToTask(task.Id, "tag2");

            // Act
            tagService.RebuildTagIndex();

            // Assert
            tagService.GetAllTags().Should().HaveCount(2);
        }

        [Fact]
        public void RebuildTagIndex_AfterManualTagModification_ShouldSync()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "tag1");

            // Manually modify task tags (simulating direct DB modification)
            task.Tags.Add("manual-tag");
            taskService.UpdateTask(task);

            // Act
            tagService.RebuildTagIndex();

            // Assert
            tagService.GetAllTags().Should().Contain("manual-tag");
        }

        // ====================================================================
        // THREAD-SAFETY TESTS
        // ====================================================================

        [Fact]
        public void AddTagToTask_Concurrent_ShouldHandleAllRequests()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            var tasks = new System.Threading.Tasks.Task[10];

            // Act - Multiple threads adding tags simultaneously
            for (int i = 0; i < 10; i++)
            {
                int tagIndex = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    tagService.AddTagToTask(task.Id, $"tag{tagIndex}");
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);

            // Assert
            var updatedTask = taskService.GetTask(task.Id);
            updatedTask.Tags.Count.Should().Be(10);
        }

        [Fact]
        public void GetTasksByTag_WhileModifying_ShouldNotThrow()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "concurrent");

            var readTask = System.Threading.Tasks.Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    tagService.GetTasksByTag("concurrent");
                }
            });

            var writeTask = System.Threading.Tasks.Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    tagService.AddTagToTask(task.Id, $"tag{i}");
                }
            });

            // Act & Assert - Should not throw
            Action action = () => System.Threading.Tasks.Task.WaitAll(readTask, writeTask);
            action.Should().NotThrow();
        }

        // ====================================================================
        // EDGE CASE TESTS
        // ====================================================================

        [Fact]
        public void AddTagToTask_NullTag_ShouldReturnFalse()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });

            // Act
            var result = tagService.AddTagToTask(task.Id, null);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetTasksByTags_EmptyList_ShouldReturnEmpty()
        {
            // Act
            var tasks = tagService.GetTasksByTags(new List<string>());

            // Assert
            tasks.Should().BeEmpty();
        }

        [Fact]
        public void GetTasksByTags_NullList_ShouldReturnEmpty()
        {
            // Act
            var tasks = tagService.GetTasksByTags(null);

            // Assert
            tasks.Should().BeEmpty();
        }

        [Fact]
        public void GetTagSuggestions_NullPrefix_ShouldReturnRecentTags()
        {
            // Arrange
            var task = taskService.AddTask(new TaskItem { Title = "Task" });
            tagService.AddTagToTask(task.Id, "tag1");

            // Act
            var suggestions = tagService.GetTagSuggestions(null);

            // Assert
            suggestions.Should().NotBeEmpty();
        }
    }
}
