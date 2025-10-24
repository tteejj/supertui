using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Simple counter widget - demonstrates independent state management
    /// Each instance maintains its own count, even when switching workspaces
    /// </summary>
    public class CounterWidget : WidgetBase
    {
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
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Title
            var title = new TextBlock
            {
                Text = "COUNTER",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Instructions
            instructionText = new TextBlock
            {
                Text = "Press Up/Down arrows",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(title);
            stackPanel.Children.Add(countText);
            stackPanel.Children.Add(instructionText);

            border.Child = stackPanel;
            this.Content = border;
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
            instructionText.Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176));
        }

        public override void OnWidgetFocusLost()
        {
            instructionText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
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
    }
}
