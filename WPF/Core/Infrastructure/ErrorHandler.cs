using System;
using System.Threading.Tasks;
using System.Windows;

namespace SuperTUI.Infrastructure
{
    // ============================================================================
    // ERROR HANDLING & RESILIENCE
    // ============================================================================

    /// <summary>
    /// Global error handler with recovery strategies
    /// </summary>
    public class ErrorHandler : IErrorHandler
    {
        private static ErrorHandler instance;
        public static ErrorHandler Instance => instance ??= new ErrorHandler();

        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        public void HandleError(Exception ex, string context, ErrorSeverity severity = ErrorSeverity.Error, bool showToUser = true)
        {
            Logger.Instance.Error(context, $"Error occurred: {ex.Message}", ex);

            ErrorOccurred?.Invoke(this, new ErrorEventArgs
            {
                Exception = ex,
                Context = context,
                Severity = severity,
                Timestamp = DateTime.Now
            });

            if (showToUser && severity >= ErrorSeverity.Error)
            {
                // TODO: Show error dialog to user
                ShowErrorDialog(ex, context);
            }
        }

        private void ShowErrorDialog(Exception ex, string context)
        {
            // Simple message box for now - can be replaced with custom dialog
            System.Windows.MessageBox.Show(
                $"An error occurred in {context}:\n\n{ex.Message}\n\nSee log for details.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        /// <summary>
        /// Execute an action with retry logic (synchronous - BLOCKS UI THREAD)
        /// </summary>
        /// <remarks>
        /// WARNING: This method blocks the UI thread during retries. Prefer ExecuteWithRetryAsync for UI operations.
        /// </remarks>
        [Obsolete("Use ExecuteWithRetryAsync for UI operations to avoid blocking the UI thread")]
        public T ExecuteWithRetry<T>(Func<T> action, int maxRetries = 3, int delayMs = 100, string context = "Operation")
        {
            int attempts = 0;
            Exception lastException = null;

            while (attempts < maxRetries)
            {
                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;

                    if (attempts < maxRetries)
                    {
                        Logger.Instance.Warning(context, $"Attempt {attempts} failed, retrying: {ex.Message}");
                        System.Threading.Thread.Sleep(delayMs * attempts); // Exponential backoff
                    }
                }
            }

            HandleError(lastException, context, ErrorSeverity.Error);
            throw lastException;
        }

        /// <summary>
        /// Execute an async action with retry logic (non-blocking)
        /// </summary>
        /// <remarks>
        /// Use this method for UI operations to avoid blocking the UI thread during retries.
        /// </remarks>
        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int delayMs = 100, string context = "Operation")
        {
            int attempts = 0;
            Exception lastException = null;

            while (attempts < maxRetries)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;

                    if (attempts < maxRetries)
                    {
                        Logger.Instance.Warning(context, $"Attempt {attempts} failed, retrying: {ex.Message}");
                        await Task.Delay(delayMs * attempts); // Exponential backoff, non-blocking
                    }
                }
            }

            HandleError(lastException, context, ErrorSeverity.Error);
            throw lastException;
        }
    }

    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public string Context { get; set; }
        public ErrorSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
