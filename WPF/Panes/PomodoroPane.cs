// PomodoroPane.cs - Pomodoro timer for focused work sessions
// 25min work → 5min break → repeat 4x → 15min long break

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;
using SuperTUI.Core;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Pomodoro timer pane - 25/5 work/break intervals with keyboard controls
    /// </summary>
    public class PomodoroPane : PaneBase
    {
        // Timer state
        private DispatcherTimer timer;
        private DateTime sessionStartTime;
        private TimeSpan workDuration = TimeSpan.FromMinutes(25);
        private TimeSpan shortBreakDuration = TimeSpan.FromMinutes(5);
        private TimeSpan longBreakDuration = TimeSpan.FromMinutes(15);
        private int pomodorosCompleted = 0;
        private bool isRunning = false;
        private bool isBreak = false;

        // UI components
        private TextBlock timerDisplay;
        private TextBlock sessionLabel;
        private TextBlock statsDisplay;
        private TextBlock helpText;
        private TextBlock statusBar;
        private Grid mainGrid;

        // Theme colors
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush successBrush;
        private SolidColorBrush errorBrush;
        private SolidColorBrush surfaceBrush;
        private SolidColorBrush dimBrush;

        public PomodoroPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext)
            : base(logger, themeManager, projectContext)
        {
            PaneName = "Pomodoro";
            PaneIcon = "🍅";
        }

        public override void Initialize()
        {
            base.Initialize();
            CacheThemeColors();
            RegisterPaneShortcuts();

            // Initialize timer
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += Timer_Tick;

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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Timer display
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Session label
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Stats
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Help
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status
            mainGrid.KeyDown += MainGrid_KeyDown;

            // Minimal header
            var header = new TextBlock
            {
                Text = "🍅",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 20,
                Foreground = accentBrush,
                Padding = new Thickness(16, 8, 16, 4),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Timer display (large)
            timerDisplay = new TextBlock
            {
                Text = "25:00",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 96,
                FontWeight = FontWeights.Bold,
                Foreground = fgBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(timerDisplay, 1);
            mainGrid.Children.Add(timerDisplay);

            // Session label
            sessionLabel = new TextBlock
            {
                Text = "Ready to work",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 20,
                Foreground = dimBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(sessionLabel, 2);
            mainGrid.Children.Add(sessionLabel);

            // Compact stats
            statsDisplay = new TextBlock
            {
                Text = "0 🍅",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                Foreground = dimBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(statsDisplay, 3);
            mainGrid.Children.Add(statsDisplay);

            // Compact help
            helpText = new TextBlock
            {
                Text = "Space:Start/Pause  R:Reset",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                Foreground = dimBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(16, 0, 16, 8)
            };
            Grid.SetRow(helpText, 4);
            mainGrid.Children.Add(helpText);

            // Status bar
            statusBar = new TextBlock
            {
                Text = "Press Space to start",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = fgBrush,
                Background = surfaceBrush,
                Padding = new Thickness(16, 8, 16, 8)
            };
            Grid.SetRow(statusBar, 5);
            mainGrid.Children.Add(statusBar);

            return mainGrid;
        }

        private void CacheThemeColors()
        {
            var theme = themeManager.CurrentTheme;
            bgBrush = new SolidColorBrush(theme.Background);
            fgBrush = new SolidColorBrush(theme.Foreground);
            accentBrush = new SolidColorBrush(theme.Primary);
            successBrush = new SolidColorBrush(theme.Success);
            errorBrush = new SolidColorBrush(theme.Error);
            surfaceBrush = new SolidColorBrush(theme.Surface);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
        }

        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;
            shortcuts.RegisterForPane(PaneName, Key.Space, ModifierKeys.None, () => ToggleTimer(), "Start/Pause timer");
            shortcuts.RegisterForPane(PaneName, Key.R, ModifierKeys.None, () => ResetTimer(), "Reset timer");
            shortcuts.RegisterForPane(PaneName, Key.B, ModifierKeys.None, () => SkipBreak(), "Skip break");
            shortcuts.RegisterForPane(PaneName, Key.OemPlus, ModifierKeys.None, () => ExtendTime(), "Extend time +5min");
            shortcuts.RegisterForPane(PaneName, Key.OemMinus, ModifierKeys.None, () => ReduceTime(), "Reduce time -5min");
        }

        private void MainGrid_KeyDown(object sender, KeyEventArgs e)
        {
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
                e.Handled = true;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var elapsed = DateTime.Now - sessionStartTime;
            var duration = isBreak ? (pomodorosCompleted % 4 == 0 ? longBreakDuration : shortBreakDuration) : workDuration;
            var remaining = duration - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                CompleteSession();
            }
            else
            {
                UpdateDisplay(remaining);
            }
        }

        private void CompleteSession()
        {
            timer.Stop();
            isRunning = false;

            if (!isBreak)
            {
                // Completed work session
                pomodorosCompleted++;
                bool isLongBreak = pomodorosCompleted % 4 == 0;
                isBreak = true;

                ShowStatus($"Work session complete! Starting {(isLongBreak ? "long" : "short")} break", false);
                sessionLabel.Text = isLongBreak ? "LONG BREAK - Relax!" : "Short break - Stretch!";
                sessionLabel.Foreground = successBrush;

                // Auto-start break
                sessionStartTime = DateTime.Now;
                timer.Start();
                isRunning = true;
            }
            else
            {
                // Completed break
                isBreak = false;
                sessionLabel.Text = "Break over - Ready to work";
                sessionLabel.Foreground = accentBrush;
                ShowStatus("Break complete! Press Space to start next pomodoro", false);
                UpdateDisplay(workDuration);
            }

            UpdateStats();
        }

        private void ToggleTimer()
        {
            if (isRunning)
            {
                // Pause
                timer.Stop();
                isRunning = false;
                ShowStatus("Paused", false);
            }
            else
            {
                // Start/Resume
                if (sessionStartTime == DateTime.MinValue)
                {
                    sessionStartTime = DateTime.Now;
                }
                else
                {
                    // Resume: adjust start time to account for pause
                    var duration = isBreak ? (pomodorosCompleted % 4 == 0 ? longBreakDuration : shortBreakDuration) : workDuration;
                    var currentDisplay = timerDisplay.Text;
                    var parts = currentDisplay.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int mins) && int.TryParse(parts[1], out int secs))
                    {
                        var remaining = TimeSpan.FromMinutes(mins) + TimeSpan.FromSeconds(secs);
                        sessionStartTime = DateTime.Now - (duration - remaining);
                    }
                }

                timer.Start();
                isRunning = true;
                sessionLabel.Text = isBreak ? (pomodorosCompleted % 4 == 0 ? "LONG BREAK" : "Short break") : "Working - Stay focused!";
                sessionLabel.Foreground = isBreak ? successBrush : accentBrush;
                ShowStatus("Timer running", false);
            }
        }

        private void ResetTimer()
        {
            timer.Stop();
            isRunning = false;
            isBreak = false;
            sessionStartTime = DateTime.MinValue;
            sessionLabel.Text = "Ready to work";
            sessionLabel.Foreground = dimBrush;
            UpdateDisplay(workDuration);
            ShowStatus("Timer reset", false);
        }

        private void SkipBreak()
        {
            if (isBreak && isRunning)
            {
                CompleteSession();
                ShowStatus("Break skipped", false);
            }
        }

        private void ExtendTime()
        {
            if (!isRunning)
            {
                workDuration = workDuration.Add(TimeSpan.FromMinutes(5));
                UpdateDisplay(workDuration);
                ShowStatus($"Work duration: {workDuration.TotalMinutes}min", false);
            }
        }

        private void ReduceTime()
        {
            if (!isRunning && workDuration > TimeSpan.FromMinutes(5))
            {
                workDuration = workDuration.Subtract(TimeSpan.FromMinutes(5));
                UpdateDisplay(workDuration);
                ShowStatus($"Work duration: {workDuration.TotalMinutes}min", false);
            }
        }

        private void UpdateDisplay(TimeSpan remaining)
        {
            int totalSeconds = (int)remaining.TotalSeconds;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerDisplay.Text = $"{minutes:D2}:{seconds:D2}";

            // Color based on time remaining
            if (remaining.TotalMinutes < 1)
                timerDisplay.Foreground = errorBrush;
            else if (remaining.TotalMinutes < 5)
                timerDisplay.Foreground = new SolidColorBrush(themeManager.CurrentTheme.Warning);
            else
                timerDisplay.Foreground = fgBrush;
        }

        private void UpdateStats()
        {
            statsDisplay.Text = $"{pomodorosCompleted} 🍅";
        }

        private void ShowStatus(string message, bool isError)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                statusBar.Text = message;
                statusBar.Foreground = isError ? errorBrush : fgBrush;
            });
        }

        protected override void OnDispose()
        {
            timer?.Stop();
            timer = null;
            base.OnDispose();
        }
    }
}
