using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// UI component that displays notifications as toast/snackbar elements
    /// Stacks notifications vertically in top-right corner
    /// Supports animations and auto-dismiss
    /// </summary>
    public class NotificationPanel : StackPanel
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly INotificationManager notificationManager;
        private readonly Dictionary<Guid, Border> notificationViews = new Dictionary<Guid, Border>();
        private readonly object lockObject = new object();

        public NotificationPanel(ILogger logger, IThemeManager themeManager, INotificationManager notificationManager)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.notificationManager = notificationManager ?? throw new ArgumentNullException(nameof(notificationManager));

            // Panel configuration
            Orientation = Orientation.Vertical;
            VerticalAlignment = VerticalAlignment.Top;
            HorizontalAlignment = HorizontalAlignment.Right;
            Margin = new Thickness(0, 10, 10, 0);

            // Subscribe to notification events
            notificationManager.NotificationShown += OnNotificationShown;
            notificationManager.NotificationDismissed += OnNotificationDismissed;

            logger.Debug("NotificationPanel", "Initialized notification panel");
        }

        /// <summary>
        /// Handle notification shown event
        /// </summary>
        private void OnNotificationShown(object sender, NotificationEventArgs e)
        {
            // Must be on UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnNotificationShown(sender, e));
                return;
            }

            lock (lockObject)
            {
                var notification = e.Notification;

                // Create notification UI
                var notificationView = CreateNotificationView(notification);

                // Add to panel
                Children.Add(notificationView);
                notificationViews[notification.Id] = notificationView;

                // Animate in
                AnimateIn(notificationView);

                logger.Debug("NotificationPanel", $"Showing notification: {notification.Title}");
            }
        }

        /// <summary>
        /// Handle notification dismissed event
        /// </summary>
        private void OnNotificationDismissed(object sender, NotificationEventArgs e)
        {
            // Must be on UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnNotificationDismissed(sender, e));
                return;
            }

            lock (lockObject)
            {
                var notification = e.Notification;

                if (notificationViews.TryGetValue(notification.Id, out var notificationView))
                {
                    // Animate out, then remove
                    AnimateOut(notificationView, () =>
                    {
                        Children.Remove(notificationView);
                        notificationViews.Remove(notification.Id);
                    });

                    logger.Debug("NotificationPanel", $"Dismissing notification: {notification.Title}");
                }
            }
        }

        /// <summary>
        /// Create the visual representation of a notification
        /// </summary>
        private Border CreateNotificationView(Notification notification)
        {
            var theme = themeManager.CurrentTheme;

            // Determine colors based on severity
            Color backgroundColor;
            Color borderColor;
            Color textColor;
            string icon;

            switch (notification.Severity)
            {
                case NotificationSeverity.Success:
                    backgroundColor = Color.FromArgb(230, theme.Success.R, theme.Success.G, theme.Success.B);
                    borderColor = theme.Success;
                    textColor = theme.Background;
                    icon = "✓";
                    break;
                case NotificationSeverity.Warning:
                    backgroundColor = Color.FromArgb(230, theme.Warning.R, theme.Warning.G, theme.Warning.B);
                    borderColor = theme.Warning;
                    textColor = theme.Background;
                    icon = "⚠";
                    break;
                case NotificationSeverity.Error:
                    backgroundColor = Color.FromArgb(230, theme.Error.R, theme.Error.G, theme.Error.B);
                    borderColor = theme.Error;
                    textColor = theme.Background;
                    icon = "✗";
                    break;
                case NotificationSeverity.Info:
                default:
                    backgroundColor = Color.FromArgb(230, theme.Info.R, theme.Info.G, theme.Info.B);
                    borderColor = theme.Info;
                    textColor = theme.Background;
                    icon = "ℹ";
                    break;
            }

            // Container
            var container = new Border
            {
                Background = new SolidColorBrush(backgroundColor),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 10),
                Width = 350,
                MaxHeight = 150,
                Padding = new Thickness(12),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 10,
                    ShadowDepth = 3,
                    Opacity = 0.5
                }
            };

            // Content grid
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Content
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Dismiss button

            // Icon
            var iconText = new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(textColor),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);

            // Content stack
            var contentStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Title
            var titleText = new TextBlock
            {
                Text = notification.Title,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(textColor),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            contentStack.Children.Add(titleText);

            // Message
            var messageText = new TextBlock
            {
                Text = notification.Message,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(textColor),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 80
            };
            contentStack.Children.Add(messageText);

            Grid.SetColumn(contentStack, 1);
            grid.Children.Add(contentStack);

            // Dismiss button
            var dismissButton = new Button
            {
                Content = "×",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(textColor),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Width = 30,
                Height = 30,
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(0)
            };

            dismissButton.Click += (s, e) =>
            {
                notificationManager.Dismiss(notification.Id);
            };

            Grid.SetColumn(dismissButton, 2);
            grid.Children.Add(dismissButton);

            container.Child = grid;

            return container;
        }

        /// <summary>
        /// Animate notification sliding in from right
        /// </summary>
        private void AnimateIn(Border notificationView)
        {
            // Start off-screen to the right
            var translateTransform = new TranslateTransform(400, 0);
            notificationView.RenderTransform = translateTransform;

            // Slide in animation
            var slideAnimation = new DoubleAnimation
            {
                From = 400,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            // Fade in animation
            var fadeAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            translateTransform.BeginAnimation(TranslateTransform.XProperty, slideAnimation);
            notificationView.BeginAnimation(OpacityProperty, fadeAnimation);
        }

        /// <summary>
        /// Animate notification sliding out to the right
        /// </summary>
        private void AnimateOut(Border notificationView, Action onComplete)
        {
            var translateTransform = notificationView.RenderTransform as TranslateTransform
                ?? new TranslateTransform(0, 0);

            if (notificationView.RenderTransform == null)
            {
                notificationView.RenderTransform = translateTransform;
            }

            // Slide out animation
            var slideAnimation = new DoubleAnimation
            {
                From = 0,
                To = 400,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            // Fade out animation
            var fadeAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            slideAnimation.Completed += (s, e) =>
            {
                onComplete?.Invoke();
            };

            translateTransform.BeginAnimation(TranslateTransform.XProperty, slideAnimation);
            notificationView.BeginAnimation(OpacityProperty, fadeAnimation);
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            notificationManager.NotificationShown -= OnNotificationShown;
            notificationManager.NotificationDismissed -= OnNotificationDismissed;

            Children.Clear();
            notificationViews.Clear();

            logger.Debug("NotificationPanel", "Disposed notification panel");
        }
    }
}
