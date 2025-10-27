using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Time tracking widget with manual timer and Pomodoro mode
    /// Supports real-time task time tracking with start/stop controls
    /// </summary>
    public class TimeTrackingWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;

        private Theme theme;
        private TaskService taskService;

        // UI Components
        private Grid mainGrid;
        private TextBlock headerLabel;
        private ComboBox modeComboBox;
        private ListBox taskListBox;
        private TextBlock timerDisplay;
        private TextBlock statusLabel;
        private Button startStopButton;
        private Button resetButton;
        private StackPanel pomodoroPanel;
        private TextBlock pomodoroStatsLabel;

        // Timer state
        private DispatcherTimer updateTimer;
        private TaskTimeSession currentSession;
        private PomodoroSession pomodoroSession;
        private bool isPomodoroMode = false;
        private List<TaskItem> tasks;

        // Pomodoro settings
        private int pomodoroWorkMinutes = 25;
        private int pomodoroShortBreakMinutes = 5;
        private int pomodoroLongBreakMinutes = 15;
        private int pomodorosUntilLongBreak = 4;

        public TimeTrackingWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            WidgetName = "Time Tracker";
            WidgetType = "TimeTracking";
        }

        public TimeTrackingWidget() : this(Logger.Instance, ThemeManager.Instance, ConfigurationManager.Instance)
        {
        }

        public override void Initialize()
        {
            theme = themeManager.CurrentTheme;
            taskService = TaskService.Instance;

            // Initialize services
            taskService.Initialize();

            // Load Pomodoro settings from config
            pomodoroWorkMinutes = config.Get<int>("Pomodoro.WorkMinutes", 25);
            pomodoroShortBreakMinutes = config.Get<int>("Pomodoro.ShortBreakMinutes", 5);
            pomodoroLongBreakMinutes = config.Get<int>("Pomodoro.LongBreakMinutes", 15);
            pomodorosUntilLongBreak = config.Get<int>("Pomodoro.PomodorosUntilLongBreak", 4);

            BuildUI();
            LoadTasks();

            // Start update timer (1 second interval)
            updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

            logger?.Info("TimeTracking", "Time Tracking widget initialized");
        }

        private void BuildUI()
        {
            mainGrid = new Grid
            {
                Background = new SolidColorBrush(theme.Background)
            };

            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Mode selector
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Task list
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Timer display
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Pomodoro stats

            // Header
            headerLabel = new TextBlock
            {
                Text = "⏱ TIME TRACKER",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(headerLabel, 0);
            mainGrid.Children.Add(headerLabel);

            // Mode selector
            var modePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 10)
            };

            var modeLabel = new TextBlock
            {
                Text = "Mode:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            modePanel.Children.Add(modeLabel);

            modeComboBox = new ComboBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Width = 150,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border)
            };
            modeComboBox.Items.Add("Manual Timer");
            modeComboBox.Items.Add("Pomodoro (25/5)");
            modeComboBox.SelectedIndex = 0;
            modeComboBox.SelectionChanged += ModeComboBox_SelectionChanged;
            modePanel.Children.Add(modeComboBox);

            Grid.SetRow(modePanel, 1);
            mainGrid.Children.Add(modePanel);

            // Task list
            var taskListBorder = new Border
            {
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10, 0, 10, 10)
            };

            taskListBox = new ListBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0)
            };
            taskListBox.SelectionChanged += TaskListBox_SelectionChanged;

            taskListBorder.Child = taskListBox;
            Grid.SetRow(taskListBorder, 2);
            mainGrid.Children.Add(taskListBorder);

            // Timer display
            var timerPanel = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10, 0, 10, 10),
                Padding = new Thickness(10)
            };

            var timerStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            timerDisplay = new TextBlock
            {
                Text = "00:00:00",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Success),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            timerStack.Children.Add(timerDisplay);

            statusLabel = new TextBlock
            {
                Text = "Select a task to start",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            timerStack.Children.Add(statusLabel);

            timerPanel.Child = timerStack;
            Grid.SetRow(timerPanel, 3);
            mainGrid.Children.Add(timerPanel);

            // Controls
            var controlsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 10)
            };

            startStopButton = new Button
            {
                Content = "Start",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Width = 100,
                Background = new SolidColorBrush(theme.Success),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand
            };
            startStopButton.Click += StartStopButton_Click;
            controlsPanel.Children.Add(startStopButton);

            resetButton = new Button
            {
                Content = "Reset",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Width = 100,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 5, 10, 5),
                Cursor = Cursors.Hand
            };
            resetButton.Click += ResetButton_Click;
            controlsPanel.Children.Add(resetButton);

            Grid.SetRow(controlsPanel, 4);
            mainGrid.Children.Add(controlsPanel);

            // Pomodoro stats panel
            pomodoroPanel = new StackPanel
            {
                Margin = new Thickness(10, 0, 10, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            pomodoroStatsLabel = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                TextAlignment = TextAlignment.Center
            };
            pomodoroPanel.Children.Add(pomodoroStatsLabel);

            Grid.SetRow(pomodoroPanel, 5);
            mainGrid.Children.Add(pomodoroPanel);

            Content = mainGrid;
        }

        #region Event Handlers

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            isPomodoroMode = modeComboBox.SelectedIndex == 1;
            pomodoroPanel.Visibility = isPomodoroMode ? Visibility.Visible : Visibility.Collapsed;

            // Reset current session when switching modes
            if (currentSession != null && currentSession.IsActive)
            {
                StopCurrentSession();
            }

            if (pomodoroSession != null && pomodoroSession.IsActive)
            {
                StopPomodoro();
            }

            UpdateDisplay();
            logger?.Debug("TimeTracking", $"Switched to {(isPomodoroMode ? "Pomodoro" : "Manual")} mode");
        }

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDisplay();
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPomodoroMode)
            {
                if (pomodoroSession == null || !pomodoroSession.IsActive)
                {
                    StartPomodoro();
                }
                else
                {
                    StopPomodoro();
                }
            }
            else
            {
                if (currentSession == null || !currentSession.IsActive)
                {
                    StartManualTimer();
                }
                else
                {
                    StopCurrentSession();
                }
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPomodoroMode)
            {
                if (pomodoroSession != null)
                {
                    StopPomodoro();
                    pomodoroSession = null;
                }
            }
            else
            {
                if (currentSession != null)
                {
                    StopCurrentSession();
                    currentSession = null;
                }
            }

            UpdateDisplay();
            logger?.Debug("TimeTracking", "Timer reset");
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateDisplay();

            // Check Pomodoro phase completion
            if (isPomodoroMode && pomodoroSession != null && pomodoroSession.IsActive)
            {
                if (pomodoroSession.IsPhaseComplete)
                {
                    HandlePomodoroPhaseComplete();
                }
            }
        }

        #endregion

        #region Manual Timer

        private void StartManualTimer()
        {
            var selectedTask = taskListBox.SelectedItem as TaskItem;
            if (selectedTask == null)
            {
                MessageBox.Show("Please select a task first", "No Task Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            currentSession = new TaskTimeSession
            {
                TaskId = selectedTask.Id,
                StartTime = DateTime.Now
            };

            logger?.Info("TimeTracking", $"Started timer for: {selectedTask.Title}");
            UpdateDisplay();
        }

        private void StopCurrentSession()
        {
            if (currentSession == null || !currentSession.IsActive)
                return;

            currentSession.Stop();

            var selectedTask = taskListBox.SelectedItem as TaskItem;
            if (selectedTask != null)
            {
                var durationMinutes = (int)currentSession.Duration.TotalMinutes;
                logger?.Info("TimeTracking", $"Stopped timer for: {selectedTask.Title} ({durationMinutes} minutes)");

                // Show summary
                MessageBox.Show(
                    $"Time logged: {currentSession.Duration.Hours:D2}:{currentSession.Duration.Minutes:D2}:{currentSession.Duration.Seconds:D2}\n\n" +
                    $"Task: {selectedTask.Title}",
                    "Time Logged",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            currentSession = null;
            UpdateDisplay();
        }

        #endregion

        #region Pomodoro

        private void StartPomodoro()
        {
            var selectedTask = taskListBox.SelectedItem as TaskItem;
            if (selectedTask == null)
            {
                MessageBox.Show("Please select a task first", "No Task Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            pomodoroSession = new PomodoroSession
            {
                TaskId = selectedTask.Id,
                Phase = PomodoroPhase.Work,
                StartTime = DateTime.Now,
                WorkMinutes = pomodoroWorkMinutes,
                ShortBreakMinutes = pomodoroShortBreakMinutes,
                LongBreakMinutes = pomodoroLongBreakMinutes,
                IsActive = true
            };

            logger?.Info("TimeTracking", $"Started Pomodoro for: {selectedTask.Title}");
            UpdateDisplay();
        }

        private void StopPomodoro()
        {
            if (pomodoroSession == null)
                return;

            pomodoroSession.IsActive = false;

            var selectedTask = taskListBox.SelectedItem as TaskItem;
            if (selectedTask != null)
            {
                logger?.Info("TimeTracking", $"Stopped Pomodoro for: {selectedTask.Title} ({pomodoroSession.CompletedPomodoros} completed)");
            }

            UpdateDisplay();
        }

        private void HandlePomodoroPhaseComplete()
        {
            if (pomodoroSession == null)
                return;

            switch (pomodoroSession.Phase)
            {
                case PomodoroPhase.Work:
                    pomodoroSession.CompletedPomodoros++;

                    // Determine next break type
                    if (pomodoroSession.CompletedPomodoros % pomodorosUntilLongBreak == 0)
                    {
                        pomodoroSession.Phase = PomodoroPhase.LongBreak;
                        MessageBox.Show($"Work session complete! Time for a long break ({pomodoroLongBreakMinutes} minutes).\n\n" +
                            $"Completed Pomodoros: {pomodoroSession.CompletedPomodoros}",
                            "Long Break Time!",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        pomodoroSession.Phase = PomodoroPhase.ShortBreak;
                        MessageBox.Show($"Work session complete! Time for a short break ({pomodoroShortBreakMinutes} minutes).\n\n" +
                            $"Completed Pomodoros: {pomodoroSession.CompletedPomodoros}",
                            "Break Time!",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    break;

                case PomodoroPhase.ShortBreak:
                case PomodoroPhase.LongBreak:
                    pomodoroSession.Phase = PomodoroPhase.Work;
                    MessageBox.Show($"Break complete! Ready for another work session ({pomodoroWorkMinutes} minutes)?",
                        "Back to Work!",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;
            }

            pomodoroSession.StartTime = DateTime.Now;
            UpdateDisplay();
        }

        #endregion

        #region Display Updates

        private void UpdateDisplay()
        {
            var selectedTask = taskListBox.SelectedItem as TaskItem;

            if (isPomodoroMode)
            {
                UpdatePomodoroDisplay(selectedTask);
            }
            else
            {
                UpdateManualTimerDisplay(selectedTask);
            }
        }

        private void UpdateManualTimerDisplay(TaskItem selectedTask)
        {
            if (currentSession != null && currentSession.IsActive)
            {
                var duration = currentSession.Duration;
                timerDisplay.Text = $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
                timerDisplay.Foreground = new SolidColorBrush(theme.Success);
                statusLabel.Text = selectedTask != null ? $"Tracking: {selectedTask.Title}" : "Tracking...";
                startStopButton.Content = "Stop";
                startStopButton.Background = new SolidColorBrush(theme.Error);
            }
            else
            {
                timerDisplay.Text = "00:00:00";
                timerDisplay.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
                statusLabel.Text = selectedTask != null ? "Ready to start" : "Select a task to start";
                startStopButton.Content = "Start";
                startStopButton.Background = new SolidColorBrush(theme.Success);
            }

            startStopButton.IsEnabled = selectedTask != null;
            resetButton.IsEnabled = currentSession != null;
        }

        private void UpdatePomodoroDisplay(TaskItem selectedTask)
        {
            if (pomodoroSession != null && pomodoroSession.IsActive)
            {
                var remaining = pomodoroSession.TimeRemaining;
                timerDisplay.Text = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";

                // Color based on phase
                timerDisplay.Foreground = pomodoroSession.Phase == PomodoroPhase.Work
                    ? new SolidColorBrush(theme.Success)
                    : new SolidColorBrush(theme.Warning);

                var phaseText = pomodoroSession.Phase switch
                {
                    PomodoroPhase.Work => "WORK",
                    PomodoroPhase.ShortBreak => "SHORT BREAK",
                    PomodoroPhase.LongBreak => "LONG BREAK",
                    _ => "IDLE"
                };

                statusLabel.Text = selectedTask != null ? $"{phaseText}: {selectedTask.Title}" : phaseText;
                pomodoroStatsLabel.Text = $"Completed: {pomodoroSession.CompletedPomodoros} | " +
                    $"Next break: {(pomodoroSession.CompletedPomodoros % pomodorosUntilLongBreak == pomodorosUntilLongBreak - 1 ? "Long" : "Short")}";

                startStopButton.Content = "Stop";
                startStopButton.Background = new SolidColorBrush(theme.Error);
            }
            else
            {
                timerDisplay.Text = $"{pomodoroWorkMinutes:D2}:00";
                timerDisplay.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
                statusLabel.Text = selectedTask != null ? "Ready to start" : "Select a task to start";

                if (pomodoroSession != null)
                {
                    pomodoroStatsLabel.Text = $"Completed: {pomodoroSession.CompletedPomodoros}";
                }
                else
                {
                    pomodoroStatsLabel.Text = $"Work: {pomodoroWorkMinutes}m | Break: {pomodoroShortBreakMinutes}m | Long: {pomodoroLongBreakMinutes}m";
                }

                startStopButton.Content = "Start";
                startStopButton.Background = new SolidColorBrush(theme.Success);
            }

            startStopButton.IsEnabled = selectedTask != null;
            resetButton.IsEnabled = pomodoroSession != null;
        }

        private void LoadTasks()
        {
            tasks = taskService.GetTasks(t => !t.Deleted &&
                (t.Status == TaskStatus.Pending || t.Status == TaskStatus.InProgress));

            taskListBox.Items.Clear();
            foreach (var task in tasks.OrderByDescending(t => t.Priority).ThenBy(t => t.DueDate))
            {
                taskListBox.Items.Add(task);
            }

            logger?.Debug("TimeTracking", $"Loaded {tasks.Count} active tasks");
        }

        #endregion

        #region Widget Lifecycle

        public void ApplyTheme()
        {
            theme = themeManager.CurrentTheme;

            if (mainGrid != null)
            {
                mainGrid.Background = new SolidColorBrush(theme.Background);
            }

            UpdateDisplay();
            logger?.Debug("TimeTracking", "Applied theme update");
        }

        protected override void OnDispose()
        {
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Tick -= UpdateTimer_Tick;
            }

            if (currentSession != null && currentSession.IsActive)
            {
                StopCurrentSession();
            }

            if (pomodoroSession != null && pomodoroSession.IsActive)
            {
                StopPomodoro();
            }

            logger?.Info("TimeTracking", "Time Tracking widget disposed");
        }

        #endregion
    }
}
