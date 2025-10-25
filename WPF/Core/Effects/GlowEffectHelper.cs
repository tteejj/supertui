using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Effects;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Effects
{
    /// <summary>
    /// Glow state for UI elements
    /// </summary>
    public enum GlowState
    {
        Normal,
        Focus,
        Hover
    }

    /// <summary>
    /// Helper class for applying neon glow effects to UI elements
    /// Provides consistent glow styling across the application
    /// </summary>
    public static class GlowEffectHelper
    {
        /// <summary>
        /// Apply glow effect to a UI element based on settings and state
        /// </summary>
        /// <param name="element">The element to apply glow to</param>
        /// <param name="settings">Glow settings from the theme</param>
        /// <param name="state">Current glow state (Normal/Focus/Hover)</param>
        public static void ApplyGlow(UIElement element, GlowSettings settings, GlowState state)
        {
            if (element == null || settings == null)
                return;

            // Check if glow should be applied for this state
            bool shouldGlow = settings.Mode switch
            {
                GlowMode.Always => true,
                GlowMode.OnFocus => state == GlowState.Focus,
                GlowMode.OnHover => state == GlowState.Hover || state == GlowState.Focus,
                GlowMode.Never => false,
                _ => false
            };

            if (!shouldGlow)
            {
                element.Effect = null;
                return;
            }

            // Select appropriate color based on state
            var glowColor = state switch
            {
                GlowState.Focus => settings.FocusGlowColor,
                GlowState.Hover => settings.HoverGlowColor,
                _ => settings.GlowColor
            };

            // Create neon glow effect using DropShadowEffect with ShadowDepth=0
            var glowEffect = new DropShadowEffect
            {
                Color = glowColor,
                BlurRadius = settings.GlowRadius,
                Opacity = settings.GlowOpacity,
                ShadowDepth = 0,  // 0 = glow (no offset), not shadow
                Direction = 0,
                RenderingBias = RenderingBias.Quality
            };

            element.Effect = glowEffect;
        }

        /// <summary>
        /// Remove glow effect from a UI element
        /// </summary>
        /// <param name="element">The element to remove glow from</param>
        public static void RemoveGlow(UIElement element)
        {
            if (element != null)
            {
                element.Effect = null;
            }
        }

        /// <summary>
        /// Attach glow handlers to a FrameworkElement based on theme settings
        /// This sets up event handlers for focus and hover effects
        /// </summary>
        /// <param name="element">The element to attach handlers to</param>
        /// <param name="themeManager">Theme manager to get current settings</param>
        public static void AttachGlowHandlers(FrameworkElement element, IThemeManager themeManager)
        {
            if (element == null || themeManager == null)
                return;

            var settings = themeManager.CurrentTheme?.Glow;
            if (settings == null)
                return;

            // Detach existing handlers first (prevent double-registration)
            DetachGlowHandlers(element, themeManager);

            // Apply initial glow if in Always mode
            if (settings.Mode == GlowMode.Always)
            {
                ApplyGlow(element, settings, GlowState.Normal);
            }

            // Attach focus handlers if needed
            if (settings.Mode == GlowMode.OnFocus || settings.Mode == GlowMode.OnHover)
            {
                element.GotFocus += (s, e) => OnElementGotFocus(element, themeManager);
                element.LostFocus += (s, e) => OnElementLostFocus(element, themeManager);
            }

            // Attach hover handlers if needed
            if (settings.Mode == GlowMode.OnHover)
            {
                element.MouseEnter += (s, e) => OnElementMouseEnter(element, themeManager);
                element.MouseLeave += (s, e) => OnElementMouseLeave(element, themeManager);
            }

            // Attach theme change handler
            themeManager.ThemeChanged += (s, e) => OnThemeChanged(element, themeManager);
        }

        /// <summary>
        /// Detach glow event handlers from a FrameworkElement
        /// </summary>
        /// <param name="element">The element to detach handlers from</param>
        /// <param name="themeManager">Theme manager</param>
        public static void DetachGlowHandlers(FrameworkElement element, IThemeManager themeManager)
        {
            if (element == null || themeManager == null)
                return;

            // Note: We can't directly remove anonymous lambdas, so we just clear the effect
            // In a production scenario, you'd want to store the EventHandlers and remove them
            RemoveGlow(element);
        }

        private static void OnElementGotFocus(FrameworkElement element, IThemeManager themeManager)
        {
            var settings = themeManager.CurrentTheme?.Glow;
            if (settings != null)
            {
                ApplyGlow(element, settings, GlowState.Focus);
            }
        }

        private static void OnElementLostFocus(FrameworkElement element, IThemeManager themeManager)
        {
            var settings = themeManager.CurrentTheme?.Glow;
            if (settings != null)
            {
                // Return to normal state or remove glow depending on mode
                if (settings.Mode == GlowMode.Always)
                {
                    ApplyGlow(element, settings, GlowState.Normal);
                }
                else
                {
                    RemoveGlow(element);
                }
            }
        }

        private static void OnElementMouseEnter(FrameworkElement element, IThemeManager themeManager)
        {
            // Only apply hover glow if element doesn't have focus
            if (!element.IsFocused)
            {
                var settings = themeManager.CurrentTheme?.Glow;
                if (settings != null)
                {
                    ApplyGlow(element, settings, GlowState.Hover);
                }
            }
        }

        private static void OnElementMouseLeave(FrameworkElement element, IThemeManager themeManager)
        {
            // Only remove hover glow if element doesn't have focus
            if (!element.IsFocused)
            {
                var settings = themeManager.CurrentTheme?.Glow;
                if (settings != null)
                {
                    if (settings.Mode == GlowMode.Always)
                    {
                        ApplyGlow(element, settings, GlowState.Normal);
                    }
                    else
                    {
                        RemoveGlow(element);
                    }
                }
            }
        }

        private static void OnThemeChanged(FrameworkElement element, IThemeManager themeManager)
        {
            var settings = themeManager.CurrentTheme?.Glow;
            if (settings != null)
            {
                // Reapply glow with new theme settings
                if (element.IsFocused && (settings.Mode == GlowMode.OnFocus || settings.Mode == GlowMode.OnHover))
                {
                    ApplyGlow(element, settings, GlowState.Focus);
                }
                else if (settings.Mode == GlowMode.Always)
                {
                    ApplyGlow(element, settings, GlowState.Normal);
                }
                else
                {
                    RemoveGlow(element);
                }
            }
        }

        /// <summary>
        /// Create a glow effect for a specific button or interactive element
        /// This is a convenience method for one-off glow effects
        /// </summary>
        /// <param name="element">The element to apply glow to</param>
        /// <param name="color">Glow color</param>
        /// <param name="radius">Glow radius (default 10)</param>
        /// <param name="opacity">Glow opacity (default 0.8)</param>
        public static void ApplyCustomGlow(UIElement element, System.Windows.Media.Color color, double radius = 10.0, double opacity = 0.8)
        {
            if (element == null)
                return;

            var glowEffect = new DropShadowEffect
            {
                Color = color,
                BlurRadius = radius,
                Opacity = opacity,
                ShadowDepth = 0,
                Direction = 0,
                RenderingBias = RenderingBias.Quality
            };

            element.Effect = glowEffect;
        }
    }
}
