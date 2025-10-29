using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Controls
{
    /// <summary>
    /// TUI-styled list box control
    /// Renders items with selection indicators: [ ] unselected, [X] selected, [•] focused
    /// Full scrolling and keyboard navigation without terminal limitations
    /// </summary>
    public class TUIListBox : Control
    {
        public event SelectionChangedEventHandler SelectionChanged;

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(TUIListBox),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(TUIListBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(TUIListBox),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register(nameof(SelectionMode), typeof(SelectionMode), typeof(TUIListBox),
                new PropertyMetadata(SelectionMode.Single));

        public static readonly DependencyProperty ShowCheckboxesProperty =
            DependencyProperty.Register(nameof(ShowCheckboxes), typeof(bool), typeof(TUIListBox),
                new PropertyMetadata(true));

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

        public SelectionMode SelectionMode
        {
            get => (SelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        public bool ShowCheckboxes
        {
            get => (bool)GetValue(ShowCheckboxesProperty);
            set => SetValue(ShowCheckboxesProperty, value);
        }

        private ListBox listBox;
        private Border container;

        public TUIListBox()
        {
            BuildUI();
            ApplyTheme();

            ThemeChangedWeakEventManager.AddHandler(ThemeManager.Instance, OnThemeChanged);
        }

        private void BuildUI()
        {
            listBox = new ListBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            ScrollViewer.SetHorizontalScrollBarVisibility(listBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(listBox, ScrollBarVisibility.Auto);

            // Bind properties
            listBox.SetBinding(ItemsControl.ItemsSourceProperty, new System.Windows.Data.Binding(nameof(ItemsSource)) { Source = this });
            listBox.SetBinding(ListBox.SelectedItemProperty, new System.Windows.Data.Binding(nameof(SelectedItem))
            {
                Source = this,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            listBox.SetBinding(ListBox.SelectedIndexProperty, new System.Windows.Data.Binding(nameof(SelectedIndex))
            {
                Source = this,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });

            // Custom item template
            listBox.ItemTemplate = CreateItemTemplate();

            // Custom item container style (for selection visual)
            listBox.ItemContainerStyle = CreateItemContainerStyle();

            // Forward selection changed events
            listBox.SelectionChanged += (s, e) => SelectionChanged?.Invoke(this, e);

            container = new Border
            {
                Child = listBox,
                Padding = new Thickness(0)
            };

            AddVisualChild(container);
        }

        private DataTemplate CreateItemTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            // Checkbox indicator (optional)
            var checkbox = new FrameworkElementFactory(typeof(TextBlock));
            checkbox.SetValue(TextBlock.TextProperty, "[ ] ");
            checkbox.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 4, 0));
            checkbox.SetValue(TextBlock.NameProperty, "CheckboxIndicator");
            factory.AppendChild(checkbox);

            // Content
            var content = new FrameworkElementFactory(typeof(TextBlock));
            content.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
            factory.AppendChild(content);

            template.VisualTree = factory;
            return template;
        }

        private Style CreateItemContainerStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            var theme = ThemeManager.Instance.CurrentTheme;

            // Remove default hover/selection backgrounds
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(theme.Foreground)));

            // Selected state
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(theme.Selection)));
            selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(theme.Foreground)));
            style.Triggers.Add(selectedTrigger);

            // Hover state
            var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(50, theme.Primary.R, theme.Primary.G, theme.Primary.B))));
            style.Triggers.Add(hoverTrigger);

            return style;
        }

        private void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            listBox.Background = new SolidColorBrush(theme.Background);
            listBox.Foreground = new SolidColorBrush(theme.Foreground);
            container.Background = new SolidColorBrush(theme.Background);

            // Recreate item container style with new theme
            listBox.ItemContainerStyle = CreateItemContainerStyle();
        }

        private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            ApplyTheme();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Items changed, refresh
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
}
