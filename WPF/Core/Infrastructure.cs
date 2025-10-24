using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SuperTUI.Infrastructure
{
    // ============================================================================
    // LOGGING SYSTEM
    // ============================================================================

    /// <summary>
    /// Log severity levels
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// Log entry structure
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public string StackTrace { get; set; }
        public int ThreadId { get; set; }
    }

    /// <summary>
    /// Log sink interface for extensibility
    /// </summary>
    public interface ILogSink
    {
        void Write(LogEntry entry);
        void Flush();
    }

    /// <summary>
    /// File-based log sink with rotation
    /// </summary>
    public class FileLogSink : ILogSink
    {
        private readonly string logDirectory;
        private readonly string logFilePrefix;
        private readonly long maxFileSizeBytes;
        private readonly int maxFiles;
        private readonly object lockObject = new object();
        private StreamWriter currentWriter;
        private string currentFilePath;
        private long currentFileSize;

        public FileLogSink(string logDirectory, string logFilePrefix = "supertui", long maxFileSizeMB = 10, int maxFiles = 5)
        {
            this.logDirectory = logDirectory;
            this.logFilePrefix = logFilePrefix;
            this.maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;
            this.maxFiles = maxFiles;

            Directory.CreateDirectory(logDirectory);
            OpenNewLogFile();
        }

        private void OpenNewLogFile()
        {
            if (currentWriter != null)
            {
                currentWriter.Flush();
                currentWriter.Close();
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            currentFilePath = Path.Combine(logDirectory, $"{logFilePrefix}_{timestamp}.log");
            currentWriter = new StreamWriter(currentFilePath, true, Encoding.UTF8) { AutoFlush = false };
            currentFileSize = 0;

            // Rotate old files
            RotateOldFiles();
        }

        private void RotateOldFiles()
        {
            var logFiles = Directory.GetFiles(logDirectory, $"{logFilePrefix}_*.log")
                .OrderByDescending(f => File.GetCreationTime(f))
                .Skip(maxFiles)
                .ToList();

            foreach (var file in logFiles)
            {
                try { File.Delete(file); } catch { }
            }
        }

        public void Write(LogEntry entry)
        {
            lock (lockObject)
            {
                string line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level,-8}] [{entry.Category}] {entry.Message}";

                if (entry.Exception != null)
                {
                    line += $"\n    Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}";
                    line += $"\n    {entry.Exception.StackTrace}";
                }

                if (entry.Properties.Count > 0)
                {
                    line += $"\n    Properties: {string.Join(", ", entry.Properties.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
                }

                line += "\n";

                byte[] bytes = Encoding.UTF8.GetBytes(line);
                currentFileSize += bytes.Length;

                if (currentFileSize > maxFileSizeBytes)
                {
                    OpenNewLogFile();
                }

                currentWriter.Write(line);
            }
        }

        public void Flush()
        {
            lock (lockObject)
            {
                currentWriter?.Flush();
            }
        }
    }

    /// <summary>
    /// In-memory log sink for debugging and testing
    /// </summary>
    public class MemoryLogSink : ILogSink
    {
        private readonly int maxEntries;
        private readonly Queue<LogEntry> entries;

        public MemoryLogSink(int maxEntries = 1000)
        {
            this.maxEntries = maxEntries;
            this.entries = new Queue<LogEntry>(maxEntries);
        }

        public void Write(LogEntry entry)
        {
            lock (entries)
            {
                if (entries.Count >= maxEntries)
                {
                    entries.Dequeue();
                }
                entries.Enqueue(entry);
            }
        }

        public void Flush() { }

        public List<LogEntry> GetEntries(LogLevel minLevel = LogLevel.Trace, string category = null, int count = 100)
        {
            lock (entries)
            {
                return entries
                    .Where(e => e.Level >= minLevel && (category == null || e.Category == category))
                    .TakeLast(count)
                    .ToList();
            }
        }

        public void Clear()
        {
            lock (entries)
            {
                entries.Clear();
            }
        }
    }

    /// <summary>
    /// Centralized logging system with multiple sinks
    /// </summary>
    public class Logger : ILogger
    {
        private static Logger instance;
        public static Logger Instance => instance ??= new Logger();

        private readonly List<ILogSink> sinks = new List<ILogSink>();
        private LogLevel minLevel = LogLevel.Info;
        private readonly HashSet<string> enabledCategories = new HashSet<string>();
        private bool logAllCategories = true;

        public void AddSink(ILogSink sink)
        {
            lock (sinks)
            {
                sinks.Add(sink);
            }
        }

        public void SetMinLevel(LogLevel level)
        {
            minLevel = level;
        }

        public void EnableCategory(string category)
        {
            logAllCategories = false;
            enabledCategories.Add(category);
        }

        public void DisableCategory(string category)
        {
            enabledCategories.Remove(category);
        }

        public void Log(LogLevel level, string category, string message, Exception exception = null, Dictionary<string, object> properties = null)
        {
            if (level < minLevel)
                return;

            if (!logAllCategories && !enabledCategories.Contains(category))
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Category = category,
                Message = message,
                Exception = exception,
                Properties = properties ?? new Dictionary<string, object>(),
                StackTrace = level >= LogLevel.Error ? Environment.StackTrace : null,
                ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId
            };

            lock (sinks)
            {
                foreach (var sink in sinks)
                {
                    try
                    {
                        sink.Write(entry);
                    }
                    catch
                    {
                        // Don't let logging errors crash the app
                    }
                }
            }
        }

        public void Trace(string category, string message) => Log(LogLevel.Trace, category, message);
        public void Debug(string category, string message) => Log(LogLevel.Debug, category, message);
        public void Info(string category, string message) => Log(LogLevel.Info, category, message);
        public void Warning(string category, string message) => Log(LogLevel.Warning, category, message);
        public void Error(string category, string message, Exception ex = null) => Log(LogLevel.Error, category, message, ex);
        public void Critical(string category, string message, Exception ex = null) => Log(LogLevel.Critical, category, message, ex);

        public void Flush()
        {
            lock (sinks)
            {
                foreach (var sink in sinks)
                {
                    sink.Flush();
                }
            }
        }
    }

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
            Register("App.LogDirectory", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SuperTUI", "Logs"), "Log file directory", "Application");
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
            Register("Backup.Directory", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SuperTUI", "Backups"), "Backup directory", "Backup");
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

                    // Handle special cases that Convert.ChangeType doesn't support
                    var targetType = typeof(T);

                    // Collections and complex types - return as-is or default
                    if (targetType.IsGenericType || targetType.IsArray || !targetType.IsPrimitive && targetType != typeof(string) && targetType != typeof(decimal))
                    {
                        // For complex types, try direct cast or return default
                        if (configValue.Value != null && configValue.Value.GetType() == targetType)
                        {
                            return (T)configValue.Value;
                        }

                        Logger.Instance.Warning("Config", $"Cannot convert complex type for key {key}, returning default");
                        return defaultValue;
                    }

                    // Primitive types - use Convert.ChangeType
                    return (T)Convert.ChangeType(configValue.Value, typeof(T));
                }
                catch (Exception ex)
                {
                    Logger.Instance.Warning("Config", $"Failed to convert config value {key}: {ex.Message}");
                    return defaultValue;
                }
            }

            return defaultValue;
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
            try
            {
                var configData = config.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Value
                );

                string json = JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json, Encoding.UTF8);

                Logger.Instance.Info("Config", $"Configuration saved to {path}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Config", $"Failed to save configuration: {ex.Message}", ex);
            }
        }

        public void LoadFromFile(string path)
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
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

    // ============================================================================
    // THEME SYSTEM
    // ============================================================================

    /// <summary>
    /// Complete theme definition with all UI colors and styles
    /// </summary>
    public class Theme
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsDark { get; set; }

        // Primary colors
        public Color Primary { get; set; }
        public Color Secondary { get; set; }
        public Color Success { get; set; }
        public Color Warning { get; set; }
        public Color Error { get; set; }
        public Color Info { get; set; }

        // Background colors
        public Color Background { get; set; }
        public Color BackgroundSecondary { get; set; }
        public Color Surface { get; set; }
        public Color SurfaceHighlight { get; set; }

        // Text colors
        public Color Foreground { get; set; }
        public Color ForegroundSecondary { get; set; }
        public Color ForegroundDisabled { get; set; }

        // UI element colors
        public Color Border { get; set; }
        public Color BorderActive { get; set; }
        public Color Focus { get; set; }
        public Color Selection { get; set; }
        public Color Hover { get; set; }
        public Color Active { get; set; }

        // Syntax highlighting colors (for code editors)
        public Color SyntaxKeyword { get; set; }
        public Color SyntaxString { get; set; }
        public Color SyntaxComment { get; set; }
        public Color SyntaxNumber { get; set; }
        public Color SyntaxFunction { get; set; }
        public Color SyntaxVariable { get; set; }

        public static Theme CreateDarkTheme()
        {
            return new Theme
            {
                Name = "Dark",
                Description = "Dark theme with high contrast",
                IsDark = true,

                Primary = Color.FromRgb(0, 120, 212),        // #0078D4
                Secondary = Color.FromRgb(107, 107, 107),     // #6B6B6B
                Success = Color.FromRgb(16, 124, 16),         // #107C10
                Warning = Color.FromRgb(255, 140, 0),         // #FF8C00
                Error = Color.FromRgb(232, 17, 35),           // #E81123
                Info = Color.FromRgb(78, 201, 176),           // #4EC9B0

                Background = Color.FromRgb(12, 12, 12),       // #0C0C0C
                BackgroundSecondary = Color.FromRgb(26, 26, 26), // #1A1A1A
                Surface = Color.FromRgb(30, 30, 30),          // #1E1E1E
                SurfaceHighlight = Color.FromRgb(45, 45, 45), // #2D2D2D

                Foreground = Color.FromRgb(204, 204, 204),    // #CCCCCC
                ForegroundSecondary = Color.FromRgb(136, 136, 136), // #888888
                ForegroundDisabled = Color.FromRgb(102, 102, 102),  // #666666

                Border = Color.FromRgb(58, 58, 58),           // #3A3A3A
                BorderActive = Color.FromRgb(78, 201, 176),   // #4EC9B0
                Focus = Color.FromRgb(78, 201, 176),          // #4EC9B0
                Selection = Color.FromRgb(51, 153, 255),      // #3399FF
                Hover = Color.FromRgb(45, 45, 48),            // #2D2D30
                Active = Color.FromRgb(62, 62, 64),           // #3E3E40

                SyntaxKeyword = Color.FromRgb(86, 156, 214),  // #569CD6
                SyntaxString = Color.FromRgb(206, 145, 120),  // #CE9178
                SyntaxComment = Color.FromRgb(106, 153, 85),  // #6A9955
                SyntaxNumber = Color.FromRgb(181, 206, 168),  // #B5CEA8
                SyntaxFunction = Color.FromRgb(220, 220, 170), // #DCDCAA
                SyntaxVariable = Color.FromRgb(156, 220, 254)  // #9CDCFE
            };
        }

        public static Theme CreateLightTheme()
        {
            return new Theme
            {
                Name = "Light",
                Description = "Light theme with clean aesthetics",
                IsDark = false,

                Primary = Color.FromRgb(0, 120, 212),
                Secondary = Color.FromRgb(150, 150, 150),
                Success = Color.FromRgb(16, 124, 16),
                Warning = Color.FromRgb(255, 140, 0),
                Error = Color.FromRgb(232, 17, 35),
                Info = Color.FromRgb(0, 120, 212),

                Background = Color.FromRgb(255, 255, 255),
                BackgroundSecondary = Color.FromRgb(245, 245, 245),
                Surface = Color.FromRgb(250, 250, 250),
                SurfaceHighlight = Color.FromRgb(240, 240, 240),

                Foreground = Color.FromRgb(0, 0, 0),
                ForegroundSecondary = Color.FromRgb(96, 96, 96),
                ForegroundDisabled = Color.FromRgb(168, 168, 168),

                Border = Color.FromRgb(204, 204, 204),
                BorderActive = Color.FromRgb(0, 120, 212),
                Focus = Color.FromRgb(0, 120, 212),
                Selection = Color.FromRgb(51, 153, 255),
                Hover = Color.FromRgb(229, 229, 229),
                Active = Color.FromRgb(204, 204, 204),

                SyntaxKeyword = Color.FromRgb(0, 0, 255),
                SyntaxString = Color.FromRgb(163, 21, 21),
                SyntaxComment = Color.FromRgb(0, 128, 0),
                SyntaxNumber = Color.FromRgb(9, 134, 88),
                SyntaxFunction = Color.FromRgb(121, 94, 38),
                SyntaxVariable = Color.FromRgb(0, 16, 128)
            };
        }
    }

    /// <summary>
    /// Theme manager with hot-reloading support
    /// </summary>
    public class ThemeManager : IThemeManager
    {
        private static ThemeManager instance;
        public static ThemeManager Instance => instance ??= new ThemeManager();

        private readonly Dictionary<string, Theme> themes = new Dictionary<string, Theme>();
        private Theme currentTheme;
        private string themesDirectory;

        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        public Theme CurrentTheme => currentTheme;

        public void Initialize(string themesDir = null)
        {
            themesDirectory = themesDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SuperTUI", "Themes");

            // Register built-in themes
            RegisterTheme(Theme.CreateDarkTheme());
            RegisterTheme(Theme.CreateLightTheme());

            // Load custom themes
            LoadCustomThemes();

            // Apply saved theme from config
            string savedTheme = ConfigurationManager.Instance.Get<string>("UI.Theme", "Dark");
            ApplyTheme(savedTheme);
        }

        public void RegisterTheme(Theme theme)
        {
            themes[theme.Name] = theme;
            Logger.Instance.Debug("Theme", $"Registered theme: {theme.Name}");
        }

        public void ApplyTheme(string themeName)
        {
            if (!themes.TryGetValue(themeName, out var theme))
            {
                Logger.Instance.Warning("Theme", $"Theme not found: {themeName}, falling back to Dark");
                theme = themes["Dark"];
            }

            var oldTheme = currentTheme;
            currentTheme = theme;

            Logger.Instance.Info("Theme", $"Applied theme: {theme.Name}");

            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs { OldTheme = oldTheme, NewTheme = theme });
        }

        public List<Theme> GetAvailableThemes()
        {
            return themes.Values.ToList();
        }

        public void SaveTheme(Theme theme, string filename = null)
        {
            try
            {
                filename = filename ?? Path.Combine(themesDirectory, $"{theme.Name}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(filename));

                string json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filename, json, Encoding.UTF8);

                Logger.Instance.Info("Theme", $"Saved theme {theme.Name} to {filename}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Theme", $"Failed to save theme: {ex.Message}", ex);
            }
        }

        private void LoadCustomThemes()
        {
            if (!Directory.Exists(themesDirectory))
                return;

            try
            {
                foreach (var file in Directory.GetFiles(themesDirectory, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file, Encoding.UTF8);
                        var theme = JsonSerializer.Deserialize<Theme>(json);
                        RegisterTheme(theme);
                        Logger.Instance.Info("Theme", $"Loaded custom theme: {theme.Name}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning("Theme", $"Failed to load theme from {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Theme", $"Failed to load custom themes: {ex.Message}", ex);
            }
        }
    }

    public class ThemeChangedEventArgs : EventArgs
    {
        public Theme OldTheme { get; set; }
        public Theme NewTheme { get; set; }
    }

    // ============================================================================
    // INPUT VALIDATION & SECURITY
    // ============================================================================

    /// <summary>
    /// Input validation utilities for security
    /// </summary>
    public static class ValidationHelper
    {
        // Common regex patterns
        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex AlphanumericRegex = new Regex(@"^[a-zA-Z0-9]+$", RegexOptions.Compiled);
        private static readonly Regex PathSeparatorRegex = new Regex(@"[<>:|?*]", RegexOptions.Compiled);

        public static bool IsValidEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email);
        }

        public static bool IsAlphanumeric(string input)
        {
            return !string.IsNullOrWhiteSpace(input) && AlphanumericRegex.IsMatch(input);
        }

        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Check for invalid characters
                if (PathSeparatorRegex.IsMatch(path))
                    return false;

                // Check for path traversal
                string fullPath = Path.GetFullPath(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsWithinDirectory(string path, string allowedDirectory)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                string fullAllowedPath = Path.GetFullPath(allowedDirectory);
                return fullPath.StartsWith(fullAllowedPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string SanitizeFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return "unnamed";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", filename.Split(invalidChars));
        }

        public static string SanitizeInput(string input, int maxLength = 1000)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Truncate if too long
            if (input.Length > maxLength)
                input = input.Substring(0, maxLength);

            // Remove control characters
            return new string(input.Where(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').ToArray());
        }
    }

    /// <summary>
    /// Security manager for file access validation and sandboxing
    /// </summary>
    public class SecurityManager : ISecurityManager
    {
        private static SecurityManager instance;
        public static SecurityManager Instance => instance ??= new SecurityManager();

        private readonly HashSet<string> allowedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private long maxFileSizeBytes;

        public void Initialize()
        {
            // Load from config
            var extensions = ConfigurationManager.Instance.Get<List<string>>("Security.AllowedExtensions");
            if (extensions != null)
            {
                foreach (var ext in extensions)
                {
                    allowedExtensions.Add(ext);
                }
            }

            maxFileSizeBytes = ConfigurationManager.Instance.Get<int>("Security.MaxFileSize", 10) * 1024 * 1024;

            // Add default allowed directories
            AddAllowedDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddAllowedDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

            Logger.Instance.Info("Security", "Security manager initialized");
        }

        public void AddAllowedDirectory(string directory)
        {
            try
            {
                string fullPath = Path.GetFullPath(directory);

                // Ensure path ends with directory separator for proper prefix matching
                if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                    !fullPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                {
                    fullPath += Path.DirectorySeparatorChar;
                }

                allowedDirectories.Add(fullPath);
                Logger.Instance.Debug("Security", $"Added allowed directory: {fullPath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning("Security", $"Failed to add allowed directory {directory}: {ex.Message}");
            }
        }

        public bool ValidateFileAccess(string path, bool checkWrite = false)
        {
            if (!ConfigurationManager.Instance.Get<bool>("Security.ValidateFileAccess", true))
                return true; // Validation disabled

            try
            {
                // Validate path format
                if (!ValidationHelper.IsValidPath(path))
                {
                    Logger.Instance.Warning("Security", $"Invalid path format: {path}");
                    return false;
                }

                string fullPath = Path.GetFullPath(path);

                // Ensure fullPath also has trailing separator for directory comparison
                string fullPathWithSeparator = fullPath;
                if (Directory.Exists(fullPath))
                {
                    if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                        !fullPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                    {
                        fullPathWithSeparator += Path.DirectorySeparatorChar;
                    }
                }

                // Check if within allowed directories
                // Both paths now end with separator, preventing "C:\AllowedDir" from matching "C:\AllowedDir_Evil"
                bool inAllowedDirectory = allowedDirectories.Any(dir =>
                    fullPathWithSeparator.StartsWith(dir, StringComparison.OrdinalIgnoreCase) ||
                    (fullPath + Path.DirectorySeparatorChar).StartsWith(dir, StringComparison.OrdinalIgnoreCase));

                if (!inAllowedDirectory)
                {
                    Logger.Instance.Warning("Security", $"Path outside allowed directories: {path}");
                    return false;
                }

                // Check extension (case-insensitive)
                string extension = Path.GetExtension(path);
                if (!string.IsNullOrEmpty(extension) && !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    Logger.Instance.Warning("Security", $"File extension not allowed: {extension}");
                    return false;
                }

                // Check file size (if exists)
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    if (fileInfo.Length > maxFileSizeBytes)
                    {
                        Logger.Instance.Warning("Security", $"File too large: {fileInfo.Length} bytes");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Security", $"File access validation error: {ex.Message}", ex);
                return false;
            }
        }

        public bool ValidateScriptExecution()
        {
            return ConfigurationManager.Instance.Get<bool>("Security.AllowScriptExecution", false);
        }
    }

    // ============================================================================
    // ERROR HANDLING & RESILIENCE
    // ============================================================================

    /// <summary>
    /// Global error handler with recovery strategies
    /// </summary>
    public class ErrorHandler : IErrorHandler
    {
        private static ErrorHandler instance;
        public static ErrorHandler Instance => instance ??= new ErrorHandler();

        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        public void HandleError(Exception ex, string context, ErrorSeverity severity = ErrorSeverity.Error, bool showToUser = true)
        {
            Logger.Instance.Error(context, $"Error occurred: {ex.Message}", ex);

            ErrorOccurred?.Invoke(this, new ErrorEventArgs
            {
                Exception = ex,
                Context = context,
                Severity = severity,
                Timestamp = DateTime.Now
            });

            if (showToUser && severity >= ErrorSeverity.Error)
            {
                // TODO: Show error dialog to user
                ShowErrorDialog(ex, context);
            }
        }

        private void ShowErrorDialog(Exception ex, string context)
        {
            // Simple message box for now - can be replaced with custom dialog
            System.Windows.MessageBox.Show(
                $"An error occurred in {context}:\n\n{ex.Message}\n\nSee log for details.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        /// <summary>
        /// Execute an action with retry logic (synchronous - BLOCKS UI THREAD)
        /// </summary>
        /// <remarks>
        /// WARNING: This method blocks the UI thread during retries. Prefer ExecuteWithRetryAsync for UI operations.
        /// </remarks>
        [Obsolete("Use ExecuteWithRetryAsync for UI operations to avoid blocking the UI thread")]
        public T ExecuteWithRetry<T>(Func<T> action, int maxRetries = 3, int delayMs = 100, string context = "Operation")
        {
            int attempts = 0;
            Exception lastException = null;

            while (attempts < maxRetries)
            {
                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;

                    if (attempts < maxRetries)
                    {
                        Logger.Instance.Warning(context, $"Attempt {attempts} failed, retrying: {ex.Message}");
                        System.Threading.Thread.Sleep(delayMs * attempts); // Exponential backoff
                    }
                }
            }

            HandleError(lastException, context, ErrorSeverity.Error);
            throw lastException;
        }

        /// <summary>
        /// Execute an async action with retry logic (non-blocking)
        /// </summary>
        /// <remarks>
        /// Use this method for UI operations to avoid blocking the UI thread during retries.
        /// </remarks>
        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int delayMs = 100, string context = "Operation")
        {
            int attempts = 0;
            Exception lastException = null;

            while (attempts < maxRetries)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;

                    if (attempts < maxRetries)
                    {
                        Logger.Instance.Warning(context, $"Attempt {attempts} failed, retrying: {ex.Message}");
                        await Task.Delay(delayMs * attempts); // Exponential backoff, non-blocking
                    }
                }
            }

            HandleError(lastException, context, ErrorSeverity.Error);
            throw lastException;
        }
    }

    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public string Context { get; set; }
        public ErrorSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
