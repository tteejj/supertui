using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Error severity levels for categorized error handling
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>Log warning and continue (e.g., missing optional config)</summary>
        Recoverable,
        /// <summary>Log error, disable feature, continue (e.g., widget crash)</summary>
        Degraded,
        /// <summary>Log critical, show error, exit (e.g., security violation)</summary>
        Fatal
    }

    /// <summary>
    /// Error categories for consistent handling
    /// </summary>
    public enum ErrorCategory
    {
        Configuration,  // Config file errors
        IO,            // File I/O errors
        Network,       // Network errors (if applicable)
        Security,      // Security violations
        Plugin,        // Plugin errors
        Widget,        // Widget errors
        Internal       // Internal framework errors
    }

    /// <summary>
    /// Centralized error handling policy
    /// Determines severity and appropriate response for different error types
    ///
    /// POLICY RULES:
    /// - Security errors: ALWAYS Fatal (show dialog, exit immediately)
    /// - Configuration errors: Recoverable (use defaults, log warning)
    /// - Widget errors: Degraded (disable widget, log error, show notification)
    /// - IO errors: Check type (UnauthorizedAccess = Fatal, others = Recoverable)
    /// - Plugin errors: Degraded (disable plugin, log error)
    /// - Network errors: Recoverable (retry logic, use cache)
    /// - Internal errors: Fatal (framework bug, cannot continue safely)
    /// </summary>
    public static class ErrorHandlingPolicy
    {
        /// <summary>
        /// Determine error severity based on category and exception type
        /// </summary>
        public static ErrorSeverity GetSeverity(ErrorCategory category, Exception ex)
        {
            // Security violations are ALWAYS fatal
            if (category == ErrorCategory.Security)
            {
                return ErrorSeverity.Fatal;
            }

            // Check for specific exception types that override category
            if (ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException)
            {
                return ErrorSeverity.Fatal;
            }

            if (ex is OutOfMemoryException ||
                ex is StackOverflowException)
            {
                return ErrorSeverity.Fatal;
            }

            // Category-specific severity
            switch (category)
            {
                case ErrorCategory.Configuration:
                    // Missing config values are recoverable (use defaults)
                    if (ex is System.Collections.Generic.KeyNotFoundException ||
                        ex is ArgumentNullException)
                    {
                        return ErrorSeverity.Recoverable;
                    }
                    // Config file corruption is degraded (disable feature)
                    if (ex is System.Text.Json.JsonException ||
                        ex is InvalidOperationException)
                    {
                        return ErrorSeverity.Degraded;
                    }
                    return ErrorSeverity.Recoverable;

                case ErrorCategory.IO:
                    // File not found is recoverable (can create or use default)
                    if (ex is FileNotFoundException ||
                        ex is DirectoryNotFoundException)
                    {
                        return ErrorSeverity.Recoverable;
                    }
                    // Disk full or write failures are degraded (disable feature)
                    if (ex is IOException)
                    {
                        return ErrorSeverity.Degraded;
                    }
                    return ErrorSeverity.Recoverable;

                case ErrorCategory.Widget:
                    // Widget errors should disable the widget but not crash the app
                    return ErrorSeverity.Degraded;

                case ErrorCategory.Plugin:
                    // Plugin errors should disable the plugin but not crash the app
                    return ErrorSeverity.Degraded;

                case ErrorCategory.Network:
                    // Network errors are generally recoverable (retry/cache)
                    return ErrorSeverity.Recoverable;

                case ErrorCategory.Internal:
                    // Internal framework errors are fatal (cannot guarantee stability)
                    return ErrorSeverity.Fatal;

                default:
                    // Unknown category - be conservative and treat as degraded
                    return ErrorSeverity.Degraded;
            }
        }

        /// <summary>
        /// Handle error with appropriate logging and user notification
        /// </summary>
        /// <param name="category">Error category</param>
        /// <param name="ex">Exception that occurred</param>
        /// <param name="context">Human-readable context (e.g., "Loading configuration from config.json")</param>
        /// <param name="logger">Optional logger instance (uses Logger.Instance if null)</param>
        public static void Handle(ErrorCategory category, Exception ex, string context, ILogger logger = null)
        {
            logger = logger ?? Logger.Instance;
            var severity = GetSeverity(category, ex);
            var categoryName = category.ToString();

            // Build error message
            var message = string.IsNullOrEmpty(context)
                ? $"Error: {ex.Message}"
                : $"{context}: {ex.Message}";

            // Log based on severity
            switch (severity)
            {
                case ErrorSeverity.Recoverable:
                    logger.Warning(categoryName, message);
                    logger.Debug(categoryName, $"Stack trace: {ex.StackTrace}");
                    // No user notification for recoverable errors
                    break;

                case ErrorSeverity.Degraded:
                    logger.Error(categoryName, message, ex);

                    // Show non-modal notification to user
                    ShowNotification(
                        "Feature Disabled",
                        $"A feature has been disabled due to an error:\n\n{message}\n\nThe application will continue running.",
                        MessageBoxImage.Warning);
                    break;

                case ErrorSeverity.Fatal:
                    logger.Critical(categoryName, message, ex);

                    // Show modal error dialog and exit
                    ShowFatalError(category, message, ex);

                    // Flush logs before exit
                    logger.Flush();

                    // Exit with error code
                    Environment.Exit(1);
                    break;
            }
        }

        /// <summary>
        /// Execute action with policy-based error handling
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="category">Error category</param>
        /// <param name="action">Action to execute</param>
        /// <param name="defaultValue">Value to return on error</param>
        /// <param name="context">Human-readable context</param>
        /// <returns>Action result or default value on error</returns>
        public static T SafeExecute<T>(ErrorCategory category, Func<T> action, T defaultValue = default, string context = null)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                Handle(category, ex, context);
                return defaultValue;
            }
        }

        /// <summary>
        /// Execute action with policy-based error handling (void return)
        /// </summary>
        /// <param name="category">Error category</param>
        /// <param name="action">Action to execute</param>
        /// <param name="context">Human-readable context</param>
        public static void SafeExecute(ErrorCategory category, Action action, string context = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Handle(category, ex, context);
            }
        }

        /// <summary>
        /// Execute action with custom error handling delegate
        /// Use this when you need custom recovery logic beyond the default policy
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="category">Error category</param>
        /// <param name="action">Action to execute</param>
        /// <param name="onError">Custom error handler (return value to use on error)</param>
        /// <param name="context">Human-readable context</param>
        /// <returns>Action result or error handler result</returns>
        public static T SafeExecute<T>(ErrorCategory category, Func<T> action, Func<Exception, T> onError, string context = null)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                Handle(category, ex, context);
                return onError(ex);
            }
        }

        /// <summary>
        /// Show non-modal notification to user (doesn't block execution)
        /// </summary>
        private static void ShowNotification(string title, string message, MessageBoxImage icon)
        {
            // Use Dispatcher to ensure we're on UI thread
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.HasShutdownStarted)
            {
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(
                            message,
                            title,
                            MessageBoxButton.OK,
                            icon);
                    }));
                }
                catch (TaskCanceledException)
                {
                    // Dispatcher is shutting down - fallback to console
                    Console.WriteLine($"[{icon}] {title}: {message}");
                }
            }
            else
            {
                // No UI available - just log to console
                Console.WriteLine($"[{icon}] {title}: {message}");
            }
        }

        /// <summary>
        /// Show modal fatal error dialog and prepare for exit
        /// </summary>
        private static void ShowFatalError(ErrorCategory category, string message, Exception ex)
        {
            var fullMessage = $"A fatal error has occurred:\n\n{message}\n\n" +
                             $"Category: {category}\n" +
                             $"Exception: {ex.GetType().Name}\n\n" +
                             $"The application will now exit.\n\n" +
                             $"Please check the log files for more details.";

            // Use Dispatcher if available, otherwise show synchronously
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.HasShutdownStarted)
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            fullMessage,
                            "Fatal Error - Application Terminating",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error,
                            MessageBoxResult.OK);
                    });
                }
                catch (Exception dispatcherEx)
                {
                    // Dispatcher failed - fallback to console
                    Console.WriteLine($"[FATAL ERROR] {fullMessage}");
                    Console.WriteLine($"(Dispatcher error: {dispatcherEx.Message})");
                }
            }
            else
            {
                Console.WriteLine($"[FATAL ERROR] {fullMessage}");
            }
        }

        /// <summary>
        /// Check if an exception should be retried based on its type
        /// Use this for implementing retry logic with transient errors
        /// </summary>
        public static bool IsTransientError(Exception ex)
        {
            // Network transient errors
            if (ex is System.Net.WebException ||
                ex is System.Net.Http.HttpRequestException ||
                ex is System.Net.Sockets.SocketException)
            {
                return true;
            }

            // IO transient errors (file locks, etc.)
            if (ex is IOException ioEx)
            {
                // Check for specific IO errors that are transient
                const int ERROR_SHARING_VIOLATION = 32;
                const int ERROR_LOCK_VIOLATION = 33;

                var hResult = ioEx.HResult & 0xFFFF;
                if (hResult == ERROR_SHARING_VIOLATION || hResult == ERROR_LOCK_VIOLATION)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Execute action with retry logic for transient errors
        /// </summary>
        /// <param name="category">Error category</param>
        /// <param name="action">Action to execute</param>
        /// <param name="maxRetries">Maximum number of retries (default 3)</param>
        /// <param name="delayMs">Delay between retries in milliseconds (default 100)</param>
        /// <param name="context">Human-readable context</param>
        public static T SafeExecuteWithRetry<T>(
            ErrorCategory category,
            Func<T> action,
            int maxRetries = 3,
            int delayMs = 100,
            string context = null)
        {
            var attempt = 0;
            Exception lastException = null;

            while (attempt <= maxRetries)
            {
                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // Check if this is a transient error worth retrying
                    if (!IsTransientError(ex) || attempt >= maxRetries)
                    {
                        // Non-transient or out of retries - handle normally
                        Handle(category, ex, context);
                        return default;
                    }

                    // Log retry attempt
                    Logger.Instance.Debug(category.ToString(),
                        $"Transient error on attempt {attempt + 1}/{maxRetries + 1}: {ex.Message}. Retrying...");

                    // Wait before retry (exponential backoff)
                    if (delayMs > 0)
                    {
                        System.Threading.Thread.Sleep(delayMs * (attempt + 1));
                    }

                    attempt++;
                }
            }

            // Should never reach here, but handle just in case
            if (lastException != null)
            {
                Handle(category, lastException, context);
            }
            return default;
        }
    }
}
