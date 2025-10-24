using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for logging service - enables testing and mocking
    /// </summary>
    public interface ILogger
    {
        void Trace(string category, string message);
        void Debug(string category, string message);
        void Info(string category, string message);
        void Warning(string category, string message);
        void Error(string category, string message, Exception ex = null);
        void Critical(string category, string message, Exception ex = null);

        void Log(LogLevel level, string category, string message, Exception exception = null, Dictionary<string, object> properties = null);

        void SetMinLevel(LogLevel level);
        void AddSink(ILogSink sink);
        void Flush();

        void EnableCategory(string category);
        void DisableCategory(string category);
    }
}


using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for theme management - enables testing and mocking
    /// </summary>
    public interface IThemeManager
    {
        Theme CurrentTheme { get; }
        event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        void Initialize(string themesDirectory = null);
        void ApplyTheme(string themeName);
        void RegisterTheme(Theme theme);
        void SaveTheme(Theme theme, string filename = null);

        List<Theme> GetAvailableThemes();
    }
}


using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for configuration management - enables testing and mocking
    /// </summary>
    public interface IConfigurationManager
    {
        void Initialize(string configFilePath);
        void Register(string key, object defaultValue, string description, string category = "General", Func<object, bool> validator = null);

        T Get<T>(string key, T defaultValue = default);
        void Set<T>(string key, T value, bool saveImmediately = false);

        void Save();
        void Load();
        void ResetToDefaults();

        Dictionary<string, ConfigValue> GetCategory(string category);
        List<string> GetCategories();
    }
}


using System;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for security management - enables testing and mocking
    /// </summary>
    public interface ISecurityManager
    {
        void Initialize();
        void AddAllowedDirectory(string directory);
        bool ValidateFileAccess(string path, bool checkWrite = false);
        bool ValidateScriptExecution();
    }
}


using System;
using System.Threading.Tasks;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for error handling - enables testing and mocking
    /// </summary>
    public interface IErrorHandler
    {
        event EventHandler<ErrorEventArgs> ErrorOccurred;

        void HandleError(Exception ex, string context, ErrorSeverity severity = ErrorSeverity.Error, bool showToUser = true);

        [Obsolete("Use ExecuteWithRetryAsync for UI operations to avoid blocking the UI thread")]
        T ExecuteWithRetry<T>(Func<T> action, int maxRetries = 3, int delayMs = 100, string context = "Operation");

        Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int delayMs = 100, string context = "Operation");
    }
}


using System;
using System.Collections.Generic;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for service container (DI) - enables testing and mocking
    /// </summary>
    public interface IServiceContainer
    {
        // Registration methods
        void RegisterSingleton<TService>(TService instance);
        void RegisterSingleton<TService, TImplementation>() where TImplementation : TService;
        void RegisterSingleton<TService>(Func<ServiceContainer, TService> factory);
        void RegisterTransient<TService, TImplementation>() where TImplementation : TService;
        void RegisterTransient<TService>(Func<ServiceContainer, TService> factory);

        // Resolution methods
        T Resolve<T>();
        object Resolve(Type serviceType);
        bool TryResolve<T>(out T service);

        // Query methods
        bool IsRegistered<T>();
        bool IsRegistered(Type serviceType);
        IEnumerable<Type> GetRegisteredServices();
        string GetServiceInfo(Type serviceType);

        // Management methods
        void Clear();
    }
}


using System;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for event bus - enables testing and mocking
    /// Provides pub/sub messaging for inter-component communication
    /// </summary>
    public interface IEventBus
    {
        // Typed pub/sub
        void Subscribe<TEvent>(Action<TEvent> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false);
        void Unsubscribe<TEvent>(Action<TEvent> handler);
        void Publish<TEvent>(TEvent eventData);

        // Named events (string-based, backward compatibility)
        void Subscribe(string eventName, Action<object> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false);
        void Unsubscribe(string eventName, Action<object> handler);
        void Publish(string eventName, object data = null);

        // Request/response pattern
        void RegisterRequestHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler);
        TResponse Request<TRequest, TResponse>(TRequest request);
        bool TryRequest<TRequest, TResponse>(TRequest request, out TResponse response);

        // Utilities
        void CleanupDeadSubscriptions();
        (long Published, long Delivered, int TypedSubscribers, int NamedSubscribers) GetStatistics();
        bool HasSubscribers<TEvent>();
        bool HasSubscribers(string eventName);
        void Clear();
    }
}


using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for layout engines - enables testing and mocking
    /// </summary>
    public interface ILayoutEngine
    {
        Panel Container { get; }

        void AddChild(UIElement child, LayoutParams layoutParams);
        void RemoveChild(UIElement child);
        void Clear();
        List<UIElement> GetChildren();
    }
}


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for widgets - enables testing and mocking
    /// </summary>
    public interface IWidget : INotifyPropertyChanged, IDisposable
    {
        string WidgetName { get; set; }
        string WidgetType { get; set; }
        Guid WidgetId { get; }
        bool HasFocus { get; set; }

        void Initialize();
        void Refresh();
        void OnActivated();
        void OnDeactivated();
        void OnWidgetKeyDown(KeyEventArgs e);
        void OnWidgetFocusReceived();
        void OnWidgetFocusLost();

        Dictionary<string, object> SaveState();
        void RestoreState(Dictionary<string, object> state);
    }
}


using System;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for components that respond to theme changes
    /// Widgets implementing this interface will automatically be notified when theme changes
    /// </summary>
    public interface IThemeable
    {
        /// <summary>
        /// Called when the theme changes - widget should update all theme-dependent colors and styles
        /// </summary>
        void ApplyTheme();
    }
}


using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for workspaces - enables testing and mocking
    /// </summary>
    public interface IWorkspace
    {
        string Name { get; set; }
        int Index { get; set; }
        bool IsActive { get; set; }
        ObservableCollection<WidgetBase> Widgets { get; }
        Panel Container { get; }

        void AddWidget(WidgetBase widget, LayoutParams layoutParams);
        void RemoveWidget(WidgetBase widget);
        void ClearWidgets();
        void Activate();
        void Deactivate();
        bool HandleKeyDown(System.Windows.Input.KeyEventArgs e);
    }
}


