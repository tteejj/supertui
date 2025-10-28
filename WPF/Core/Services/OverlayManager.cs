using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using SuperTUI.Core.Interfaces;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Overlay zone positions (floating panels over workspace)
    /// </summary>
    public enum OverlayZone
    {
        Left,       // Filter panels, navigation
        Right,      // Detail panels, context info
        Top,        // Command palette, search
        Bottom,     // Quick add, inline creation
        Center      // Modal dialogs, full forms
    }

    /// <summary>
    /// Manages overlay zones that float over the workspace
    /// Provides terminal-style UI with WPF capabilities (smooth animations, transparency, blur)
    /// </summary>
    public class OverlayManager
    {
        private static OverlayManager instance;
        public static OverlayManager Instance => instance ??= new OverlayManager();

        private readonly SuperTUI.Infrastructure.ILogger logger;
        private readonly SuperTUI.Infrastructure.IThemeManager themeManager;

        // Container references
        private Grid rootContainer;           // MainWindow's root Grid (both layers)
        private Panel workspaceContainer;     // Workspace content (Layer 1 - base)

        // Overlay zones (Layer 2 - floating)
        private Grid leftZone;
        private Grid rightZone;
        private Grid topZone;
        private Grid bottomZone;
        private Grid centerZone;

        // Zone visibility state
        private bool isLeftZoneVisible = false;
        private bool isRightZoneVisible = false;
        private bool isTopZoneVisible = false;
        private bool isBottomZoneVisible = false;
        private bool isCenterZoneVisible = false;

        // Zone content tracking
        private UIElement leftZoneContent;
        private UIElement rightZoneContent;
        private UIElement topZoneContent;
        private UIElement bottomZoneContent;
        private UIElement centerZoneContent;

        // Zone dimensions
        private const double LeftZoneWidth = 300;
        private const double RightZoneWidth = 350;
        private const double TopZoneHeight = 400;
        private const double BottomZoneHeight = 150;
        private const double CenterZoneMaxWidth = 800;
        private const double CenterZoneMaxHeight = 600;

        // Animation durations
        private const int SlideAnimationDuration = 200;  // ms
        private const int FadeAnimationDuration = 200;   // ms

        // Events
        public event Action<OverlayZone> ZoneOpened;
        public event Action<OverlayZone> ZoneClosed;

        private OverlayManager()
        {
            logger = SuperTUI.Infrastructure.Logger.Instance;
            themeManager = SuperTUI.Infrastructure.ThemeManager.Instance;
        }

        /// <summary>
        /// Initialize overlay manager with container references
        /// Call this from MainWindow after workspace manager is set up
        /// </summary>
        public void Initialize(Grid rootContainer, Panel workspaceContainer)
        {
            logger?.Info("OverlayManager", $"Initialize called - rootContainer: {rootContainer?.GetType().Name ?? "NULL"}, workspaceContainer: {workspaceContainer?.GetType().Name ?? "NULL"}");

            this.rootContainer = rootContainer ?? throw new ArgumentNullException(nameof(rootContainer));
            this.workspaceContainer = workspaceContainer ?? throw new ArgumentNullException(nameof(workspaceContainer));

            CreateOverlayZones();

            logger?.Info("OverlayManager", $"Initialized overlay zone system - bottomZone null? {bottomZone == null}");
        }

        /// <summary>
        /// Create all overlay zones (initially collapsed)
        /// </summary>
        private void CreateOverlayZones()
        {
            var theme = themeManager.CurrentTheme;

            // Left zone (filters, navigation)
            leftZone = CreateZoneContainer(LeftZoneWidth, double.NaN, HorizontalAlignment.Left, VerticalAlignment.Stretch);
            Panel.SetZIndex(leftZone, 1000);
            Grid.SetRow(leftZone, 1); // Place in workspace row, not title bar
            rootContainer.Children.Add(leftZone);

            // Right zone (details, context)
            rightZone = CreateZoneContainer(RightZoneWidth, double.NaN, HorizontalAlignment.Right, VerticalAlignment.Stretch);
            Panel.SetZIndex(rightZone, 1000);
            Grid.SetRow(rightZone, 1); // Place in workspace row
            rootContainer.Children.Add(rightZone);

            // Top zone (command palette, search)
            topZone = CreateZoneContainer(double.NaN, TopZoneHeight, HorizontalAlignment.Stretch, VerticalAlignment.Top);
            Panel.SetZIndex(topZone, 1500);
            Grid.SetRow(topZone, 1); // Place in workspace row
            rootContainer.Children.Add(topZone);

            // Bottom zone (quick add, inline creation)
            bottomZone = CreateZoneContainer(double.NaN, BottomZoneHeight, HorizontalAlignment.Stretch, VerticalAlignment.Bottom);
            Panel.SetZIndex(bottomZone, 1500);
            Grid.SetRow(bottomZone, 1); // Place in workspace row
            rootContainer.Children.Add(bottomZone);

            // Center zone (modals, full dialogs)
            centerZone = CreateZoneContainer(CenterZoneMaxWidth, CenterZoneMaxHeight, HorizontalAlignment.Center, VerticalAlignment.Center);
            Panel.SetZIndex(centerZone, 2000);  // Highest - modals on top
            Grid.SetRow(centerZone, 1); // Place in workspace row
            rootContainer.Children.Add(centerZone);
        }

        /// <summary>
        /// Create a zone container with styling
        /// </summary>
        private Grid CreateZoneContainer(double width, double height, HorizontalAlignment hAlign, VerticalAlignment vAlign)
        {
            var theme = themeManager.CurrentTheme;

            var zone = new Grid
            {
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Background = new SolidColorBrush(Color.FromArgb(242, theme.Surface.R, theme.Surface.G, theme.Surface.B)),  // 95% opacity
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };

            if (!double.IsNaN(width))
                zone.Width = width;

            if (!double.IsNaN(height))
                zone.Height = height;

            // Add drop shadow effect (floats over content)
            zone.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 20,
                ShadowDepth = 5,
                Opacity = 0.5
            };

            // Add border for terminal aesthetic
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(2),
                Child = new Grid()  // Content will be added here
            };

            zone.Children.Add(border);

            return zone;
        }

        #region Left Zone (Filters, Navigation)

        /// <summary>
        /// Show left zone with content
        /// </summary>
        public void ShowLeftZone(UIElement content)
        {
            if (isLeftZoneVisible && leftZoneContent == content)
                return;  // Already showing this content

            // Set content
            var border = leftZone.Children[0] as Border;
            var contentGrid = border.Child as Grid;
            contentGrid.Children.Clear();
            contentGrid.Children.Add(content);
            leftZoneContent = content;

            // Show and animate
            leftZone.Visibility = Visibility.Visible;

            // Slide from left
            var transform = new TranslateTransform(-LeftZoneWidth, 0);
            leftZone.RenderTransform = transform;

            var slideAnim = new DoubleAnimation
            {
                From = -LeftZoneWidth,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            transform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            leftZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            // Adapt workspace (shift right, shrink width)
            AdaptWorkspaceForLeftZone(true);

            isLeftZoneVisible = true;
            ZoneOpened?.Invoke(OverlayZone.Left);

            logger?.Debug("OverlayManager", "Opened left zone");
        }

        /// <summary>
        /// Hide left zone
        /// </summary>
        public void HideLeftZone()
        {
            if (!isLeftZoneVisible)
                return;

            var transform = leftZone.RenderTransform as TranslateTransform;

            var slideAnim = new DoubleAnimation
            {
                From = 0,
                To = -LeftZoneWidth,
                Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            slideAnim.Completed += (s, e) =>
            {
                leftZone.Visibility = Visibility.Collapsed;
                leftZoneContent = null;
            };

            transform?.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            leftZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            // Restore workspace width
            AdaptWorkspaceForLeftZone(false);

            isLeftZoneVisible = false;
            ZoneClosed?.Invoke(OverlayZone.Left);

            logger?.Debug("OverlayManager", "Closed left zone");
        }

        /// <summary>
        /// Adapt workspace container when left zone opens/closes
        /// </summary>
        private void AdaptWorkspaceForLeftZone(bool show)
        {
            if (workspaceContainer is FrameworkElement fe)
            {
                var targetMargin = show
                    ? new Thickness(LeftZoneWidth + 10, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom)
                    : new Thickness(0, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);

                var marginAnim = new ThicknessAnimation
                {
                    From = fe.Margin,
                    To = targetMargin,
                    Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                fe.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);
            }
        }

        #endregion

        #region Right Zone (Details, Context)

        /// <summary>
        /// Show right zone with content
        /// </summary>
        public void ShowRightZone(UIElement content)
        {
            if (isRightZoneVisible && rightZoneContent == content)
                return;

            var border = rightZone.Children[0] as Border;
            var contentGrid = border.Child as Grid;
            contentGrid.Children.Clear();
            contentGrid.Children.Add(content);
            rightZoneContent = content;

            rightZone.Visibility = Visibility.Visible;

            var transform = new TranslateTransform(RightZoneWidth, 0);
            rightZone.RenderTransform = transform;

            var slideAnim = new DoubleAnimation
            {
                From = RightZoneWidth,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            transform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            rightZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            AdaptWorkspaceForRightZone(true);

            isRightZoneVisible = true;
            ZoneOpened?.Invoke(OverlayZone.Right);

            logger?.Debug("OverlayManager", "Opened right zone");
        }

        /// <summary>
        /// Hide right zone
        /// </summary>
        public void HideRightZone()
        {
            if (!isRightZoneVisible)
                return;

            var transform = rightZone.RenderTransform as TranslateTransform;

            var slideAnim = new DoubleAnimation
            {
                From = 0,
                To = RightZoneWidth,
                Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            slideAnim.Completed += (s, e) =>
            {
                rightZone.Visibility = Visibility.Collapsed;
                rightZoneContent = null;
            };

            transform?.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            rightZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            AdaptWorkspaceForRightZone(false);

            isRightZoneVisible = false;
            ZoneClosed?.Invoke(OverlayZone.Right);

            logger?.Debug("OverlayManager", "Closed right zone");
        }

        private void AdaptWorkspaceForRightZone(bool show)
        {
            if (workspaceContainer is FrameworkElement fe)
            {
                var targetMargin = show
                    ? new Thickness(fe.Margin.Left, fe.Margin.Top, RightZoneWidth + 10, fe.Margin.Bottom)
                    : new Thickness(fe.Margin.Left, fe.Margin.Top, 0, fe.Margin.Bottom);

                var marginAnim = new ThicknessAnimation
                {
                    From = fe.Margin,
                    To = targetMargin,
                    Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                fe.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);
            }
        }

        #endregion

        #region Top Zone (Command Palette, Search)

        public void ShowTopZone(UIElement content)
        {
            if (isTopZoneVisible && topZoneContent == content)
                return;

            var border = topZone.Children[0] as Border;
            var contentGrid = border.Child as Grid;
            contentGrid.Children.Clear();
            contentGrid.Children.Add(content);
            topZoneContent = content;

            topZone.Visibility = Visibility.Visible;

            var transform = new TranslateTransform(0, -TopZoneHeight);
            topZone.RenderTransform = transform;

            var slideAnim = new DoubleAnimation
            {
                From = -TopZoneHeight,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            transform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            topZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            DimWorkspace(true);

            isTopZoneVisible = true;
            ZoneOpened?.Invoke(OverlayZone.Top);

            logger?.Debug("OverlayManager", "Opened top zone");
        }

        public void HideTopZone()
        {
            if (!isTopZoneVisible)
                return;

            var transform = topZone.RenderTransform as TranslateTransform;

            var slideAnim = new DoubleAnimation
            {
                From = 0,
                To = -TopZoneHeight,
                Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            slideAnim.Completed += (s, e) =>
            {
                topZone.Visibility = Visibility.Collapsed;
                topZoneContent = null;
            };

            transform?.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            topZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            DimWorkspace(false);

            isTopZoneVisible = false;
            ZoneClosed?.Invoke(OverlayZone.Top);

            logger?.Debug("OverlayManager", "Closed top zone");
        }

        #endregion

        #region Bottom Zone (Quick Add, Inline Creation)

        public void ShowBottomZone(UIElement content)
        {
            logger?.Debug("OverlayManager", $"ShowBottomZone called - bottomZone null? {bottomZone == null}");

            if (isBottomZoneVisible && bottomZoneContent == content)
                return;

            if (bottomZone == null)
            {
                logger?.Error("OverlayManager", "bottomZone is NULL - Initialize() was not called properly!");
                throw new InvalidOperationException("OverlayManager.Initialize() must be called before showing overlays");
            }

            var border = bottomZone.Children[0] as Border;
            var contentGrid = border.Child as Grid;
            contentGrid.Children.Clear();
            contentGrid.Children.Add(content);
            bottomZoneContent = content;

            bottomZone.Visibility = Visibility.Visible;

            var transform = new TranslateTransform(0, BottomZoneHeight);
            bottomZone.RenderTransform = transform;

            var slideAnim = new DoubleAnimation
            {
                From = BottomZoneHeight,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            transform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            bottomZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            isBottomZoneVisible = true;
            ZoneOpened?.Invoke(OverlayZone.Bottom);

            logger?.Debug("OverlayManager", "Opened bottom zone");
        }

        public void HideBottomZone()
        {
            if (!isBottomZoneVisible)
                return;

            var transform = bottomZone.RenderTransform as TranslateTransform;

            var slideAnim = new DoubleAnimation
            {
                From = 0,
                To = BottomZoneHeight,
                Duration = TimeSpan.FromMilliseconds(SlideAnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            slideAnim.Completed += (s, e) =>
            {
                bottomZone.Visibility = Visibility.Collapsed;
                bottomZoneContent = null;
            };

            transform?.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            bottomZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            isBottomZoneVisible = false;
            ZoneClosed?.Invoke(OverlayZone.Bottom);

            logger?.Debug("OverlayManager", "Closed bottom zone");
        }

        #endregion

        #region Center Zone (Modals, Full Dialogs)

        public void ShowCenterZone(UIElement content)
        {
            if (isCenterZoneVisible && centerZoneContent == content)
                return;

            var border = centerZone.Children[0] as Border;
            var contentGrid = border.Child as Grid;
            contentGrid.Children.Clear();
            contentGrid.Children.Add(content);
            centerZoneContent = content;

            centerZone.Visibility = Visibility.Visible;

            // Fade in (no slide for center modals)
            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            centerZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            DimWorkspace(true, 0.3);  // Dim more for modals

            isCenterZoneVisible = true;
            ZoneOpened?.Invoke(OverlayZone.Center);

            logger?.Debug("OverlayManager", "Opened center zone");
        }

        public void HideCenterZone()
        {
            if (!isCenterZoneVisible)
                return;

            var fadeAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            fadeAnim.Completed += (s, e) =>
            {
                centerZone.Visibility = Visibility.Collapsed;
                centerZoneContent = null;
            };

            centerZone.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            DimWorkspace(false);

            isCenterZoneVisible = false;
            ZoneClosed?.Invoke(OverlayZone.Center);

            logger?.Debug("OverlayManager", "Closed center zone");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Dim workspace when overlays are active
        /// </summary>
        private void DimWorkspace(bool dim, double targetOpacity = 0.7)
        {
            if (workspaceContainer == null)
                return;

            var opacityAnim = new DoubleAnimation
            {
                From = workspaceContainer.Opacity,
                To = dim ? targetOpacity : 1.0,
                Duration = TimeSpan.FromMilliseconds(FadeAnimationDuration)
            };

            workspaceContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        /// <summary>
        /// Check if any overlay zone is currently visible
        /// </summary>
        public bool IsAnyOverlayVisible =>
            isLeftZoneVisible || isRightZoneVisible || isTopZoneVisible ||
            isBottomZoneVisible || isCenterZoneVisible;

        /// <summary>
        /// Get count of visible overlays
        /// </summary>
        public int VisibleOverlayCount
        {
            get
            {
                int count = 0;
                if (isLeftZoneVisible) count++;
                if (isRightZoneVisible) count++;
                if (isTopZoneVisible) count++;
                if (isBottomZoneVisible) count++;
                if (isCenterZoneVisible) count++;
                return count;
            }
        }

        /// <summary>
        /// Close all overlays (cascade close with Esc)
        /// </summary>
        public void CloseAllOverlays()
        {
            if (isCenterZoneVisible)
                HideCenterZone();
            else if (isTopZoneVisible)
                HideTopZone();
            else if (isBottomZoneVisible)
                HideBottomZone();
            else if (isRightZoneVisible)
                HideRightZone();
            else if (isLeftZoneVisible)
                HideLeftZone();
        }

        /// <summary>
        /// Handle keyboard input for overlay zones
        /// Returns true if key was handled, false if should be passed to workspace
        /// </summary>
        public bool HandleKeyDown(KeyEventArgs e)
        {
            // Esc closes active overlays (cascade close - highest priority first)
            if (e.Key == Key.Escape)
            {
                if (IsAnyOverlayVisible)
                {
                    CloseAllOverlays();
                    return true;  // Consumed
                }
            }

            // Tab cycles focus between overlay and workspace
            if (e.Key == Key.Tab)
            {
                if (IsAnyOverlayVisible)
                {
                    // Let overlay content handle tab first
                    // If not handled, cycle to workspace
                    return false;  // Pass to focused element
                }
            }

            // Delegate to focused overlay content if it handles IInputElement
            // This allows overlays to handle their own keyboard shortcuts

            return false;  // Not handled by overlay manager
        }

        /// <summary>
        /// Toggle left zone (open if closed, close if open)
        /// </summary>
        public void ToggleLeftZone(UIElement content = null)
        {
            if (isLeftZoneVisible)
                HideLeftZone();
            else if (content != null)
                ShowLeftZone(content);
        }

        /// <summary>
        /// Toggle right zone
        /// </summary>
        public void ToggleRightZone(UIElement content = null)
        {
            if (isRightZoneVisible)
                HideRightZone();
            else if (content != null)
                ShowRightZone(content);
        }

        #endregion
    }
}
