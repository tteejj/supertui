# SuperTUI Remediation Plan
**Created:** 2025-10-24
**Target:** Production-Ready v1.0
**Timeline:** 4-6 weeks

---

## Overview

This plan transforms SuperTUI from a functional prototype into a production-ready framework by addressing the 10 critical issues identified in the analysis report. The plan is organized into 4 phases, each with clear deliverables and success criteria.

**Total Estimated Effort:** 21-25 days (4-6 weeks with testing and documentation)

---

## Phase 1: Critical Security Fixes (Week 1)
**Goal:** Eliminate critical security vulnerabilities
**Duration:** 5 days
**Priority:** IMMEDIATE - Required before any deployment

### Issue #1: SecurityManager Config Bypass (1 day)

**Location:** `WPF/Core/Infrastructure/SecurityManager.cs`

**Current Problem:**
```csharp
if (!ConfigurationManager.Instance.Get<bool>("Security.ValidateFileAccess", true))
{
    return true; // Security disabled!
}
```

**Fix Strategy:**
1. Remove config-based bypass - make security always-on
2. Add `SecurityMode` enum: `Strict`, `Permissive`, `Development`
3. SecurityMode set at initialization, immutable afterward
4. Add symlink resolution using `Path.GetFullPath()` + File.ResolveLinkTarget()
5. Add canonicalization check to prevent `..` bypasses

**Implementation:**
```csharp
public enum SecurityMode
{
    Strict,      // Production - all validation enabled
    Permissive,  // Allow UNC paths, larger files
    Development  // Minimal validation (logs warnings)
}

public class SecurityManager
{
    private SecurityMode mode;
    private bool isInitialized = false;

    public void Initialize(SecurityMode mode = SecurityMode.Strict)
    {
        if (isInitialized)
            throw new InvalidOperationException("SecurityManager already initialized");

        this.mode = mode;
        isInitialized = true;
        // ... rest of initialization
    }

    public bool ValidateFileAccess(string path, bool checkWrite = false)
    {
        // No config bypass - always validate based on mode
        if (mode == SecurityMode.Development)
        {
            Logger.Instance.Warning("Security", $"DEV MODE: Allowing {path}");
            return true;
        }

        // Resolve symlinks (Windows 10+)
        string resolvedPath = ResolveSymlinks(path);

        // Canonicalize to prevent ../ bypasses
        string canonicalPath = Path.GetFullPath(resolvedPath);

        // ... rest of validation
    }
}
```

**Testing:**
- [ ] Path traversal attacks (`../../../etc/passwd`)
- [ ] Symlink attacks (create symlink outside allowed dirs)
- [ ] UNC path handling (`\\server\share`)
- [ ] Config manipulation cannot bypass security

**Success Criteria:**
- Security cannot be disabled via config
- All path traversal attacks blocked
- Symlinks resolved before validation
- Mode documented in security docs

---

### Issue #3: FileExplorer Arbitrary Execution (1 day)

**Location:** `WPF/Widgets/FileExplorerWidget.cs:272-287`

**Current Problem:**
```csharp
Process.Start(new ProcessStartInfo
{
    FileName = file.FullName,      // ANY FILE
    UseShellExecute = true         // EXECUTES IT
});
```

**Fix Strategy:**
1. Define safe file extensions (read-only: .txt, .md, .pdf, .jpg, etc.)
2. Define dangerous extensions (executable: .exe, .bat, .ps1, .cmd, .vbs, .js)
3. Add confirmation dialog for dangerous files
4. Integrate with SecurityManager.ValidateFileAccess()
5. Add "Preview" vs "Execute" modes

**Implementation:**
```csharp
private static readonly HashSet<string> SafeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".txt", ".md", ".log", ".json", ".xml", ".yaml",
    ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp",
    ".mp3", ".mp4", ".avi", ".mov"
};

private static readonly HashSet<string> DangerousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".scr", ".pif"
};

private void OpenSelectedItem()
{
    if (!(item is FileInfo file))
        return;

    // Security validation
    if (!SecurityManager.Instance.ValidateFileAccess(file.FullName, checkWrite: false))
    {
        UpdateStatus("Access denied by security policy", theme.Error);
        return;
    }

    string ext = file.Extension.ToLowerInvariant();

    // Block dangerous files entirely (or show confirmation)
    if (DangerousExtensions.Contains(ext))
    {
        var result = MessageBox.Show(
            $"'{file.Name}' is an executable file.\n\nExecuting this file could be dangerous. Are you sure?",
            "Security Warning",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;
    }

    // Publish event (other widgets can listen)
    EventBus.Instance.Publish(new FileSelectedEvent { FilePath = file.FullName, ... });

    // Open with shell (user has confirmed if dangerous)
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = file.FullName,
            UseShellExecute = true
        });
        UpdateStatus($"Opened: {file.Name}", theme.Success);
    }
    catch (Exception ex)
    {
        UpdateStatus($"Cannot open: {ex.Message}", theme.Error);
    }
}
```

