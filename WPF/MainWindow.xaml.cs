using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core.Commands;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Services;
using SuperTUI.DI;
using SuperTUI.Infrastructure;
using SuperTUI.Widgets;
using SuperTUI.Core;

namespace SuperTUI
{
    /// <summary>
    /// MainWindow with blank canvas + auto-tiling panes
    /// i3-style keyboard-driven workflow
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DI.ServiceContainer serviceContainer;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IProjectContextManager projectContext;
        private readonly IStatePersistenceManager statePersistence;
        private readonly CommandHistory commandHistory;

        private PaneManager paneManager;
        private PaneFactory paneFactory;
        private StatusBarWidget statusBar;
        private PaneWorkspaceManager workspaceManager;
        private Panes.CommandPalettePane commandPalette;
        private Core.Components.DebugOverlay debugOverlay;
        private bool isDebugMode = false;

        public MainWindow(DI.ServiceContainer container)
        {
            serviceContainer = container ?? throw new ArgumentNullException(nameof(container));

            // Get services
            logger = serviceContainer.GetRequiredService<ILogger>();
            themeManager = serviceContainer.GetRequiredService<IThemeManager>();
            projectContext = serviceContainer.GetRequiredService<IProjectContextManager>();
            statePersistence = serviceContainer.GetRequiredService<IStatePersistenceManager>();
            commandHistory = serviceContainer.GetRequiredService<CommandHistory>();

            InitializeComponent();

            // Apply terminal theme
            themeManager.ApplyTheme("Terminal");

            // Apply theme colors to MainWindow
            var theme = themeManager.CurrentTheme;
            this.Background = new SolidColorBrush(theme.Background);
            RootContainer.Background = new SolidColorBrush(theme.Background);
            PaneCanvas.Background = new SolidColorBrush(theme.Background);
            StatusBarContainer.Background = new SolidColorBrush(theme.Surface);
            StatusBarContainer.BorderBrush = new SolidColorBrush(theme.Border);

            // Initialize components
            InitializeWorkspaceManager();
            InitializePaneSystem();
            InitializeStatusBar();

            // Restore full application state from disk (with checksum validation)
            RestoreApplicationState();

            // Load current workspace state
            RestoreWorkspaceState();

            // Register all keyboard shortcuts
            RegisterAllShortcuts();

            // Keyboard handlers
            this.KeyDown += MainWindow_KeyDown;
            Closing += MainWindow_Closing;

            logger.Log(LogLevel.Info, "MainWindow", "Initialized with blank canvas");
        }

        private void InitializeWorkspaceManager()
        {
            workspaceManager = new PaneWorkspaceManager(logger);
            workspaceManager.WorkspaceChanged += OnWorkspaceChanged;
            logger.Log(LogLevel.Info, "MainWindow", $"Workspace manager initialized (current: {workspaceManager.CurrentWorkspaceIndex})");
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangedEventArgs e)
        {
            // Save old workspace state
            SaveCurrentWorkspaceState();

            // Restore new workspace state
            RestoreWorkspaceState();

            // Update status bar workspace indicator
            statusBar?.UpdateWorkspaceIndicator(e.NewIndex);

            logger.Log(LogLevel.Info, "MainWindow", $"Switched from workspace {e.OldIndex} to {e.NewIndex}");
        }

