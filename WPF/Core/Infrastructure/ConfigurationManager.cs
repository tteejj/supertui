using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SuperTUI.Infrastructure
{
    // ============================================================================
    // CONFIGURATION SYSTEM
    // ============================================================================

    /// <summary>
    /// Configuration value with type safety and validation
    /// </summary>
    public class ConfigValue
    {
        public string Key { get; set; }
        public object Value { get; set; }
        public Type ValueType { get; set; }
        public object DefaultValue { get; set; }
        public string Description { get; set; }
        public Func<object, bool> Validator { get; set; }
        public bool IsReadOnly { get; set; }
        public string Category { get; set; }
    }

    /// <summary>
    /// Hierarchical configuration system with validation and persistence
    /// </summary>
    public class ConfigurationManager : IConfigurationManager
    {
        private static ConfigurationManager instance;
        public static ConfigurationManager Instance => instance ??= new ConfigurationManager();

        private readonly Dictionary<string, ConfigValue> config = new Dictionary<string, ConfigValue>();
        private readonly Dictionary<string, Dictionary<string, ConfigValue>> categories = new Dictionary<string, Dictionary<string, ConfigValue>>();
        private string configFilePath;

        public event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        public void Initialize(string configPath)
        {
            configFilePath = configPath;
            RegisterDefaultSettings();

            if (File.Exists(configPath))
            {
                LoadFromFile(configPath);
            }
            else
            {
                SaveToFile(configPath);
            }
        }

        private void RegisterDefaultSettings()
        {
            // Application settings
            Register("App.Title", "SuperTUI", "Application title", "Application");
            Register("App.LogLevel", LogLevel.Info, "Minimum log level", "Application");
            Register("App.LogDirectory", Path.Combine(SuperTUI.Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(), "Logs"), "Log file directory", "Application");
            Register("App.AutoSave", true, "Auto-save state on exit", "Application");
            Register("App.AutoSaveInterval", 300, "Auto-save interval in seconds (0 = disabled)", "Application", value => (int)value >= 0);

            // UI settings
            Register("UI.Theme", "Dark", "UI theme name", "UI");
            Register("UI.FontFamily", "Cascadia Mono, Consolas", "Font family", "UI");
            Register("UI.FontSize", 12, "Font size in points", "UI", value => (int)value >= 8 && (int)value <= 24);
            Register("UI.AnimationDuration", 200, "Animation duration in milliseconds", "UI", value => (int)value >= 0 && (int)value <= 1000);
            Register("UI.ShowLineNumbers", false, "Show line numbers in editors", "UI");
            Register("UI.WordWrap", true, "Enable word wrap", "UI");

            // Performance settings
            Register("Performance.MaxFPS", 60, "Maximum frames per second", "Performance", value => (int)value >= 1 && (int)value <= 144);
            Register("Performance.EnableVSync", true, "Enable vertical sync", "Performance");
            Register("Performance.LazyLoadThreshold", 100, "Item count threshold for lazy loading", "Performance", value => (int)value >= 10);
            Register("Performance.VirtualizationEnabled", true, "Enable UI virtualization for large lists", "Performance");

            // Security settings
            Register("Security.AllowScriptExecution", false, "Allow PowerShell script execution from UI", "Security");
            Register("Security.ValidateFileAccess", true, "Validate file access paths", "Security");
            Register("Security.MaxFileSize", 10, "Maximum file size in MB for operations", "Security", value => (int)value > 0);
            Register("Security.AllowedExtensions", new List<string> { ".txt", ".md", ".json", ".csv", ".log" }, "Allowed file extensions for operations", "Security");

            // Backup settings
            Register("Backup.Enabled", true, "Enable automatic backups", "Backup");
            Register("Backup.Directory", Path.Combine(SuperTUI.Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(), "Backups"), "Backup directory", "Backup");
            Register("Backup.Interval", 3600, "Backup interval in seconds", "Backup", value => (int)value >= 60);
            Register("Backup.MaxBackups", 10, "Maximum number of backups to keep", "Backup", value => (int)value >= 1);
            Register("Backup.CompressBackups", true, "Compress backup files", "Backup");
        }

        public void Register<T>(string key, T defaultValue, string description = "", string category = "General", Func<object, bool> validator = null)
        {
            var configValue = new ConfigValue
            {
                Key = key,
                Value = defaultValue,
                DefaultValue = defaultValue,
                ValueType = typeof(T),
                Description = description,
                Category = category,
                Validator = validator,
                IsReadOnly = false
            };

            config[key] = configValue;

            if (!categories.ContainsKey(category))
            {
                categories[category] = new Dictionary<string, ConfigValue>();
            }
            categories[category][key] = configValue;

            Logger.Instance.Debug("Config", $"Registered config key: {key} = {defaultValue}");
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (config.TryGetValue(key, out var configValue))
            {
                try
                {
                    // Direct type match - fast path
                    if (configValue.Value is T typedValue)
                        return typedValue;

                    // Handle JsonElement from loaded config files
                    if (configValue.Value is JsonElement jsonElement)
                    {
                        return DeserializeJsonElement<T>(jsonElement, key, defaultValue);
                    }

                    var targetType = typeof(T);

                    // Null handling - return default for null values
                    if (configValue.Value == null)
                    {
                        Logger.Instance.Debug("Config", $"Config value for key {key} is null, returning default");
                        return defaultValue;
                    }

                    // Collections and complex types - use JSON serialization
                    if (targetType.IsGenericType || targetType.IsArray || targetType.IsClass && targetType != typeof(string))
                    {
                        // For complex types, try direct cast first
                        if (configValue.Value.GetType() == targetType || targetType.IsAssignableFrom(configValue.Value.GetType()))
                        {
                            return (T)configValue.Value;
                        }

                        // Try JSON serialization round-trip as fallback
                        try
                        {
                            var serializerOptions = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true,
                                WriteIndented = true
                            };

                            string json = JsonSerializer.Serialize(configValue.Value, serializerOptions);
                            T result = JsonSerializer.Deserialize<T>(json, serializerOptions);

                            if (result == null)
                            {
                                Logger.Instance.Warning("Config", $"JSON deserialization returned null for key {key}, using default");
                                return defaultValue;
                            }

                            Logger.Instance.Debug("Config", $"Successfully converted {key} via JSON round-trip");
                            return result;
                        }
                        catch (JsonException ex)
                        {
                            Logger.Instance.Error("Config", $"JSON serialization failed for key {key}: {ex.Message}", ex);
                            return defaultValue;
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Error("Config", $"Cannot convert complex type for key {key}: {ex.Message}", ex);
                            return defaultValue;
                        }
                    }

                    // Enums - special handling
                    if (targetType.IsEnum)
                    {
                        if (configValue.Value is string enumString)
                        {
                            return (T)Enum.Parse(targetType, enumString, ignoreCase: true);
                        }
                        else if (configValue.Value is int enumInt)
                        {
                            return (T)Enum.ToObject(targetType, enumInt);
                        }
                    }

                    // Primitive types and simple value types - use Convert.ChangeType
                    if (targetType.IsPrimitive || targetType == typeof(decimal) || targetType == typeof(string) || targetType == typeof(DateTime) || targetType == typeof(Guid))
                    {
                        return (T)Convert.ChangeType(configValue.Value, typeof(T));
                    }

                    // Fallback - try direct cast
                    return (T)configValue.Value;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("Config", $"Failed to convert config value {key} (type: {configValue.ValueType?.Name ?? "unknown"}): {ex.Message}", ex);
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Deserialize a JsonElement to the target type
        /// Handles the case where config values are loaded from JSON files
        /// </summary>
        private T DeserializeJsonElement<T>(JsonElement jsonElement, string key, T defaultValue)
        {
            try
            {
                var targetType = typeof(T);

                // Handle primitive types and strings
                if (targetType == typeof(string))
                {
                    return (T)(object)jsonElement.GetString();
                }
                else if (targetType == typeof(int))
                {
                    return (T)(object)jsonElement.GetInt32();
                }
                else if (targetType == typeof(long))
                {
                    return (T)(object)jsonElement.GetInt64();
                }
                else if (targetType == typeof(bool))
                {
                    return (T)(object)jsonElement.GetBoolean();
                }
                else if (targetType == typeof(double))
                {
                    return (T)(object)jsonElement.GetDouble();
                }
                else if (targetType == typeof(decimal))
                {
                    return (T)(object)jsonElement.GetDecimal();
                }
                else if (targetType.IsEnum)
                {
                    string stringValue = jsonElement.GetString();
                    return (T)Enum.Parse(targetType, stringValue);
                }
                // Handle complex types (List<T>, Dictionary<K,V>, etc.)
                else
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Config", $"Failed to deserialize JsonElement for key {key}: {ex.Message}", ex);
                return defaultValue;
            }
        }

        public void Set<T>(string key, T value, bool saveImmediately = false)
        {
            if (!config.TryGetValue(key, out var configValue))
            {
                Logger.Instance.Warning("Config", $"Unknown config key: {key}");
                return;
            }

            if (configValue.IsReadOnly)
            {
                Logger.Instance.Warning("Config", $"Cannot modify read-only config key: {key}");
                return;
            }

            // Validate
            if (configValue.Validator != null && !configValue.Validator(value))
            {
                Logger.Instance.Warning("Config", $"Validation failed for config key {key} with value {value}");
                return;
            }

            var oldValue = configValue.Value;
            configValue.Value = value;

            Logger.Instance.Info("Config", $"Config changed: {key} = {value} (was {oldValue})");

            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs { Key = key, OldValue = oldValue, NewValue = value });

            if (saveImmediately && !string.IsNullOrEmpty(configFilePath))
            {
                SaveToFile(configFilePath);
            }
        }

        public Dictionary<string, ConfigValue> GetCategory(string category)
        {
            return categories.TryGetValue(category, out var cat) ? cat : new Dictionary<string, ConfigValue>();
        }

        public List<string> GetCategories()
        {
            return categories.Keys.ToList();
        }

        public void SaveToFile(string path)
        {
            // Synchronous wrapper for backward compatibility
            SaveToFileAsync(path).GetAwaiter().GetResult();
        }

        public async Task SaveToFileAsync(string path)
        {
            try
            {
                var configData = config.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Value
                );

                string json = JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                await File.WriteAllTextAsync(path, json, Encoding.UTF8);

                Logger.Instance.Info("Config", $"Configuration saved to {path}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Config", $"Failed to save configuration: {ex.Message}", ex);
            }
        }

        public void LoadFromFile(string path)
        {
            // Synchronous wrapper for backward compatibility
            LoadFromFileAsync(path).GetAwaiter().GetResult();
        }

        public async Task LoadFromFileAsync(string path)
        {
            try
            {
                string json = await File.ReadAllTextAsync(path, Encoding.UTF8);
                var configData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                foreach (var kvp in configData)
                {
                    if (config.TryGetValue(kvp.Key, out var configValue))
                    {
                        try
                        {
                            // Deserialize based on the registered type
                            object value = JsonSerializer.Deserialize(kvp.Value.GetRawText(), configValue.ValueType);
                            configValue.Value = value;
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Warning("Config", $"Failed to load config value {kvp.Key}: {ex.Message}");
                        }
                    }
                }

                Logger.Instance.Info("Config", $"Configuration loaded from {path}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Config", $"Failed to load configuration: {ex.Message}", ex);
            }
        }

        public void ResetToDefaults()
        {
            foreach (var kvp in config)
            {
                kvp.Value.Value = kvp.Value.DefaultValue;
            }

            Logger.Instance.Info("Config", "Configuration reset to defaults");
        }
    }

    public class ConfigChangedEventArgs : EventArgs
    {
        public string Key { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }
}