**Testing:**
- [ ] Open .txt file → opens in notepad
- [ ] Open .exe file → shows warning dialog
- [ ] Cancel warning → file not executed
- [ ] Open file outside allowed dirs → blocked by SecurityManager
- [ ] Disguised executable (`report.pdf.exe`) → detected and warned

**Success Criteria:**
- Dangerous files require explicit confirmation
- SecurityManager integration works
- Users cannot be tricked by double extensions
- All file opens logged with full path

---

### Issue #2: Document Plugin Limitations (1 day)

**Location:** `WPF/Core/Extensions.cs:862-926`

**Current Problem:**
- Plugins loaded with `Assembly.LoadFrom()` cannot be unloaded
- No documentation of this limitation
- No security model for plugins

**Fix Strategy:**
1. Document the limitation clearly
2. Add plugin manifest validation
3. Add signature verification (optional but recommended)
4. Create plugin development guide
5. Add `[SecurityCritical]` attributes to plugin API

**Implementation:**
```csharp
/// <summary>
/// Plugin manager for loading and managing extensions.
///
/// LIMITATIONS:
/// - Plugins cannot be unloaded once loaded (requires app restart)
/// - Plugins have full access to SuperTUI internals
/// - Malicious plugins can compromise the entire application
///
/// SECURITY:
/// - Only load plugins from trusted sources
/// - Consider requiring signed assemblies in production
/// - Plugins should be code-reviewed before deployment
///
/// For true plugin isolation, consider migrating to .NET 6+ with AssemblyLoadContext.
/// </summary>
public class PluginManager
{
    public void LoadPlugin(string assemblyPath)
    {
        // Validate plugin manifest
        if (!ValidatePluginManifest(assemblyPath))
        {
            Logger.Instance.Error("PluginManager", $"Invalid plugin manifest: {assemblyPath}");
            return;
        }

        // Optional: Check digital signature
        if (ConfigurationManager.Instance.Get<bool>("Security.RequireSignedPlugins", false))
        {
            if (!VerifyPluginSignature(assemblyPath))
            {
                Logger.Instance.Error("PluginManager", $"Plugin signature verification failed: {assemblyPath}");
                return;
            }
        }

        // Security validation
        if (!SecurityManager.Instance.ValidateFileAccess(assemblyPath))
        {
            Logger.Instance.Warning("PluginManager", $"Security validation failed for plugin: {assemblyPath}");
            return;
        }

        // Log warning about unloadability
        Logger.Instance.Warning("PluginManager",
            $"Loading plugin {assemblyPath}. WARNING: Plugin will remain in memory until app restart.");

        var assembly = Assembly.LoadFrom(assemblyPath);
        // ... rest of loading logic
    }
}
```

**Deliverables:**
- [ ] Plugin development guide (PLUGIN_GUIDE.md)
- [ ] Security documentation for plugins
- [ ] Example secure plugin
- [ ] Plugin manifest schema

**Success Criteria:**
- Limitations clearly documented
- Plugin developers know security implications
- Signature verification option available
- Security best practices documented

---

### Phase 1 Testing & Documentation

**Test Suite:**
1. Security test suite (pytest or xUnit)
   - Path traversal tests
   - Symlink tests
   - Config bypass tests
   - FileExplorer execution tests

2. Security documentation
   - `SECURITY.md` - security model overview
   - `PLUGIN_GUIDE.md` - plugin development guide
   - Update `README.md` with security notes

**Deliverables:**
- [ ] Security test suite (15+ tests)
- [ ] SECURITY.md documentation
- [ ] PLUGIN_GUIDE.md documentation
- [ ] All Phase 1 issues marked FIXED

---

## Phase 2: Reliability Improvements (Week 2)
**Goal:** Fix data loss and crash issues
**Duration:** 6 days
**Priority:** HIGH - Required for production use

### Issue #4: Logger Silent Log Dropping (2 days)

**Location:** `WPF/Core/Infrastructure/Logger.cs:224-239`

**Current Problem:**
- Logs dropped silently under load
- No backpressure mechanism
- Critical events may be lost

**Fix Strategy:**
1. Add priority queues (Critical/Error never dropped)
2. Add configurable behavior: `Drop`, `Block`, `Throttle`
3. Add metrics for dropped logs
4. Separate audit log sink (security events)

