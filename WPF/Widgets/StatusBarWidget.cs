using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Persistent status bar showing: [Project: Name] | [3 Tasks] | [⏱️ 5h 30m] | [14:35]
    /// </summary>
    public class StatusBarWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IProjectContextManager projectContext;
        private readonly ITimeTrackingService timeTracking;
        private readonly ITaskService taskService;
        private readonly IConfigurationManager config;

        private TextBlock projectLabel;
        private TextBlock taskCountLabel;
        private TextBlock timeLabel;
        private TextBlock clockLabel;
        private Border container;
        private DispatcherTimer timeUpdateTimer;
        private DispatcherTimer clockTimer;

        public StatusBarWidget(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            ITimeTrackingService timeTracking,
            ITaskService taskService,
            IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
            this.timeTracking = timeTracking ?? throw new ArgumentNullException(nameof(timeTracking));
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            WidgetName = "StatusBar";
            RegisterConfigurationDefaults();
            BuildUI();
        }

        public StatusBarWidget()
            : this(
                Logger.Instance,
                ThemeManager.Instance,
                ProjectContextManager.Instance,
                TimeTrackingService.Instance,
                Core.Services.TaskService.Instance,
                ConfigurationManager.Instance)
        {
        }

        private void RegisterConfigurationDefaults()
        {
            config.Register("StatusBar.ShowProject", true, "Show project name in status bar", "StatusBar");
            config.Register("StatusBar.ShowTasks", true, "Show task count in status bar", "StatusBar");
            config.Register("StatusBar.ShowTime", true, "Show time tracking in status bar", "StatusBar");
            config.Register("StatusBar.ShowClock", true, "Show current time in status bar", "StatusBar");
        }

        private void BuildUI()
        {
            // Get configuration settings
            bool showProject = config.Get("StatusBar.ShowProject", true);
            bool showTasks = config.Get("StatusBar.ShowTasks", true);
            bool showTime = config.Get("StatusBar.ShowTime", true);
            bool showClock = config.Get("StatusBar.ShowClock", true);

            // Create horizontal stack panel for labels
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Height = 30,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Project label
            if (showProject)
            {
                projectLabel = new TextBlock
                {
                    Text = "[Project: None]",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12
                };
                stackPanel.Children.Add(projectLabel);
            }

            // Task count label
            if (showTasks)
            {
                taskCountLabel = new TextBlock
                {
                    Text = "[0 Tasks]",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12
                };
                stackPanel.Children.Add(taskCountLabel);
            }

            // Time tracking label
            if (showTime)
            {
                timeLabel = new TextBlock
                {
                    Text = "[⏱️ 0h 0m]",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12
                };
                stackPanel.Children.Add(timeLabel);
            }

            // Clock label
            if (showClock)
            {
                clockLabel = new TextBlock
                {
                    Text = "[00:00]",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 10, 0),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12
                };
                stackPanel.Children.Add(clockLabel);
            }

            // Add separator between labels if needed (visual improvement)
            AddSeparators(stackPanel);

            container = new Border
            {
                Child = stackPanel,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0)
            };

            Content = container;
            ApplyTheme();
        }

        private void AddSeparators(StackPanel panel)
        {
            // Insert separators between existing labels
            var children = panel.Children.Cast<UIElement>().ToList();
            panel.Children.Clear();

            for (int i = 0; i < children.Count; i++)
            {
                panel.Children.Add(children[i]);

                // Add separator after each element except the last
                if (i < children.Count - 1)
                {
                    var separator = new TextBlock
                    {
                        Text = " | ",
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        Opacity = 0.5
                    };
                    panel.Children.Add(separator);
                }
            }
        }

        public override void Initialize()
        {
            // Subscribe to context changes
            projectContext.ProjectContextChanged += OnProjectContextChanged;

            // Subscribe to task events
            taskService.TaskAdded += OnTaskChanged;
            taskService.TaskUpdated += OnTaskChanged;
            taskService.TaskDeleted += OnTaskDeleted;
            taskService.TasksReloaded += OnTasksReloaded;

            // Update initial state
            UpdateProjectLabel();
            UpdateTaskCountLabel();
            UpdateTimeLabel();
            UpdateClockLabel();

            // Start timer for time tracking updates (every 10 seconds)
            if (timeLabel != null)
            {
                timeUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                timeUpdateTimer.Tick += (s, e) => UpdateTimeLabel();
                timeUpdateTimer.Start();
            }

            // Start timer for clock updates (every minute)
            if (clockLabel != null)
            {
                clockTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(1)
                };
                clockTimer.Tick += (s, e) => UpdateClockLabel();
                clockTimer.Start();
            }

            logger.Log(LogLevel.Info, WidgetName, "StatusBar initialized");
        }

        private void OnProjectContextChanged(object sender, ProjectContextChangedEventArgs e)
        {
            UpdateProjectLabel();
            UpdateTaskCountLabel();
            UpdateTimeLabel();
        }

        private void OnTaskChanged(TaskItem task)
        {
            UpdateTaskCountLabel();
        }

        private void OnTaskDeleted(Guid taskId)
        {
            UpdateTaskCountLabel();
        }

        private void OnTasksReloaded()
        {
            UpdateTaskCountLabel();
        }

        private void UpdateProjectLabel()
        {
            if (projectLabel == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (projectContext.CurrentProject != null)
                {
                    projectLabel.Text = $"[Project: {projectContext.CurrentProject.Name}]";
                }
                else
                {
                    projectLabel.Text = "[Project: All]";
                }
            });
        }

        private void UpdateTaskCountLabel()
        {
            if (taskCountLabel == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Get tasks for current project
                    var tasks = projectContext.CurrentProject != null
                        ? taskService.GetTasksForProject(projectContext.CurrentProject.Id)
                        : taskService.GetAllTasks();

                    // Filter out completed/deleted tasks
                    var activeTasks = tasks.Where(t => t.Status != TaskStatus.Completed && !t.Deleted).ToList();
                    var count = activeTasks.Count;

                    taskCountLabel.Text = count == 1 ? "[1 Task]" : $"[{count} Tasks]";
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Error, WidgetName, $"Error updating task count: {ex.Message}");
                    taskCountLabel.Text = "[? Tasks]";
                }
            });
        }

        private void UpdateClockLabel()
        {
            if (clockLabel == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                clockLabel.Text = $"[{DateTime.Now:HH:mm}]";
            });
        }

        private void UpdateTimeLabel()
        {
            if (timeLabel == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Get this week's entries for current project
                    var allEntries = timeTracking.GetAllEntries();
                    var thisWeek = allEntries.FindAll(e =>
                        e.WeekEnding >= DateTime.Today.AddDays(-7) &&
                        (projectContext.CurrentProject == null || e.ProjectId.ToString() == projectContext.CurrentProject.Id.ToString()));

                    var totalMinutes = thisWeek.Sum(e => e.Hours * 60);
                    var hours = (int)(totalMinutes / 60);
                    var minutes = (int)(totalMinutes % 60);

                    timeLabel.Text = $"[⏱️ {hours}h {minutes}m]";
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Error, WidgetName, $"Error updating time label: {ex.Message}");
                    timeLabel.Text = "[⏱️ --]";
                }
            });
        }

        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;
            if (theme == null) return;

            var bg = theme.GetColor("background");
            var fg = theme.GetColor("foreground");
            var border = theme.GetColor("border");

            container.Background = new SolidColorBrush(bg);
            container.BorderBrush = new SolidColorBrush(border);

            // Apply foreground to all labels (including separators)
            if (container.Child is StackPanel stackPanel)
            {
                foreach (var child in stackPanel.Children)
                {
                    if (child is TextBlock textBlock)
                    {
                        textBlock.Foreground = new SolidColorBrush(fg);
                    }
                }
            }
        }

        protected override void OnDispose()
        {
            // Unsubscribe from project context events
            projectContext.ProjectContextChanged -= OnProjectContextChanged;

            // Unsubscribe from task events
            taskService.TaskAdded -= OnTaskChanged;
            taskService.TaskUpdated -= OnTaskChanged;
            taskService.TaskDeleted -= OnTaskDeleted;
            taskService.TasksReloaded -= OnTasksReloaded;

            // Stop and dispose timers
            if (timeUpdateTimer != null)
            {
                timeUpdateTimer.Stop();
                timeUpdateTimer = null;
            }

            if (clockTimer != null)
            {
                clockTimer.Stop();
                clockTimer = null;
            }

            logger.Log(LogLevel.Info, WidgetName, "StatusBar disposed");
            base.OnDispose();
        }
    }
}
