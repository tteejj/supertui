using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using SuperTUI.Core.Services;
using SuperTUI.Core.Models;

namespace SuperTUI.Tests.Services
{
    /// <summary>
    /// Comprehensive tests for TimeTrackingService
    /// Tests cover: CRUD operations, week calculations, aggregations, persistence, thread-safety
    /// </summary>
    [Trait("Category", "Critical")]
    [Trait("Priority", "High")]
    [Collection("SingletonTests")] // Shared collection to prevent parallel execution with other singleton tests
    public class TimeTrackingServiceTests : IDisposable
    {
        private TimeTrackingService service;
        private string testDataFile;

        public TimeTrackingServiceTests()
        {
            service = TimeTrackingService.Instance;
            testDataFile = Path.Combine(Path.GetTempPath(), $"timetracking_test_{Guid.NewGuid()}.json");
            service.Clear(); // Clear state from previous tests
            service.Initialize(testDataFile);
        }

        public void Dispose()
        {
            // DO NOT dispose singleton service - just clear its state
            // Disposing a singleton breaks subsequent tests
            service?.Clear();

            try
            {
                if (File.Exists(testDataFile))
                    File.Delete(testDataFile);

                // Cleanup backup files
                var directory = Path.GetDirectoryName(testDataFile);
                var backups = Directory.GetFiles(directory, "timetracking_test_*.bak");
                foreach (var backup in backups)
                {
                    try { File.Delete(backup); } catch { }
                }
            }
            catch { }
        }

        // ====================================================================
        // WEEK CALCULATION TESTS
        // ====================================================================

        [Fact]
        public void GetWeekEnding_Sunday_ShouldReturnSameDay()
        {
            // Arrange - Create a Sunday
            var sunday = new DateTime(2025, 11, 2); // This is a Sunday

            // Act
            var weekEnding = TimeTrackingService.GetWeekEnding(sunday);

            // Assert
            weekEnding.Should().Be(sunday.Date);
        }

        [Fact]
        public void GetWeekEnding_Monday_ShouldReturnNextSunday()
        {
            // Arrange
            var monday = new DateTime(2025, 10, 27); // Monday

            // Act
            var weekEnding = TimeTrackingService.GetWeekEnding(monday);

            // Assert
            weekEnding.DayOfWeek.Should().Be(DayOfWeek.Sunday);
            weekEnding.Should().BeAfter(monday);
        }

        [Fact]
        public void GetWeekEnding_Saturday_ShouldReturnNextDay()
        {
            // Arrange
            var saturday = new DateTime(2025, 11, 1); // Saturday

            // Act
            var weekEnding = TimeTrackingService.GetWeekEnding(saturday);

            // Assert
            weekEnding.DayOfWeek.Should().Be(DayOfWeek.Sunday);
            weekEnding.Should().Be(saturday.AddDays(1).Date);
        }

        [Fact]
        public void GetWeekStart_ShouldReturnMonday6DaysBefore()
        {
            // Arrange
            var sunday = new DateTime(2025, 11, 2); // Sunday

            // Act
            var weekStart = TimeTrackingService.GetWeekStart(sunday);

            // Assert
            weekStart.DayOfWeek.Should().Be(DayOfWeek.Monday);
            weekStart.Should().Be(sunday.AddDays(-6));
        }

        [Fact]
        public void GetFiscalYear_April_ShouldReturnNextYear()
        {
            // Arrange - Fiscal year starts April 1
            var aprilDate = new DateTime(2025, 4, 15);

            // Act
            var fiscalYear = TimeTrackingService.GetFiscalYear(aprilDate);

            // Assert
            fiscalYear.Should().Be(2026); // FY 2026
        }

        [Fact]
        public void GetFiscalYear_March_ShouldReturnCurrentYear()
        {
            // Arrange
            var marchDate = new DateTime(2025, 3, 15);

            // Act
            var fiscalYear = TimeTrackingService.GetFiscalYear(marchDate);

            // Assert
            fiscalYear.Should().Be(2025); // Still FY 2025
        }

        [Fact]
        public void GetFiscalYearStart_ShouldReturnApril1()
        {
            // Act
            var fyStart = TimeTrackingService.GetFiscalYearStart(2026);

            // Assert
            fyStart.Should().Be(new DateTime(2025, 4, 1));
        }