**Implementation:**
```csharp
public class FileLogSink : ILogSink, IDisposable
{
    // Separate queues by priority
    private readonly BlockingCollection<LogEntry> criticalQueue;
    private readonly BlockingCollection<LogEntry> normalQueue;
    private readonly LogDropPolicy dropPolicy;

    public enum LogDropPolicy
    {
        DropOldest,    // Drop oldest normal logs when full
        BlockCaller,   // Block Write() call until queue has space
        Throttle       // Slow down logging rate
    }

    public void Write(LogEntry entry)
    {
        // Critical logs NEVER dropped
        if (entry.Level >= LogLevel.Error)
        {
            if (!criticalQueue.TryAdd(entry, millisecondsTimeout: 5000))
            {
                // If even critical queue is full, this is serious
                throw new InvalidOperationException("Critical log queue overflow! Disk may be full or slow.");
            }
            return;
        }

        // Normal logs respect policy
        bool added = false;
        switch (dropPolicy)
        {
            case LogDropPolicy.DropOldest:
                if (!normalQueue.TryAdd(entry, millisecondsTimeout: 0))
                {
                    droppedLogCount++;
                    if (normalQueue.TryTake(out _))  // Remove oldest
                    {
                        normalQueue.TryAdd(entry, millisecondsTimeout: 0);
                    }
                }
                break;

            case LogDropPolicy.BlockCaller:
                normalQueue.Add(entry);  // Blocks until space available
                break;

            case LogDropPolicy.Throttle:
                if (!normalQueue.TryAdd(entry, millisecondsTimeout: 100))
                {
                    droppedLogCount++;
                }
                break;
        }
    }

    // Expose metrics
    public long GetDroppedLogCount() => Interlocked.Read(ref droppedLogCount);
    public int GetQueueDepth() => normalQueue.Count + criticalQueue.Count;
}
```

**Testing:**
- [ ] Write 100,000 logs rapidly → measure drop rate
- [ ] Critical logs never dropped even under extreme load
- [ ] Metrics exposed via API
- [ ] Different policies tested

**Success Criteria:**
- Critical logs (Error, Critical) never dropped
- Drop rate < 0.1% under normal load
- Metrics available for monitoring
- Policy configurable per deployment

---

### Issue #6: ConfigurationManager Type Conversion (2 days)

**Location:** `WPF/Core/Infrastructure/ConfigurationManager.cs:147-244`

**Current Problem:**
```csharp
var extensions = config.Get<List<string>>("Security.AllowedExtensions");
// CRASHES because JsonElement != List<string>
```

**Fix Strategy:**
1. Simplify type conversion logic
2. Add explicit support for collections
3. Add schema validation
4. Fail fast on unsupported types
5. Add unit tests for all supported types

**Implementation:**
```csharp
public T Get<T>(string key, T defaultValue = default)
{
    if (!config.TryGetValue(key, out var configValue))
        return defaultValue;

    try
    {
        // Fast path: exact type match
        if (configValue.Value is T directValue)
            return directValue;

        // Null handling
        if (configValue.Value == null)
            return defaultValue;

        var targetType = typeof(T);

        // JsonElement from file deserialization
        if (configValue.Value is JsonElement jsonElement)
            return DeserializeJsonElement<T>(jsonElement);

        // Primitives and strings
        if (IsPrimitiveOrString(targetType))
            return (T)Convert.ChangeType(configValue.Value, targetType);

        // Enums
        if (targetType.IsEnum)
            return ParseEnum<T>(configValue.Value);

        // Collections (List<T>, Dictionary<K,V>, T[])
        if (IsCollection(targetType))
            return DeserializeCollection<T>(configValue.Value);

        // Complex objects
        if (targetType.IsClass)
            return DeserializeObject<T>(configValue.Value);

        // Unsupported type
        throw new NotSupportedException(
            $"Configuration type {targetType.Name} is not supported. " +
            $"Supported types: primitives, strings, enums, List<T>, Dictionary<K,V>, arrays, and simple objects.");
    }
    catch (Exception ex)
    {
        Logger.Instance.Error("Config",
            $"Failed to convert config key '{key}' to {typeof(T).Name}: {ex.Message}", ex);

        // Fail fast in development, use default in production
        if (SecurityManager.Instance.Mode == SecurityMode.Development)
            throw;

        return defaultValue;
    }
}

private T DeserializeCollection<T>(object value)
{
    // Handle all collection types consistently
    var json = JsonSerializer.Serialize(value);
    return JsonSerializer.Deserialize<T>(json);
}
```

