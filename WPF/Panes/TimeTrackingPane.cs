// TimeTrackingPane.cs - Time tracking with weekly reports and keyboard navigation
// Track project hours with week-ending dates (Sunday)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Time tracking pane with weekly report view
    /// </summary>
    public class TimeTrackingPane : PaneBase
    {
        // Services
        private readonly ITimeTrackingService timeService;
        private readonly IProjectService projectService;

        // UI Components
        private Grid mainGrid;
        private ListBox entryListBox;
        private TextBlock weekLabel;
        private TextBlock summaryText;
        private TextBlock statusBar;

        // State
        private DateTime currentWeekEnding;
        private List<TimeEntry> currentEntries = new List<TimeEntry>();
        private TimeEntry selectedEntry;

        // Theme colors
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush successBrush;
        private SolidColorBrush surfaceBrush;
        private SolidColorBrush dimBrush;
        private SolidColorBrush borderBrush;

        public TimeTrackingPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            ITimeTrackingService timeService,
            IProjectService projectService)
            : base(logger, themeManager, projectContext)
        {
            this.timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            PaneName = "Time Tracking";
            PaneIcon = "⏱️";
        }

        public override void Initialize()
        {
            base.Initialize();
            CacheThemeColors();
            RegisterPaneShortcuts();

            // Initialize service
            timeService.Initialize();
            currentWeekEnding = TimeTrackingService.GetCurrentWeekEnding();

            LoadWeek();

            // Subscribe to changes
            timeService.EntryAdded += (e) => LoadWeek();
            timeService.EntryUpdated += (e) => LoadWeek();
            timeService.EntryDeleted += (id) => LoadWeek();

            // Focus list
            this.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.Input.Keyboard.Focus(entryListBox);
                if (entryListBox.Items.Count > 0)
                    entryListBox.SelectedIndex = 0;
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override UIElement BuildContent()
        {
            CacheThemeColors();

            mainGrid = new Grid(); // No background - let PaneBase border show through for focus indicator
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Week nav
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Entry list
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Summary
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status

            // Header
            var header = new TextBlock
            {
                Text = "⏱️ Time Tracking",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Week navigation
            var weekNav = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };

            weekLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };
            weekNav.Children.Add(weekLabel);
            UpdateWeekLabel();

            Grid.SetRow(weekNav, 1);
            mainGrid.Children.Add(weekNav);

            // Entry list
            entryListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 13,
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(16, 0, 16, 12)
            };
            entryListBox.SelectionChanged += EntryListBox_SelectionChanged;
            entryListBox.KeyDown += EntryListBox_KeyDown;
            Grid.SetRow(entryListBox, 2);
            mainGrid.Children.Add(entryListBox);

            // Summary
            summaryText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = accentBrush,
                Background = surfaceBrush,
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(16, 0, 16, 12),
                Text = "Total: 0.0 hrs | 0 entries"
            };
            Grid.SetRow(summaryText, 3);
            mainGrid.Children.Add(summaryText);

            // Status bar
            statusBar = new TextBlock
            {
                Text = "W:WeeklyReport | Shift+←→:PrevNext week | A:Add | E:Edit | D:Delete | Ctrl+R:Refresh",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = fgBrush,
                Background = surfaceBrush,
                Padding = new Thickness(16, 8, 16, 8),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(statusBar, 4);
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
            surfaceBrush = new SolidColorBrush(theme.Surface);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
            borderBrush = new SolidColorBrush(theme.Border);
        }

        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;
            shortcuts.RegisterForPane(PaneName, Key.Left, ModifierKeys.Shift, () => PreviousWeek(), "Previous week");
            shortcuts.RegisterForPane(PaneName, Key.Right, ModifierKeys.Shift, () => NextWeek(), "Next week");
            shortcuts.RegisterForPane(PaneName, Key.W, ModifierKeys.None, () => ShowWeeklyReport(), "Show weekly report");
            shortcuts.RegisterForPane(PaneName, Key.A, ModifierKeys.None, () => AddEntry(), "Add time entry");
            shortcuts.RegisterForPane(PaneName, Key.E, ModifierKeys.None, () => EditEntry(), "Edit selected entry");
            shortcuts.RegisterForPane(PaneName, Key.D, ModifierKeys.None, () => DeleteEntry(), "Delete selected entry");
            shortcuts.RegisterForPane(PaneName, Key.R, ModifierKeys.Control, () => LoadWeek(), "Refresh");
        }

        private void EntryListBox_KeyDown(object sender, KeyEventArgs e)
        {
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
                e.Handled = true;
        }

        private void EntryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (entryListBox.SelectedItem is Border border && border.Tag is TimeEntry entry)
            {
                selectedEntry = entry;
            }
            else
            {
                selectedEntry = null;
            }
        }

        private void LoadWeek()
        {
            try
            {
                var report = timeService.GetWeeklyReport(currentWeekEnding);
                currentEntries = report.Entries.OrderBy(e => e.ProjectId).ThenBy(e => e.CreatedAt).ToList();
                RefreshList();
                UpdateSummary();
                ShowStatus($"Loaded {currentEntries.Count} entries for week ending {currentWeekEnding:MM/dd/yyyy}", false);
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(ErrorCategory.IO, ex, "Loading time entries", logger);
                ShowStatus("Failed to load time entries", true);
            }
        }

        private void RefreshList()
        {
            this.Dispatcher.Invoke(() =>
            {
                entryListBox.Items.Clear();
                foreach (var entry in currentEntries)
                {
                    var card = CreateEntryCard(entry);
                    entryListBox.Items.Add(card);
                }
            });
        }

        private Border CreateEntryCard(TimeEntry entry)
        {
            // Show timecode prominently if no project, otherwise show project
            string displayName;
            if (!string.IsNullOrWhiteSpace(entry.ID1))
            {
                // Timecode entry (non-project)
                displayName = $"[{entry.ID1}]";
            }
            else if (entry.ProjectId != Guid.Empty)
            {
                // Project entry
                var project = projectService.GetProject(entry.ProjectId);
                displayName = project?.Name ?? "Unknown Project";
            }
            else
            {
                // Neither project nor timecode
                displayName = "General Time";
            }

            var text = $"{displayName} - {entry.TotalHours:F1} hrs";
            if (!string.IsNullOrWhiteSpace(entry.Description))
                text += $"\n{entry.Description}";

            var textBlock = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 13,
                Foreground = fgBrush,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap
            };

            var border = new Border
            {
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(2, 0, 0, 0),
                Background = bgBrush,
                Margin = new Thickness(2),
                Child = textBlock,
                Tag = entry
            };

            return border;
        }

        private void UpdateWeekLabel()
        {
            var weekStart = TimeTrackingService.GetWeekStart(currentWeekEnding);
            weekLabel.Text = $"Week: {weekStart:MM/dd} - {currentWeekEnding:MM/dd/yyyy}";
        }

        private void UpdateSummary()
        {
            var totalHours = currentEntries.Sum(e => e.TotalHours);
            summaryText.Text = $"Total: {totalHours:F1} hrs | {currentEntries.Count} entries";
        }

        private void PreviousWeek()
        {
            currentWeekEnding = currentWeekEnding.AddDays(-7);
            UpdateWeekLabel();
            LoadWeek();
        }

        private void NextWeek()
        {
            currentWeekEnding = currentWeekEnding.AddDays(7);
            UpdateWeekLabel();
            LoadWeek();
        }

        private void ShowWeeklyReport()
        {
            var report = timeService.GetWeeklyReport(currentWeekEnding);
            var weekStart = TimeTrackingService.GetWeekStart(currentWeekEnding);

            // Separate timecode entries from project entries
            var timecodeEntries = report.Entries.Where(e => !string.IsNullOrWhiteSpace(e.ID1)).ToList();
            var projectEntries = report.Entries.Where(e => string.IsNullOrWhiteSpace(e.ID1) && e.ProjectId != Guid.Empty).ToList();

            var reportText = $"WEEKLY TIME REPORT\n";
            reportText += $"Week Ending: {currentWeekEnding:MM/dd/yyyy} ({weekStart:MM/dd} - {currentWeekEnding:MM/dd})\n";
            reportText += $"{'=',-60}\n\n";

            decimal grandTotal = 0;

            // Timecode entries first
            if (timecodeEntries.Any())
            {
                reportText += "TIMECODE ENTRIES:\n";
                var timecodeGroups = timecodeEntries.GroupBy(e => e.ID1).OrderBy(g => g.Key);

                foreach (var group in timecodeGroups)
                {
                    var codeTotal = group.Sum(e => e.TotalHours);
                    grandTotal += codeTotal;

                    reportText += $"[{group.Key}]: {codeTotal:F1} hrs\n";

                    foreach (var entry in group)
                    {
                        reportText += $"  - {entry.TotalHours:F1} hrs";
                        if (!string.IsNullOrWhiteSpace(entry.Description))
                            reportText += $" | {entry.Description}";
                        reportText += "\n";
                    }
                }
                reportText += "\n";
            }

            // Project entries
            if (projectEntries.Any())
            {
                reportText += "PROJECT ENTRIES:\n";
                var projectGroups = projectEntries.GroupBy(e => e.ProjectId).ToList();

                foreach (var group in projectGroups.OrderBy(g => g.Key))
                {
                    var project = projectService.GetProject(group.Key);
                    var projectName = project?.Name ?? "Unknown Project";
                    var projectTotal = group.Sum(e => e.TotalHours);
                    grandTotal += projectTotal;

                    reportText += $"{projectName}: {projectTotal:F1} hrs\n";

                    foreach (var entry in group)
                    {
                        reportText += $"  - {entry.TotalHours:F1} hrs";
                        if (!string.IsNullOrWhiteSpace(entry.Description))
                            reportText += $" | {entry.Description}";
                        reportText += "\n";
                    }
                }
                reportText += "\n";
            }

            reportText += $"{'=',-60}\n";
            reportText += $"TOTAL: {grandTotal:F1} hours\n";

            MessageBox.Show(reportText, "Weekly Time Report", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddEntry()
        {
            var dialog = CreateEntryDialog(null);
            if (dialog.ShowDialog() == true && dialog.Tag is TimeEntry newEntry)
            {
                try
                {
                    timeService.AddEntry(newEntry);
                    ShowStatus($"Added: {newEntry.TotalHours:F1} hrs", false);
                }
                catch (Exception ex)
                {
                    ErrorHandlingPolicy.Handle(ErrorCategory.IO, ex, "Adding time entry", logger);
                    ShowStatus("Failed to add entry", true);
                }
            }
        }

        private void EditEntry()
        {
            if (selectedEntry == null) return;

            var dialog = CreateEntryDialog(selectedEntry);
            if (dialog.ShowDialog() == true && dialog.Tag is TimeEntry updatedEntry)
            {
                try
                {
                    timeService.UpdateEntry(updatedEntry);
                    ShowStatus($"Updated: {updatedEntry.TotalHours:F1} hrs", false);
                }
                catch (Exception ex)
                {
                    ErrorHandlingPolicy.Handle(ErrorCategory.IO, ex, "Updating time entry", logger);
                    ShowStatus("Failed to update entry", true);
                }
            }
        }

        private void DeleteEntry()
        {
            if (selectedEntry == null) return;

            var result = MessageBox.Show($"Delete time entry ({selectedEntry.TotalHours:F1} hrs)?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    timeService.DeleteEntry(selectedEntry.Id);
                    ShowStatus("Entry deleted", false);
                }
                catch (Exception ex)
                {
                    ErrorHandlingPolicy.Handle(ErrorCategory.IO, ex, "Deleting time entry", logger);
                    ShowStatus("Failed to delete entry", true);
                }
            }
        }

        private Window CreateEntryDialog(TimeEntry existing)
        {
            var isEdit = existing != null;
            var entry = existing ?? new TimeEntry { WeekEnding = currentWeekEnding, ProjectId = Guid.Empty };

            var window = new Window
            {
                Title = isEdit ? "Edit Time Entry" : "Add Time Entry",
                Width = 500,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = surfaceBrush,
                Owner = Window.GetWindow(this)
            };

            var grid = new Grid { Margin = new Thickness(16) };
            for (int i = 0; i < 8; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int row = 0;

            // Timecode (ID1) - Primary field for non-project time
            grid.Children.Add(new TextBlock { Text = "Timecode (2-6 digits, leave blank for project time):", Foreground = fgBrush, Margin = new Thickness(0, 0, 0, 4) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row++);

            var timecodeBox = new TextBox
            {
                Text = entry.ID1 ?? "",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                Background = bgBrush,
                Foreground = fgBrush,
                CaretBrush = fgBrush,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 12),
                MaxLength = 6
            };
            Grid.SetRow(timecodeBox, row++);
            grid.Children.Add(timecodeBox);

            // Project selector (optional if timecode provided)
            grid.Children.Add(new TextBlock { Text = "OR Project (leave blank if using timecode):", Foreground = fgBrush, Margin = new Thickness(0, 0, 0, 4) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row++);

            var projectBox = new ComboBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                Background = bgBrush,
                Foreground = fgBrush,
                Margin = new Thickness(0, 0, 0, 12)
            };
            projectBox.Items.Add(new { Id = Guid.Empty, Display = "(None - using timecode)" });
            var projects = projectService.GetAllProjects().ToList();
            foreach (var p in projects)
                projectBox.Items.Add(new { Id = p.Id, Display = p.Name });
            projectBox.DisplayMemberPath = "Display";
            projectBox.SelectedValuePath = "Id";
            projectBox.SelectedValue = entry.ProjectId;
            if (projectBox.SelectedValue == null)
                projectBox.SelectedIndex = 0;
            Grid.SetRow(projectBox, row++);
            grid.Children.Add(projectBox);

            // Hours
            grid.Children.Add(new TextBlock { Text = "Hours:", Foreground = fgBrush, Margin = new Thickness(0, 0, 0, 4) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row++);

            var hoursBox = new TextBox
            {
                Text = entry.Hours > 0 ? entry.Hours.ToString("F1") : "",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                Background = bgBrush,
                Foreground = fgBrush,
                CaretBrush = fgBrush,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(hoursBox, row++);
            grid.Children.Add(hoursBox);

            // Description
            grid.Children.Add(new TextBlock { Text = "Description (optional):", Foreground = fgBrush, Margin = new Thickness(0, 0, 0, 4) });
            Grid.SetRow(grid.Children[grid.Children.Count - 1], row++);

            var descBox = new TextBox
            {
                Text = entry.Description ?? "",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                Background = bgBrush,
                Foreground = fgBrush,
                CaretBrush = fgBrush,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 12),
                AcceptsReturn = true,
                Height = 60
            };
            Grid.SetRow(descBox, row++);
            grid.Children.Add(descBox);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var saveButton = new Button { Content = "Save", Width = 80, Margin = new Thickness(0, 0, 8, 0), Background = accentBrush, Foreground = bgBrush };
            saveButton.Click += (s, e) =>
            {
                var timecode = timecodeBox.Text.Trim();
                var selectedProjectId = projectBox.SelectedValue != null ? (Guid)projectBox.SelectedValue : Guid.Empty;

                // Validate: Must have either timecode OR project
                if (string.IsNullOrWhiteSpace(timecode) && selectedProjectId == Guid.Empty)
                {
                    MessageBox.Show("Please enter a timecode OR select a project", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate timecode format (2-6 digits)
                if (!string.IsNullOrWhiteSpace(timecode))
                {
                    if (timecode.Length < 2 || timecode.Length > 6 || !timecode.All(char.IsDigit))
                    {
                        MessageBox.Show("Timecode must be 2-6 digits", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (!decimal.TryParse(hoursBox.Text, out decimal hours) || hours <= 0)
                {
                    MessageBox.Show("Please enter valid hours (> 0)", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                entry.ID1 = string.IsNullOrWhiteSpace(timecode) ? null : timecode;
                entry.ProjectId = selectedProjectId;
                entry.Hours = hours;
                entry.Description = descBox.Text.Trim();
                entry.WeekEnding = currentWeekEnding;
                entry.UpdatedAt = DateTime.Now;

                window.Tag = entry;
                window.DialogResult = true;
            };
            var cancelButton = new Button { Content = "Cancel", Width = 80, Background = surfaceBrush, Foreground = fgBrush };
            cancelButton.Click += (s, e) => window.DialogResult = false;

            // Tab order: timecode -> hours -> description (skip project unless needed)
            timecodeBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    System.Windows.Input.Keyboard.Focus(hoursBox);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape) window.DialogResult = false;
            };

            hoursBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    System.Windows.Input.Keyboard.Focus(descBox);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape) window.DialogResult = false;
            };

            descBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape) window.DialogResult = false;
            };

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, row);
            grid.Children.Add(buttonPanel);

            window.Content = grid;
            window.Loaded += (s, e) => System.Windows.Input.Keyboard.Focus(timecodeBox);

            return window;
        }

        private void ShowStatus(string message, bool isError)
        {
            this.Dispatcher.Invoke(() =>
            {
                statusBar.Text = message + " | W:Report | Shift+←→:Week | A:Add | E:Edit | D:Delete";
            });
        }

        protected override void OnDispose()
        {
            // Lambda-based handlers, no need to unsubscribe
            base.OnDispose();
        }
    }
}
