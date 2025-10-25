using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core;
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

        private Border containerBorder;
        private TextBlock titleText;
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
            var theme = themeManager.CurrentTheme;

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };

            contentPanel = new StackPanel();

            // Title
            titleText = new TextBlock
            {
                Text = "TASKS",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10)
            };
            contentPanel.Children.Add(titleText);

            containerBorder.Child = contentPanel;
            this.Content = containerBorder;
        }

        public override void Initialize()
        {
            // Initialize with sample data
            // In real implementation, this would come from TaskService
            Data = new TaskData
            {
                TotalTasks = 15,
                CompletedTasks = 7,
                PendingTasks = 6,
                OverdueTasks = 2
            };
        }

        private void UpdateDisplay()
        {
            // Clear existing items (except title)
            while (contentPanel.Children.Count > 1)
                contentPanel.Children.RemoveAt(1);

            if (Data == null) return;

            var theme = themeManager.CurrentTheme;

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
            // In real implementation, fetch latest data from TaskService
            // For now, simulate data change
            if (Data != null)
            {
                var random = new Random();
                Data = new TaskData
                {
                    TotalTasks = Data.TotalTasks,
                    CompletedTasks = random.Next(0, Data.TotalTasks),
                    PendingTasks = random.Next(0, Data.TotalTasks),
                    OverdueTasks = random.Next(0, 5)
                };
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
            var theme = themeManager.CurrentTheme;

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
                containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
            }

            // Rebuild display with new theme colors
            if (Data != null)
            {
                UpdateDisplay();
            }
        }
    }
}
