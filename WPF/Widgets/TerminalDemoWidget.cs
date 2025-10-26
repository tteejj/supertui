using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Demo widget showcasing keyboard-driven terminal aesthetic
    /// Inspired by classic green-screen terminals, Fallout PipBoy, Matrix style
    /// </summary>
    public class TerminalDemoWidget : WidgetBase
    {
        private TextBlock displayText;
        private int selectedIndex = 0;
        private string[] menuItems = new[]
        {
            "SYSTEM STATUS",
            "TASK MANAGER",
            "FILE BROWSER",
            "NETWORK MONITOR",
            "SETTINGS",
            "HELP",
            "ABOUT",
            "EXIT"
        };

        private string[] statusItems = new[]
        {
            "CPU: 45% │ MEM: 62%",
            "DISK: 78% │ NET: OK",
            "TEMP: 42°C │ FAN: 2400RPM"
        };

        public TerminalDemoWidget()
        {
            WidgetName = "Terminal Demo";
            WidgetType = "TerminalDemo";
        }

        public override void Initialize()
        {
            BuildUI();
            this.Focusable = true;
            this.Focus();
        }

        private void BuildUI()
        {
            var mainGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 0, 0)) // Pure black background
            };

            // Main container with padding
            var container = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)), // Green border
                BorderThickness = new Thickness(2),
                Margin = new Thickness(10),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel();

            // Header
            var header = CreateHeader();
            stackPanel.Children.Add(header);

            // Separator line
            stackPanel.Children.Add(CreateSeparator());

            // Status section
            var statusSection = CreateStatusSection();
            stackPanel.Children.Add(statusSection);

            // Separator
            stackPanel.Children.Add(CreateSeparator());

            // Main menu
            var menuSection = CreateMenuSection();
            stackPanel.Children.Add(menuSection);

            // Separator
            stackPanel.Children.Add(CreateSeparator());

            // Help text
            var helpText = CreateHelpText();
            stackPanel.Children.Add(helpText);

            container.Child = stackPanel;
            mainGrid.Children.Add(container);

            this.Content = mainGrid;

            // Keyboard handling
            this.KeyDown += OnKeyDown;
        }

        private TextBlock CreateHeader()
        {
            return new TextBlock
            {
                Text = "╔════════════════════════════════════════╗\n" +
                       "║   SUPERTUI TERMINAL INTERFACE v1.0    ║\n" +
                       "╚════════════════════════════════════════╝",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)), // Green
                Margin = new Thickness(0, 0, 0, 10)
            };
        }

        private TextBlock CreateSeparator()
        {
            return new TextBlock
            {
                Text = "────────────────────────────────────────",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 0)),
                Margin = new Thickness(0, 5, 0, 5)
            };
        }

        private StackPanel CreateStatusSection()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };

            var title = new TextBlock
            {
                Text = "┌─ SYSTEM STATUS ─┐",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                Margin = new Thickness(0, 0, 0, 3)
            };
            panel.Children.Add(title);

            foreach (var status in statusItems)
            {
                var statusLine = new TextBlock
                {
                    Text = "│ " + status,
                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                    Margin = new Thickness(0, 1, 0, 1)
                };
                panel.Children.Add(statusLine);
            }

            var bottom = new TextBlock
            {
                Text = "└──────────────────┘",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            panel.Children.Add(bottom);

            return panel;
        }

        private StackPanel CreateMenuSection()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };

            var title = new TextBlock
            {
                Text = "┌─ MAIN MENU (USE ↑↓ OR TAB) ─┐",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(title);

            // Store menu items for updates
            displayText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                LineHeight = 18
            };

            panel.Children.Add(displayText);

            var bottom = new TextBlock
            {
                Text = "└────────────────────────────────┘",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                Margin = new Thickness(0, 5, 0, 0)
            };
            panel.Children.Add(bottom);

            UpdateMenuDisplay();

            return panel;
        }

        private TextBlock CreateHelpText()
        {
            return new TextBlock
            {
                Text = "│ KEYBOARD: [↑][↓][TAB] NAVIGATE │ [ENTER] SELECT │ [ESC] BACK │",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 180, 0)),
                Margin = new Thickness(0, 10, 0, 0),
                TextAlignment = TextAlignment.Center
            };
        }

        private void UpdateMenuDisplay()
        {
            if (displayText == null) return;

            var text = "";
            for (int i = 0; i < menuItems.Length; i++)
            {
                var prefix = i == selectedIndex ? "► " : "  ";
                var highlight = i == selectedIndex ? "[" : " ";
                var highlightEnd = i == selectedIndex ? "]" : " ";

                text += $"│ {highlight}{i + 1}{highlightEnd} {prefix}{menuItems[i],-25}│\n";
            }

            displayText.Text = text;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            bool handled = true;

            switch (e.Key)
            {
                // Arrow keys for navigation
                case Key.Up:
                    selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : menuItems.Length - 1;
                    break;

                case Key.Down:
                    selectedIndex = (selectedIndex + 1) % menuItems.Length;
                    break;

                // Tab for forward navigation
                case Key.Tab:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        // Shift+Tab = previous
                        selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : menuItems.Length - 1;
                    }
                    else
                    {
                        // Tab = next
                        selectedIndex = (selectedIndex + 1) % menuItems.Length;
                    }
                    break;

                // Enter to select
                case Key.Enter:
                    ExecuteSelection();
                    break;

                // Escape to go back (demo only - shows message)
                case Key.Escape:
                    MessageBox.Show("ESC pressed - would return to previous screen", "Terminal Demo");
                    break;

                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                UpdateMenuDisplay();
                e.Handled = true;
            }
        }

        private void ExecuteSelection()
        {
            var selected = menuItems[selectedIndex];
            MessageBox.Show(
                $"You selected: {selected}\n\nThis is a demo - actual functionality would be implemented here.",
                "Terminal Demo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public override Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["SelectedIndex"] = selectedIndex
            };
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state.TryGetValue("SelectedIndex", out var idx) && idx is int index)
            {
                selectedIndex = index;
                UpdateMenuDisplay();
            }
        }

        protected override void OnDispose()
        {
            // Clean up event handlers
            this.KeyDown -= OnKeyDown;
        }
    }
}
