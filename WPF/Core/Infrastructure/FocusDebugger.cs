using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Ultra-verbose focus operation tracking for debugging
    /// Logs every focus operation with complete context: thread, dispatcher priority, stack trace, timing
    /// Use LogLevel.Focus to enable
    /// </summary>
    public static class FocusDebugger
    {
        private static ILogger logger;
        private static bool isEnabled = true;

        public static void Initialize(ILogger log)
        {
            logger = log;
        }

        public static void Enable()
        {
            isEnabled = true;
        }

        public static void Disable()
        {
            isEnabled = false;
        }

        /// <summary>
        /// Log the start of a focus operation with full context
        /// </summary>
        public static void LogFocusOperation(
            string eventName,
            UIElement target = null,
            string paneName = null,
            string additionalInfo = null,
            [CallerMemberName] string callerMethod = "",
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0)
        {
            if (!isEnabled || logger == null) return;

            try
            {
                var beforeFocus = Keyboard.FocusedElement;
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var thread = Thread.CurrentThread.ManagedThreadId;

                var sb = new StringBuilder();
                sb.AppendLine($"╔═══════════════════════════════════════════════════════════════");
                sb.AppendLine($"║ [FOCUS] Event: {eventName}");
                sb.AppendLine($"║   Timestamp: {timestamp}");
                sb.AppendLine($"║   Source: {callerMethod} in {Path.GetFileName(callerFile)}:{callerLine}");
                sb.AppendLine($"║   Thread: {thread}");

                // Check if on UI thread
                var isUiThread = Application.Current?.Dispatcher?.CheckAccess() ?? false;
                sb.AppendLine($"║   On UI Thread: {isUiThread}");

                // Get current dispatcher operation priority if available
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    sb.AppendLine($"║   Dispatcher HasShutdownStarted: {dispatcher.HasShutdownStarted}");
                }

                if (target != null)
                {
                    var fwElement = target as FrameworkElement;
                    sb.AppendLine($"║   Target Type: {target.GetType().Name}");
                    if (fwElement != null)
                    {
                        sb.AppendLine($"║   Target Name: {fwElement.Name ?? "(unnamed)"}");
                        sb.AppendLine($"║   Target IsLoaded: {fwElement.IsLoaded}");
                    }
                    sb.AppendLine($"║   Target IsVisible: {target.IsVisible}");
                    sb.AppendLine($"║   Target Focusable: {target.Focusable}");
                    sb.AppendLine($"║   Target IsEnabled: {target.IsEnabled}");
                    sb.AppendLine($"║   Target IsKeyboardFocused: {target.IsKeyboardFocused}");
                    sb.AppendLine($"║   Target IsKeyboardFocusWithin: {target.IsKeyboardFocusWithin}");
                }

                sb.AppendLine($"║   Current Keyboard.FocusedElement: {beforeFocus?.GetType().Name ?? "null"}");

                if (!string.IsNullOrEmpty(paneName))
                {
                    sb.AppendLine($"║   Pane: {paneName}");
                }

                if (!string.IsNullOrEmpty(additionalInfo))
                {
                    sb.AppendLine($"║   Info: {additionalInfo}");
                }

                // Log stack trace (top 5 calls for context)
                var stackTrace = new StackTrace(1, true);
                var frames = stackTrace.GetFrames()?.Take(5);
                if (frames != null)
                {
                    sb.AppendLine($"║   Call Stack:");
                    foreach (var frame in frames)
                    {
                        var method = frame.GetMethod();
                        var fileName = Path.GetFileName(frame.GetFileName());
                        var lineNumber = frame.GetFileLineNumber();
                        sb.AppendLine($"║     ← {method?.DeclaringType?.Name}.{method?.Name} ({fileName}:{lineNumber})");
                    }
                }

                sb.AppendLine($"╚═══════════════════════════════════════════════════════════════");

                logger.Log(LogLevel.Focus, "FocusDebug", sb.ToString());
            }
            catch (Exception ex)
            {
                // Don't let debug logging break the app
                logger?.Log(LogLevel.Error, "FocusDebug", $"Error in LogFocusOperation: {ex.Message}");
            }
        }

        /// <summary>
        /// Log the result of a focus operation
        /// </summary>
        public static void LogFocusResult(
            string eventName,
            bool success,
            UIElement target = null,
            long durationMs = 0,
            string errorMessage = null)
        {
            if (!isEnabled || logger == null) return;

            try
            {
                var afterFocus = Keyboard.FocusedElement;
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                var sb = new StringBuilder();
                sb.AppendLine($"╔═══════════════════════════════════════════════════════════════");
                sb.AppendLine($"║ [FOCUS] Result: {eventName}");
                sb.AppendLine($"║   Timestamp: {timestamp}");
                sb.AppendLine($"║   Success: {success}");
                sb.AppendLine($"║   Duration: {durationMs}ms");

                if (target != null)
                {
                    sb.AppendLine($"║   Target: {target.GetType().Name}");
                    sb.AppendLine($"║   Target IsKeyboardFocused: {target.IsKeyboardFocused}");
                    sb.AppendLine($"║   Target IsKeyboardFocusWithin: {target.IsKeyboardFocusWithin}");
                }

                sb.AppendLine($"║   After Keyboard.FocusedElement: {afterFocus?.GetType().Name ?? "null"}");

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    sb.AppendLine($"║   Error: {errorMessage}");
                }

                sb.AppendLine($"╚═══════════════════════════════════════════════════════════════");

                logger.Log(LogLevel.Focus, "FocusDebug", sb.ToString());
            }
            catch (Exception ex)
            {
                logger?.Log(LogLevel.Error, "FocusDebug", $"Error in LogFocusResult: {ex.Message}");
            }
        }

        /// <summary>
        /// Log a dispatcher operation being queued
        /// </summary>
        public static void LogDispatcherOperation(
            string operationName,
            DispatcherPriority priority,
            string context = null,
            [CallerMemberName] string callerMethod = "",
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0)
        {
            if (!isEnabled || logger == null) return;

            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var thread = Thread.CurrentThread.ManagedThreadId;

                var sb = new StringBuilder();
                sb.AppendLine($"╔═══════════════════════════════════════════════════════════════");
                sb.AppendLine($"║ [FOCUS] Dispatcher Operation: {operationName}");
                sb.AppendLine($"║   Timestamp: {timestamp}");
                sb.AppendLine($"║   Priority: {priority} ({(int)priority})");
                sb.AppendLine($"║   Thread: {thread}");
                sb.AppendLine($"║   Source: {callerMethod} in {Path.GetFileName(callerFile)}:{callerLine}");

                if (!string.IsNullOrEmpty(context))
                {
                    sb.AppendLine($"║   Context: {context}");
                }

                sb.AppendLine($"╚═══════════════════════════════════════════════════════════════");

                logger.Log(LogLevel.Focus, "FocusDebug", sb.ToString());
            }
            catch (Exception ex)
            {
                logger?.Log(LogLevel.Error, "FocusDebug", $"Error in LogDispatcherOperation: {ex.Message}");
            }
        }

        /// <summary>
        /// Log when a pane's state changes
        /// </summary>
        public static void LogPaneStateChange(
            string paneName,
            string stateChange,
            bool isActive = false,
            bool isKeyboardFocusWithin = false,
            string additionalInfo = null)
        {
            if (!isEnabled || logger == null) return;

            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var currentFocus = Keyboard.FocusedElement;

                var sb = new StringBuilder();
                sb.AppendLine($"╔═══════════════════════════════════════════════════════════════");
                sb.AppendLine($"║ [FOCUS] Pane State Change: {paneName}");
                sb.AppendLine($"║   Timestamp: {timestamp}");
                sb.AppendLine($"║   Change: {stateChange}");
                sb.AppendLine($"║   IsActive: {isActive}");
                sb.AppendLine($"║   IsKeyboardFocusWithin: {isKeyboardFocusWithin}");
                sb.AppendLine($"║   Current Keyboard.FocusedElement: {currentFocus?.GetType().Name ?? "null"}");

                if (!string.IsNullOrEmpty(additionalInfo))
                {
                    sb.AppendLine($"║   Info: {additionalInfo}");
                }

                sb.AppendLine($"╚═══════════════════════════════════════════════════════════════");

                logger.Log(LogLevel.Focus, "FocusDebug", sb.ToString());
            }
            catch (Exception ex)
            {
                logger?.Log(LogLevel.Error, "FocusDebug", $"Error in LogPaneStateChange: {ex.Message}");
            }
        }
    }
}
