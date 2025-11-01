using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Manages user-facing notifications (toast/snackbar style)
    /// Thread-safe implementation with auto-dismiss timers
    /// </summary>
    public class NotificationManager : INotificationManager, IDisposable
    {
        private static NotificationManager instance;

        /// <summary>
        /// Singleton instance for infrastructure use.
        /// Components should use injected INotificationManager. Infrastructure may use Instance when DI is unavailable.
        /// </summary>
        public static NotificationManager Instance => instance ??= new NotificationManager();

        private readonly object lockObject = new object();
        private readonly List<Notification> activeNotifications = new List<Notification>();
        private readonly Dictionary<Guid, Timer> dismissTimers = new Dictionary<Guid, Timer>();
        private readonly ILogger logger;
        private readonly Dispatcher dispatcher;

        // Events
        public event EventHandler<NotificationEventArgs> NotificationShown;
        public event EventHandler<NotificationEventArgs> NotificationDismissed;

        private NotificationManager()
        {
            logger = Logger.Instance;
            // Get the dispatcher from the current application
            dispatcher = System.Windows.Application.Current?.Dispatcher;

            if (dispatcher == null)
            {
                logger?.Warning("NotificationManager", "Dispatcher is null - notifications may not work correctly");
            }
        }

        /// <summary>
        /// Show a success notification (green)
        /// </summary>
        public void ShowSuccess(string message, string title = "Success", int durationMs = 5000)
        {
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Severity = NotificationSeverity.Success,
                DurationMs = durationMs
            };
            Show(notification);
        }

        /// <summary>
        /// Show a warning notification (yellow/amber)
        /// </summary>
        public void ShowWarning(string message, string title = "Warning", int durationMs = 5000)
        {
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Severity = NotificationSeverity.Warning,
                DurationMs = durationMs
            };
            Show(notification);
        }

        /// <summary>
        /// Show an error notification (red)
        /// </summary>
        public void ShowError(string message, string title = "Error", int durationMs = 10000)
        {
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Severity = NotificationSeverity.Error,
                DurationMs = durationMs
            };
            Show(notification);
        }

        /// <summary>
        /// Show an info notification (blue/cyan)
        /// </summary>
        public void ShowInfo(string message, string title = "Info", int durationMs = 5000)
        {
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Severity = NotificationSeverity.Info,
                DurationMs = durationMs
            };
            Show(notification);
        }

        /// <summary>
        /// Show a custom notification
        /// Thread-safe - can be called from any thread
        /// </summary>
        public void Show(Notification notification)
        {
            if (notification == null)
            {
                logger?.Warning("NotificationManager", "Attempted to show null notification");
                return;
            }

            // Validate message
            if (string.IsNullOrWhiteSpace(notification.Message))
            {
                logger?.Warning("NotificationManager", "Attempted to show notification with empty message");
                return;
            }

            // Thread-safe operation
            lock (lockObject)
            {
                // Add to active notifications
                activeNotifications.Add(notification);

                // Log the notification
                var logLevel = notification.Severity switch
                {
                    NotificationSeverity.Error => LogLevel.Error,
                    NotificationSeverity.Warning => LogLevel.Warning,
                    NotificationSeverity.Success => LogLevel.Info,
                    NotificationSeverity.Info => LogLevel.Info,
                    _ => LogLevel.Info
                };

                logger?.Log(logLevel, "NotificationManager",
                    $"[{notification.Severity}] {notification.Title}: {notification.Message}");

                // Set up auto-dismiss timer if duration > 0
                if (notification.DurationMs > 0)
                {
                    var timer = new Timer(
                        _ => AutoDismiss(notification.Id),
                        null,
                        notification.DurationMs,
                        Timeout.Infinite);

                    dismissTimers[notification.Id] = timer;
                }

                // Raise event on UI thread
                RaiseNotificationShown(notification);
            }
        }

        /// <summary>
        /// Dismiss a specific notification by ID
        /// Thread-safe - can be called from any thread
        /// </summary>
        public void Dismiss(Guid notificationId)
        {
            lock (lockObject)
            {
                var notification = activeNotifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification == null)
                {
                    return; // Already dismissed or never existed
                }

                // Mark as dismissed
                notification.IsDismissed = true;

                // Cancel timer if exists
                if (dismissTimers.TryGetValue(notificationId, out var timer))
                {
                    timer.Dispose();
                    dismissTimers.Remove(notificationId);
                }

                // Remove from active list
                activeNotifications.RemoveAll(n => n.Id == notificationId);

                // Raise event on UI thread
                RaiseNotificationDismissed(notification);
            }
        }

        /// <summary>
        /// Dismiss all notifications
        /// </summary>
        public void DismissAll()
        {
            lock (lockObject)
            {
                var notificationIds = activeNotifications.Select(n => n.Id).ToList();
                foreach (var id in notificationIds)
                {
                    Dismiss(id);
                }
            }
        }

        /// <summary>
        /// Auto-dismiss timer callback
        /// </summary>
        private void AutoDismiss(Guid notificationId)
        {
            Dismiss(notificationId);
        }

        /// <summary>
        /// Raise NotificationShown event on UI thread
        /// </summary>
        private void RaiseNotificationShown(Notification notification)
        {
            if (NotificationShown == null)
            {
                return;
            }

            var eventArgs = new NotificationEventArgs { Notification = notification };

            // Ensure event is raised on UI thread
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    NotificationShown?.Invoke(this, eventArgs);
                }), DispatcherPriority.Normal);
            }
            else
            {
                NotificationShown?.Invoke(this, eventArgs);
            }
        }

        /// <summary>
        /// Raise NotificationDismissed event on UI thread
        /// </summary>
        private void RaiseNotificationDismissed(Notification notification)
        {
            if (NotificationDismissed == null)
            {
                return;
            }

            var eventArgs = new NotificationEventArgs { Notification = notification };

            // Ensure event is raised on UI thread
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    NotificationDismissed?.Invoke(this, eventArgs);
                }), DispatcherPriority.Normal);
            }
            else
            {
                NotificationDismissed?.Invoke(this, eventArgs);
            }
        }

        /// <summary>
        /// Get all active notifications (for debugging)
        /// </summary>
        public IReadOnlyList<Notification> GetActiveNotifications()
        {
            lock (lockObject)
            {
                return activeNotifications.ToList();
            }
        }

        /// <summary>
        /// Dispose all resources
        /// </summary>
        public void Dispose()
        {
            lock (lockObject)
            {
                // Dispose all timers
                foreach (var timer in dismissTimers.Values)
                {
                    timer?.Dispose();
                }
                dismissTimers.Clear();

                // Clear notifications
                activeNotifications.Clear();
            }
        }
    }
}
