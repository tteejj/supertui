using System;
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
        public bool IsActive { get; private set; }
        public new bool IsFocused { get; internal set; }  // Set by PaneManager (hides UIElement.IsFocused)
        public virtual PaneSizePreference SizePreference => PaneSizePreference.Flex;

        // Constructor
        protected PaneBase(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));

            BuildPaneStructure();
        }

        /// <summary>
        /// Build the standard pane structure (header + content)
        /// </summary>
        private void BuildPaneStructure()
        {
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
            // Subscribe to project context changes
            projectContext.ProjectContextChanged += OnProjectContextChanged;

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
        /// </summary>
        internal void SetActive(bool active)
        {
            if (IsActive != active)
            {
                IsActive = active;
                ApplyTheme();
                OnActiveChanged(active);
            }
        }

        /// <summary>
        /// Override to handle active state changes
        /// </summary>
        protected virtual void OnActiveChanged(bool isActive)
        {
            // Default: do nothing, let subclasses handle
        }

        /// <summary>
        /// Called when focus state changes (for visual feedback)
        /// </summary>
        public virtual void OnFocusChanged()
        {
            ApplyTheme();
        }

        /// <summary>
        /// Apply terminal theme to pane
        /// </summary>
        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;
            if (theme == null) return;

            var background = theme.GetColor("pane_background");
            var headerBg = theme.GetColor("pane_header");
            var foreground = theme.GetColor("foreground");
            var border = IsFocused ? theme.GetColor("border_active") : theme.GetColor("border");
            var accent = theme.GetColor("accent");

            // Container with focus indicator
            containerBorder.Background = new SolidColorBrush(background);
            containerBorder.BorderBrush = new SolidColorBrush(border);
            containerBorder.BorderThickness = new Thickness(IsFocused ? 2 : 1);  // Thicker border when focused

            // Header
            headerBorder.Background = new SolidColorBrush(headerBg);
            headerBorder.BorderBrush = new SolidColorBrush(border);
            headerText.Foreground = new SolidColorBrush(IsFocused ? accent : foreground);

            // Content
            if (contentArea.Content is Panel panel)
            {
                panel.Background = new SolidColorBrush(background);
            }
        }

        /// <summary>
        /// Dispose pane resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from events
                projectContext.ProjectContextChanged -= OnProjectContextChanged;

                // Let subclasses clean up
                OnDispose();
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
        public object CustomData { get; set; }  // Pane-specific state (JSON-serializable)
    }
}
