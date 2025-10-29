using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Controls
{
    /// <summary>
    /// TUI-styled text input control
    /// Looks like: [ input text here          ]
    /// Full WPF functionality (cursor, selection, etc.) with terminal aesthetics
    /// </summary>
    public class TUITextInput : Control
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(TUITextInput),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(TUITextInput),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty PrefixProperty =
            DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(TUITextInput),
                new PropertyMetadata("[ "));

        public static readonly DependencyProperty SuffixProperty =
            DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(TUITextInput),
                new PropertyMetadata(" ]"));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public string Prefix
        {
            get => (string)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public string Suffix
        {
            get => (string)GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        private TextBox textBox;
        private TextBlock prefixBlock;
        private TextBlock suffixBlock;
        private Border container;

        public TUITextInput()
        {
            BuildUI();
            ApplyTheme();

            ThemeChangedWeakEventManager.AddHandler(ThemeManager.Instance, OnThemeChanged);
        }

        private void BuildUI()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Prefix
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Input
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Suffix

            // Prefix
            prefixBlock = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };
            prefixBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Prefix)) { Source = this });
            Grid.SetColumn(prefixBlock, 0);
            grid.Children.Add(prefixBlock);

            // TextBox (actual input)
            textBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2, 0, 2, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            textBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(Text))
            {
                Source = this,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
            Grid.SetColumn(textBox, 1);
            grid.Children.Add(textBox);

            // Suffix
            suffixBlock = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };
            suffixBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Suffix)) { Source = this });
            Grid.SetColumn(suffixBlock, 2);
            grid.Children.Add(suffixBlock);

            container = new Border
            {
                Child = grid,
                Padding = new Thickness(4, 2, 4, 2)
            };

            AddVisualChild(container);
        }

        private void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            // Prefix/Suffix in dim color
            prefixBlock.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            suffixBlock.Foreground = new SolidColorBrush(theme.ForegroundSecondary);

            // TextBox styling
            textBox.Foreground = new SolidColorBrush(theme.Foreground);
            textBox.Background = new SolidColorBrush(theme.Surface);
            textBox.CaretBrush = new SolidColorBrush(theme.Primary);
            textBox.SelectionBrush = new SolidColorBrush(Color.FromArgb(128, theme.Primary.R, theme.Primary.G, theme.Primary.B));

            container.Background = new SolidColorBrush(theme.Surface);
        }

        private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            ApplyTheme();
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

        // Focus handling
        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            textBox.Focus();
        }
    }
}