        /// <summary>
        /// Register all keyboard shortcuts with ShortcutManager
        /// Centralizes shortcut management instead of hardcoded KeyDown checks
        /// </summary>
        private void RegisterAllShortcuts()
        {
            var shortcuts = serviceContainer.GetRequiredService<IShortcutManager>();

            // Help overlay: ? (Shift+/)
            shortcuts.RegisterGlobal(Key.OemQuestion, ModifierKeys.Shift,
                () => ShowHelpOverlay(),
                "Show help overlay");

            // Command palette: : (Shift+;)
            shortcuts.RegisterGlobal(Key.OemSemicolon, ModifierKeys.Shift,
                () => ShowCommandPalette(),
                "Open command palette");

            // Toggle move pane mode: F12
            shortcuts.RegisterGlobal(Key.F12, ModifierKeys.None,
                () => ToggleMovePaneMode(),
                "Toggle move pane mode");

            // Toggle debug overlay: Ctrl+Shift+D
            shortcuts.RegisterGlobal(Key.D, ModifierKeys.Control | ModifierKeys.Shift,
                () => ToggleDebugMode(),
                "Toggle debug overlay");

            // Workspace switching: Ctrl+1-9
            for (int i = 1; i <= 9; i++)
            {
                int workspace = i; // Capture for closure
                shortcuts.RegisterGlobal((Key)((int)Key.D1 + i - 1), ModifierKeys.Control,
                    () => workspaceManager.SwitchToWorkspace(workspace - 1),
                    $"Switch to workspace {workspace}");
            }

            // Pane navigation: Ctrl+Shift+Arrows (only when not typing)
            shortcuts.RegisterGlobal(Key.Left, ModifierKeys.Control | ModifierKeys.Shift,
                () => { if (!IsTypingInTextBox()) paneManager.NavigateFocus(FocusDirection.Left); },
                "Focus pane left");

            shortcuts.RegisterGlobal(Key.Right, ModifierKeys.Control | ModifierKeys.Shift,
                () => { if (!IsTypingInTextBox()) paneManager.NavigateFocus(FocusDirection.Right); },
                "Focus pane right");

            shortcuts.RegisterGlobal(Key.Up, ModifierKeys.Control | ModifierKeys.Shift,
                () => { if (!IsTypingInTextBox()) paneManager.NavigateFocus(FocusDirection.Up); },
                "Focus pane up");

            shortcuts.RegisterGlobal(Key.Down, ModifierKeys.Control | ModifierKeys.Shift,
                () => { if (!IsTypingInTextBox()) paneManager.NavigateFocus(FocusDirection.Down); },
                "Focus pane down");

            // Pane opening shortcuts: Ctrl+Shift+Key
            shortcuts.RegisterGlobal(Key.T, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("tasks"),
                "Open Tasks pane");

            shortcuts.RegisterGlobal(Key.N, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("notes"),
                "Open Notes pane");

            shortcuts.RegisterGlobal(Key.P, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("projects"),
                "Open Projects pane");

            shortcuts.RegisterGlobal(Key.E, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("excel-import"),
                "Open Excel Import pane");

            shortcuts.RegisterGlobal(Key.F, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("files"),
                "Open Files pane");

            shortcuts.RegisterGlobal(Key.C, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("calendar"),
                "Open Calendar pane");

            // Close focused pane: Ctrl+Shift+Q
            shortcuts.RegisterGlobal(Key.Q, ModifierKeys.Control | ModifierKeys.Shift,
                () => paneManager.CloseFocusedPane(),
                "Close focused pane");

            // Undo: Ctrl+Z
            shortcuts.RegisterGlobal(Key.Z, ModifierKeys.Control,
                () => UndoLastCommand(),
                "Undo last command");

            // Redo: Ctrl+Y
            shortcuts.RegisterGlobal(Key.Y, ModifierKeys.Control,
                () => RedoLastCommand(),
                "Redo last command");

            logger.Log(LogLevel.Info, "MainWindow", "Registered all keyboard shortcuts with ShortcutManager");
        }

        /// <summary>
        /// Check if user is currently typing in a text control
        /// Used to prevent navigation shortcuts from interfering with text editing
        /// </summary>
        private bool IsTypingInTextBox()
        {
            return Keyboard.FocusedElement is TextBox ||
                   Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase;
        }

