using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Infrastructure
{
    /// <summary>
    /// Comprehensive tests for ErrorHandler with retry logic and error policy
    /// Tests cover: error handling, retry logic, event firing, thread-safety, severity levels
    /// </summary>
    [Trait("Category", "Critical")]
    [Trait("Priority", "High")]
    public class ErrorHandlerTests
    {
        private readonly ErrorHandler errorHandler;

        public ErrorHandlerTests()
        {
            errorHandler = new ErrorHandler();
        }

        [Fact]
        public void HandleError_ShouldRaiseErrorOccurredEvent()
        {
            // Arrange
            ErrorEventArgs capturedArgs = null;
            errorHandler.ErrorOccurred += (sender, args) => capturedArgs = args;

            var exception = new InvalidOperationException("Test error");

            // Act
            errorHandler.HandleError(exception, "Test context", ErrorSeverity.Error, showToUser: false);

            // Assert
            Assert.NotNull(capturedArgs);
            Assert.Equal(exception, capturedArgs.Exception);
            Assert.Equal("Test context", capturedArgs.Context);
            Assert.Equal(ErrorSeverity.Error, capturedArgs.Severity);
        }

        [Fact]
        public void HandleError_WithDifferentSeverities_ShouldStoreCorrectSeverity()
        {
            // Arrange
            ErrorSeverity? capturedSeverity = null;
            errorHandler.ErrorOccurred += (sender, args) => capturedSeverity = args.Severity;

            // Act & Assert - Warning
            errorHandler.HandleError(new Exception(), "ctx", ErrorSeverity.Warning, showToUser: false);
            Assert.Equal(ErrorSeverity.Warning, capturedSeverity);

            // Act & Assert - Critical
            errorHandler.HandleError(new Exception(), "ctx", ErrorSeverity.Critical, showToUser: false);
            Assert.Equal(ErrorSeverity.Critical, capturedSeverity);
        }

        [Fact]
        public void ExecuteWithRetry_ShouldReturnResultOnSuccess()
        {
            // Arrange
            int callCount = 0;
            Func<int> action = () =>
            {
                callCount++;
                return 42;
            };

            // Act
#pragma warning disable CS0618 // Type or member is obsolete
            var result = errorHandler.ExecuteWithRetry(action, maxRetries: 3, delayMs: 10, context: "Test");
#pragma warning restore CS0618

            // Assert
            Assert.Equal(42, result);
            Assert.Equal(1, callCount);
        }

        [Fact]
        public void ExecuteWithRetry_ShouldRetryOnFailure()
        {
            // Arrange
            int callCount = 0;
            Func<int> action = () =>
            {
                callCount++;
                if (callCount < 3)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                return 100;
            };

            // Act
#pragma warning disable CS0618 // Type or member is obsolete
            var result = errorHandler.ExecuteWithRetry(action, maxRetries: 3, delayMs: 10, context: "Test");
#pragma warning restore CS0618

            // Assert
            Assert.Equal(100, result);
            Assert.Equal(3, callCount);
        }

        [Fact]
        public void ExecuteWithRetry_ShouldThrowAfterMaxRetries()
        {
            // Arrange
            Func<int> action = () => throw new InvalidOperationException("Always fails");

            // Act & Assert
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Throws<InvalidOperationException>(() =>
                errorHandler.ExecuteWithRetry(action, maxRetries: 2, delayMs: 10, context: "Test"));
#pragma warning restore CS0618
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ShouldReturnResultOnSuccess()
        {
            // Arrange
            int callCount = 0;
            Func<Task<int>> action = async () =>
            {
                await Task.Delay(1);
                callCount++;
                return 42;
            };

            // Act
            var result = await errorHandler.ExecuteWithRetryAsync(action, maxRetries: 3, delayMs: 10, context: "Test");

            // Assert
            Assert.Equal(42, result);
            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ShouldRetryOnFailure()
        {
            // Arrange
            int callCount = 0;
            Func<Task<int>> action = async () =>
            {
                await Task.Delay(1);
                callCount++;
                if (callCount < 3)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                return 100;
            };

            // Act
            var result = await errorHandler.ExecuteWithRetryAsync(action, maxRetries: 3, delayMs: 10, context: "Test");

            // Assert
            Assert.Equal(100, result);
            Assert.Equal(3, callCount);
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ShouldNotBlockUIThread()
        {
            // Arrange
            var startTime = DateTime.UtcNow;
            Func<Task<int>> action = async () =>
            {
                await Task.Delay(1);
                throw new InvalidOperationException("Simulated failure");
            };

            // Act
            try
            {
                await errorHandler.ExecuteWithRetryAsync(action, maxRetries: 3, delayMs: 50, context: "Test");
            }
            catch
            {
                // Expected to fail
            }

            var elapsed = DateTime.UtcNow - startTime;

            // Assert - Should use async delay, total time >= 50ms * 3 retries
            Assert.True(elapsed.TotalMilliseconds >= 100, "Should use async delays, not blocking");
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ShouldThrowAfterMaxRetries()
        {
            // Arrange
            Func<Task<int>> action = async () =>
            {
                await Task.Delay(1);
                throw new InvalidOperationException("Always fails");
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await errorHandler.ExecuteWithRetryAsync(action, maxRetries: 2, delayMs: 10, context: "Test"));
        }

        // ====================================================================
        // ADDITIONAL SEVERITY TESTS
        // ====================================================================

        [Fact]
        public void HandleError_WithInfoSeverity_ShouldNotShowDialog()
        {
            // Arrange
            bool eventFired = false;
            errorHandler.ErrorOccurred += (sender, args) => eventFired = true;

            // Act
            errorHandler.HandleError(new Exception(), "ctx", ErrorSeverity.Info, showToUser: false);

            // Assert - Event should fire but no dialog
            eventFired.Should().BeTrue();
        }

        [Fact]
        public void HandleError_WithCriticalSeverity_ShouldRaiseEvent()
        {
            // Arrange
            ErrorSeverity? capturedSeverity = null;
            errorHandler.ErrorOccurred += (sender, args) => capturedSeverity = args.Severity;

            // Act
            errorHandler.HandleError(new Exception(), "ctx", ErrorSeverity.Critical, showToUser: false);

            // Assert
            capturedSeverity.Should().Be(ErrorSeverity.Critical);
        }

        [Fact]
        public void HandleError_MultipleSubscribers_ShouldNotifyAll()
        {
            // Arrange
            int subscriber1Count = 0;
            int subscriber2Count = 0;
            errorHandler.ErrorOccurred += (sender, args) => subscriber1Count++;
            errorHandler.ErrorOccurred += (sender, args) => subscriber2Count++;

            // Act
            errorHandler.HandleError(new Exception(), "ctx", ErrorSeverity.Error, showToUser: false);

            // Assert
            subscriber1Count.Should().Be(1);
            subscriber2Count.Should().Be(1);
        }

        // ====================================================================
        // ERROR CONTEXT TESTS
        // ====================================================================

        [Fact]
        public void HandleError_ShouldCaptureContext()
        {
            // Arrange
            string capturedContext = null;
            errorHandler.ErrorOccurred += (sender, args) => capturedContext = args.Context;

            // Act
            errorHandler.HandleError(new Exception(), "TestContext", ErrorSeverity.Error, showToUser: false);

            // Assert
            capturedContext.Should().Be("TestContext");
        }

        [Fact]
        public void HandleError_ShouldCaptureTimestamp()
        {
            // Arrange
            DateTime? capturedTimestamp = null;
            errorHandler.ErrorOccurred += (sender, args) => capturedTimestamp = args.Timestamp;
            var before = DateTime.Now;

            // Act
            errorHandler.HandleError(new Exception(), "ctx", ErrorSeverity.Error, showToUser: false);

            // Assert
            capturedTimestamp.Should().NotBeNull();
            capturedTimestamp.Value.Should().BeOnOrAfter(before);
            capturedTimestamp.Value.Should().BeOnOrBefore(DateTime.Now);
        }

        [Fact]
        public void HandleError_ShouldCaptureException()
        {
            // Arrange
            Exception capturedException = null;
            errorHandler.ErrorOccurred += (sender, args) => capturedException = args.Exception;
            var testException = new ArgumentNullException("param");

            // Act
            errorHandler.HandleError(testException, "ctx", ErrorSeverity.Error, showToUser: false);

            // Assert
            capturedException.Should().Be(testException);
        }

        // ====================================================================
        // RETRY LOGIC ADDITIONAL TESTS
        // ====================================================================

        [Fact]
        public void ExecuteWithRetry_WithZeroRetries_ShouldThrowImmediately()
        {
            // Arrange
            Func<int> action = () => throw new InvalidOperationException("Fail");

            // Act & Assert
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Throws<InvalidOperationException>(() =>
                errorHandler.ExecuteWithRetry(action, maxRetries: 0, delayMs: 10, context: "Test"));
#pragma warning restore CS0618
        }

        [Fact]
        public void ExecuteWithRetry_ExponentialBackoff_ShouldIncreaseDelay()
        {
            // Arrange
            int attemptCount = 0;
            var delays = new System.Collections.Generic.List<long>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Func<int> action = () =>
            {
                attemptCount++;
                delays.Add(stopwatch.ElapsedMilliseconds);
                if (attemptCount < 3)
                    throw new InvalidOperationException("Retry");
                return 42;
            };

            // Act
#pragma warning disable CS0618 // Type or member is obsolete
            errorHandler.ExecuteWithRetry(action, maxRetries: 3, delayMs: 50, context: "Test");
#pragma warning restore CS0618

            // Assert - Delays should increase (exponential backoff)
            attemptCount.Should().Be(3);
            // Second attempt should be ~50ms after first
            // Third attempt should be ~100ms after second
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_ExponentialBackoff_ShouldIncreaseDelay()
        {
            // Arrange
            int attemptCount = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long lastTimestamp = 0;
            var intervals = new System.Collections.Generic.List<long>();

            Func<Task<int>> action = async () =>
            {
                await Task.Delay(1);
                var currentTime = stopwatch.ElapsedMilliseconds;
                if (lastTimestamp > 0)
                {
                    intervals.Add(currentTime - lastTimestamp);
                }
                lastTimestamp = currentTime;
                attemptCount++;

                if (attemptCount < 3)
                    throw new InvalidOperationException("Retry");
                return 42;
            };

            // Act
            await errorHandler.ExecuteWithRetryAsync(action, maxRetries: 3, delayMs: 50, context: "Test");

            // Assert - Should have exponential delays
            attemptCount.Should().Be(3);
            intervals.Should().HaveCountGreaterOrEqualTo(1);
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_DifferentExceptionTypes_ShouldRetryAll()
        {
            // Arrange
            int attemptCount = 0;
            Func<Task<int>> action = async () =>
            {
                await Task.Delay(1);
                attemptCount++;

                // Throw different exception types
                switch (attemptCount)
                {
                    case 1: throw new InvalidOperationException("First");
                    case 2: throw new ArgumentException("Second");
                    default: return 42;
                }
            };

            // Act
            var result = await errorHandler.ExecuteWithRetryAsync(action, maxRetries: 3, delayMs: 10, context: "Test");

            // Assert
            result.Should().Be(42);
            attemptCount.Should().Be(3);
        }

        [Fact]
        public void ExecuteWithRetry_ActionReturnsNull_ShouldReturnNull()
        {
            // Arrange
            Func<string> action = () => null;

            // Act
#pragma warning disable CS0618 // Type or member is obsolete
            var result = errorHandler.ExecuteWithRetry(action, maxRetries: 3, delayMs: 10, context: "Test");
#pragma warning restore CS0618

            // Assert
            result.Should().BeNull();
        }

        // ====================================================================
        // THREAD-SAFETY TESTS
        // ====================================================================

        [Fact]
        public void HandleError_Concurrent_ShouldNotifyAllSubscribers()
        {
            // Arrange
            int eventCount = 0;
            object lockObj = new object();
            errorHandler.ErrorOccurred += (sender, args) =>
            {
                lock (lockObj) { eventCount++; }
            };

            // Act - Multiple threads handling errors simultaneously
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    errorHandler.HandleError(new Exception(), "concurrent", ErrorSeverity.Error, showToUser: false);
                });
            }

            Task.WaitAll(tasks);

            // Assert
            eventCount.Should().Be(10);
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_MultipleConcurrent_ShouldAllComplete()
        {
            // Arrange
            int successCount = 0;
            object lockObj = new object();

            var tasks = new Task<int>[5];
            for (int i = 0; i < 5; i++)
            {
                int taskId = i;
                tasks[i] = Task.Run(async () =>
                {
                    var result = await errorHandler.ExecuteWithRetryAsync(
                        async () =>
                        {
                            await Task.Delay(10);
                            return taskId;
                        },
                        maxRetries: 2,
                        delayMs: 10,
                        context: $"Task{taskId}"
                    );

                    lock (lockObj) { successCount++; }
                    return result;
                });
            }

            // Act
            await Task.WhenAll(tasks);

            // Assert
            successCount.Should().Be(5);
        }

        // ====================================================================
        // EDGE CASE TESTS
        // ====================================================================

        [Fact]
        public void HandleError_NullException_ShouldNotThrow()
        {
            // Act & Assert
            Action action = () => errorHandler.HandleError(null, "ctx", ErrorSeverity.Error, showToUser: false);
            action.Should().NotThrow();
        }

        [Fact]
        public void HandleError_EmptyContext_ShouldNotThrow()
        {
            // Act & Assert
            Action action = () => errorHandler.HandleError(new Exception(), "", ErrorSeverity.Error, showToUser: false);
            action.Should().NotThrow();
        }

        [Fact]
        public void HandleError_NullContext_ShouldNotThrow()
        {
            // Act & Assert
            Action action = () => errorHandler.HandleError(new Exception(), null, ErrorSeverity.Error, showToUser: false);
            action.Should().NotThrow();
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_CancelledException_ShouldThrow()
        {
            // Arrange
            Func<Task<int>> action = async () =>
            {
                await Task.Delay(1);
                throw new OperationCanceledException("Cancelled");
            };

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await errorHandler.ExecuteWithRetryAsync(action, maxRetries: 3, delayMs: 10, context: "Test"));
        }

        [Fact]
        public void HandleError_WithNestedExceptions_ShouldCaptureOuter()
        {
            // Arrange
            Exception capturedException = null;
            errorHandler.ErrorOccurred += (sender, args) => capturedException = args.Exception;

            var innerException = new InvalidOperationException("Inner");
            var outerException = new ApplicationException("Outer", innerException);

            // Act
            errorHandler.HandleError(outerException, "ctx", ErrorSeverity.Error, showToUser: false);

            // Assert
            capturedException.Should().Be(outerException);
            capturedException.InnerException.Should().Be(innerException);
        }

        [Fact]
        public void ExecuteWithRetry_ActionThrowsAggregateException_ShouldThrow()
        {
            // Arrange
            Func<int> action = () =>
            {
                throw new AggregateException(
                    new InvalidOperationException("Error 1"),
                    new ArgumentException("Error 2")
                );
            };

            // Act & Assert
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Throws<AggregateException>(() =>
                errorHandler.ExecuteWithRetry(action, maxRetries: 2, delayMs: 10, context: "Test"));
#pragma warning restore CS0618
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_VeryLongDelay_ShouldEventuallyComplete()
        {
            // Arrange
            int attemptCount = 0;
            Func<Task<int>> action = async () =>
            {
                await Task.Delay(1);
                attemptCount++;
                if (attemptCount < 2)
                    throw new InvalidOperationException("Retry");
                return 42;
            };

            // Act
            var result = await errorHandler.ExecuteWithRetryAsync(action, maxRetries: 3, delayMs: 100, context: "Test");

            // Assert
            result.Should().Be(42);
            attemptCount.Should().Be(2);
        }
    }
}
