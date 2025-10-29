using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Modal command palette for discovering and opening panes
    /// Features: Fuzzy search, keyboard navigation, command support
    /// Keyboard: : or Ctrl+Space to open, Escape to close, arrows to navigate, Enter to execute
    /// </summary>
    public class CommandPalettePane : PaneBase
    {
        // Services
        private readonly PaneFactory paneFactory;
        private readonly PaneManager paneManager;
        private readonly IConfigurationManager configManager;

        // UI Components
        private TextBox searchBox;
        private ListBox resultsListBox;
        private TextBlock statusText;
        private Border overlayBorder;
        private Border paletteBox;

        // Data
        private List<PaletteItem> allItems = new List<PaletteItem>();
        private List<PaletteItem> filteredItems = new List<PaletteItem>();
        private List<string> commandHistory = new List<string>();
        private int historyIndex = -1;

        // State
        public event EventHandler<string> CommandExecuted;
        public event EventHandler CloseRequested;

        public override PaneSizePreference SizePreference => PaneSizePreference.Fixed;

        public CommandPalettePane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager configManager,
            PaneFactory paneFactory,
            PaneManager paneManager)
            : base(logger, themeManager, projectContext)
        {
            this.configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            this.paneFactory = paneFactory ?? throw new ArgumentNullException(nameof(paneFactory));
            this.paneManager = paneManager ?? throw new ArgumentNullException(nameof(paneManager));

            PaneName = "Command Palette";
            Width = 600;
            Height = 400;
        }

        protected override UIElement BuildContent()
        {
            // Build palette items from available panes
            BuildPaletteItems();

            // Modal overlay (semi-transparent dark background)
            overlayBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(204, 10, 14, 20)), // 0.8 opacity
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Centered palette box
            paletteBox = new Border
            {
                Width = 600,
                Height = 400,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.5,
                    BlurRadius = 20,
                    ShadowDepth = 0
                }
            };

            // Content grid: search box + results + status
            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Results
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status

            // Search box
            var searchContainer = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 12, 16, 12)
            };

            searchBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CaretBrush = Brushes.Lime,
                Text = ""
            };
            searchBox.TextChanged += OnSearchTextChanged;
            searchBox.PreviewKeyDown += OnSearchBoxKeyDown;
            searchContainer.Child = searchBox;
            Grid.SetRow(searchContainer, 0);
            contentGrid.Children.Add(searchContainer);

            // Results list
            resultsListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 8, 8, 8)
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(resultsListBox, ScrollBarVisibility.Disabled);

            // Clean list style
            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(8, 6, 8, 6)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0, 2, 0, 2)));

            // Hover effect
            var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 50, 255, 100))));
            itemStyle.Triggers.Add(hoverTrigger);

            // Selection effect
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 50, 255, 100))));
            itemStyle.Triggers.Add(selectedTrigger);

            resultsListBox.ItemContainerStyle = itemStyle;
            resultsListBox.PreviewKeyDown += OnResultsKeyDown;

            Grid.SetRow(resultsListBox, 1);
            contentGrid.Children.Add(resultsListBox);

            // Status bar
            var statusBar = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 8, 16, 8)
            };

            statusText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Text = "Type to search panes and commands | ↑↓ Navigate | Enter Execute | Esc Close"
            };
            statusBar.Child = statusText;
            Grid.SetRow(statusBar, 2);
            contentGrid.Children.Add(statusBar);

            paletteBox.Child = contentGrid;
            overlayBorder.Child = paletteBox;

            // Initialize with all items
            RefreshResults("");

            return overlayBorder;
        }

        public override void Initialize()
        {
            base.Initialize();

            // Auto-focus search box when palette opens
            Loaded += (s, e) =>
            {
                searchBox.Focus();
                Keyboard.Focus(searchBox);
            };
        }

        /// <summary>
        /// Build palette items from available panes and commands
        /// </summary>
        private void BuildPaletteItems()
        {
            allItems.Clear();

            // Add panes from PaneFactory
            var paneTypes = paneFactory.GetAvailablePaneTypes();
            foreach (var paneType in paneTypes)
            {
                var description = GetPaneDescription(paneType);
                allItems.Add(new PaletteItem
                {
                    Type = PaletteItemType.Pane,
                    Name = paneType,
                    DisplayName = $":{paneType}",
                    Description = description,
                    Icon = "📄"
                });
            }

            // Add system commands
            allItems.Add(new PaletteItem
            {
                Type = PaletteItemType.Command,
                Name = "close",
                DisplayName = ":close",
                Description = "Close focused pane",
                Icon = "✕"
            });

            allItems.Add(new PaletteItem
            {
                Type = PaletteItemType.Command,
                Name = "quit",
                DisplayName = ":quit",
                Description = "Exit application",
                Icon = "⏻"
            });

            allItems.Add(new PaletteItem
            {
                Type = PaletteItemType.Command,
                Name = "theme",
                DisplayName = ":theme <name>",
                Description = "Switch color theme",
                Icon = "🎨"
            });

            allItems.Add(new PaletteItem
            {
                Type = PaletteItemType.Command,
                Name = "project",
                DisplayName = ":project <name>",
                Description = "Switch to project",
                Icon = "📁"
            });

            Log($"Built {allItems.Count} palette items");
        }

        private string GetPaneDescription(string paneType)
        {
            // Map pane types to descriptions (can be extended with metadata system)
            return paneType.ToLower() switch
            {
                "tasks" => "View and manage tasks",
                "notes" => "Browse and edit notes",
                "processing" => "Process inbox items",
                "projects" => "View all projects",
                "calendar" => "View calendar and agenda",
                "timeline" => "View task timeline",
                _ => $"Open {paneType} pane"
            };
        }

        /// <summary>
        /// Fuzzy search with character matching and scoring
        /// </summary>
        private void RefreshResults(string query)
        {
            resultsListBox.Items.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                // Show all items
                filteredItems = allItems.OrderBy(i => i.Name).ToList();
            }
            else
            {
                // Fuzzy search: match characters in order, score by position
                filteredItems = allItems
                    .Select(item => new { Item = item, Score = FuzzyScore(item.Name, query) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Item)
                    .ToList();
            }

            // Display results
            foreach (var item in filteredItems.Take(20))
            {
                var resultPanel = BuildResultItem(item, query);
                resultsListBox.Items.Add(resultPanel);
            }

            // Auto-select first item
            if (resultsListBox.Items.Count > 0)
            {
                resultsListBox.SelectedIndex = 0;
            }

            // Update status
            statusText.Text = filteredItems.Count > 0
                ? $"{filteredItems.Count} results | ↑↓ Navigate | Enter Execute | Esc Close"
                : "No results | Esc Close";
        }

        /// <summary>
        /// Calculate fuzzy match score (higher = better match)
        /// </summary>
        private int FuzzyScore(string text, string query)
        {
            text = text.ToLower();
            query = query.ToLower();

            if (text.Contains(query))
                return 1000; // Exact substring match = highest score

            int score = 0;
            int queryIndex = 0;
            int lastMatchIndex = -1;

            for (int i = 0; i < text.Length && queryIndex < query.Length; i++)
            {
                if (text[i] == query[queryIndex])
                {
                    // Match found
                    score += 100;

                    // Bonus for consecutive matches
                    if (lastMatchIndex >= 0 && i == lastMatchIndex + 1)
                        score += 50;

                    // Bonus for match at start
                    if (queryIndex == 0 && i == 0)
                        score += 100;

                    lastMatchIndex = i;
                    queryIndex++;
                }
            }

            // All characters must match
            if (queryIndex < query.Length)
                return 0;

            return score;
        }

        /// <summary>
        /// Build UI for a single result item
        /// </summary>
        private StackPanel BuildResultItem(PaletteItem item, string query)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            // Icon
            var icon = new TextBlock
            {
                Text = item.Icon,
                FontSize = 14,
                Width = 30,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(icon);

            // Name with highlighting
            var nameText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Highlight matching characters
            if (!string.IsNullOrWhiteSpace(query))
            {
                BuildHighlightedText(nameText, item.DisplayName, query);
            }
            else
            {
                nameText.Text = item.DisplayName;
            }

            panel.Children.Add(nameText);

            // Description
            var description = new TextBlock
            {
                Text = item.Description,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(description);

            panel.Tag = item;
            return panel;
        }

        /// <summary>
        /// Build text with highlighted matching characters
        /// </summary>
        private void BuildHighlightedText(TextBlock textBlock, string text, string query)
        {
            textBlock.Inlines.Clear();
            query = query.ToLower();
            int queryIndex = 0;

            for (int i = 0; i < text.Length; i++)
            {
                bool isMatch = queryIndex < query.Length &&
                               char.ToLower(text[i]) == query[queryIndex];

                var run = new System.Windows.Documents.Run(text[i].ToString());
                if (isMatch)
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)); // Bright green
                    run.FontWeight = FontWeights.Bold;
                    queryIndex++;
                }

                textBlock.Inlines.Add(run);
            }
        }

        /// <summary>
        /// Handle search text changes
        /// </summary>
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshResults(searchBox.Text.Trim());
        }

        /// <summary>
        /// Handle keyboard in search box
        /// </summary>
        private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;

                case Key.Down:
                    // Move to results list
                    if (resultsListBox.Items.Count > 0)
                    {
                        resultsListBox.Focus();
                        resultsListBox.SelectedIndex = 0;
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    // Navigate command history
                    if (commandHistory.Count > 0)
                    {
                        if (historyIndex == -1)
                            historyIndex = commandHistory.Count - 1;
                        else
                            historyIndex = Math.Max(0, historyIndex - 1);

                        searchBox.Text = commandHistory[historyIndex];
                        searchBox.CaretIndex = searchBox.Text.Length;
                    }
                    e.Handled = true;
                    break;

                case Key.Enter:
                    ExecuteSelected();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Handle keyboard in results list
        /// </summary>
        private void OnResultsKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    ExecuteSelected();
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (resultsListBox.SelectedIndex == 0)
                    {
                        // Return to search box
                        searchBox.Focus();
                        searchBox.CaretIndex = searchBox.Text.Length;
                        e.Handled = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// Execute the selected command/pane
        /// </summary>
        private void ExecuteSelected()
        {
            if (resultsListBox.SelectedItem is StackPanel panel && panel.Tag is PaletteItem item)
            {
                var command = searchBox.Text.Trim();

                // Add to history
                if (!string.IsNullOrWhiteSpace(command) && !commandHistory.Contains(command))
                {
                    commandHistory.Add(command);
                    if (commandHistory.Count > 50)
                        commandHistory.RemoveAt(0);
                }

                // Execute command
                ExecuteCommand(item, command);

                // Notify and close
                CommandExecuted?.Invoke(this, command);
            }
        }

        /// <summary>
        /// Execute a command or open a pane
        /// </summary>
        private void ExecuteCommand(PaletteItem item, string fullCommand)
        {
            try
            {
                ShowStatus("Opening...");

                switch (item.Type)
                {
                    case PaletteItemType.Pane:
                        // Open pane
                        var pane = paneFactory.CreatePane(item.Name);
                        paneManager.OpenPane(pane);
                        Log($"Opened pane: {item.Name}");
                        CloseRequested?.Invoke(this, EventArgs.Empty);
                        break;

                    case PaletteItemType.Command:
                        ExecuteSystemCommand(item.Name, fullCommand);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to execute command '{item.Name}': {ex.Message}", LogLevel.Error);
                ShowStatus($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute system commands
        /// </summary>
        private void ExecuteSystemCommand(string commandName, string fullCommand)
        {
            switch (commandName.ToLower())
            {
                case "close":
                    paneManager.CloseFocusedPane();
                    Log("Closed focused pane");
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case "quit":
                    Log("Quit command executed");
                    Application.Current.Shutdown();
                    break;

                case "theme":
                    // Parse: :theme <name>
                    var themeParts = fullCommand.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (themeParts.Length > 1)
                    {
                        var themeName = themeParts[1];
                        themeManager.ApplyTheme(themeName);
                        Log($"Switched to theme: {themeName}");
                        ShowStatus($"Theme: {themeName}");
                    }
                    else
                    {
                        ShowStatus("Usage: :theme <name>");
                    }
                    break;

                case "project":
                    // Parse: :project <name>
                    var projectParts = fullCommand.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (projectParts.Length > 1)
                    {
                        var projectName = string.Join(" ", projectParts.Skip(1));
                        // Would need project service to switch projects
                        ShowStatus($"Project switching not yet implemented: {projectName}");
                    }
                    else
                    {
                        ShowStatus("Usage: :project <name>");
                    }
                    break;

                default:
                    ShowStatus($"Unknown command: {commandName}");
                    break;
            }
        }

        /// <summary>
        /// Show status message
        /// </summary>
        private void ShowStatus(string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                statusText.Text = message;
            });
        }

        protected override void OnDispose()
        {
            // Clean up if needed
            base.OnDispose();
        }

        /// <summary>
        /// Animate palette opening
        /// </summary>
        public void AnimateOpen()
        {
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            overlayBorder.BeginAnimation(OpacityProperty, fadeIn);

            var scaleTransform = new ScaleTransform(0.95, 0.95);
            paletteBox.RenderTransform = scaleTransform;
            paletteBox.RenderTransformOrigin = new Point(0.5, 0.5);

            var scaleUp = new DoubleAnimation
            {
                From = 0.95,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUp);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUp);
        }

        /// <summary>
        /// Animate palette closing
        /// </summary>
        public void AnimateClose(Action onComplete)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(100)
            };
            fadeOut.Completed += (s, e) => onComplete?.Invoke();
            overlayBorder.BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    /// <summary>
    /// Palette item types
    /// </summary>
    public enum PaletteItemType
    {
        Pane,
        Command
    }

    /// <summary>
    /// Palette item (pane or command)
    /// </summary>
    public class PaletteItem
    {
        public PaletteItemType Type { get; set; }
        public string Name { get; set; }           // Internal name (e.g., "tasks")
        public string DisplayName { get; set; }     // Display name (e.g., ":tasks")
        public string Description { get; set; }
        public string Icon { get; set; }
    }
}
