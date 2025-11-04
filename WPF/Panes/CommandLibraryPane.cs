using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Commands;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Command Library Pane - Searchable clipboard manager for command snippets
    /// Search, filter, and copy commands to clipboard with Enter
    /// </summary>
    public class CommandLibraryPane : PaneBase
    {
        private readonly IConfigurationManager config;
        private readonly Core.Commands.CommandService commandService;

        // UI Components
        private TextBox searchBox;
        private ListBox commandListBox;
        private TextBlock detailPanel;
        private TextBlock statusBar;

        // State
        private List<Core.Commands.Command> allCommands = new List<Core.Commands.Command>();
        private List<Core.Commands.Command> filteredCommands = new List<Core.Commands.Command>();
        private Core.Commands.Command selectedCommand;

        // Theme colors
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush surfaceBrush;
        private SolidColorBrush borderBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush dimBrush;
        private SolidColorBrush successBrush;

        #region Constructor

        public CommandLibraryPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager config)
            : base(logger, themeManager, projectContext)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            // Initialize CommandService with logger
            var legacyLogger = logger as Logger ?? Logger.Instance;
            this.commandService = new Core.Commands.CommandService(legacyLogger);

            PaneName = "Commands";
            PaneIcon = "💾";
        }

        #endregion

        #region Initialization

        public override void Initialize()
        {
            base.Initialize();

            CacheThemeColors();
            RegisterPaneShortcuts();
            LoadCommands();

            // Focus the list by default so shortcuts work immediately
            this.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.Input.Keyboard.Focus(commandListBox);
                if (commandListBox.Items.Count > 0)
                    commandListBox.SelectedIndex = 0;
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override UIElement BuildContent()
        {
            return BuildUI();
        }

        private void CacheThemeColors()
        {
            var theme = themeManager.CurrentTheme;
            bgBrush = new SolidColorBrush(theme.Background);
            fgBrush = new SolidColorBrush(theme.Foreground);
            surfaceBrush = new SolidColorBrush(theme.Surface);
            borderBrush = new SolidColorBrush(theme.Border);
            accentBrush = new SolidColorBrush(theme.Primary);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
            successBrush = new SolidColorBrush(theme.Success);
        }

        private Grid BuildUI()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150) }); // Detail
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status

            // Search box
            searchBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                Background = surfaceBrush,
                Foreground = fgBrush,
                CaretBrush = fgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8),
                Text = "Search commands... (t:tag, +term1 +term2)"
            };
            searchBox.GotFocus += (s, e) =>
            {
                if (searchBox.Text.StartsWith("Search"))
                    searchBox.Text = "";
            };
            searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(searchBox.Text))
                    searchBox.Text = "Search commands... (t:tag, +term1 +term2)";
            };
            searchBox.TextChanged += OnSearchTextChanged;
            searchBox.KeyDown += SearchBox_KeyDown;
            Grid.SetRow(searchBox, 0);
            grid.Children.Add(searchBox);

            // Command list
            commandListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Background = bgBrush,
                Foreground = fgBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            commandListBox.SelectionChanged += OnCommandSelectionChanged;
            commandListBox.KeyDown += CommandListBox_KeyDown;
            Grid.SetRow(commandListBox, 1);
            grid.Children.Add(commandListBox);

            // Detail panel
            detailPanel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 13,
                Foreground = dimBrush,
                Background = surfaceBrush,
                Padding = new Thickness(12),
                TextWrapping = TextWrapping.Wrap,
                Text = "Select a command to see details"
            };
            Grid.SetRow(detailPanel, 2);
            grid.Children.Add(detailPanel);

            // Status bar
            statusBar = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Background = surfaceBrush,
                Padding = new Thickness(8, 4, 8, 4),
                Text = GetStatusBarText()
            };
            Grid.SetRow(statusBar, 3);
            grid.Children.Add(statusBar);

            return grid;
        }

        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;

            shortcuts.RegisterForPane(PaneName, Key.Enter, ModifierKeys.None,
                () => CopySelectedCommand(),
                "Copy command to clipboard");

            shortcuts.RegisterForPane(PaneName, Key.A, ModifierKeys.None,
                () => ShowAddDialog(),
                "Add new command");

            shortcuts.RegisterForPane(PaneName, Key.E, ModifierKeys.None,
                () => { if (selectedCommand != null) ShowEditDialog(); },
                "Edit selected command");

            shortcuts.RegisterForPane(PaneName, Key.D, ModifierKeys.None,
                () => { if (selectedCommand != null) DeleteSelectedCommand(); },
                "Delete selected command");

            shortcuts.RegisterForPane(PaneName, Key.Escape, ModifierKeys.None,
                () => { searchBox.Text = ""; System.Windows.Input.Keyboard.Focus(searchBox); },
                "Clear search");
        }

        #endregion

        #region Data Loading

        private void LoadCommands()
        {
            try
            {
                allCommands = commandService.GetAllCommands();
                FilterCommands();
                Log($"Loaded {allCommands.Count} commands");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    "Loading commands",
                    logger);
                ShowStatus("Failed to load commands", isError: true);
            }
        }

        private void FilterCommands()
        {
            var query = searchBox.Text;

            // If search box has placeholder text, show all
            if (string.IsNullOrWhiteSpace(query) || query.StartsWith("Search"))
            {
                filteredCommands = allCommands.ToList();
            }
            else
            {
                // Use CommandService search with advanced syntax
                filteredCommands = commandService.SearchCommands(query);
            }

            UpdateCommandList();
        }

        private void UpdateCommandList()
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                commandListBox.Items.Clear();

                if (!filteredCommands.Any())
                {
                    var placeholder = new TextBlock
                    {
                        Text = allCommands.Any()
                            ? "No commands match your search"
                            : "Press 'A' to add your first command",
                        Foreground = dimBrush,
                        Padding = new Thickness(12),
                        FontStyle = FontStyles.Italic
                    };
                    commandListBox.Items.Add(placeholder);
                    detailPanel.Text = "";
                    return;
                }

                foreach (var cmd in filteredCommands)
                {
                    var item = CreateCommandListItem(cmd);
                    commandListBox.Items.Add(item);
                }

                // Select first item
                if (commandListBox.Items.Count > 0)
                {
                    commandListBox.SelectedIndex = 0;
                }

                UpdateStatusBar();
            });
        }

        private Border CreateCommandListItem(Core.Commands.Command cmd)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Title
            var title = new TextBlock
            {
                Text = cmd.Title,
                Foreground = fgBrush,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(title, 0);
            grid.Children.Add(title);

            // Tags
            if (cmd.Tags != null && cmd.Tags.Length > 0)
            {
                var tags = new TextBlock
                {
                    Text = "t:" + string.Join(",", cmd.Tags),
                    Foreground = accentBrush,
                    FontSize = 12
                };
                Grid.SetColumn(tags, 1);
                grid.Children.Add(tags);
            }

            var border = new Border
            {
                Child = grid,
                Padding = new Thickness(12, 8, 12, 8),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = surfaceBrush,
                Tag = cmd // Store command for selection
            };

            return border;
        }

        #endregion

        #region Event Handlers

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            FilterCommands();
        }

        private void OnCommandSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (commandListBox.SelectedItem is Border border && border.Tag is Core.Commands.Command cmd)
            {
                selectedCommand = cmd;
                ShowCommandDetails(cmd);
            }
            else
            {
                selectedCommand = null;
                detailPanel.Text = "Select a command to see details";
            }
        }

        private void CommandListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Don't handle shortcuts if user is typing in the search box
            if (Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is TextBox)
                return;

            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
                e.Handled = true;
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                searchBox.Text = "";
                System.Windows.Input.Keyboard.Focus(commandListBox);
                e.Handled = true;
            }
            else if (e.Key == Key.Down || e.Key == Key.Up)
            {
                // Allow arrow keys to move focus to list
                System.Windows.Input.Keyboard.Focus(commandListBox);
                if (commandListBox.Items.Count > 0 && commandListBox.SelectedIndex == -1)
                    commandListBox.SelectedIndex = 0;
                e.Handled = true;
            }
        }

        private void ShowCommandDetails(Core.Commands.Command cmd)
        {
            var used = cmd.UseCount > 0
                ? $"Used: {cmd.UseCount} times, last: {cmd.LastUsed:yyyy-MM-dd HH:mm}"
                : "Never used";

            detailPanel.Text = $"Command: {cmd.CommandText}\n\n" +
                             $"Description: {cmd.Description}\n" +
                             $"Tags: {string.Join(", ", cmd.Tags ?? Array.Empty<string>())}\n" +
                             $"{used}";
        }

        #endregion

        #region Command Actions

        private void CopySelectedCommand()
        {
            if (selectedCommand == null)
            {
                ShowStatus("No command selected", isError: true);
                return;
            }

            try
            {
                commandService.CopyToClipboard(selectedCommand.Id);
                ShowStatus($"Copied to clipboard: {selectedCommand.Title}", isError: false);
                Log($"Copied command: {selectedCommand.Title}");

                // Refresh to show updated usage count
                LoadCommands();
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Internal,
                    ex,
                    "Copying command to clipboard",
                    logger);
                ShowStatus("Failed to copy to clipboard", isError: true);
            }
        }

        private void ShowAddDialog()
        {
            var dialog = CreateCommandDialog(null);
            if (dialog.ShowDialog() == true && dialog.Tag is Core.Commands.Command newCmd)
            {
                try
                {
                    commandService.AddCommand(newCmd);
                    LoadCommands();
                    ShowStatus($"Added: {newCmd.Title}", isError: false);
                    Log($"Added command: {newCmd.Title}");
                }
                catch (Exception ex)
                {
                    ErrorHandlingPolicy.Handle(
                        ErrorCategory.IO,
                        ex,
                        "Adding command",
                        logger);
                    ShowStatus("Failed to add command", isError: true);
                }
            }
        }

        private void ShowEditDialog()
        {
            if (selectedCommand == null) return;

            var dialog = CreateCommandDialog(selectedCommand);
            if (dialog.ShowDialog() == true && dialog.Tag is Core.Commands.Command updatedCmd)
            {
                try
                {
                    commandService.UpdateCommand(updatedCmd);
                    LoadCommands();
                    ShowStatus($"Updated: {updatedCmd.Title}", isError: false);
                    Log($"Updated command: {updatedCmd.Title}");
                }
                catch (Exception ex)
                {
                    ErrorHandlingPolicy.Handle(
                        ErrorCategory.IO,
                        ex,
                        "Updating command",
                        logger);
                    ShowStatus("Failed to update command", isError: true);
                }
            }
        }

        private void DeleteSelectedCommand()
        {
            if (selectedCommand == null) return;

            try
            {
                var title = selectedCommand.Title;
                commandService.DeleteCommand(selectedCommand.Id);
                LoadCommands();
                ShowStatus($"Deleted: {title}", isError: false);
                Log($"Deleted command: {title}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    "Deleting command",
                    logger);
                ShowStatus("Failed to delete command", isError: true);
            }
        }

        #endregion

        #region Dialog

        private Window CreateCommandDialog(Core.Commands.Command existing)
        {
            var isEdit = existing != null;
            var cmd = existing ?? new Core.Commands.Command();

            var window = new Window
            {
                Title = isEdit ? "Edit Command" : "Add Command",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = surfaceBrush,
                Owner = Window.GetWindow(this)
            };

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title
            var titleLabel = new TextBlock { Text = "Title:", Foreground = fgBrush, Margin = new Thickness(0, 0, 0, 4) };
            Grid.SetRow(titleLabel, 0);
            grid.Children.Add(titleLabel);

            var titleBox = new TextBox
            {
                Text = cmd.Title,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                Background = bgBrush,
                Foreground = fgBrush,
                CaretBrush = fgBrush,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(titleBox, 0);
            titleBox.Margin = new Thickness(0, 20, 0, 12);
            grid.Children.Add(titleBox);

            // Description
            var descLabel = new TextBlock { Text = "Description:", Foreground = fgBrush, Margin = new Thickness(0, 0, 0, 4) };
            Grid.SetRow(descLabel, 1);
            grid.Children.Add(descLabel);

            var descBox = new TextBox
            {
                Text = cmd.Description,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                Background = bgBrush,
                Foreground = fgBrush,
                CaretBrush = fgBrush,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 20, 0, 12)
            };
            Grid.SetRow(descBox, 1);
            grid.Children.Add(descBox);

            // Command text
            var cmdLabel = new TextBlock { Text = "Command:", Foreground = fgBrush, Margin = new Thickness(0, 0, 0, 4) };
            Grid.SetRow(cmdLabel, 2);
            grid.Children.Add(cmdLabel);

            var cmdBox = new TextBox
            {
                Text = cmd.CommandText,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                Background = bgBrush,
                Foreground = fgBrush,
                CaretBrush = fgBrush,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 20, 0, 12)
            };
            Grid.SetRow(cmdBox, 2);
            grid.Children.Add(cmdBox);

            // Tags
            var tagsLabel = new TextBlock { Text = "Tags (comma-separated):", Foreground = fgBrush, Margin = new Thickness(0, 0, 0, 4) };
            Grid.SetRow(tagsLabel, 3);
            grid.Children.Add(tagsLabel);

            var tagsBox = new TextBox
            {
                Text = cmd.Tags != null ? string.Join(", ", cmd.Tags) : "",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                Background = bgBrush,
                Foreground = fgBrush,
                CaretBrush = fgBrush,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 20, 0, 12)
            };
            Grid.SetRow(tagsBox, 3);
            grid.Children.Add(tagsBox);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var saveButton = new Button
            {
                Content = "Save",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Background = accentBrush,
                Foreground = bgBrush
            };
            saveButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(cmdBox.Text))
                {
                    MessageBox.Show("Command text is required", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                cmd.Title = titleBox.Text.Trim();
                cmd.Description = descBox.Text.Trim();
                cmd.CommandText = cmdBox.Text.Trim();
                cmd.Tags = tagsBox.Text.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToArray();

                window.Tag = cmd;
                window.DialogResult = true;
            };
            buttonPanel.Children.Add(saveButton);

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Background = surfaceBrush,
                Foreground = fgBrush
            };
            cancelButton.Click += (s, e) => window.DialogResult = false;
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 4);
            grid.Children.Add(buttonPanel);

            // Keyboard navigation: Enter advances fields, Ctrl+Enter saves, Escape cancels
            titleBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    System.Windows.Input.Keyboard.Focus(descBox);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    window.DialogResult = false;
                }
            };

            descBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    System.Windows.Input.Keyboard.Focus(cmdBox);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    window.DialogResult = false;
                }
            };

            cmdBox.KeyDown += (s, e) =>
            {
                // Ctrl+Enter to save (don't interfere with normal Enter for multiline text)
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    window.DialogResult = false;
                }
                // Tab to move to tags (default behavior)
            };

            tagsBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    window.DialogResult = false;
                }
            };

            // Auto-focus title field on load
            window.Loaded += (s, e) => System.Windows.Input.Keyboard.Focus(titleBox);

            window.Content = grid;
            return window;
        }

        #endregion

        #region Status and Helpers

        private void ShowStatus(string message, bool isError)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                statusBar.Text = message;
                statusBar.Foreground = isError ? new SolidColorBrush(themeManager.CurrentTheme.Error) : dimBrush;

                // Reset to normal after 3 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    UpdateStatusBar();
                };
                timer.Start();
            });
        }

        private void UpdateStatusBar()
        {
            statusBar.Text = GetStatusBarText();
            statusBar.Foreground = dimBrush;
        }

        private string GetStatusBarText()
        {
            return $"{allCommands.Count} commands | Enter:Copy A:Add E:Edit D:Delete Esc:ClearSearch";
        }

        private void Log(string message)
        {
            logger?.Log(LogLevel.Info, PaneName, message);
        }

        #endregion

        #region Theme

        private void OnThemeChanged(object sender, EventArgs e)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                CacheThemeColors();
                // Would need to rebuild UI to apply theme - simplified for now
            });
        }

        #endregion

        #region Cleanup

        protected override void OnDispose()
        {
            // Clean up event handlers
            if (searchBox != null)
                searchBox.TextChanged -= OnSearchTextChanged;

            if (commandListBox != null)
                commandListBox.SelectionChanged -= OnCommandSelectionChanged;

            base.OnDispose();
        }

        #endregion
    }
}
