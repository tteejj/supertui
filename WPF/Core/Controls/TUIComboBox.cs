using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Controls
{
    /// <summary>
    /// TUI-styled combo box / dropdown control
    /// Looks like: Status: [ Todo ▼ ]
    /// Click to show dropdown list with TUI styling
    /// </summary>
    public class TUIComboBox : Control
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(TUIComboBox),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(TUIComboBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(TUIComboBox),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(TUIComboBox),
                new PropertyMetadata(string.Empty));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        private ComboBox comboBox;
        private TextBlock labelBlock;
        private TextBlock prefixBlock;
        private TextBlock suffixBlock;
        private Border container;

        public TUIComboBox()
        {
            BuildUI();
            ApplyTheme();

            ThemeChangedWeakEventManager.AddHandler(ThemeManager.Instance, OnThemeChanged);
        }

        private void BuildUI()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Label
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Prefix [
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // ComboBox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Suffix ]

            // Label (optional)
            labelBlock = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(0)
            };
            labelBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Label)) { Source = this });
            labelBlock.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding(nameof(Label))
            {
                Source = this,
                Converter = new EmptyStringToVisibilityConverter()
            });
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            // Prefix
            prefixBlock = new TextBlock
            {
                Text = "[ ",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };
            Grid.SetColumn(prefixBlock, 1);
            grid.Children.Add(prefixBlock);

            // ComboBox (styled to be borderless)
            comboBox = new ComboBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2, 0, 2, 0),
                MinWidth = 100,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // Bind properties
            comboBox.SetBinding(ItemsControl.ItemsSourceProperty, new System.Windows.Data.Binding(nameof(ItemsSource)) { Source = this });
            comboBox.SetBinding(ComboBox.SelectedItemProperty, new System.Windows.Data.Binding(nameof(SelectedItem))
            {
                Source = this,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            comboBox.SetBinding(ComboBox.SelectedIndexProperty, new System.Windows.Data.Binding(nameof(SelectedIndex))
            {
                Source = this,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });

            Grid.SetColumn(comboBox, 2);
            grid.Children.Add(comboBox);

            // Suffix (with dropdown arrow)
            suffixBlock = new TextBlock
            {
                Text = " ▼ ]",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };
            Grid.SetColumn(suffixBlock, 3);
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

            // Label in primary color
            labelBlock.Foreground = new SolidColorBrush(theme.Primary);

            // Prefix/Suffix in dim color
            prefixBlock.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            suffixBlock.Foreground = new SolidColorBrush(theme.ForegroundSecondary);

            // ComboBox styling
            comboBox.Foreground = new SolidColorBrush(theme.Foreground);
            comboBox.Background = new SolidColorBrush(theme.Surface);

            // Style the dropdown popup
            StyleDropdown();

            container.Background = new SolidColorBrush(theme.Surface);
        }

        private void StyleDropdown()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            // Style for items
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(theme.Foreground)));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));

            // Hover
            var hoverTrigger = new Trigger { Property = ComboBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(theme.Selection)));
            itemStyle.Triggers.Add(hoverTrigger);

            // Selected
            var selectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(theme.Selection)));
            itemStyle.Triggers.Add(selectedTrigger);

            comboBox.ItemContainerStyle = itemStyle;
        }

        private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            ApplyTheme();
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Update visual if needed
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

    // Converter to hide label when empty
    internal class EmptyStringToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
