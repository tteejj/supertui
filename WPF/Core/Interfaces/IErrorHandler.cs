using System;
using System.Threading.Tasks;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for error handling - enables testing and mocking
    /// </summary>
    public interface IErrorHandler
    {
        event EventHandler<ErrorEventArgs> ErrorOccurred;

        void HandleError(Exception ex, string context, ErrorSeverity severity = ErrorSeverity.Error, bool showToUser = true);

        [Obsolete("Use ExecuteWithRetryAsync for UI operations to avoid blocking the UI thread")]
        T ExecuteWithRetry<T>(Func<T> action, int maxRetries = 3, int delayMs = 100, string context = "Operation");

        Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int delayMs = 100, string context = "Operation");
    }
}
