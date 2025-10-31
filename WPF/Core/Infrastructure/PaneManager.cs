using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SuperTUI.Core.Components;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Manages open panes with auto-tiling (i3-style)
    /// - Blank canvas by default
    /// - AddPane() auto-tiles with TilingLayoutEngine
    /// - ClosePane() removes and remaining panes expand
    /// - Tracks focused pane for keyboard navigation
    /// </summary>
    public class PaneManager
    {
        private readonly TilingLayoutEngine tilingEngine;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;
        private readonly NavigationFeedbackManager feedbackManager;
        private readonly FocusHistoryManager focusHistory;
        private readonly List<PaneBase> openPanes = new List<PaneBase>();
        private PaneBase focusedPane;

        public Panel Container => tilingEngine.Container;
        public IReadOnlyList<PaneBase> OpenPanes => openPanes.AsReadOnly();
        public PaneBase FocusedPane => focusedPane;
        public int PaneCount => openPanes.Count;

        public event EventHandler<PaneEventArgs> PaneOpened;
        public event EventHandler<PaneEventArgs> PaneClosed;
        public event EventHandler<PaneEventArgs> PaneFocusChanged;

        public PaneManager(ILogger logger, IThemeManager themeManager, FocusHistoryManager focusHistory, IConfigurationManager config = null)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config;
            this.focusHistory = focusHistory ?? throw new ArgumentNullException(nameof(focusHistory));
            this.tilingEngine = new TilingLayoutEngine(logger, themeManager);

            // Initialize feedback manager if config is provided
            if (config != null)
            {
                this.feedbackManager = new NavigationFeedbackManager(logger, config, themeManager);
            }

            logger.Log(LogLevel.Info, "PaneManager", "Initialized with blank canvas");
        }

        /// <summary>
        /// Opens a pane and auto-tiles it
        /// </summary>
        public void OpenPane(PaneBase pane)
        {
            if (pane == null)
                throw new ArgumentNullException(nameof(pane));

            if (openPanes.Contains(pane))
            {
                logger.Log(LogLevel.Warning, "PaneManager", $"Pane {pane.PaneName} already open");
                FocusPane(pane);
                return;
            }

            // Initialize pane
            pane.Initialize();

            // Add to tiling engine (auto-tiles)
            tilingEngine.AddChild(pane, new LayoutParams());
            openPanes.Add(pane);

            // Set as focused
            FocusPane(pane);

            logger.Log(LogLevel.Info, "PaneManager", $"Opened pane: {pane.PaneName} (total: {openPanes.Count})");
            PaneOpened?.Invoke(this, new PaneEventArgs(pane));
        }

        /// <summary>
        /// Closes a pane and auto-reflows remaining panes
        /// </summary>
        public void ClosePane(PaneBase pane)
        {
            if (pane == null || !openPanes.Contains(pane))
                return;

            // Remove from tiling engine (auto-reflows)
            tilingEngine.RemoveChild(pane);
            openPanes.Remove(pane);

            // Dispose pane
            pane.Dispose();

            // Focus next pane if this was focused
            if (focusedPane == pane)
            {
                focusedPane = null;
                if (openPanes.Count > 0)
                {
                    FocusPane(openPanes[0]);
                }
            }

            logger.Log(LogLevel.Info, "PaneManager", $"Closed pane: {pane.PaneName} (remaining: {openPanes.Count})");
            PaneClosed?.Invoke(this, new PaneEventArgs(pane));
        }

        /// <summary>
        /// Closes the currently focused pane
        /// </summary>
        public void CloseFocusedPane()
        {
            if (focusedPane != null)
            {
                ClosePane(focusedPane);
            }
        }

        /// <summary>
        /// Closes all panes
        /// </summary>
        public void CloseAll()
        {
            var panesToClose = openPanes.ToList(); // Copy to avoid modification during iteration
            foreach (var pane in panesToClose)
            {
                ClosePane(pane);
            }
        }

        /// <summary>
        /// Sets focus to a specific pane
        /// </summary>
        public void FocusPane(PaneBase pane)
        {
            if (pane == null || !openPanes.Contains(pane))
                return;

            // Unfocus previous
            if (focusedPane != null && focusedPane != pane)
            {
                focusedPane.SetActive(false);
                focusedPane.OnFocusChanged();
            }

            // Focus new
            focusedPane = pane;
            focusedPane.SetActive(true);
            focusedPane.OnFocusChanged();

            // Use FocusHistoryManager's fallback chain to ensure focus is never lost
            // This replaces manual focus attempts with a robust 4-level fallback:
            // 1. Try the pane itself
            // 2. Try first focusable child
            // 3. Try pane again
            // 4. Try main window as last resort
            pane.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!pane.IsKeyboardFocusWithin)
                {
                    bool focusApplied = focusHistory.ApplyFocusToPane(pane);
                    if (!focusApplied)
                    {
                        logger.Log(LogLevel.Warning, "PaneManager", 
                            $"Failed to apply focus to pane {pane.PaneName} even with fallback chain");
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Input);

            PaneFocusChanged?.Invoke(this, new PaneEventArgs(pane));
        }

        /// <summary>
        /// Navigate focus in a direction (i3-style)
        /// </summary>
        public void NavigateFocus(FocusDirection direction)
        {
            if (focusedPane == null || openPanes.Count <= 1)
                return;

            var targetPane = tilingEngine.FindWidgetInDirection(focusedPane, direction) as PaneBase;
            if (targetPane != null)
            {
                FocusPane(targetPane);
                logger.Log(LogLevel.Debug, "PaneManager", $"Focus moved {direction} to {targetPane.PaneName}");
            }
            else
            {
                // Navigation hit edge - show feedback
                feedbackManager?.ShowNavigationEdgeFeedback(focusedPane, direction);
                logger.Log(LogLevel.Debug, "PaneManager", $"Navigation blocked: at edge in direction {direction}");
            }
        }

        /// <summary>
        /// Swap focused pane with pane in direction (i3-style move)
        /// </summary>
        public void MovePane(FocusDirection direction)
        {
            if (focusedPane == null || openPanes.Count <= 1)
                return;

            var targetPane = tilingEngine.FindWidgetInDirection(focusedPane, direction) as PaneBase;
            if (targetPane != null)
            {
                tilingEngine.SwapWidgets(focusedPane, targetPane);
                logger.Log(LogLevel.Debug, "PaneManager", $"Moved {focusedPane.PaneName} {direction}");
            }
        }

        /// <summary>
        /// Get state for workspace persistence
        /// </summary>
        public PaneManagerState GetState()
        {
            return new PaneManagerState
            {
                OpenPaneTypes = openPanes.Select(p => p.GetType().FullName).ToList(),
                FocusedPaneIndex = focusedPane != null ? openPanes.IndexOf(focusedPane) : -1
            };
        }

        /// <summary>
        /// Restore state from workspace
        /// Note: Caller must provide pane instances (we can't recreate without factory)
        /// </summary>
        public void RestoreState(PaneManagerState state, List<PaneBase> panesToRestore)
        {
            CloseAll();

            if (panesToRestore == null || panesToRestore.Count == 0)
                return;

            foreach (var pane in panesToRestore)
            {
                OpenPane(pane);
            }

            // Restore focus
            if (state.FocusedPaneIndex >= 0 && state.FocusedPaneIndex < openPanes.Count)
            {
                FocusPane(openPanes[state.FocusedPaneIndex]);
            }

            logger.Log(LogLevel.Info, "PaneManager", $"Restored {panesToRestore.Count} panes");
        }

        /// <summary>
        /// Cleanup resources on application shutdown
        /// </summary>
        public void Cleanup()
        {
            feedbackManager?.Cleanup();
            logger.Log(LogLevel.Debug, "PaneManager", "Cleanup complete");
        }
    }

    /// <summary>
    /// State for workspace persistence
    /// </summary>
    public class PaneManagerState
    {
        public List<string> OpenPaneTypes { get; set; } = new List<string>();
        public int FocusedPaneIndex { get; set; } = -1;
    }

    /// <summary>
    /// Event args for pane events
    /// </summary>
    public class PaneEventArgs : EventArgs
    {
        public PaneBase Pane { get; }

        public PaneEventArgs(PaneBase pane)
        {
            Pane = pane;
        }
    }
}
