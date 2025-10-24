using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Simple counter widget - demonstrates independent state management
    /// Each instance maintains its own count, even when switching workspaces
    /// </summary>
    public class CounterWidget : WidgetBase, IThemeable
    {
        private Border containerBorder;
        private TextBlock titleText;
        private TextBlock countText;
        private TextBlock instructionText;

        private int count = 0;
        public int Count
        {
            get => count;
            set
            {
                count = value;
                OnPropertyChanged(nameof(Count));
                UpdateDisplay();
            }
        }

        public CounterWidget()
        {
            WidgetType = "Counter";
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Title
            titleText = new TextBlock
            {
                Text = "COUNTER",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Count display
            countText = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.SyntaxKeyword),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Instructions
            instructionText = new TextBlock
            {
                Text = "Press Up/Down arrows",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(countText);
            stackPanel.Children.Add(instructionText);

            containerBorder.Child = stackPanel;
            this.Content = containerBorder;
        }

        public override void Initialize()
        {
            Count = 0;
        }

        public override void OnWidgetKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    Count++;
                    e.Handled = true;
                    break;

                case Key.Down:
                    Count--;
                    e.Handled = true;
                    break;

                case Key.R:
                    Count = 0;
                    e.Handled = true;
                    break;
            }
        }

        public override void OnWidgetFocusReceived()
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            instructionText.Foreground = new SolidColorBrush(theme.Focus);
        }

        public override void OnWidgetFocusLost()
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            instructionText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
        }

        private void UpdateDisplay()
        {
            countText.Text = Count.ToString();
        }

        public override System.Collections.Generic.Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            state["Count"] = Count;
            return state;
        }

        public override void RestoreState(System.Collections.Generic.Dictionary<string, object> state)
        {
            if (state.ContainsKey("Count"))
            {
                Count = (int)state["Count"];
            }
        }

        protected override void OnDispose()
        {
            // No resources to dispose currently
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
                containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
            }

            if (countText != null)
            {
                countText.Foreground = new SolidColorBrush(theme.SyntaxKeyword);
            }

            if (instructionText != null)
            {
                // Update based on focus state
                instructionText.Foreground = new SolidColorBrush(
                    HasFocus ? theme.Focus : theme.ForegroundDisabled);
            }
        }
    }
}
