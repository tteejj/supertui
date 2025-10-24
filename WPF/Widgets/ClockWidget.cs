using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Simple clock widget showing current time
    /// </summary>
    public class ClockWidget : WidgetBase
    {
        private TextBlock timeText;
        private TextBlock dateText;
        private DispatcherTimer timer;

        private string currentTime;
        public string CurrentTime
        {
            get => currentTime;
            set
            {
                currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }

        private string currentDate;
        public string CurrentDate
        {
            get => currentDate;
            set
            {
                currentDate = value;
                OnPropertyChanged(nameof(CurrentDate));
            }
        }

        public ClockWidget()
        {
            WidgetType = "Clock";
            BuildUI();
        }

        private void BuildUI()
        {
            // Container
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Time display
            timeText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)), // Terminal accent
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Date display
            dateText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(timeText);
            stackPanel.Children.Add(dateText);
            border.Child = stackPanel;

            this.Content = border;

            // Bind to properties (for demonstration of data binding)
            timeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CurrentTime") { Source = this });
            dateText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CurrentDate") { Source = this });
        }

        public override void Initialize()
        {
            // Update immediately
            UpdateTime();

            // Set up timer to update every second
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (s, e) => UpdateTime();
            timer.Start();
        }

        private void UpdateTime()
        {
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");
            CurrentDate = now.ToString("dddd, MMMM dd, yyyy");
        }

        public override void OnActivated()
        {
            // Resume timer when workspace is shown
            timer?.Start();
        }

        public override void OnDeactivated()
        {
            // Pause timer when workspace is hidden (save CPU)
            timer?.Stop();
        }
    }
}
