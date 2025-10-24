using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Simple notes widget - demonstrates text input and state preservation
    /// </summary>
    public class NotesWidget : WidgetBase
    {
        private TextBox notesTextBox;

        public NotesWidget()
        {
            WidgetType = "Notes";
            BuildUI();
        }

        private void BuildUI()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel();

            // Title
            var title = new TextBlock
            {
                Text = "NOTES",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Text box
            notesTextBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 100
            };

            stackPanel.Children.Add(title);
            stackPanel.Children.Add(notesTextBox);

            border.Child = stackPanel;
            this.Content = border;
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
    }
}
