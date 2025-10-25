using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Simple clock widget showing current time
    /// </summary>
    public class ClockWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;

        private StandardWidgetFrame frame;
        private Border containerBorder;
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

        /// <summary>
        /// DI constructor - preferred for new code
        /// </summary>
        public ClockWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            WidgetType = "Clock";
            BuildUI();
        }

        /// <summary>
        /// Parameterless constructor for backward compatibility
        /// </summary>
        public ClockWidget()
            : this(Logger.Instance, ThemeManager.Instance, ConfigurationManager.Instance)
        {
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            // Create standard frame
            frame = new StandardWidgetFrame(themeManager)
            {
                Title = "CLOCK"
            };
            frame.SetStandardShortcuts("Updates every second", "?: Help");

            // Container
            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
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
                Foreground = new SolidColorBrush(theme.Info),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Date display
            dateText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                FontSize = 14,
                Foreground = new SolidColorBrush(theme.Foreground),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(timeText);
            stackPanel.Children.Add(dateText);
            containerBorder.Child = stackPanel;

            frame.Content = containerBorder;
            this.Content = frame;

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
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateTime();
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

        protected override void OnDispose()
        {
            // Stop and dispose timer
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick;
                timer = null;
            }

            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
                containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (timeText != null)
            {
                timeText.Foreground = new SolidColorBrush(theme.Info);
            }

            if (dateText != null)
            {
                dateText.Foreground = new SolidColorBrush(theme.Foreground);
            }
        }
    }
}
