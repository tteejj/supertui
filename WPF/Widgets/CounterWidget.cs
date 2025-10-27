using System;
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
    /// Simple counter widget - demonstrates independent state management
    /// Each instance maintains its own count, even when switching workspaces
    /// </summary>
    public class CounterWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;

        private StandardWidgetFrame frame;
        private Border containerBorder;
        private TextBlock countText;
        private TextBlock instructionText;

        private int count = 0;
        public int Count
        {
            get => count;
            set
            {
                count = value;
                OnPropertyChanged(nameof(Count));
                UpdateDisplay();
            }
        }

        public CounterWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            WidgetType = "Counter";
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            // Create standard frame
            frame = new StandardWidgetFrame(themeManager)
            {
                Title = "COUNTER"
            };
            frame.SetStandardShortcuts("↑/↓: Increment/Decrement", "R: Reset", "?: Help");

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Padding = new Thickness(20)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Count display
            countText = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.SyntaxKeyword),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Instructions
            instructionText = new TextBlock
            {
                Text = "Press Up/Down arrows",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(countText);
            stackPanel.Children.Add(instructionText);

            containerBorder.Child = stackPanel;
            frame.Content = containerBorder;
            this.Content = frame;
        }

        public override void Initialize()
        {
            Count = 0;
        }

        public override void OnWidgetKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    Count++;
                    e.Handled = true;
                    break;

                case Key.Down:
                    Count--;
                    e.Handled = true;
                    break;

                case Key.R:
                    Count = 0;
                    e.Handled = true;
                    break;
            }
        }

        public override void OnWidgetFocusReceived()
        {
            var theme = themeManager.CurrentTheme;
            instructionText.Foreground = new SolidColorBrush(theme.Focus);
        }

        public override void OnWidgetFocusLost()
        {
            var theme = themeManager.CurrentTheme;
            instructionText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
        }

        private void UpdateDisplay()
        {
            countText.Text = Count.ToString();
        }

        public override System.Collections.Generic.Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            state["Count"] = Count;
            return state;
        }

        public override void RestoreState(System.Collections.Generic.Dictionary<string, object> state)
        {
            if (state.ContainsKey("Count"))
            {
                // Handle JsonElement from deserialized state files
                try
                {
                    Count = Convert.ToInt32(state["Count"]);
                }
                catch (Exception ex)
                {
                    logger.Warning("CounterWidget", $"Failed to restore Count state: {ex.Message}");
                    Count = 0; // Reset to default
                }
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

            if (frame != null)
            {
                frame.ApplyTheme();
            }

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
            }

            if (countText != null)
            {
                countText.Foreground = new SolidColorBrush(theme.SyntaxKeyword);
            }

            if (instructionText != null)
            {
                // Update based on focus state
                instructionText.Foreground = new SolidColorBrush(
                    HasFocus ? theme.Focus : theme.ForegroundDisabled);
            }
        }
    }
}
