using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SuperTUI.Core.Infrastructure;

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
    ///
    /// PHASE 2 FIX: Improved type conversion with better error handling
    ///
    /// SUPPORTED TYPES:
    /// - Primitives: int, long, bool, double, decimal, float, byte, etc.
    /// - Strings: string
    /// - Enums: any enum type
    /// - Collections: List<T>, Dictionary<K,V>, T[] where T is supported
    /// - Complex objects: any class with public properties
    /// - Nullable types: int?, bool?, etc.
    ///
    /// UNSUPPORTED TYPES:
    /// - Delegates, functions
    /// - Interfaces (without concrete implementation)
    /// - Abstract classes
    /// - Generic types with complex constraints
    /// </summary>
    public class ConfigurationManager : IConfigurationManager
    {
        private static ConfigurationManager instance;

        /// <summary>
        /// Singleton instance - DEPRECATED in Phase 3
        /// Use dependency injection: Get IConfigurationManager from ServiceContainer
        /// </summary>
        [Obsolete("Use dependency injection instead. Get IConfigurationManager from ServiceContainer.", error: false)]
        public static ConfigurationManager Instance => instance ??= new ConfigurationManager();

        private Dictionary<string, ConfigValue> config = new Dictionary<string, ConfigValue>();
        private Dictionary<string, Dictionary<string, ConfigValue>> categories = new Dictionary<string, Dictionary<string, ConfigValue>>();
        private string configFilePath;
        private bool isInitialized = false;

        public event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        public void Initialize(string configPath)
        {
            if (isInitialized)
            {
                Logger.Instance.Warning("Config", "ConfigurationManager already initialized. Ignoring duplicate Initialize() call.");
                return;
            }

            configFilePath = configPath;
            RegisterDefaultSettings();
            isInitialized = true;

            if (File.Exists(configPath))
            {
                LoadFromFile(configPath);

                // Validate configuration after loading
                Validate();
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

        // Interface-compliant non-generic Register method
        public void Register(string key, object defaultValue, string description, string category = "General", Func<object, bool> validator = null)
        {
            var configValue = new ConfigValue
            {
                Key = key,
                Value = defaultValue,
                DefaultValue = defaultValue,
                ValueType = defaultValue?.GetType() ?? typeof(object),
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
                    return GetValueInternal<T>(configValue, key, defaultValue, throwOnError: false);
                }
                catch (Exception ex)
                {
                    // PHASE 2 FIX: Better error reporting with ErrorPolicy
                    var targetType = typeof(T);
                    var sourceType = configValue.Value?.GetType().Name ?? "null";

                    ErrorHandlingPolicy.Handle(
                        ErrorCategory.Configuration,
                        ex,
                        $"Failed to convert config key '{key}' from {sourceType} to {targetType.Name} (value: {configValue.Value})");

                    return defaultValue;
                }
            }

            // Key not found - log warning
            Logger.Instance.Debug("Config", $"Configuration key '{key}' not found, using default value: {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// Internal method for type conversion that can throw or return defaults
        /// </summary>
        private T GetValueInternal<T>(ConfigValue configValue, string key, T defaultValue, bool throwOnError)
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
                        if (throwOnError)
                            throw new InvalidCastException($"JSON deserialization returned null for key '{key}'");
                        return defaultValue;
                    }

                    Logger.Instance.Debug("Config", $"Successfully converted {key} via JSON round-trip");
                    return result;
                }
                catch (JsonException ex)
                {
                    Logger.Instance.Error("Config", $"JSON serialization failed for key {key}: {ex.Message}", ex);
                    if (throwOnError)
                        throw new InvalidCastException($"JSON serialization failed for key '{key}': {ex.Message}", ex);
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

            // PHASE 2 FIX: Fail fast on truly unsupported types
            if (targetType.IsInterface || targetType.IsAbstract)
            {
                throw new NotSupportedException(
                    $"Configuration does not support interface or abstract types: {targetType.Name}. " +
                    $"Use a concrete class instead.");
            }

            if (typeof(Delegate).IsAssignableFrom(targetType))
            {
                throw new NotSupportedException(
                    $"Configuration does not support delegate types: {targetType.Name}");
            }

            // Last resort - try direct cast
            try
            {
                return (T)configValue.Value;
            }
            catch (InvalidCastException ex)
            {
                throw new NotSupportedException(
                    $"Configuration type conversion failed for '{key}'. " +
                    $"Type {targetType.Name} is not supported or value cannot be converted. " +
                    $"Supported types: primitives, strings, enums, List<T>, Dictionary<K,V>, arrays, and simple objects. " +
                    $"See ConfigurationManager documentation for details.",
                    ex);
            }
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
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Configuration,
                    ex,
                    $"Failed to deserialize JsonElement for key {key}");
                return defaultValue;
            }
        }

        public void Set<T>(string key, T value, bool saveImmediately = false)
        {
            if (!config.TryGetValue(key, out var configValue))
            {
                Logger.Instance.Warning("Config", $"Unknown config key: {key}");
                throw new ArgumentException($"Unknown configuration key: {key}", nameof(key));
            }

            if (configValue.IsReadOnly)
            {
                Logger.Instance.Warning("Config", $"Cannot modify read-only config key: {key}");
                throw new InvalidOperationException($"Configuration key '{key}' is read-only and cannot be modified");
            }

            // Validate
            if (configValue.Validator != null && !configValue.Validator(value))
            {
                Logger.Instance.Warning("Config", $"Validation failed for config key {key} with value {value}");
                throw new ArgumentException($"Validation failed for configuration key '{key}' with value '{value}'", nameof(value));
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

        // Interface-compliant Save method (uses configured path)
        public void Save()
        {
            if (string.IsNullOrEmpty(configFilePath))
            {
                Logger.Instance.Warning("Config", "Cannot save: no config file path configured");
                return;
            }
            SaveToFile(configFilePath);
        }

        // Interface-compliant Load method (uses configured path)
        public void Load()
        {
            if (string.IsNullOrEmpty(configFilePath))
            {
                Logger.Instance.Warning("Config", "Cannot load: no config file path configured");
                return;
            }
            if (File.Exists(configFilePath))
            {
                LoadFromFile(configFilePath);
            }
        }

        public void SaveToFile(string path)
        {
            // Synchronous wrapper - use GetAwaiter().GetResult() to avoid deadlocks
            SaveToFileAsync(path).ConfigureAwait(false).GetAwaiter().GetResult();
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
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Configuration,
                    ex,
                    $"Saving configuration to {path}");
            }
        }

        public void LoadFromFile(string path)
        {
            // Synchronous wrapper - use GetAwaiter().GetResult() to avoid deadlocks
            LoadFromFileAsync(path).ConfigureAwait(false).GetAwaiter().GetResult();
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
                        ErrorHandlingPolicy.SafeExecute(
                            ErrorCategory.Configuration,
                            () =>
                            {
                                // Deserialize based on the registered type
                                object value = JsonSerializer.Deserialize(kvp.Value.GetRawText(), configValue.ValueType);
                                configValue.Value = value;
                            },
                            context: $"Loading config value {kvp.Key}");
                    }
                }

                Logger.Instance.Info("Config", $"Configuration loaded from {path}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Configuration,
                    ex,
                    $"Loading configuration from {path}");
            }
        }

        /// <summary>
        /// Validates all configuration values against their registered validators.
        /// Logs warnings for invalid values but does not throw exceptions.
        /// </summary>
        /// <returns>True if all values are valid, false otherwise</returns>
        public bool Validate()
        {
            bool allValid = true;
            int validatedCount = 0;
            int invalidCount = 0;

            foreach (var kvp in config)
            {
                validatedCount++;
                var configValue = kvp.Value;

                // Check for null when not expected
                if (configValue.Value == null && configValue.DefaultValue != null)
                {
                    Logger.Instance.Warning("Config",
                        $"Configuration key '{kvp.Key}' has null value but expects type {configValue.ValueType.Name}. " +
                        $"Using default: {configValue.DefaultValue}");
                    configValue.Value = configValue.DefaultValue;
                    allValid = false;
                    invalidCount++;
                    continue;
                }

                // Run validator if configured
                if (configValue.Validator != null && configValue.Value != null)
                {
                    try
                    {
                        if (!configValue.Validator(configValue.Value))
                        {
                            Logger.Instance.Warning("Config",
                                $"Configuration key '{kvp.Key}' failed validation. " +
                                $"Value: {configValue.Value}, Default: {configValue.DefaultValue}. " +
                                $"Resetting to default.");
                            configValue.Value = configValue.DefaultValue;
                            allValid = false;
                            invalidCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("Config",
                            $"Validator for '{kvp.Key}' threw exception: {ex.Message}. " +
                            $"Resetting to default.", ex);
                        configValue.Value = configValue.DefaultValue;
                        allValid = false;
                        invalidCount++;
                    }
                }
            }

            if (allValid)
            {
                Logger.Instance.Info("Config", $"Configuration validation passed. {validatedCount} values checked.");
            }
            else
            {
                Logger.Instance.Warning("Config",
                    $"Configuration validation found {invalidCount} invalid values out of {validatedCount} total. " +
                    $"Invalid values reset to defaults.");
            }

            return allValid;
        }

        /// <summary>
        /// Gets a configuration value with strict validation.
        /// Throws exception if key doesn't exist or type conversion fails.
        /// Use this when configuration values are required for operation.
        /// </summary>
        /// <typeparam name="T">Expected type of the value</typeparam>
        /// <param name="key">Configuration key</param>
        /// <returns>Configuration value</returns>
        /// <exception cref="KeyNotFoundException">If key doesn't exist</exception>
        /// <exception cref="InvalidCastException">If type conversion fails</exception>
        public T GetStrict<T>(string key)
        {
            if (!config.ContainsKey(key))
            {
                throw new KeyNotFoundException(
                    $"Required configuration key '{key}' not found. " +
                    $"Available keys: {string.Join(", ", config.Keys.Take(10))}...");
            }

            var configValue = config[key];

            if (configValue.Value == null)
            {
                if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                {
                    throw new InvalidOperationException(
                        $"Configuration key '{key}' has null value but type {typeof(T).Name} is not nullable.");
                }
                return default(T);
            }

            try
            {
                // Use internal method with throwOnError=true for strict enforcement
                T value = GetValueInternal<T>(configValue, key, default(T), throwOnError: true);

                // Verify we got a valid value (for reference types)
                if (value == null && configValue.Value != null)
                {
                    throw new InvalidCastException(
                        $"Failed to convert configuration key '{key}' " +
                        $"from {configValue.Value.GetType().Name} to {typeof(T).Name}");
                }

                return value;
            }
            catch (KeyNotFoundException)
            {
                // Re-throw key not found exceptions
                throw;
            }
            catch (InvalidOperationException)
            {
                // Re-throw invalid operation exceptions
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    $"Failed to get configuration key '{key}' as type {typeof(T).Name}. " +
                    $"Current value type: {configValue.Value?.GetType().Name ?? "null"}. " +
                    $"Error: {ex.Message}", ex);
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

        /// <summary>
        /// Reset initialization state for testing purposes ONLY.
        /// WARNING: This bypasses state guarantees and should NEVER be used in production.
        /// </summary>
        public void ResetForTesting()
        {
            #if DEBUG
            isInitialized = false;
            config = new Dictionary<string, ConfigValue>();
            categories = new Dictionary<string, Dictionary<string, ConfigValue>>();
            configFilePath = null;
            Logger.Instance.Warning("Config", "ConfigurationManager reset for testing - NOT for production use");
            #else
            throw new InvalidOperationException(
                "ResetForTesting() is only available in DEBUG builds and should NEVER be called in production.");
            #endif
        }
    }

    public class ConfigChangedEventArgs : EventArgs
    {
        public string Key { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }
}
