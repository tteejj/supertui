using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core.Components;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets.Overlays
{
    public class ConfirmationOverlay : OverlayBase
    {
        private readonly IThemeManager themeManager;
        private readonly string title;
        private readonly string message;
        private readonly Action onConfirm;
        private readonly Action onCancel;

        public ConfirmationOverlay(IThemeManager themeManager, string title, string message, Action onConfirm, Action onCancel)
        {
            this.themeManager = themeManager;
            this.title = title;
            this.message = message;
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;

            BuildUI();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            var container = new Border
            {
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Warning),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(20),
                MaxWidth = 400
            };

            var stackPanel = new StackPanel();

            var titleText = new TextBlock
            {
                Text = title,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Warning),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(titleText);

            var messageText = new TextBlock
            {
                Text = message,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(messageText);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var yesButton = new Button
            {
                Content = "Yes",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Success),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            yesButton.Click += (s, e) => onConfirm?.Invoke();
            buttonPanel.Children.Add(yesButton);

            var noButton = new Button
            {
                Content = "No",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Error),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            noButton.Click += (s, e) => onCancel?.Invoke();
            buttonPanel.Children.Add(noButton);

            stackPanel.Children.Add(buttonPanel);

            container.Child = stackPanel;
            this.Content = container;
        }

        public override void OnShown()
        {
            // Focus the 'No' button by default for safety
            var noButton = (this.Content as Border)?.Child as StackPanel;
            var buttonPanel = noButton?.Children[2] as StackPanel;
            var button = buttonPanel?.Children[1] as Button;
            button?.Focus();
        }

        public override bool HandleKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Y)
            {
                onConfirm?.Invoke();
                e.Handled = true;
                return true;
            }
            else if (e.Key == Key.N || e.Key == Key.Escape)
            {
                onCancel?.Invoke();
                e.Handled = true;
                return true;
            }

            return false;
        }
    }
}
