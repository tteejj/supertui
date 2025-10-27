using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    /// <summary>
    /// Error boundary that wraps widgets and catches exceptions
    /// Prevents one widget crash from taking down the entire application
    /// </summary>
    public class ErrorBoundary : ContentControl
    {
        private readonly WidgetBase widget;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private bool hasError = false;
        private Exception lastException;

        public ErrorBoundary(WidgetBase widget, ILogger logger, IThemeManager themeManager)
        {
            this.widget = widget;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

            try
            {
                // Set the widget as content
                this.Content = widget;
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget construction");
            }
        }

        /// <summary>
        /// Safely initialize the widget
        /// </summary>
        public void SafeInitialize()
        {
            if (hasError) return;

            try
            {
                widget.Initialize();
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget initialization");
            }
        }

        /// <summary>
        /// Safely activate the widget
        /// </summary>
        public void SafeActivate()
        {
            if (hasError) return;

            try
            {
                widget.OnActivated();
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget activation");
            }
        }

        /// <summary>
        /// Safely deactivate the widget
        /// </summary>
        public void SafeDeactivate()
        {
            if (hasError) return;

            try
            {
                widget.OnDeactivated();
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget deactivation");
            }
        }

        /// <summary>
        /// Safely dispose the widget
        /// </summary>
        public void SafeDispose()
        {
            try
            {
                widget?.Dispose();
            }
            catch (Exception ex)
            {
                logger?.Error("ErrorBoundary",
                    $"Error disposing widget {widget?.WidgetName ?? "Unknown"}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get the wrapped widget (for state persistence, etc.)
        /// </summary>
        public WidgetBase GetWidget() => widget;

        /// <summary>
        /// Check if the widget is in an error state
        /// </summary>
        public bool HasError => hasError;

        /// <summary>
        /// Attempt to recover from error by reinitializing the widget
        /// </summary>
        public bool TryRecover()
        {
            if (!hasError) return true;

            try
            {
                hasError = false;
                lastException = null;

                // Clear error UI
                this.Content = widget;

                // Try to reinitialize
                widget.Initialize();

                logger?.Info("ErrorBoundary", $"Successfully recovered widget: {widget.WidgetName}");
                return true;
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget recovery");
                return false;
            }
        }

        private void HandleError(Exception ex, string phase)
        {
            hasError = true;
            lastException = ex;

            // Log the error
            logger?.Error("ErrorBoundary",
                $"Widget error in {phase}: {widget?.WidgetName ?? "Unknown"} - {ex.Message}", ex);

            // Replace content with error UI
            this.Content = CreateErrorUI(phase, ex);
        }

        private UIElement CreateErrorUI(string phase, Exception ex)
        {
            var theme = themeManager.CurrentTheme;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 232, 17, 35)), // Error color with transparency
                BorderBrush = new SolidColorBrush(theme.Error),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(4)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Error icon
            var iconText = new TextBlock
            {
                Text = "⚠",
                FontSize = 48,
                Foreground = new SolidColorBrush(theme.Error),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Widget name
            var widgetNameText = new TextBlock
            {
                Text = $"Widget Error: {widget?.WidgetName ?? "Unknown"}",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Phase text
            var phaseText = new TextBlock
            {
                Text = $"Failed during: {phase}",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            // Error message
            var errorText = new TextBlock
            {
                Text = ex.Message,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Error),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Recovery button
            var recoveryButton = new Button
            {
                Content = "Try to Recover",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(15, 5, 15, 5),
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            recoveryButton.Click += (s, e) => TryRecover();

            // Instructions
            var instructionsText = new TextBlock
            {
                Text = "The widget encountered an error and was safely isolated.\nOther widgets continue to work normally.",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };

            stackPanel.Children.Add(iconText);
            stackPanel.Children.Add(widgetNameText);
            stackPanel.Children.Add(phaseText);
            stackPanel.Children.Add(errorText);
            stackPanel.Children.Add(recoveryButton);
            stackPanel.Children.Add(instructionsText);

            border.Child = stackPanel;
            return border;
        }
    }
}