using System;
using System.Collections.ObjectModel;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for workspace manager - enables testing and mocking
    /// </summary>
    public interface IWorkspaceManager
    {
        ObservableCollection<Workspace> Workspaces { get; }
        Workspace CurrentWorkspace { get; }
        int CurrentWorkspaceIndex { get; }

        void AddWorkspace(Workspace workspace);
        void RemoveWorkspace(int index);
        void SwitchToWorkspace(int index);
        void NextWorkspace();
        void PreviousWorkspace();
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
    /// File-based log sink with rotation and async I/O
    /// Uses a background thread to write logs without blocking the UI
    /// </summary>
    public class FileLogSink : ILogSink, IDisposable
    {
        private readonly string logDirectory;
        private readonly string logFilePrefix;
        private readonly long maxFileSizeBytes;
        private readonly int maxFiles;
        private readonly System.Collections.Concurrent.BlockingCollection<string> logQueue;
        private readonly System.Threading.Thread writerThread;
        private readonly object lockObject = new object();
        private StreamWriter currentWriter;
        private string currentFilePath;
        private long currentFileSize;
        private bool disposed = false;

        // Track dropped logs
        private long droppedLogCount = 0;
        private DateTime lastDroppedLogWarning = DateTime.MinValue;

        public FileLogSink(string logDirectory, string logFilePrefix = "supertui", long maxFileSizeMB = 10, int maxFiles = 5)
        {
            this.logDirectory = logDirectory;
            this.logFilePrefix = logFilePrefix;
            this.maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;
            this.maxFiles = maxFiles;

            // Create queue for async logging (bounded to prevent memory issues)
            this.logQueue = new System.Collections.Concurrent.BlockingCollection<string>(boundedCapacity: 10000);

            Directory.CreateDirectory(logDirectory);
            OpenNewLogFile();

            // Start background writer thread
            writerThread = new System.Threading.Thread(WriterThreadProc)
            {
                IsBackground = true,
                Name = "FileLogSink Writer"
            };
            writerThread.Start();
        }

        private void OpenNewLogFile()
        {
            lock (lockObject)
            {
                if (currentWriter != null)
                {
                    currentWriter.Flush();
                    currentWriter.Close();
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                currentFilePath = Path.Combine(logDirectory, $"{logFilePrefix}_{timestamp}.log");
                // AutoFlush removed - we flush manually on a schedule
                currentWriter = new StreamWriter(currentFilePath, true, Encoding.UTF8) { AutoFlush = false };
                currentFileSize = 0;

                // Rotate old files
                RotateOldFiles();
            }
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

        /// <summary>
        /// Background thread that writes log entries to disk
        /// This prevents blocking the UI thread during log writes
        /// </summary>
        private void WriterThreadProc()
        {
            var lastFlushTime = DateTime.Now;
            const int flushIntervalMs = 1000; // Flush every second

            try
            {
                while (!disposed)
                {
                    // Try to get a log entry from queue (timeout to allow periodic flush)
                    if (logQueue.TryTake(out string line, millisecondsTimeout: 100))
                    {
                        lock (lockObject)
                        {
                            if (currentWriter != null && !disposed)
                            {
                                byte[] bytes = Encoding.UTF8.GetBytes(line);
                                currentFileSize += bytes.Length;

                                if (currentFileSize > maxFileSizeBytes)
                                {
                                    OpenNewLogFile();
                                }

                                currentWriter.Write(line);
                            }
                        }
                    }

                    // Periodic flush (every second or when queue is empty)
                    if ((DateTime.Now - lastFlushTime).TotalMilliseconds >= flushIntervalMs || logQueue.Count == 0)
                    {
                        lock (lockObject)
                        {
                            currentWriter?.Flush();
                        }
                        lastFlushTime = DateTime.Now;
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                // Expected when disposing
            }
            catch (Exception ex)
            {
                // Log to console as fallback (can't use Logger here - infinite loop!)
                Console.WriteLine($"FileLogSink writer thread error: {ex.Message}");
            }
            finally
            {
                // Drain remaining queue items on shutdown
                while (logQueue.TryTake(out string line, millisecondsTimeout: 0))
                {
                    try
                    {
                        lock (lockObject)
                        {
                            currentWriter?.Write(line);
                        }
                    }
                    catch { }
                }

                lock (lockObject)
                {
                    currentWriter?.Flush();
                    currentWriter?.Close();
                }
            }
        }

        public void Write(LogEntry entry)
        {
            if (disposed) return;

            // Format log entry
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

            // Add to queue (non-blocking)
            // If queue is full, TryAdd will fail and log is dropped (prevents memory issues)
            if (!logQueue.TryAdd(line, millisecondsTimeout: 0))
            {
                // Queue is full - log is dropped
                // Increment counter and warn periodically
                System.Threading.Interlocked.Increment(ref droppedLogCount);

                // Warn at most once per minute to avoid console spam
                if ((DateTime.Now - lastDroppedLogWarning).TotalSeconds >= 60)
                {
                    lastDroppedLogWarning = DateTime.Now;
                    Console.WriteLine($"[FileLogSink WARNING] Log queue full! {droppedLogCount} logs dropped. " +
                                    "Disk may be slow or logging rate too high. Consider increasing queue size or reducing log level.");
                }
            }
        }

        /// <summary>
        /// Get number of dropped logs since sink creation
        /// </summary>
        public long GetDroppedLogCount() => droppedLogCount;

        public void Flush()
        {
            if (disposed) return;

            // Wait for queue to drain
            while (logQueue.Count > 0)
            {
                System.Threading.Thread.Sleep(10);
            }

            lock (lockObject)
            {
                currentWriter?.Flush();
            }
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;
            logQueue.CompleteAdding();

            // Wait for writer thread to finish (with timeout)
            if (writerThread != null && writerThread.IsAlive)
            {
                writerThread.Join(timeout: TimeSpan.FromSeconds(5));
            }

            logQueue.Dispose();
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

        /// <summary>
        /// Get diagnostics information about the logger
        /// </summary>
        public Dictionary<string, object> GetDiagnostics()
        {
            var diagnostics = new Dictionary<string, object>
            {
                ["MinLevel"] = minLevel.ToString(),
                ["LogAllCategories"] = logAllCategories,
                ["EnabledCategories"] = string.Join(", ", enabledCategories),
                ["SinkCount"] = sinks.Count
            };

            lock (sinks)
            {
                var sinkInfo = new List<Dictionary<string, object>>();
                foreach (var sink in sinks)
                {
                    var info = new Dictionary<string, object>
                    {
                        ["Type"] = sink.GetType().Name
                    };

                    // Get dropped log count if it's a FileLogSink
                    if (sink is FileLogSink fileSink)
                    {
                        info["DroppedLogs"] = fileSink.GetDroppedLogCount();
                    }

                    sinkInfo.Add(info);
                }
                diagnostics["Sinks"] = sinkInfo;
            }

            return diagnostics;
        }

        /// <summary>
        /// Get total number of dropped logs across all file sinks
        /// </summary>
        public long GetTotalDroppedLogs()
        {
            long total = 0;
            lock (sinks)
            {
                foreach (var sink in sinks)
                {
                    if (sink is FileLogSink fileSink)
                    {
                        total += fileSink.GetDroppedLogCount();
                    }
                }
            }
            return total;
        }
    }
}


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


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SuperTUI.Infrastructure
{
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
                SuperTUI.Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(), "Themes");

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
            // Synchronous wrapper for backward compatibility
            SaveThemeAsync(theme, filename).GetAwaiter().GetResult();
        }

        public async Task SaveThemeAsync(Theme theme, string filename = null)
        {
            try
            {
                filename = filename ?? Path.Combine(themesDirectory, $"{theme.Name}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(filename));

                string json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filename, json, Encoding.UTF8);

                Logger.Instance.Info("Theme", $"Saved theme {theme.Name} to {filename}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Theme", $"Failed to save theme: {ex.Message}", ex);
            }
        }

        private void LoadCustomThemes()
        {
            // Synchronous wrapper for backward compatibility
            LoadCustomThemesAsync().GetAwaiter().GetResult();
        }

        private async Task LoadCustomThemesAsync()
        {
            if (!Directory.Exists(themesDirectory))
                return;

            try
            {
                foreach (var file in Directory.GetFiles(themesDirectory, "*.json"))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(file, Encoding.UTF8);
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
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SuperTUI.Infrastructure
{
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

        /// <summary>
        /// Validates that a path is safe and well-formed
        /// Does NOT validate against allowed directories - use ValidateFileAccess for that
        /// </summary>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Check for invalid characters (Windows path-specific: <>:|?*)
                if (PathSeparatorRegex.IsMatch(path))
                    return false;

                // Check for null bytes (path traversal technique)
                if (path.Contains('\0'))
                    return false;

                // Try to get full path - this will throw if path is malformed
                string fullPath = Path.GetFullPath(path);

                // Check for UNC paths if not allowed
                if (fullPath.StartsWith(@"\\") || fullPath.StartsWith("//"))
                {
                    // UNC paths like \\server\share are potentially dangerous
                    // Allow them only if explicitly enabled in config
                    return false;
                }

                // Path is syntactically valid
                return true;
            }
            catch (ArgumentException)
            {
                // Path contains invalid characters or format
                return false;
            }
            catch (SecurityException)
            {
                // Caller doesn't have required permissions
                return false;
            }
            catch (NotSupportedException)
            {
                // Path format is not supported
                return false;
            }
            catch (PathTooLongException)
            {
                // Path exceeds system maximum length
                return false;
            }
            catch
            {
                // Any other exception means invalid path
                return false;
            }
        }

        /// <summary>
        /// Validates that a path is within an allowed directory
        /// Properly handles path traversal attacks (../, ..\, etc.)
        /// </summary>
        public static bool IsWithinDirectory(string path, string allowedDirectory)
        {
            try
            {
                // Get absolute paths to prevent traversal attacks
                string fullPath = Path.GetFullPath(path);
                string fullAllowedPath = Path.GetFullPath(allowedDirectory);

                // Normalize by removing trailing separators for consistent comparison
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                fullAllowedPath = fullAllowedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Check if paths are equal (file/dir IS the allowed directory)
                if (fullPath.Equals(fullAllowedPath, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Check if file/dir is a child of the allowed directory
                // Must start with "allowedDir\" or "allowedDir/" to prevent:
                // - "/allowed" matching "/allowedButDifferent"
                // - "C:\allowed" matching "C:\allowed-different"
                return fullPath.StartsWith(fullAllowedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                       fullPath.StartsWith(fullAllowedPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Any error means we can't verify the path is safe
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

                // Normalize by removing trailing separators for consistent comparison
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                allowedDirectories.Add(fullPath);
                Logger.Instance.Debug("Security", $"Added allowed directory: {fullPath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning("Security", $"Failed to add allowed directory {directory}: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates file access against security policies
        /// Checks: path format, allowed directories, file extensions, file size
        /// Logs all denied access attempts for security auditing
        /// </summary>
        public bool ValidateFileAccess(string path, bool checkWrite = false)
        {
            if (!ConfigurationManager.Instance.Get<bool>("Security.ValidateFileAccess", true))
            {
                Logger.Instance.Debug("Security", "File access validation is DISABLED - allowing all paths");
                return true; // Validation disabled
            }

            try
            {
                // Step 1: Validate path format and syntax
                if (!ValidationHelper.IsValidPath(path))
                {
                    // Security audit log
                    Logger.Instance.Warning("Security", $"SECURITY VIOLATION: Invalid path format attempted: '{path}'");
                    return false;
                }

                // Get normalized absolute path
                string fullPath = Path.GetFullPath(path);
                string normalizedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Step 2: Check if within allowed directories
                bool inAllowedDirectory = allowedDirectories.Any(dir =>
                    ValidationHelper.IsWithinDirectory(normalizedPath, dir));

                if (!inAllowedDirectory)
                {
                    // Security audit log - include original and normalized paths
                    Logger.Instance.Warning("Security",
                        $"SECURITY VIOLATION: Path outside allowed directories\n" +
                        $"  Original path: '{path}'\n" +
                        $"  Normalized path: '{normalizedPath}'\n" +
                        $"  Allowed directories: {string.Join(", ", allowedDirectories)}");
                    return false;
                }

                // Step 3: Check file extension allowlist
                string extension = Path.GetExtension(path);
                if (!string.IsNullOrEmpty(extension))
                {
                    // Extensions should be checked case-insensitively
                    if (allowedExtensions.Count > 0 && !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        // Security audit log
                        Logger.Instance.Warning("Security",
                            $"SECURITY VIOLATION: Disallowed file extension\n" +
                            $"  Path: '{path}'\n" +
                            $"  Extension: '{extension}'\n" +
                            $"  Allowed: {string.Join(", ", allowedExtensions)}");
                        return false;
                    }
                }

                // Step 4: Check file size limits (if file exists)
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    if (fileInfo.Length > maxFileSizeBytes)
                    {
                        // Security audit log
                        Logger.Instance.Warning("Security",
                            $"SECURITY VIOLATION: File exceeds size limit\n" +
                            $"  Path: '{path}'\n" +
                            $"  Size: {fileInfo.Length:N0} bytes\n" +
                            $"  Limit: {maxFileSizeBytes:N0} bytes ({maxFileSizeBytes / 1024 / 1024} MB)");
                        return false;
                    }
                }

                // Step 5: Additional write-specific checks
                if (checkWrite)
                {
                    // Check if directory is writable (for new files)
                    if (!File.Exists(fullPath))
                    {
                        string directory = Path.GetDirectoryName(fullPath);
                        if (!Directory.Exists(directory))
                        {
                            Logger.Instance.Warning("Security",
                                $"SECURITY VIOLATION: Attempt to write to non-existent directory\n" +
                                $"  Path: '{path}'\n" +
                                $"  Directory: '{directory}'");
                            return false;
                        }
                    }
                }

                // All checks passed
                Logger.Instance.Debug("Security", $"File access validated: '{path}' (normalized: '{normalizedPath}')");
                return true;
            }
            catch (Exception ex)
            {
                // Security audit log for unexpected errors
                Logger.Instance.Error("Security",
                    $"SECURITY ERROR: File access validation failed with exception\n" +
                    $"  Path: '{path}'\n" +
                    $"  Error: {ex.Message}", ex);
                return false;
            }
        }

        public bool ValidateScriptExecution()
        {
            return ConfigurationManager.Instance.Get<bool>("Security.AllowScriptExecution", false);
        }
    }
}


using System;
using System.Threading.Tasks;
using System.Windows;

namespace SuperTUI.Infrastructure
{
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


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    /// <summary>
    /// Service lifetime options
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>Single instance for entire application lifetime</summary>
        Singleton,
        /// <summary>New instance every time</summary>
        Transient,
        /// <summary>Single instance per scope (not implemented yet)</summary>
        Scoped
    }

    /// <summary>
    /// Service descriptor for registration
    /// </summary>
    internal class ServiceDescriptor
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public object ImplementationInstance { get; set; }
        public Func<ServiceContainer, object> ImplementationFactory { get; set; }
        public ServiceLifetime Lifetime { get; set; }
    }

    /// <summary>
    /// Dependency injection container
    /// Supports singleton and transient lifetimes, with factory functions
    /// </summary>
    public class ServiceContainer : IServiceContainer
    {
        private static ServiceContainer instance;
        public static ServiceContainer Instance => instance ??= new ServiceContainer();

        private readonly Dictionary<Type, ServiceDescriptor> descriptors = new Dictionary<Type, ServiceDescriptor>();
        private readonly Dictionary<Type, object> singletonInstances = new Dictionary<Type, object>();
        private readonly object lockObject = new object();
        private readonly HashSet<Type> resolvingTypes = new HashSet<Type>();

        /// <summary>
        /// Register a singleton service with an existing instance
        /// </summary>
        public void RegisterSingleton<TService>(TService instance)
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationInstance = instance,
                    Lifetime = ServiceLifetime.Singleton
                };

                descriptors[typeof(TService)] = descriptor;
                singletonInstances[typeof(TService)] = instance;
            }
        }

        /// <summary>
        /// Register a singleton service with a type
        /// </summary>
        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationType = typeof(TImplementation),
                    Lifetime = ServiceLifetime.Singleton
                };

                descriptors[typeof(TService)] = descriptor;
            }
        }

        /// <summary>
        /// Register a singleton service with a factory function
        /// </summary>
        public void RegisterSingleton<TService>(Func<ServiceContainer, TService> factory)
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationFactory = container => factory(container),
                    Lifetime = ServiceLifetime.Singleton
                };

                descriptors[typeof(TService)] = descriptor;
            }
        }

        /// <summary>
        /// Register a transient service (new instance each time)
        /// </summary>
        public void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationType = typeof(TImplementation),
                    Lifetime = ServiceLifetime.Transient
                };

                descriptors[typeof(TService)] = descriptor;
            }
        }

        /// <summary>
        /// Register a transient service with a factory function
        /// </summary>
        public void RegisterTransient<TService>(Func<ServiceContainer, TService> factory)
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationFactory = container => factory(container),
                    Lifetime = ServiceLifetime.Transient
                };

                descriptors[typeof(TService)] = descriptor;
            }
        }

        /// <summary>
        /// Resolve a service instance
        /// </summary>
        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        /// <summary>
        /// Resolve a service instance by type
        /// </summary>
        public object Resolve(Type serviceType)
        {
            lock (lockObject)
            {
                if (!descriptors.TryGetValue(serviceType, out var descriptor))
                {
                    // Better error message with suggestions
                    var registered = string.Join(", ", descriptors.Keys.Select(t => t.Name));
                    throw new InvalidOperationException(
                        $"Service of type '{serviceType.Name}' is not registered.\n" +
                        $"Registered services: {(descriptors.Count > 0 ? registered : "None")}\n" +
                        $"Did you forget to call ServiceRegistration.ConfigureServices()?");
                }

                // Circular dependency detection
                if (resolvingTypes.Contains(serviceType))
                {
                    var chain = string.Join(" -> ", resolvingTypes.Select(t => t.Name)) + " -> " + serviceType.Name;
                    throw new InvalidOperationException(
                        $"Circular dependency detected: {chain}\n" +
                        $"This usually means two services depend on each other. Consider using a factory or breaking the dependency.");
                }

                resolvingTypes.Add(serviceType);
                try
                {
                    // Singleton - return existing or create and cache
                    if (descriptor.Lifetime == ServiceLifetime.Singleton)
                    {
                        // Check if already instantiated
                        if (singletonInstances.TryGetValue(serviceType, out var existingInstance))
                        {
                            return existingInstance;
                        }

                        // Create new instance
                        var newInstance = CreateInstance(descriptor);
                        singletonInstances[serviceType] = newInstance;
                        return newInstance;
                    }

                    // Transient - always create new instance
                    if (descriptor.Lifetime == ServiceLifetime.Transient)
                    {
                        return CreateInstance(descriptor);
                    }

                    throw new NotImplementedException($"Service lifetime {descriptor.Lifetime} is not yet implemented");
                }
                finally
                {
                    resolvingTypes.Remove(serviceType);
                }
            }
        }

        /// <summary>
        /// Try to resolve a service, returns false if not found
        /// </summary>
        public bool TryResolve<T>(out T service)
        {
            lock (lockObject)
            {
                if (!descriptors.ContainsKey(typeof(T)))
                {
                    service = default(T);
                    return false;
                }

                try
                {
                    service = Resolve<T>();
                    return true;
                }
                catch
                {
                    service = default(T);
                    return false;
                }
            }
        }

        /// <summary>
        /// Check if a service is registered
        /// </summary>
        public bool IsRegistered<T>()
        {
            return descriptors.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Check if a service is registered by type
        /// </summary>
        public bool IsRegistered(Type serviceType)
        {
            return descriptors.ContainsKey(serviceType);
        }

        /// <summary>
        /// Get all registered service types (for debugging/diagnostics)
        /// </summary>
        public IEnumerable<Type> GetRegisteredServices()
        {
            return descriptors.Keys.ToList();
        }

        /// <summary>
        /// Get service registration info (for debugging/diagnostics)
        /// </summary>
        public string GetServiceInfo(Type serviceType)
        {
            if (!descriptors.TryGetValue(serviceType, out var descriptor))
            {
                return $"{serviceType.Name}: Not registered";
            }

            string impl = descriptor.ImplementationType?.Name ??
                         descriptor.ImplementationInstance?.GetType().Name ??
                         "Factory";

            string lifetime = descriptor.Lifetime.ToString();
            bool instantiated = descriptor.Lifetime == ServiceLifetime.Singleton &&
                               singletonInstances.ContainsKey(serviceType);

            return $"{serviceType.Name} -> {impl} ({lifetime}, {(instantiated ? "instantiated" : "not yet instantiated")})";
        }

        /// <summary>
        /// Clear all registrations
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                descriptors.Clear();
                singletonInstances.Clear();
            }
        }

        private object CreateInstance(ServiceDescriptor descriptor)
        {
            // If instance is already provided
            if (descriptor.ImplementationInstance != null)
            {
                return descriptor.ImplementationInstance;
            }

            // If factory is provided
            if (descriptor.ImplementationFactory != null)
            {
                return descriptor.ImplementationFactory(this);
            }

            // Create using reflection (basic constructor injection)
            if (descriptor.ImplementationType != null)
            {
                return CreateInstanceWithDI(descriptor.ImplementationType);
            }

            throw new InvalidOperationException($"Cannot create instance for service {descriptor.ServiceType.Name}");
        }

        private object CreateInstanceWithDI(Type implementationType)
        {
            // Get constructors ordered by parameter count (prefer ones with most params for DI)
            var constructors = implementationType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .ToList();

            if (constructors.Count == 0)
            {
                throw new InvalidOperationException($"No public constructors found for {implementationType.Name}");
            }

            // Try each constructor until one works
            foreach (var constructor in constructors)
            {
                try
                {
                    var parameters = constructor.GetParameters();
                    var parameterInstances = new object[parameters.Length];

                    // Resolve each parameter
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;

                        if (descriptors.ContainsKey(paramType))
                        {
                            parameterInstances[i] = Resolve(paramType);
                        }
                        else
                        {
                            // Parameter not registered, try next constructor
                            throw new InvalidOperationException($"Cannot resolve parameter {paramType.Name}");
                        }
                    }

                    // All parameters resolved, create instance
                    return constructor.Invoke(parameterInstances);
                }
                catch
                {
                    // Try next constructor
                    continue;
                }
            }

            // No constructor worked, throw error
            throw new InvalidOperationException($"Cannot create instance of {implementationType.Name}. All constructors failed.");
        }

        // Legacy methods for backward compatibility
        [Obsolete("Use RegisterSingleton<T>(T instance) instead")]
        public void Register<T>(T service)
        {
            RegisterSingleton(service);
        }

        [Obsolete("Use Resolve<T>() instead")]
        public T Get<T>()
        {
            return TryResolve<T>(out var service) ? service : default(T);
        }

        [Obsolete("Use TryResolve<T>(out T service) instead")]
        public bool TryGet<T>(out T service)
        {
            return TryResolve(out service);
        }
    }

    // ============================================================================
    // EVENT BUS
    // ============================================================================

    /// <summary>
    /// Simple event bus for inter-widget communication
    /// Allows widgets to communicate without direct references
}


using System;

namespace SuperTUI.Core.Events
{
    // ============================================================================
    // WORKSPACE EVENTS
    // ============================================================================

    public class WorkspaceChangedEvent
    {
        public string WorkspaceName { get; set; }
        public int WorkspaceIndex { get; set; }
        public int WidgetCount { get; set; }
    }

    public class WorkspaceCreatedEvent
    {
        public string WorkspaceName { get; set; }
        public int WorkspaceIndex { get; set; }
    }

    public class WorkspaceRemovedEvent
    {
        public string WorkspaceName { get; set; }
        public int WorkspaceIndex { get; set; }
    }

    // ============================================================================
    // WIDGET EVENTS
    // ============================================================================