**Testing:**
- [ ] `List<string>` from JSON file
- [ ] `Dictionary<string, int>` from JSON file
- [ ] `int[]` from JSON file
- [ ] Nested objects
- [ ] Invalid JSON → throws in dev, logs + defaults in prod

**Success Criteria:**
- All collection types work from JSON files
- Clear error messages for unsupported types
- Unit test coverage > 90%
- Documentation of supported types

---

### Issue #5: State Persistence Fragility (2 days)

**Location:** `WPF/Core/Extensions.cs:426-520`

**Current Problem:**
- No schema validation
- Silent failures on invalid data
- Migration infrastructure unused

**Fix Strategy:**
1. Add JSON schema validation
2. Add integrity checks (checksums)
3. Create first real migration (1.0 → 1.1)
4. Add backup before migration
5. Add state validation on load

**Implementation:**
```csharp
public class StateSnapshot
{
    public string Version { get; set; } = StateVersion.Current;
    public DateTime Timestamp { get; set; }
    public string SchemaVersion { get; set; } = "1.0";
    public string Checksum { get; set; }  // SHA256 of data

    // Existing fields...

    /// <summary>
    /// Calculate checksum for integrity verification
    /// </summary>
    public void CalculateChecksum()
    {
        var data = JsonSerializer.Serialize(new {
            Version,
            Timestamp,
            ApplicationState,
            Workspaces,
            UserData
        });

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        Checksum = Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verify checksum matches data
    /// </summary>
    public bool VerifyChecksum()
    {
        var originalChecksum = Checksum;
        CalculateChecksum();
        var newChecksum = Checksum;
        Checksum = originalChecksum;
        return originalChecksum == newChecksum;
    }
}

public async Task<StateSnapshot> LoadStateAsync()
{
    try
    {
        if (!File.Exists(currentStateFile))
            return null;

        string json = await File.ReadAllTextAsync(currentStateFile, Encoding.UTF8);
        var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json);

        // Integrity check
        if (!string.IsNullOrEmpty(snapshot.Checksum))
        {
            if (!snapshot.VerifyChecksum())
            {
                Logger.Instance.Error("StatePersistence", "State file corrupted (checksum mismatch)!");

                // Try to restore from backup
                var backups = GetAvailableBackups();
                if (backups.Count > 0)
                {
                    Logger.Instance.Info("StatePersistence", "Attempting restore from backup...");
                    snapshot = RestoreFromBackup(backups[0]);
                }
                else
                {
                    throw new InvalidOperationException("State file corrupted and no backups available");
                }
            }
        }

        // Version migration
        if (snapshot.Version != StateVersion.Current)
        {
            // Backup before migration
            await CreateBackupAsync();

            snapshot = migrationManager.MigrateToCurrentVersion(snapshot);

            // Save migrated state
            await SaveStateAsync(snapshot, createBackup: false);
        }

        return snapshot;
    }
    catch (Exception ex)
    {
        Logger.Instance.Error("StatePersistence", $"Failed to load state: {ex.Message}", ex);
        throw;  // Don't silently continue with corrupt state
    }
}

// Example migration
public class Migration_1_0_to_1_1 : IStateMigration
{
    public string FromVersion => "1.0";
    public string ToVersion => "1.1";

    public StateSnapshot Migrate(StateSnapshot snapshot)
    {
        Logger.Instance.Info("StateMigration", "Migrating from 1.0 to 1.1");

        // Add new field: EnableAnimations
        if (!snapshot.ApplicationState.ContainsKey("EnableAnimations"))
        {
            snapshot.ApplicationState["EnableAnimations"] = true;
        }

        // Transform workspace data
        foreach (var workspace in snapshot.Workspaces)
        {
            // Ensure all widgets have WidgetId (add if missing)
            foreach (var widgetState in workspace.WidgetStates)
            {
                if (!widgetState.ContainsKey("WidgetId"))
                {
                    widgetState["WidgetId"] = Guid.NewGuid().ToString();
                    Logger.Instance.Info("StateMigration", "Generated WidgetId for legacy widget");
                }
            }
        }

        snapshot.Version = "1.1";
        return snapshot;
    }
}
```

**Testing:**
- [ ] Load valid state → succeeds
- [ ] Load corrupted state (modify JSON) → detects checksum failure
- [ ] Load v1.0 state → migrates to v1.1
- [ ] Migration failure → restores from backup
- [ ] Missing backup → fails gracefully with error message

**Success Criteria:**
- Checksum validation detects corruption
- First migration (1.0 → 1.1) works
- Backup created before migration
- Clear error messages on corruption

