using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets.Overlays
{
    /// <summary>
    /// Command palette overlay (top zone)
    /// Fuzzy search all commands, instant execution
    /// Vim-style command mode with autocomplete
    /// </summary>
    public class CommandPaletteOverlay : UserControl
    {
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;

        private TextBox inputBox;
        private ListBox suggestionList;
        private List<Command> allCommands = new List<Command>();

        public event Action<Command> CommandExecuted;
        public event Action Cancelled;

        public CommandPaletteOverlay(ITaskService taskService, IProjectService projectService)
        {
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.logger = Logger.Instance;
            this.themeManager = ThemeManager.Instance;

            BuildCommandList();
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            var mainPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Background = new SolidColorBrush(theme.Surface)
            };

            // Input box with command prefix
            var inputPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var promptText = new TextBlock
            {
                Text = ">",
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            inputBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 18,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(5),
                MinWidth = 600
            };

            inputBox.TextChanged += OnInputTextChanged;
            inputBox.KeyDown += OnInputKeyDown;

            inputPanel.Children.Add(promptText);
            inputPanel.Children.Add(inputBox);
            mainPanel.Children.Add(inputPanel);

            // Suggestion list
            suggestionList = new ListBox
            {
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0),
                MaxHeight = 300,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 14
            };

            suggestionList.SelectionChanged += OnSuggestionSelected;
            suggestionList.KeyDown += OnSuggestionKeyDown;

            mainPanel.Children.Add(suggestionList);

            // Hint text
            var hintText = new TextBlock
            {
                Text = "[↓]Select [Enter]Execute [Tab]Complete [Esc]Cancel",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.Children.Add(hintText);

            this.Content = mainPanel;
            this.Focusable = true;

            // Focus input on load
            this.Loaded += (s, e) =>
            {
                inputBox.Focus();
                UpdateSuggestions("");  // Show all commands initially
            };
        }

        private void BuildCommandList()
        {
            // Task commands
            allCommands.Add(new Command("create task", "Create a new task", "n", CommandCategory.Task));
            allCommands.Add(new Command("filter active", "Show active tasks only", "2", CommandCategory.Filter));
            allCommands.Add(new Command("filter completed", "Show completed tasks", "3", CommandCategory.Filter));
            allCommands.Add(new Command("filter today", "Show tasks due today", "4", CommandCategory.Filter));
            allCommands.Add(new Command("filter overdue", "Show overdue tasks", "5", CommandCategory.Filter));
            allCommands.Add(new Command("filter high", "Show high priority tasks", "", CommandCategory.Filter));
            allCommands.Add(new Command("clear filters", "Clear all filters", "0", CommandCategory.Filter));

            // View commands
            allCommands.Add(new Command("view list", "Switch to list view", "Alt+1", CommandCategory.View));
            allCommands.Add(new Command("view kanban", "Switch to kanban view", "Alt+2", CommandCategory.View));
            allCommands.Add(new Command("view timeline", "Switch to timeline view", "Alt+3", CommandCategory.View));
            allCommands.Add(new Command("view calendar", "Switch to calendar view", "Alt+4", CommandCategory.View));
            allCommands.Add(new Command("view table", "Switch to table view", "Alt+5", CommandCategory.View));

            // Navigation commands
            allCommands.Add(new Command("goto tasks", "Go to task management", "", CommandCategory.Navigation));
            allCommands.Add(new Command("goto projects", "Go to project stats", "", CommandCategory.Navigation));
            allCommands.Add(new Command("goto kanban", "Go to kanban board", "", CommandCategory.Navigation));
            allCommands.Add(new Command("goto agenda", "Go to agenda", "", CommandCategory.Navigation));
            allCommands.Add(new Command("jump", "Jump to anything", "Ctrl+J", CommandCategory.Navigation));

            // Workspace commands
            allCommands.Add(new Command("workspace 1", "Switch to workspace 1", "Alt+1", CommandCategory.Workspace));
            allCommands.Add(new Command("workspace 2", "Switch to workspace 2", "Alt+2", CommandCategory.Workspace));
            allCommands.Add(new Command("workspace 3", "Switch to workspace 3", "Alt+3", CommandCategory.Workspace));

            // Sort commands
            allCommands.Add(new Command("sort priority", "Sort by priority", "", CommandCategory.Sort));
            allCommands.Add(new Command("sort duedate", "Sort by due date", "", CommandCategory.Sort));
            allCommands.Add(new Command("sort title", "Sort by title", "", CommandCategory.Sort));
            allCommands.Add(new Command("sort created", "Sort by created date", "", CommandCategory.Sort));
            allCommands.Add(new Command("sort updated", "Sort by updated date", "", CommandCategory.Sort));

            // Group commands
            allCommands.Add(new Command("group status", "Group by status", "", CommandCategory.Group));
            allCommands.Add(new Command("group priority", "Group by priority", "", CommandCategory.Group));
            allCommands.Add(new Command("group project", "Group by project", "", CommandCategory.Group));
            allCommands.Add(new Command("group none", "Clear grouping", "", CommandCategory.Group));

            // Project commands
            var projects = projectService.GetProjects(p => !p.Deleted);
            foreach (var project in projects.Take(10))
            {
                allCommands.Add(new Command($"project {project.Name}", $"Switch to {project.Name} project", "", CommandCategory.Project));
            }

            // System commands
            allCommands.Add(new Command("help", "Show keyboard shortcuts", "?", CommandCategory.System));
            allCommands.Add(new Command("settings", "Open settings", "", CommandCategory.System));
            allCommands.Add(new Command("theme", "Change theme", "", CommandCategory.System));

            // Sort alphabetically within categories
            allCommands = allCommands.OrderBy(c => c.Category).ThenBy(c => c.Name).ToList();
        }

        private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = inputBox.Text.Trim();
            UpdateSuggestions(searchText);
        }

        private void UpdateSuggestions(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all commands grouped by category
                suggestionList.ItemsSource = allCommands.Take(15);
            }
            else
            {
                // Fuzzy search with scoring
                var matches = allCommands
                    .Select(cmd => new
                    {
                        Command = cmd,
                        Score = FuzzyScore(cmd.Name, searchText)
                    })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Take(15)
                    .Select(x => x.Command)
                    .ToList();

                suggestionList.ItemsSource = matches;
            }

            // Auto-select first item
            if (suggestionList.Items.Count > 0)
            {
                suggestionList.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Fuzzy matching score (0 = no match, higher = better match)
        /// </summary>
        private int FuzzyScore(string text, string pattern)
        {
            text = text.ToLower();
            pattern = pattern.ToLower();

            // Exact match = highest score
            if (text == pattern)
                return 1000;

            // Starts with = high score
            if (text.StartsWith(pattern))
                return 500;

            // Contains = medium score
            if (text.Contains(pattern))
                return 250;

            // Fuzzy char-by-char matching
            int score = 0;
            int patternIndex = 0;

            for (int i = 0; i < text.Length && patternIndex < pattern.Length; i++)
            {
                if (text[i] == pattern[patternIndex])
                {
                    score += 10;
                    patternIndex++;
                }
            }

            // All chars matched?
            if (patternIndex == pattern.Length)
                return score;

            return 0;  // No match
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Execute selected command
                if (suggestionList.SelectedItem is Command cmd)
                {
                    ExecuteCommand(cmd);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                // Cancel
                Cancelled?.Invoke();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // Move selection down
                if (suggestionList.Items.Count > 0)
                {
                    suggestionList.Focus();
                    suggestionList.SelectedIndex = 0;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Tab)
            {
                // Autocomplete to first suggestion
                if (suggestionList.SelectedItem is Command cmd)
                {
                    inputBox.Text = cmd.Name;
                    inputBox.SelectionStart = inputBox.Text.Length;
                    e.Handled = true;
                }
            }
        }

        private void OnSuggestionKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Execute selected command
                if (suggestionList.SelectedItem is Command cmd)
                {
                    ExecuteCommand(cmd);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                // Return to input box
                inputBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && suggestionList.SelectedIndex == 0)
            {
                // At top of list, return to input
                inputBox.Focus();
                inputBox.SelectionStart = inputBox.Text.Length;
                e.Handled = true;
            }
        }

        private void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            // Double-click to execute (handled by default ListBox behavior)
        }

        private void ExecuteCommand(Command cmd)
        {
            logger?.Info("CommandPaletteOverlay", $"Executing command: {cmd.Name}");
            CommandExecuted?.Invoke(cmd);
        }

        #region Data Templates

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // Set custom item template for suggestions
            var theme = themeManager.CurrentTheme;

            var template = new DataTemplate(typeof(Command));
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            factory.SetValue(StackPanel.MarginProperty, new Thickness(5));

            // Command name (bold)
            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
            nameFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            nameFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(theme.Foreground));
            nameFactory.SetValue(TextBlock.MinWidthProperty, 200.0);

            // Description (dimmed)
            var descFactory = new FrameworkElementFactory(typeof(TextBlock));
            descFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Description"));
            descFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(128, 128, 128)));
            descFactory.SetValue(TextBlock.MarginProperty, new Thickness(10, 0, 10, 0));

            // Shortcut (highlighted)
            var shortcutFactory = new FrameworkElementFactory(typeof(TextBlock));
            shortcutFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Shortcut"));
            shortcutFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(theme.Primary));

            factory.AppendChild(nameFactory);
            factory.AppendChild(descFactory);
            factory.AppendChild(shortcutFactory);

            template.VisualTree = factory;
            suggestionList.ItemTemplate = template;
        }

        #endregion
    }

    #region Command Model

    public enum CommandCategory
    {
        Task,
        Filter,
        View,
        Navigation,
        Workspace,
        Sort,
        Group,
        Project,
        System
    }

    public class Command
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Shortcut { get; set; }
        public CommandCategory Category { get; set; }

        public Command(string name, string description, string shortcut, CommandCategory category)
        {
            Name = name;
            Description = description;
            Shortcut = shortcut;
            Category = category;
        }

        public override string ToString() => Name;
    }

    #endregion
}
