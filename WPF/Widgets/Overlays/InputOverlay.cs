using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core.Components;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets.Overlays
{
    public class InputOverlay : OverlayBase
    {
        private readonly IThemeManager themeManager;
        private readonly string title;
        private readonly string prompt;
        private readonly string defaultValue;

        private TextBox inputTextBox;
        private Action<string> onConfirm;
        private Action onCancel;

        public InputOverlay(IThemeManager themeManager, string title, string prompt, string defaultValue, Action<string> onConfirm, Action onCancel)
        {
            this.themeManager = themeManager;
            this.title = title;
            this.prompt = prompt;
            this.defaultValue = defaultValue;
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
                BorderBrush = new SolidColorBrush(theme.Primary),
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
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(titleText);

            var promptText = new TextBlock
            {
                Text = prompt,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(promptText);

            inputTextBox = new TextBox
            {
                Text = defaultValue,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };
            stackPanel.Children.Add(inputTextBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Success),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            okButton.Click += (s, e) => onConfirm?.Invoke(inputTextBox.Text);
            buttonPanel.Children.Add(okButton);

            var cancelButton = new Button
            {
                Content = "Cancel",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Error),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            cancelButton.Click += (s, e) => onCancel?.Invoke();
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(buttonPanel);

            container.Child = stackPanel;
            this.Content = container;
        }

        public override void OnShown()
        {
            inputTextBox.Focus();
            inputTextBox.SelectAll();
        }

        public override bool HandleKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                onConfirm?.Invoke(inputTextBox.Text);
                e.Handled = true;
                return true;
            }
            else if (e.Key == Key.Escape)
            {
                onCancel?.Invoke();
                e.Handled = true;
                return true;
            }

            return false;
        }
    }
}