---

### Phase 2 Testing & Documentation

**Test Suite:**
1. Reliability test suite
   - Logger stress tests (100k logs/sec)
   - Config deserialization tests (all types)
   - State migration tests (1.0 → 1.1)
   - Corruption detection tests

2. Performance benchmarks
   - Logger throughput
   - Config access speed
   - State save/load time

**Deliverables:**
- [ ] Reliability test suite (25+ tests)
- [ ] Performance benchmark results
- [ ] Migration guide for users
- [ ] All Phase 2 issues marked FIXED

---

## Phase 3: Architecture Improvements (Week 3-4)
**Goal:** Improve testability and maintainability
**Duration:** 8 days
**Priority:** MEDIUM - Required for long-term maintainability

### Issue #7: Replace Singletons with DI (5 days)

**Location:** Throughout `Infrastructure/*`

**Current Problem:**
```csharp
Logger.Instance.Info(...);
ConfigurationManager.Instance.Get(...);
ThemeManager.Instance.ApplyTheme(...);
// Untestable, hidden dependencies, global state
```

**Fix Strategy:**
1. Create service container (already exists at `WPF/Core/Infrastructure/ServiceContainer.cs`)
2. Define interfaces for all services (partially done)
3. Convert widgets to accept dependencies via constructor
4. Maintain backward compatibility with .Instance for legacy code
5. Gradual migration path (both patterns work during transition)

**Implementation:**

**Step 1: Service Registration (Day 1)**
```csharp
// WPF/Core/DI/ServiceRegistration.cs (already exists, enhance it)
public static class ServiceRegistration
{
    public static ServiceContainer RegisterCoreServices(this ServiceContainer container)
    {
        // Register as singleton implementations
        container.RegisterSingleton<ILogger, Logger>();
        container.RegisterSingleton<IConfigurationManager, ConfigurationManager>();
        container.RegisterSingleton<IThemeManager, ThemeManager>();
        container.RegisterSingleton<ISecurityManager, SecurityManager>();
        container.RegisterSingleton<IEventBus, EventBus>();

        // Initialize singletons
        var logger = container.Resolve<ILogger>();
        var config = container.Resolve<IConfigurationManager>();
        var theme = container.Resolve<IThemeManager>();
        var security = container.Resolve<ISecurityManager>();

        // Initialize with dependencies
        config.Initialize("config.json");
        theme.Initialize();
        security.Initialize();

        return container;
    }
}
```

**Step 2: Backward Compatibility Bridge (Day 2)**
```csharp
// Keep .Instance pattern for backward compatibility
public class Logger : ILogger
{
    private static Logger instance;
    public static Logger Instance
    {
        get
        {
            if (instance == null)
            {
                // Fallback: create if not registered in DI
                instance = new Logger();
            }
            return instance;
        }
        internal set => instance = value;  // Allow DI to set
    }

    // Rest of implementation...
}

// In ServiceContainer:
public T RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface, new()
{
    var instance = new TImplementation();
    services[typeof(TInterface)] = instance;

    // Special handling for infrastructure singletons
    if (instance is Logger logger)
        Logger.Instance = logger;
    if (instance is ConfigurationManager config)
        ConfigurationManager.Instance = config;
    // ... etc

    return instance;
}
```

**Step 3: Update Widgets (Day 3-4)**
```csharp
// NEW: Constructor injection
public class ClockWidget : WidgetBase, IThemeable
{
    private readonly IThemeManager themeManager;
    private readonly ILogger logger;

    // Constructor for DI
    public ClockWidget(IThemeManager themeManager, ILogger logger)
    {
        this.themeManager = themeManager;
        this.logger = logger;

        WidgetType = "Clock";
        BuildUI();
    }

    // Legacy constructor for backward compatibility
    public ClockWidget() : this(ThemeManager.Instance, Logger.Instance)
    {
    }

    private void BuildUI()
    {
        var theme = themeManager.CurrentTheme;  // Use injected dependency
        // ...
    }
}

// Widget factory for DI
public class WidgetFactory
{
    private readonly ServiceContainer container;

    public WidgetFactory(ServiceContainer container)
    {
        this.container = container;
    }

    public T CreateWidget<T>() where T : WidgetBase
    {
        // Resolve dependencies and create widget
        return container.Resolve<T>();
    }
}
```

