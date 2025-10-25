using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// WPF Canvas overlay that renders CRT-style scanlines and bloom effects.
    /// Designed to be layered on top of the main UI to provide retro terminal aesthetics.
    /// </summary>
    public class CRTEffectsOverlay : Canvas
    {
        private List<Rectangle> scanlineCache = new List<Rectangle>();
        private bool isInitialized = false;

        #region Dependency Properties

        public static readonly DependencyProperty EnableScanlinesProperty =
            DependencyProperty.Register(
                nameof(EnableScanlines),
                typeof(bool),
                typeof(CRTEffectsOverlay),
                new PropertyMetadata(true, OnScanlinePropertyChanged));

        public static readonly DependencyProperty ScanlineOpacityProperty =
            DependencyProperty.Register(
                nameof(ScanlineOpacity),
                typeof(double),
                typeof(CRTEffectsOverlay),
                new PropertyMetadata(0.15, OnScanlinePropertyChanged));

        public static readonly DependencyProperty ScanlineSpacingProperty =
            DependencyProperty.Register(
                nameof(ScanlineSpacing),
                typeof(int),
                typeof(CRTEffectsOverlay),
                new PropertyMetadata(4, OnScanlinePropertyChanged));

        public static readonly DependencyProperty ScanlineColorProperty =
            DependencyProperty.Register(
                nameof(ScanlineColor),
                typeof(Color),
                typeof(CRTEffectsOverlay),
                new PropertyMetadata(Colors.Black, OnScanlinePropertyChanged));

        public static readonly DependencyProperty EnableBloomProperty =
            DependencyProperty.Register(
                nameof(EnableBloom),
                typeof(bool),
                typeof(CRTEffectsOverlay),
                new PropertyMetadata(false, OnBloomPropertyChanged));

        public static readonly DependencyProperty BloomIntensityProperty =
            DependencyProperty.Register(
                nameof(BloomIntensity),
                typeof(double),
                typeof(CRTEffectsOverlay),
                new PropertyMetadata(0.3, OnBloomPropertyChanged));

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets whether scanlines are rendered.
        /// </summary>
        public bool EnableScanlines
        {
            get => (bool)GetValue(EnableScanlinesProperty);
            set => SetValue(EnableScanlinesProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of scanlines (0.0 to 1.0).
        /// Typical values: 0.1 to 0.3 for subtle effect.
        /// </summary>
        public double ScanlineOpacity
        {
            get => (double)GetValue(ScanlineOpacityProperty);
            set => SetValue(ScanlineOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the spacing between scanlines in pixels.
        /// Typical values: 2 to 6 pixels.
        /// </summary>
        public int ScanlineSpacing
        {
            get => (int)GetValue(ScanlineSpacingProperty);
            set => SetValue(ScanlineSpacingProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of scanlines.
        /// Typically black for darkening effect.
        /// </summary>
        public Color ScanlineColor
        {
            get => (Color)GetValue(ScanlineColorProperty);
            set => SetValue(ScanlineColorProperty, value);
        }

        /// <summary>
        /// Gets or sets whether bloom (glow) effect is enabled.
        /// </summary>
        public bool EnableBloom
        {
            get => (bool)GetValue(EnableBloomProperty);
            set => SetValue(EnableBloomProperty, value);
        }

        /// <summary>
        /// Gets or sets the intensity of the bloom effect (0.0 to 1.0).
        /// Higher values create stronger glow on bright elements.
        /// </summary>
        public double BloomIntensity
        {
            get => (double)GetValue(BloomIntensityProperty);
            set => SetValue(BloomIntensityProperty, value);
        }

        #endregion

        #region Constructor

        public CRTEffectsOverlay()
        {
            // Overlay should not block input
            IsHitTestVisible = false;

            // Subscribe to size changes
            SizeChanged += OnSizeChanged;
            Loaded += OnLoaded;

            // Set background to transparent
            Background = Brushes.Transparent;
        }

        #endregion

        #region Event Handlers

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!isInitialized)
            {
                isInitialized = true;
                RebuildScanlines();
                ApplyBloom();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (isInitialized && EnableScanlines)
            {
                RebuildScanlines();
            }
        }

        private static void OnScanlinePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var overlay = d as CRTEffectsOverlay;
            if (overlay != null && overlay.isInitialized)
            {
                overlay.RebuildScanlines();
            }
        }

        private static void OnBloomPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var overlay = d as CRTEffectsOverlay;
            if (overlay != null && overlay.isInitialized)
            {
                overlay.ApplyBloom();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Rebuilds the scanline overlay based on current settings.
        /// Called automatically when properties change or size changes.
        /// </summary>
        public void RebuildScanlines()
        {
            // Clear existing scanlines
            Children.Clear();
            scanlineCache.Clear();

            // Don't render if disabled or dimensions are invalid
            if (!EnableScanlines || ActualHeight <= 0 || ActualWidth <= 0 || ScanlineSpacing <= 0)
            {
                return;
            }

            // Clamp opacity to valid range
            double opacity = Math.Max(0.0, Math.Min(1.0, ScanlineOpacity));

            // Create brush once for all scanlines
            var brush = new SolidColorBrush(ScanlineColor)
            {
                Opacity = opacity
            };

            // Freeze brush for better performance
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            // Generate scanlines
            for (int y = 0; y < ActualHeight; y += ScanlineSpacing)
            {
                var line = new Rectangle
                {
                    Width = ActualWidth,
                    Height = 1,
                    Fill = brush,
                    IsHitTestVisible = false // Scanlines don't block input
                };

                Canvas.SetLeft(line, 0);
                Canvas.SetTop(line, y);

                Children.Add(line);
                scanlineCache.Add(line);
            }
        }

        /// <summary>
        /// Applies or removes the bloom effect based on current settings.
        /// Called automatically when bloom properties change.
        /// </summary>
        public void ApplyBloom()
        {
            if (EnableBloom && BloomIntensity > 0)
            {
                // Calculate blur radius (0-20 pixels based on intensity)
                double blurRadius = Math.Max(0, Math.Min(20, BloomIntensity * 20));

                // Create blur effect for bloom/glow
                var blurEffect = new BlurEffect
                {
                    Radius = blurRadius,
                    KernelType = KernelType.Gaussian,
                    RenderingBias = RenderingBias.Performance
                };

                Effect = blurEffect;
            }
            else
            {
                // Remove effect
                Effect = null;
            }
        }

        /// <summary>
        /// Updates the overlay from theme settings.
        /// Call this when the theme changes.
        /// </summary>
        /// <param name="enableScanlines">Whether to enable scanlines</param>
        /// <param name="scanlineOpacity">Scanline opacity (0.0 to 1.0)</param>
        /// <param name="scanlineSpacing">Spacing between scanlines in pixels</param>
        /// <param name="scanlineColor">Color of scanlines</param>
        /// <param name="enableBloom">Whether to enable bloom effect</param>
        /// <param name="bloomIntensity">Bloom intensity (0.0 to 1.0)</param>
        public void UpdateFromTheme(
            bool enableScanlines,
            double scanlineOpacity,
            int scanlineSpacing,
            Color scanlineColor,
            bool enableBloom,
            double bloomIntensity)
        {
            // Batch updates to avoid multiple rebuilds
            EnableScanlines = enableScanlines;
            ScanlineOpacity = scanlineOpacity;
            ScanlineSpacing = scanlineSpacing;
            ScanlineColor = scanlineColor;
            EnableBloom = enableBloom;
            BloomIntensity = bloomIntensity;

            // Force rebuild with new settings
            if (isInitialized)
            {
                RebuildScanlines();
                ApplyBloom();
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleans up resources when overlay is removed.
        /// </summary>
        public void Dispose()
        {
            SizeChanged -= OnSizeChanged;
            Loaded -= OnLoaded;
            Children.Clear();
            scanlineCache.Clear();
        }

        #endregion
    }
}
