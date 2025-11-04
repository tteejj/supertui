// SettingsPane.cs - Configuration and theme selector
// Keyboard-driven settings management

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Settings pane - Theme selector and configuration options
    /// </summary>
    public class SettingsPane : PaneBase
    {
        // Services
        private readonly IConfigurationManager configManager;

        // UI Components
        private ListBox themeListBox;
        private TextBlock statusBar;
        private TextBlock previewText;

        // Theme colors
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush surfaceBrush;
        private SolidColorBrush dimBrush;

        // State
        private string[] availableThemes = { "Dark", "Light", "Solarized", "Matrix", "Cyberpunk", "Nord", "Dracula" };

        public SettingsPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager configManager)
            : base(logger, themeManager, projectContext)
        {
            this.configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            PaneName = "Settings";
            PaneIcon = "⚙️";
        }

        public override void Initialize()
        {
            base.Initialize();
            CacheThemeColors();
            RegisterPaneShortcuts();

            // Focus theme list on load
            this.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.Input.Keyboard.Focus(themeListBox);
                if (themeListBox.Items.Count > 0)
                {
                    // Select current theme
                    var currentTheme = themeManager.CurrentTheme.Name;
                    for (int i = 0; i < themeListBox.Items.Count; i++)
                    {
                        if (themeListBox.Items[i] is Border border && border.Tag as string == currentTheme)
                        {
                            themeListBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override UIElement BuildContent()
        {
            CacheThemeColors();

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Theme list
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Preview
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status

            // Header
            var header = new TextBlock
            {
                Text = "⚙️ Settings",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Padding = new Thickness(16, 8, 16, 12),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Theme list
            var themeSection = new StackPanel { Margin = new Thickness(16, 0, 16, 16) };

            var themeLabel = new TextBlock
            {
                Text = "Theme:",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = fgBrush,
                Margin = new Thickness(0, 0, 0, 8)
            };
            themeSection.Children.Add(themeLabel);

            themeListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 13,
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderBrush = new SolidColorBrush(themeManager.CurrentTheme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Height = 280
            };
            themeListBox.KeyDown += ThemeListBox_KeyDown;
            themeListBox.SelectionChanged += ThemeListBox_SelectionChanged;

            foreach (var themeName in availableThemes)
            {
                var item = CreateThemeItem(themeName);
                themeListBox.Items.Add(item);
            }

            themeSection.Children.Add(themeListBox);
            Grid.SetRow(themeSection, 1);
            mainGrid.Children.Add(themeSection);

            // Preview
            previewText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = dimBrush,
                Background = surfaceBrush,
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(16, 0, 16, 8),
                TextWrapping = TextWrapping.Wrap,
                Text = $"Current theme: {themeManager.CurrentTheme.Name}\nPress Enter to apply selected theme"
            };
            Grid.SetRow(previewText, 2);
            mainGrid.Children.Add(previewText);

            // Status bar
            statusBar = new TextBlock
            {
                Text = "↑↓:Select theme | Enter:Apply | Esc:Close",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = fgBrush,
                Background = surfaceBrush,
                Padding = new Thickness(16, 8, 16, 8)
            };
            Grid.SetRow(statusBar, 3);
            mainGrid.Children.Add(statusBar);

            return mainGrid;
        }

        private Border CreateThemeItem(string themeName)
        {
            var isCurrentTheme = themeName == themeManager.CurrentTheme.Name;

            var textBlock = new TextBlock
            {
                Text = isCurrentTheme ? $"● {themeName} (current)" : $"  {themeName}",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 13,
                Foreground = isCurrentTheme ? accentBrush : fgBrush,
                Padding = new Thickness(12, 8, 12, 8)
            };

            var border = new Border
            {
                Child = textBlock,
                Background = surfaceBrush,
                BorderBrush = new SolidColorBrush(themeManager.CurrentTheme.Border),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Tag = themeName
            };

            return border;
        }

        private void CacheThemeColors()
        {
            var theme = themeManager.CurrentTheme;
            fgBrush = new SolidColorBrush(theme.Foreground);
            accentBrush = new SolidColorBrush(theme.Primary);
            surfaceBrush = new SolidColorBrush(theme.Surface);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
        }

        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;
            shortcuts.RegisterForPane(PaneName, Key.Enter, ModifierKeys.None, () => ApplySelectedTheme(), "Apply selected theme");
        }

        private void ThemeListBox_KeyDown(object sender, KeyEventArgs e)
        {
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
                e.Handled = true;
        }

        private void ThemeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (themeListBox.SelectedItem is Border border && border.Tag is string themeName)
            {
                previewText.Text = $"Selected: {themeName}\nPress Enter to apply this theme";
            }
        }

        private void ApplySelectedTheme()
        {
            if (themeListBox.SelectedItem is Border border && border.Tag is string themeName)
            {
                try
                {
                    themeManager.ApplyTheme(themeName);
                    ShowStatus($"✓ Applied theme: {themeName}", false);
                    logger.Log(LogLevel.Info, "Settings", $"Changed theme to: {themeName}");

                    // Refresh the list to show new current theme
                    this.Dispatcher.InvokeAsync(() =>
                    {
                        int selectedIndex = themeListBox.SelectedIndex;
                        themeListBox.Items.Clear();
                        foreach (var theme in availableThemes)
                        {
                            themeListBox.Items.Add(CreateThemeItem(theme));
                        }
                        if (selectedIndex >= 0 && selectedIndex < themeListBox.Items.Count)
                        {
                            themeListBox.SelectedIndex = selectedIndex;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
                catch (Exception ex)
                {
                    ShowStatus($"✗ Failed to apply theme: {ex.Message}", true);
                    logger.Log(LogLevel.Error, "Settings", $"Failed to apply theme '{themeName}': {ex.Message}");
                }
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            this.Dispatcher.Invoke(() =>
            {
                statusBar.Text = message;
                statusBar.Foreground = isError ? new SolidColorBrush(themeManager.CurrentTheme.Error) : fgBrush;

                // Reset after 3 seconds
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    statusBar.Text = "↑↓:Select theme | Enter:Apply | Esc:Close";
                    statusBar.Foreground = fgBrush;
                };
                timer.Start();
            });
        }

        protected override void OnDispose()
        {
            // Unsubscribe from selection changed
            if (themeListBox != null)
            {
                themeListBox.SelectionChanged -= ThemeListBox_SelectionChanged;
                themeListBox.KeyDown -= ThemeListBox_KeyDown;
            }

            base.OnDispose();
        }
    }
}