**Step 4: Update Application (Day 5)**
```csharp
// WPF/SuperTUI.ps1 - Updated initialization
$container = New-Object SuperTUI.Core.ServiceContainer
$container.RegisterCoreServices()

# Register widget types
$container.RegisterTransient([SuperTUI.Widgets.ClockWidget])
$container.RegisterTransient([SuperTUI.Widgets.TodoWidget])

# Get services
$logger = $container.Resolve([SuperTUI.Infrastructure.ILogger])
$config = $container.Resolve([SuperTUI.Infrastructure.IConfigurationManager])

# Create widgets via factory
$widgetFactory = New-Object SuperTUI.Core.WidgetFactory($container)
$clockWidget = $widgetFactory.CreateWidget([SuperTUI.Widgets.ClockWidget])
```

**Testing:**
- [ ] Create widget via DI → receives dependencies
- [ ] Create widget via new() → uses .Instance fallback
- [ ] Mock ILogger in unit test → widget uses mock
- [ ] Multiple widgets share same logger instance

**Success Criteria:**
- All infrastructure services registered in DI
- Widgets support constructor injection
- Backward compatibility maintained (.Instance still works)
- Unit tests can mock dependencies
- Migration guide for developers

---

### Issue #8: Error Handling Policy (2 days)

**Current Problem:**
- Inconsistent error handling
- Silent failures
- No user feedback

**Fix Strategy:**
1. Define error handling policy
2. Create error categories
3. Add user-visible error notifications
4. Add error recovery strategies

**Implementation:**

**Step 1: Error Policy (Day 1)**
```csharp
// WPF/Core/Infrastructure/ErrorPolicy.cs
public enum ErrorSeverity
{
    Recoverable,    // Log warning, continue
    Degraded,       // Log error, disable feature, continue
    Fatal           // Log critical, show error, exit
}

public enum ErrorCategory
{
    Configuration,  // Config file errors
    IO,            // File I/O errors
    Network,       // Network errors
    Security,      // Security violations
    Plugin,        // Plugin errors
    Widget,        // Widget errors
    Internal       // Internal framework errors
}

public class ErrorHandlingPolicy
{
    public static ErrorSeverity GetSeverity(ErrorCategory category, Exception ex)
    {
        switch (category)
        {
            case ErrorCategory.Security:
                return ErrorSeverity.Fatal;  // Security errors always fatal

            case ErrorCategory.Configuration:
                if (ex is FileNotFoundException)
                    return ErrorSeverity.Recoverable;  // Use defaults
                return ErrorSeverity.Degraded;  // Invalid config → limited functionality

            case ErrorCategory.Widget:
                return ErrorSeverity.Degraded;  // Widget errors shouldn't crash app

            case ErrorCategory.IO:
                if (ex is UnauthorizedAccessException)
                    return ErrorSeverity.Fatal;
                return ErrorSeverity.Recoverable;  // Retry or skip

            default:
                return ErrorSeverity.Degraded;
        }
    }

    public static void Handle(ErrorCategory category, Exception ex, string context)
    {
        var severity = GetSeverity(category, ex);

        switch (severity)
        {
            case ErrorSeverity.Recoverable:
                Logger.Instance.Warning(category.ToString(), $"{context}: {ex.Message}");
                break;

            case ErrorSeverity.Degraded:
                Logger.Instance.Error(category.ToString(), $"{context}: {ex.Message}", ex);
                NotifyUser($"Error in {category}: {ex.Message}", severity);
                break;

            case ErrorSeverity.Fatal:
                Logger.Instance.Critical(category.ToString(), $"FATAL: {context}", ex);
                NotifyUser($"Critical error: {ex.Message}\n\nApplication will exit.", severity);
                Environment.Exit(1);
                break;
        }
    }

    private static void NotifyUser(string message, ErrorSeverity severity)
    {
        var icon = severity == ErrorSeverity.Fatal
            ? MessageBoxImage.Error
            : MessageBoxImage.Warning;

        MessageBox.Show(message, "SuperTUI Error", MessageBoxButton.OK, icon);
    }
}
```

**Step 2: Apply Policy Throughout Codebase (Day 2)**
```csharp
// Example: ConfigurationManager
public void LoadFromFile(string path)
{
    try
    {
        string json = File.ReadAllText(path);
        // ... deserialize
    }
    catch (FileNotFoundException ex)
    {
        ErrorHandlingPolicy.Handle(
            ErrorCategory.Configuration,
            ex,
            $"Config file not found: {path}. Using defaults.");
        // Continue with default config
    }
    catch (JsonException ex)
    {
        ErrorHandlingPolicy.Handle(
            ErrorCategory.Configuration,
            ex,
            $"Invalid config file: {path}");
        // Continue with degraded functionality
    }
}

// Example: SecurityManager
public bool ValidateFileAccess(string path)
{
    try
    {
        // ... validation
    }
    catch (Exception ex)
    {
        ErrorHandlingPolicy.Handle(
            ErrorCategory.Security,
            ex,
            $"Security validation failed for: {path}");
        return false;  // Deny by default
    }
}
```

