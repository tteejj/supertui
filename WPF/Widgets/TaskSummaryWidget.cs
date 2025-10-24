using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Widget showing task count summary
    /// </summary>
    public class TaskSummaryWidget : WidgetBase
    {
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

        private StackPanel contentPanel;

        public TaskSummaryWidget()
        {
            WidgetType = "TaskSummary";
            BuildUI();
        }

        private void BuildUI()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };

            contentPanel = new StackPanel();

            // Title
            var title = new TextBlock
            {
                Text = "TASKS",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            contentPanel.Children.Add(title);

            border.Child = contentPanel;
            this.Content = border;
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

            // Add stat items
            AddStatItem("Total", Data.TotalTasks.ToString(), "#4EC9B0");
            AddStatItem("Completed", Data.CompletedTasks.ToString(), "#6A9955");
            AddStatItem("Pending", Data.PendingTasks.ToString(), "#569CD6");
            AddStatItem("Overdue", Data.OverdueTasks.ToString(), "#F48771");
        }

        private void AddStatItem(string label, string value, string colorHex)
        {
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
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                Width = 100
            };

            var valueText = new TextBlock
            {
                Text = value,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex))
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
    }
}
