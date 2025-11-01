using System;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Severity level for notifications
    /// </summary>
    public enum NotificationSeverity
    {
        /// <summary>Success message (green)</summary>
        Success,
        /// <summary>Warning message (yellow/amber)</summary>
        Warning,
        /// <summary>Error message (red)</summary>
        Error,
        /// <summary>Informational message (blue/cyan)</summary>
        Info
    }

    /// <summary>
    /// Notification data for display
    /// </summary>
    public class Notification
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationSeverity Severity { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DurationMs { get; set; }
        public bool IsDismissed { get; set; }

        public Notification()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            IsDismissed = false;
        }
    }

    /// <summary>
    /// Event args for notification events
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        public Notification Notification { get; set; }
    }

    /// <summary>
    /// Interface for notification management
    /// Displays user-friendly toast/snackbar notifications for errors, warnings, and success messages
    /// </summary>
    public interface INotificationManager
    {
        /// <summary>
        /// Event raised when a notification should be shown
        /// </summary>
        event EventHandler<NotificationEventArgs> NotificationShown;

        /// <summary>
        /// Event raised when a notification should be dismissed
        /// </summary>
        event EventHandler<NotificationEventArgs> NotificationDismissed;

        /// <summary>
        /// Show a success notification (green)
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">Optional title (defaults to "Success")</param>
        /// <param name="durationMs">Duration in milliseconds (defaults to 5000)</param>
        void ShowSuccess(string message, string title = "Success", int durationMs = 5000);

        /// <summary>
        /// Show a warning notification (yellow/amber)
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">Optional title (defaults to "Warning")</param>
        /// <param name="durationMs">Duration in milliseconds (defaults to 5000)</param>
        void ShowWarning(string message, string title = "Warning", int durationMs = 5000);

        /// <summary>
        /// Show an error notification (red)
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">Optional title (defaults to "Error")</param>
        /// <param name="durationMs">Duration in milliseconds (defaults to 10000)</param>
        void ShowError(string message, string title = "Error", int durationMs = 10000);

        /// <summary>
        /// Show an info notification (blue/cyan)
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">Optional title (defaults to "Info")</param>
        /// <param name="durationMs">Duration in milliseconds (defaults to 5000)</param>
        void ShowInfo(string message, string title = "Info", int durationMs = 5000);

        /// <summary>
        /// Show a custom notification
        /// </summary>
        /// <param name="notification">The notification to show</param>
        void Show(Notification notification);

        /// <summary>
        /// Dismiss a specific notification by ID
        /// </summary>
        /// <param name="notificationId">The ID of the notification to dismiss</param>
        void Dismiss(Guid notificationId);

        /// <summary>
        /// Dismiss all notifications
        /// </summary>
        void DismissAll();
    }
}