**Testing:**
- [ ] Recoverable error → logs warning, continues
- [ ] Degraded error → shows message box, continues
- [ ] Fatal error → shows error, exits
- [ ] Security error → always treated as fatal

**Success Criteria:**
- Consistent error handling across codebase
- User-visible error notifications
- Error categories documented
- Clear recovery strategies

---

### Issue #10: Widget Resource Leaks (1 day)

**Location:** Multiple widgets

**Current Problem:**
```csharp
protected override void OnDispose()
{
    if (timer != null)
    {
        timer.Stop();
        timer.Tick -= Timer_Tick;
        timer = null;  // Missing: timer.Dispose()
    }
}
```

**Fix Strategy:**
1. Add Dispose() calls to all IDisposable resources
2. Add using statements where appropriate
3. Add finalizers for critical resources
4. Create dispose checklist for widget developers

**Implementation:**
```csharp
public class ClockWidget : WidgetBase, IThemeable
{
    private DispatcherTimer timer;

    protected override void OnDispose()
    {
        if (timer != null)
        {
            timer.Stop();
            timer.Tick -= Timer_Tick;
            timer.Dispose();  // ADDED
            timer = null;
        }

        base.OnDispose();
    }
}

// Add dispose checklist to widget base
public abstract class WidgetBase : UserControl, IWidget
{
    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                OnDispose();

                // Verify disposal (development mode only)
                if (SecurityManager.Instance.Mode == SecurityMode.Development)
                {
                    VerifyDisposal();
                }
            }

            disposed = true;
        }
    }

    private void VerifyDisposal()
    {
        // Use reflection to find undisposed IDisposable fields
        var disposableFields = this.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => typeof(IDisposable).IsAssignableFrom(f.FieldType))
            .ToList();

        foreach (var field in disposableFields)
        {
            var value = field.GetValue(this);
            if (value != null)
            {
                Logger.Instance.Warning("WidgetDisposal",
                    $"Widget {WidgetName} has undisposed field: {field.Name}");
            }
        }
    }
}
```

**Testing:**
- [ ] Create widget, dispose → no resource leaks (memory profiler)
- [ ] Development mode → warns about undisposed fields
- [ ] All widgets dispose properly
- [ ] Repeated create/dispose cycles → memory stable

**Success Criteria:**
- All IDisposable resources properly disposed
- Development mode detects leaks
- Widget developer guide includes dispose checklist
- Memory profiler shows no leaks

---

### Phase 3 Testing & Documentation

**Test Suite:**
1. Unit tests with DI
   - Mock logger in widget test
   - Mock config in security test
   - Test error handling policy

2. Integration tests
   - Full application startup with DI
   - Widget creation via factory
   - Error scenarios

**Deliverables:**
- [ ] Unit test suite with mocking (30+ tests)
- [ ] DI migration guide for developers
- [ ] Error handling documentation
- [ ] Widget development checklist
- [ ] All Phase 3 issues marked FIXED

---

## Phase 4: Production Readiness (Week 4-5)
**Goal:** Final hardening and testing
**Duration:** 5 days
**Priority:** HIGH - Polish before release

### Documentation (2 days)

**Deliverables:**
1. **SECURITY.md** - Complete security model documentation
2. **ARCHITECTURE.md** - Architecture overview with DI patterns
3. **CONTRIBUTING.md** - Developer guide (error handling, testing, DI)
4. **TESTING.md** - Testing guide and test coverage report
5. **CHANGELOG.md** - Document all changes from prototype to v1.0

### Integration Testing (2 days)

**Test Scenarios:**
1. Complete user workflows
   - Launch app → create workspace → add widgets → save state → exit
   - Restart → verify state restored correctly
   - Switch workspaces → verify state preserved
   - Close widgets → verify no memory leaks

2. Error scenarios
   - Corrupt config file → app starts with defaults
   - Corrupt state file → restores from backup
   - Invalid plugin → logs error, continues
   - Security violation → blocks access, logs audit event

3. Performance testing
   - 100 widgets → measure memory and CPU
   - 10,000 logs/sec → measure drop rate
   - State with 50 workspaces → save/load time

### Security Audit (1 day)

