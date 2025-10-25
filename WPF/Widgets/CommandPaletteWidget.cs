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
using SuperTUI.Infrastructure;
using SuperTUI.Extensions;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Command palette widget with fuzzy search.
    /// Quick access to commands, files, and actions via Ctrl+P style interface.
    /// </summary>
    public class CommandPaletteWidget : WidgetBase, IThemeable
    {
        private TextBox searchBox;
        private ListBox resultsBox;
        private TextBlock statusLabel;
        private List<PaletteItem> allItems;
        private List<PaletteItem> filteredItems;
        private Theme theme;

        public CommandPaletteWidget()
        {
            Name = "Command Palette";
            allItems = new List<PaletteItem>();
            filteredItems = new List<PaletteItem>();
        }

        public override void Initialize()
        {
            theme = ThemeManager.Instance.CurrentTheme;
            BuildUI();
            PopulateCommands();
            RefreshResults();
        }

        private void BuildUI()
        {
            var mainPanel = new DockPanel
            {
                Background = new SolidColorBrush(theme.Background),
                LastChildFill = true
            };

            // Title
            var title = new TextBlock
            {
                Text = "COMMAND PALETTE",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(title, Dock.Top);
            mainPanel.Children.Add(title);

            // Search box
            var searchPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(searchPanel, Dock.Top);

            var searchIcon = new TextBlock
            {
                Text = "🔍",
                FontSize = 14,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            searchPanel.Children.Add(searchIcon);

            searchBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                Height = 30
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            searchBox.KeyDown += SearchBox_KeyDown;
            searchPanel.Children.Add(searchBox);

            mainPanel.Children.Add(searchPanel);

            // Status label
            statusLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 5, 0, 0)
            };
            DockPanel.SetDock(statusLabel, Dock.Bottom);
            mainPanel.Children.Add(statusLabel);

            // Results list
            resultsBox = new ListBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            resultsBox.MouseDoubleClick += ResultsBox_MouseDoubleClick;

            mainPanel.Children.Add(resultsBox);
            Content = mainPanel;

            // Focus search box
            searchBox.Focus();
        }

        private void PopulateCommands()
        {
            // Workspace commands
            allItems.Add(new PaletteItem
            {
                Category = "Workspace",
                Name = "Switch to Next Workspace",
                Description = "Navigate to next workspace",
                Icon = "📂",
                Action = () => Logger.Instance.Info("Palette", "Switch to next workspace")
            });

            allItems.Add(new PaletteItem
            {
                Category = "Workspace",
                Name = "Switch to Previous Workspace",
                Description = "Navigate to previous workspace",
                Icon = "📂",
                Action = () => Logger.Instance.Info("Palette", "Switch to previous workspace")
            });

            // Theme commands
            allItems.Add(new PaletteItem
            {
                Category = "Theme",
                Name = "Change Theme",
                Description = "Select a different theme",
                Icon = "🎨",
                Action = () => Logger.Instance.Info("Palette", "Change theme")
            });

            allItems.Add(new PaletteItem
            {
                Category = "Theme",
                Name = "Reload Theme",
                Description = "Refresh current theme",
                Icon = "🔄",
                Action = () =>
                {
                    theme = ThemeManager.Instance.CurrentTheme;
                    Logger.Instance.Info("Palette", "Theme reloaded");
                }
            });

            // Configuration commands
            allItems.Add(new PaletteItem
            {
                Category = "Config",
                Name = "Edit Configuration",
                Description = "Open configuration file",
                Icon = "⚙️",
                Action = () => Logger.Instance.Info("Palette", "Edit configuration")
            });

            allItems.Add(new PaletteItem
            {
                Category = "Config",
                Name = "Reload Configuration",
                Description = "Reload settings from file",
                Icon = "🔄",
                Action = () => Logger.Instance.Info("Palette", "Reload configuration")
            });

            // State commands
            allItems.Add(new PaletteItem
            {
                Category = "State",
                Name = "Save State",
                Description = "Save current workspace state",
                Icon = "💾",
                Action = () =>
                {
                    var persistence = StatePersistenceManager.Instance;
                    persistence.SaveState();
                    Logger.Instance.Info("Palette", "State saved");
                }
            });

            allItems.Add(new PaletteItem
            {
                Category = "State",
                Name = "Load State",
                Description = "Restore saved workspace state",
                Icon = "📥",
                Action = () =>
                {
                    var persistence = StatePersistenceManager.Instance;
                    persistence.LoadState();
                    Logger.Instance.Info("Palette", "State loaded");
                }
            });

            // Help commands
            allItems.Add(new PaletteItem
            {
                Category = "Help",
                Name = "Show Keyboard Shortcuts",
                Description = "Display all keyboard shortcuts",
                Icon = "⌨️",
                Action = () => Logger.Instance.Info("Palette", "Show shortcuts")
            });

            allItems.Add(new PaletteItem
            {
                Category = "Help",
                Name = "View Event Statistics",
                Description = "Show EventBus statistics",
                Icon = "📊",
                Action = () =>
                {
                    var stats = EventBus.Instance.GetStatistics();
                    Logger.Instance.Info("Palette", $"Events: {stats.Published} published, {stats.Delivered} delivered");
                }
            });

            allItems.Add(new PaletteItem
            {
                Category = "Help",
                Name = "About SuperTUI",
                Description = "Version and information",
                Icon = "ℹ️",
                Action = () => Logger.Instance.Info("Palette", "SuperTUI Framework v0.1.0")
            });
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshResults();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && resultsBox.SelectedItem != null)
            {
                ExecuteSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (resultsBox.Items.Count > 0)
                {
                    if (resultsBox.SelectedIndex < resultsBox.Items.Count - 1)
                        resultsBox.SelectedIndex++;
                    else
                        resultsBox.SelectedIndex = 0;
                    resultsBox.ScrollIntoView(resultsBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (resultsBox.Items.Count > 0)
                {
                    if (resultsBox.SelectedIndex > 0)
                        resultsBox.SelectedIndex--;
                    else
                        resultsBox.SelectedIndex = resultsBox.Items.Count - 1;
                    resultsBox.ScrollIntoView(resultsBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                searchBox.Clear();
                e.Handled = true;
            }
        }

        private void ResultsBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelected();
        }

        private void RefreshResults()
        {
            var searchText = searchBox.Text.Trim().ToLower();
            resultsBox.Items.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                // Show all items
                filteredItems = allItems.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();
            }
            else
            {
                // Fuzzy search
                filteredItems = allItems
                    .Where(item => FuzzyMatch(item, searchText))
                    .OrderByDescending(item => CalculateScore(item, searchText))
                    .ThenBy(item => item.Name)
                    .ToList();
            }

            foreach (var item in filteredItems.Take(50)) // Limit to 50 results
            {
                resultsBox.Items.Add(FormatItem(item));
            }

            if (resultsBox.Items.Count > 0)
                resultsBox.SelectedIndex = 0;

            statusLabel.Text = $"{filteredItems.Count} results";
        }

        private bool FuzzyMatch(PaletteItem item, string search)
        {
            var text = $"{item.Category} {item.Name} {item.Description}".ToLower();

            // Simple contains check
            if (text.Contains(search))
                return true;

            // Fuzzy match - all characters in order
            int searchIndex = 0;
            foreach (char c in text)
            {
                if (searchIndex < search.Length && c == search[searchIndex])
                    searchIndex++;
            }
            return searchIndex == search.Length;
        }

        private int CalculateScore(PaletteItem item, string search)
        {
            var text = $"{item.Name} {item.Description}".ToLower();
            int score = 0;

            // Exact match bonus
            if (item.Name.ToLower().Contains(search))
                score += 100;

            // Start of word bonus
            if (item.Name.ToLower().StartsWith(search))
                score += 50;

            // Category match bonus
            if (item.Category.ToLower().Contains(search))
                score += 25;

            // Length penalty (prefer shorter matches)
            score -= text.Length / 10;

            return score;
        }

        private string FormatItem(PaletteItem item)
        {
            return $"{item.Icon} {item.Name} - {item.Description} [{item.Category}]";
        }

        private void ExecuteSelected()
        {
            if (resultsBox.SelectedIndex < 0 || resultsBox.SelectedIndex >= filteredItems.Count)
                return;

            var item = filteredItems[resultsBox.SelectedIndex];

            try
            {
                item.Action?.Invoke();
                Logger.Instance.Info("Palette", $"Executed: {item.Name}");
                statusLabel.Text = $"✓ Executed: {item.Name}";
                statusLabel.Foreground = new SolidColorBrush(theme.Success);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Palette", $"Failed to execute: {ex.Message}");
                statusLabel.Text = $"✗ Error: {ex.Message}";
                statusLabel.Foreground = new SolidColorBrush(theme.Error);
            }
        }

        public void AddCommand(string category, string name, string description, string icon, Action action)
        {
            allItems.Add(new PaletteItem
            {
                Category = category,
                Name = name,
                Description = description,
                Icon = icon,
                Action = action
            });
            RefreshResults();
        }

        public override Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["SearchText"] = searchBox.Text
            };
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state.TryGetValue("SearchText", out var text))
            {
                searchBox.Text = text.ToString();
            }
        }

        protected override void OnDispose()
        {
            searchBox.TextChanged -= SearchBox_TextChanged;
            searchBox.KeyDown -= SearchBox_KeyDown;
            resultsBox.MouseDoubleClick -= ResultsBox_MouseDoubleClick;
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            theme = ThemeManager.Instance.CurrentTheme;

            if (searchBox != null)
            {
                searchBox.Background = new SolidColorBrush(theme.Surface);
                searchBox.Foreground = new SolidColorBrush(theme.Foreground);
                searchBox.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (resultsBox != null)
            {
                resultsBox.Background = new SolidColorBrush(theme.Background);
                resultsBox.Foreground = new SolidColorBrush(theme.Foreground);
            }

            if (statusLabel != null)
            {
                statusLabel.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
            }
        }
    }

    /// <summary>
    /// Represents a command palette item
    /// </summary>
    public class PaletteItem
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public Action Action { get; set; }

        public override string ToString() => $"{Icon} {Name}";
    }
}
