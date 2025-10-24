using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Simple notes widget - demonstrates text input and state preservation
    /// </summary>
    public class NotesWidget : WidgetBase, IThemeable
    {
        private Border containerBorder;
        private TextBlock titleText;
        private TextBox notesTextBox;

        public NotesWidget()
        {
            WidgetType = "Notes";
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel();

            // Title
            titleText = new TextBlock
            {
                Text = "NOTES",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Text box
            notesTextBox = new TextBox
            {
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 100
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(notesTextBox);

            containerBorder.Child = stackPanel;
            this.Content = containerBorder;
        }

        public override void Initialize()
        {
            notesTextBox.Text = "Type your notes here...";
        }

        public override void OnWidgetFocusReceived()
        {
            // Auto-focus the textbox when widget is focused
            Dispatcher.BeginInvoke(new Action(() =>
            {
                notesTextBox.Focus();
                notesTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        public override System.Collections.Generic.Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            state["Notes"] = notesTextBox.Text;
            return state;
        }

        public override void RestoreState(System.Collections.Generic.Dictionary<string, object> state)
        {
            if (state.ContainsKey("Notes"))
            {
                notesTextBox.Text = (string)state["Notes"];
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
            var theme = ThemeManager.Instance.CurrentTheme;

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
                containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
            }

            if (notesTextBox != null)
            {
                notesTextBox.Background = new SolidColorBrush(theme.Surface);
                notesTextBox.Foreground = new SolidColorBrush(theme.Foreground);
                notesTextBox.BorderBrush = new SolidColorBrush(theme.Border);
            }
        }
    }
}
