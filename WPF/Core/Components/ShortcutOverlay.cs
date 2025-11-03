using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Full-screen overlay that displays keyboard shortcuts with context awareness
    /// Shows global shortcuts + current widget-specific shortcuts
    /// </summary>
    public class ShortcutOverlay : Border
    {
        private readonly IThemeManager themeManager;
        private readonly IShortcutManager shortcutManager;
        private readonly ILogger logger;

        private Grid overlayGrid;
        private Border contentBorder;
        private TextBlock titleText;
        private StackPanel shortcutPanel;
        private TextBlock footerText;
        private string currentWidgetType;

        public event Action CloseRequested;

        public ShortcutOverlay(IThemeManager themeManager, IShortcutManager shortcutManager, ILogger logger)
        {
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.shortcutManager = shortcutManager ?? throw new ArgumentNullException(nameof(shortcutManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            BuildUI();
            this.Visibility = Visibility.Collapsed;
            this.IsVisibleChanged += OnVisibilityChanged;
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            // Main overlay grid (fills entire window)
            overlayGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)) // Semi-transparent black
            };

            // Center content border
            contentBorder = new Border
            {
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                MaxWidth = 900,
                MaxHeight = 700,
                Margin = new Thickness(50),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = theme.Primary,
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.8
                }
            };

            var mainPanel = new StackPanel
            {
                Margin = new Thickness(30)
            };

            // Title
            titleText = new TextBlock
            {
                Text = "⌨  KEYBOARD SHORTCUTS",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 0, 0, 20),
                TextAlignment = TextAlignment.Center
            };

            // Scrollable shortcut panel
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 500
            };

            shortcutPanel = new StackPanel
            {
                Margin = new Thickness(0)
            };

            scrollViewer.Content = shortcutPanel;

            // Footer
            footerText = new TextBlock
            {
                Text = "Press ESC or ? to close",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 20, 0, 0),
                TextAlignment = TextAlignment.Center,
                FontStyle = FontStyles.Italic
            };

            mainPanel.Children.Add(titleText);
            mainPanel.Children.Add(scrollViewer);
            mainPanel.Children.Add(footerText);

            contentBorder.Child = mainPanel;
            overlayGrid.Children.Add(contentBorder);

            this.Child = overlayGrid;

            // Handle clicks on overlay background to close
            overlayGrid.MouseDown += (s, e) =>
            {
                if (e.OriginalSource == overlayGrid)
                {
                    Hide();
                    e.Handled = true;
                }
            };

            // Handle keyboard for closing
            this.KeyDown += OnKeyDown;
            this.Focusable = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.OemQuestion || (e.Key == Key.Oem2 && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift))
            {
                Hide();
                e.Handled = true;
            }
        }

        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
            {
                System.Windows.Input.Keyboard.Focus(this);
                System.Windows.Input.Keyboard.Focus(this);
            }
        }

        /// <summary>
        /// Show the overlay with shortcuts for the specified widget type
        /// </summary>
        public void Show(string widgetType = null)
        {
            currentWidgetType = widgetType;
            UpdateShortcuts();
            this.Visibility = Visibility.Visible;
            logger.Info("ShortcutOverlay", $"Showing shortcuts for widget: {widgetType ?? "Global"}");
        }

        /// <summary>
        /// Hide the overlay
        /// </summary>
        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
            CloseRequested?.Invoke();
            logger.Info("ShortcutOverlay", "Overlay hidden");
        }

        private void UpdateShortcuts()
        {
            shortcutPanel.Children.Clear();

            var theme = themeManager.CurrentTheme;
            var allShortcuts = CollectShortcuts();

            // Group shortcuts by category
            var grouped = allShortcuts
                .OrderBy(s => s.Priority)
                .ThenBy(s => s.Category)
                .GroupBy(s => s.Category);

            foreach (var group in grouped)
            {
                // Category header
                var categoryHeader = new TextBlock
                {
                    Text = $"──── {group.Key.ToUpper()} ────",
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(theme.Info),
                    Margin = new Thickness(0, 15, 0, 10),
                    TextAlignment = TextAlignment.Left
                };

                if (shortcutPanel.Children.Count > 0) // Not the first category
                {
                    shortcutPanel.Children.Add(categoryHeader);
                }
                else
                {
                    categoryHeader.Margin = new Thickness(0, 0, 0, 10);
                    shortcutPanel.Children.Add(categoryHeader);
                }

                // Shortcuts in this category
                foreach (var shortcut in group)
                {
                    var itemGrid = new Grid
                    {
                        Margin = new Thickness(10, 3, 10, 3)
                    };

                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var keysText = new TextBlock
                    {
                        Text = shortcut.Keys,
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(theme.SyntaxKeyword)
                    };

                    var descText = new TextBlock
                    {
                        Text = shortcut.Description,
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                        FontSize = 12,
                        Foreground = new SolidColorBrush(theme.Foreground),
                        TextWrapping = TextWrapping.Wrap
                    };

                    Grid.SetColumn(keysText, 0);
                    Grid.SetColumn(descText, 1);

                    itemGrid.Children.Add(keysText);
                    itemGrid.Children.Add(descText);

                    shortcutPanel.Children.Add(itemGrid);
                }
            }

            // Update footer with context
            if (!string.IsNullOrEmpty(currentWidgetType))
            {
                footerText.Text = $"Shortcuts for: {currentWidgetType}  •  Press ESC or ? to close";
            }
            else
            {
                footerText.Text = "Global Shortcuts  •  Press ESC or ? to close";
            }
        }

        private List<ShortcutInfo> CollectShortcuts()
        {
            var shortcuts = new List<ShortcutInfo>();

            // Global application shortcuts (priority 0)
            shortcuts.Add(new ShortcutInfo { Category = "Global", Keys = "?", Description = "Show/hide this help overlay", Priority = 0 });
            shortcuts.Add(new ShortcutInfo { Category = "Global", Keys = "Ctrl+Q", Description = "Quit application", Priority = 0 });
            shortcuts.Add(new ShortcutInfo { Category = "Global", Keys = "Ctrl+,", Description = "Open settings", Priority = 0 });

            // Workspace shortcuts (priority 1)
            shortcuts.Add(new ShortcutInfo { Category = "Workspace", Keys = "Ctrl+Tab", Description = "Next workspace", Priority = 1 });
            shortcuts.Add(new ShortcutInfo { Category = "Workspace", Keys = "Ctrl+Shift+Tab", Description = "Previous workspace", Priority = 1 });
            shortcuts.Add(new ShortcutInfo { Category = "Workspace", Keys = "Ctrl+1-9", Description = "Switch to workspace 1-9", Priority = 1 });

            // Widget navigation shortcuts (priority 2)
            shortcuts.Add(new ShortcutInfo { Category = "Navigation", Keys = "Tab", Description = "Focus next widget", Priority = 2 });
            shortcuts.Add(new ShortcutInfo { Category = "Navigation", Keys = "Shift+Tab", Description = "Focus previous widget", Priority = 2 });
            shortcuts.Add(new ShortcutInfo { Category = "Navigation", Keys = "Ctrl+N", Description = "Add widget to slot", Priority = 2 });

            // Get shortcuts from ShortcutManager
            var registeredShortcuts = shortcutManager.GetGlobalShortcuts();
            foreach (var shortcut in registeredShortcuts)
            {
                shortcuts.Add(new ShortcutInfo
                {
                    Category = InferCategory(shortcut.Description),
                    Keys = FormatKeyCombo(shortcut.Key, shortcut.Modifiers),
                    Description = shortcut.Description,
                    Priority = 3
                });
            }

            // Widget-specific shortcuts (priority 4)
            if (!string.IsNullOrEmpty(currentWidgetType))
            {
                var widgetShortcuts = GetWidgetSpecificShortcuts(currentWidgetType);
                shortcuts.AddRange(widgetShortcuts);
            }

            return shortcuts;
        }

        private List<ShortcutInfo> GetWidgetSpecificShortcuts(string widgetType)
        {
            var shortcuts = new List<ShortcutInfo>();

            switch (widgetType)
            {
                case "TaskManagement":
                    shortcuts.Add(new ShortcutInfo { Category = "Task Management", Keys = "Enter", Description = "Edit selected task", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Task Management", Keys = "Delete", Description = "Delete selected task", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Task Management", Keys = "Ctrl+N", Description = "Create new task", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Task Management", Keys = "F2", Description = "Rename task", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Task Management", Keys = "Space", Description = "Toggle task completion", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Task Management", Keys = "Up/Down", Description = "Navigate task list", Priority = 4 });
                    break;

                case "KanbanBoard":
                    shortcuts.Add(new ShortcutInfo { Category = "Kanban Board", Keys = "Enter", Description = "Edit selected task", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Kanban Board", Keys = "E", Description = "Open task in Task Management", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Kanban Board", Keys = "Left/Right", Description = "Move between columns", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Kanban Board", Keys = "Up/Down", Description = "Navigate tasks in column", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Kanban Board", Keys = "Ctrl+Left/Right", Description = "Move task to different status", Priority = 4 });
                    break;

                case "Agenda":
                    shortcuts.Add(new ShortcutInfo { Category = "Agenda", Keys = "Enter", Description = "Edit selected task", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Agenda", Keys = "E", Description = "Open task in Task Management", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Agenda", Keys = "Up/Down", Description = "Navigate task list", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Agenda", Keys = "Space", Description = "Toggle task completion", Priority = 4 });
                    break;

                case "FileExplorer":
                    shortcuts.Add(new ShortcutInfo { Category = "File Explorer", Keys = "Enter", Description = "Open selected file/folder", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "File Explorer", Keys = "Backspace", Description = "Go to parent directory", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "File Explorer", Keys = "Delete", Description = "Delete selected item", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "File Explorer", Keys = "F5", Description = "Refresh directory", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "File Explorer", Keys = "Up/Down", Description = "Navigate file list", Priority = 4 });
                    break;

                case "CommandPalette":
                    shortcuts.Add(new ShortcutInfo { Category = "Command Palette", Keys = "Enter", Description = "Execute selected command", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Command Palette", Keys = "Up/Down", Description = "Navigate command list", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Command Palette", Keys = "Escape", Description = "Clear search / Close palette", Priority = 4 });
                    break;

                case "Settings":
                    shortcuts.Add(new ShortcutInfo { Category = "Settings", Keys = "Ctrl+S", Description = "Save settings", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Settings", Keys = "F5", Description = "Reload settings from file", Priority = 4 });
                    break;

                case "GitStatus":
                    shortcuts.Add(new ShortcutInfo { Category = "Git Status", Keys = "F5", Description = "Refresh git status", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Git Status", Keys = "Up/Down", Description = "Navigate file list", Priority = 4 });
                    break;

                case "Todo":
                    shortcuts.Add(new ShortcutInfo { Category = "Todo", Keys = "Enter", Description = "Edit selected todo", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Todo", Keys = "Space", Description = "Toggle todo completion", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Todo", Keys = "Delete", Description = "Delete selected todo", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Todo", Keys = "Ctrl+N", Description = "Create new todo", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Todo", Keys = "Up/Down", Description = "Navigate todo list", Priority = 4 });
                    break;

                case "Notes":
                    shortcuts.Add(new ShortcutInfo { Category = "Notes", Keys = "Ctrl+S", Description = "Save current note", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Notes", Keys = "Ctrl+N", Description = "Create new note", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Notes", Keys = "Delete", Description = "Delete selected note", Priority = 4 });
                    shortcuts.Add(new ShortcutInfo { Category = "Notes", Keys = "F2", Description = "Rename note", Priority = 4 });
                    break;
            }

            return shortcuts;
        }

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
                return "Global";
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

        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            if (contentBorder != null)
            {
                contentBorder.Background = new SolidColorBrush(theme.Background);
                contentBorder.BorderBrush = new SolidColorBrush(theme.Primary);

                if (contentBorder.Effect is DropShadowEffect effect)
                {
                    effect.Color = theme.Primary;
                }
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.Primary);
            }

            if (footerText != null)
            {
                footerText.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            }

            // Refresh shortcuts to update colors
            UpdateShortcuts();
        }

        private class ShortcutInfo
        {
            public string Category { get; set; }
            public string Keys { get; set; }
            public string Description { get; set; }
            public int Priority { get; set; }
        }
    }
}
