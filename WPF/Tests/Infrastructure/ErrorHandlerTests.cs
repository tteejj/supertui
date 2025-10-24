using System;
using System.Threading.Tasks;
using Xunit;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Infrastructure
{
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
    }
}