    public class WidgetActivatedEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
        public string WorkspaceName { get; set; }
    }

    public class WidgetDeactivatedEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
    }

    public class WidgetFocusReceivedEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
    }

    public class WidgetFocusLostEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
    }

    public class WidgetRefreshedEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // ============================================================================
    // THEME EVENTS
    // ============================================================================

    public class ThemeChangedEvent
    {
        public string OldThemeName { get; set; }
        public string NewThemeName { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class ThemeLoadedEvent
    {
        public string ThemeName { get; set; }
        public string FilePath { get; set; }
    }

    // ============================================================================
    // FILE SYSTEM EVENTS
    // ============================================================================

    public class DirectoryChangedEvent
    {
        public string OldPath { get; set; }
        public string NewPath { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class FileSelectedEvent
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public DateTime SelectedAt { get; set; }
    }

    public class FileCreatedEvent
    {
        public string FilePath { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class FileDeletedEvent
    {
        public string FilePath { get; set; }
        public DateTime DeletedAt { get; set; }
    }

    // ============================================================================
    // GIT EVENTS
    // ============================================================================

    public class BranchChangedEvent
    {
        public string Repository { get; set; }
        public string OldBranch { get; set; }
        public string NewBranch { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class CommitCreatedEvent
    {
        public string Repository { get; set; }
        public string CommitHash { get; set; }
        public string Message { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RepositoryStatusChangedEvent
    {
        public string Repository { get; set; }
        public int FilesModified { get; set; }
        public int FilesAdded { get; set; }
        public int FilesDeleted { get; set; }
        public bool HasUncommittedChanges { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    // ============================================================================
    // TERMINAL EVENTS
    // ============================================================================

    public class CommandExecutedEvent
    {
        public string Command { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public DateTime ExecutedAt { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class TerminalOutputEvent
    {
        public string Output { get; set; }
        public bool IsError { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class WorkingDirectoryChangedEvent
    {
        public string OldDirectory { get; set; }
        public string NewDirectory { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    // ============================================================================
    // SYSTEM EVENTS
    // ============================================================================

    public class SystemResourcesChangedEvent
    {
        public double CpuUsagePercent { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long MemoryTotalBytes { get; set; }
        public long DiskFreeBytes { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class NetworkActivityEvent
    {
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // ============================================================================
    // TASK/TODO EVENTS
    // ============================================================================

    public class TaskCreatedEvent
    {
        public Guid TaskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TaskCompletedEvent
    {
        public Guid TaskId { get; set; }
        public string Title { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    public class TaskDeletedEvent
    {
        public Guid TaskId { get; set; }
        public DateTime DeletedAt { get; set; }
    }

    public class TaskStatusChangedEvent
    {
        public Guid TaskId { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    // ============================================================================
    // NOTIFICATION EVENTS
    // ============================================================================

    public class NotificationEvent
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationLevel Level { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan? Duration { get; set; } // Auto-dismiss duration
    }

    public enum NotificationLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    // ============================================================================
    // COMMAND PALETTE EVENTS
    // ============================================================================

    public class CommandPaletteOpenedEvent
    {
        public DateTime OpenedAt { get; set; }
    }

    public class CommandPaletteClosedEvent
    {
        public DateTime ClosedAt { get; set; }
    }

    public class CommandExecutedFromPaletteEvent
    {
        public string CommandName { get; set; }
        public string CommandCategory { get; set; }
        public DateTime ExecutedAt { get; set; }
    }

    // ============================================================================
    // STATE EVENTS
    // ============================================================================

    public class StateSavedEvent
    {
        public string FilePath { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime SavedAt { get; set; }
    }

    public class StateLoadedEvent
    {
        public string FilePath { get; set; }
        public string Version { get; set; }
        public DateTime LoadedAt { get; set; }
    }

    public class UndoPerformedEvent
    {
        public DateTime PerformedAt { get; set; }
    }

    public class RedoPerformedEvent
    {
        public DateTime PerformedAt { get; set; }
    }

    // ============================================================================
    // CONFIGURATION EVENTS
    // ============================================================================

    public class ConfigurationChangedEvent
    {
        public string Key { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class ConfigurationSavedEvent
    {
        public string FilePath { get; set; }
        public int SettingsCount { get; set; }
        public DateTime SavedAt { get; set; }
    }

    // ============================================================================
    // REQUEST/RESPONSE PATTERNS
    // ============================================================================

    // Request current system stats
    public class GetSystemStatsRequest { }
    public class GetSystemStatsResponse
    {
        public double CpuPercent { get; set; }
        public long MemoryUsed { get; set; }
        public long MemoryTotal { get; set; }
    }

    // Request git status
    public class GetGitStatusRequest
    {
        public string RepositoryPath { get; set; }
    }
    public class GetGitStatusResponse
    {
        public string Branch { get; set; }
        public int FilesChanged { get; set; }
        public bool HasChanges { get; set; }
    }

    // Request task list
    public class GetTaskListRequest
    {
        public bool IncludeCompleted { get; set; }
    }
    public class GetTaskListResponse
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
    }
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    /// <summary>
    /// Subscription priority levels
    /// </summary>
    public enum SubscriptionPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Internal subscription holder with weak reference support
    /// </summary>
    internal class Subscription
    {
        public Type EventType { get; set; }
        public WeakReference HandlerReference { get; set; }
        public Delegate StrongHandler { get; set; }
        public SubscriptionPriority Priority { get; set; }
        public bool IsWeak { get; set; }

        public bool IsAlive => !IsWeak || HandlerReference?.IsAlive == true;

        public void Invoke(object eventData)
        {
            Delegate handler = IsWeak
                ? HandlerReference?.Target as Delegate
                : StrongHandler;

            handler?.DynamicInvoke(eventData);
        }
    }

    /// <summary>
    /// Enhanced event bus for inter-widget communication
    /// Supports strong typing, weak references, priorities, and filtering
    /// </summary>
    public class EventBus : IEventBus
    {
        private static EventBus instance;
        public static EventBus Instance => instance ??= new EventBus();

        private readonly Dictionary<Type, List<Subscription>> typedSubscriptions = new Dictionary<Type, List<Subscription>>();
        private readonly Dictionary<string, List<Subscription>> namedSubscriptions = new Dictionary<string, List<Subscription>>();
        private readonly object lockObject = new object();

        // Statistics
        private long totalPublished = 0;
        private long totalDelivered = 0;

        #region Typed Pub/Sub

        /// <summary>
        /// Subscribe to a strongly-typed event
        /// </summary>
        /// <param name="handler">Event handler callback</param>
        /// <param name="priority">Subscription priority (higher priority handlers are called first)</param>
        /// <param name="useWeakReference">Use weak reference (default false). WARNING: Weak references to lambdas/closures will be GC'd immediately!</param>
        /// <remarks>
        /// IMPORTANT: useWeakReference defaults to FALSE because:
        /// - Most subscriptions use lambdas or closures
        /// - Weak references to lambdas get garbage collected immediately
        /// - This would cause event handlers to mysteriously stop working
        ///
        /// Only use weak references when:
        /// - Handler is a method on a long-lived object
        /// - You explicitly maintain a strong reference to the delegate elsewhere
        ///
        /// For most use cases, use strong references and explicitly Unsubscribe when done.
        /// </remarks>
        public void Subscribe<TEvent>(Action<TEvent> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false)
        {
            lock (lockObject)
            {
                var eventType = typeof(TEvent);

                if (!typedSubscriptions.ContainsKey(eventType))
                    typedSubscriptions[eventType] = new List<Subscription>();

                var subscription = new Subscription
                {
                    EventType = eventType,
                    Priority = priority,
                    IsWeak = useWeakReference
                };

                if (useWeakReference)
                {
                    subscription.HandlerReference = new WeakReference(handler);
                }
                else
                {
                    subscription.StrongHandler = handler;
                }

                typedSubscriptions[eventType].Add(subscription);

                // Sort by priority (highest first)
                typedSubscriptions[eventType] = typedSubscriptions[eventType]
                    .OrderByDescending(s => s.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Unsubscribe from a strongly-typed event
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            lock (lockObject)
            {
                var eventType = typeof(TEvent);

                if (typedSubscriptions.ContainsKey(eventType))
                {
                    typedSubscriptions[eventType].RemoveAll(s =>
                    {
                        if (s.IsWeak)
                        {
                            var target = s.HandlerReference?.Target as Action<TEvent>;
                            return target == handler || !s.HandlerReference.IsAlive;
                        }
                        else
                        {
                            return s.StrongHandler as Action<TEvent> == handler;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Publish a strongly-typed event
        /// </summary>
        public void Publish<TEvent>(TEvent eventData)
        {
            lock (lockObject)
            {
                totalPublished++;
                var eventType = typeof(TEvent);

                if (!typedSubscriptions.ContainsKey(eventType))
                    return;

                var subs = typedSubscriptions[eventType].ToList();
                var deadSubscriptions = new List<Subscription>();

                foreach (var subscription in subs)
                {
                    if (!subscription.IsAlive)
                    {
                        deadSubscriptions.Add(subscription);
                        continue;
                    }

                    try
                    {
                        subscription.Invoke(eventData);
                        totalDelivered++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance?.Error("EventBus",
                            $"Error delivering event {eventType.Name}: {ex.Message}", ex);
                    }
                }

                // Clean up dead subscriptions
                if (deadSubscriptions.Count > 0)
                {
                    typedSubscriptions[eventType].RemoveAll(s => deadSubscriptions.Contains(s));
                }
            }
        }

        #endregion

        #region Named Events (String-based, backward compatibility)

        /// <summary>
        /// Subscribe to a named event (string-based)
        /// </summary>
        /// <param name="eventName">Name of the event</param>
        /// <param name="handler">Event handler callback</param>
        /// <param name="priority">Subscription priority</param>
        /// <param name="useWeakReference">Use weak reference (default false). See Subscribe&lt;TEvent&gt; remarks for details.</param>
        public void Subscribe(string eventName, Action<object> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false)
        {
            lock (lockObject)
            {
                if (!namedSubscriptions.ContainsKey(eventName))
                    namedSubscriptions[eventName] = new List<Subscription>();

                var subscription = new Subscription
                {
                    Priority = priority,
                    IsWeak = useWeakReference
                };

                if (useWeakReference)
                {
                    subscription.HandlerReference = new WeakReference(handler);
                }
                else
                {
                    subscription.StrongHandler = handler;
                }

                namedSubscriptions[eventName].Add(subscription);

                // Sort by priority
                namedSubscriptions[eventName] = namedSubscriptions[eventName]
                    .OrderByDescending(s => s.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Unsubscribe from a named event
        /// </summary>
        public void Unsubscribe(string eventName, Action<object> handler)
        {
            lock (lockObject)
            {
                if (namedSubscriptions.ContainsKey(eventName))
                {
                    namedSubscriptions[eventName].RemoveAll(s =>
                    {
                        if (s.IsWeak)
                        {
                            var target = s.HandlerReference?.Target as Action<object>;
                            return target == handler || !s.HandlerReference.IsAlive;
                        }
                        else
                        {
                            return s.StrongHandler as Action<object> == handler;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Publish a named event
        /// </summary>
        public void Publish(string eventName, object data = null)
        {
            lock (lockObject)
            {
                totalPublished++;

                if (!namedSubscriptions.ContainsKey(eventName))
                    return;

                var subs = namedSubscriptions[eventName].ToList();
                var deadSubscriptions = new List<Subscription>();

                foreach (var subscription in subs)
                {
                    if (!subscription.IsAlive)
                    {
                        deadSubscriptions.Add(subscription);
                        continue;
                    }

                    try
                    {
                        subscription.Invoke(data);
                        totalDelivered++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance?.Error("EventBus",
                            $"Error delivering named event '{eventName}': {ex.Message}", ex);
                    }
                }

                // Clean up dead subscriptions
                if (deadSubscriptions.Count > 0)
                {
                    namedSubscriptions[eventName].RemoveAll(s => deadSubscriptions.Contains(s));
                }
            }
        }

        #endregion

        #region Request/Response Pattern

        private readonly Dictionary<Type, Delegate> requestHandlers = new Dictionary<Type, Delegate>();

        /// <summary>
        /// Register a request handler
        /// </summary>
        public void RegisterRequestHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler)
        {
            lock (lockObject)
            {
                requestHandlers[typeof(TRequest)] = handler;
            }
        }

        /// <summary>
        /// Send a request and get a response
        /// </summary>
        public TResponse Request<TRequest, TResponse>(TRequest request)
        {
            lock (lockObject)
            {
                var requestType = typeof(TRequest);

                if (!requestHandlers.ContainsKey(requestType))
                {
                    throw new InvalidOperationException($"No handler registered for request type {requestType.Name}");
                }

                var handler = requestHandlers[requestType] as Func<TRequest, TResponse>;
                return handler(request);
            }
        }

        /// <summary>
        /// Try to send a request, returns false if no handler
        /// </summary>
        public bool TryRequest<TRequest, TResponse>(TRequest request, out TResponse response)
        {
            lock (lockObject)
            {
                var requestType = typeof(TRequest);

                if (!requestHandlers.ContainsKey(requestType))
                {
                    response = default(TResponse);
                    return false;
                }

                try
                {
                    var handler = requestHandlers[requestType] as Func<TRequest, TResponse>;
                    response = handler(request);
                    return true;
                }
                catch
                {
                    response = default(TResponse);
                    return false;
                }
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Remove all dead subscriptions (garbage collected handlers)
        /// </summary>
        public void CleanupDeadSubscriptions()
        {
            lock (lockObject)
            {
                // Clean typed subscriptions
                foreach (var kvp in typedSubscriptions.ToList())
                {
                    typedSubscriptions[kvp.Key].RemoveAll(s => !s.IsAlive);
                }

                // Clean named subscriptions
                foreach (var kvp in namedSubscriptions.ToList())
                {
                    namedSubscriptions[kvp.Key].RemoveAll(s => !s.IsAlive);
                }
            }
        }

        /// <summary>
        /// Get statistics about event bus usage
        /// </summary>
        public (long Published, long Delivered, int TypedSubscribers, int NamedSubscribers) GetStatistics()
        {
            lock (lockObject)
            {
                int typedCount = typedSubscriptions.Sum(kvp => kvp.Value.Count);
                int namedCount = namedSubscriptions.Sum(kvp => kvp.Value.Count);

                return (totalPublished, totalDelivered, typedCount, namedCount);
            }
        }

        /// <summary>
        /// Check if any subscribers exist for an event type
        /// </summary>
        public bool HasSubscribers<TEvent>()
        {
            lock (lockObject)
            {
                var eventType = typeof(TEvent);
                return typedSubscriptions.ContainsKey(eventType) &&
                       typedSubscriptions[eventType].Any(s => s.IsAlive);
            }
        }

        /// <summary>
        /// Check if any subscribers exist for a named event
        /// </summary>
        public bool HasSubscribers(string eventName)
        {
            lock (lockObject)
            {
                return namedSubscriptions.ContainsKey(eventName) &&
                       namedSubscriptions[eventName].Any(s => s.IsAlive);
            }
        }

        /// <summary>
        /// Clear all subscriptions
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                typedSubscriptions.Clear();
                namedSubscriptions.Clear();
                requestHandlers.Clear();
                totalPublished = 0;
                totalDelivered = 0;
            }
        }

        #endregion
    }

    // ============================================================================
    // KEYBOARD SHORTCUT MANAGER
    // ============================================================================
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    public class KeyboardShortcut
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public ModifierKeys Modifier => Modifiers; // Alias for consistency
        public Action Action { get; set; }
        public string Description { get; set; }

        public bool Matches(Key key, ModifierKeys modifiers)
        {
            return Key == key && Modifiers == modifiers;
        }
    }

    public class ShortcutManager
    {
        private static readonly Lazy<ShortcutManager> instance =
            new Lazy<ShortcutManager>(() => new ShortcutManager());
        public static ShortcutManager Instance => instance.Value;

        private List<KeyboardShortcut> globalShortcuts = new List<KeyboardShortcut>();
        private Dictionary<string, List<KeyboardShortcut>> workspaceShortcuts = new Dictionary<string, List<KeyboardShortcut>>();

        public void RegisterGlobal(Key key, ModifierKeys modifiers, Action action, string description = "")
        {
            globalShortcuts.Add(new KeyboardShortcut
            {
                Key = key,
                Modifiers = modifiers,
                Action = action,
                Description = description
            });
        }

        public void RegisterForWorkspace(string workspaceName, Key key, ModifierKeys modifiers, Action action, string description = "")
        {
            if (!workspaceShortcuts.ContainsKey(workspaceName))
                workspaceShortcuts[workspaceName] = new List<KeyboardShortcut>();

            workspaceShortcuts[workspaceName].Add(new KeyboardShortcut
            {
                Key = key,
                Modifiers = modifiers,
                Action = action,
                Description = description
            });
        }

        public bool HandleKeyDown(Key key, ModifierKeys modifiers, string currentWorkspace)
        {
            // Try workspace-specific shortcuts first
            if (!string.IsNullOrEmpty(currentWorkspace) && workspaceShortcuts.ContainsKey(currentWorkspace))
            {
                foreach (var shortcut in workspaceShortcuts[currentWorkspace])
                {
                    if (shortcut.Matches(key, modifiers))
                    {
                        shortcut.Action?.Invoke();
                        return true;
                    }
                }
            }

            // Try global shortcuts
            foreach (var shortcut in globalShortcuts)
            {
                if (shortcut.Matches(key, modifiers))
                {
                    shortcut.Action?.Invoke();
                    return true;
                }
            }

            return false;
        }

        public List<KeyboardShortcut> GetAllShortcuts()
        {
            var all = new List<KeyboardShortcut>(globalShortcuts);
            foreach (var kvp in workspaceShortcuts)
            {
                all.AddRange(kvp.Value);
            }
            return all;
        }

        public bool HandleKeyDown(Key key, ModifierKeys modifiers)
        {
            return HandleKeyDown(key, modifiers, null);
        }
    }
}


using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SuperTUI.Core
{
    /// <summary>
    /// Size constraint types
    /// </summary>
    public enum SizeMode
    {
        Auto,           // Size to content
        Star,           // Proportional (e.g., 1*, 2*)
        Pixels,         // Fixed pixels
        Percentage      // Percentage of available space
    }

    /// <summary>
    /// Layout parameters for positioning widgets/screens
    /// </summary>
    public class LayoutParams
    {
        // Grid layout
        public int? Row { get; set; }
        public int? Column { get; set; }
        public int? RowSpan { get; set; } = 1;
        public int? ColumnSpan { get; set; } = 1;

        // Dock layout
        public Dock? Dock { get; set; }

        // Size constraints
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? MinWidth { get; set; }
        public double? MinHeight { get; set; }
        public double? MaxWidth { get; set; }
        public double? MaxHeight { get; set; }

        // Proportional sizing (for Grid)
        public double StarWidth { get; set; } = 1.0;  // e.g., 2.0 = 2*, 0.5 = 0.5*
        public double StarHeight { get; set; } = 1.0;

        // Margin
        public Thickness? Margin { get; set; }

        // Alignment
        public HorizontalAlignment? HorizontalAlignment { get; set; }
        public VerticalAlignment? VerticalAlignment { get; set; }
    }

    /// <summary>
    /// Base class for layout engines
    /// </summary>
    public abstract class LayoutEngine
    {
        public Panel Container { get; protected set; }
        protected List<UIElement> children = new List<UIElement>();
        protected Dictionary<UIElement, LayoutParams> layoutParams = new Dictionary<UIElement, LayoutParams>();

        public abstract void AddChild(UIElement child, LayoutParams layoutParams);
        public abstract void RemoveChild(UIElement child);
        public abstract void Clear();

        public virtual List<UIElement> GetChildren() => new List<UIElement>(children);

        protected void ApplyCommonParams(UIElement child, LayoutParams lp)
        {
            if (child is FrameworkElement fe)
            {
                // Size
                if (lp.Width.HasValue)
                    fe.Width = lp.Width.Value;

                if (lp.Height.HasValue)
                    fe.Height = lp.Height.Value;

                // Min/Max size
                if (lp.MinWidth.HasValue)
                    fe.MinWidth = lp.MinWidth.Value;

                if (lp.MinHeight.HasValue)
                    fe.MinHeight = lp.MinHeight.Value;

                if (lp.MaxWidth.HasValue)
                    fe.MaxWidth = lp.MaxWidth.Value;

                if (lp.MaxHeight.HasValue)
                    fe.MaxHeight = lp.MaxHeight.Value;

                // Margin
                if (lp.Margin.HasValue)
                    fe.Margin = lp.Margin.Value;
                else
                    fe.Margin = new Thickness(5); // Default margin

                // Alignment
                if (lp.HorizontalAlignment.HasValue)
                    fe.HorizontalAlignment = lp.HorizontalAlignment.Value;
                else
                    fe.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;

                if (lp.VerticalAlignment.HasValue)
                    fe.VerticalAlignment = lp.VerticalAlignment.Value;
                else
                    fe.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    public class GridLayoutEngine : LayoutEngine
    {
        private Grid grid;
        private bool enableSplitters;

        public GridLayoutEngine(int rows, int columns, bool enableSplitters = false)
        {
            grid = new Grid();
            Container = grid;
            this.enableSplitters = enableSplitters;

            // Create row definitions with star sizing
            for (int i = 0; i < rows; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Star),
                    MinHeight = 50 // Minimum row height
                });
            }

            // Create column definitions with star sizing
            for (int i = 0; i < columns; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                    MinWidth = 100 // Minimum column width
                });
            }

            // Add splitters if enabled
            if (enableSplitters)
            {
                AddGridSplitters(rows, columns);
            }
        }

        private void AddGridSplitters(int rows, int columns)
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            var splitterBrush = new SolidColorBrush(theme.Border);

            // Add vertical splitters between columns
            for (int col = 0; col < columns - 1; col++)
            {
                var splitter = new GridSplitter
                {
                    Width = 5,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Background = splitterBrush,
                    ResizeDirection = GridResizeDirection.Columns
                };

                Grid.SetColumn(splitter, col);
                Grid.SetRowSpan(splitter, rows);
                grid.Children.Add(splitter);
            }

            // Add horizontal splitters between rows
            for (int row = 0; row < rows - 1; row++)
            {
                var splitter = new GridSplitter
                {
                    Height = 5,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    Background = splitterBrush,
                    ResizeDirection = GridResizeDirection.Rows
                };

                Grid.SetRow(splitter, row);
                Grid.SetColumnSpan(splitter, columns);
                grid.Children.Add(splitter);
            }
        }

        public void SetColumnWidth(int column, GridLength width)
        {
            if (column >= 0 && column < grid.ColumnDefinitions.Count)
            {
                grid.ColumnDefinitions[column].Width = width;
            }
        }

        public void SetRowHeight(int row, GridLength height)
        {
            if (row >= 0 && row < grid.RowDefinitions.Count)
            {
                grid.RowDefinitions[row].Height = height;
            }
        }

        public override void AddChild(UIElement child, LayoutParams lp)
        {
            // Validate row/column references
            if (lp.Row.HasValue)
            {
                if (lp.Row.Value < 0 || lp.Row.Value >= grid.RowDefinitions.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(lp.Row),
                        $"Row {lp.Row.Value} is invalid. Grid has {grid.RowDefinitions.Count} rows (0-{grid.RowDefinitions.Count - 1})");
                }
                Grid.SetRow(child, lp.Row.Value);
            }

            if (lp.Column.HasValue)
            {
                if (lp.Column.Value < 0 || lp.Column.Value >= grid.ColumnDefinitions.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(lp.Column),
                        $"Column {lp.Column.Value} is invalid. Grid has {grid.ColumnDefinitions.Count} columns (0-{grid.ColumnDefinitions.Count - 1})");
                }
                Grid.SetColumn(child, lp.Column.Value);
            }

            // Validate span doesn't exceed grid bounds
            if (lp.RowSpan.HasValue)
            {
                int row = lp.Row ?? 0;
                if (row + lp.RowSpan.Value > grid.RowDefinitions.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(lp.RowSpan),
                        $"RowSpan {lp.RowSpan.Value} starting at row {row} exceeds grid bounds ({grid.RowDefinitions.Count} rows)");
                }
                Grid.SetRowSpan(child, lp.RowSpan.Value);
            }

            if (lp.ColumnSpan.HasValue)
            {
                int col = lp.Column ?? 0;
                if (col + lp.ColumnSpan.Value > grid.ColumnDefinitions.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(lp.ColumnSpan),
                        $"ColumnSpan {lp.ColumnSpan.Value} starting at column {col} exceeds grid bounds ({grid.ColumnDefinitions.Count} columns)");
                }
                Grid.SetColumnSpan(child, lp.ColumnSpan.Value);
            }

            // Apply star sizing to grid definitions if specified
            if (lp.Row.HasValue && lp.StarHeight != 1.0)
            {
                SetRowHeight(lp.Row.Value, new GridLength(lp.StarHeight, GridUnitType.Star));
            }

            if (lp.Column.HasValue && lp.StarWidth != 1.0)
            {
                SetColumnWidth(lp.Column.Value, new GridLength(lp.StarWidth, GridUnitType.Star));
            }

            ApplyCommonParams(child, lp);
            children.Add(child);
            layoutParams[child] = lp;
            grid.Children.Add(child);
        }

        public override void RemoveChild(UIElement child)
        {
            grid.Children.Remove(child);
            children.Remove(child);
            layoutParams.Remove(child);
        }

        public override void Clear()
        {
            grid.Children.Clear();
            children.Clear();
            layoutParams.Clear();
        }
    }
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    public class DockLayoutEngine : LayoutEngine
    {
        private DockPanel dockPanel;

        public DockLayoutEngine()
        {
            dockPanel = new DockPanel();
            dockPanel.LastChildFill = true;
            Container = dockPanel;
        }

        public override void AddChild(UIElement child, LayoutParams lp)
        {
            if (lp.Dock.HasValue)
                DockPanel.SetDock(child, lp.Dock.Value);

            ApplyCommonParams(child, lp);
            children.Add(child);
            layoutParams[child] = lp;
            dockPanel.Children.Add(child);
        }

        public override void RemoveChild(UIElement child)
        {
            dockPanel.Children.Remove(child);
            children.Remove(child);
            layoutParams.Remove(child);
        }

        public override void Clear()
        {
            dockPanel.Children.Clear();
            children.Clear();
            layoutParams.Clear();
        }
    }

    /// <summary>
    /// Stack-based layout engine
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    public class StackLayoutEngine : LayoutEngine
    {
        private StackPanel stackPanel;

        public StackLayoutEngine(Orientation orientation = Orientation.Vertical)
        {
            stackPanel = new StackPanel();
            stackPanel.Orientation = orientation;
            Container = stackPanel;
        }

        public override void AddChild(UIElement child, LayoutParams lp)
        {
            ApplyCommonParams(child, lp);
            children.Add(child);
            layoutParams[child] = lp;
            stackPanel.Children.Add(child);
        }

        public override void RemoveChild(UIElement child)
        {
            stackPanel.Children.Remove(child);
            children.Remove(child);
            layoutParams.Remove(child);
        }

        public override void Clear()
        {
            stackPanel.Children.Clear();
            children.Clear();
            layoutParams.Clear();
        }
    }

    // ============================================================================
    // WORKSPACE SYSTEM
    // ============================================================================

    /// <summary>
    /// Represents a workspace (desktop) containing widgets and screens
    /// Each workspace maintains independent state for all its widgets/screens
}


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    /// <summary>
    /// Base class for all widgets - small, focused, self-contained components
    /// Each widget maintains its own state independently
    /// </summary>
    public abstract class WidgetBase : UserControl, IWidget
    {
        public string WidgetName { get; set; }
        public string WidgetType { get; set; }
        public Guid WidgetId { get; private set; } = Guid.NewGuid();

        // Focus management
        private bool hasFocus;
        public bool HasFocus
        {
            get => hasFocus;
            set
            {
                if (hasFocus != value)
                {
                    hasFocus = value;
                    OnPropertyChanged(nameof(HasFocus));
                    UpdateFocusVisual();

                    if (value)
                        OnWidgetFocusReceived();
                    else
                        OnWidgetFocusLost();
                }
            }
        }

        // Container wrapper for focus visual
        private Border containerBorder;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public WidgetBase()
        {
            // Wrap widget content in a border for focus indication
            this.Loaded += (s, e) => WrapInFocusBorder();
            this.Focusable = true;
            this.GotFocus += (s, e) => HasFocus = true;
            this.LostFocus += (s, e) => HasFocus = false;

            // Subscribe to theme changes if widget implements IThemeable
            if (this is IThemeable)
            {
                ThemeManager.Instance.ThemeChanged += OnThemeChanged;
            }
        }

        private void WrapInFocusBorder()
        {
            if (this.Content != null && containerBorder == null)
            {
                var originalContent = this.Content;
                this.Content = null;

                containerBorder = new Border
                {
                    Child = originalContent as UIElement,
                    BorderThickness = new Thickness(2),
                    BorderBrush = Brushes.Transparent
                };

                this.Content = containerBorder;
                UpdateFocusVisual();
            }
        }

        private void UpdateFocusVisual()
        {
            if (containerBorder != null)
            {
                var theme = ThemeManager.Instance.CurrentTheme;
                containerBorder.BorderBrush = HasFocus
                    ? new SolidColorBrush(theme.Focus)
                    : Brushes.Transparent;
            }
        }

        /// <summary>
        /// Handler for theme changes - calls ApplyTheme() if widget implements IThemeable
        /// </summary>
        private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (this is IThemeable themeable)
            {
                themeable.ApplyTheme();
            }

            // Always update focus visual (uses theme colors)
            UpdateFocusVisual();
        }

        /// <summary>
        /// Initialize widget - called once when widget is created
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Refresh widget data - can be called manually or on timer
        /// </summary>
        public virtual void Refresh() { }

        /// <summary>
        /// Called when widget becomes visible (workspace switched to)
        /// </summary>
        public virtual void OnActivated() { }

        /// <summary>
        /// Called when widget becomes hidden (workspace switched away)
        /// Widget state is preserved, just hidden
        /// </summary>
        public virtual void OnDeactivated() { }

        /// <summary>
        /// Handle keyboard input when widget has focus
        /// </summary>
        public virtual void OnWidgetKeyDown(KeyEventArgs e) { }

        /// <summary>
        /// Called when widget receives focus
        /// </summary>
        public virtual void OnWidgetFocusReceived() { }

        /// <summary>
        /// Called when widget loses focus
        /// </summary>
        public virtual void OnWidgetFocusLost() { }

        /// <summary>
        /// Save widget state (for persistence)
        /// </summary>
        public virtual Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["WidgetName"] = WidgetName,
                ["WidgetType"] = WidgetType,
                ["WidgetId"] = WidgetId
            };
        }

        /// <summary>
        /// Restore widget state (from persistence)
        /// </summary>
        public virtual void RestoreState(Dictionary<string, object> state)
        {
            // Override in derived classes to restore specific state
        }

        /// <summary>
        /// Dispose pattern implementation
        /// </summary>
        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    OnDispose();
                }

                disposed = true;
            }
        }

        /// <summary>
        /// Override in derived classes to dispose widget-specific resources
        /// (timers, event subscriptions, etc.)
        /// </summary>
        protected virtual void OnDispose()
        {
            // Unsubscribe from theme changes
            if (this is IThemeable)
            {
                ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
            }

            // Override in derived classes
        }
    }
}


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SuperTUI.Core
{
    /// <summary>
    /// Base class for screens - larger interactive components
    /// </summary>
    public abstract class ScreenBase : UserControl, INotifyPropertyChanged
    {
        public string ScreenName { get; set; }
        public string ScreenType { get; set; }
        public Guid ScreenId { get; private set; } = Guid.NewGuid();

        private bool hasFocus;
        public bool HasFocus
        {
            get => hasFocus;
            set
            {
                if (hasFocus != value)
                {
                    hasFocus = value;
                    OnPropertyChanged(nameof(HasFocus));

                    if (value)
                        OnFocusReceived();
                    else
                        OnFocusLost();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ScreenBase()
        {
            this.Focusable = true;
            this.GotFocus += (s, e) => HasFocus = true;
            this.LostFocus += (s, e) => HasFocus = false;
        }

        /// <summary>
        /// Initialize screen - called once when screen is created
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Handle keyboard input
        /// </summary>
        public virtual void OnScreenKeyDown(KeyEventArgs e) { }

        /// <summary>
        /// Called when screen receives focus
        /// </summary>
        public virtual void OnFocusReceived() { }

        /// <summary>
        /// Called when screen loses focus
        /// </summary>
        public virtual void OnFocusLost() { }

        /// <summary>
        /// Check if screen can be closed (return false to prevent)
        /// </summary>
        public virtual bool CanClose() => true;

        /// <summary>
        /// Save screen state
        /// </summary>
        public virtual Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["ScreenName"] = ScreenName,
                ["ScreenType"] = ScreenType,
                ["ScreenId"] = ScreenId
            };
        }

        /// <summary>
        /// Restore screen state
        /// </summary>
        public virtual void RestoreState(Dictionary<string, object> state)
        {
            // Override in derived classes
        }
    }
}


using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    /// <summary>
    /// Error boundary that wraps widgets and catches exceptions
    /// Prevents one widget crash from taking down the entire application
    /// </summary>
    public class ErrorBoundary : ContentControl
    {
        private readonly WidgetBase widget;
        private bool hasError = false;
        private Exception lastException;

        public ErrorBoundary(WidgetBase widget)
        {
            this.widget = widget;

            try
            {
                // Set the widget as content
                this.Content = widget;
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget construction");
            }
        }

        /// <summary>
        /// Safely initialize the widget
        /// </summary>
        public void SafeInitialize()
        {
            if (hasError) return;

            try
            {
                widget.Initialize();
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget initialization");
            }
        }

        /// <summary>
        /// Safely activate the widget
        /// </summary>
        public void SafeActivate()
        {
            if (hasError) return;

            try
            {
                widget.OnActivated();
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget activation");
            }
        }

        /// <summary>
        /// Safely deactivate the widget
        /// </summary>
        public void SafeDeactivate()
        {
            if (hasError) return;

            try
            {
                widget.OnDeactivated();
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget deactivation");
            }
        }

        /// <summary>
        /// Safely dispose the widget
        /// </summary>
        public void SafeDispose()
        {
            try
            {
                widget?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("ErrorBoundary",
                    $"Error disposing widget {widget?.WidgetName ?? "Unknown"}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get the wrapped widget (for state persistence, etc.)
        /// </summary>
        public WidgetBase GetWidget() => widget;

        /// <summary>
        /// Check if the widget is in an error state
        /// </summary>
        public bool HasError => hasError;

        /// <summary>
        /// Attempt to recover from error by reinitializing the widget
        /// </summary>
        public bool TryRecover()
        {
            if (!hasError) return true;

            try
            {
                hasError = false;
                lastException = null;

                // Clear error UI
                this.Content = widget;

                // Try to reinitialize
                widget.Initialize();

                Logger.Instance?.Info("ErrorBoundary", $"Successfully recovered widget: {widget.WidgetName}");
                return true;
            }
            catch (Exception ex)
            {
                HandleError(ex, "Widget recovery");
                return false;
            }
        }

        private void HandleError(Exception ex, string phase)
        {
            hasError = true;
            lastException = ex;

            // Log the error
            Logger.Instance?.Error("ErrorBoundary",
                $"Widget error in {phase}: {widget?.WidgetName ?? "Unknown"} - {ex.Message}", ex);

            // Replace content with error UI
            this.Content = CreateErrorUI(phase, ex);
        }

        private UIElement CreateErrorUI(string phase, Exception ex)
        {
            var theme = ThemeManager.Instance?.CurrentTheme ?? Theme.CreateDarkTheme();

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 232, 17, 35)), // Error color with transparency
                BorderBrush = new SolidColorBrush(theme.Error),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(4)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Error icon
            var iconText = new TextBlock
            {
                Text = "⚠",
                FontSize = 48,
                Foreground = new SolidColorBrush(theme.Error),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Widget name
            var widgetNameText = new TextBlock
            {
                Text = $"Widget Error: {widget?.WidgetName ?? "Unknown"}",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Phase text
            var phaseText = new TextBlock
            {
                Text = $"Failed during: {phase}",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            // Error message
            var errorText = new TextBlock
            {
                Text = ex.Message,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Error),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Recovery button
            var recoveryButton = new Button
            {
                Content = "Try to Recover",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Padding = new Thickness(15, 5, 15, 5),
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            recoveryButton.Click += (s, e) => TryRecover();

            // Instructions
            var instructionsText = new TextBlock
            {
                Text = "The widget encountered an error and was safely isolated.\nOther widgets continue to work normally.",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };

            stackPanel.Children.Add(iconText);
            stackPanel.Children.Add(widgetNameText);
            stackPanel.Children.Add(phaseText);
            stackPanel.Children.Add(errorText);
            stackPanel.Children.Add(recoveryButton);
            stackPanel.Children.Add(instructionsText);

            border.Child = stackPanel;
            return border;
        }
    }
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    public class Workspace
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public LayoutEngine Layout { get; set; }
        public List<WidgetBase> Widgets { get; set; } = new List<WidgetBase>();
        public List<ScreenBase> Screens { get; set; } = new List<ScreenBase>();
        private List<ErrorBoundary> errorBoundaries = new List<ErrorBoundary>();

        private bool isActive = false;
        private UIElement focusedElement;
        private List<UIElement> focusableElements = new List<UIElement>();

        public Workspace(string name, int index, LayoutEngine layout)
        {
            Name = name;
            Index = index;
            Layout = layout;
        }

        public void AddWidget(WidgetBase widget, LayoutParams layoutParams)
        {
            Widgets.Add(widget);

            // Wrap widget in error boundary
            var errorBoundary = new ErrorBoundary(widget);
            errorBoundaries.Add(errorBoundary);

            // Add error boundary to layout instead of widget directly
            Layout.AddChild(errorBoundary, layoutParams);
            focusableElements.Add(widget); // Focus still goes to widget

            // Safely initialize
            errorBoundary.SafeInitialize();

            if (isActive)
                errorBoundary.SafeActivate();
        }

        public void AddScreen(ScreenBase screen, LayoutParams layoutParams)
        {
            Screens.Add(screen);
            Layout.AddChild(screen, layoutParams);
            focusableElements.Add(screen);

            if (isActive)
                screen.OnFocusReceived();
        }

        public void Activate()
        {
            isActive = true;

            // Safely activate widgets through error boundaries
            foreach (var errorBoundary in errorBoundaries)
            {
                errorBoundary.SafeActivate();
            }

            foreach (var screen in Screens)
            {
                try
                {
                    screen.OnFocusReceived();
                }
                catch (Exception ex)
                {
                    Logger.Instance?.Error("Workspace", $"Error activating screen: {ex.Message}", ex);
                }
            }

            // Focus first element if nothing focused
            if (focusedElement == null && focusableElements.Count > 0)
            {
                FocusElement(focusableElements[0]);
            }
        }

        public void Deactivate()
        {
            isActive = false;

            // Safely deactivate widgets through error boundaries
            foreach (var errorBoundary in errorBoundaries)
            {
                errorBoundary.SafeDeactivate();
            }

            foreach (var screen in Screens)
            {
                try
                {
                    screen.OnFocusLost();
                }
                catch (Exception ex)
                {
                    Logger.Instance?.Error("Workspace", $"Error deactivating screen: {ex.Message}", ex);
                }
            }

            // Don't clear focus - preserve it for when workspace reactivates
        }

        public void FocusNext()
        {
            if (focusableElements.Count == 0) return;

            int currentIndex = focusedElement != null
                ? focusableElements.IndexOf(focusedElement)
                : -1;

            int nextIndex = (currentIndex + 1) % focusableElements.Count;
            FocusElement(focusableElements[nextIndex]);
        }

        public void FocusPrevious()
        {
            if (focusableElements.Count == 0) return;

            int currentIndex = focusedElement != null
                ? focusableElements.IndexOf(focusedElement)
                : 0;

            int prevIndex = (currentIndex - 1 + focusableElements.Count) % focusableElements.Count;
            FocusElement(focusableElements[prevIndex]);
        }

        private void FocusElement(UIElement element)
        {
            // Clear previous focus
            if (focusedElement != null)
            {
                if (focusedElement is WidgetBase widget)
                    widget.HasFocus = false;
                else if (focusedElement is ScreenBase screen)
                    screen.HasFocus = false;
            }

            // Set new focus
            focusedElement = element;

            if (element is WidgetBase newWidget)
            {
                newWidget.HasFocus = true;
                newWidget.Focus();
            }
            else if (element is ScreenBase newScreen)
            {
                newScreen.HasFocus = true;
                newScreen.Focus();
            }
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            // Let focused element handle key first
            if (focusedElement is WidgetBase widget)
            {
                widget.OnWidgetKeyDown(e);
            }
            else if (focusedElement is ScreenBase screen)
            {
                screen.OnScreenKeyDown(e);
            }

            // Handle Tab for focus switching (if not handled by widget/screen)
            if (!e.Handled && e.Key == Key.Tab)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    FocusPrevious();
                else
                    FocusNext();

                e.Handled = true;
            }
        }

        public Panel GetContainer()
        {
            return Layout.Container;
        }

        public Dictionary<string, object> SaveState()
        {
            var state = new Dictionary<string, object>
            {
                ["Name"] = Name,
                ["Index"] = Index,
                ["Widgets"] = Widgets.Select(w => w.SaveState()).ToList(),
                ["Screens"] = Screens.Select(s => s.SaveState()).ToList()
            };
            return state;
        }
    }

        /// <summary>
        /// Dispose of workspace and all its widgets
        /// </summary>
        public void Dispose()
        {
            Logger.Instance?.Info("Workspace", $"Disposing workspace: {Name}");

            // Safely dispose all widgets through error boundaries
            foreach (var errorBoundary in errorBoundaries.ToList())
            {
                errorBoundary.SafeDispose();
            }

            // Dispose all screens
            foreach (var screen in Screens.ToList())
            {
                try
                {
                    screen.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Instance?.Error("Workspace", $"Error disposing screen: {ex.Message}", ex);
                }
            }

            // Clear collections
            Widgets.Clear();
            Screens.Clear();
            errorBoundaries.Clear();
            focusableElements.Clear();
            focusedElement = null;

            // Clear layout
            Layout?.Clear();
        }
    }

    /// <summary>
    /// Manages workspaces and handles switching between them
    /// Each workspace maintains its own independent state
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    public class WorkspaceManager
    {
        public ObservableCollection<Workspace> Workspaces { get; private set; }
        public Workspace CurrentWorkspace { get; private set; }

        private ContentControl workspaceContainer;

        public event Action<Workspace> WorkspaceChanged;

        public WorkspaceManager(ContentControl container)
        {
            Workspaces = new ObservableCollection<Workspace>();
            workspaceContainer = container;
        }

        public void AddWorkspace(Workspace workspace)
        {
            Workspaces.Add(workspace);

            // First workspace becomes current
            if (Workspaces.Count == 1)
            {
                SwitchToWorkspace(workspace.Index);
            }
        }

        public void RemoveWorkspace(int index)
        {
            var workspace = Workspaces.FirstOrDefault(w => w.Index == index);
            if (workspace != null)
            {
                workspace.Deactivate();
                Workspaces.Remove(workspace);

                // Dispose of workspace resources
                workspace.Dispose();
            }
        }

        public void SwitchToWorkspace(int index)
        {
            var workspace = Workspaces.FirstOrDefault(w => w.Index == index);
            if (workspace != null && workspace != CurrentWorkspace)
            {
                // Deactivate current (preserves state)
                CurrentWorkspace?.Deactivate();

                // Activate new
                CurrentWorkspace = workspace;
                CurrentWorkspace.Activate();

                // Update UI
                workspaceContainer.Content = CurrentWorkspace.GetContainer();

                // Notify listeners
                WorkspaceChanged?.Invoke(CurrentWorkspace);
            }
        }

        public void SwitchToNext()
        {
            if (Workspaces.Count == 0) return;

            int currentIndex = Workspaces.IndexOf(CurrentWorkspace);
            int nextIndex = (currentIndex + 1) % Workspaces.Count;
            SwitchToWorkspace(Workspaces[nextIndex].Index);
        }

        public void SwitchToPrevious()
        {
            if (Workspaces.Count == 0) return;

            int currentIndex = Workspaces.IndexOf(CurrentWorkspace);
            int prevIndex = (currentIndex - 1 + Workspaces.Count) % Workspaces.Count;
            SwitchToWorkspace(Workspaces[prevIndex].Index);
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            CurrentWorkspace?.HandleKeyDown(e);
        }

        /// <summary>
        /// Dispose all workspaces and cleanup resources
        /// Call this when the application is closing
        /// </summary>
        public void Dispose()
        {
            Logger.Instance?.Info("WorkspaceManager", "Disposing all workspaces");

            foreach (var workspace in Workspaces.ToList())
            {
                try
                {
                    workspace.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Instance?.Error("WorkspaceManager", $"Error disposing workspace {workspace.Name}: {ex.Message}", ex);
                }
            }

            Workspaces.Clear();
            CurrentWorkspace = null;
            workspaceContainer.Content = null;
        }
    }

    // ============================================================================
    // SERVICE CONTAINER
    // ============================================================================

    /// <summary>
    /// Simple dependency injection container for services
    /// Services are singleton and shared across all widgets/screens
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Represents a saved workspace template that can be loaded and exported.
    /// Templates include layout configuration and widget definitions.
    /// </summary>
    public class WorkspaceTemplate
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Version { get; set; }
        public LayoutType LayoutType { get; set; }
        public Dictionary<string, object> LayoutConfig { get; set; }
        public List<WidgetDefinition> Widgets { get; set; }
        public Dictionary<string, string> Metadata { get; set; }

        public WorkspaceTemplate()
        {
            LayoutConfig = new Dictionary<string, object>();
            Widgets = new List<WidgetDefinition>();
            Metadata = new Dictionary<string, string>();
            CreatedAt = DateTime.Now;
            Version = "1.0";
        }
    }

    /// <summary>
    /// Defines a widget instance in a template
    /// </summary>
    public class WidgetDefinition
    {
        public string WidgetType { get; set; }
        public string Name { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public Dictionary<string, object> LayoutParameters { get; set; }

        public WidgetDefinition()
        {
            Parameters = new Dictionary<string, object>();
            LayoutParameters = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Layout type enumeration
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LayoutType
    {
        Grid,
        Dock,
        Stack
    }

    /// <summary>
    /// Manages workspace templates - save, load, export, import
    /// </summary>
    public class WorkspaceTemplateManager
    {
        private static WorkspaceTemplateManager instance;
        private static readonly object lockObject = new object();

        private string templatesDirectory;
        private Dictionary<string, WorkspaceTemplate> loadedTemplates;

        public static WorkspaceTemplateManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new WorkspaceTemplateManager();
                        }
                    }
                }
                return instance;
            }
        }

        private WorkspaceTemplateManager()
        {
            templatesDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SuperTUI", "Templates");

            if (!Directory.Exists(templatesDirectory))
                Directory.CreateDirectory(templatesDirectory);

            loadedTemplates = new Dictionary<string, WorkspaceTemplate>();
            LoadAllTemplates();
        }

        /// <summary>
        /// Save a workspace template to disk
        /// </summary>
        public void SaveTemplate(WorkspaceTemplate template)
        {
            try
            {
                var fileName = SanitizeFileName(template.Name) + ".json";
                var filePath = Path.Combine(templatesDirectory, fileName);

                var json = JsonSerializer.Serialize(template, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                File.WriteAllText(filePath, json);
                loadedTemplates[template.Name] = template;

                Logger.Instance.Info("Templates", $"Saved template: {template.Name}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to save template: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load a workspace template by name
        /// </summary>
        public WorkspaceTemplate LoadTemplate(string name)
        {
            if (loadedTemplates.TryGetValue(name, out var template))
                return template;

            var fileName = SanitizeFileName(name) + ".json";
            var filePath = Path.Combine(templatesDirectory, fileName);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Template not found: {name}");

            try
            {
                var json = File.ReadAllText(filePath);
                template = JsonSerializer.Deserialize<WorkspaceTemplate>(json);
                loadedTemplates[name] = template;

                Logger.Instance.Info("Templates", $"Loaded template: {name}");
                return template;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to load template: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Export a template to a specific file path
        /// </summary>
        public void ExportTemplate(string templateName, string exportPath)
        {
            var template = LoadTemplate(templateName);

            try
            {
                var json = JsonSerializer.Serialize(template, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                File.WriteAllText(exportPath, json);
                Logger.Instance.Info("Templates", $"Exported template to: {exportPath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to export template: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Import a template from a file
        /// </summary>
        public WorkspaceTemplate ImportTemplate(string importPath)
        {
            try
            {
                var json = File.ReadAllText(importPath);
                var template = JsonSerializer.Deserialize<WorkspaceTemplate>(json);

                // Save to templates directory
                SaveTemplate(template);

                Logger.Instance.Info("Templates", $"Imported template: {template.Name}");
                return template;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to import template: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Delete a template
        /// </summary>
        public void DeleteTemplate(string name)
        {
            var fileName = SanitizeFileName(name) + ".json";
            var filePath = Path.Combine(templatesDirectory, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                loadedTemplates.Remove(name);
                Logger.Instance.Info("Templates", $"Deleted template: {name}");
            }
        }

        /// <summary>
        /// List all available templates
        /// </summary>
        public List<WorkspaceTemplate> ListTemplates()
        {
            return loadedTemplates.Values.OrderBy(t => t.Name).ToList();
        }

        /// <summary>
        /// Check if a template exists
        /// </summary>
        public bool TemplateExists(string name)
        {
            return loadedTemplates.ContainsKey(name);
        }

        /// <summary>
        /// Load all templates from disk
        /// </summary>
        private void LoadAllTemplates()
        {
            try
            {
                var files = Directory.GetFiles(templatesDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var template = JsonSerializer.Deserialize<WorkspaceTemplate>(json);
                        if (template != null)
                        {
                            loadedTemplates[template.Name] = template;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("Templates", $"Failed to load template file {file}: {ex.Message}");
                    }
                }

                Logger.Instance.Info("Templates", $"Loaded {loadedTemplates.Count} templates");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to load templates: {ex.Message}");
            }
        }

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", name.Split(invalidChars));
            return sanitized;
        }

        /// <summary>
        /// Create built-in example templates
        /// </summary>
        public void CreateBuiltInTemplates()
        {
            // Developer workspace template
            var devTemplate = new WorkspaceTemplate
            {
                Name = "Developer",
                Description = "Development workspace with terminal, git, and file explorer",
                Author = "SuperTUI",
                LayoutType = LayoutType.Grid,
                LayoutConfig = new Dictionary<string, object>
                {
                    ["Rows"] = 2,
                    ["Columns"] = 3,
                    ["Splitters"] = true
                },
                Widgets = new List<WidgetDefinition>
                {
                    new WidgetDefinition
                    {
                        WidgetType = "GitStatusWidget",
                        Name = "Git Status",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 0
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "FileExplorerWidget",
                        Name = "Files",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 1
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "SystemMonitorWidget",
                        Name = "System",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 2
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "TerminalWidget",
                        Name = "Terminal",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 1,
                            ["Column"] = 0,
                            ["ColumnSpan"] = 3
                        }
                    }
                }
            };

            // Productivity workspace template
            var productivityTemplate = new WorkspaceTemplate
            {
                Name = "Productivity",
                Description = "Todo list, notes, and clock for task management",
                Author = "SuperTUI",
                LayoutType = LayoutType.Grid,
                LayoutConfig = new Dictionary<string, object>
                {
                    ["Rows"] = 2,
                    ["Columns"] = 2,
                    ["Splitters"] = true
                },
                Widgets = new List<WidgetDefinition>
                {
                    new WidgetDefinition
                    {
                        WidgetType = "TodoWidget",
                        Name = "Tasks",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 0
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "ClockWidget",
                        Name = "Clock",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 1
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "NotesWidget",
                        Name = "Notes",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 1,
                            ["Column"] = 0,
                            ["ColumnSpan"] = 2
                        }
                    }
                }
            };

            // Save built-in templates
            if (!TemplateExists("Developer"))
                SaveTemplate(devTemplate);

            if (!TemplateExists("Productivity"))
                SaveTemplate(productivityTemplate);
        }
    }
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Extensions
{
    /// <summary>
    /// Helper for portable data directory paths
    /// </summary>
    public static class PortableDataDirectory
    {
        private static string dataDirectory;

        /// <summary>
        /// Get or set the data directory. If not set, returns a directory next to the current directory.
        /// </summary>
        public static string DataDirectory
        {
            get
            {
                if (dataDirectory == null)
                {
                    // Default: .data folder in current directory
                    dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".data");
                }

                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                }

                return dataDirectory;
            }
            set
            {
                dataDirectory = value;
                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                }
            }
        }

        /// <summary>
        /// Get path to SuperTUI data directory
        /// </summary>
        public static string GetSuperTUIDataDirectory()
        {
            var path = Path.Combine(DataDirectory, "SuperTUI");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }

    // ============================================================================
    // STATE PERSISTENCE SYSTEM
    // ============================================================================

    /// <summary>
    /// State snapshot for persistence with versioning support
    /// </summary>
    public class StateSnapshot
    {
        /// <summary>
        /// Schema version of this state snapshot. Format: "major.minor"
        /// Breaking changes increment major, compatible changes increment minor.
        /// </summary>
        public string Version { get; set; } = StateVersion.Current;

        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> ApplicationState { get; set; } = new Dictionary<string, object>();
        public List<WorkspaceState> Workspaces { get; set; } = new List<WorkspaceState>();
        public Dictionary<string, object> UserData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// State version constants and comparison utilities
    /// </summary>
    public static class StateVersion
    {
        public const string Current = "1.0";

        // Historical versions for migration tracking
        public const string V1_0 = "1.0"; // Initial version

        /// <summary>
        /// Compare two version strings (format: "major.minor")
        /// </summary>
        /// <returns>-1 if v1 < v2, 0 if equal, 1 if v1 > v2</returns>
        public static int Compare(string v1, string v2)
        {
            if (string.IsNullOrEmpty(v1)) v1 = "1.0";
            if (string.IsNullOrEmpty(v2)) v2 = "1.0";

            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');

            int major1 = int.Parse(parts1[0]);
            int minor1 = parts1.Length > 1 ? int.Parse(parts1[1]) : 0;

            int major2 = int.Parse(parts2[0]);
            int minor2 = parts2.Length > 1 ? int.Parse(parts2[1]) : 0;

            if (major1 != major2) return major1.CompareTo(major2);
            return minor1.CompareTo(minor2);
        }

        /// <summary>
        /// Check if a version is compatible with the current version
        /// (same major version, minor version can be lower)
        /// </summary>
        public static bool IsCompatible(string version)
        {
            if (string.IsNullOrEmpty(version)) return true; // Assume compatible for missing version

            var parts1 = version.Split('.');
            var parts2 = Current.Split('.');

            int major1 = int.Parse(parts1[0]);
            int major2 = int.Parse(parts2[0]);

            // Compatible if same major version
            return major1 == major2;
        }
    }

    public class WorkspaceState
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public List<Dictionary<string, object>> WidgetStates { get; set; } = new List<Dictionary<string, object>>();
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Interface for state migration from one version to another
    /// </summary>
    public interface IStateMigration
    {
        /// <summary>
        /// Version this migration migrates FROM
        /// </summary>
        string FromVersion { get; }

        /// <summary>
        /// Version this migration migrates TO
        /// </summary>
        string ToVersion { get; }

        /// <summary>
        /// Perform the migration
        /// </summary>
        StateSnapshot Migrate(StateSnapshot snapshot);
    }

    /// <summary>
    /// Manages state migrations across versions
    /// </summary>
    public class StateMigrationManager
    {
        private readonly List<IStateMigration> migrations = new List<IStateMigration>();

        public StateMigrationManager()
        {
            // Register migrations in order
            // RegisterMigration(new Migration_1_0_to_1_1());
            // ^ Migration 1.0 to 1.1 adds WidgetId to all widgets
            // ^ Uncomment when needed (currently at 1.0)

            // Future migrations will be added here:
            // RegisterMigration(new Migration_1_1_to_2_0());
        }

        public void RegisterMigration(IStateMigration migration)
        {
            migrations.Add(migration);
            Logger.Instance.Debug("StateMigration", $"Registered migration: {migration.FromVersion} -> {migration.ToVersion}");
        }

        /// <summary>
        /// Migrate a state snapshot to the current version
        /// </summary>
        public StateSnapshot MigrateToCurrentVersion(StateSnapshot snapshot)
        {
            if (snapshot.Version == StateVersion.Current)
            {
                Logger.Instance.Debug("StateMigration", "State is already at current version");
                return snapshot;
            }

            // Check if version is compatible
            if (!StateVersion.IsCompatible(snapshot.Version))
            {
                Logger.Instance.Warning("StateMigration",
                    $"State version {snapshot.Version} is not compatible with current version {StateVersion.Current}. " +
                    "Migration may fail or produce unexpected results.");
            }

            Logger.Instance.Info("StateMigration", $"Migrating state from {snapshot.Version} to {StateVersion.Current}");

            // Build migration path
            var migrationPath = BuildMigrationPath(snapshot.Version, StateVersion.Current);
            if (migrationPath.Count == 0)
            {
                Logger.Instance.Warning("StateMigration",
                    $"No migration path found from {snapshot.Version} to {StateVersion.Current}. " +
                    "State will be loaded as-is, which may cause errors.");
                return snapshot;
            }

            // Execute migrations in sequence
            var currentSnapshot = snapshot;
            foreach (var migration in migrationPath)
            {
                try
                {
                    Logger.Instance.Info("StateMigration", $"Applying migration: {migration.FromVersion} -> {migration.ToVersion}");
                    currentSnapshot = migration.Migrate(currentSnapshot);
                    currentSnapshot.Version = migration.ToVersion;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("StateMigration",
                        $"Migration failed: {migration.FromVersion} -> {migration.ToVersion}", ex);
                    throw new InvalidOperationException(
                        $"State migration failed at version {migration.FromVersion}. Cannot proceed.", ex);
                }
            }

            Logger.Instance.Info("StateMigration", $"State successfully migrated to {StateVersion.Current}");
            return currentSnapshot;
        }

        /// <summary>
        /// Build a migration path from source version to target version
        /// </summary>
        private List<IStateMigration> BuildMigrationPath(string fromVersion, string toVersion)
        {
            var path = new List<IStateMigration>();
            var currentVersion = fromVersion;

            // Simple linear search for migration path
            // For more complex version graphs, implement a proper pathfinding algorithm (BFS/Dijkstra)
            while (currentVersion != toVersion)
            {
                var nextMigration = migrations.FirstOrDefault(m => m.FromVersion == currentVersion);
                if (nextMigration == null)
                {
                    Logger.Instance.Warning("StateMigration",
                        $"No migration found from {currentVersion}. Migration path incomplete.");
                    break;
                }

                path.Add(nextMigration);
                currentVersion = nextMigration.ToVersion;

                // Prevent infinite loops
                if (path.Count > 100)
                {
                    Logger.Instance.Error("StateMigration", "Migration path too long (>100 steps). Possible circular dependency.");
                    throw new InvalidOperationException("Migration path contains circular dependency");
                }
            }

            return path;
        }

        /// <summary>
        /// Get all registered migrations
        /// </summary>
        public IReadOnlyList<IStateMigration> GetMigrations() => migrations.AsReadOnly();
    }

    // Example migration (template for future use)
    /*
    /// <summary>
    /// Example migration from version 1.0 to 1.1
    /// </summary>
    public class Migration_1_0_to_1_1 : IStateMigration
    {
        public string FromVersion => "1.0";
        public string ToVersion => "1.1";

        public StateSnapshot Migrate(StateSnapshot snapshot)
        {
            Logger.Instance.Info("StateMigration", "Migrating from 1.0 to 1.1");

            // Example: Add new field to ApplicationState
            if (!snapshot.ApplicationState.ContainsKey("NewField"))
            {
                snapshot.ApplicationState["NewField"] = "DefaultValue";
            }

            // Example: Transform workspace data
            foreach (var workspace in snapshot.Workspaces)
            {
                if (!workspace.CustomData.ContainsKey("NewWorkspaceField"))
                {
                    workspace.CustomData["NewWorkspaceField"] = 0;
                }
            }

            // Example: Migrate widget states
            foreach (var workspace in snapshot.Workspaces)
            {
                foreach (var widgetState in workspace.WidgetStates)
                {
                    // Rename a field
                    if (widgetState.ContainsKey("OldFieldName"))
                    {
                        widgetState["NewFieldName"] = widgetState["OldFieldName"];
                        widgetState.Remove("OldFieldName");
                    }
                }
            }

            return snapshot;
        }
    }
    */

    /// <summary>
    /// State persistence manager with versioning and backup
    /// </summary>
    public class StatePersistenceManager
    {
        private static StatePersistenceManager instance;
        public static StatePersistenceManager Instance => instance ??= new StatePersistenceManager();

        private string stateDirectory;
        private string currentStateFile;
        private StateSnapshot currentState;
        private readonly LinkedList<StateSnapshot> undoHistory = new LinkedList<StateSnapshot>();
        private readonly LinkedList<StateSnapshot> redoHistory = new LinkedList<StateSnapshot>();
        private readonly StateMigrationManager migrationManager = new StateMigrationManager();
        private const int MaxUndoLevels = 50;

        public event EventHandler<StateChangedEventArgs> StateChanged;

        /// <summary>
        /// Get the migration manager for registering custom migrations
        /// </summary>
        public StateMigrationManager MigrationManager => migrationManager;

        public void Initialize(string stateDir = null)
        {
            stateDirectory = stateDir ?? Path.Combine(
                PortableDataDirectory.GetSuperTUIDataDirectory(), "State");

            Directory.CreateDirectory(stateDirectory);
            currentStateFile = Path.Combine(stateDirectory, "current_state.json");

            Logger.Instance.Info("StatePersistence", $"Initialized state persistence at {stateDirectory}");
        }

        public StateSnapshot CaptureState(WorkspaceManager workspaceManager, Dictionary<string, object> customData = null)
        {
            var snapshot = new StateSnapshot
            {
                Timestamp = DateTime.Now,
                ApplicationState = new Dictionary<string, object>
                {
                    ["CurrentWorkspaceIndex"] = workspaceManager.CurrentWorkspace?.Index ?? 0
                },
                UserData = customData ?? new Dictionary<string, object>()
            };

            // Capture workspace states
            foreach (var workspace in workspaceManager.Workspaces)
            {
                var workspaceState = new WorkspaceState
                {
                    Name = workspace.Name,
                    Index = workspace.Index
                };

                // Capture widget states
                foreach (var widget in workspace.Widgets)
                {
                    try
                    {
                        var widgetState = widget.SaveState();
                        workspaceState.WidgetStates.Add(widgetState);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning("StatePersistence", $"Failed to save widget state: {ex.Message}");
                    }
                }

                snapshot.Workspaces.Add(workspaceState);
            }

            currentState = snapshot;
            Logger.Instance.Debug("StatePersistence", "State snapshot captured");

            return snapshot;
        }

        /// <summary>
        /// Restores application state from a snapshot
        /// Matches widgets by WidgetId ONLY - does not fallback to WidgetName
        /// </summary>
        /// <remarks>
        /// Design Decision: We require WidgetId for state restoration because:
        /// 1. Multiple widgets can have the same name (e.g., "Counter 1", "Counter 2" both named "Counter")
        /// 2. Name-based matching is non-deterministic (depends on widget creation order)
        /// 3. Name-based matching can restore state to the WRONG widget silently
        /// 4. WidgetId is unique per widget instance and never changes
        ///
        /// Legacy states without WidgetId will log a warning and be skipped.
        /// User should save state again to generate WidgetIds for all widgets.
        /// </remarks>
        public void RestoreState(StateSnapshot snapshot, WorkspaceManager workspaceManager)
        {
            try
            {
                Logger.Instance.Info("StatePersistence", "Restoring state from snapshot");

                // Restore workspace states
                foreach (var workspaceState in snapshot.Workspaces)
                {
                    var workspace = workspaceManager.Workspaces.FirstOrDefault(w => w.Index == workspaceState.Index);
                    if (workspace != null)
                    {
                        // Restore widget states by matching WidgetId ONLY
                        // WidgetName fallback removed to prevent ambiguous matching with duplicate names
                        foreach (var widgetState in workspaceState.WidgetStates)
                        {
                            try
                            {
                                // Find widget by ID (required)
                                if (widgetState.TryGetValue("WidgetId", out var widgetIdObj))
                                {
                                    Guid widgetId;

                                    // Handle different serialization formats
                                    if (widgetIdObj is Guid guid)
                                    {
                                        widgetId = guid;
                                    }
                                    else if (widgetIdObj is string guidString && Guid.TryParse(guidString, out var parsedGuid))
                                    {
                                        widgetId = parsedGuid;
                                    }
                                    else
                                    {
                                        Logger.Instance.Warning("StatePersistence",
                                            $"Widget state has invalid WidgetId format: {widgetIdObj?.GetType().Name ?? "null"}");
                                        continue;
                                    }

                                    // Find widget by ID
                                    var widget = workspace.Widgets.FirstOrDefault(w => w.WidgetId == widgetId);
                                    if (widget != null)
                                    {
                                        widget.RestoreState(widgetState);
                                        Logger.Instance.Debug("StatePersistence",
                                            $"Restored widget: {widget.WidgetName} (ID: {widgetId})");
                                    }
                                    else
                                    {
                                        // Widget with this ID doesn't exist (might have been removed)
                                        string widgetName = widgetState.TryGetValue("WidgetName", out var nameObj) ? nameObj?.ToString() : "Unknown";
                                        Logger.Instance.Debug("StatePersistence",
                                            $"Widget '{widgetName}' with ID {widgetId} not found in workspace (may have been removed)");
                                    }
                                }
                                else
                                {
                                    // No WidgetId in saved state - this is a legacy state or corrupted data
                                    string widgetName = widgetState.TryGetValue("WidgetName", out var nameObj) ? nameObj?.ToString() : "Unknown";
                                    string widgetType = widgetState.TryGetValue("WidgetType", out var typeObj) ? typeObj?.ToString() : "Unknown";

                                    Logger.Instance.Warning("StatePersistence",
                                        $"LEGACY STATE DETECTED: Widget '{widgetName}' (type: {widgetType}) has no WidgetId. " +
                                        $"State will NOT be restored. Please save state again to generate WidgetIds.");

                                    // NOTE: We deliberately do NOT attempt name-based matching because:
                                    // 1. It's ambiguous when multiple widgets have the same name
                                    // 2. It's non-deterministic (depends on widget order)
                                    // 3. It leads to subtle bugs that are hard to diagnose
                                    // Instead, we require the user to save state again, which will generate WidgetIds
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error("StatePersistence",
                                    $"Failed to restore widget state: {ex.Message}", ex);
                            }
                        }
                    }
                }

                // Restore current workspace
                if (snapshot.ApplicationState.TryGetValue("CurrentWorkspaceIndex", out var currentIndex))
                {
                    workspaceManager.SwitchToWorkspace((int)currentIndex);
                }

                StateChanged?.Invoke(this, new StateChangedEventArgs { Snapshot = snapshot });
                Logger.Instance.Info("StatePersistence", "State restored successfully");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to restore state: {ex.Message}", ex);
            }
        }

        public void SaveState(StateSnapshot snapshot = null, bool createBackup = false)
        {
            // Synchronous wrapper for backward compatibility
            SaveStateAsync(snapshot, createBackup).GetAwaiter().GetResult();
        }

        public async Task SaveStateAsync(StateSnapshot snapshot = null, bool createBackup = false)
        {
            snapshot = snapshot ?? currentState;
            if (snapshot == null)
            {
                Logger.Instance.Warning("StatePersistence", "No state to save");
                return;
            }

            try
            {
                // Create backup if requested
                if (createBackup && File.Exists(currentStateFile))
                {
                    await CreateBackupAsync();
                }

                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(currentStateFile, json, Encoding.UTF8);

                Logger.Instance.Info("StatePersistence", $"State saved to {currentStateFile}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to save state: {ex.Message}", ex);
            }
        }

        public StateSnapshot LoadState()
        {
            // Synchronous wrapper for backward compatibility
            return LoadStateAsync().GetAwaiter().GetResult();
        }

        public async Task<StateSnapshot> LoadStateAsync()
        {
            try
            {
                if (!File.Exists(currentStateFile))
                {
                    Logger.Instance.Info("StatePersistence", "No saved state found");
                    return null;
                }

                string json = await File.ReadAllTextAsync(currentStateFile, Encoding.UTF8);
                var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json);

                // Check version and migrate if necessary
                if (snapshot.Version != StateVersion.Current)
                {
                    Logger.Instance.Info("StatePersistence",
                        $"State version mismatch. Loaded: {snapshot.Version}, Current: {StateVersion.Current}");

                    // Create backup before migration
                    await CreateBackupAsync();

                    // Perform migration
                    snapshot = migrationManager.MigrateToCurrentVersion(snapshot);

                    // Save migrated state
                    await SaveStateAsync(snapshot, createBackup: false);
                }

                Logger.Instance.Info("StatePersistence", $"State loaded successfully (version {snapshot.Version})");
                return snapshot;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to load state: {ex.Message}", ex);
                return null;
            }
        }

        public void PushUndo(StateSnapshot snapshot)
        {
            // Add to end (most recent)
            undoHistory.AddLast(snapshot);

            // Remove oldest if exceeds max
            if (undoHistory.Count > MaxUndoLevels)
            {
                undoHistory.RemoveFirst();
            }

            // Clear redo history when new action performed
            redoHistory.Clear();
        }

        public StateSnapshot Undo()
        {
            if (undoHistory.Count == 0)
            {
                Logger.Instance.Info("StatePersistence", "No undo history available");
                return null;
            }

            // Get most recent (last)
            var snapshot = undoHistory.Last.Value;
            undoHistory.RemoveLast();

            // Save current state to redo
            if (currentState != null)
            {
                redoHistory.AddLast(currentState);
            }

            Logger.Instance.Info("StatePersistence", "Undo performed");
            return snapshot;
        }

        public StateSnapshot Redo()
        {
            if (redoHistory.Count == 0)
            {
                Logger.Instance.Info("StatePersistence", "No redo history available");
                return null;
            }

            // Get most recent redo
            var snapshot = redoHistory.Last.Value;
            redoHistory.RemoveLast();

            // Save current state to undo
            if (currentState != null)
            {
                undoHistory.AddLast(currentState);
            }

            Logger.Instance.Info("StatePersistence", "Redo performed");
            return snapshot;
        }

        private void CreateBackup()
        {
            // Synchronous wrapper for backward compatibility
            CreateBackupAsync().GetAwaiter().GetResult();
        }

        private async Task CreateBackupAsync()
        {
            if (!ConfigurationManager.Instance.Get<bool>("Backup.Enabled", true))
                return;

            try
            {
                string backupDir = ConfigurationManager.Instance.Get<string>("Backup.Directory");
                Directory.CreateDirectory(backupDir);

                string backupFile = Path.Combine(backupDir, $"state_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.Copy(currentStateFile, backupFile);

                // Compress if enabled
                if (ConfigurationManager.Instance.Get<bool>("Backup.CompressBackups", true))
                {
                    string zipFile = backupFile + ".gz";
                    using (var input = File.OpenRead(backupFile))
                    using (var output = File.Create(zipFile))
                    using (var gzip = new GZipStream(output, CompressionMode.Compress))
                    {
                        await input.CopyToAsync(gzip);
                    }
                    File.Delete(backupFile);
                    backupFile = zipFile;
                }

                // Clean old backups
                int maxBackups = ConfigurationManager.Instance.Get<int>("Backup.MaxBackups", 10);
                var backups = Directory.GetFiles(backupDir, "state_backup_*")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Skip(maxBackups)
                    .ToList();

                foreach (var old in backups)
                {
                    try { File.Delete(old); } catch { }
                }

                Logger.Instance.Info("StatePersistence", $"Backup created: {backupFile}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to create backup: {ex.Message}", ex);
            }
        }

        public List<string> GetAvailableBackups()
        {
            try
            {
                string backupDir = ConfigurationManager.Instance.Get<string>("Backup.Directory");
                if (!Directory.Exists(backupDir))
                    return new List<string>();

                return Directory.GetFiles(backupDir, "state_backup_*")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to get backups: {ex.Message}", ex);
                return new List<string>();
            }
        }

        public StateSnapshot RestoreFromBackup(string backupPath)
        {
            try
            {
                string json;

                if (backupPath.EndsWith(".gz"))
                {
                    using (var input = File.OpenRead(backupPath))
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzip))
                    {
                        json = reader.ReadToEnd();
                    }
                }
                else
                {
                    json = File.ReadAllText(backupPath, Encoding.UTF8);
                }

                var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json);
                Logger.Instance.Info("StatePersistence", $"Restored from backup: {backupPath}");
                return snapshot;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to restore from backup: {ex.Message}", ex);
                return null;
            }
        }
    }

    public class StateChangedEventArgs : EventArgs
    {
        public StateSnapshot Snapshot { get; set; }
    }

    // ============================================================================
    // PLUGIN / EXTENSION SYSTEM
    // ============================================================================

    /// <summary>
    /// Plugin metadata
    /// </summary>
    public class PluginMetadata
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Plugin interface that all plugins must implement
    /// </summary>
    public interface IPlugin
    {
        PluginMetadata Metadata { get; }
        void Initialize(PluginContext context);
        void Shutdown();
    }

    /// <summary>
    /// Context provided to plugins
    /// </summary>
    public class PluginContext
    {
        public Logger Logger { get; set; }
        public ConfigurationManager Config { get; set; }
        public ThemeManager Themes { get; set; }
        public WorkspaceManager Workspaces { get; set; }
        public Dictionary<string, object> SharedData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Plugin manager for loading and managing extensions
    /// </summary>
    public class PluginManager
    {
        private static PluginManager instance;
        public static PluginManager Instance => instance ??= new PluginManager();

        private readonly Dictionary<string, IPlugin> plugins = new Dictionary<string, IPlugin>();
        private readonly Dictionary<string, Assembly> pluginAssemblies = new Dictionary<string, Assembly>();
        private string pluginsDirectory;
        private PluginContext pluginContext;

        public event EventHandler<PluginEventArgs> PluginLoaded;
        public event EventHandler<PluginEventArgs> PluginUnloaded;

        public void Initialize(string pluginsDir, PluginContext context)
        {
            pluginsDirectory = pluginsDir ?? Path.Combine(
                PortableDataDirectory.GetSuperTUIDataDirectory(), "Plugins");

            pluginContext = context;

            Directory.CreateDirectory(pluginsDirectory);

            Logger.Instance.Info("PluginManager", $"Initialized plugin system at {pluginsDirectory}");
        }

        public void LoadPlugins()
        {
            if (!Directory.Exists(pluginsDirectory))
            {
                Logger.Instance.Warning("PluginManager", "Plugins directory not found");
                return;
            }

            try
            {
                // Load DLLs from plugin directory
                var dllFiles = Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories);

                foreach (var dllFile in dllFiles)
                {
                    LoadPlugin(dllFile);
                }

                Logger.Instance.Info("PluginManager", $"Loaded {plugins.Count} plugins");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("PluginManager", $"Failed to load plugins: {ex.Message}", ex);
            }
        }

        public void LoadPlugin(string assemblyPath)
        {
            try
            {
                // Validate file access
                if (!SecurityManager.Instance.ValidateFileAccess(assemblyPath))
                {
                    Logger.Instance.Warning("PluginManager", $"Security validation failed for plugin: {assemblyPath}");
                    return;
                }

                // WARNING: Assembly.LoadFrom loads into the default AppDomain and CANNOT be unloaded
                // until the application exits. This is a known limitation of .NET Framework.
                // For true plugin unloading, consider migrating to .NET Core/5+ with AssemblyLoadContext,
                // or use separate AppDomains (deprecated in .NET Core).
                // See: https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
                var assembly = Assembly.LoadFrom(assemblyPath);
                pluginAssemblies[assemblyPath] = assembly;

                Logger.Instance.Warning("PluginManager", $"Plugin assembly loaded and will remain in memory until app exit: {assemblyPath}");

                // Find plugin types
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        var plugin = (IPlugin)Activator.CreateInstance(pluginType);

                        // Check if already loaded
                        if (plugins.ContainsKey(plugin.Metadata.Name))
                        {
                            Logger.Instance.Warning("PluginManager", $"Plugin already loaded: {plugin.Metadata.Name}");
                            continue;
                        }

                        // Check dependencies
                        if (!CheckDependencies(plugin.Metadata.Dependencies))
                        {
                            Logger.Instance.Warning("PluginManager", $"Plugin dependencies not met: {plugin.Metadata.Name}");
                            continue;
                        }

                        // Initialize plugin
                        plugin.Initialize(pluginContext);

                        plugins[plugin.Metadata.Name] = plugin;
                        Logger.Instance.Info("PluginManager", $"Loaded plugin: {plugin.Metadata.Name} v{plugin.Metadata.Version}");

                        PluginLoaded?.Invoke(this, new PluginEventArgs { Plugin = plugin });
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("PluginManager", $"Failed to instantiate plugin {pluginType.Name}: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("PluginManager", $"Failed to load plugin assembly {assemblyPath}: {ex.Message}", ex);
            }
        }

        private bool CheckDependencies(List<string> dependencies)
        {
            if (dependencies == null || dependencies.Count == 0)
                return true;

            foreach (var dep in dependencies)
            {
                if (!plugins.ContainsKey(dep))
                    return false;
            }

            return true;
        }

        public void UnloadPlugin(string pluginName)
        {
            if (!plugins.TryGetValue(pluginName, out var plugin))
            {
                Logger.Instance.Warning("PluginManager", $"Plugin not found: {pluginName}");
                return;
            }

            try
            {
                plugin.Shutdown();
                plugins.Remove(pluginName);

                Logger.Instance.Info("PluginManager", $"Plugin deactivated: {pluginName}");
                Logger.Instance.Warning("PluginManager", $"Plugin assembly remains in memory (cannot unload in .NET Framework)");
                PluginUnloaded?.Invoke(this, new PluginEventArgs { Plugin = plugin });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("PluginManager", $"Error unloading plugin {pluginName}: {ex.Message}", ex);
            }
        }

        public IPlugin GetPlugin(string name)
        {
            return plugins.TryGetValue(name, out var plugin) ? plugin : null;
        }

        public List<IPlugin> GetAllPlugins()
        {
            return plugins.Values.ToList();
        }

        public void UnloadAll()
        {
            var pluginNames = plugins.Keys.ToList();
            foreach (var name in pluginNames)
            {
                UnloadPlugin(name);
            }
        }
    }

    public class PluginEventArgs : EventArgs
    {
        public IPlugin Plugin { get; set; }
    }

    // ============================================================================
    // PERFORMANCE MONITORING
    // ============================================================================

    /// <summary>
    /// Performance counter for monitoring operations
    /// </summary>
    public class PerformanceCounter
    {
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private readonly Queue<TimeSpan> samples = new Queue<TimeSpan>();
        private readonly int maxSamples;

        public string Name { get; }
        public TimeSpan LastDuration { get; private set; }
        public TimeSpan AverageDuration => samples.Count > 0 ? TimeSpan.FromTicks((long)samples.Average(s => s.Ticks)) : TimeSpan.Zero;
        public TimeSpan MinDuration => samples.Count > 0 ? TimeSpan.FromTicks(samples.Min(s => s.Ticks)) : TimeSpan.Zero;
        public TimeSpan MaxDuration => samples.Count > 0 ? TimeSpan.FromTicks(samples.Max(s => s.Ticks)) : TimeSpan.Zero;
        public int SampleCount => samples.Count;

        public PerformanceCounter(string name, int maxSamples = 100)
        {
            Name = name;
            this.maxSamples = maxSamples;
        }

        public void Start()
        {
            stopwatch.Restart();
        }

        public void Stop()
        {
            stopwatch.Stop();
            LastDuration = stopwatch.Elapsed;

            if (samples.Count >= maxSamples)
            {
                samples.Dequeue();
            }
            samples.Enqueue(LastDuration);
        }

        public void Reset()
        {
            samples.Clear();
            LastDuration = TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Performance monitor for tracking various metrics
    /// </summary>
    public class PerformanceMonitor
    {
        private static PerformanceMonitor instance;
        public static PerformanceMonitor Instance => instance ??= new PerformanceMonitor();

        private readonly Dictionary<string, PerformanceCounter> counters = new Dictionary<string, PerformanceCounter>();

        public PerformanceCounter GetCounter(string name)
        {
            if (!counters.TryGetValue(name, out var counter))
            {
                counter = new PerformanceCounter(name);
                counters[name] = counter;
            }
            return counter;
        }

        public void StartOperation(string name)
        {
            GetCounter(name).Start();
        }

        public void StopOperation(string name)
        {
            var counter = GetCounter(name);
            counter.Stop();

            // Log if operation took too long
            if (counter.LastDuration.TotalMilliseconds > 100)
            {
                Logger.Instance.Warning("Performance", $"Slow operation detected: {name} took {counter.LastDuration.TotalMilliseconds:F2}ms");
            }
        }

        public Dictionary<string, PerformanceCounter> GetAllCounters()
        {
            return new Dictionary<string, PerformanceCounter>(counters);
        }

        public void ResetAll()
        {
            foreach (var counter in counters.Values)
            {
                counter.Reset();
            }
        }

        public string GenerateReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Performance Report ===");
            sb.AppendLine();

            foreach (var kvp in counters.OrderByDescending(c => c.Value.AverageDuration))
            {
                var counter = kvp.Value;
                sb.AppendLine($"{counter.Name}:");
                sb.AppendLine($"  Samples: {counter.SampleCount}");
                sb.AppendLine($"  Last: {counter.LastDuration.TotalMilliseconds:F2}ms");
                sb.AppendLine($"  Avg:  {counter.AverageDuration.TotalMilliseconds:F2}ms");
                sb.AppendLine($"  Min:  {counter.MinDuration.TotalMilliseconds:F2}ms");
                sb.AppendLine($"  Max:  {counter.MaxDuration.TotalMilliseconds:F2}ms");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}


using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Simple clock widget showing current time
    /// </summary>
    public class ClockWidget : WidgetBase, IThemeable
    {
        private Border containerBorder;
        private TextBlock timeText;
        private TextBlock dateText;
        private DispatcherTimer timer;

        private string currentTime;
        public string CurrentTime
        {
            get => currentTime;
            set
            {
                currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }

        private string currentDate;
        public string CurrentDate
        {
            get => currentDate;
            set
            {
                currentDate = value;
                OnPropertyChanged(nameof(CurrentDate));
            }
        }

        public ClockWidget()
        {
            WidgetType = "Clock";
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            // Container
            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Time display
            timeText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Info),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Date display
            dateText = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                FontSize = 14,
                Foreground = new SolidColorBrush(theme.Foreground),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(timeText);
            stackPanel.Children.Add(dateText);
            containerBorder.Child = stackPanel;

            this.Content = containerBorder;

            // Bind to properties (for demonstration of data binding)
            timeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CurrentTime") { Source = this });
            dateText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CurrentDate") { Source = this });
        }

        public override void Initialize()
        {
            // Update immediately
            UpdateTime();

            // Set up timer to update every second
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateTime();
        }

        private void UpdateTime()
        {
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");
            CurrentDate = now.ToString("dddd, MMMM dd, yyyy");
        }

        public override void OnActivated()
        {
            // Resume timer when workspace is shown
            timer?.Start();
        }

        public override void OnDeactivated()
        {
            // Pause timer when workspace is hidden (save CPU)
            timer?.Stop();
        }

        protected override void OnDispose()
        {
            // Stop and dispose timer
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick;
                timer = null;
            }

            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
                containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (timeText != null)
            {
                timeText.Foreground = new SolidColorBrush(theme.Info);
            }

            if (dateText != null)
            {
                dateText.Foreground = new SolidColorBrush(theme.Foreground);
            }
        }
    }
}


using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Simple counter widget - demonstrates independent state management
    /// Each instance maintains its own count, even when switching workspaces
    /// </summary>
    public class CounterWidget : WidgetBase, IThemeable
    {
        private Border containerBorder;
        private TextBlock titleText;
        private TextBlock countText;
        private TextBlock instructionText;

        private int count = 0;
        public int Count
        {
            get => count;
            set
            {
                count = value;
                OnPropertyChanged(nameof(Count));
                UpdateDisplay();
            }
        }

        public CounterWidget()
        {
            WidgetType = "Counter";
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Title
            titleText = new TextBlock
            {
                Text = "COUNTER",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Count display
            countText = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.SyntaxKeyword),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Instructions
            instructionText = new TextBlock
            {
                Text = "Press Up/Down arrows",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(countText);
            stackPanel.Children.Add(instructionText);

            containerBorder.Child = stackPanel;
            this.Content = containerBorder;
        }

        public override void Initialize()
        {
            Count = 0;
        }

        public override void OnWidgetKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    Count++;
                    e.Handled = true;
                    break;

                case Key.Down:
                    Count--;
                    e.Handled = true;
                    break;

                case Key.R:
                    Count = 0;
                    e.Handled = true;
                    break;
            }
        }

        public override void OnWidgetFocusReceived()
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            instructionText.Foreground = new SolidColorBrush(theme.Focus);
        }

        public override void OnWidgetFocusLost()
        {
            var theme = ThemeManager.Instance.CurrentTheme;
            instructionText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
        }

        private void UpdateDisplay()
        {
            countText.Text = Count.ToString();
        }

        public override System.Collections.Generic.Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            state["Count"] = Count;
            return state;
        }

        public override void RestoreState(System.Collections.Generic.Dictionary<string, object> state)
        {
            if (state.ContainsKey("Count"))
            {
                Count = (int)state["Count"];
            }
        }

        protected override void OnDispose()
        {
            // No resources to dispose currently
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
                containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
            }

            if (countText != null)
            {
                countText.Foreground = new SolidColorBrush(theme.SyntaxKeyword);
            }

            if (instructionText != null)
            {
                // Update based on focus state
                instructionText.Foreground = new SolidColorBrush(
                    HasFocus ? theme.Focus : theme.ForegroundDisabled);
            }
        }
    }
}


using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Simple notes widget - demonstrates text input and state preservation
    /// </summary>
    public class NotesWidget : WidgetBase, IThemeable
    {
        private Border containerBorder;
        private TextBlock titleText;
        private TextBox notesTextBox;

        public NotesWidget()
        {
            WidgetType = "Notes";
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel();

            // Title
            titleText = new TextBlock
            {
                Text = "NOTES",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Text box
            notesTextBox = new TextBox
            {
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 100
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(notesTextBox);

            containerBorder.Child = stackPanel;
            this.Content = containerBorder;
        }

        public override void Initialize()
        {
            notesTextBox.Text = "Type your notes here...";
        }

        public override void OnWidgetFocusReceived()
        {
            // Auto-focus the textbox when widget is focused
            Dispatcher.BeginInvoke(new Action(() =>
            {
                notesTextBox.Focus();
                notesTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        public override System.Collections.Generic.Dictionary<string, object> SaveState()
        {
            var state = base.SaveState();
            state["Notes"] = notesTextBox.Text;
            return state;
        }

        public override void RestoreState(System.Collections.Generic.Dictionary<string, object> state)
        {
            if (state.ContainsKey("Notes"))
            {
                notesTextBox.Text = (string)state["Notes"];
            }
        }

        protected override void OnDispose()
        {
            // No resources to dispose currently
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
                containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
            }

            if (notesTextBox != null)
            {
                notesTextBox.Background = new SolidColorBrush(theme.Surface);
                notesTextBox.Foreground = new SolidColorBrush(theme.Foreground);
                notesTextBox.BorderBrush = new SolidColorBrush(theme.Border);
            }
        }
    }
}


using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Widget showing task count summary
    /// </summary>
    public class TaskSummaryWidget : WidgetBase, IThemeable
    {
        // This would normally come from a service
        // For demo purposes, we'll create a simple data structure
        public class TaskData
        {
            public int TotalTasks { get; set; }
            public int CompletedTasks { get; set; }
            public int PendingTasks { get; set; }
            public int OverdueTasks { get; set; }
        }

        private TaskData taskData;
        public TaskData Data
        {
            get => taskData;
            set
            {
                taskData = value;
                OnPropertyChanged(nameof(Data));
                UpdateDisplay();
            }
        }

        private Border containerBorder;
        private TextBlock titleText;
        private StackPanel contentPanel;

        public TaskSummaryWidget()
        {
            WidgetType = "TaskSummary";
            BuildUI();
        }

        private void BuildUI()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            containerBorder = new Border
            {
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };

            contentPanel = new StackPanel();

            // Title
            titleText = new TextBlock
            {
                Text = "TASKS",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.ForegroundDisabled),
                Margin = new Thickness(0, 0, 0, 10)
            };
            contentPanel.Children.Add(titleText);

            containerBorder.Child = contentPanel;
            this.Content = containerBorder;
        }

        public override void Initialize()
        {
            // Initialize with sample data
            // In real implementation, this would come from TaskService
            Data = new TaskData
            {
                TotalTasks = 15,
                CompletedTasks = 7,
                PendingTasks = 6,
                OverdueTasks = 2
            };
        }

        private void UpdateDisplay()
        {
            // Clear existing items (except title)
            while (contentPanel.Children.Count > 1)
                contentPanel.Children.RemoveAt(1);

            if (Data == null) return;

            var theme = ThemeManager.Instance.CurrentTheme;

            // Add stat items using theme colors
            AddStatItem("Total", Data.TotalTasks.ToString(), theme.Info);
            AddStatItem("Completed", Data.CompletedTasks.ToString(), theme.Success);
            AddStatItem("Pending", Data.PendingTasks.ToString(), theme.Primary);
            AddStatItem("Overdue", Data.OverdueTasks.ToString(), theme.Error);
        }

        private void AddStatItem(string label, string value, Color color)
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var labelText = new TextBlock
            {
                Text = label + ":",
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                Foreground = new SolidColorBrush(theme.Foreground),
                Width = 100
            };

            var valueText = new TextBlock
            {
                Text = value,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            };

            itemPanel.Children.Add(labelText);
            itemPanel.Children.Add(valueText);
            contentPanel.Children.Add(itemPanel);
        }

        public override void Refresh()
        {
            // In real implementation, fetch latest data from TaskService
            // For now, simulate data change
            if (Data != null)
            {
                var random = new Random();
                Data = new TaskData
                {
                    TotalTasks = Data.TotalTasks,
                    CompletedTasks = random.Next(0, Data.TotalTasks),
                    PendingTasks = random.Next(0, Data.TotalTasks),
                    OverdueTasks = random.Next(0, 5)
                };
            }
        }

        protected override void OnDispose()
        {
            // No resources to dispose currently
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            if (containerBorder != null)
            {
                containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);
                containerBorder.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(theme.ForegroundDisabled);
            }

            // Rebuild display with new theme colors
            if (Data != null)
            {
                UpdateDisplay();
            }
        }
    }
}

