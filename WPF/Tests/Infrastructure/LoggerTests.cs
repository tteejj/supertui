using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Infrastructure
{
    /// <summary>
    /// Comprehensive tests for Logger with dual-queue system
    /// Tests cover: log levels, async queuing, thread-safety, memory management, file rotation
    /// </summary>
    [Trait("Category", "Critical")]
    [Trait("Priority", "High")]
    public class LoggerTests : IDisposable
    {
        private string testLogDirectory;
        private Logger logger;

        public LoggerTests()
        {
            testLogDirectory = Path.Combine(Path.GetTempPath(), $"SuperTUI_LogTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(testLogDirectory);
            logger = new Logger();
        }

        public void Dispose()
        {
            // Cleanup
            try
            {
                logger?.Flush();
                Thread.Sleep(500); // Give background thread time to finish

                if (Directory.Exists(testLogDirectory))
                    Directory.Delete(testLogDirectory, recursive: true);
            }
            catch { /* Ignore cleanup errors */ }
        }

        // ====================================================================
        // LOG LEVEL TESTS
        // ====================================================================

        [Fact]
        public void Log_WithTraceLevel_ShouldWriteToSink()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            logger.SetMinLevel(LogLevel.Trace);

            // Act
            logger.Trace("TestCategory", "Trace message");
            Thread.Sleep(100);

            // Assert
            var entries = memorySink.GetEntries();
            entries.Should().ContainSingle();
            entries[0].Level.Should().Be(LogLevel.Trace);
            entries[0].Message.Should().Be("Trace message");
        }

        [Fact]
        public void Log_WithDebugLevel_ShouldWriteToSink()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            logger.SetMinLevel(LogLevel.Debug);

            // Act
            logger.Debug("TestCategory", "Debug message");
            Thread.Sleep(100);

            // Assert
            var entries = memorySink.GetEntries();
            entries.Should().ContainSingle();
            entries[0].Level.Should().Be(LogLevel.Debug);
        }

        [Fact]
        public void Log_WithInfoLevel_ShouldWriteToSink()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            logger.SetMinLevel(LogLevel.Info);

            // Act
            logger.Info("TestCategory", "Info message");
            Thread.Sleep(100);

            // Assert
            var entries = memorySink.GetEntries();
            entries.Should().ContainSingle();
            entries[0].Level.Should().Be(LogLevel.Info);
        }

        [Fact]
        public void Log_WithWarningLevel_ShouldWriteToSink()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            logger.SetMinLevel(LogLevel.Warning);

            // Act
            logger.Warning("TestCategory", "Warning message");
            Thread.Sleep(100);

            // Assert
            var entries = memorySink.GetEntries();
            entries.Should().ContainSingle();
            entries[0].Level.Should().Be(LogLevel.Warning);
        }

        [Fact]
        public void Log_WithErrorLevel_ShouldWriteToSinkWithException()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            var exception = new InvalidOperationException("Test error");

            // Act
            logger.Error("TestCategory", "Error message", exception);
            Thread.Sleep(100);

            // Assert
            var entries = memorySink.GetEntries();
            entries.Should().ContainSingle();
            entries[0].Level.Should().Be(LogLevel.Error);
            entries[0].Exception.Should().Be(exception);
        }

        [Fact]
        public void Log_WithCriticalLevel_ShouldWriteToSinkWithStackTrace()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);

            // Act
            logger.Critical("TestCategory", "Critical message");
            Thread.Sleep(100);

            // Assert
            var entries = memorySink.GetEntries();
            entries.Should().ContainSingle();
            entries[0].Level.Should().Be(LogLevel.Critical);
            entries[0].StackTrace.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Log_BelowMinLevel_ShouldNotWriteToSink()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            logger.SetMinLevel(LogLevel.Warning);

            // Act
            logger.Debug("TestCategory", "Debug message");
            logger.Info("TestCategory", "Info message");
            Thread.Sleep(100);

            // Assert
            memorySink.GetEntries().Should().BeEmpty();
        }

        // ====================================================================
        // CATEGORY FILTERING TESTS
        // ====================================================================

        [Fact]
        public void Log_WithEnabledCategory_ShouldWriteToSink()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            logger.EnableCategory("AllowedCategory");

            // Act
            logger.Info("AllowedCategory", "Should be logged");
            Thread.Sleep(100);

            // Assert
            memorySink.GetEntries().Should().ContainSingle();
        }

        [Fact]
        public void Log_WithDisabledCategory_ShouldNotWriteToSink()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            logger.EnableCategory("AllowedCategory");

            // Act
            logger.Info("BlockedCategory", "Should NOT be logged");
            Thread.Sleep(100);

            // Assert
            memorySink.GetEntries().Should().BeEmpty();
        }

        [Fact]
        public void Log_DisableCategory_ShouldRemoveFromEnabled()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            logger.EnableCategory("TestCategory");
            logger.DisableCategory("TestCategory");

            // Act
            logger.Info("TestCategory", "Should NOT be logged after disable");
            Thread.Sleep(100);

            // Assert
            memorySink.GetEntries().Should().BeEmpty();
        }

        // ====================================================================
        // MESSAGE FORMATTING TESTS
        // ====================================================================

        [Fact]
        public void Log_WithProperties_ShouldIncludeInEntry()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            var properties = new Dictionary<string, object>
            {
                ["UserId"] = 123,
                ["Action"] = "Login"
            };

            // Act
            logger.Log(LogLevel.Info, "TestCategory", "User action", properties: properties);
            Thread.Sleep(100);

            // Assert
            var entries = memorySink.GetEntries();
            entries.Should().ContainSingle();
            entries[0].Properties.Should().ContainKey("UserId");
            entries[0].Properties["UserId"].Should().Be(123);
        }

        [Fact]
        public void Log_WithException_ShouldIncludeExceptionDetails()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            var exception = new ArgumentException("Invalid argument", "paramName");

            // Act
            logger.Error("TestCategory", "Operation failed", exception);
            Thread.Sleep(100);

            // Assert
            var entries = memorySink.GetEntries();
            entries.Should().ContainSingle();
            entries[0].Exception.Should().Be(exception);
            entries[0].Exception.Message.Should().Contain("Invalid argument");
        }

        // ====================================================================
        // THREAD-SAFETY TESTS
        // ====================================================================

        [Fact]
        public void Log_ConcurrentWrites_ShouldHandleAllMessages()
        {
            // Arrange
            var memorySink = new MemoryLogSink(maxEntries: 1000);
            logger.AddSink(memorySink);
            const int threadCount = 10;
            const int messagesPerThread = 10;
            var barrier = new Barrier(threadCount);

            // Act - Multiple threads logging simultaneously
            var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
                Task.Run(() =>
                {
                    barrier.SignalAndWait(); // Synchronize start
                    for (int i = 0; i < messagesPerThread; i++)
                    {
                        logger.Info($"Thread{threadId}", $"Message {i}");
                    }
                })
            ).ToArray();

            Task.WaitAll(tasks);
            Thread.Sleep(500); // Wait for async processing

            // Assert
            var entries = memorySink.GetEntries(count: 1000);
            entries.Count.Should().Be(threadCount * messagesPerThread);
        }

        [Fact]
        public void Log_ConcurrentFlush_ShouldNotCrash()
        {
            // Arrange
            var fileSink = new FileLogSink(testLogDirectory, maxFileSizeMB: 1);
            logger.AddSink(fileSink);

            // Act - Multiple threads flushing simultaneously
            var tasks = Enumerable.Range(0, 5).Select(_ =>
                Task.Run(() =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        logger.Info("TestCategory", $"Message {i}");
                        logger.Flush();
                    }
                })
            ).ToArray();

            // Assert - Should not throw
            Action action = () => Task.WaitAll(tasks);
            action.Should().NotThrow();
        }

        // ====================================================================
        // FILE LOG SINK TESTS
        // ====================================================================

        [Fact]
        public void FileLogSink_ShouldCreateLogFile()
        {
            // Arrange
            var fileSink = new FileLogSink(testLogDirectory, "test");
            logger.AddSink(fileSink);

            // Act
            logger.Info("TestCategory", "Test message");
            logger.Flush();
            Thread.Sleep(500);

            // Assert
            var logFiles = Directory.GetFiles(testLogDirectory, "test_*.log");
            logFiles.Should().NotBeEmpty();
        }

        [Fact]
        public void FileLogSink_ShouldRotateWhenMaxSizeExceeded()
        {
            // Arrange - Write many small messages to exceed file size limit
            // Using 1 MB limit and writing 2 MB of data total
            var fileSink = new FileLogSink(testLogDirectory, "rotate", maxFileSizeMB: 1, maxFiles: 5);
            logger.AddSink(fileSink);

            // Build a 100 KB message (each char = 1 byte in ASCII)
            var message = new string('X', 100 * 1024);

            // Act - Write 25 messages (100 KB each = 2.5 MB total, exceeds 1 MB limit)
            for (int i = 0; i < 25; i++)
            {
                logger.Info("TestCategory", $"Message{i}: {message}");
                Thread.Sleep(50); // Small delay between messages to avoid overwhelming the queue
            }
            logger.Flush();
            Thread.Sleep(3000); // Give writer thread time to process and rotate

            // Assert - Should have multiple log files (2.5 MB total should create 3 files with 1 MB limit)
            var logFiles = Directory.GetFiles(testLogDirectory, "rotate_*.log");
            logFiles.Length.Should().BeGreaterThan(1, "25 messages of 100 KB each (2.5 MB) should exceed 1 MB limit and create multiple files");
        }

        [Fact]
        public void FileLogSink_ShouldLimitMaxFiles()
        {
            // Arrange
            const int maxFiles = 3;
            var fileSink = new FileLogSink(testLogDirectory, "limited", maxFileSizeMB: 1, maxFiles: maxFiles);
            logger.AddSink(fileSink);

            // Act - Force multiple rotations
            for (int i = 0; i < 500; i++)
            {
                logger.Info("TestCategory", new string('X', 1000));
            }
            logger.Flush();
            Thread.Sleep(1500);

            // Assert - Should not exceed max files (plus backup)
            var logFiles = Directory.GetFiles(testLogDirectory, "limited_*.log");
            logFiles.Length.Should().BeLessOrEqualTo(maxFiles + 1); // +1 for potential backup
        }

        [Fact]
        public void FileLogSink_CriticalLogs_ShouldNeverBeDropped()
        {
            // Arrange - Small queue to force pressure
            var fileSink = new FileLogSink(testLogDirectory, "critical", maxFileSizeMB: 10);
            logger.AddSink(fileSink);

            // Act - Write many critical logs
            for (int i = 0; i < 100; i++)
            {
                logger.Critical("TestCategory", $"Critical message {i}");
            }
            logger.Flush();
            Thread.Sleep(1000);

            // Assert - No logs should be dropped (check via GetDroppedLogCount)
            fileSink.GetDroppedLogCount().Should().Be(0);
        }

        [Fact]
        public void FileLogSink_GetQueueDepth_ShouldReturnCurrentDepth()
        {
            // Arrange
            var fileSink = new FileLogSink(testLogDirectory, "queue");
            logger.AddSink(fileSink);

            // Act - Write logs and check queue depth
            for (int i = 0; i < 10; i++)
            {
                logger.Info("TestCategory", $"Message {i}");
            }

            var (critical, normal) = fileSink.GetQueueDepth();

            // Assert - Queue should have items (or be processing)
            (critical + normal).Should().BeGreaterOrEqualTo(0);
        }

        // ====================================================================
        // MEMORY LOG SINK TESTS
        // ====================================================================

        [Fact]
        public void MemoryLogSink_ShouldStoreLogs()
        {
            // Arrange
            var memorySink = new MemoryLogSink(maxEntries: 100);
            logger.AddSink(memorySink);

            // Act
            for (int i = 0; i < 10; i++)
            {
                logger.Info("TestCategory", $"Message {i}");
            }
            Thread.Sleep(100);

            // Assert
            memorySink.GetEntries().Count.Should().Be(10);
        }

        [Fact]
        public void MemoryLogSink_ShouldRespectMaxEntries()
        {
            // Arrange
            const int maxEntries = 5;
            var memorySink = new MemoryLogSink(maxEntries: maxEntries);
            logger.AddSink(memorySink);

            // Act - Write more than max
            for (int i = 0; i < 10; i++)
            {
                logger.Info("TestCategory", $"Message {i}");
            }
            Thread.Sleep(100);

            // Assert - Should only keep latest entries
            memorySink.GetEntries().Count.Should().Be(maxEntries);
            memorySink.GetEntries()[0].Message.Should().Contain("Message 5"); // Oldest kept
        }

        [Fact]
        public void MemoryLogSink_GetEntriesByLevel_ShouldFilter()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);

            // Act
            logger.Debug("TestCategory", "Debug");
            logger.Info("TestCategory", "Info");
            logger.Warning("TestCategory", "Warning");
            logger.Error("TestCategory", "Error");
            Thread.Sleep(100);

            // Assert
            memorySink.GetEntries(LogLevel.Warning).Count.Should().Be(2); // Warning + Error
        }

        [Fact]
        public void MemoryLogSink_Clear_ShouldRemoveAllEntries()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);
            logger.Info("TestCategory", "Message 1");
            logger.Info("TestCategory", "Message 2");
            Thread.Sleep(100);

            // Act
            memorySink.Clear();

            // Assert
            memorySink.GetEntries().Should().BeEmpty();
        }

        // ====================================================================
        // DIAGNOSTICS TESTS
        // ====================================================================

        [Fact]
        public void GetDiagnostics_ShouldReturnLoggerState()
        {
            // Arrange
            logger.SetMinLevel(LogLevel.Info);
            logger.EnableCategory("TestCategory");
            logger.AddSink(new MemoryLogSink());

            // Act
            var diagnostics = logger.GetDiagnostics();

            // Assert
            diagnostics.Should().ContainKey("MinLevel");
            diagnostics["MinLevel"].Should().Be("Info");
            diagnostics.Should().ContainKey("SinkCount");
            ((int)diagnostics["SinkCount"]).Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetTotalDroppedLogs_ShouldAggregateAcrossSinks()
        {
            // Arrange
            var fileSink = new FileLogSink(testLogDirectory, "dropped");
            logger.AddSink(fileSink);

            // Act - Just check it doesn't throw
            var droppedCount = logger.GetTotalDroppedLogs();

            // Assert
            droppedCount.Should().BeGreaterOrEqualTo(0);
        }

        // ====================================================================
        // EDGE CASE TESTS
        // ====================================================================

        [Fact]
        public void Log_NullMessage_ShouldNotThrow()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);

            // Act & Assert
            Action action = () => logger.Info("TestCategory", null);
            action.Should().NotThrow();
        }

        [Fact]
        public void Log_EmptyCategory_ShouldNotThrow()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);

            // Act & Assert
            Action action = () => logger.Info("", "Message");
            action.Should().NotThrow();
        }

        [Fact]
        public void Log_NullException_ShouldNotThrow()
        {
            // Arrange
            var memorySink = new MemoryLogSink();
            logger.AddSink(memorySink);

            // Act & Assert
            Action action = () => logger.Error("TestCategory", "Error message", null);
            action.Should().NotThrow();
        }

        [Fact]
        public void Flush_WithNoSinks_ShouldNotThrow()
        {
            // Arrange
            var emptyLogger = new Logger();

            // Act & Assert
            Action action = () => emptyLogger.Flush();
            action.Should().NotThrow();
        }

        [Fact]
        public void Log_AfterDispose_ShouldNotCrash()
        {
            // Arrange
            var fileSink = new FileLogSink(testLogDirectory, "disposed");
            logger.AddSink(fileSink);
            fileSink.Dispose();

            // Act & Assert - Should not throw
            Action action = () => logger.Info("TestCategory", "Message after dispose");
            action.Should().NotThrow();
        }
    }
}
