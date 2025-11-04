using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly FocusHistoryManager focusHistory;
        private readonly INotificationManager notificationManager;

        private PaneManager paneManager;
        private PaneFactory paneFactory;
        private StatusBarWidget statusBar;
        private PaneWorkspaceManager workspaceManager;
        private Panes.CommandPalettePane commandPalette;
        private Core.Components.DebugOverlay debugOverlay;
        private Core.Components.NotificationPanel notificationPanel;
        private bool isDebugMode = false;
        private FocusState? commandPaletteFocusState = null;

        public MainWindow(DI.ServiceContainer container)
        {
            serviceContainer = container ?? throw new ArgumentNullException(nameof(container));

            // Get services
            logger = serviceContainer.GetRequiredService<ILogger>();
            themeManager = serviceContainer.GetRequiredService<IThemeManager>();
            projectContext = serviceContainer.GetRequiredService<IProjectContextManager>();
            statePersistence = serviceContainer.GetRequiredService<IStatePersistenceManager>();
            commandHistory = serviceContainer.GetRequiredService<CommandHistory>();
            focusHistory = serviceContainer.GetRequiredService<FocusHistoryManager>();
            notificationManager = serviceContainer.GetRequiredService<INotificationManager>();

            // Initialize FocusDebugger for ultra-verbose focus logging
            Core.Infrastructure.FocusDebugger.Initialize(logger);
            logger.Log(LogLevel.Info, "MainWindow", "FocusDebugger initialized");

            // Initialize StatePersistenceManager so it can actually save state!
            statePersistence.Initialize();
            logger.Log(LogLevel.Info, "MainWindow", "StatePersistenceManager initialized");

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
            InitializeNotificationPanel();
            SubscribeToEvents();

            // Restore full application state from disk (with checksum validation)
            RestoreApplicationState();

            // Load current workspace state
            RestoreWorkspaceState();

            // Register all keyboard shortcuts
            RegisterAllShortcuts();

            // Keyboard handlers
            this.KeyDown += MainWindow_KeyDown;
            Closing += MainWindow_Closing;

            // CRITICAL FIX: Handle window activation/deactivation for focus management
            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;
            this.Loaded += MainWindow_Loaded;

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
            logger.Log(LogLevel.Info, "MainWindow",
                $"Switching from workspace {e.OldIndex} to {e.NewIndex}");

            // Capture current keyboard focus IMMEDIATELY (synchronously)
            // This prevents race conditions where async focus operations corrupt saved state
            var currentFocusedElement = System.Windows.Input.Keyboard.FocusedElement as UIElement;
            var currentFocusedPane = paneManager?.FocusedPane;

            logger.Log(LogLevel.Debug, "MainWindow",
                $"Captured focus state: Element={currentFocusedElement?.GetType().Name}, Pane={currentFocusedPane?.PaneName}");

            // CRITICAL: Get the old workspace state BEFORE the index changes
            var oldWorkspace = workspaceManager.GetWorkspace(e.OldIndex);

            logger.Log(LogLevel.Debug, "MainWindow",
                $"Old workspace {e.OldIndex} has {paneManager.OpenPanes.Count} panes");

            // Save current panes to the OLD workspace
            var paneState = paneManager.GetState();
            oldWorkspace.OpenPaneTypes = paneState.OpenPaneTypes;
            oldWorkspace.FocusedPaneIndex = paneState.FocusedPaneIndex;

            // Save individual pane states (synchronously, before async operations start)
            var paneStates = new List<Core.Components.PaneState>();
            foreach (var pane in paneManager.OpenPanes)
            {
                try
                {
                    paneStates.Add(pane.SaveState());
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Error, "MainWindow", $"Failed to save state for pane: {ex.Message}");
                }
            }
            oldWorkspace.PaneStates = paneStates;

            // Save focus state synchronously using captured values
            oldWorkspace.FocusState = focusHistory.SaveWorkspaceState();
            if (projectContext.CurrentProject != null)
            {
                oldWorkspace.CurrentProjectId = projectContext.CurrentProject.Id;
            }

            // Update the old workspace
            workspaceManager.UpdateWorkspace(e.OldIndex, oldWorkspace);

            // Restore new workspace state
            RestoreWorkspaceState();

            // Update status bar workspace indicator
            statusBar?.UpdateWorkspaceIndicator(e.NewIndex);

            logger.Log(LogLevel.Info, "MainWindow",
                $"Switched to workspace {e.NewIndex} (now has {paneManager.OpenPanes.Count} panes)");
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

            // Pane navigation: Ctrl+Shift+Arrows
            // Context checking now handled by ShortcutManager itself
            shortcuts.RegisterGlobal(Key.Left, ModifierKeys.Control | ModifierKeys.Shift,
                () => paneManager.NavigateFocus(FocusDirection.Left),
                "Focus pane left");

            shortcuts.RegisterGlobal(Key.Right, ModifierKeys.Control | ModifierKeys.Shift,
                () => paneManager.NavigateFocus(FocusDirection.Right),
                "Focus pane right");

            shortcuts.RegisterGlobal(Key.Up, ModifierKeys.Control | ModifierKeys.Shift,
                () => paneManager.NavigateFocus(FocusDirection.Up),
                "Focus pane up");

            shortcuts.RegisterGlobal(Key.Down, ModifierKeys.Control | ModifierKeys.Shift,
                () => paneManager.NavigateFocus(FocusDirection.Down),
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

            shortcuts.RegisterGlobal(Key.M, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("commands"),
                "Open Commands pane (snippet library)");

            shortcuts.RegisterGlobal(Key.O, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("pomodoro"),
                "Open Pomodoro timer pane");

            shortcuts.RegisterGlobal(Key.K, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("kanban"),
                "Open Kanban board pane");

            shortcuts.RegisterGlobal(Key.W, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("timetracking"),
                "Open Time Tracking pane (Weekly hours)");

            shortcuts.RegisterGlobal(Key.D0, ModifierKeys.Control | ModifierKeys.Shift,
                () => OpenPane("clock"),
                "Open Clock pane");

            shortcuts.RegisterGlobal(Key.OemComma, ModifierKeys.Control,
                () => OpenPane("settings"),
                "Open Settings pane");

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

            // Refresh all panes: Ctrl+R
            shortcuts.RegisterGlobal(Key.R, ModifierKeys.Control,
                () => RefreshAllPanes(),
                "Refresh all panes");

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
                notificationManager.ShowInfo("Nothing to undo", "Undo");
                return;
            }

            try
            {
                var description = commandHistory.GetUndoDescription();
                commandHistory.Undo();
                logger.Log(LogLevel.Info, "MainWindow", $"Undone: {description}");
                notificationManager.ShowSuccess($"Undone: {description}", "Undo");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Failed to undo: {ex.Message}", ex);
                notificationManager.ShowError($"Failed to undo: {ex.Message}", "Undo Error");
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
                notificationManager.ShowInfo("Nothing to redo", "Redo");
                return;
            }

            try
            {
                var description = commandHistory.GetRedoDescription();
                commandHistory.Redo();
                logger.Log(LogLevel.Info, "MainWindow", $"Redone: {description}");
                notificationManager.ShowSuccess($"Redone: {description}", "Redo");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Failed to redo: {ex.Message}", ex);
                notificationManager.ShowError($"Failed to redo: {ex.Message}", "Redo Error");
            }
        }

        /// <summary>
        /// Refresh all panes (Ctrl+R)
        /// Publishes RefreshRequestedEvent to all subscribed panes
        /// </summary>
        private void RefreshAllPanes()
        {
            try
            {
                var eventBus = serviceContainer.GetRequiredService<IEventBus>();
                eventBus.Publish(new Core.Events.RefreshRequestedEvent
                {
                    TargetWidget = null, // null = all panes
                    Reason = "User requested refresh (Ctrl+R)"
                });

                logger.Log(LogLevel.Info, "MainWindow", "Published RefreshRequestedEvent to all panes");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Failed to refresh panes: {ex.Message}", ex);
            }
        }

        private void SaveCurrentWorkspaceState(int? workspaceIndex = null)
        {
            // CRITICAL FIX: Use provided workspace index if specified, otherwise use current
            // This fixes the workspace switching bug where panes appear to "move" between workspaces
            var targetIndex = workspaceIndex ?? workspaceManager.CurrentWorkspaceIndex;
            var state = workspaceManager.GetWorkspace(targetIndex);

            // Save pane state
            var paneState = paneManager.GetState();
            state.OpenPaneTypes = paneState.OpenPaneTypes;
            state.FocusedPaneIndex = paneState.FocusedPaneIndex;

            // Save individual pane states
            var paneStates = new List<Core.Components.PaneState>();
            var openPanes = paneManager.OpenPanes;
            foreach (var pane in openPanes)
            {
                try
                {
                    var paneStateData = pane.SaveState();
                    paneStates.Add(paneStateData);
                    logger.Log(LogLevel.Debug, "MainWindow",
                        $"Saved state for pane {pane.PaneName}: {(paneStateData.CustomData != null ? "has custom data" : "no custom data")}");
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Error, "MainWindow",
                        $"Failed to save state for pane {pane.PaneName}: {ex.Message}");
                }
            }
            state.PaneStates = paneStates;

            // CRITICAL: Save focus state for perfect restoration
            state.FocusState = focusHistory.SaveWorkspaceState();

            // Save project context
            if (projectContext.CurrentProject != null)
            {
                state.CurrentProjectId = projectContext.CurrentProject.Id;
            }
            else
            {
                state.CurrentProjectId = null;
            }

            // CRITICAL FIX: Update the specific workspace, not just the current one
            workspaceManager.UpdateWorkspace(targetIndex, state);
            logger.Log(LogLevel.Debug, "MainWindow", $"Saved workspace {state.Index} state ({state.OpenPaneTypes.Count} panes, {paneStates.Count} pane states)");
        }

        private void RestoreApplicationState()
        {
            try
            {
                // FIX: Use synchronous file I/O during initialization to avoid deadlock
                // (blocking .Result on UI thread with async I/O causes classic async deadlock)
                var stateFile = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SuperTUI", "State", "state.json");

                StateSnapshot snapshot = null;
                if (System.IO.File.Exists(stateFile))
                {
                    string json = System.IO.File.ReadAllText(stateFile, System.Text.Encoding.UTF8);
                    snapshot = System.Text.Json.JsonSerializer.Deserialize<StateSnapshot>(json);
                }

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

        private async void RestoreWorkspaceState()
        {
            try
            {
                var state = workspaceManager.CurrentWorkspace;
                logger?.Log(LogLevel.Debug, "MainWindow", $"Restoring workspace {state.Index} state");

                // Unsubscribe from all pane events before closing
                // This prevents memory leaks from event handlers holding references to disposed panes
                foreach (var pane in paneManager.OpenPanes.ToList())
                {
                    if (pane is Panes.NotesPane notesPane)
                    {
                        notesPane.FileBrowserRequested -= OnNotesPaneRequestFileBrowser;
                    }
                    // Add more pane types here if they have MainWindow event subscriptions
                }

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
                logger.Log(LogLevel.Info, "MainWindow", $"[WORKSPACE RESTORE] Attempting to restore {state.OpenPaneTypes.Count} panes");
                var panesToRestore = new List<Core.Components.PaneBase>();
                var failedPanes = new List<string>();
                int index = 0;
                foreach (var paneTypeName in state.OpenPaneTypes)
                {
                    try
                    {
                        logger.Log(LogLevel.Info, "MainWindow", $"[WORKSPACE RESTORE] [{index + 1}/{state.OpenPaneTypes.Count}] Restoring pane type: '{paneTypeName}'");
                        var pane = paneFactory.CreatePane(paneTypeName);
                        panesToRestore.Add(pane);
                        logger.Log(LogLevel.Info, "MainWindow", $"[WORKSPACE RESTORE] ✓ Successfully restored pane [{index + 1}]: {pane.PaneName}");
                        index++;
                    }
                    catch (Exception ex)
                    {
                        logger.Log(LogLevel.Error, "MainWindow", $"[WORKSPACE RESTORE] ✗ Failed to restore pane [{index + 1}] '{paneTypeName}': {ex.Message}");
                        logger.Log(LogLevel.Error, "MainWindow", $"[WORKSPACE RESTORE] Stack trace: {ex.StackTrace}");
                        failedPanes.Add(paneTypeName);
                        index++;
                    }
                }

                logger.Log(LogLevel.Info, "MainWindow", $"[WORKSPACE RESTORE] Restoration summary: {panesToRestore.Count} succeeded, {failedPanes.Count} failed");

                // Show notification if any panes failed to restore
                if (failedPanes.Count > 0)
                {
                    var paneList = string.Join(", ", failedPanes);
                    notificationManager.ShowWarning(
                        $"Could not restore {failedPanes.Count} pane(s): {paneList}",
                        "Workspace Restoration Warning");
                }

                if (panesToRestore.Count > 0)
                {
                    var paneState = new PaneManagerState
                    {
                        OpenPaneTypes = state.OpenPaneTypes,
                        FocusedPaneIndex = state.FocusedPaneIndex
                    };
                    paneManager.RestoreState(paneState, panesToRestore);

                    // Subscribe to pane-specific events after restoration
                    foreach (var pane in panesToRestore)
                    {
                        if (pane is Panes.NotesPane notesPane)
                        {
                            notesPane.FileBrowserRequested += OnNotesPaneRequestFileBrowser;
                        }
                    }

                    // Restore individual pane states on UI thread
                    if (state.PaneStates != null && state.PaneStates.Count > 0)
                    {
                        for (int i = 0; i < panesToRestore.Count && i < state.PaneStates.Count; i++)
                        {
                            try
                            {
                                panesToRestore[i].RestoreState(state.PaneStates[i]);
                                logger.Log(LogLevel.Debug, "MainWindow",
                                    $"Restored state for pane {panesToRestore[i].PaneName}");
                            }
                            catch (Exception ex)
                            {
                                logger.Log(LogLevel.Error, "MainWindow",
                                    $"Failed to restore state for pane {panesToRestore[i].PaneName}: {ex.Message}");
                            }
                        }
                    }

                    // Restore focus history
                    if (state.FocusState != null)
                    {
                        focusHistory.RestoreWorkspaceState(state.FocusState);
                    }

                    // Restore focus to the correct pane
                    if (state.FocusedPaneIndex >= 0 && state.FocusedPaneIndex < paneManager?.OpenPanes.Count)
                    {
                        var targetPane = paneManager.OpenPanes[state.FocusedPaneIndex];

                        // Wait for pane to be fully loaded
                        await WaitForLoadedAsync(targetPane);

                        // Delay slightly to ensure layout is complete
                        await Task.Delay(50);

                        // Focus the pane
                        await Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            paneManager?.FocusPane(targetPane);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    }

                    logger.Log(LogLevel.Debug, "MainWindow", $"Workspace {state.Index} state restored successfully");
                }

                UpdateStatusBarContext();
                logger.Log(LogLevel.Info, "MainWindow", $"Restored workspace {state.Index} ({panesToRestore.Count} panes)");
            }
            catch (Exception ex)
            {
                logger?.Log(LogLevel.Error, "MainWindow", $"Error in RestoreWorkspaceState: {ex.Message}");
            }
        }


        private void InitializePaneSystem()
        {
            // Create pane manager (blank canvas) with config for navigation feedback
            var config = serviceContainer.GetRequiredService<IConfigurationManager>();
            paneManager = new PaneManager(logger, themeManager, focusHistory, config);

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
                serviceContainer.GetRequiredService<IShortcutManager>(),
                serviceContainer.GetRequiredService<ITaskService>(),
                serviceContainer.GetRequiredService<IProjectService>(),
                serviceContainer.GetRequiredService<ITimeTrackingService>(),
                serviceContainer.GetRequiredService<ITagService>(),
                serviceContainer.GetRequiredService<IEventBus>(),
                commandHistory,
                focusHistory
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
        }

        private void InitializeNotificationPanel()
        {
            notificationPanel = new Core.Components.NotificationPanel(
                logger,
                themeManager,
                notificationManager
            );

            // Add to notification overlay
            if (!NotificationOverlay.Children.Contains(notificationPanel))
            {
                NotificationOverlay.Children.Add(notificationPanel);
            }

            logger.Log(LogLevel.Info, "MainWindow", "Notification panel initialized");
        }

        private void SubscribeToEvents()
        {
            var eventBus = serviceContainer.GetRequiredService<IEventBus>();

            // Subscribe to CloseFileBrowserEvent
            eventBus.Subscribe<Core.Events.CloseFileBrowserEvent>(evt =>
            {
                // Close FileBrowser pane when NotesPane opens a file
                var fileBrowserPane = paneManager.OpenPanes.OfType<Panes.SimpleFileBrowserPane>().FirstOrDefault();
                if (fileBrowserPane != null)
                {
                    logger.Log(LogLevel.Info, "MainWindow", $"Closing FileBrowser pane (reason: {evt.Reason})");
                    paneManager.ClosePane(fileBrowserPane);

                    // DON'T call FocusPane here - the requesting pane (NotesPane) already handled its own focus
                    // Calling FocusPane causes a race condition where FocusManager finds the ListBox instead of editor
                }
            });

            logger.Log(LogLevel.Info, "MainWindow", "Subscribed to EventBus events");
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

                // Subscribe to pane-specific events after opening
                if (pane is Panes.NotesPane notesPane)
                {
                    notesPane.FileBrowserRequested += OnNotesPaneRequestFileBrowser;
                }

                logger.Log(LogLevel.Info, "MainWindow", $"Opened pane: {paneName}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Failed to open pane '{paneName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Handle NotesPane requesting file browser to select a note file
        /// </summary>
        private void OnNotesPaneRequestFileBrowser(object sender, string notesPath)
        {
            try
            {
                logger.Log(LogLevel.Info, "MainWindow", $"NotesPane requested FileBrowser for path: {notesPath}");

                // Check if FileBrowser is already open (SimpleFileBrowserPane is the actual type)
                var existingFileBrowser = paneManager.OpenPanes.OfType<Panes.SimpleFileBrowserPane>().FirstOrDefault();

                if (existingFileBrowser != null)
                {
                    // FileBrowser already open, just navigate to notes folder
                    existingFileBrowser.SetInitialPath(notesPath);
                    paneManager.FocusPane(existingFileBrowser);
                    logger.Log(LogLevel.Debug, "MainWindow", "Navigated existing FileBrowser to notes folder");
                }
                else
                {
                    // Open new FileBrowser pane (SimpleFileBrowserPane is the actual type returned by factory)
                    var fileBrowserPane = paneFactory.CreatePane("files") as Panes.SimpleFileBrowserPane;
                    if (fileBrowserPane != null)
                    {
                        paneManager.OpenPane(fileBrowserPane);

                        // Set initial path to notes folder BEFORE focusing
                        // This ensures the path is set when the pane becomes visible
                        fileBrowserPane.SetInitialPath(notesPath);

                        // Focus the newly opened FileBrowser so user can immediately navigate
                        // Queue focus with Send priority to ensure it happens after SetInitialPath
                        Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            paneManager.FocusPane(fileBrowserPane);
                        }, System.Windows.Threading.DispatcherPriority.Send);

                        logger.Log(LogLevel.Info, "MainWindow", $"Opened FileBrowser pane for notes folder: {notesPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Failed to open FileBrowser from NotesPane: {ex.Message}");
                notificationManager.ShowError($"Failed to open file browser: {ex.Message}", "File Browser Error");
            }
        }

        /// <summary>
        /// Show command palette as modal overlay
        /// CRITICAL FIX: Block input to background panes to prevent accidental actions
        /// Save previous focus state for restoration
        /// </summary>
        private void ShowCommandPalette()
        {
            // Save focus state before opening modal
            commandPaletteFocusState = new FocusState
            {
                PreviousElement = Keyboard.FocusedElement as UIElement,
                PreviousPane = paneManager?.FocusedPane,
                CapturedAt = DateTime.Now
            };

            logger.Log(LogLevel.Debug, "MainWindow",
                $"Opening command palette, saving focus state: Pane={commandPaletteFocusState.PreviousPane?.PaneName}, Element={commandPaletteFocusState.PreviousElement?.GetType().Name}");

            if (commandPalette == null)
            {
                // Create command palette
                commandPalette = new Panes.CommandPalettePane(
                    logger,
                    themeManager,
                    projectContext,
                    serviceContainer.GetRequiredService<IConfigurationManager>(),
                    paneFactory,
                    paneManager,
                    serviceContainer.GetRequiredService<IEventBus>()
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

            // CRITICAL FIX: Block input to background panes when modal is shown
            // This prevents users from accidentally triggering actions in background panes
            PaneCanvas.IsHitTestVisible = false;
            PaneCanvas.Focusable = false;

            ModalOverlay.Visibility = Visibility.Visible;

            // Animate opening
            commandPalette.AnimateOpen();

            // Focus search box EVERY time palette opens (not just first time)
            commandPalette.FocusSearchBox();

            logger.Log(LogLevel.Debug, "MainWindow", "Command palette opened (background input blocked)");
        }

        /// <summary>
        /// Hide command palette modal overlay
        /// Correct focus restoration order - re-enable input FIRST, then restore focus
        /// </summary>
        private async void HideCommandPalette()
        {
            if (commandPalette != null && ModalOverlay.Visibility == Visibility.Visible)
            {
                // Retrieve saved focus state
                var savedState = commandPaletteFocusState;
                UIElement previousElement = savedState?.PreviousElement;
                Core.Components.PaneBase previousPane = savedState?.PreviousPane;

                // Animate closing
                commandPalette.AnimateClose(async () =>
                {
                    await Application.Current?.Dispatcher.InvokeAsync(async () =>
                    {
                        // Re-enable input FIRST (before focus restoration)
                        PaneCanvas.IsHitTestVisible = true;
                        PaneCanvas.Focusable = true;

                        // THEN hide overlay
                        ModalOverlay.Visibility = Visibility.Collapsed;
                        ModalOverlay.Children.Clear();

                        // Restore focus (fallback chain)
                        if (previousElement is FrameworkElement fwElement && fwElement.IsLoaded && fwElement.Focusable)
                        {
                            // Try saved element first
                            await Application.Current?.Dispatcher.InvokeAsync(() =>
                            {
                                System.Windows.Input.Keyboard.Focus(fwElement);
                            }, System.Windows.Threading.DispatcherPriority.Input);

                            logger.Log(LogLevel.Debug, "MainWindow", $"Restored focus to saved element: {fwElement.GetType().Name}");
                        }
                        else if (previousPane != null)
                        {
                            // Wait for pane to load if needed
                            await WaitForLoadedAsync(previousPane);

                            // Focus the pane
                            await Application.Current?.Dispatcher.InvokeAsync(() =>
                            {
                                paneManager?.FocusPane(previousPane);
                            }, System.Windows.Threading.DispatcherPriority.Input);

                            logger.Log(LogLevel.Debug, "MainWindow", $"Restored focus to pane: {previousPane.PaneName}");
                        }
                        else
                        {
                            logger.Log(LogLevel.Warning, "MainWindow", "No previous focus state saved, focus may be lost");
                        }

                        // Clear focus state
                        commandPaletteFocusState = null;
                    }, System.Windows.Threading.DispatcherPriority.Input);
                });

                logger.Log(LogLevel.Debug, "MainWindow", "Command palette closed (background input restored)");
            }
        }

        private void ShowHelpOverlay()
        {
            OpenPane("help");
        }

        /// <summary>
        /// Handle window activation (Alt+Tab back, clicking on window, etc.)
        /// CRITICAL FIX: Wait for pane to be loaded before restoring focus
        /// </summary>
        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // RACE CONDITION FIX: Check pane exists and is loaded before restoring focus
            var focusedPane = paneManager?.FocusedPane;
            if (focusedPane == null) return;

            logger.Log(LogLevel.Debug, "MainWindow", $"Window activated, restoring focus to {focusedPane.PaneName}");

            // Use dispatcher to ensure focus is set after activation completes
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // CRITICAL: Check pane is still valid and loaded
                    if (focusedPane == null || !focusedPane.IsLoaded)
                    {
                        logger.Log(LogLevel.Debug, "MainWindow", "Cannot restore focus - pane not loaded or no longer valid");
                        return;
                    }

                    paneManager.FocusPane(focusedPane);
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Warning, "MainWindow", $"Failed to restore focus on window activation: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// Handle window deactivation (Alt+Tab away, clicking outside, etc.)
        /// </summary>
        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            // Log but don't change anything - we'll restore focus when activated
            logger.Log(LogLevel.Debug, "MainWindow", "Window deactivated");
        }

        /// <summary>
        /// Handle window loaded event - set initial focus
        /// CRITICAL FIX: Wait for pane to be loaded before setting focus
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            logger.Log(LogLevel.Debug, "MainWindow", "Window loaded, setting initial focus");

            // RACE CONDITION FIX: Check panes exist and are loaded before setting focus
            if (paneManager?.PaneCount > 0 && paneManager.FocusedPane != null)
            {
                var firstPane = paneManager.FocusedPane;
                if (firstPane != null)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // CRITICAL: Check pane is still valid and loaded
                            if (firstPane == null || !firstPane.IsLoaded)
                            {
                                logger.Log(LogLevel.Debug, "MainWindow", "Cannot set initial focus - pane not loaded or no longer valid");
                                return;
                            }

                            paneManager.FocusPane(firstPane);
                            logger.Log(LogLevel.Debug, "MainWindow", $"Initial focus set to {firstPane.PaneName}");
                        }
                        catch (Exception ex)
                        {
                            logger.Log(LogLevel.Warning, "MainWindow", $"Failed to set initial focus: {ex.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        /// <summary>
        /// Wait for a FrameworkElement to finish loading.
        /// Returns immediately if already loaded.
        /// </summary>
        private Task WaitForLoadedAsync(FrameworkElement element)
        {
            if (element.IsLoaded)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            RoutedEventHandler handler = null;
            handler = (s, e) =>
            {
                element.Loaded -= handler;
                tcs.TrySetResult(true);
            };
            element.Loaded += handler;
            return tcs.Task;
        }

        /// <summary>
        /// Show move pane mode visual feedback overlay
        /// FIX 2: Add visual feedback for F12 move mode
        /// CRITICAL FIX: Block input to background panes during move mode
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

            // CRITICAL FIX: Block input to background panes during move mode
            // This prevents accidental input to panes while rearranging layout
            PaneCanvas.IsHitTestVisible = false;
            PaneCanvas.Focusable = false;

            logger.Log(LogLevel.Debug, "MainWindow", "Move pane mode overlay shown (background input blocked)");
        }

        /// <summary>
        /// Hide move pane mode visual feedback overlay
        /// CRITICAL FIX: Re-enable input to background panes when move mode exits
        /// </summary>
        private void HideMovePaneModeOverlay()
        {
            // Only remove the move pane mode overlay, keep other overlays
            var existingOverlay = ModalOverlay.Children.OfType<Border>().FirstOrDefault(b => b.Name == "MovePaneModeOverlay");
            if (existingOverlay != null)
            {
                ModalOverlay.Children.Remove(existingOverlay);

                // If no more overlays, hide the modal overlay container and re-enable input
                if (ModalOverlay.Children.Count == 0)
                {
                    ModalOverlay.Visibility = Visibility.Collapsed;

                    // CRITICAL FIX: Re-enable input to background panes when all modals closed
                    PaneCanvas.IsHitTestVisible = true;
                    PaneCanvas.Focusable = true;
                }

                logger.Log(LogLevel.Debug, "MainWindow", "Move pane mode overlay hidden (background input restored)");
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
        /// CRITICAL FIX: Block input to background panes during debug mode
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

            // CRITICAL FIX: Block input to background panes during debug mode
            // This prevents accidental actions while viewing debug information
            PaneCanvas.IsHitTestVisible = false;
            PaneCanvas.Focusable = false;

            logger.Log(LogLevel.Debug, "MainWindow", "Debug overlay shown (background input blocked)");
        }

        /// <summary>
        /// Hide debug overlay
        /// CRITICAL FIX: Re-enable input to background panes when debug mode exits
        /// </summary>
        private void HideDebugOverlay()
        {
            var existingOverlay = ModalOverlay.Children.OfType<Core.Components.DebugOverlay>().FirstOrDefault();
            if (existingOverlay != null)
            {
                existingOverlay.Stop();
                ModalOverlay.Children.Remove(existingOverlay);

                // If no more overlays, hide the modal overlay container and re-enable input
                if (ModalOverlay.Children.Count == 0)
                {
                    ModalOverlay.Visibility = Visibility.Collapsed;

                    // CRITICAL FIX: Re-enable input to background panes when all modals closed
                    PaneCanvas.IsHitTestVisible = true;
                    PaneCanvas.Focusable = true;
                }

                logger.Log(LogLevel.Debug, "MainWindow", "Debug overlay hidden (background input restored)");
            }

            debugOverlay = null;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                logger.Log(LogLevel.Info, "MainWindow", "Window closing, saving state...");

                // Unsubscribe event handlers to prevent memory leaks
                if (workspaceManager != null)
                {
                    workspaceManager.WorkspaceChanged -= OnWorkspaceChanged;
                }

                if (paneManager != null)
                {
                    paneManager.PaneFocusChanged -= OnPaneFocusChanged;
                }

                // Note: MainWindow lifetime events (KeyDown, Activated, etc.) are automatically
                // cleaned up when window closes, but we unsubscribe from long-lived services

                // Save current workspace state
                SaveCurrentWorkspaceState();

                // Save all workspaces to disk
                workspaceManager?.SaveWorkspaces();

                // Capture and save full application state with checksums
                var snapshot = statePersistence.CaptureState(workspaceManager);

                // FIX: Use synchronous file I/O during shutdown to avoid deadlock
                // (blocking .Wait() on UI thread with async I/O causes classic async deadlock)
                if (snapshot != null)
                {
                    snapshot.Timestamp = DateTime.Now;
                    snapshot.CalculateChecksum();

                    var stateFile = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SuperTUI", "State", "state.json");

                    // Ensure directory exists
                    var stateDir = System.IO.Path.GetDirectoryName(stateFile);
                    if (!System.IO.Directory.Exists(stateDir))
                    {
                        System.IO.Directory.CreateDirectory(stateDir);
                    }

                    string json = System.Text.Json.JsonSerializer.Serialize(snapshot,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                    System.IO.File.WriteAllText(stateFile, json, System.Text.Encoding.UTF8);

                    logger.Log(LogLevel.Info, "MainWindow", $"State saved to {stateFile}");
                }

                // Close all panes
                paneManager?.CloseAll();

                // Cleanup pane manager resources (navigation feedback, etc.)
                paneManager?.Cleanup();

                // Dispose status bar
                statusBar?.Dispose();

                // Dispose command palette
                commandPalette?.Dispose();

                // Dispose debug overlay if active
                debugOverlay?.Stop();
                debugOverlay = null;

                // Dispose notification panel
                notificationPanel?.Dispose();

                logger.Log(LogLevel.Info, "MainWindow", "Shutdown complete");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Error during shutdown: {ex.Message}");

                // Show error notification (user may see it briefly before window closes)
                notificationManager?.ShowError(
                    $"Failed to save application state: {ex.Message}",
                    "Shutdown Error");
            }
        }
    }
}