        /// <summary>
        /// Toggle move pane mode on/off with visual feedback
        /// </summary>
        private void ToggleMovePaneMode()
        {
            isMovePaneMode = !isMovePaneMode;
            if (isMovePaneMode)
            {
                ShowMovePaneModeOverlay();
            }
            else
            {
                HideMovePaneModeOverlay();
            }
            logger.Log(LogLevel.Info, "MainWindow", $"Move pane mode: {(isMovePaneMode ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Undo last command (Ctrl+Z)
        /// </summary>
        private void UndoLastCommand()
        {
            if (!commandHistory.CanUndo)
            {
                logger.Log(LogLevel.Debug, "MainWindow", "Nothing to undo");
                return;
            }

            try
            {
                var description = commandHistory.GetUndoDescription();
                commandHistory.Undo();
                logger.Log(LogLevel.Info, "MainWindow", $"Undone: {description}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Failed to undo: {ex.Message}", ex);
                MessageBox.Show($"Failed to undo: {ex.Message}", "Undo Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Redo last undone command (Ctrl+Y)
        /// </summary>
        private void RedoLastCommand()
        {
            if (!commandHistory.CanRedo)
            {
                logger.Log(LogLevel.Debug, "MainWindow", "Nothing to redo");
                return;
            }

            try
            {
                var description = commandHistory.GetRedoDescription();
                commandHistory.Redo();
                logger.Log(LogLevel.Info, "MainWindow", $"Redone: {description}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Failed to redo: {ex.Message}", ex);
                MessageBox.Show($"Failed to redo: {ex.Message}", "Redo Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCurrentWorkspaceState()
        {
            var state = workspaceManager.CurrentWorkspace;

            // Save pane state
            var paneState = paneManager.GetState();
            state.OpenPaneTypes = paneState.OpenPaneTypes;
            state.FocusedPaneIndex = paneState.FocusedPaneIndex;

            // Save project context
            if (projectContext.CurrentProject != null)
            {
                state.CurrentProjectId = projectContext.CurrentProject.Id;
            }
            else
            {
                state.CurrentProjectId = null;
            }

            workspaceManager.UpdateCurrentWorkspace(state);
            logger.Log(LogLevel.Debug, "MainWindow", $"Saved workspace {state.Index} state ({state.OpenPaneTypes.Count} panes)");
        }

        private void RestoreApplicationState()
        {
            try
            {
                var snapshot = statePersistence.LoadStateAsync().Result;
                if (snapshot != null)
                {
                    // Validate checksum
                    if (snapshot.VerifyChecksum())
                    {
                        statePersistence.RestoreState(snapshot, workspaceManager);
                        logger.Log(LogLevel.Info, "MainWindow", "Application state restored from disk");
                    }
                    else
                    {
                        logger.Log(LogLevel.Warning, "MainWindow", "State file checksum invalid, skipping restore");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, "MainWindow", $"Failed to restore application state: {ex.Message}");
            }
        }

        private void RestoreWorkspaceState()
        {
            var state = workspaceManager.CurrentWorkspace;

            // Close all current panes
            paneManager.CloseAll();

            // Restore project context
            if (state.CurrentProjectId.HasValue)
            {
                var project = serviceContainer.GetRequiredService<IProjectService>()
                    .GetAllProjects()
                    .FirstOrDefault(p => p.Id == state.CurrentProjectId.Value);

                if (project != null)
                {
                    projectContext.SetProject(project);
                }
            }
            else
            {
                projectContext.ClearProject();
            }

            // Restore panes
            var panesToRestore = new List<Core.Components.PaneBase>();
            foreach (var paneTypeName in state.OpenPaneTypes)
            {
                try
                {
                    var pane = paneFactory.CreatePane(paneTypeName);
                    panesToRestore.Add(pane);
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Warning, "MainWindow", $"Failed to restore pane '{paneTypeName}': {ex.Message}");
                }
            }

            if (panesToRestore.Count > 0)
            {
                var paneState = new PaneManagerState
                {
                    OpenPaneTypes = state.OpenPaneTypes,
                    FocusedPaneIndex = state.FocusedPaneIndex
                };
                paneManager.RestoreState(paneState, panesToRestore);
            }

            UpdateStatusBarContext();
            logger.Log(LogLevel.Info, "MainWindow", $"Restored workspace {state.Index} ({panesToRestore.Count} panes)");
        }


        private void InitializePaneSystem()
        {
            // Create pane manager (blank canvas)
            paneManager = new PaneManager(logger, themeManager);

            // Check if container is already added (prevents "logical parent" error)
            if (!PaneCanvas.Children.Contains(paneManager.Container))
            {
                PaneCanvas.Children.Add(paneManager.Container);
            }

            // Create pane factory
            paneFactory = new PaneFactory(
                logger,
                themeManager,
                projectContext,
                serviceContainer.GetRequiredService<IConfigurationManager>(),
                serviceContainer.GetRequiredService<ISecurityManager>(),
                serviceContainer.GetRequiredService<ITaskService>(),
                serviceContainer.GetRequiredService<IProjectService>(),
                serviceContainer.GetRequiredService<ITimeTrackingService>(),
                serviceContainer.GetRequiredService<ITagService>(),
                serviceContainer.GetRequiredService<IEventBus>(),
                commandHistory
            );

            // Subscribe to pane events
            paneManager.PaneFocusChanged += OnPaneFocusChanged;

            logger.Log(LogLevel.Info, "MainWindow", "Pane system initialized (blank canvas)");
        }

        private void InitializeStatusBar()
        {
            statusBar = new StatusBarWidget(
                logger,
                themeManager,
                projectContext,
                serviceContainer.GetRequiredService<ITimeTrackingService>(),
                serviceContainer.GetRequiredService<ITaskService>(),
                serviceContainer.GetRequiredService<IConfigurationManager>()
            );
            statusBar.Initialize();

            // Check if status bar is already set (prevents "logical parent" error)
            if (StatusBarContainer.Child == null)
            {
                StatusBarContainer.Child = statusBar;
            }

            // Show welcome screen if no panes are open and user hasn't disabled it
            ShowWelcomeScreenIfNeeded();
        }

        /// <summary>
        /// Show welcome screen overlay when no panes are open
        /// FIX 1: Add welcome screen/onboarding
        /// </summary>
        private void ShowWelcomeScreenIfNeeded()
        {
            var config = serviceContainer.GetRequiredService<IConfigurationManager>();
            bool showWelcome = config.Get<bool>("UI.ShowWelcomeScreen", true);

            if (showWelcome && paneManager.OpenPanes.Count == 0)
            {
                ShowWelcomeOverlay();
            }
        }

        /// <summary>
        /// Display welcome overlay with quick-start instructions
        /// </summary>
        private void ShowWelcomeOverlay()
        {
            var theme = themeManager.CurrentTheme;

            // Create semi-transparent background
            var background = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, theme.Background.R, theme.Background.G, theme.Background.B))
            };

            // Create welcome content
            var contentPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 600
            };

            // Title
            var titleText = new TextBlock
            {
                Text = "Welcome to SuperTUI",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 30)
            };
            contentPanel.Children.Add(titleText);

            // Quick start instructions
            var instructionsText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = new SolidColorBrush(theme.Foreground),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 20),
                Text = "Quick Start:\n\n" +
                       "  ?                  Show help\n" +
                       "  :                  Open command palette\n" +
                       "  Ctrl+Shift+T       Open Tasks pane\n" +
                       "  Ctrl+Shift+N       Open Notes pane\n" +
                       "  Ctrl+Shift+F       Open Files pane\n" +
                       "  Ctrl+Shift+Arrows  Navigate between panes\n" +
                       "  Ctrl+1-9           Switch workspaces"
            };
            contentPanel.Children.Add(instructionsText);

            // Don't show again checkbox
            var checkboxPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 20, 0, 20)
            };

