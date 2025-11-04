using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Help pane displaying all keyboard shortcuts dynamically from ShortcutManager
    /// Keyboard: ? or Shift+/ to open
    /// Features: Grouped by category (Global, Pane-specific), searchable
    /// </summary>
    public class HelpPane : PaneBase
    {
        private readonly IShortcutManager shortcutManager;
        private readonly IConfigurationManager configManager;

        // UI Components
        private ScrollViewer contentScrollViewer;
        private StackPanel shortcutsPanel;
        private TextBlock helpHeaderText;

        // Data
        private List<ShortcutGroup> allGroups;

        public HelpPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IShortcutManager shortcutManager,
            IConfigurationManager configManager)
            : base(logger, themeManager, projectContext)
        {
            this.shortcutManager = shortcutManager ?? throw new ArgumentNullException(nameof(shortcutManager));
            this.configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));

            PaneName = "Keyboard Shortcuts";
            PaneIcon = "?";
        }

        public override void Initialize()
        {
            base.Initialize();

            // Subscribe to theme changes
            themeManager.ThemeChanged += OnThemeChanged;
        }

        protected override UIElement BuildContent()
        {
            var theme = themeManager.CurrentTheme;

            // Main container
            var container = new Grid();
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

            // Title header
            var titleHeader = new Border
            {
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 12, 16, 12)
            };

            helpHeaderText = new TextBlock
            {
                Text = "⌨️  Keyboard Shortcuts Reference",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary)
            };
            titleHeader.Child = helpHeaderText;
            Grid.SetRow(titleHeader, 0);
            container.Children.Add(titleHeader);

            // Shortcuts content (scrollable)
            contentScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(16, 16, 16, 16)
            };

            shortcutsPanel = new StackPanel();
            contentScrollViewer.Content = shortcutsPanel;

            Grid.SetRow(contentScrollViewer, 1);
            container.Children.Add(contentScrollViewer);

            // Build shortcuts display
            BuildShortcutsDisplay();

            return container;
        }

        /// <summary>
        /// Build the shortcuts display grouped by category
        /// </summary>
        private void BuildShortcutsDisplay()
        {
            allGroups = new List<ShortcutGroup>();

            // Get all shortcuts from ShortcutManager
            var globalShortcuts = shortcutManager.GetGlobalShortcuts();

            // Group 1: Global Shortcuts
            if (globalShortcuts.Count > 0)
            {
                allGroups.Add(new ShortcutGroup
                {
                    Title = "Global Shortcuts",
                    Description = "Available anywhere in the application",
                    Shortcuts = globalShortcuts.ToList()
                });
            }

            // Group 2: Pane-Specific Shortcuts
            // Get all registered pane shortcuts (auto-discover from ShortcutManager)
            var allPaneShortcuts = shortcutManager.GetAllPaneShortcuts();
            foreach (var kvp in allPaneShortcuts)
            {
                var paneName = kvp.Key;
                var shortcuts = kvp.Value;

                if (shortcuts.Count > 0)
                {
                    allGroups.Add(new ShortcutGroup
                    {
                        Title = $"{paneName} Pane",
                        Description = $"Available when {paneName} pane is focused",
                        Shortcuts = shortcuts.ToList()
                    });
                }
            }

            // Add footer note
            var footerNote = "Press Esc or Ctrl+Shift+Q to close this pane";

            // Render the groups
            RefreshDisplay(footerNote);
        }

        /// <summary>
        /// Refresh the display with current search filter
        /// </summary>
        private void RefreshDisplay(string footerNote = null)
        {
            shortcutsPanel.Children.Clear();

            var theme = themeManager.CurrentTheme;
            int visibleGroupCount = 0;

            foreach (var group in allGroups)
            {
                // Show all shortcuts (search removed)
                var filteredShortcuts = group.Shortcuts;

                if (filteredShortcuts.Count == 0)
                    continue;

                visibleGroupCount++;

                // Group header
                var groupHeader = new TextBlock
                {
                    Text = group.Title,
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(theme.Primary),
                    Margin = new Thickness(0, 0, 0, 4)
                };

                // Add top margin only after first group
                if (visibleGroupCount > 1)
                {
                    groupHeader.Margin = new Thickness(0, 20, 0, 4);
                }

                shortcutsPanel.Children.Add(groupHeader);

                // Group description
                var groupDesc = new TextBlock
                {
                    Text = group.Description,
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(
                        (byte)(theme.Foreground.R * 0.7),
                        (byte)(theme.Foreground.G * 0.7),
                        (byte)(theme.Foreground.B * 0.7)
                    )),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                shortcutsPanel.Children.Add(groupDesc);

                // Shortcuts list
                foreach (var shortcut in filteredShortcuts.OrderBy(s => s.ToString()))
                {
                    var shortcutItem = BuildShortcutItem(shortcut);
                    shortcutsPanel.Children.Add(shortcutItem);
                }
            }

            // No results message
            if (visibleGroupCount == 0)
            {
                var noResults = new TextBlock
                {
                    Text = "No shortcuts registered",
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(
                        (byte)(theme.Foreground.R * 0.6),
                        (byte)(theme.Foreground.G * 0.6),
                        (byte)(theme.Foreground.B * 0.6)
                    )),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 32, 0, 0)
                };
                shortcutsPanel.Children.Add(noResults);
            }

            // Footer note
            if (!string.IsNullOrWhiteSpace(footerNote) && visibleGroupCount > 0)
            {
                var separator = new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(theme.Border),
                    Margin = new Thickness(0, 20, 0, 12)
                };
                shortcutsPanel.Children.Add(separator);

                var footer = new TextBlock
                {
                    Text = footerNote,
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(
                        (byte)(theme.Foreground.R * 0.6),
                        (byte)(theme.Foreground.G * 0.6),
                        (byte)(theme.Foreground.B * 0.6)
                    )),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 16)
                };
                shortcutsPanel.Children.Add(footer);
            }
        }

        /// <summary>
        /// Build UI for a single shortcut item
        /// </summary>
        private UIElement BuildShortcutItem(KeyboardShortcut shortcut)
        {
            var theme = themeManager.CurrentTheme;

            var grid = new Grid
            {
                Margin = new Thickness(0, 2, 0, 2)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Shortcut keys (monospace, highlighted)
            var keysText = new TextBlock
            {
                Text = FormatShortcutKeys(shortcut),
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Success),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(keysText, 0);
            grid.Children.Add(keysText);

            // Description
            var descText = new TextBlock
            {
                Text = shortcut.Description ?? "(No description)",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.Foreground),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(descText, 1);
            grid.Children.Add(descText);

            return grid;
        }

        /// <summary>
        /// Format shortcut keys in a readable way
        /// </summary>
        private string FormatShortcutKeys(KeyboardShortcut shortcut)
        {
            var parts = new List<string>();

            if ((shortcut.Modifiers & ModifierKeys.Control) != 0)
                parts.Add("Ctrl");
            if ((shortcut.Modifiers & ModifierKeys.Alt) != 0)
                parts.Add("Alt");
            if ((shortcut.Modifiers & ModifierKeys.Shift) != 0)
                parts.Add("Shift");
            if ((shortcut.Modifiers & ModifierKeys.Windows) != 0)
                parts.Add("Win");

            // Format the key nicely
            var keyName = FormatKeyName(shortcut.Key);
            parts.Add(keyName);

            return string.Join(" + ", parts);
        }

        /// <summary>
        /// Format key name in a user-friendly way
        /// </summary>
        private string FormatKeyName(Key key)
        {
            // Handle special keys
            return key switch
            {
                Key.OemQuestion => "?",
                Key.OemSemicolon => ";",
                Key.OemComma => ",",
                Key.OemPeriod => ".",
                Key.OemMinus => "-",
                Key.OemPlus => "+",
                Key.OemTilde => "~",
                //                 Key.Oem5 => "\\",
                Key.OemOpenBrackets => "[",
                Key.OemCloseBrackets => "]",
                Key.Space => "Space",
                Key.Enter => "Enter",
                Key.Escape => "Esc",
                Key.Back => "Backspace",
                Key.Delete => "Del",
                Key.Tab => "Tab",
                Key.PageUp => "PgUp",
                Key.PageDown => "PgDn",
                Key.Home => "Home",
                Key.End => "End",
                Key.Left => "←",
                Key.Right => "→",
                Key.Up => "↑",
                Key.Down => "↓",
                Key.D1 => "1",
                Key.D2 => "2",
                Key.D3 => "3",
                Key.D4 => "4",
                Key.D5 => "5",
                Key.D6 => "6",
                Key.D7 => "7",
                Key.D8 => "8",
                Key.D9 => "9",
                Key.D0 => "0",
                _ => key.ToString()
            };
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                ApplyTheme();
            });
        }

        private void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            // Update all controls
            if (shortcutsPanel != null)
            {
                // Rebuild display to apply new theme colors
                RefreshDisplay();
            }

            if (contentScrollViewer != null)
            {
                // Nothing specific needed - background managed by parent
            }

            this.InvalidateVisual();
        }

        protected override void OnDispose()
        {
            // Unsubscribe from theme changes
            themeManager.ThemeChanged -= OnThemeChanged;

            // Clean up if needed
            base.OnDispose();
        }
    }

    /// <summary>
    /// Grouped shortcuts for display
    /// </summary>
    internal class ShortcutGroup
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<KeyboardShortcut> Shortcuts { get; set; }
    }
}
