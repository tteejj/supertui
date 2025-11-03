using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Size preference for panes (used by TilingLayoutEngine)
    /// </summary>
    public enum PaneSizePreference
    {
        Flex,        // Expand to fill available space (default)
        Small,       // Prefer 300px width/height
        Medium,      // Prefer 600px width/height
        Large,       // Prefer 900px width/height
        Fixed        // Use exact size specified in pane
    }

    /// <summary>
    /// Base class for all panes in the clean pane system
    /// Terminal aesthetic: clean boxes, monospace font, minimal borders, green accents
    /// </summary>
    public abstract class PaneBase : UserControl, IThemeable, IDisposable
    {
        // Services (DI)
        protected readonly ILogger logger;
        protected readonly IThemeManager themeManager;
        protected readonly IProjectContextManager projectContext;
        private readonly Infrastructure.FocusHistoryManager focusHistory;

        // UI structure
        protected Border containerBorder;
        protected Grid mainGrid;
        protected StackPanel headerPanel;
        protected TextBlock headerText;
        protected Border headerBorder;
        protected ContentControl contentArea;

        // Properties
        public string PaneName { get; protected set; }
        public string PaneIcon { get; protected set; }  // Optional emoji/icon
        // Removed IsActive property - use IsKeyboardFocusWithin instead (WPF native)
        // [Obsolete] public bool IsActive { get; private set; }
        public virtual PaneSizePreference SizePreference => PaneSizePreference.Flex;

        // Constructor
        protected PaneBase(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            Infrastructure.FocusHistoryManager focusHistory = null)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
            this.focusHistory = focusHistory;  // Optional - may be null in tests

            BuildPaneStructure();
        }

        /// <summary>
        /// Build the standard pane structure (header + content)
        /// </summary>
        private void BuildPaneStructure()
        {
            // CRITICAL FIX: Make pane focusable to receive keyboard events
            this.Focusable = true;

            // CRITICAL FIX: Subscribe to focus changes to update visual state
            this.IsKeyboardFocusWithinChanged += OnKeyboardFocusWithinChanged;

            logger.Log(LogLevel.Debug, PaneName ?? "Pane",
                "Subscribed to IsKeyboardFocusWithinChanged event");

            // Main grid: header + content
            mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

            // Header
            headerBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 30
            };

            headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            headerText = new TextBlock
            {
                Text = PaneName ?? "Pane",
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };

            headerPanel.Children.Add(headerText);
            headerBorder.Child = headerPanel;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // Content area (ContentControl for flexibility)
            contentArea = new ContentControl
            {
                Margin = new Thickness(12)
            };

            Grid.SetRow(contentArea, 1);
            mainGrid.Children.Add(contentArea);

            // Container border
            containerBorder = new Border
            {
                BorderThickness = new Thickness(1),
                Child = mainGrid
            };

            Content = containerBorder;
        }

        /// <summary>
        /// Initialize the pane (called by PaneManager after creation)
        /// </summary>
        public virtual void Initialize()
        {
            // Track this pane in FocusHistoryManager for proper cleanup
            if (focusHistory != null)
            {
                focusHistory.TrackPane(this);
            }

            // Subscribe to project context changes
            projectContext.ProjectContextChanged += OnProjectContextChanged;

            // Subscribe to theme changes for automatic re-theming
            themeManager.ThemeChanged += OnThemeChanged;

            // Build pane-specific content
            var content = BuildContent();
            if (content != null)
            {
                contentArea.Content = content;
            }

            // Apply theme
            ApplyTheme();

            logger.Log(LogLevel.Info, PaneName ?? "Pane", "Initialized");
        }

        /// <summary>
        /// Override to build pane-specific content
        /// Return a UIElement to be displayed in the content area
        /// </summary>
        protected abstract UIElement BuildContent();

        /// <summary>
        /// Override to handle project context changes
        /// </summary>
        protected virtual void OnProjectContextChanged(object sender, ProjectContextChangedEventArgs e)
        {
            // Default: do nothing, let subclasses handle
        }

        /// <summary>
        /// Handle theme changes and automatically re-apply theme
        /// </summary>
        private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            // Re-apply theme on theme change
            ApplyTheme();

            // Notify subclasses of theme change
            OnThemeChangedOverride(e);
        }

        /// <summary>
        /// Override to handle theme changes in subclasses
        /// Base class already calls ApplyTheme(), so this is for additional logic
        /// </summary>
        protected virtual void OnThemeChangedOverride(ThemeChangedEventArgs e)
        {
            // Default: do nothing, let subclasses handle if needed
        }

        /// <summary>
        /// Save pane state for workspace persistence
        /// </summary>
        public virtual PaneState SaveState()
        {
            return new PaneState
            {
                PaneType = GetType().Name,
                CustomData = null  // Subclasses can override
            };
        }

        /// <summary>
        /// Restore pane state from workspace
        /// </summary>
        public virtual void RestoreState(PaneState state)
        {
            // Default: do nothing, let subclasses handle
        }

        /// <summary>
        /// Set pane active state (called by PaneManager on focus change)
        /// Simplified - no longer tracks IsActive property
        /// Visual state updated automatically via IsKeyboardFocusWithinChanged
        /// </summary>
        internal void SetActive(bool active)
        {
            // Removed IsActive property tracking
            // ApplyTheme() is now called automatically by IsKeyboardFocusWithinChanged
            // We only need to call the lifecycle methods

            // Call lifecycle methods for subclass overrides
            OnActiveChanged(active);
        }

        /// <summary>
        /// Override to handle active state changes
        /// </summary>
        protected virtual void OnActiveChanged(bool isActive)
        {
            // When pane becomes active, subclasses should focus their primary control
            if (isActive)
            {
                OnPaneGainedFocus();
            }
            else
            {
                OnPaneLostFocus();
            }
        }

        /// <summary>
        /// Called when pane gains focus - override to focus specific child control
        /// Applies focus asynchronously to avoid race conditions
        /// </summary>
        protected virtual void OnPaneGainedFocus()
        {
            // Schedule focus application asynchronously
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                // Default: Let WPF find first focusable child
                this.MoveFocus(new System.Windows.Input.TraversalRequest(
                    System.Windows.Input.FocusNavigationDirection.First));
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// Called when pane loses focus - override to handle cleanup
        /// </summary>
        protected virtual void OnPaneLostFocus()
        {
            // Default: do nothing, let subclasses handle
        }


        /// <summary>
        /// Apply terminal theme to pane
        /// Uses both IsActive (from PaneManager) and IsKeyboardFocusWithin (from WPF) to determine focus state
        /// </summary>
        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;
            if (theme == null) return;

            // Guard against cross-thread access in tests
            // WPF UI objects must be accessed on the thread that created them
            if (!CheckAccess())
            {
                logger?.Log(LogLevel.Debug, PaneName ?? "Pane",
                    "ApplyTheme called on non-UI thread - skipping visual updates (test mode)");
                return;
            }

            // Single source of truth - use only WPF's IsKeyboardFocusWithin
            // Removed IsActive property (was redundant tracking)
            bool hasFocus = this.IsKeyboardFocusWithin;

            logger.Log(LogLevel.Debug, PaneName ?? "Pane",
                $"ApplyTheme called - HasFocus: {hasFocus}, IsKeyboardFocusWithin: {IsKeyboardFocusWithin}");

            var background = theme.Background;
            var headerBg = hasFocus ? theme.BorderActive : theme.Surface;  // Change header bg when focused
            var foreground = theme.Foreground;
            var border = hasFocus ? theme.BorderActive : theme.Border;

            // Container with focus indicator
            containerBorder.Background = new SolidColorBrush(background);
            containerBorder.BorderBrush = new SolidColorBrush(border);
            containerBorder.BorderThickness = new Thickness(hasFocus ? 3 : 1);  // Much thicker border when focused (3px vs 1px)

            // Add drop shadow effect when focused
            if (hasFocus)
            {
                containerBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = border,
                    Direction = 0,
                    ShadowDepth = 0,
                    BlurRadius = 12,
                    Opacity = 0.8
                };
            }
            else
            {
                containerBorder.Effect = null;
            }

            // Header - change background color when focused for maximum visibility
            headerBorder.Background = new SolidColorBrush(headerBg);
            headerBorder.BorderBrush = new SolidColorBrush(border);
            headerText.Foreground = new SolidColorBrush(foreground);  // Always use foreground, border shows focus

            // Add visual indicator in header text for better focus tracking
            if (hasFocus)
            {
                headerText.Text = $"► {PaneName}";  // Add arrow prefix when focused
                headerText.FontWeight = FontWeights.Bold;
            }
            else
            {
                headerText.Text = PaneName;
                headerText.FontWeight = FontWeights.Normal;
            }

            // Content
            if (contentArea.Content is Panel panel)
            {
                panel.Background = new SolidColorBrush(background);
            }
        }

        /// <summary>
        /// Handles keyboard focus changes to update visual state
        /// Single automatic theme application when focus changes
        /// No state tracking needed - WPF's IsKeyboardFocusWithin is the source of truth
        /// PHASE 2C: Explicit focus recording replaces global event handler
        /// </summary>
        private void OnKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            bool hasFocus = (bool)e.NewValue;

            logger.Log(LogLevel.Debug, PaneName ?? "Pane",
                $"Focus changed: {(hasFocus ? "GAINED" : "LOST")} keyboard focus");

            // Single call to ApplyTheme when focus changes
            // This is now the ONLY place ApplyTheme is called automatically
            ApplyTheme();

            // PHASE 2C: Record focus change in history (replaces global event handler)
            // Only record when gaining focus - more efficient than tracking ALL focus events
            if (hasFocus && focusHistory != null)
            {
                var focusedElement = System.Windows.Input.Keyboard.FocusedElement as UIElement;
                if (focusedElement != null)
                {
                    focusHistory.RecordFocusFromPane(focusedElement, this.PaneName);
                    logger.Log(LogLevel.Debug, PaneName ?? "Pane",
                        $"Recorded focus on {focusedElement.GetType().Name}");
                }
            }

            // Log current visual state for debugging
            if (hasFocus)
            {
                logger.Log(LogLevel.Debug, PaneName ?? "Pane",
                    "Visual state updated: thick border + glow + highlighted header");
            }
            else
            {
                logger.Log(LogLevel.Debug, PaneName ?? "Pane",
                    "Visual state updated: thin border, no glow, default header");
            }
        }

        /// <summary>
        /// Dispose pane resources
        /// </summary>
        private bool disposedValue = false; // To detect redundant calls

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) // Prevent double disposal
            {
                if (disposing)
                {
                    // Unsubscribe from focus events
                    this.IsKeyboardFocusWithinChanged -= OnKeyboardFocusWithinChanged;

                    // Untrack this pane from FocusHistoryManager to prevent memory leaks
                    if (focusHistory != null)
                    {
                        focusHistory.UntrackPane(this);
                    }

                    // Unsubscribe from events
                    projectContext.ProjectContextChanged -= OnProjectContextChanged;
                    themeManager.ThemeChanged -= OnThemeChanged;

                    // Let subclasses clean up
                    OnDispose();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Override to clean up pane-specific resources
        /// </summary>
        protected virtual void OnDispose()
        {
            // Default: do nothing, let subclasses handle
        }

        /// <summary>
        /// Helper for logging
        /// </summary>
        protected void Log(string message, LogLevel level = LogLevel.Info)
        {
            logger.Log(level, PaneName ?? GetType().Name, message);
        }
    }

    /// <summary>
    /// Pane state for workspace persistence
    /// </summary>
    public class PaneState
    {
        public string PaneType { get; set; }
        public Dictionary<string, object> CustomData { get; set; }  // Pane-specific state (JSON-serializable)
    }
}
