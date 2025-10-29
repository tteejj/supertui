using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Controls
{
    /// <summary>
    /// TUI-styled status bar for showing keyboard shortcuts and hints
    /// Looks like: [Enter]Create [Esc]Cancel [Tab]Next
    /// </summary>
    public class TUIStatusBar : Control
    {
        public static readonly DependencyProperty CommandsProperty =
            DependencyProperty.Register(nameof(Commands), typeof(List<TUICommand>), typeof(TUIStatusBar),
                new PropertyMetadata(null, OnCommandsChanged));

        public static readonly DependencyProperty StatusTextProperty =
            DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(TUIStatusBar),
                new PropertyMetadata(string.Empty, OnStatusTextChanged));

        public List<TUICommand> Commands
        {
            get => (List<TUICommand>)GetValue(CommandsProperty);
            set => SetValue(CommandsProperty, value);
        }

        public string StatusText
        {
            get => (string)GetValue(StatusTextProperty);
            set => SetValue(StatusTextProperty, value);
        }

        private StackPanel commandsPanel;
        private TextBlock statusTextBlock;
        private Border container;

        public TUIStatusBar()
        {
            Commands = new List<TUICommand>();
            BuildUI();
            ApplyTheme();

            ThemeChangedWeakEventManager.AddHandler(ThemeManager.Instance, OnThemeChanged);
        }

        private void BuildUI()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Commands
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Status text

            // Commands panel (left side)
            commandsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(8, 4, 8, 4)
            };
            Grid.SetColumn(commandsPanel, 0);
            grid.Children.Add(commandsPanel);

            // Status text (right side)
            statusTextBlock = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 4, 8, 4),
                FontStyle = FontStyles.Italic
            };
            statusTextBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(StatusText)) { Source = this });
            Grid.SetColumn(statusTextBlock, 1);
            grid.Children.Add(statusTextBlock);

            container = new Border
            {
                Child = grid,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            AddVisualChild(container);
        }

        private void RenderCommands()
        {
            commandsPanel.Children.Clear();

            if (Commands == null || Commands.Count == 0)
                return;

            var theme = ThemeManager.Instance.CurrentTheme;

            foreach (var cmd in Commands)
            {
                // Key label [Enter]
                var keyBlock = new TextBlock
                {
                    Text = $"[{cmd.Key}]",
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    Foreground = new SolidColorBrush(theme.Primary),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 2, 0)
                };
                commandsPanel.Children.Add(keyBlock);

                // Description
                var descBlock = new TextBlock
                {
                    Text = cmd.Description,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    Foreground = new SolidColorBrush(theme.Foreground),
                    Margin = new Thickness(0, 0, 12, 0)
                };
                commandsPanel.Children.Add(descBlock);
            }
        }

        private void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            container.Background = new SolidColorBrush(theme.Surface);
            container.BorderBrush = new SolidColorBrush(theme.Border);
            statusTextBlock.Foreground = new SolidColorBrush(theme.ForegroundSecondary);

            RenderCommands();
        }

        private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            ApplyTheme();
        }

        private static void OnCommandsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TUIStatusBar bar)
            {
                bar.RenderCommands();
            }
        }

        private static void OnStatusTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Handled by binding
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return container;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            container.Measure(constraint);
            return container.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            container.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }
    }

    /// <summary>
    /// Represents a keyboard command shown in the status bar
    /// </summary>
    public class TUICommand
    {
        public string Key { get; set; }
        public string Description { get; set; }

        public TUICommand(string key, string description)
        {
            Key = key;
            Description = description;
        }
    }
}
