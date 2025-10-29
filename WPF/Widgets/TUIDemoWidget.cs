using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Controls;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Demo widget showing TUI-styled controls in action
    /// This demonstrates the terminal aesthetic WITHOUT terminal limitations
    /// </summary>
    public class TUIDemoWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;

        public TUIDemoWidget(ILogger logger, IThemeManager themeManager)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

            WidgetName = "TUI Demo";
            WidgetType = "TUIDemo";
        }

        public TUIDemoWidget()
            : this(Logger.Instance, ThemeManager.Instance)
        {
        }

        public override void Initialize()
        {
            BuildUI();
            ApplyTheme();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            // Main container
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar
            mainGrid.Background = new SolidColorBrush(theme.Background);

            // Content area with TUI boxes
            var contentScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8)
            };

            var contentStack = new StackPanel();

            // ===== BOX 1: Task List Example =====
            var taskBox = new TUIBox
            {
                Title = "TASKS",
                BorderStyle = TUIBorderStyle.Single,
                ShowTitle = true
            };

            var taskContent = new StackPanel();

            // Task list
            var taskList = new TUIListBox
            {
                ItemsSource = new List<string>
                {
                    "○ ctrl n test task [Oct 31]",
                    "● new task test"
                },
                MinHeight = 100
            };
            taskContent.Children.Add(taskList);

            taskBox.Content = taskContent;
            contentStack.Children.Add(taskBox);

            // ===== BOX 2: Quick Add Form =====
            var quickAddBox = new TUIBox
            {
                Title = "QUICK ADD",
                BorderStyle = TUIBorderStyle.Single,
                ShowTitle = true
            };

            var formGrid = new Grid();
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title input
            var titleLabel = new TextBlock
            {
                Text = "Title",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 4, 0, 2)
            };
            Grid.SetRow(titleLabel, 0);
            formGrid.Children.Add(titleLabel);

            var titleInput = new TUITextInput
            {
                Placeholder = "Enter task title...",
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(titleInput, 1);
            formGrid.Children.Add(titleInput);

            // Description input
            var descLabel = new TextBlock
            {
                Text = "Description",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 4, 0, 2)
            };
            Grid.SetRow(descLabel, 2);
            formGrid.Children.Add(descLabel);

            var descInput = new TUITextInput
            {
                Placeholder = "Add description...",
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(descInput, 3);
            formGrid.Children.Add(descInput);

            // Status and Priority dropdowns side-by-side
            var dropdownGrid = new Grid();
            dropdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dropdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var statusCombo = new TUIComboBox
            {
                Label = "Status",
                ItemsSource = new List<string> { "Todo", "In Progress", "Done" },
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 4, 8)
            };
            Grid.SetColumn(statusCombo, 0);
            dropdownGrid.Children.Add(statusCombo);

            var priorityCombo = new TUIComboBox
            {
                Label = "Priority",
                ItemsSource = new List<string> { "Low", "Medium", "High" },
                SelectedIndex = 1,
                Margin = new Thickness(4, 0, 0, 8)
            };
            Grid.SetColumn(priorityCombo, 1);
            dropdownGrid.Children.Add(priorityCombo);

            Grid.SetRow(dropdownGrid, 4);
            formGrid.Children.Add(dropdownGrid);

            // Project dropdown
            var projectCombo = new TUIComboBox
            {
                Label = "Project",
                ItemsSource = new List<string> { "None", "SuperTUI", "Personal", "Work" },
                SelectedIndex = 0
            };
            Grid.SetRow(projectCombo, 5);
            formGrid.Children.Add(projectCombo);

            quickAddBox.Content = formGrid;
            contentStack.Children.Add(quickAddBox);

            // ===== BOX 3: Different Border Styles =====
            var stylesBox = new TUIBox
            {
                Title = "BORDER STYLES",
                BorderStyle = TUIBorderStyle.Double,
                ShowTitle = true
            };

            var stylesStack = new StackPanel();

            var singleBox = new TUIBox
            {
                Title = "Single Line",
                BorderStyle = TUIBorderStyle.Single,
                Content = new TextBlock
                {
                    Text = "┌─┐│└┘ single line borders",
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    Foreground = new SolidColorBrush(theme.Foreground)
                }
            };
            stylesStack.Children.Add(singleBox);

            var roundedBox = new TUIBox
            {
                Title = "Rounded",
                BorderStyle = TUIBorderStyle.Rounded,
                Content = new TextBlock
                {
                    Text = "╭─╮│╰╯ rounded corners",
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    Foreground = new SolidColorBrush(theme.Foreground)
                }
            };
            stylesStack.Children.Add(roundedBox);

            var boldBox = new TUIBox
            {
                Title = "Bold",
                BorderStyle = TUIBorderStyle.Bold,
                Content = new TextBlock
                {
                    Text = "┏━┓┃┗┛ bold/thick borders",
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    Foreground = new SolidColorBrush(theme.Foreground)
                }
            };
            stylesStack.Children.Add(boldBox);

            stylesBox.Content = stylesStack;
            contentStack.Children.Add(stylesBox);

            contentScroll.Content = contentStack;
            Grid.SetRow(contentScroll, 0);
            mainGrid.Children.Add(contentScroll);

            // Status bar at bottom
            var statusBar = new TUIStatusBar
            {
                Commands = new List<TUICommand>
                {
                    new TUICommand("Ctrl+S", "Save"),
                    new TUICommand("Esc", "Cancel"),
                    new TUICommand("Tab", "Next"),
                    new TUICommand("F2", "Edit")
                },
                StatusText = "Creating new task..."
            };
            Grid.SetRow(statusBar, 1);
            mainGrid.Children.Add(statusBar);

            Content = mainGrid;
        }

        public void ApplyTheme()
        {
            // Theme is applied automatically by TUI controls
        }

        protected override void OnDispose()
        {
            base.OnDispose();
        }
    }
}
