using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Overlay that appears when user presses 'G' for quick widget jumping
    /// Shows available widget jump targets with single-key activation
    /// </summary>
    public class QuickJumpOverlay : Border
    {
        private readonly IThemeManager themeManager;
        private readonly ILogger logger;

        private Grid overlayGrid;
        private Border contentBorder;
        private TextBlock titleText;
        private StackPanel jumpPanel;

        private Dictionary<Key, JumpTarget> jumpTargets = new();
        public event Action<string, object> JumpRequested;
        public event Action CloseRequested;

        public QuickJumpOverlay(IThemeManager themeManager, ILogger logger)
        {
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            BuildUI();
            this.Visibility = Visibility.Collapsed;
            this.IsVisibleChanged += OnVisibilityChanged;
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            // Main overlay grid
            overlayGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)) // Semi-transparent black
            };

            // Center content border
            contentBorder = new Border
            {
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                MaxWidth = 500,
                MaxHeight = 400,
                Margin = new Thickness(50),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = theme.Primary,
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.8
                }
            };

            var mainPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // Title
            titleText = new TextBlock
            {
                Text = "⚡ QUICK JUMP",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 0, 0, 15),
                TextAlignment = TextAlignment.Center
            };

            // Jump options panel
            jumpPanel = new StackPanel
            {
                Margin = new Thickness(0)
            };

            // Footer
            var footerText = new TextBlock
            {
                Text = "Press key to jump  •  Esc to cancel",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 15, 0, 0),
                TextAlignment = TextAlignment.Center,
                FontStyle = FontStyles.Italic
            };

            mainPanel.Children.Add(titleText);
            mainPanel.Children.Add(jumpPanel);
            mainPanel.Children.Add(footerText);

            contentBorder.Child = mainPanel;
            overlayGrid.Children.Add(contentBorder);

            this.Child = overlayGrid;

            // Handle clicks on overlay background to close
            overlayGrid.MouseDown += (s, e) =>
            {
                if (e.OriginalSource == overlayGrid)
                {
                    Hide();
                    e.Handled = true;
                }
            };

            // Handle keyboard
            this.KeyDown += OnKeyDown;
            this.Focusable = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.G)
            {
                Hide();
                e.Handled = true;
                return;
            }

            // Check if this key matches a jump target
            if (jumpTargets.ContainsKey(e.Key))
            {
                var target = jumpTargets[e.Key];
                logger.Info("QuickJump", $"Jumping to {target.TargetWidget}");
                JumpRequested?.Invoke(target.TargetWidget, target.Context);
                Hide();
                e.Handled = true;
            }
        }

        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
            {
                System.Windows.Input.Keyboard.Focus(this);
                System.Windows.Input.Keyboard.Focus(this);
            }
        }

        /// <summary>
        /// Register a jump target accessible via single keystroke
        /// </summary>
        public void RegisterJump(Key key, string targetWidget, string description, Func<object> contextProvider = null)
        {
            jumpTargets[key] = new JumpTarget
            {
                Key = key,
                TargetWidget = targetWidget,
                Description = description,
                ContextProvider = contextProvider
            };
        }

        /// <summary>
        /// Clear all registered jump targets
        /// </summary>
        public void ClearJumps()
        {
            jumpTargets.Clear();
        }

        /// <summary>
        /// Show the overlay with current jump targets
        /// </summary>
        public void Show()
        {
            UpdateJumpDisplay();
            this.Visibility = Visibility.Visible;
            logger.Info("QuickJump", "Overlay shown");
        }

        /// <summary>
        /// Hide the overlay
        /// </summary>
        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
            CloseRequested?.Invoke();
            logger.Info("QuickJump", "Overlay hidden");
        }

        private void UpdateJumpDisplay()
        {
            jumpPanel.Children.Clear();

            var theme = themeManager.CurrentTheme;

            // Sort jump targets by key
            var sortedTargets = new List<JumpTarget>(jumpTargets.Values);
            sortedTargets.Sort((a, b) => a.Key.CompareTo(b.Key));

            foreach (var target in sortedTargets)
            {
                var itemGrid = new Grid
                {
                    Margin = new Thickness(0, 3, 0, 3)
                };

                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Key display
                var keyText = new TextBlock
                {
                    Text = FormatKey(target.Key),
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(theme.SyntaxKeyword)
                };

                // Description
                var descText = new TextBlock
                {
                    Text = target.Description,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(theme.Foreground),
                    TextWrapping = TextWrapping.Wrap
                };

                Grid.SetColumn(keyText, 0);
                Grid.SetColumn(descText, 1);

                itemGrid.Children.Add(keyText);
                itemGrid.Children.Add(descText);

                jumpPanel.Children.Add(itemGrid);
            }

            // If no jumps registered, show message
            if (sortedTargets.Count == 0)
            {
                var noJumpsText = new TextBlock
                {
                    Text = "No quick jumps available from this widget",
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                    FontStyle = FontStyles.Italic,
                    TextAlignment = TextAlignment.Center
                };
                jumpPanel.Children.Add(noJumpsText);
            }
        }

        private string FormatKey(Key key)
        {
            // Format key for display
            return key.ToString().ToUpper();
        }

        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            if (contentBorder != null)
            {
                contentBorder.Background = new SolidColorBrush(theme.Background);
                contentBorder.BorderBrush = new SolidColorBrush(theme.Primary);

                if (contentBorder.Effect is DropShadowEffect effect)
                {
                    effect.Color = theme.Primary;
                }
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.Primary);
            }

            // Refresh jump display with new colors
            UpdateJumpDisplay();
        }

        private class JumpTarget
        {
            public Key Key { get; set; }
            public string TargetWidget { get; set; }
            public string Description { get; set; }
            public Func<object> ContextProvider { get; set; }

            public object Context => ContextProvider?.Invoke();
        }
    }
}