            var dontShowCheckbox = new CheckBox
            {
                Content = "Don't show this again",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                VerticalAlignment = VerticalAlignment.Center
            };
            checkboxPanel.Children.Add(dontShowCheckbox);
            contentPanel.Children.Add(checkboxPanel);

            // Close button
            var closeButton = new Button
            {
                Content = "Get Started",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(30, 10, 30, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(theme.Primary),
                Foreground = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            closeButton.Click += (s, e) =>
            {
                // Save preference if checkbox is checked
                if (dontShowCheckbox.IsChecked == true)
                {
                    var config = serviceContainer.GetRequiredService<IConfigurationManager>();
                    config.Set<bool>("UI.ShowWelcomeScreen", false, saveImmediately: true);
                    logger.Log(LogLevel.Info, "MainWindow", "Welcome screen disabled by user");
                }

                ModalOverlay.Visibility = Visibility.Collapsed;
                ModalOverlay.Children.Clear();
                logger.Log(LogLevel.Debug, "MainWindow", "Welcome screen closed");
            };

            contentPanel.Children.Add(closeButton);

            // Add to background
            background.Child = contentPanel;

            // Show in modal overlay
            ModalOverlay.Children.Clear();
            ModalOverlay.Children.Add(background);
            ModalOverlay.Visibility = Visibility.Visible;

            logger.Log(LogLevel.Info, "MainWindow", "Welcome screen displayed");
        }

        private void OnPaneFocusChanged(object sender, PaneEventArgs e)
        {
            UpdateStatusBarContext();
        }

        private void UpdateStatusBarContext()
        {
            // Note: StatusBarWidget updates automatically via project context events
            // No need to manually set context - it's handled by the widget itself
        }

        private bool isMovePaneMode = false;

        /// <summary>
        /// Main keyboard event handler - delegates to ShortcutManager
        /// Only handles context-specific keys (move pane mode arrows) that can't be pre-registered
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            var shortcuts = serviceContainer.GetRequiredService<IShortcutManager>();

            // Let ShortcutManager handle registered shortcuts first
            bool handled = shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers);

