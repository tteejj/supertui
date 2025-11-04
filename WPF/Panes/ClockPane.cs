// ClockPane.cs - Large clock display with date and time
// Simple, clean clock for focus and time awareness

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Clock pane - Large time and date display
    /// </summary>
    public class ClockPane : PaneBase
    {
        // UI Components
        private Grid mainGrid;
        private TextBlock timeDisplay;
        private TextBlock dateDisplay;
        private TextBlock dayDisplay;
        private TextBlock secondsDisplay;
        private DispatcherTimer clockTimer;

        // Theme colors
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush dimBrush;
        private SolidColorBrush surfaceBrush;

        // State
        private bool show24Hour = true;

        public ClockPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext)
            : base(logger, themeManager, projectContext)
        {
            PaneName = "Clock";
            PaneIcon = "🕐";
        }

        public override void Initialize()
        {
            base.Initialize();
            CacheThemeColors();
            RegisterPaneShortcuts();

            // Initialize clock timer
            clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            clockTimer.Tick += ClockTimer_Tick;
            clockTimer.Start();

            // Focus grid for keyboard input
            this.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.Input.Keyboard.Focus(mainGrid);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override UIElement BuildContent()
        {
            CacheThemeColors();

            mainGrid = new Grid { Focusable = true }; // No background - let PaneBase border show through for focus indicator
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Time
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Seconds
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Day
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Date
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Help
            mainGrid.KeyDown += MainGrid_KeyDown;

            // Header
            var header = new TextBlock
            {
                Text = "🕐 Clock",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Time display (large HH:MM)
            timeDisplay = new TextBlock
            {
                Text = "00:00",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 120,
                FontWeight = FontWeights.Bold,
                Foreground = fgBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(timeDisplay, 1);
            mainGrid.Children.Add(timeDisplay);

            // Seconds display (smaller)
            secondsDisplay = new TextBlock
            {
                Text = ":00",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 48,
                Foreground = dimBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -20, 0, 16)
            };
            Grid.SetRow(secondsDisplay, 2);
            mainGrid.Children.Add(secondsDisplay);

            // Day of week
            dayDisplay = new TextBlock
            {
                Text = "Monday",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 28,
                Foreground = accentBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(dayDisplay, 3);
            mainGrid.Children.Add(dayDisplay);

            // Date display
            dateDisplay = new TextBlock
            {
                Text = "January 1, 2025",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 20,
                Foreground = dimBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24)
            };
            Grid.SetRow(dateDisplay, 4);
            mainGrid.Children.Add(dateDisplay);

            // Help text
            var helpText = new TextBlock
            {
                Text = "T:Toggle 12/24hr format",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = dimBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(16, 0, 16, 16),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(helpText, 5);
            mainGrid.Children.Add(helpText);

            UpdateClock();
            return mainGrid;
        }

        private void CacheThemeColors()
        {
            var theme = themeManager.CurrentTheme;
            bgBrush = new SolidColorBrush(theme.Background);
            fgBrush = new SolidColorBrush(theme.Foreground);
            accentBrush = new SolidColorBrush(theme.Primary);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
            surfaceBrush = new SolidColorBrush(theme.Surface);
        }

        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;
            shortcuts.RegisterForPane(PaneName, Key.T, ModifierKeys.None, () => ToggleTimeFormat(), "Toggle 12/24 hour format");
        }

        private void MainGrid_KeyDown(object sender, KeyEventArgs e)
        {
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
                e.Handled = true;
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;

            // Time
            if (show24Hour)
            {
                timeDisplay.Text = now.ToString("HH:mm");
            }
            else
            {
                timeDisplay.Text = now.ToString("hh:mm");
            }

            // Seconds
            secondsDisplay.Text = $":{now:ss}";

            // Day
            dayDisplay.Text = now.ToString("dddd");

            // Date
            dateDisplay.Text = now.ToString("MMMM d, yyyy");

            // AM/PM indicator for 12-hour format
            if (!show24Hour)
            {
                dateDisplay.Text += $" {now:tt}";
            }
        }

        private void ToggleTimeFormat()
        {
            show24Hour = !show24Hour;
            UpdateClock();
        }

        protected override void OnDispose()
        {
            clockTimer?.Stop();
            clockTimer = null;
            base.OnDispose();
        }
    }
}
