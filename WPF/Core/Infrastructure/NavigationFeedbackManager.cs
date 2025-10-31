using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core.Components;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Manages visual and audio feedback when navigation fails at grid edges
    /// Provides:
    /// - Visual feedback (border flash on current pane)
    /// - Audio feedback (optional system beep)
    /// - Debug logging
    /// - Configurable behavior via ConfigurationManager
    /// </summary>
    public class NavigationFeedbackManager
    {
        private readonly ILogger logger;
        private readonly IConfigurationManager config;
        private readonly IThemeManager themeManager;
        private DispatcherTimer currentFeedbackTimer;

        public NavigationFeedbackManager(
            ILogger logger,
            IConfigurationManager config,
            IThemeManager themeManager)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        }

        /// <summary>
        /// Shows navigation feedback when navigation attempt fails at grid edge
        /// </summary>
        /// <param name="pane">The pane where navigation was attempted</param>
        /// <param name="direction">The direction that was attempted</param>
        public void ShowNavigationEdgeFeedback(PaneBase pane, FocusDirection direction)
        {
            if (pane == null)
                return;

            try
            {
                // Get configuration settings
                bool enableVisualFeedback = config.Get("Navigation.EnableVisualFeedback", true);
                bool enableAudioFeedback = config.Get("Navigation.EnableAudioFeedback", true);
                int feedbackDurationMs = config.Get("Navigation.FeedbackDurationMs", 200);

                logger.Log(LogLevel.Debug, "NavigationFeedback",
                    $"Navigation hit edge: {pane.PaneName} attempted {direction}");

                // Play audio feedback if enabled
                if (enableAudioFeedback)
                {
                    PlaySystemBeep();
                }

                // Show visual feedback if enabled
                if (enableVisualFeedback)
                {
                    ShowBorderFlash(pane, feedbackDurationMs);
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, "NavigationFeedback",
                    $"Error showing navigation feedback: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a brief border flash on the pane to indicate edge hit
        /// </summary>
        private void ShowBorderFlash(PaneBase pane, int durationMs)
        {
            try
            {
                // Stop any existing feedback
                StopCurrentFeedback();

                // Save original border
                var originalBrush = pane.GetBorderBrush();
                var originalThickness = pane.GetBorderThickness();

                // Apply edge feedback style (use Warning color from theme)
                var warningColor = themeManager.CurrentTheme?.Warning ?? Colors.Orange;
                var edgeFeedbackColor = new SolidColorBrush(warningColor);
                pane.SetBorderBrush(edgeFeedbackColor);
                pane.SetBorderThickness(new Thickness(3)); // Make border more visible

                // Schedule restoration
                currentFeedbackTimer = new DispatcherTimer();
                currentFeedbackTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
                currentFeedbackTimer.Tick += (s, e) =>
                {
                    try
                    {
                        // Restore original style
                        pane.SetBorderBrush(originalBrush);
                        pane.SetBorderThickness(originalThickness);
                        currentFeedbackTimer.Stop();
                        currentFeedbackTimer = null;
                    }
                    catch (Exception ex)
                    {
                        logger.Log(LogLevel.Warning, "NavigationFeedback",
                            $"Error restoring border style: {ex.Message}");
                    }
                };
                currentFeedbackTimer.Start();
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, "NavigationFeedback",
                    $"Error showing border flash: {ex.Message}");
            }
        }

        /// <summary>
        /// Play a system beep to indicate navigation edge
        /// </summary>
        private void PlaySystemBeep()
        {
            try
            {
                // Use system beep for subtle audio feedback
                System.Media.SystemSounds.Beep.Play();
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Debug, "NavigationFeedback",
                    $"Could not play system beep: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop any currently active feedback
        /// </summary>
        private void StopCurrentFeedback()
        {
            if (currentFeedbackTimer != null)
            {
                currentFeedbackTimer.Stop();
                currentFeedbackTimer = null;
            }
        }

        /// <summary>
        /// Cleanup on application shutdown
        /// </summary>
        public void Cleanup()
        {
            StopCurrentFeedback();
        }
    }

    /// <summary>
    /// Extension methods for PaneBase to support border manipulation for feedback
    /// </summary>
    public static class NavigationFeedbackExtensions
    {
        /// <summary>
        /// Get the current border brush from a pane
        /// </summary>
        public static Brush GetBorderBrush(this PaneBase pane)
        {
            // Access the pane's container border through reflection
            var containerBorderField = typeof(PaneBase).GetField("containerBorder",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (containerBorderField?.GetValue(pane) is Border border)
            {
                return border.BorderBrush;
            }
            return new SolidColorBrush(Colors.Gray);
        }

        /// <summary>
        /// Set the border brush on a pane
        /// </summary>
        public static void SetBorderBrush(this PaneBase pane, Brush brush)
        {
            var containerBorderField = typeof(PaneBase).GetField("containerBorder",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (containerBorderField?.GetValue(pane) is Border border)
            {
                border.BorderBrush = brush;
            }
        }

        /// <summary>
        /// Get the current border thickness from a pane
        /// </summary>
        public static Thickness GetBorderThickness(this PaneBase pane)
        {
            var containerBorderField = typeof(PaneBase).GetField("containerBorder",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (containerBorderField?.GetValue(pane) is Border border)
            {
                return border.BorderThickness;
            }
            return new Thickness(1);
        }

        /// <summary>
        /// Set the border thickness on a pane
        /// </summary>
        public static void SetBorderThickness(this PaneBase pane, Thickness thickness)
        {
            var containerBorderField = typeof(PaneBase).GetField("containerBorder",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (containerBorderField?.GetValue(pane) is Border border)
            {
                border.BorderThickness = thickness;
            }
        }
    }
}
