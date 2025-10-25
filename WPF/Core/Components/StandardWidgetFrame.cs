using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Standard frame for widgets providing consistent header, content area, and footer
    /// Includes title, context info, and keyboard shortcuts display
    /// </summary>
    public class StandardWidgetFrame : Border
    {
        private readonly IThemeManager themeManager;

        private Grid mainGrid;
        private Border headerBorder;
        private TextBlock titleText;
        private TextBlock contextText;
        private ContentControl contentArea;
        private Border footerBorder;
        private TextBlock footerText;

        public string Title
        {
            get => titleText?.Text ?? "";
            set { if (titleText != null) titleText.Text = value; }
        }

        public string ContextInfo
        {
            get => contextText?.Text ?? "";
            set
            {
                if (contextText != null)
                {
                    contextText.Text = value;
                    contextText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        public string FooterInfo
        {
            get => footerText?.Text ?? "";
            set
            {
                if (footerText != null)
                {
                    footerText.Text = value;
                    footerBorder.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        public UIElement Content
        {
            get => contentArea?.Content as UIElement;
            set { if (contentArea != null) contentArea.Content = value; }
        }

        public StandardWidgetFrame(IThemeManager themeManager)
        {
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            // Main grid with 3 rows: header, content, footer
            mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // Header
            headerBorder = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            titleText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                VerticalAlignment = VerticalAlignment.Center
            };

            contextText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };

            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(contextText, 1);
            headerGrid.Children.Add(titleText);
            headerGrid.Children.Add(contextText);
            headerBorder.Child = headerGrid;

            // Content area
            contentArea = new ContentControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Footer
            footerBorder = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Visibility = Visibility.Collapsed
            };

            footerText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                VerticalAlignment = VerticalAlignment.Center
            };

            footerBorder.Child = footerText;

            // Add to grid
            Grid.SetRow(headerBorder, 0);
            Grid.SetRow(contentArea, 1);
            Grid.SetRow(footerBorder, 2);

            mainGrid.Children.Add(headerBorder);
            mainGrid.Children.Add(contentArea);
            mainGrid.Children.Add(footerBorder);

            // Set as child
            this.Child = mainGrid;
            this.Background = new SolidColorBrush(theme.Background);
            this.BorderBrush = new SolidColorBrush(theme.Border);
            this.BorderThickness = new Thickness(0);
        }

        /// <summary>
        /// Apply theme to all frame elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            this.Background = new SolidColorBrush(theme.Background);
            this.BorderBrush = new SolidColorBrush(theme.Border);

            if (headerBorder != null)
            {
                headerBorder.Background = new SolidColorBrush(theme.Surface);
                headerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.Primary);
            }

            if (contextText != null)
            {
                contextText.Foreground = new SolidColorBrush(theme.ForegroundSecondary);
            }

            if (footerBorder != null)
            {
                footerBorder.Background = new SolidColorBrush(theme.Surface);
                footerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (footerText != null)
            {
                footerText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
            }
        }

        /// <summary>
        /// Set standard keyboard shortcuts footer for a widget
        /// </summary>
        public void SetStandardShortcuts(params string[] shortcuts)
        {
            if (shortcuts == null || shortcuts.Length == 0)
            {
                FooterInfo = "";
                return;
            }

            FooterInfo = string.Join(" | ", shortcuts);
        }
    }
}
