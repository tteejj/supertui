using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Project statistics dashboard widget showing key metrics and charts
    /// Displays: Active Projects, Total Tasks, Total Time, Completion %, Overdue, Due Soon
    /// Also shows: Top Projects by Time, Recent Activity feed
    /// </summary>
    public class ProjectStatsWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;

        private Theme theme;
        private TaskService taskService;
        private ProjectService projectService;
        private TimeTrackingService timeService;

        // UI Components
        private Grid mainGrid;
        private DispatcherTimer refreshTimer;

        // Metric cards
        private TextBlock activeProjectsValue;
        private TextBlock totalTasksValue;
        private TextBlock totalTimeValue;
        private TextBlock completionPercentValue;
        private ProgressBar completionProgressBar;
        private TextBlock overdueTasksValue;
        private TextBlock dueSoonProjectsValue;

        // Charts
        private ListBox topProjectsListBox;
        private ListBox recentActivityListBox;

        public ProjectStatsWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            WidgetName = "Project Stats";
            WidgetType = "ProjectStats";
        }

        public ProjectStatsWidget()
            : this(Logger.Instance, ThemeManager.Instance, ConfigurationManager.Instance)
        {
        }

        public override void Initialize()
        {
            theme = themeManager.CurrentTheme;
            taskService = TaskService.Instance;
            projectService = ProjectService.Instance;
            timeService = TimeTrackingService.Instance;

            // Initialize services
            taskService.Initialize();
            projectService.Initialize();
            timeService.Initialize();

            BuildUI();
            RefreshMetrics();

            // Subscribe to service events
            taskService.TaskAdded += (t) => RefreshMetrics();
            taskService.TaskUpdated += (t) => RefreshMetrics();
            taskService.TaskDeleted += (id) => RefreshMetrics();
            projectService.ProjectAdded += (p) => RefreshMetrics();
            projectService.ProjectUpdated += (p) => RefreshMetrics();
            timeService.EntryAdded += (e) => RefreshMetrics();
            timeService.EntryUpdated += (e) => RefreshMetrics();

            // Setup refresh timer (every 30 seconds)
            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            refreshTimer.Tick += (s, e) => RefreshMetrics();
            refreshTimer.Start();

            logger.Info("ProjectStatsWidget", "Project stats widget initialized");
        }

        private void BuildUI()
        {
            // Main grid with 3 columns x 3 rows
            mainGrid = new Grid
            {
                Background = new SolidColorBrush(theme.Background),
                Margin = new Thickness(10)
            };

            // Define columns (equal width)
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Define rows
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(120) }); // Metrics row 1
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(120) }); // Metrics row 2
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Charts

            // Build metric cards (2 rows x 3 columns)
            var activeProjectsCard = BuildMetricCard("ACTIVE PROJECTS", out activeProjectsValue, theme.Info);
            Grid.SetColumn(activeProjectsCard, 0);
            Grid.SetRow(activeProjectsCard, 0);
            mainGrid.Children.Add(activeProjectsCard);

            var totalTasksCard = BuildMetricCard("TOTAL TASKS", out totalTasksValue, theme.Foreground);
            Grid.SetColumn(totalTasksCard, 1);
            Grid.SetRow(totalTasksCard, 0);
            mainGrid.Children.Add(totalTasksCard);

            var totalTimeCard = BuildMetricCard("TOTAL TIME (hrs)", out totalTimeValue, theme.ForegroundSecondary);
            Grid.SetColumn(totalTimeCard, 2);
            Grid.SetRow(totalTimeCard, 0);
            mainGrid.Children.Add(totalTimeCard);

            var completionCard = BuildCompletionCard(out completionPercentValue, out completionProgressBar);
            Grid.SetColumn(completionCard, 0);
            Grid.SetRow(completionCard, 1);
            mainGrid.Children.Add(completionCard);

            var overdueCard = BuildMetricCard("OVERDUE TASKS", out overdueTasksValue, theme.Error);
            Grid.SetColumn(overdueCard, 1);
            Grid.SetRow(overdueCard, 1);
            mainGrid.Children.Add(overdueCard);

            var dueSoonCard = BuildMetricCard("DUE SOON PROJECTS", out dueSoonProjectsValue, theme.Warning);
            Grid.SetColumn(dueSoonCard, 2);
            Grid.SetRow(dueSoonCard, 1);
            mainGrid.Children.Add(dueSoonCard);

            // Build charts (row 3, spanning all columns)
            var chartsPanel = BuildChartsPanel();
            Grid.SetColumn(chartsPanel, 0);
            Grid.SetRow(chartsPanel, 2);
            Grid.SetColumnSpan(chartsPanel, 3);
            mainGrid.Children.Add(chartsPanel);

            this.Content = mainGrid;
        }

        private Border BuildMetricCard(string title, out TextBlock valueLabel, System.Windows.Media.Color accentColor)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(accentColor),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(5),
                Padding = new Thickness(15),
                CornerRadius = new CornerRadius(5)
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            // Title
            var titleBlock = new TextBlock
            {
                Text = title,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10),
                TextAlignment = TextAlignment.Center
            };
            stack.Children.Add(titleBlock);

            // Value (large number)
            valueLabel = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accentColor),
                TextAlignment = TextAlignment.Center
            };
            stack.Children.Add(valueLabel);

            border.Child = stack;
            return border;
        }

        private Border BuildCompletionCard(out TextBlock percentLabel, out ProgressBar progressBar)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Success),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(5),
                Padding = new Thickness(15),
                CornerRadius = new CornerRadius(5)
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            // Title
            var titleBlock = new TextBlock
            {
                Text = "COMPLETION",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10),
                TextAlignment = TextAlignment.Center
            };
            stack.Children.Add(titleBlock);

            // Percentage value
            percentLabel = new TextBlock
            {
                Text = "0%",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Success),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stack.Children.Add(percentLabel);

            // Progress bar
            progressBar = new ProgressBar
            {
                Height = 10,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = new SolidColorBrush(theme.Success),
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderThickness = new Thickness(0)
            };
            stack.Children.Add(progressBar);

            border.Child = stack;
            return border;
        }

        private Grid BuildChartsPanel()
        {
            var grid = new Grid
            {
                Margin = new Thickness(5, 10, 5, 5)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Top Projects by Time
            var topProjectsPanel = BuildTopProjectsPanel();
            Grid.SetColumn(topProjectsPanel, 0);
            grid.Children.Add(topProjectsPanel);

            // Recent Activity
            var recentActivityPanel = BuildRecentActivityPanel();
            Grid.SetColumn(recentActivityPanel, 1);
            grid.Children.Add(recentActivityPanel);

            return grid;
        }

        private Border BuildTopProjectsPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 5, 0),
                Padding = new Thickness(10)
            };

            var stack = new StackPanel();

            // Header
            var header = new TextBlock
            {
                Text = "TOP PROJECTS BY TIME",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(header);

            // ListBox
            topProjectsListBox = new ListBox
            {
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11
            };
            stack.Children.Add(topProjectsListBox);

            border.Child = stack;
            return border;
        }

        private Border BuildRecentActivityPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(5, 0, 0, 0),
                Padding = new Thickness(10)
            };

            var stack = new StackPanel();

            // Header
            var header = new TextBlock
            {
                Text = "RECENT ACTIVITY",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(header);

            // ListBox
            recentActivityListBox = new ListBox
            {
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10
            };
            stack.Children.Add(recentActivityListBox);

            border.Child = stack;
            return border;
        }

        private void RefreshMetrics()
        {
            try
            {
                // Get all data
                var projects = projectService.GetAllProjects();
                var activeProjects = projects.Where(p => p.Status == ProjectStatus.Active).ToList();
                var allTasks = taskService.GetAllTasks();
                var totalHours = timeService.GetAllProjectAggregates().Sum(a => (double)a.TotalHours);

                // Active Projects
                activeProjectsValue.Text = activeProjects.Count.ToString();

                // Total Tasks
                totalTasksValue.Text = allTasks.Count.ToString();

                // Total Time
                totalTimeValue.Text = totalHours.ToString("F1");

                // Completion Percentage
                var completedTasks = allTasks.Count(t => t.Status == TaskStatus.Completed);
                var completionPercent = allTasks.Count > 0 ? (completedTasks * 100.0 / allTasks.Count) : 0;
                completionPercentValue.Text = $"{completionPercent:F0}%";
                completionProgressBar.Value = completionPercent;

                // Update color based on completion
                var completionColor = completionPercent >= 75 ? theme.Success :
                                     completionPercent >= 50 ? theme.Info :
                                     completionPercent >= 25 ? theme.Warning :
                                     theme.Error;
                completionPercentValue.Foreground = new SolidColorBrush(completionColor);
                completionProgressBar.Foreground = new SolidColorBrush(completionColor);

                // Overdue Tasks
                var overdueTasks = allTasks.Count(t => t.IsOverdue);
                overdueTasksValue.Text = overdueTasks.ToString();
                overdueTasksValue.Foreground = new SolidColorBrush(overdueTasks > 0 ? theme.Error : theme.ForegroundDisabled);

                // Due Soon Projects
                var dueSoonProjects = projects.Count(p => p.Status == ProjectStatus.Active && IsDueSoon(p.EndDate));
                dueSoonProjectsValue.Text = dueSoonProjects.ToString();
                dueSoonProjectsValue.Foreground = new SolidColorBrush(dueSoonProjects > 0 ? theme.Warning : theme.ForegroundDisabled);

                // Top Projects by Time
                RefreshTopProjects();

                // Recent Activity
                RefreshRecentActivity();

                logger.Debug("ProjectStatsWidget", "Refreshed metrics");
            }
            catch (Exception ex)
            {
                logger.Error("ProjectStatsWidget", $"Failed to refresh metrics: {ex.Message}", ex);
            }
        }

        private bool IsDueSoon(DateTime? endDate)
        {
            if (!endDate.HasValue)
                return false;

            var today = DateTime.Now.Date;
            var daysUntilDue = (endDate.Value.Date - today).Days;
            return daysUntilDue >= 0 && daysUntilDue <= 14; // Within 2 weeks
        }

        private void RefreshTopProjects()
        {
            topProjectsListBox.Items.Clear();

            var projectAggregates = timeService.GetAllProjectAggregates()
                .OrderByDescending(a => a.TotalHours)
                .Take(10)
                .ToList();

            var hoursByProject = projectAggregates.ToDictionary(a => a.ProjectId, a => (double)a.TotalHours);
            var projects = projectService.GetAllProjects();

            var topProjects = hoursByProject
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .ToList();

            foreach (var kvp in topProjects)
            {
                var project = projects.FirstOrDefault(p => p.Id == kvp.Key);
                if (project != null)
                {
                    var projectName = !string.IsNullOrWhiteSpace(project.Nickname) ? project.Nickname : project.Name;
                    var hours = kvp.Value;

                    var stack = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    // Project name
                    var nameBlock = new TextBlock
                    {
                        Text = projectName.Length > 20 ? projectName.Substring(0, 17) + "..." : projectName,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Width = 150,
                        Foreground = new SolidColorBrush(theme.Foreground)
                    };
                    stack.Children.Add(nameBlock);

                    // Bar visualization
                    var maxHours = topProjects.Max(p => p.Value);
                    var barWidth = maxHours > 0 ? (hours / maxHours) * 100 : 0;

                    var bar = new Rectangle
                    {
                        Width = barWidth,
                        Height = 12,
                        Fill = new SolidColorBrush(theme.Info),
                        Margin = new Thickness(5, 0, 5, 0)
                    };
                    stack.Children.Add(bar);

                    // Hours value
                    var hoursBlock = new TextBlock
                    {
                        Text = $"{hours:F1}h",
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(theme.ForegroundSecondary)
                    };
                    stack.Children.Add(hoursBlock);

                    topProjectsListBox.Items.Add(stack);
                }
            }

            if (hoursByProject.Count == 0)
            {
                var emptyMessage = new TextBlock
                {
                    Text = "No time entries logged",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                    FontStyle = FontStyles.Italic
                };
                topProjectsListBox.Items.Add(emptyMessage);
            }
        }

        private void RefreshRecentActivity()
        {
            recentActivityListBox.Items.Clear();

            // Get recent tasks (created or updated in last 7 days)
            var recentTasks = taskService.GetAllTasks()
                .Where(t => (DateTime.Now - t.UpdatedAt).TotalDays <= 7)
                .OrderByDescending(t => t.UpdatedAt)
                .Take(15)
                .ToList();

            foreach (var task in recentTasks)
            {
                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 1, 0, 1)
                };

                // Timestamp
                var timeAgo = GetTimeAgo(task.UpdatedAt);
                var timeBlock = new TextBlock
                {
                    Text = timeAgo.PadRight(12),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                    Margin = new Thickness(0, 0, 5, 0)
                };
                stack.Children.Add(timeBlock);

                // Status icon
                var statusBlock = new TextBlock
                {
                    Text = task.StatusIcon,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                    Width = 20
                };
                stack.Children.Add(statusBlock);

                // Task title (truncated)
                var titleBlock = new TextBlock
                {
                    Text = task.Title.Length > 35 ? task.Title.Substring(0, 32) + "..." : task.Title,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(theme.Foreground)
                };
                stack.Children.Add(titleBlock);

                recentActivityListBox.Items.Add(stack);
            }

            if (recentTasks.Count == 0)
            {
                var emptyMessage = new TextBlock
                {
                    Text = "No recent activity",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                    FontStyle = FontStyles.Italic
                };
                recentActivityListBox.Items.Add(emptyMessage);
            }
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "just now";
            else if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m ago";
            else if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h ago";
            else if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays}d ago";
            else
                return dateTime.ToString("MM/dd");
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // R to refresh
            if (e.Key == Key.R)
            {
                RefreshMetrics();
                e.Handled = true;
            }
        }

        public override void OnWidgetFocusReceived()
        {
            // No specific focus action needed
        }

        public override Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            // No specific state to save
            return state;
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            // No specific state to restore
        }

        protected override void OnDispose()
        {
            // Unsubscribe from events
            if (taskService != null)
            {
                taskService.TaskAdded -= (t) => RefreshMetrics();
                taskService.TaskUpdated -= (t) => RefreshMetrics();
                taskService.TaskDeleted -= (id) => RefreshMetrics();
            }

            if (projectService != null)
            {
                projectService.ProjectAdded -= (p) => RefreshMetrics();
                projectService.ProjectUpdated -= (p) => RefreshMetrics();
            }

            if (timeService != null)
            {
                timeService.EntryAdded -= (e) => RefreshMetrics();
                timeService.EntryUpdated -= (e) => RefreshMetrics();
            }

            // Stop and dispose timer
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer.Tick -= (s, e) => RefreshMetrics();
                refreshTimer = null;
            }

            logger.Info("ProjectStatsWidget", "Project stats widget disposed");
            base.OnDispose();
        }

        public void ApplyTheme()
        {
            theme = themeManager.CurrentTheme;

            if (mainGrid != null)
            {
                mainGrid.Background = new SolidColorBrush(theme.Background);
            }

            // Refresh UI to apply new theme
            RefreshMetrics();

            logger.Debug("ProjectStatsWidget", "Applied theme update");
        }
    }
}
