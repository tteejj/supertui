using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets.Overlays
{
    /// <summary>
    /// Detail panel overlay for selected task
    /// Shows in right zone, auto-updates when task selection changes
    /// </summary>
    public class TaskDetailOverlay : UserControl
    {
        private readonly TaskItem task;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly ITimeTrackingService timeTracking;

        private StackPanel mainPanel;
        private TextBlock titleText;
        private TextBlock statusText;
        private TextBlock priorityText;
        private TextBlock dueDateText;
        private TextBlock progressText;
        private TextBlock estimatedText;
        private TextBlock actualText;
        private TextBlock varianceText;
        private TextBlock assignedToText;
        private TextBlock externalIdText;
        private TextBlock descriptionText;
        private TextBlock tagsText;
        private TextBlock actionsText;

        public TaskDetailOverlay(TaskItem task)
        {
            this.task = task ?? throw new ArgumentNullException(nameof(task));
            this.logger = Logger.Instance;
            this.themeManager = ThemeManager.Instance;
            this.timeTracking = TimeTrackingService.Instance;

            BuildUI();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            mainPanel = new StackPanel
            {
                Margin = new Thickness(15),
                Background = new SolidColorBrush(theme.Surface)
            };

            // Title (prominent)
            titleText = new TextBlock
            {
                Text = task.Title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };
            mainPanel.Children.Add(titleText);

            // Status
            AddLabelValuePair("Status:", GetStatusDisplay(), theme);

            // Priority
            AddLabelValuePair("Priority:", GetPriorityDisplay(), theme);

            // Due Date
            if (task.DueDate.HasValue)
            {
                var dueDateDisplay = task.DueDate.Value.ToString("yyyy-MM-dd (ddd)");
                var daysUntil = (task.DueDate.Value - DateTime.Today).Days;

                if (daysUntil < 0)
                    dueDateDisplay += $" (Overdue by {-daysUntil} days)";
                else if (daysUntil == 0)
                    dueDateDisplay += " (Today!)";
                else
                    dueDateDisplay += $" (in {daysUntil} days)";

                AddLabelValuePair("Due:", dueDateDisplay, theme);
            }

            // Progress
            if (task.Progress > 0)
            {
                AddLabelValuePair("Progress:", $"{task.Progress}%", theme);
            }

            // Assigned To
            if (!string.IsNullOrWhiteSpace(task.AssignedTo))
            {
                AddLabelValuePair("Assigned:", task.AssignedTo, theme);
            }

            // External IDs
            if (!string.IsNullOrWhiteSpace(task.ExternalId1) || !string.IsNullOrWhiteSpace(task.ExternalId2))
            {
                var externalIds = $"{task.ExternalId1 ?? "-"} / {task.ExternalId2 ?? "-"}";
                AddLabelValuePair("Ext IDs:", externalIds, theme);
            }

            // Time Tracking Section
            AddSeparator(theme);

            // Estimated Duration
            if (task.EstimatedDuration.HasValue)
            {
                AddLabelValuePair("Estimated:", FormatDuration(task.EstimatedDuration.Value), theme);
            }

            // Actual Duration (calculated from time entries)
            var actualDuration = task.ActualDuration;
            if (actualDuration > TimeSpan.Zero)
            {
                AddLabelValuePair("Actual:", FormatDuration(actualDuration), theme);

                // Time Variance
                if (task.EstimatedDuration.HasValue)
                {
                    var variance = task.TimeVariance ?? TimeSpan.Zero;
                    var varianceColor = task.IsOverEstimate ? theme.Error : theme.Success;
                    var variancePrefix = task.IsOverEstimate ? "+" : "";

                    AddLabelValuePair(
                        "Variance:",
                        $"{variancePrefix}{FormatDuration(variance)}",
                        theme,
                        varianceColor
                    );
                }
            }

            // Description
            if (!string.IsNullOrWhiteSpace(task.Description))
            {
                AddSeparator(theme);

                var descLabel = new TextBlock
                {
                    Text = "Description:",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(theme.Foreground),
                    Margin = new Thickness(0, 5, 0, 5)
                };
                mainPanel.Children.Add(descLabel);

                descriptionText = new TextBlock
                {
                    Text = task.Description,
                    Foreground = new SolidColorBrush(theme.Foreground),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                mainPanel.Children.Add(descriptionText);
            }

            // Tags
            if (task.Tags != null && task.Tags.Count > 0)
            {
                AddSeparator(theme);

                var tagsLabel = new TextBlock
                {
                    Text = "Tags:",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(theme.Foreground),
                    Margin = new Thickness(0, 5, 0, 5)
                };
                mainPanel.Children.Add(tagsLabel);

                var tagsPanel = new WrapPanel();
                foreach (var tag in task.Tags)
                {
                    var tagBorder = new Border
                    {
                        Background = new SolidColorBrush(theme.Primary),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 5, 5)
                    };

                    var tagText = new TextBlock
                    {
                        Text = tag,
                        Foreground = new SolidColorBrush(theme.Background),
                        FontSize = 11
                    };

                    tagBorder.Child = tagText;
                    tagsPanel.Children.Add(tagBorder);
                }
                mainPanel.Children.Add(tagsPanel);
            }

            // Metadata
            AddSeparator(theme);
            AddLabelValuePair("Created:", task.CreatedAt.ToString("yyyy-MM-dd HH:mm"), theme);
            AddLabelValuePair("Updated:", task.UpdatedAt.ToString("yyyy-MM-dd HH:mm"), theme);

            if (task.CompletedDate.HasValue)
            {
                AddLabelValuePair("Completed:", task.CompletedDate.Value.ToString("yyyy-MM-dd HH:mm"), theme);
            }

            // Available Actions (keyboard shortcuts)
            AddSeparator(theme);
            actionsText = new TextBlock
            {
                Text = "[e]dit  [t]imer  [d]elete  [Esc]close",
                Foreground = new SolidColorBrush(theme.Primary),
                FontSize = 12,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.Children.Add(actionsText);

            // Wrap in ScrollViewer for long content
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = mainPanel
            };

            this.Content = scrollViewer;
            this.Focusable = true;
        }

        /// <summary>
        /// Add label-value pair to panel
        /// </summary>
        private void AddLabelValuePair(string label, string value, Theme theme, Color? valueColor = null)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 3)
            };

            var labelText = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Width = 100
            };

            var valueText = new TextBlock
            {
                Text = value,
                Foreground = new SolidColorBrush(valueColor ?? theme.Foreground),
                TextWrapping = TextWrapping.Wrap
            };

            panel.Children.Add(labelText);
            panel.Children.Add(valueText);
            mainPanel.Children.Add(panel);
        }

        /// <summary>
        /// Add visual separator
        /// </summary>
        private void AddSeparator(Theme theme)
        {
            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(theme.Border),
                Margin = new Thickness(0, 10, 0, 10)
            };
            mainPanel.Children.Add(separator);
        }

        /// <summary>
        /// Get status display with symbol
        /// </summary>
        private string GetStatusDisplay()
        {
            return task.Status switch
            {
                TaskStatus.Pending => "○ Pending",
                TaskStatus.InProgress => "◐ In Progress",
                TaskStatus.Completed => "● Completed",
                TaskStatus.Cancelled => "✗ Cancelled",
                _ => task.Status.ToString()
            };
        }

        /// <summary>
        /// Get priority display with symbol
        /// </summary>
        private string GetPriorityDisplay()
        {
            return task.Priority switch
            {
                TaskPriority.Low => "↓ Low",
                TaskPriority.Medium => "→ Medium",
                TaskPriority.High => "↑ High",
                TaskPriority.Today => "‼ Today",
                _ => task.Priority.ToString()
            };
        }

        /// <summary>
        /// Format duration for display
        /// </summary>
        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
                return $"{duration.TotalDays:F1}d";
            else if (duration.TotalHours >= 1)
                return $"{duration.TotalHours:F1}h";
            else if (duration.TotalMinutes >= 1)
                return $"{duration.TotalMinutes:F0}m";
            else
                return $"{duration.TotalSeconds:F0}s";
        }

        /// <summary>
        /// Handle keyboard shortcuts within overlay
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Let parent handle e/t/d shortcuts
            // Overlay just displays info, actions delegated to widget

            base.OnKeyDown(e);
        }
    }
}
