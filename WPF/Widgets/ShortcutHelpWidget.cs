using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Widget that displays all registered keyboard shortcuts
    /// Provides searchable list of shortcuts with descriptions
    /// </summary>
    public class ShortcutHelpWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private StandardWidgetFrame frame;
        private Border containerBorder;
        private TextBox searchBox;
        private ListBox shortcutList;
        private TextBlock footerText;

        private List<ShortcutInfo> allShortcuts = new List<ShortcutInfo>();
        private string currentFilter = "";

        private class ShortcutInfo
        {
            public string Keys { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
        }

        public ShortcutHelpWidget(ILogger logger, IThemeManager themeManager)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            WidgetType = "ShortcutHelp";
            WidgetName = "Keyboard Shortcuts";
        }

        // Backward compatibility constructor
        public ShortcutHelpWidget() : this(Logger.Instance, ThemeManager.Instance)
        {
        }

        public override void Initialize()
        {
            LoadShortcuts();
            BuildUI();
            UpdateDisplay();
        }

        private void LoadShortcuts()
        {
            allShortcuts.Clear();

            // Load globally registered shortcuts from ShortcutManager first
            try
            {
                var shortcutManager = ShortcutManager.Instance;
                var registeredShortcuts = shortcutManager.GetAllShortcuts();

                foreach (var shortcut in registeredShortcuts)
                {
                    var category = InferCategory(shortcut.Description);
                    allShortcuts.Add(new ShortcutInfo
                    {
                        Category = category,
                        Keys = FormatKeyCombo(shortcut.Key, shortcut.Modifier),
                        Description = shortcut.Description ?? "Shortcut"
                    });
                }

                logger.Info("ShortcutHelp", $"Loaded {registeredShortcuts.Count} shortcuts from ShortcutManager");
            }
            catch (Exception ex)
            {
                logger.Warning("ShortcutHelp", $"Failed to load shortcuts from ShortcutManager: {ex.Message}");
            }

            // Add widget-specific shortcuts that aren't globally registered
            // These are handled by individual widgets via OnWidgetKeyDown()

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Counter Widget",
                Keys = "Up",
                Description = "Increment counter"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Counter Widget",
                Keys = "Down",
                Description = "Decrement counter"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Counter Widget",
                Keys = "R",
                Description = "Reset counter to zero"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "File Explorer",
                Keys = "Enter",
                Description = "Open selected file/folder"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "File Explorer",
                Keys = "Backspace",
                Description = "Go up one directory"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "File Explorer",
                Keys = "F5",
                Description = "Refresh file list"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Todo Widget",
                Keys = "Space",
                Description = "Toggle todo completion"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Terminal Widget",
                Keys = "Up",
                Description = "Previous command in history"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Terminal Widget",
                Keys = "Down",
                Description = "Next command in history"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Command Palette",
                Keys = "Enter",
                Description = "Execute selected command"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Command Palette",
                Keys = "Up/Down",
                Description = "Navigate command list"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Command Palette",
                Keys = "Escape",
                Description = "Clear search / Close palette"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Settings Widget",
                Keys = "Ctrl+S",
                Description = "Save settings"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Settings Widget",
                Keys = "F5",
                Description = "Reload settings from file"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Shortcut Help",
                Keys = "F5",
                Description = "Refresh shortcut list"
            });

            allShortcuts.Add(new ShortcutInfo
            {
                Category = "Shortcut Help",
                Keys = "Escape",
                Description = "Clear search"
            });

            logger.Info("ShortcutHelp", $"Loaded total of {allShortcuts.Count} keyboard shortcuts");
        }

        /// <summary>
        /// Infer category from shortcut description
        /// </summary>
        private string InferCategory(string description)
        {
            if (string.IsNullOrEmpty(description))
                return "General";

            var lower = description.ToLower();

            if (lower.Contains("workspace") || lower.Contains("switch to workspace"))
                return "Workspace";
            if (lower.Contains("focus") || lower.Contains("widget"))
                return "Navigation";
            if (lower.Contains("quit") || lower.Contains("exit") || lower.Contains("close"))
                return "Application";
            if (lower.Contains("help"))
                return "Help";

            return "General";
        }

        private string FormatKeyCombo(Key key, ModifierKeys modifier)
        {
            var parts = new List<string>();

            if ((modifier & ModifierKeys.Control) == ModifierKeys.Control)
                parts.Add("Ctrl");
            if ((modifier & ModifierKeys.Alt) == ModifierKeys.Alt)
                parts.Add("Alt");
            if ((modifier & ModifierKeys.Shift) == ModifierKeys.Shift)
                parts.Add("Shift");
            if ((modifier & ModifierKeys.Windows) == ModifierKeys.Windows)
                parts.Add("Win");

            parts.Add(key.ToString());

            return string.Join("+", parts);
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            // Create standard frame
            frame = new StandardWidgetFrame(themeManager)
            {
                Title = "KEYBOARD SHORTCUTS"
            };
            frame.SetStandardShortcuts("Type to search", "F5: Refresh", "?: Help");

            var mainPanel = new StackPanel
            {
                Margin = new Thickness(15)
            };

            // Search box
            var searchPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(15, 5, 15, 10)
            };

            var searchLabel = new TextBlock
            {
                Text = "Search:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            searchBox = new TextBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = 200
            };

            searchBox.TextChanged += (s, e) =>
            {
                currentFilter = searchBox.Text?.ToLower() ?? "";
                UpdateDisplay();
            };

            searchPanel.Children.Add(searchLabel);
            searchPanel.Children.Add(searchBox);

            // Shortcut list
            shortcutList = new ListBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(15, 0, 15, 10),
                MinHeight = 300
            };
            ScrollViewer.SetVerticalScrollBarVisibility(shortcutList, ScrollBarVisibility.Auto);

            // Footer
            footerText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(15, 0, 15, 15),
                TextAlignment = TextAlignment.Center
            };

            mainPanel.Children.Add(searchPanel);
            mainPanel.Children.Add(shortcutList);
            mainPanel.Children.Add(footerText);

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Child = mainPanel
            };

            frame.Content = containerBorder;
            this.Content = frame;
        }

        private void UpdateDisplay()
        {
            shortcutList.Items.Clear();

            var filtered = allShortcuts
                .Where(s =>
                    string.IsNullOrEmpty(currentFilter) ||
                    s.Keys.ToLower().Contains(currentFilter) ||
                    s.Description.ToLower().Contains(currentFilter) ||
                    s.Category.ToLower().Contains(currentFilter))
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Keys);

            string currentCategory = null;

            foreach (var shortcut in filtered)
            {
                // Add category header
                if (shortcut.Category != currentCategory)
                {
                    currentCategory = shortcut.Category;

                    var categoryHeader = new TextBlock
                    {
                        Text = $"─── {currentCategory} ───",
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(themeManager.CurrentTheme.Info),
                        Margin = new Thickness(0, 10, 0, 5)
                    };

                    shortcutList.Items.Add(categoryHeader);
                }

                // Add shortcut item
                var itemPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(10, 2, 10, 2)
                };

                var keysText = new TextBlock
                {
                    Text = shortcut.Keys.PadRight(20),
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(themeManager.CurrentTheme.SyntaxKeyword),
                    Width = 150
                };

                var descText = new TextBlock
                {
                    Text = shortcut.Description,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    Foreground = new SolidColorBrush(themeManager.CurrentTheme.Foreground)
                };

                itemPanel.Children.Add(keysText);
                itemPanel.Children.Add(descText);

                shortcutList.Items.Add(itemPanel);
            }

            footerText.Text = $"Showing {shortcutList.Items.Count - allShortcuts.Select(s => s.Category).Distinct().Count()} shortcuts" +
                             (string.IsNullOrEmpty(currentFilter) ? "" : $" (filtered from {allShortcuts.Count})");
        }

        public override void OnWidgetKeyDown(KeyEventArgs e)
        {
            // F5 to refresh
            if (e.Key == Key.F5)
            {
                LoadShortcuts();
                UpdateDisplay();
                e.Handled = true;
            }
            // Escape to clear search
            else if (e.Key == Key.Escape && !string.IsNullOrEmpty(searchBox.Text))
            {
                searchBox.Text = "";
                e.Handled = true;
            }
        }

        protected override void OnDispose()
        {
            // No resources to dispose
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            if (frame != null)
            {
                frame.ApplyTheme();
            }

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
            }

            if (searchBox != null)
            {
                searchBox.Background = new SolidColorBrush(theme.Surface);
                searchBox.Foreground = new SolidColorBrush(theme.Foreground);
                searchBox.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (shortcutList != null)
            {
                shortcutList.Background = new SolidColorBrush(theme.BackgroundSecondary);
                shortcutList.Foreground = new SolidColorBrush(theme.Foreground);
                shortcutList.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (footerText != null)
            {
                footerText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
            }

            // Rebuild display with new colors
            UpdateDisplay();
        }
    }
}