            if (handled)
            {
                e.Handled = true;
                return;
            }

            // Handle context-specific shortcuts (move pane mode arrows)
            // These can't be pre-registered because they depend on isMovePaneMode state
            if (isMovePaneMode && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        paneManager.MovePane(FocusDirection.Left);
                        e.Handled = true;
                        return;
                    case Key.Right:
                        paneManager.MovePane(FocusDirection.Right);
                        e.Handled = true;
                        return;
                    case Key.Up:
                        paneManager.MovePane(FocusDirection.Up);
                        e.Handled = true;
                        return;
                    case Key.Down:
                        paneManager.MovePane(FocusDirection.Down);
                        e.Handled = true;
                        return;
                    case Key.Escape:
                        isMovePaneMode = false;
                        HideMovePaneModeOverlay();
                        logger.Log(LogLevel.Info, "MainWindow", "Move pane mode: OFF");
                        e.Handled = true;
                        return;
                }
            }
        }


        /// <summary>
        /// Open a pane by type name
        /// </summary>
        private void OpenPane(string paneName)
        {
            try
            {
                var pane = paneFactory.CreatePane(paneName);
                paneManager.OpenPane(pane);
                logger.Log(LogLevel.Info, "MainWindow", $"Opened pane: {paneName}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Failed to open pane '{paneName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Show command palette as modal overlay
        /// </summary>
        private void ShowCommandPalette()
        {
            if (commandPalette == null)
            {
                // Create command palette
                commandPalette = new Panes.CommandPalettePane(
                    logger,
                    themeManager,
                    projectContext,
                    serviceContainer.GetRequiredService<IConfigurationManager>(),
                    paneFactory,
                    paneManager
                );

                commandPalette.Initialize();
                commandPalette.CloseRequested += (s, e) => HideCommandPalette();
            }

            // Clear modal overlay and ensure command palette has no parent (prevents "logical parent" error)
            ModalOverlay.Children.Clear();

            // Check if command palette is not already in the overlay
            if (!ModalOverlay.Children.Contains(commandPalette))
            {
                ModalOverlay.Children.Add(commandPalette);
            }

            ModalOverlay.Visibility = Visibility.Visible;

            // Animate opening
            commandPalette.AnimateOpen();

            logger.Log(LogLevel.Debug, "MainWindow", "Command palette opened");
        }

        /// <summary>
        /// Hide command palette modal overlay
        /// </summary>
        private void HideCommandPalette()
        {
            if (commandPalette != null && ModalOverlay.Visibility == Visibility.Visible)
            {
                // Animate closing
                commandPalette.AnimateClose(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ModalOverlay.Visibility = Visibility.Collapsed;
                        ModalOverlay.Children.Clear();
                    });
                });

                logger.Log(LogLevel.Debug, "MainWindow", "Command palette closed");
            }
        }

        private void ShowHelpOverlay()
        {
            OpenPane("help");
        }

        /// <summary>
        /// Show move pane mode visual feedback overlay
        /// FIX 2: Add visual feedback for F12 move mode
        /// </summary>
        private void ShowMovePaneModeOverlay()
        {
            var theme = themeManager.CurrentTheme;

            // Create semi-transparent background overlay
            var overlay = new Border
            {
                Name = "MovePaneModeOverlay",
                Background = new SolidColorBrush(Color.FromArgb(180, theme.Background.R, theme.Background.G, theme.Background.B)),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 60,
                BorderThickness = new Thickness(0, 0, 0, 2),
                BorderBrush = new SolidColorBrush(theme.Primary)
            };

            // Create instruction text
            var textPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var titleText = new TextBlock
            {
                Text = "MOVE PANE MODE",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            textPanel.Children.Add(titleText);

            var instructionText = new TextBlock
            {
                Text = "Use arrow keys to move panes | Press Esc or F12 to exit",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            textPanel.Children.Add(instructionText);

            overlay.Child = textPanel;

            // Add to modal overlay (don't clear - might have other overlays)
            // Remove existing move pane overlay if present
            var existingOverlay = ModalOverlay.Children.OfType<Border>().FirstOrDefault(b => b.Name == "MovePaneModeOverlay");
            if (existingOverlay != null)
            {
                ModalOverlay.Children.Remove(existingOverlay);
            }

            ModalOverlay.Children.Add(overlay);
            ModalOverlay.Visibility = Visibility.Visible;

            logger.Log(LogLevel.Debug, "MainWindow", "Move pane mode overlay shown");
        }

        /// <summary>
        /// Hide move pane mode visual feedback overlay
        /// </summary>
        private void HideMovePaneModeOverlay()
        {
            // Only remove the move pane mode overlay, keep other overlays
            var existingOverlay = ModalOverlay.Children.OfType<Border>().FirstOrDefault(b => b.Name == "MovePaneModeOverlay");
            if (existingOverlay != null)
            {
                ModalOverlay.Children.Remove(existingOverlay);

                // If no more overlays, hide the modal overlay container
                if (ModalOverlay.Children.Count == 0)
                {
                    ModalOverlay.Visibility = Visibility.Collapsed;
                }

                logger.Log(LogLevel.Debug, "MainWindow", "Move pane mode overlay hidden");
            }
        }

        /// <summary>
        /// Toggle debug overlay showing performance metrics and diagnostics
        /// Keyboard: Ctrl+Shift+D
        /// </summary>
        private void ToggleDebugMode()
        {
            isDebugMode = !isDebugMode;

            if (isDebugMode)
            {
                ShowDebugOverlay();
            }
            else
            {
                HideDebugOverlay();
            }
        }

        /// <summary>
        /// Show debug overlay with performance and diagnostic info
        /// </summary>
        private void ShowDebugOverlay()
        {
            // Remove existing debug overlay if present
            var existingOverlay = ModalOverlay.Children.OfType<Core.Components.DebugOverlay>().FirstOrDefault();
            if (existingOverlay != null)
            {
                existingOverlay.Stop();
                ModalOverlay.Children.Remove(existingOverlay);
            }

            // Create new debug overlay
            var performanceMonitor = serviceContainer.GetRequiredService<IPerformanceMonitor>();
            debugOverlay = new Core.Components.DebugOverlay(logger, themeManager, performanceMonitor, workspaceManager);

            ModalOverlay.Children.Add(debugOverlay);
            ModalOverlay.Visibility = Visibility.Visible;

            logger.Log(LogLevel.Debug, "MainWindow", "Debug overlay shown");
        }

        /// <summary>
        /// Hide debug overlay
        /// </summary>
        private void HideDebugOverlay()
        {
            var existingOverlay = ModalOverlay.Children.OfType<Core.Components.DebugOverlay>().FirstOrDefault();
            if (existingOverlay != null)
            {
                existingOverlay.Stop();
                ModalOverlay.Children.Remove(existingOverlay);

                // If no more overlays, hide the modal overlay container
                if (ModalOverlay.Children.Count == 0)
                {
                    ModalOverlay.Visibility = Visibility.Collapsed;
                }

                logger.Log(LogLevel.Debug, "MainWindow", "Debug overlay hidden");
            }

            debugOverlay = null;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                logger.Log(LogLevel.Info, "MainWindow", "Window closing, saving state...");

                // Save current workspace state
                SaveCurrentWorkspaceState();

                // Save all workspaces to disk
                workspaceManager?.SaveWorkspaces();

                // Capture and save full application state with checksums
                var snapshot = statePersistence.CaptureState(workspaceManager);
                statePersistence.SaveStateAsync(snapshot, createBackup: true).Wait();

                // Close all panes
                paneManager?.CloseAll();

                // Dispose status bar
                statusBar?.Dispose();

                // Dispose command palette
                commandPalette?.Dispose();

                // Dispose debug overlay if active
                debugOverlay?.Stop();
                debugOverlay = null;

                logger.Log(LogLevel.Info, "MainWindow", "Shutdown complete");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Error during shutdown: {ex.Message}");
            }
        }
    }
}
