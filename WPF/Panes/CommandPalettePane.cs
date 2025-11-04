using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Interfaces;
using SuperTUI.Extensions;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Modal command palette for discovering and opening panes
    /// Features: Fuzzy search, keyboard navigation, command support
    /// Keyboard: : or Ctrl+Space to open, Escape to close, arrows to navigate, Enter to execute
    /// Implements IModal for proper modal management
    /// </summary>
    public class CommandPalettePane : PaneBase, IModal
    {
        // Services
        private readonly PaneFactory paneFactory;
        private readonly PaneManager paneManager;
        private readonly IConfigurationManager configManager;
        private readonly IEventBus eventBus;

        // Event handlers (store references for proper unsubscription)
        private Action<Core.Events.RefreshRequestedEvent> refreshRequestedHandler;

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

        // IModal implementation
        public ModalResult Result { get; private set; } = ModalResult.None;
        public object CustomResult { get; private set; }
        public UIElement ModalElement => this;
        public string ModalName => "CommandPalette";
        public event EventHandler<ModalClosedEventArgs> CloseRequested;

        public override PaneSizePreference SizePreference => PaneSizePreference.Fixed;

        public CommandPalettePane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager configManager,
            PaneFactory paneFactory,
            PaneManager paneManager,
            IEventBus eventBus)
            : base(logger, themeManager, projectContext)
        {
            this.configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            this.paneFactory = paneFactory ?? throw new ArgumentNullException(nameof(paneFactory));
            this.paneManager = paneManager ?? throw new ArgumentNullException(nameof(paneManager));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            PaneName = "Command Palette";
            Width = 600;
            Height = 400;
        }

        protected override UIElement BuildContent()
        {
            // Build palette items from available panes
            BuildPaletteItems();

            var theme = themeManager.CurrentTheme;

            // Modal overlay (semi-transparent dark background)
            overlayBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(204, theme.Background.R, theme.Background.G, theme.Background.B)), // 0.8 opacity
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Centered palette box - CRT TERMINAL AESTHETIC
            var phosphorGreen = Color.FromRgb(0, 255, 0);  // #00FF00
            var darkGreen = Color.FromRgb(0, 20, 0);  // Very dark green background

            paletteBox = new Border
            {
                Width = 600,
                Height = 400,
                Background = new SolidColorBrush(Colors.Black),  // Pure black CRT background
                BorderBrush = new SolidColorBrush(phosphorGreen),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(2),  // Thicker for visibility
                CornerRadius = new CornerRadius(0),  // Sharp corners, no rounding
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = phosphorGreen,  // Green glow, not black shadow
                    Opacity = 0.8,
                    BlurRadius = 15,  // Phosphor glow effect
                    ShadowDepth = 0
                }
            };

            // Content grid: search box + results + status
            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Results
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status

            // Search box - CRT TERMINAL STYLE
            var searchContainer = new Border
            {
                BorderBrush = new SolidColorBrush(phosphorGreen),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4, 2, 4, 2)  // Minimal padding for terminal look
            };

            searchBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas"),  // True terminal font
                FontSize = 12,
                Foreground = new SolidColorBrush(phosphorGreen),  // Green text
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CaretBrush = new SolidColorBrush(phosphorGreen),  // Green cursor
                Text = ""
            };
            searchBox.TextChanged += OnSearchTextChanged;
            searchBox.PreviewKeyDown += OnSearchBoxKeyDown;
            searchBox.ApplyFocusStyling(themeManager);
            searchContainer.Child = searchBox;
            Grid.SetRow(searchContainer, 0);
            contentGrid.Children.Add(searchContainer);

            // Results list - CRT TERMINAL STYLE
            resultsListBox = new ListBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(phosphorGreen),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2, 2, 2, 2)  // Minimal padding, compact
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(resultsListBox, ScrollBarVisibility.Disabled);

            // Clean list style - CRT TERMINAL
            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(phosphorGreen)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(2, 1, 2, 1)));  // Tight spacing
            itemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0)));  // No margins between items

            // Hover effect (darker green background)
            var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(darkGreen)));
            itemStyle.Triggers.Add(hoverTrigger);

            // Selection effect (full green background, black text for contrast)
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(phosphorGreen)));
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(Colors.Black)));  // Invert colors
            itemStyle.Triggers.Add(selectedTrigger);

            resultsListBox.ItemContainerStyle = itemStyle;
            resultsListBox.PreviewKeyDown += OnResultsKeyDown;

            Grid.SetRow(resultsListBox, 1);
            contentGrid.Children.Add(resultsListBox);

            // Status bar - CRT TERMINAL STYLE
            var statusBar = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(phosphorGreen),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(4, 2, 4, 2)  // Minimal padding
            };

            statusText = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(phosphorGreen),
                Text = "↑↓ Navigate | Enter Execute | Esc Close"  // Shorter, terminal-style
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

            // Subscribe to theme changes
            themeManager.ThemeChanged += OnThemeChanged;

            // Subscribe to EventBus for cross-pane communication
            refreshRequestedHandler = OnRefreshRequested;
            eventBus.Subscribe(refreshRequestedHandler);

            // Removed Loaded event - only fires once, doesn't work for subsequent opens
            // Focus is now set explicitly via FocusSearchBox() method called by MainWindow
        }

        /// <summary>
        /// Build palette items from available panes and commands
        /// </summary>
        private void BuildPaletteItems()
        {
            allItems.Clear();

            // Add panes from PaneFactory (excluding hidden ones)
            var paneTypes = paneFactory.GetPaletteVisiblePaneTypes();
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
            // First try to get description from PaneFactory metadata
            var metadata = paneFactory.GetPaneMetadata(paneType);
            if (metadata != null && !string.IsNullOrEmpty(metadata.Description))
            {
                return metadata.Description;
            }

            // Fallback to hardcoded descriptions for legacy panes with extra shortcuts
            return paneType.ToLower() switch
            {
                "tasks" => "Manage tasks - A:Add S:Subtask E:Edit D:Delete Space:Toggle Shift+D:Date Shift+T:Tags",
                "notes" => "Browse and edit notes - A:New E:Edit D:Delete S:Search F:Filter O:OpenFile",
                "projects" => "Manage projects - A:Add D:Delete K:SetContext X:ExportT2020 Click:Edit",
                "excel-import" => "Import from Excel - I:Import (paste SVI-CAS W3:W130)",
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
            var theme = themeManager.CurrentTheme;
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            // Icon
            var icon = new TextBlock
            {
                Text = item.Icon,
                FontSize = 14,
                Width = 30,
                Foreground = new SolidColorBrush(theme.Primary),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(icon);

            // Name with highlighting
            var nameText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
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

            // Description (muted - use ForegroundSecondary or calculate)
            var mutedFg = theme.ForegroundSecondary != default(Color)
                ? new SolidColorBrush(theme.ForegroundSecondary)
                : new SolidColorBrush(Color.FromRgb(
                    (byte)(theme.Foreground.R * 0.7),
                    (byte)(theme.Foreground.G * 0.7),
                    (byte)(theme.Foreground.B * 0.7)
                ));

            var description = new TextBlock
            {
                Text = item.Description,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Foreground = mutedFg,
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
            var theme = themeManager.CurrentTheme;
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
                    run.Foreground = new SolidColorBrush(theme.Primary); // Use primary/accent color for highlights
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
                    Result = ModalResult.Cancel;
                    CloseRequested?.Invoke(this, new ModalClosedEventArgs(Result));
                    e.Handled = true;
                    break;

                case Key.Down:
                    // Move to results list
                    if (resultsListBox.Items.Count > 0)
                    {
                        System.Windows.Input.Keyboard.Focus(resultsListBox);
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
                    Result = ModalResult.Cancel;
                    CloseRequested?.Invoke(this, new ModalClosedEventArgs(Result));
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
                        System.Windows.Input.Keyboard.Focus(searchBox);
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

                        // Publish event for pane coordination
                        eventBus.Publish(new Core.Events.CommandExecutedFromPaletteEvent
                        {
                            CommandName = item.Name,
                            CommandCategory = "Pane",
                            ExecutedAt = DateTime.Now
                        });

                        Result = ModalResult.OK;
                        CloseRequested?.Invoke(this, new ModalClosedEventArgs(Result));
                        break;

                    case PaletteItemType.Command:
                        ExecuteSystemCommand(item.Name, fullCommand);

                        // Publish event for command coordination
                        eventBus.Publish(new Core.Events.CommandExecutedFromPaletteEvent
                        {
                            CommandName = item.Name,
                            CommandCategory = "System",
                            ExecutedAt = DateTime.Now
                        });
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
                    Result = ModalResult.OK;
                    CloseRequested?.Invoke(this, new ModalClosedEventArgs(Result));
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
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                statusText.Text = message;
            });
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                ApplyTheme();
            });
        }

        private void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            var bgBrush = new SolidColorBrush(theme.Background);
            var fgBrush = new SolidColorBrush(theme.Foreground);
            var surfaceBrush = new SolidColorBrush(theme.Surface);
            var borderActiveBrush = new SolidColorBrush(theme.BorderActive);

            // Update all controls
            if (overlayBorder != null)
            {
                overlayBorder.Background = new SolidColorBrush(Color.FromArgb(204, theme.Background.R, theme.Background.G, theme.Background.B));
            }

            if (searchBox != null)
            {
                searchBox.Foreground = fgBrush;
                searchBox.Background = Brushes.Transparent;
            }

            if (resultsListBox != null)
            {
                resultsListBox.Foreground = fgBrush;
                resultsListBox.Background = Brushes.Transparent;
            }

            this.InvalidateVisual();
        }

        protected override void OnDispose()
        {
            // Unsubscribe from EventBus to prevent memory leaks
            if (refreshRequestedHandler != null)
            {
                eventBus.Unsubscribe(refreshRequestedHandler);
                refreshRequestedHandler = null;
            }

            // Unsubscribe from theme changes
            themeManager.ThemeChanged -= OnThemeChanged;

            // Clean up if needed
            base.OnDispose();
        }

        /// <summary>
        /// Handle RefreshRequestedEvent - rebuild palette items
        /// </summary>
        private void OnRefreshRequested(Core.Events.RefreshRequestedEvent evt)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() =>
            {
                RefreshCommands();
                Log("CommandPalettePane refreshed (RefreshRequestedEvent)");
            });
        }

        /// <summary>
        /// Refresh commands by rebuilding palette items
        /// </summary>
        private void RefreshCommands()
        {
            BuildPaletteItems();
            RefreshResults(searchBox?.Text?.Trim() ?? "");
        }

        // IModal interface methods
        public void Show()
        {
            AnimateOpen();
            if (searchBox != null) System.Windows.Input.Keyboard.Focus(searchBox);
        }

        public void Hide()
        {
            AnimateClose(null);
        }

        public bool OnEscape()
        {
            Result = ModalResult.Cancel;
            return true; // Allow close
        }

        public bool OnEnter()
        {
            ExecuteSelected();
            Result = ModalResult.OK;
            return true; // Allow close
        }

        /// <summary>
        /// Focus search box explicitly
        /// Called by MainWindow every time palette opens (not just first time)
        /// </summary>
        public void FocusSearchBox()
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() =>
            {
                if (searchBox != null && searchBox.IsLoaded)
                {
                    System.Windows.Input.Keyboard.Focus(searchBox);
                    searchBox.SelectAll(); // Select any previous search text for easy replacement
                    logger.Log(LogLevel.Debug, PaneName, "Search box focused and text selected");
                }
                else if (searchBox != null)
                {
                    // Not loaded yet, wait for it
                    logger.Log(LogLevel.Debug, PaneName, "Search box not loaded, waiting for Loaded event");
                    RoutedEventHandler handler = null;
                    handler = (s, e) =>
                    {
                        searchBox.Loaded -= handler;
                        System.Windows.Input.Keyboard.Focus(searchBox);
                        searchBox.SelectAll();
                        logger.Log(LogLevel.Debug, PaneName, "Search box focused after load");
                    };
                    searchBox.Loaded += handler;
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// Animate palette opening
        /// </summary>
        public void AnimateOpen()
        {
            if (overlayBorder == null || paletteBox == null)
            {
                logger.Log(LogLevel.Warning, PaneName, "Cannot animate - UI not built yet");
                return;
            }

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
            if (overlayBorder == null)
            {
                logger.Log(LogLevel.Warning, PaneName, "Cannot animate close - UI not built yet");
                onComplete?.Invoke();
                return;
            }

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