**Audit Checklist:**
- [ ] All Process.Start() calls reviewed
- [ ] All file paths validated via SecurityManager
- [ ] All user inputs sanitized
- [ ] No SQL injection vectors (if database added)
- [ ] No XXE vulnerabilities (XML parsing)
- [ ] No deserialize untrusted data
- [ ] All secrets in config encrypted (if applicable)

---

## Phase 5: Final Review & Release (Week 5-6)
**Goal:** Final validation and v1.0 release
**Duration:** Variable
**Priority:** CRITICAL - Last gate before release

### Pre-Release Checklist

**Code Quality:**
- [ ] All 10 critical issues FIXED
- [ ] Test coverage > 70%
- [ ] No compiler warnings
- [ ] No ReSharper/Rider critical issues
- [ ] Code review by 2+ developers

**Documentation:**
- [ ] README.md updated with v1.0 features
- [ ] SECURITY.md complete
- [ ] API documentation generated (XML docs)
- [ ] Migration guide for prototype users
- [ ] Known limitations documented

**Testing:**
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Performance benchmarks meet targets
- [ ] Security audit complete
- [ ] Manual testing on Windows 10/11

**Release Artifacts:**
- [ ] NuGet package built
- [ ] Release notes written
- [ ] GitHub release created
- [ ] Example applications updated
- [ ] Migration scripts provided

---

## Success Metrics

### Code Quality Metrics
- Test Coverage: > 70% (target)
- Critical Issues: 0
- High Issues: 0
- Medium Issues: < 5
- Code Review: 100% of changes

### Performance Metrics
- Logger Throughput: > 10,000 logs/sec
- Log Drop Rate: < 0.1%
- State Save/Load: < 500ms for typical workspace
- Memory: < 100MB for 50 widgets
- Startup Time: < 2 seconds

### Security Metrics
- Security Violations Blocked: 100%
- Audit Events Logged: 100%
- Known Vulnerabilities: 0
- Penetration Test: Pass

---

## Risk Management

### High-Risk Items
1. **DI Migration** - Could break existing code
   - Mitigation: Maintain backward compatibility, gradual rollout

2. **State Migration** - Could corrupt user data
   - Mitigation: Extensive testing, automatic backups

3. **Security Changes** - Could break legitimate use cases
   - Mitigation: Configurable security modes, clear documentation

### Rollback Plan
- Tag prototype version as `v0.9-prototype`
- Each phase tagged as `v1.0-alpha1`, `v1.0-alpha2`, etc.
- Breaking changes documented in CHANGELOG.md
- Rollback scripts for state file migrations

---

## Timeline Summary

| Phase | Duration | Start | End | Deliverables |
|-------|----------|-------|-----|--------------|
| Phase 1: Security | 5 days | Day 1 | Day 5 | Security fixes, tests, docs |
| Phase 2: Reliability | 6 days | Day 6 | Day 11 | Logger, config, state fixes |
| Phase 3: Architecture | 8 days | Day 12 | Day 19 | DI, error policy, dispose fixes |
| Phase 4: Production | 5 days | Day 20 | Day 24 | Docs, integration tests, audit |
| Phase 5: Release | Variable | Day 25+ | TBD | Final review, release |

**Total:** 24+ days (5-6 weeks with testing and review)

---

## Next Steps

1. **Review this plan** with team/stakeholders
2. **Prioritize phases** based on deployment needs
3. **Set up test infrastructure** (xUnit, test data)
4. **Create feature branch** `feature/production-hardening`
5. **Begin Phase 1** with security fixes

---

## Appendix: Testing Strategy

### Unit Test Structure
```
WPF/
  Tests/
    Infrastructure/
      LoggerTests.cs
      ConfigurationManagerTests.cs
      SecurityManagerTests.cs
      ThemeManagerTests.cs
    Core/
      WorkspaceTests.cs
      LayoutEngineTests.cs
      StatePersistenceTests.cs
    Widgets/
      ClockWidgetTests.cs
      TodoWidgetTests.cs
      FileExplorerWidgetTests.cs
```

### Test Categories
- `[Category("Unit")]` - Fast, isolated unit tests
- `[Category("Integration")]` - Slower, multi-component tests
- `[Category("Performance")]` - Performance benchmarks
- `[Category("Security")]` - Security validation tests

### CI/CD Pipeline
1. On commit: Run unit tests
2. On PR: Run all tests + code coverage
3. On merge: Run performance tests
4. On release: Run security audit

---

## Document Control

**Version:** 1.0
**Last Updated:** 2025-10-24
**Owner:** Development Team
**Review Cycle:** Weekly during implementation

**Approval:**
- [ ] Technical Lead
- [ ] Security Team
- [ ] QA Team
- [ ] Product Owner
