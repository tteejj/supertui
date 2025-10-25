using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Widget showing task count summary
    /// </summary>
    public class TaskSummaryWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;

        // This would normally come from a service
        // For demo purposes, we'll create a simple data structure
        public class TaskData
        {
            public int TotalTasks { get; set; }
            public int CompletedTasks { get; set; }
            public int PendingTasks { get; set; }
            public int OverdueTasks { get; set; }
        }

        private TaskData taskData;
        public TaskData Data
        {
            get => taskData;
            set
            {
                taskData = value;
                OnPropertyChanged(nameof(Data));
                UpdateDisplay();
            }
        }

        private StandardWidgetFrame frame;
        private StackPanel contentPanel;

        /// <summary>
        /// DI constructor - preferred for new code
        /// </summary>
        public TaskSummaryWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            WidgetType = "TaskSummary";
            BuildUI();
        }

        /// <summary>
        /// Parameterless constructor for backward compatibility
        /// </summary>
        public TaskSummaryWidget()
            : this(Logger.Instance, ThemeManager.Instance, ConfigurationManager.Instance)
        {
        }

        private void BuildUI()
        {
            // Create standard frame
            frame = new StandardWidgetFrame(themeManager)
            {
                Title = "TASK SUMMARY"
            };

            // Create content panel
            contentPanel = new StackPanel
            {
                Margin = new Thickness(15)
            };

            frame.Content = contentPanel;
            frame.SetStandardShortcuts("F5: Refresh", "?: Help");

            this.Content = frame;
        }

        public override void Initialize()
        {
            // Load real data from TaskService
            RefreshData();

            // Subscribe to task events for real-time updates
            var taskService = Core.Services.TaskService.Instance;
            taskService.TaskAdded += OnTaskChanged;
            taskService.TaskUpdated += OnTaskChanged;
            taskService.TaskDeleted += (id) => RefreshData();
            taskService.TasksReloaded += RefreshData;
        }

        private void OnTaskChanged(Core.Models.TaskItem task)
        {
            RefreshData();
        }

        private void RefreshData()
        {
            var taskService = Core.Services.TaskService.Instance;
            var allTasks = taskService.GetAllTasks();

            Data = new TaskData
            {
                TotalTasks = allTasks.Count,
                CompletedTasks = allTasks.Count(t => t.Status == Core.Models.TaskStatus.Completed),
                PendingTasks = allTasks.Count(t => t.Status == Core.Models.TaskStatus.Pending),
                OverdueTasks = allTasks.Count(t => t.IsOverdue)
            };
        }

        private void UpdateDisplay()
        {
            // Clear existing items
            contentPanel.Children.Clear();

            if (Data == null) return;

            var theme = themeManager.CurrentTheme;

            // Update context info in frame
            frame.ContextInfo = $"{Data.TotalTasks} total tasks";

            // Add stat items using theme colors
            AddStatItem("Total", Data.TotalTasks.ToString(), theme.Info);
            AddStatItem("Completed", Data.CompletedTasks.ToString(), theme.Success);
            AddStatItem("Pending", Data.PendingTasks.ToString(), theme.Primary);
            AddStatItem("Overdue", Data.OverdueTasks.ToString(), theme.Error);
        }

        private void AddStatItem(string label, string value, Color color)
        {
            var theme = themeManager.CurrentTheme;

            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var labelText = new TextBlock
            {
                Text = label + ":",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                Foreground = new SolidColorBrush(theme.Foreground),
                Width = 100
            };

            var valueText = new TextBlock
            {
                Text = value,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            };

            itemPanel.Children.Add(labelText);
            itemPanel.Children.Add(valueText);
            contentPanel.Children.Add(itemPanel);
        }

        public override void Refresh()
        {
            RefreshData();
        }

        protected override void OnDispose()
        {
            // Unsubscribe from task events
            var taskService = Core.Services.TaskService.Instance;
            if (taskService != null)
            {
                taskService.TaskAdded -= OnTaskChanged;
                taskService.TaskUpdated -= OnTaskChanged;
                taskService.TaskDeleted -= (id) => RefreshData();
                taskService.TasksReloaded -= RefreshData;
            }

            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            if (frame != null)
            {
                frame.ApplyTheme();
            }

            // Rebuild display with new theme colors
            if (Data != null)
            {
                UpdateDisplay();
            }
        }
    }
}
