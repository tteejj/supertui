using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Popup widget picker for selecting which widget to add
    /// </summary>
    public class WidgetPicker : Window
    {
        private ListBox listBox;
        private List<WidgetOption> options;

        public class WidgetOption
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string TypeName { get; set; }
        }

        public WidgetOption SelectedWidget { get; private set; }

        public WidgetPicker()
        {
            Title = "Add Widget";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            BorderBrush = new SolidColorBrush(Color.FromRgb(78, 201, 176));
            BorderThickness = new Thickness(2);

            BuildUI();
            LoadWidgetOptions();

            // Set focus to listbox when window loads
            Loaded += (s, e) => {
                listBox.Focus();
                Keyboard.Focus(listBox);
            };
        }

        private void BuildUI()
        {
            var mainPanel = new DockPanel
            {
                Margin = new Thickness(15)
            };

            // Header
            var header = new TextBlock
            {
                Text = "Select Widget to Add",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            DockPanel.SetDock(header, Dock.Top);
            mainPanel.Children.Add(header);

            // Instructions
            var instructions = new TextBlock
            {
                Text = "↑/↓: Navigate | Enter: Select | Esc: Cancel",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(instructions, Dock.Top);
            mainPanel.Children.Add(instructions);

            // Widget list
            listBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(12, 12, 12)),
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                Padding = new Thickness(5)
            };

            // Custom item template
            listBox.ItemTemplate = new DataTemplate
            {
                VisualTree = CreateItemTemplate()
            };

            mainPanel.Children.Add(listBox);

            Content = mainPanel;

            // Event handlers
            listBox.MouseDoubleClick += (s, e) => SelectWidget();
            KeyDown += WidgetPicker_KeyDown;
        }

        private FrameworkElementFactory CreateItemTemplate()
        {
            var stackPanel = new FrameworkElementFactory(typeof(StackPanel));
            stackPanel.SetValue(StackPanel.MarginProperty, new Thickness(5));

            // Widget name
            var nameText = new FrameworkElementFactory(typeof(TextBlock));
            nameText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
            nameText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            nameText.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(78, 201, 176)));
            stackPanel.AppendChild(nameText);

            // Description
            var descText = new FrameworkElementFactory(typeof(TextBlock));
            descText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Description"));
            descText.SetValue(TextBlock.FontSizeProperty, 11.0);
            descText.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(153, 153, 153)));
            descText.SetValue(TextBlock.MarginProperty, new Thickness(0, 3, 0, 0));
            descText.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            stackPanel.AppendChild(descText);

            return stackPanel;
        }

        private void LoadWidgetOptions()
        {
            options = new List<WidgetOption>
            {
                new WidgetOption
                {
                    Name = "Clock",
                    Description = "Digital clock showing current time and date",
                    TypeName = "SuperTUI.Widgets.ClockWidget"
                },
                new WidgetOption
                {
                    Name = "Task Summary",
                    Description = "Overview of tasks - total, completed, pending, overdue",
                    TypeName = "SuperTUI.Widgets.TaskSummaryWidget"
                },
                new WidgetOption
                {
                    Name = "Notes",
                    Description = "Quick notes with save to file",
                    TypeName = "SuperTUI.Widgets.NotesWidget"
                },
                new WidgetOption
                {
                    Name = "Todo List",
                    Description = "Task list with add/edit/delete and persistence",
                    TypeName = "SuperTUI.Widgets.TodoWidget"
                },
                new WidgetOption
                {
                    Name = "Git Status",
                    Description = "Git repository status - branch, changes, commits",
                    TypeName = "SuperTUI.Widgets.GitStatusWidget"
                },
                new WidgetOption
                {
                    Name = "File Explorer",
                    Description = "Browse directories and files",
                    TypeName = "SuperTUI.Widgets.FileExplorerWidget"
                },
                new WidgetOption
                {
                    Name = "Command Palette",
                    Description = "Quick command search and execution",
                    TypeName = "SuperTUI.Widgets.CommandPaletteWidget"
                },
                new WidgetOption
                {
                    Name = "Shortcut Help",
                    Description = "View all keyboard shortcuts",
                    TypeName = "SuperTUI.Widgets.ShortcutHelpWidget"
                }
            };

            listBox.ItemsSource = options;
            if (options.Count > 0)
            {
                listBox.SelectedIndex = 0;
            }
        }

        private void WidgetPicker_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    SelectWidget();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    DialogResult = false;
                    Close();
                    e.Handled = true;
                    break;
            }
        }

        private void SelectWidget()
        {
            SelectedWidget = listBox.SelectedItem as WidgetOption;
            if (SelectedWidget != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
