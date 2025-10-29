using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Controls;
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

        private PaneManager paneManager;
        private PaneFactory paneFactory;
        private StatusBarWidget statusBar;
        private PaneWorkspaceManager workspaceManager;
        private Panes.CommandPalettePane commandPalette;

        public MainWindow(DI.ServiceContainer container)
        {
            serviceContainer = container ?? throw new ArgumentNullException(nameof(container));

            // Get services
            logger = serviceContainer.GetRequiredService<ILogger>();
            themeManager = serviceContainer.GetRequiredService<IThemeManager>();
            projectContext = serviceContainer.GetRequiredService<IProjectContextManager>();

            InitializeComponent();

            // Apply terminal theme
            themeManager.ApplyTheme("Terminal");

            // Initialize components
            InitializeWorkspaceManager();
            InitializePaneSystem();
            InitializeStatusBar();

            // Load current workspace state
            RestoreWorkspaceState();

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

            logger.Log(LogLevel.Info, "MainWindow", $"Switched from workspace {e.OldIndex} to {e.NewIndex}");
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
            PaneCanvas.Children.Add(paneManager.Container);

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
                serviceContainer.GetRequiredService<ITagService>()
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
            StatusBarContainer.Child = statusBar;
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

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Command palette: Ctrl+Space or : (when not in text box)
            if ((e.Key == Key.Space && e.KeyboardDevice.Modifiers == ModifierKeys.Control) ||
                (e.Key == Key.OemSemicolon && e.KeyboardDevice.Modifiers == ModifierKeys.Shift &&
                 !(Keyboard.FocusedElement is TextBox)))
            {
                ShowCommandPalette();
                e.Handled = true;
                return;
            }

            // Close focused pane: Ctrl+Shift+Q (i3-style)
            if (e.Key == Key.Q &&
                e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control) &&
                e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                paneManager.CloseFocusedPane();
                UpdateStatusBarContext();
                e.Handled = true;
                return;
            }

            // Switch workspaces: Alt+1-9 (i3-style)
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt)
            {
                // Workspace switching
                if (e.Key >= Key.D1 && e.Key <= Key.D9)
                {
                    int workspaceIndex = e.Key - Key.D1;
                    workspaceManager.SwitchToWorkspace(workspaceIndex);
                    e.Handled = true;
                    return;
                }

                // Navigate between panes
                switch (e.Key)
                {
                    case Key.Left:
                        paneManager.NavigateFocus(FocusDirection.Left);
                        e.Handled = true;
                        return;
                    case Key.Right:
                        paneManager.NavigateFocus(FocusDirection.Right);
                        e.Handled = true;
                        return;
                    case Key.Up:
                        paneManager.NavigateFocus(FocusDirection.Up);
                        e.Handled = true;
                        return;
                    case Key.Down:
                        paneManager.NavigateFocus(FocusDirection.Down);
                        e.Handled = true;
                        return;
                }
            }

            // Move panes: Alt+Shift+Arrows (i3-style)
            if (e.KeyboardDevice.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
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
                }
            }


            // Quick open shortcuts (example - will be configurable)
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.T:
                        OpenPane("tasks");
                        e.Handled = true;
                        return;
                    case Key.N:
                        OpenPane("notes");
                        e.Handled = true;
                        return;
                    case Key.F:
                        OpenPane("files");
                        e.Handled = true;
                        return;
                }
            }
        }


        private void OpenPane(string paneName)
        {
            try
            {
                var pane = paneFactory.CreatePane(paneName);
                paneManager.OpenPane(pane);
                UpdateStatusBarContext();
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

            // Show modal overlay
            ModalOverlay.Children.Clear();
            ModalOverlay.Children.Add(commandPalette);
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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                logger.Log(LogLevel.Info, "MainWindow", "Window closing, saving state...");

                // Save current workspace state
                SaveCurrentWorkspaceState();

                // Save all workspaces to disk
                workspaceManager?.SaveWorkspaces();

                // Close all panes
                paneManager?.CloseAll();

                // Dispose status bar
                statusBar?.Dispose();

                // Dispose command palette
                commandPalette?.Dispose();

                logger.Log(LogLevel.Info, "MainWindow", "Shutdown complete");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "MainWindow", $"Error during shutdown: {ex.Message}");
            }
        }
    }
}
