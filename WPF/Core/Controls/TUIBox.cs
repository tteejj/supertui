using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Controls
{
    /// <summary>
    /// TUI-styled container with box-drawing character borders
    /// Provides the visual aesthetic of terminal UI boxes without the limitations
    /// </summary>
    public class TUIBox : ContentControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(TUIBox),
                new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

        public static readonly DependencyProperty BorderStyleProperty =
            DependencyProperty.Register(nameof(BorderStyle), typeof(TUIBorderStyle), typeof(TUIBox),
                new PropertyMetadata(TUIBorderStyle.Single, OnVisualPropertyChanged));

        public static readonly DependencyProperty ShowTitleProperty =
            DependencyProperty.Register(nameof(ShowTitle), typeof(bool), typeof(TUIBox),
                new PropertyMetadata(true, OnVisualPropertyChanged));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public TUIBorderStyle BorderStyle
        {
            get => (TUIBorderStyle)GetValue(BorderStyleProperty);
            set => SetValue(BorderStyleProperty, value);
        }

        public bool ShowTitle
        {
            get => (bool)GetValue(ShowTitleProperty);
            set => SetValue(ShowTitleProperty, value);
        }

        private Border outerBorder;
        private Grid mainGrid;
        private TextBlock topBorder;
        private TextBlock leftBorder;
        private TextBlock rightBorder;
        private TextBlock bottomBorder;
        private ContentPresenter contentPresenter;

        public TUIBox()
        {
            BuildUI();
            ApplyTheme();

            // Subscribe to theme changes
            ThemeChangedWeakEventManager.AddHandler(ThemeManager.Instance, OnThemeChanged);
        }

        private void BuildUI()
        {
            // Main grid for border + content
            mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top border
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom border

            // Top border with optional title
            topBorder = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Height = 20,
                Padding = new Thickness(0)
            };
            Grid.SetRow(topBorder, 0);
            mainGrid.Children.Add(topBorder);

            // Content row with left/right borders
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Left border
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Content
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Right border

            leftBorder = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Width = 12,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(leftBorder, 0);
            contentGrid.Children.Add(leftBorder);

            contentPresenter = new ContentPresenter
            {
                Margin = new Thickness(8, 4, 8, 4)
            };
            Grid.SetColumn(contentPresenter, 1);
            contentGrid.Children.Add(contentPresenter);

            rightBorder = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Width = 12,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(rightBorder, 2);
            contentGrid.Children.Add(rightBorder);

            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // Bottom border
            bottomBorder = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Height = 20,
                Padding = new Thickness(0)
            };
            Grid.SetRow(bottomBorder, 2);
            mainGrid.Children.Add(bottomBorder);

            // Outer container
            outerBorder = new Border
            {
                Child = mainGrid
            };

            AddVisualChild(outerBorder);
        }

        private void RenderBorders()
        {
            var chars = GetBorderChars(BorderStyle);
            var theme = ThemeManager.Instance.CurrentTheme;
            var borderBrush = new SolidColorBrush(theme.Border);
            var titleBrush = new SolidColorBrush(theme.Primary);

            // Top border
            if (ShowTitle && !string.IsNullOrEmpty(Title))
            {
                var titleText = $" {Title} ";
                var titleWidth = titleText.Length;
                var leftPad = 2;
                var remainingWidth = Math.Max(0, 80 - leftPad - titleWidth - 2); // Assume ~80 char width, will expand as needed

                topBorder.Inlines.Clear();
                topBorder.Inlines.Add(new System.Windows.Documents.Run(chars.TopLeft.ToString()) { Foreground = borderBrush });
                topBorder.Inlines.Add(new System.Windows.Documents.Run(new string(chars.Horizontal, leftPad)) { Foreground = borderBrush });
                topBorder.Inlines.Add(new System.Windows.Documents.Run(titleText) { Foreground = titleBrush, FontWeight = FontWeights.Bold });
                topBorder.Inlines.Add(new System.Windows.Documents.Run(new string(chars.Horizontal, Math.Max(1, remainingWidth))) { Foreground = borderBrush });
                topBorder.Inlines.Add(new System.Windows.Documents.Run(chars.TopRight.ToString()) { Foreground = borderBrush });
            }
            else
            {
                topBorder.Inlines.Clear();
                topBorder.Inlines.Add(new System.Windows.Documents.Run(chars.TopLeft.ToString()) { Foreground = borderBrush });
                topBorder.Inlines.Add(new System.Windows.Documents.Run(new string(chars.Horizontal, 80)) { Foreground = borderBrush });
                topBorder.Inlines.Add(new System.Windows.Documents.Run(chars.TopRight.ToString()) { Foreground = borderBrush });
            }

            // Left/Right borders (repeat vertical character)
            leftBorder.Text = chars.Vertical.ToString();
            leftBorder.Foreground = borderBrush;
            rightBorder.Text = chars.Vertical.ToString();
            rightBorder.Foreground = borderBrush;

            // Bottom border
            bottomBorder.Inlines.Clear();
            bottomBorder.Inlines.Add(new System.Windows.Documents.Run(chars.BottomLeft.ToString()) { Foreground = borderBrush });
            bottomBorder.Inlines.Add(new System.Windows.Documents.Run(new string(chars.Horizontal, 80)) { Foreground = borderBrush });
            bottomBorder.Inlines.Add(new System.Windows.Documents.Run(chars.BottomRight.ToString()) { Foreground = borderBrush });

            // Background
            outerBorder.Background = new SolidColorBrush(theme.Background);
        }

        private BorderChars GetBorderChars(TUIBorderStyle style)
        {
            return style switch
            {
                TUIBorderStyle.Single => new BorderChars
                {
                    TopLeft = '┌',
                    TopRight = '┐',
                    BottomLeft = '└',
                    BottomRight = '┘',
                    Horizontal = '─',
                    Vertical = '│',
                    TJunctionDown = '┬',
                    TJunctionUp = '┴',
                    TJunctionRight = '├',
                    TJunctionLeft = '┤',
                    Cross = '┼'
                },
                TUIBorderStyle.Double => new BorderChars
                {
                    TopLeft = '╔',
                    TopRight = '╗',
                    BottomLeft = '╚',
                    BottomRight = '╝',
                    Horizontal = '═',
                    Vertical = '║',
                    TJunctionDown = '╦',
                    TJunctionUp = '╩',
                    TJunctionRight = '╠',
                    TJunctionLeft = '╣',
                    Cross = '╬'
                },
                TUIBorderStyle.Rounded => new BorderChars
                {
                    TopLeft = '╭',
                    TopRight = '╮',
                    BottomLeft = '╰',
                    BottomRight = '╯',
                    Horizontal = '─',
                    Vertical = '│',
                    TJunctionDown = '┬',
                    TJunctionUp = '┴',
                    TJunctionRight = '├',
                    TJunctionLeft = '┤',
                    Cross = '┼'
                },
                TUIBorderStyle.Bold => new BorderChars
                {
                    TopLeft = '┏',
                    TopRight = '┓',
                    BottomLeft = '┗',
                    BottomRight = '┛',
                    Horizontal = '━',
                    Vertical = '┃',
                    TJunctionDown = '┳',
                    TJunctionUp = '┻',
                    TJunctionRight = '┣',
                    TJunctionLeft = '┫',
                    Cross = '╋'
                },
                _ => GetBorderChars(TUIBorderStyle.Single)
            };
        }

        private void ApplyTheme()
        {
            RenderBorders();
        }

        private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            ApplyTheme();
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TUIBox box)
            {
                box.RenderBorders();
            }
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return outerBorder;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            outerBorder.Measure(constraint);
            return outerBorder.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            outerBorder.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);
            contentPresenter.Content = newContent;
        }
    }

    public enum TUIBorderStyle
    {
        Single,   // ┌─┐│└┘
        Double,   // ╔═╗║╚╝
        Rounded,  // ╭─╮│╰╯
        Bold      // ┏━┓┃┗┛
    }

    internal struct BorderChars
    {
        public char TopLeft;
        public char TopRight;
        public char BottomLeft;
        public char BottomRight;
        public char Horizontal;
        public char Vertical;
        public char TJunctionDown;
        public char TJunctionUp;
        public char TJunctionRight;
        public char TJunctionLeft;
        public char Cross;
    }
}
