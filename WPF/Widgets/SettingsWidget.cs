using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Settings widget for configuring SuperTUI
    /// Provides UI for all configuration options grouped by category
    /// </summary>
    public class SettingsWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;
        private Border containerBorder;
        private TextBlock titleText;
        private ComboBox categoryCombo;
        private ScrollViewer settingsScroll;
        private StackPanel settingsPanel;
        private TextBlock footerText;
        private Button saveButton;
        private Button resetButton;

        private string currentCategory = "Application";
        private Dictionary<string, object> pendingChanges = new Dictionary<string, object>();

        public SettingsWidget(ILogger logger, IThemeManager themeManager, IConfigurationManager config)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            WidgetType = "Settings";
            WidgetName = "Settings";
        }

        // Backward compatibility constructor
        public SettingsWidget() : this(Logger.Instance, ThemeManager.Instance, ConfigurationManager.Instance)
        {
        }

        public override void Initialize()
        {
            BuildUI();
            UpdateCategoryList();
            DisplayCategory(currentCategory);
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            var mainPanel = new StackPanel();

            // Title
            titleText = new TextBlock
            {
                Text = "⚙  SETTINGS",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(15, 15, 15, 10)
            };

            // Category selector
            var categoryPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(15, 5, 15, 10)
            };

            var categoryLabel = new TextBlock
            {
                Text = "Category:",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            categoryCombo = new ComboBox
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                MinWidth = 200
            };

            categoryCombo.SelectionChanged += (s, e) =>
            {
                if (categoryCombo.SelectedItem != null)
                {
                    currentCategory = categoryCombo.SelectedItem.ToString();
                    DisplayCategory(currentCategory);
                }
            };

            categoryPanel.Children.Add(categoryLabel);
            categoryPanel.Children.Add(categoryCombo);

            // Settings display
            settingsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(15, 0, 15, 10),
                MinHeight = 300
            };

            settingsPanel = new StackPanel();
            settingsScroll.Content = settingsPanel;

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(15, 0, 15, 10)
            };

            saveButton = new Button
            {
                Content = "Save Changes",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(15, 5, 15, 5),
                Background = new SolidColorBrush(theme.Success),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };

            saveButton.Click += SaveButton_Click;

            resetButton = new Button
            {
                Content = "Reset to Defaults",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(15, 5, 15, 5),
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            resetButton.Click += ResetButton_Click;

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(resetButton);

            // Footer
            footerText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(15, 0, 15, 15),
                TextAlignment = TextAlignment.Center
            };

            mainPanel.Children.Add(titleText);
            mainPanel.Children.Add(categoryPanel);
            mainPanel.Children.Add(settingsScroll);
            mainPanel.Children.Add(buttonPanel);
            mainPanel.Children.Add(footerText);

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Child = mainPanel
            };

            this.Content = containerBorder;
        }

        private void UpdateCategoryList()
        {
            try
            {
                var categories = config.GetCategories();

                categoryCombo.Items.Clear();
                foreach (var category in categories.OrderBy(c => c))
                {
                    categoryCombo.Items.Add(category);
                }

                if (categoryCombo.Items.Count > 0)
                {
                    categoryCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                logger.Error("Settings", $"Failed to load categories: {ex.Message}", ex);
                footerText.Text = "Error loading categories";
            }
        }

        private void DisplayCategory(string category)
        {
            settingsPanel.Children.Clear();

            try
            {
                var settings = config.GetCategory(category);

                if (settings.Count == 0)
                {
                    var emptyText = new TextBlock
                    {
                        Text = "No settings in this category",
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                        FontSize = 12,
                        Foreground = new SolidColorBrush(themeManager.CurrentTheme.ForegroundDisabled),
                        Margin = new Thickness(10)
                    };
                    settingsPanel.Children.Add(emptyText);
                    return;
                }

                foreach (var kvp in settings.OrderBy(s => s.Key))
                {
                    var setting = kvp.Value;
                    var settingControl = CreateSettingControl(setting);
                    settingsPanel.Children.Add(settingControl);
                }

                footerText.Text = $"Showing {settings.Count} settings in {category}";
            }
            catch (Exception ex)
            {
                logger.Error("Settings", $"Failed to display category: {ex.Message}", ex);
                footerText.Text = $"Error displaying {category} settings";
            }
        }

        private UIElement CreateSettingControl(ConfigValue setting)
        {
            var theme = themeManager.CurrentTheme;

            var panel = new StackPanel
            {
                Margin = new Thickness(10, 5, 10, 5)
            };

            // Setting name and description
            var nameText = new TextBlock
            {
                Text = setting.Key.Split('.').Last(),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground)
            };

            var descText = new TextBlock
            {
                Text = setting.Description,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 2, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };

            panel.Children.Add(nameText);
            panel.Children.Add(descText);

            // Add validation info if validator is present
            if (setting.Validator != null)
            {
                var validationText = new TextBlock
                {
                    Text = GetValidationHint(setting),
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(theme.Info),
                    Margin = new Thickness(0, 0, 0, 5),
                    FontStyle = FontStyles.Italic
                };
                panel.Children.Add(validationText);
            }

            // Input control based on type
            UIElement inputControl = null;

            if (setting.ValueType == typeof(bool))
            {
                var checkBox = new CheckBox
                {
                    IsChecked = (bool)setting.Value,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    Foreground = new SolidColorBrush(theme.Foreground)
                };

                checkBox.Checked += (s, e) => pendingChanges[setting.Key] = true;
                checkBox.Unchecked += (s, e) => pendingChanges[setting.Key] = false;

                inputControl = checkBox;
            }
            else if (setting.ValueType == typeof(int))
            {
                var textBox = new TextBox
                {
                    Text = setting.Value.ToString(),
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 11,
                    Background = new SolidColorBrush(theme.Surface),
                    Foreground = new SolidColorBrush(theme.Foreground),
                    BorderBrush = new SolidColorBrush(theme.Border),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(5),
                    MaxWidth = 200,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                textBox.TextChanged += (s, e) =>
                {
                    if (int.TryParse(textBox.Text, out int value))
                    {
                        // Validate using ConfigValue's validator if present
                        bool isValid = true;
                        if (setting.Validator != null)
                        {
                            isValid = setting.Validator(value);
                        }

                        if (isValid)
                        {
                            pendingChanges[setting.Key] = value;
                            textBox.BorderBrush = new SolidColorBrush(theme.Border);
                            textBox.ToolTip = null;
                        }
                        else
                        {
                            // Remove from pending if validation fails
                            pendingChanges.Remove(setting.Key);
                            textBox.BorderBrush = new SolidColorBrush(theme.Error);
                            textBox.ToolTip = $"Value {value} failed validation";
                        }
                    }
                    else
                    {
                        pendingChanges.Remove(setting.Key);
                        textBox.BorderBrush = new SolidColorBrush(theme.Error);
                        textBox.ToolTip = "Must be a valid integer";
                    }
                };

                inputControl = textBox;
            }
            else if (setting.ValueType == typeof(string))
            {
                var textBox = new TextBox
                {
                    Text = setting.Value?.ToString() ?? "",
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 11,
                    Background = new SolidColorBrush(theme.Surface),
                    Foreground = new SolidColorBrush(theme.Foreground),
                    BorderBrush = new SolidColorBrush(theme.Border),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(5),
                    MinWidth = 300,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                textBox.TextChanged += (s, e) => pendingChanges[setting.Key] = textBox.Text;

                inputControl = textBox;
            }
            else
            {
                // Fallback for unsupported types
                var valueText = new TextBlock
                {
                    Text = $"(Type {setting.ValueType.Name} not supported in UI)",
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(theme.Warning),
                    FontStyle = FontStyles.Italic
                };

                inputControl = valueText;
            }

            panel.Children.Add(inputControl);

            // Separator
            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(theme.Border),
                Margin = new Thickness(0, 10, 0, 0)
            };
            panel.Children.Add(separator);

            return panel;
        }

        private string GetValidationHint(ConfigValue setting)
        {
            // Try to infer validation rules by testing boundary values
            if (setting.ValueType == typeof(int))
            {
                // Test common validation patterns
                if (setting.Validator != null)
                {
                    // Try to find min/max bounds
                    int min = int.MinValue;
                    int max = int.MaxValue;

                    // Test if there's a minimum
                    for (int testMin = 0; testMin <= 100; testMin++)
                    {
                        if (setting.Validator(testMin))
                        {
                            min = testMin;
                            break;
                        }
                    }

                    // Test if there's a maximum
                    for (int testMax = 200; testMax >= 1; testMax--)
                    {
                        if (setting.Validator(testMax))
                        {
                            max = testMax;
                            break;
                        }
                    }

                    if (min != int.MinValue && max != int.MaxValue)
                    {
                        return $"Valid range: {min} - {max}";
                    }
                    else if (min != int.MinValue)
                    {
                        return $"Minimum: {min}";
                    }
                    else if (max != int.MaxValue)
                    {
                        return $"Maximum: {max}";
                    }
                }
            }

            return "Validated";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var kvp in pendingChanges)
                {
                    config.Set(kvp.Key, kvp.Value, saveImmediately: false);
                    logger.Info("Settings", $"Updated {kvp.Key} = {kvp.Value}");
                }

                config.Save();

                pendingChanges.Clear();
                footerText.Text = "Settings saved successfully";
                footerText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.Success);

                // Refresh display
                DisplayCategory(currentCategory);
            }
            catch (Exception ex)
            {
                logger.Error("Settings", $"Failed to save settings: {ex.Message}", ex);
                footerText.Text = $"Error saving settings: {ex.Message}";
                footerText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.Error);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to reset all settings to default values?",
                    "Reset Settings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    config.ResetToDefaults();
                    config.Save();

                    pendingChanges.Clear();
                    DisplayCategory(currentCategory);

                    footerText.Text = "Settings reset to defaults";
                    footerText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.Warning);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Settings", $"Failed to reset settings: {ex.Message}", ex);
                footerText.Text = $"Error resetting settings: {ex.Message}";
                footerText.Foreground = new SolidColorBrush(themeManager.CurrentTheme.Error);
            }
        }

        public override void OnWidgetKeyDown(KeyEventArgs e)
        {
            // Ctrl+S to save
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SaveButton_Click(null, null);
                e.Handled = true;
            }
            // F5 to refresh
            else if (e.Key == Key.F5)
            {
                UpdateCategoryList();
                DisplayCategory(currentCategory);
                e.Handled = true;
            }
        }

        protected override void OnDispose()
        {
            pendingChanges.Clear();
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
                containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.Primary);
            }

            // Rebuild display with new theme
            if (categoryCombo != null && !string.IsNullOrEmpty(currentCategory))
            {
                DisplayCategory(currentCategory);
            }
        }
    }
}