        [Fact]
        public void GetFiscalYearEnd_ShouldReturnMarch31()
        {
            // Act
            var fyEnd = TimeTrackingService.GetFiscalYearEnd(2026);

            // Assert
            fyEnd.Should().Be(new DateTime(2026, 3, 31));
        }

        // ====================================================================
        // CRUD OPERATION TESTS
        // ====================================================================

        [Fact]
        public void AddEntry_ValidEntry_ShouldAddSuccessfully()
        {
            // Arrange
            var entry = new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 40.0m
            };

            // Act
            var result = service.AddEntry(entry);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().NotBeEmpty();
            result.Hours.Should().Be(40.0m);
        }

        [Fact]
        public void AddEntry_ShouldNormalizeWeekEndingToSunday()
        {
            // Arrange - Use a Monday
            var monday = new DateTime(2025, 10, 27);
            var entry = new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = monday,
                Hours = 20.0m
            };

            // Act
            var result = service.AddEntry(entry);

            // Assert
            result.WeekEnding.DayOfWeek.Should().Be(DayOfWeek.Sunday);
        }

        [Fact]
        public void GetEntry_ExistingEntry_ShouldReturn()
        {
            // Arrange
            var added = service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 15.0m
            });

            // Act
            var retrieved = service.GetEntry(added.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Id.Should().Be(added.Id);
        }

        [Fact]
        public void GetEntry_NonExistentEntry_ShouldReturnNull()
        {
            // Act
            var result = service.GetEntry(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void UpdateEntry_ExistingEntry_ShouldUpdate()
        {
            // Arrange
            var entry = service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 10.0m
            });

            // Act
            entry.Hours = 25.0m;
            var result = service.UpdateEntry(entry);

            // Assert
            result.Should().BeTrue();
            var updated = service.GetEntry(entry.Id);
            updated.Hours.Should().Be(25.0m);
        }

        [Fact]
        public void UpdateEntry_NonExistentEntry_ShouldReturnFalse()
        {
            // Arrange
            var fakeEntry = new TimeEntry
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 10.0m
            };

            // Act
            var result = service.UpdateEntry(fakeEntry);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void UpdateEntry_ChangeWeekEnding_ShouldUpdateIndex()
        {
            // Arrange
            var week1 = TimeTrackingService.GetCurrentWeekEnding();
            var week2 = week1.AddDays(7);

            var entry = service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = week1,
                Hours = 20.0m
            });

            // Act
            entry.WeekEnding = week2;
            service.UpdateEntry(entry);

            // Assert
            var week1Entries = service.GetEntriesForWeek(week1);
            var week2Entries = service.GetEntriesForWeek(week2);

            week1Entries.Should().BeEmpty();
            week2Entries.Should().ContainSingle();
        }

        [Fact]
        public void DeleteEntry_SoftDelete_ShouldMarkAsDeleted()
        {
            // Arrange
            var entry = service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 30.0m
            });

            // Act
            var result = service.DeleteEntry(entry.Id, hardDelete: false);

            // Assert
            result.Should().BeTrue();
            var deleted = service.GetEntry(entry.Id);
            deleted.Should().NotBeNull();
            deleted.Deleted.Should().BeTrue();
        }

        [Fact]
        public void DeleteEntry_HardDelete_ShouldRemoveCompletely()
        {
            // Arrange
            var entry = service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 30.0m
            });

            // Act
            var result = service.DeleteEntry(entry.Id, hardDelete: true);

            // Assert
            result.Should().BeTrue();
            var deleted = service.GetEntry(entry.Id);
            deleted.Should().BeNull();
        }

        [Fact]
        public void DeleteEntry_NonExistentEntry_ShouldReturnFalse()
        {
            // Act
            var result = service.DeleteEntry(Guid.NewGuid());

            // Assert
            result.Should().BeFalse();
        }

        // ====================================================================
        // QUERY TESTS
        // ====================================================================

        [Fact]
        public void GetAllEntries_ExcludeDeleted_ShouldNotIncludeDeleted()
        {
            // Arrange
            var entry1 = service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 10.0m
            });

            var entry2 = service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 20.0m
            });

            service.DeleteEntry(entry2.Id);

            // Act
            var entries = service.GetAllEntries(includeDeleted: false);

            // Assert
            entries.Should().HaveCount(1);
            entries[0].Id.Should().Be(entry1.Id);
        }

        [Fact]
        public void GetEntriesForWeek_ShouldReturnOnlyThatWeek()
        {
            // Arrange
            var week1 = TimeTrackingService.GetCurrentWeekEnding();
            var week2 = week1.AddDays(7);

            service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = week1,
                Hours = 15.0m
            });

            service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = week2,
                Hours = 25.0m
            });

            // Act
            var week1Entries = service.GetEntriesForWeek(week1);

            // Assert
            week1Entries.Should().ContainSingle();
            week1Entries[0].WeekEnding.Should().Be(week1);
        }

        [Fact]
        public void GetEntriesForProject_ShouldReturnOnlyThatProject()
        {
            // Arrange
            var project1 = Guid.NewGuid();
            var project2 = Guid.NewGuid();
            var weekEnding = TimeTrackingService.GetCurrentWeekEnding();

            service.AddEntry(new TimeEntry { ProjectId = project1, WeekEnding = weekEnding, Hours = 10.0m });
            service.AddEntry(new TimeEntry { ProjectId = project2, WeekEnding = weekEnding, Hours = 20.0m });
            service.AddEntry(new TimeEntry { ProjectId = project1, WeekEnding = weekEnding.AddDays(7), Hours = 15.0m });

            // Act
            var project1Entries = service.GetEntriesForProject(project1);

            // Assert
            project1Entries.Should().HaveCount(2);
            project1Entries.Should().OnlyContain(e => e.ProjectId == project1);
        }

        [Fact]
        public void GetTimeEntriesForTask_ShouldReturnOnlyThatTask()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var weekEnding = TimeTrackingService.GetCurrentWeekEnding();

            service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                TaskId = taskId,
                WeekEnding = weekEnding,
                Hours = 5.0m
            });

            service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                TaskId = Guid.NewGuid(),
                WeekEnding = weekEnding,
                Hours = 10.0m
            });

            // Act
            var taskEntries = service.GetTimeEntriesForTask(taskId);

            // Assert
            taskEntries.Should().ContainSingle();
            taskEntries[0].TaskId.Should().Be(taskId);
        }

        [Fact]
        public void GetEntryForProjectAndWeek_ExactMatch_ShouldReturn()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var weekEnding = TimeTrackingService.GetCurrentWeekEnding();

            service.AddEntry(new TimeEntry
            {
                ProjectId = projectId,
                WeekEnding = weekEnding,
                Hours = 35.0m
            });

            // Act
            var entry = service.GetEntryForProjectAndWeek(projectId, weekEnding);

            // Assert
            entry.Should().NotBeNull();
            entry.ProjectId.Should().Be(projectId);
            entry.WeekEnding.Should().Be(weekEnding);
        }

        [Fact]
        public void GetEntryForProjectAndWeek_NoMatch_ShouldReturnNull()
        {
            // Act
            var entry = service.GetEntryForProjectAndWeek(Guid.NewGuid(), TimeTrackingService.GetCurrentWeekEnding());

            // Assert
            entry.Should().BeNull();
        }

        // ====================================================================
        // AGGREGATION TESTS
        // ====================================================================

        [Fact]
        public void GetProjectTotalHours_MultipleEntries_ShouldSum()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var weekEnding = TimeTrackingService.GetCurrentWeekEnding();

            service.AddEntry(new TimeEntry { ProjectId = projectId, WeekEnding = weekEnding, Hours = 10.0m });
            service.AddEntry(new TimeEntry { ProjectId = projectId, WeekEnding = weekEnding.AddDays(7), Hours = 15.0m });
            service.AddEntry(new TimeEntry { ProjectId = projectId, WeekEnding = weekEnding.AddDays(14), Hours = 20.0m });

            // Act
            var total = service.GetProjectTotalHours(projectId);

            // Assert
            total.Should().Be(45.0m);
        }

        [Fact]
        public void GetWeekTotalHours_MultipleProjects_ShouldSum()
        {
            // Arrange
            var weekEnding = TimeTrackingService.GetCurrentWeekEnding();

            service.AddEntry(new TimeEntry { ProjectId = Guid.NewGuid(), WeekEnding = weekEnding, Hours = 20.0m });
            service.AddEntry(new TimeEntry { ProjectId = Guid.NewGuid(), WeekEnding = weekEnding, Hours = 15.0m });
            service.AddEntry(new TimeEntry { ProjectId = Guid.NewGuid(), WeekEnding = weekEnding, Hours = 10.0m });

            // Act
            var total = service.GetWeekTotalHours(weekEnding);

            // Assert
            total.Should().Be(45.0m);
        }

        [Fact]
        public void GetWeeklyReport_ShouldReturnAllEntriesForWeek()
        {
            // Arrange
            var weekEnding = TimeTrackingService.GetCurrentWeekEnding();

            service.AddEntry(new TimeEntry { ProjectId = Guid.NewGuid(), WeekEnding = weekEnding, Hours = 10.0m });
            service.AddEntry(new TimeEntry { ProjectId = Guid.NewGuid(), WeekEnding = weekEnding, Hours = 20.0m });

            // Act
            var report = service.GetWeeklyReport(weekEnding);

            // Assert
            report.Should().NotBeNull();
            report.WeekEnding.Should().Be(weekEnding);
            report.Entries.Should().HaveCount(2);
        }

        [Fact]
        public void GetProjectAggregate_WithDateRange_ShouldFilterByDates()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var week1 = new DateTime(2025, 10, 27);
            var week2 = week1.AddDays(7);
            var week3 = week2.AddDays(7);

            service.AddEntry(new TimeEntry { ProjectId = projectId, WeekEnding = week1, Hours = 10.0m });
            service.AddEntry(new TimeEntry { ProjectId = projectId, WeekEnding = week2, Hours = 20.0m });
            service.AddEntry(new TimeEntry { ProjectId = projectId, WeekEnding = week3, Hours = 30.0m });

            // Act - Only include week1 and week2
            var aggregate = service.GetProjectAggregate(projectId, week1, week2);

            // Assert
            aggregate.TotalHours.Should().Be(30.0m); // 10 + 20
            aggregate.EntryCount.Should().Be(2);
        }

        [Fact]
        public void GetFiscalYearSummary_ShouldAggregateFullYear()
        {
            // Arrange
            var fiscalYear = 2026; // Apr 2025 - Mar 2026
            var fyStart = TimeTrackingService.GetFiscalYearStart(fiscalYear);
            var fyEnd = TimeTrackingService.GetFiscalYearEnd(fiscalYear);

            service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = fyStart.AddDays(7),
                Hours = 40.0m
            });

            service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = fyEnd.AddDays(-7),
                Hours = 35.0m
            });

            // Act
            var summary = service.GetFiscalYearSummary(fiscalYear);

            // Assert
            summary.FiscalYear.Should().Be(fiscalYear);
            summary.TotalHours.Should().Be(75.0m);
        }

        // ====================================================================
        // PERSISTENCE TESTS
        // ====================================================================

        [Fact]
        public void SaveToFile_ThenReload_ShouldPersistData()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            service.AddEntry(new TimeEntry
            {
                ProjectId = projectId,
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 42.5m
            });

            // Wait for debounced save
            Thread.Sleep(1000);

            // Act - Create new service instance and reload
            var newService = TimeTrackingService.Instance;
            newService.Initialize(testDataFile);

            // Assert
            var entries = newService.GetAllEntries();
            entries.Should().ContainSingle();
            entries[0].ProjectId.Should().Be(projectId);
            entries[0].Hours.Should().Be(42.5m);

            newService.Dispose();
        }

        [Fact]
        public void Clear_ShouldRemoveAllEntries()
        {
            // Arrange
            service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 10.0m
            });

            service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 20.0m
            });

            // Act
            service.Clear();

            // Assert
            service.GetAllEntries().Should().BeEmpty();
        }

        [Fact]
        public void ExportToJson_ShouldCreateFile()
        {
            // Arrange
            var exportPath = Path.Combine(Path.GetTempPath(), $"export_test_{Guid.NewGuid()}.json");
            service.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 25.0m
            });

            try
            {
                // Act
                var result = service.ExportToJson(exportPath);

                // Assert
                result.Should().BeTrue();
                File.Exists(exportPath).Should().BeTrue();
            }
            finally
            {
                // Cleanup
                try { File.Delete(exportPath); } catch { }
            }
        }

        // ====================================================================
        // THREAD-SAFETY TESTS
        // ====================================================================

        [Fact]
        public void AddEntry_Concurrent_ShouldHandleAllRequests()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var weekEnding = TimeTrackingService.GetCurrentWeekEnding();
            var tasks = new Task[10];

            // Act - Multiple threads adding entries simultaneously
            for (int i = 0; i < 10; i++)
            {
                int hours = i + 1;
                tasks[i] = Task.Run(() =>
                {
                    service.AddEntry(new TimeEntry
                    {
                        ProjectId = projectId,
                        WeekEnding = weekEnding,
                        Hours = hours
                    });
                });
            }

            Task.WaitAll(tasks);

            // Assert
            var entries = service.GetEntriesForProject(projectId);
            entries.Should().HaveCount(10);
        }

        [Fact]
        public void GetProjectTotalHours_WhileAdding_ShouldNotCrash()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var weekEnding = TimeTrackingService.GetCurrentWeekEnding();

            var readTask = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    service.GetProjectTotalHours(projectId);
                }
            });

            var writeTask = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    service.AddEntry(new TimeEntry
                    {
                        ProjectId = projectId,
                        WeekEnding = weekEnding,
                        Hours = 1.0m
                    });
                }
            });

            // Act & Assert - Should not throw
            Action action = () => Task.WaitAll(readTask, writeTask);
            action.Should().NotThrow();
        }

        // ====================================================================
        // EDGE CASE TESTS
        // ====================================================================

        [Fact]
        public void AddEntry_ZeroHours_ShouldAllow()
        {
            // Arrange
            var entry = new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 0.0m
            };

            // Act
            var result = service.AddEntry(entry);

            // Assert
            result.Should().NotBeNull();
            result.Hours.Should().Be(0.0m);
        }

        [Fact]
        public void AddEntry_NegativeHours_ShouldAllow()
        {
            // Arrange - Negative hours might represent time corrections
            var entry = new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = -5.0m
            };

            // Act
            var result = service.AddEntry(entry);

            // Assert
            result.Should().NotBeNull();
            result.Hours.Should().Be(-5.0m);
        }

        [Fact]
        public void GetEntriesForWeek_EmptyWeek_ShouldReturnEmpty()
        {
            // Act
            var entries = service.GetEntriesForWeek(TimeTrackingService.GetCurrentWeekEnding());

            // Assert
            entries.Should().BeEmpty();
        }

        [Fact]
        public void GetProjectTotalHours_NoEntries_ShouldReturnZero()
        {
            // Act
            var total = service.GetProjectTotalHours(Guid.NewGuid());

            // Assert
            total.Should().Be(0.0m);
        }

        [Fact]
        public void TimeEntry_TotalHours_ShouldCalculateCorrectly()
        {
            // Arrange
            var entry = new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 37.5m
            };

            // Act
            var added = service.AddEntry(entry);

            // Assert
            added.TotalHours.Should().Be(37.5m);
        }

        [Fact]
        public void AddEntry_VeryLargeHours_ShouldAllow()
        {
            // Arrange - Testing decimal limits
            var entry = new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 9999.99m
            };

            // Act
            var result = service.AddEntry(entry);

            // Assert
            result.Should().NotBeNull();
            result.Hours.Should().Be(9999.99m);
        }

        [Fact]
        public void Dispose_PendingSave_ShouldFlushData()
        {
            // Arrange
            var tempService = TimeTrackingService.Instance;
            var tempFile = Path.Combine(Path.GetTempPath(), $"dispose_test_{Guid.NewGuid()}.json");
            tempService.Initialize(tempFile);

            tempService.AddEntry(new TimeEntry
            {
                ProjectId = Guid.NewGuid(),
                WeekEnding = TimeTrackingService.GetCurrentWeekEnding(),
                Hours = 50.0m
            });

            // Act
            tempService.Dispose();

            // Assert - File should exist with data
            File.Exists(tempFile).Should().BeTrue();

            // Cleanup
            try { File.Delete(tempFile); } catch { }
        }
    }
}
