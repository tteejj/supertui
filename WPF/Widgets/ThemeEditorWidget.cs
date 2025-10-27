using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Theme Editor Widget - Visual theme customization interface
    /// Allows users to edit theme colors, effects, and typography with live preview
    /// </summary>
    public class ThemeEditorWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly ThemeManager themeManager; // Using concrete class for SetColorOverride/ResetOverride/GetEffectiveColor
        private readonly IConfigurationManager config;

        // UI Components
        private ComboBox themeSelector;
        private ScrollViewer mainScrollViewer;
        private Dictionary<string, Button> colorPickers = new Dictionary<string, Button>();
        private Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();
        private Dictionary<string, CheckBox> checkBoxes = new Dictionary<string, CheckBox>();
        private Dictionary<string, ComboBox> comboBoxes = new Dictionary<string, ComboBox>();

        // State tracking
        private bool isUpdatingUI = false;

        public ThemeEditorWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = (themeManager as ThemeManager) ?? ThemeManager.Instance;
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            WidgetName = "Theme Editor";
            WidgetType = "ThemeEditor";
        }


        public override void Initialize()
        {
            try
            {
                BuildUI();
                ApplyTheme();

                // Subscribe to theme changes
                themeManager.ThemeChanged += OnThemeManagerChanged;

                logger.Info("ThemeEditor", "Theme Editor initialized");
            }
            catch (Exception ex)
            {
                logger.Error("ThemeEditor", $"Failed to initialize: {ex.Message}", ex);
            }
        }

        private void BuildUI()
        {
            var mainGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26))
            };

            mainScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(10)
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(5)
            };

            // Add all sections
            stackPanel.Children.Add(CreateSectionHeader("Theme Selection"));
            stackPanel.Children.Add(CreateThemeSelectionSection());
            stackPanel.Children.Add(CreateSeparator());

            stackPanel.Children.Add(CreateCollapsibleSection("Color Overrides", CreateColorOverridesSection()));
            stackPanel.Children.Add(CreateSeparator());

            stackPanel.Children.Add(CreateCollapsibleSection("Glow Settings", CreateGlowSettingsSection()));
            stackPanel.Children.Add(CreateSeparator());

            stackPanel.Children.Add(CreateCollapsibleSection("CRT Effects", CreateCRTEffectsSection()));
            stackPanel.Children.Add(CreateSeparator());

            stackPanel.Children.Add(CreateCollapsibleSection("Opacity", CreateOpacitySection()));
            stackPanel.Children.Add(CreateSeparator());

            stackPanel.Children.Add(CreateCollapsibleSection("Typography", CreateTypographySection()));

            mainScrollViewer.Content = stackPanel;
            mainGrid.Children.Add(mainScrollViewer);
            Content = mainGrid;
        }

        #region UI Section Builders

        private UIElement CreateSectionHeader(string text)
        {
            var header = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 10, 0, 5)
            };
            return header;
        }

        private UIElement CreateSeparator()
        {
            return new Separator
            {
                Margin = new Thickness(0, 10, 0, 5),
                Background = new SolidColorBrush(Color.FromRgb(58, 58, 58))
            };
        }

        private Expander CreateCollapsibleSection(string title, UIElement content)
        {
            var expander = new Expander
            {
                Header = title,
                IsExpanded = true,
                Margin = new Thickness(0, 5, 0, 5),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            var contentBorder = new Border
            {
                Child = content,
                Margin = new Thickness(10, 5, 0, 5)
            };

            expander.Content = contentBorder;
            return expander;
        }

        private UIElement CreateThemeSelectionSection()
        {
            var panel = new StackPanel();

            // Theme selector
            var selectorPanel = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };

            var label = new TextBlock
            {
                Text = "Current Theme:",
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.White
            };
            DockPanel.SetDock(label, Dock.Left);

            themeSelector = new ComboBox
            {
                Width = 200,
                FontFamily = new FontFamily("Consolas")
            };

            var themes = themeManager.GetAvailableThemes();
            foreach (var theme in themes)
            {
                themeSelector.Items.Add(theme.Name);
            }
            themeSelector.SelectedItem = themeManager.CurrentTheme?.Name ?? "Dark";
            themeSelector.SelectionChanged += OnThemeSelectionChanged;

            selectorPanel.Children.Add(label);
            selectorPanel.Children.Add(themeSelector);
            panel.Children.Add(selectorPanel);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var resetButton = new Button
            {
                Content = "Reset All Overrides",
                Width = 150,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Consolas")
            };
            resetButton.Click += OnResetAllOverrides;

            var saveButton = new Button
            {
                Content = "Save Theme As...",
                Width = 150,
                Height = 30,
                FontFamily = new FontFamily("Consolas")
            };
            saveButton.Click += OnSaveThemeAs;

            buttonPanel.Children.Add(resetButton);
            buttonPanel.Children.Add(saveButton);
            panel.Children.Add(buttonPanel);

            return panel;
        }

        private UIElement CreateColorOverridesSection()
        {
            var panel = new StackPanel();

            // Define color properties to expose
            var colorProperties = new[]
            {
                "Primary", "Secondary", "Success", "Warning", "Error", "Info",
                "Background", "BackgroundSecondary", "Surface", "SurfaceHighlight",
                "Foreground", "ForegroundSecondary", "ForegroundDisabled",
                "Border", "BorderActive", "Focus", "Selection", "Hover", "Active",
                "SyntaxKeyword", "SyntaxString", "SyntaxComment", "SyntaxNumber", "SyntaxFunction", "SyntaxVariable"
            };

            foreach (var prop in colorProperties)
            {
                panel.Children.Add(CreateColorPickerSection(prop));
            }

            return panel;
        }

        private UIElement CreateColorPickerSection(string propertyName)
        {
            var panel = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };

            var label = new TextBlock
            {
                Text = FormatPropertyName(propertyName) + ":",
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.LightGray
            };
            DockPanel.SetDock(label, Dock.Left);

            var resetButton = new Button
            {
                Content = "Reset",
                Width = 60,
                Height = 25,
                Margin = new Thickness(5, 0, 0, 0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10
            };
            resetButton.Click += (s, e) => ResetColorOverride(propertyName);
            DockPanel.SetDock(resetButton, Dock.Right);

            var colorButton = new Button
            {
                Width = 100,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray
            };

            UpdateColorButton(colorButton, propertyName);
            colorButton.Click += (s, e) => ShowColorPickerDialog(propertyName, colorButton);

            colorPickers[propertyName] = colorButton;

            panel.Children.Add(label);
            panel.Children.Add(colorButton);
            panel.Children.Add(resetButton);

            return panel;
        }

        private UIElement CreateGlowSettingsSection()
        {
            var panel = new StackPanel();

            // Glow Mode
            panel.Children.Add(CreateComboBoxSection(
                "Glow Mode",
                new[] { "Always", "OnFocus", "OnHover", "Never" },
                (int)themeManager.CurrentTheme.Glow.Mode,
                index =>
                {
                    themeManager.CurrentTheme.Glow.Mode = (GlowMode)index;
                    NotifyThemeChanged();
                },
                "GlowMode"
            ));

            // Glow Color
            panel.Children.Add(CreateColorPickerSectionForGlow("Glow Color", "GlowColor"));

            // Focus Glow Color
            panel.Children.Add(CreateColorPickerSectionForGlow("Focus Glow Color", "FocusGlowColor"));

            // Hover Glow Color
            panel.Children.Add(CreateColorPickerSectionForGlow("Hover Glow Color", "HoverGlowColor"));

            // Glow Radius
            panel.Children.Add(CreateSliderSection(
                "Glow Radius",
                0, 30,
                themeManager.CurrentTheme.Glow.GlowRadius,
                value =>
                {
                    themeManager.CurrentTheme.Glow.GlowRadius = value;
                    NotifyThemeChanged();
                },
                "GlowRadius"
            ));

            // Glow Opacity
            panel.Children.Add(CreateSliderSection(
                "Glow Opacity",
                0, 1,
                themeManager.CurrentTheme.Glow.GlowOpacity,
                value =>
                {
                    themeManager.CurrentTheme.Glow.GlowOpacity = value;
                    NotifyThemeChanged();
                },
                "GlowOpacity"
            ));

            return panel;
        }

        private UIElement CreateColorPickerSectionForGlow(string label, string propertyName)
        {
            var panel = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };

            var labelText = new TextBlock
            {
                Text = label + ":",
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.LightGray
            };
            DockPanel.SetDock(labelText, Dock.Left);

            var colorButton = new Button
            {
                Width = 100,
                Height = 25,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray
            };

            Color currentColor = propertyName switch
            {
                "GlowColor" => themeManager.CurrentTheme.Glow.GlowColor,
                "FocusGlowColor" => themeManager.CurrentTheme.Glow.FocusGlowColor,
                "HoverGlowColor" => themeManager.CurrentTheme.Glow.HoverGlowColor,
                _ => Colors.White
            };

            colorButton.Background = new SolidColorBrush(currentColor);
            colorButton.Click += (s, e) =>
            {
                var dialog = new System.Windows.Forms.ColorDialog
                {
                    Color = System.Drawing.Color.FromArgb(currentColor.R, currentColor.G, currentColor.B)
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);

                    switch (propertyName)
                    {
                        case "GlowColor":
                            themeManager.CurrentTheme.Glow.GlowColor = newColor;
                            break;
                        case "FocusGlowColor":
                            themeManager.CurrentTheme.Glow.FocusGlowColor = newColor;
                            break;
                        case "HoverGlowColor":
                            themeManager.CurrentTheme.Glow.HoverGlowColor = newColor;
                            break;
                    }

                    colorButton.Background = new SolidColorBrush(newColor);
                    NotifyThemeChanged();
                }
            };

            colorPickers[propertyName] = colorButton;

            panel.Children.Add(labelText);
            panel.Children.Add(colorButton);

            return panel;
        }

        private UIElement CreateCRTEffectsSection()
        {
            var panel = new StackPanel();

            // Enable Scanlines
            panel.Children.Add(CreateCheckBoxSection(
                "Enable Scanlines",
                themeManager.CurrentTheme.CRTEffects.EnableScanlines,
                value =>
                {
                    themeManager.CurrentTheme.CRTEffects.EnableScanlines = value;
                    NotifyThemeChanged();
                },
                "EnableScanlines"
            ));

            // Scanline Opacity
            panel.Children.Add(CreateSliderSection(
                "Scanline Opacity",
                0, 1,
                themeManager.CurrentTheme.CRTEffects.ScanlineOpacity,
                value =>
                {
                    themeManager.CurrentTheme.CRTEffects.ScanlineOpacity = value;
                    NotifyThemeChanged();
                },
                "ScanlineOpacity"
            ));

            // Scanline Spacing
            panel.Children.Add(CreateSliderSection(
                "Scanline Spacing",
                1, 10,
                themeManager.CurrentTheme.CRTEffects.ScanlineSpacing,
                value =>
                {
                    themeManager.CurrentTheme.CRTEffects.ScanlineSpacing = (int)value;
                    NotifyThemeChanged();
                },
                "ScanlineSpacing"
            ));

            // Scanline Color - special handling
            var scanlineColorPanel = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var scanlineLabel = new TextBlock
            {
                Text = "Scanline Color:",
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.LightGray
            };
            DockPanel.SetDock(scanlineLabel, Dock.Left);

            var scanlineColorButton = new Button
            {
                Width = 100,
                Height = 25,
                Background = new SolidColorBrush(themeManager.CurrentTheme.CRTEffects.ScanlineColor),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray
            };
            scanlineColorButton.Click += (s, e) =>
            {
                var dialog = new System.Windows.Forms.ColorDialog
                {
                    Color = System.Drawing.Color.FromArgb(
                        themeManager.CurrentTheme.CRTEffects.ScanlineColor.R,
                        themeManager.CurrentTheme.CRTEffects.ScanlineColor.G,
                        themeManager.CurrentTheme.CRTEffects.ScanlineColor.B
                    )
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    themeManager.CurrentTheme.CRTEffects.ScanlineColor = newColor;
                    scanlineColorButton.Background = new SolidColorBrush(newColor);
                    NotifyThemeChanged();
                }
            };

            colorPickers["ScanlineColor"] = scanlineColorButton;
            scanlineColorPanel.Children.Add(scanlineLabel);
            scanlineColorPanel.Children.Add(scanlineColorButton);
            panel.Children.Add(scanlineColorPanel);

            // Enable Bloom
            panel.Children.Add(CreateCheckBoxSection(
                "Enable Bloom",
                themeManager.CurrentTheme.CRTEffects.EnableBloom,
                value =>
                {
                    themeManager.CurrentTheme.CRTEffects.EnableBloom = value;
                    NotifyThemeChanged();
                },
                "EnableBloom"
            ));

            // Bloom Intensity
            panel.Children.Add(CreateSliderSection(
                "Bloom Intensity",
                0, 1,
                themeManager.CurrentTheme.CRTEffects.BloomIntensity,
                value =>
                {
                    themeManager.CurrentTheme.CRTEffects.BloomIntensity = value;
                    NotifyThemeChanged();
                },
                "BloomIntensity"
            ));

            return panel;
        }

        private UIElement CreateOpacitySection()
        {
            var panel = new StackPanel();

            // Window Opacity
            panel.Children.Add(CreateSliderSection(
                "Window Opacity",
                0.5, 1.0,
                themeManager.CurrentTheme.Opacity.WindowOpacity,
                value =>
                {
                    themeManager.CurrentTheme.Opacity.WindowOpacity = value;
                    NotifyThemeChanged();
                },
                "WindowOpacity"
            ));

            // Background Opacity
            panel.Children.Add(CreateSliderSection(
                "Background Opacity",
                0.5, 1.0,
                themeManager.CurrentTheme.Opacity.BackgroundOpacity,
                value =>
                {
                    themeManager.CurrentTheme.Opacity.BackgroundOpacity = value;
                    NotifyThemeChanged();
                },
                "BackgroundOpacity"
            ));

            // Inactive Widget Opacity
            panel.Children.Add(CreateSliderSection(
                "Inactive Widget Opacity",
                0, 1,
                themeManager.CurrentTheme.Opacity.InactiveWidgetOpacity,
                value =>
                {
                    themeManager.CurrentTheme.Opacity.InactiveWidgetOpacity = value;
                    NotifyThemeChanged();
                },
                "InactiveWidgetOpacity"
            ));

            return panel;
        }

        private UIElement CreateTypographySection()
        {
            var panel = new StackPanel();

            // Font Family
            panel.Children.Add(CreateComboBoxSection(
                "Font Family",
                new[] { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console", "DejaVu Sans Mono", "Monaco" },
                Array.IndexOf(new[] { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console", "DejaVu Sans Mono", "Monaco" },
                              themeManager.CurrentTheme.Typography.FontFamily),
                index =>
                {
                    var fonts = new[] { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console", "DejaVu Sans Mono", "Monaco" };
                    themeManager.CurrentTheme.Typography.FontFamily = fonts[index];
                    NotifyThemeChanged();
                },
                "FontFamily"
            ));

            // Font Size
            panel.Children.Add(CreateSliderSection(
                "Font Size",
                8, 24,
                themeManager.CurrentTheme.Typography.FontSize,
                value =>
                {
                    themeManager.CurrentTheme.Typography.FontSize = value;
                    NotifyThemeChanged();
                },
                "FontSize"
            ));

            // Font Weight
            panel.Children.Add(CreateComboBoxSection(
                "Font Weight",
                new[] { "Normal", "Bold", "Light" },
                Array.IndexOf(new[] { "Normal", "Bold", "Light" }, themeManager.CurrentTheme.Typography.FontWeight),
                index =>
                {
                    var weights = new[] { "Normal", "Bold", "Light" };
                    themeManager.CurrentTheme.Typography.FontWeight = weights[index];
                    NotifyThemeChanged();
                },
                "FontWeight"
            ));

            return panel;
        }

        #endregion

        #region Helper Methods

        private UIElement CreateSliderSection(string label, double min, double max, double value, Action<double> onChange, string key)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 3, 0, 3) };

            var headerPanel = new DockPanel();

            var labelText = new TextBlock
            {
                Text = label + ":",
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.LightGray
            };
            DockPanel.SetDock(labelText, Dock.Left);

            var valueText = new TextBlock
            {
                Text = value.ToString("F2"),
                Width = 60,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.White
            };
            DockPanel.SetDock(valueText, Dock.Right);

            headerPanel.Children.Add(labelText);
            headerPanel.Children.Add(valueText);

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                TickFrequency = (max - min) / 10,
                IsSnapToTickEnabled = false,
                Margin = new Thickness(0, 2, 0, 0)
            };

            slider.ValueChanged += (s, e) =>
            {
                if (!isUpdatingUI)
                {
                    valueText.Text = e.NewValue.ToString("F2");
                    onChange(e.NewValue);
                }
            };

            sliders[key] = slider;

            panel.Children.Add(headerPanel);
            panel.Children.Add(slider);

            return panel;
        }

        private UIElement CreateComboBoxSection(string label, string[] items, int selectedIndex, Action<int> onChange, string key)
        {
            var panel = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };

            var labelText = new TextBlock
            {
                Text = label + ":",
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.LightGray
            };
            DockPanel.SetDock(labelText, Dock.Left);

            var comboBox = new ComboBox
            {
                Width = 150,
                FontFamily = new FontFamily("Consolas")
            };

            foreach (var item in items)
            {
                comboBox.Items.Add(item);
            }

            if (selectedIndex >= 0 && selectedIndex < items.Length)
            {
                comboBox.SelectedIndex = selectedIndex;
            }

            comboBox.SelectionChanged += (s, e) =>
            {
                if (!isUpdatingUI && comboBox.SelectedIndex >= 0)
                {
                    onChange(comboBox.SelectedIndex);
                }
            };

            comboBoxes[key] = comboBox;

            panel.Children.Add(labelText);
            panel.Children.Add(comboBox);

            return panel;
        }

        private UIElement CreateCheckBoxSection(string label, bool isChecked, Action<bool> onChange, string key)
        {
            var checkBox = new CheckBox
            {
                Content = label,
                IsChecked = isChecked,
                Margin = new Thickness(0, 3, 0, 3),
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.LightGray
            };

            checkBox.Checked += (s, e) =>
            {
                if (!isUpdatingUI)
                    onChange(true);
            };

            checkBox.Unchecked += (s, e) =>
            {
                if (!isUpdatingUI)
                    onChange(false);
            };

            checkBoxes[key] = checkBox;

            return checkBox;
        }

        private void UpdateColorButton(Button button, string propertyName)
        {
            var color = themeManager.GetEffectiveColor(propertyName);
            button.Background = new SolidColorBrush(color);

            // Use contrasting text
            var brightness = (color.R + color.G + color.B) / 3.0;
            button.Foreground = brightness > 128 ? Brushes.Black : Brushes.White;
        }

        private void ShowColorPickerDialog(string propertyName, Button colorButton)
        {
            try
            {
                var currentColor = themeManager.GetEffectiveColor(propertyName);

                var dialog = new System.Windows.Forms.ColorDialog
                {
                    Color = System.Drawing.Color.FromArgb(currentColor.R, currentColor.G, currentColor.B),
                    FullOpen = true,
                    AllowFullOpen = true
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    themeManager.SetColorOverride(propertyName, newColor);
                    UpdateColorButton(colorButton, propertyName);

                    logger.Debug("ThemeEditor", $"Set color override: {propertyName} = {newColor}");
                }
            }
            catch (Exception ex)
            {
                logger.Error("ThemeEditor", $"Failed to show color picker: {ex.Message}", ex);
            }
        }

        private void ResetColorOverride(string propertyName)
        {
            try
            {
                themeManager.ResetOverride(propertyName);

                if (colorPickers.TryGetValue(propertyName, out var button))
                {
                    UpdateColorButton(button, propertyName);
                }

                logger.Debug("ThemeEditor", $"Reset color override: {propertyName}");
            }
            catch (Exception ex)
            {
                logger.Error("ThemeEditor", $"Failed to reset color: {ex.Message}", ex);
            }
        }

        private string FormatPropertyName(string propertyName)
        {
            // Convert "SyntaxKeyword" to "Syntax Keyword"
            var result = System.Text.RegularExpressions.Regex.Replace(propertyName, "(\\B[A-Z])", " $1");
            return result;
        }

        private void NotifyThemeChanged()
        {
            // Trigger the theme changed event to update all widgets
            // This is a bit of a hack - we're manually invoking the event
            var themeChangedEvent = typeof(ThemeManager).GetEvent("ThemeChanged");
            var currentTheme = themeManager.CurrentTheme;

            // Just apply the current theme again to trigger updates
            themeManager.ApplyTheme(currentTheme.Name);
        }

        #endregion

        #region Event Handlers

        private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingUI || themeSelector.SelectedItem == null)
                return;

            try
            {
                var themeName = themeSelector.SelectedItem.ToString();
                themeManager.ApplyTheme(themeName);

                logger.Info("ThemeEditor", $"Applied theme: {themeName}");

                // Update all UI controls to reflect new theme
                RefreshUIFromTheme();
            }
            catch (Exception ex)
            {
                logger.Error("ThemeEditor", $"Failed to apply theme: {ex.Message}", ex);
            }
        }

        private void OnResetAllOverrides(object sender, RoutedEventArgs e)
        {
            try
            {
                themeManager.ResetAllOverrides();

                // Update all color pickers
                foreach (var kvp in colorPickers)
                {
                    UpdateColorButton(kvp.Value, kvp.Key);
                }

                logger.Info("ThemeEditor", "Reset all color overrides");
            }
            catch (Exception ex)
            {
                logger.Error("ThemeEditor", $"Failed to reset overrides: {ex.Message}", ex);
            }
        }

        private void OnSaveThemeAs(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create a dialog to get the theme name
                var dialog = new Window
                {
                    Title = "Save Theme As",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize
                };

                var stackPanel = new StackPanel { Margin = new Thickness(20) };

                var label = new TextBlock
                {
                    Text = "Enter theme name:",
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var textBox = new TextBox
                {
                    Text = themeManager.CurrentTheme.Name + " Custom",
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var okButton = new Button
                {
                    Content = "Save",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    FontFamily = new FontFamily("Consolas")
                };

                var cancelButton = new Button
                {
                    Content = "Cancel",
                    Width = 80,
                    Height = 30,
                    FontFamily = new FontFamily("Consolas")
                };

                okButton.Click += (s, ev) =>
                {
                    var newThemeName = textBox.Text.Trim();
                    if (!string.IsNullOrEmpty(newThemeName))
                    {
                        var newTheme = CloneCurrentTheme(newThemeName);
                        themeManager.SaveTheme(newTheme);
                        themeManager.RegisterTheme(newTheme);

                        // Update theme selector
                        if (!themeSelector.Items.Contains(newThemeName))
                        {
                            themeSelector.Items.Add(newThemeName);
                        }

                        logger.Info("ThemeEditor", $"Saved theme as: {newThemeName}");
                        dialog.Close();
                    }
                };

                cancelButton.Click += (s, ev) => dialog.Close();

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                stackPanel.Children.Add(label);
                stackPanel.Children.Add(textBox);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                logger.Error("ThemeEditor", $"Failed to save theme: {ex.Message}", ex);
            }
        }

        private void OnThemeManagerChanged(object sender, ThemeChangedEventArgs e)
        {
            // Update UI when theme changes externally
            RefreshUIFromTheme();
        }

        #endregion

        #region Theme Management

        private Theme CloneCurrentTheme(string newName)
        {
            var current = themeManager.CurrentTheme;

            return new Theme
            {
                Name = newName,
                Description = current.Description,
                IsDark = current.IsDark,

                Primary = current.Primary,
                Secondary = current.Secondary,
                Success = current.Success,
                Warning = current.Warning,
                Error = current.Error,
                Info = current.Info,

                Background = current.Background,
                BackgroundSecondary = current.BackgroundSecondary,
                Surface = current.Surface,
                SurfaceHighlight = current.SurfaceHighlight,

                Foreground = current.Foreground,
                ForegroundSecondary = current.ForegroundSecondary,
                ForegroundDisabled = current.ForegroundDisabled,

                Border = current.Border,
                BorderActive = current.BorderActive,
                Focus = current.Focus,
                Selection = current.Selection,
                Hover = current.Hover,
                Active = current.Active,

                SyntaxKeyword = current.SyntaxKeyword,
                SyntaxString = current.SyntaxString,
                SyntaxComment = current.SyntaxComment,
                SyntaxNumber = current.SyntaxNumber,
                SyntaxFunction = current.SyntaxFunction,
                SyntaxVariable = current.SyntaxVariable,

                Glow = new GlowSettings
                {
                    Mode = current.Glow.Mode,
                    GlowColor = current.Glow.GlowColor,
                    GlowRadius = current.Glow.GlowRadius,
                    GlowOpacity = current.Glow.GlowOpacity,
                    FocusGlowColor = current.Glow.FocusGlowColor,
                    HoverGlowColor = current.Glow.HoverGlowColor
                },

                CRTEffects = new CRTEffectSettings
                {
                    EnableScanlines = current.CRTEffects.EnableScanlines,
                    ScanlineOpacity = current.CRTEffects.ScanlineOpacity,
                    ScanlineSpacing = current.CRTEffects.ScanlineSpacing,
                    ScanlineColor = current.CRTEffects.ScanlineColor,
                    EnableBloom = current.CRTEffects.EnableBloom,
                    BloomIntensity = current.CRTEffects.BloomIntensity
                },

                Opacity = new OpacitySettings
                {
                    WindowOpacity = current.Opacity.WindowOpacity,
                    BackgroundOpacity = current.Opacity.BackgroundOpacity,
                    InactiveWidgetOpacity = current.Opacity.InactiveWidgetOpacity
                },

                Typography = new TypographySettings
                {
                    FontFamily = current.Typography.FontFamily,
                    FontSize = current.Typography.FontSize,
                    FontWeight = current.Typography.FontWeight,
                    PerWidgetFonts = new Dictionary<string, FontSettings>(current.Typography.PerWidgetFonts)
                },

                ColorOverrides = new Dictionary<string, Color>(current.ColorOverrides ?? new Dictionary<string, Color>())
            };
        }

        private void RefreshUIFromTheme()
        {
            isUpdatingUI = true;

            try
            {
                var theme = themeManager.CurrentTheme;

                // Update theme selector
                if (themeSelector != null)
                {
                    themeSelector.SelectedItem = theme.Name;
                }

                // Update all color pickers
                foreach (var kvp in colorPickers)
                {
                    if (kvp.Key == "GlowColor")
                        kvp.Value.Background = new SolidColorBrush(theme.Glow.GlowColor);
                    else if (kvp.Key == "FocusGlowColor")
                        kvp.Value.Background = new SolidColorBrush(theme.Glow.FocusGlowColor);
                    else if (kvp.Key == "HoverGlowColor")
                        kvp.Value.Background = new SolidColorBrush(theme.Glow.HoverGlowColor);
                    else if (kvp.Key == "ScanlineColor")
                        kvp.Value.Background = new SolidColorBrush(theme.CRTEffects.ScanlineColor);
                    else
                        UpdateColorButton(kvp.Value, kvp.Key);
                }

                // Update sliders
                if (sliders.TryGetValue("GlowRadius", out var glowRadiusSlider))
                    glowRadiusSlider.Value = theme.Glow.GlowRadius;
                if (sliders.TryGetValue("GlowOpacity", out var glowOpacitySlider))
                    glowOpacitySlider.Value = theme.Glow.GlowOpacity;
                if (sliders.TryGetValue("ScanlineOpacity", out var scanlineOpacitySlider))
                    scanlineOpacitySlider.Value = theme.CRTEffects.ScanlineOpacity;
                if (sliders.TryGetValue("ScanlineSpacing", out var scanlineSpacingSlider))
                    scanlineSpacingSlider.Value = theme.CRTEffects.ScanlineSpacing;
                if (sliders.TryGetValue("BloomIntensity", out var bloomIntensitySlider))
                    bloomIntensitySlider.Value = theme.CRTEffects.BloomIntensity;
                if (sliders.TryGetValue("WindowOpacity", out var windowOpacitySlider))
                    windowOpacitySlider.Value = theme.Opacity.WindowOpacity;
                if (sliders.TryGetValue("BackgroundOpacity", out var bgOpacitySlider))
                    bgOpacitySlider.Value = theme.Opacity.BackgroundOpacity;
                if (sliders.TryGetValue("InactiveWidgetOpacity", out var inactiveOpacitySlider))
                    inactiveOpacitySlider.Value = theme.Opacity.InactiveWidgetOpacity;
                if (sliders.TryGetValue("FontSize", out var fontSizeSlider))
                    fontSizeSlider.Value = theme.Typography.FontSize;

                // Update checkboxes
                if (checkBoxes.TryGetValue("EnableScanlines", out var scanlinesCheckBox))
                    scanlinesCheckBox.IsChecked = theme.CRTEffects.EnableScanlines;
                if (checkBoxes.TryGetValue("EnableBloom", out var bloomCheckBox))
                    bloomCheckBox.IsChecked = theme.CRTEffects.EnableBloom;

                // Update comboboxes
                if (comboBoxes.TryGetValue("GlowMode", out var glowModeCombo))
                    glowModeCombo.SelectedIndex = (int)theme.Glow.Mode;
                if (comboBoxes.TryGetValue("FontWeight", out var fontWeightCombo))
                {
                    var index = Array.IndexOf(new[] { "Normal", "Bold", "Light" }, theme.Typography.FontWeight);
                    fontWeightCombo.SelectedIndex = index >= 0 ? index : 0;
                }
            }
            finally
            {
                isUpdatingUI = false;
            }
        }

        #endregion

        #region IThemeable Implementation

        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            // Update widget background
            if (Content is Grid mainGrid)
            {
                mainGrid.Background = new SolidColorBrush(theme.Background);
            }

            RefreshUIFromTheme();
        }

        #endregion

        #region Lifecycle

        protected override void OnDispose()
        {
            try
            {
                themeManager.ThemeChanged -= OnThemeManagerChanged;

                logger.Debug("ThemeEditor", "Theme Editor disposed");
            }
            catch (Exception ex)
            {
                logger.Error("ThemeEditor", $"Error during dispose: {ex.Message}", ex);
            }

            base.OnDispose();
        }

        #endregion
    }
}
