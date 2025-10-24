# SuperTUI Infrastructure Guide

## Overview

The SuperTUI framework now includes comprehensive infrastructure for production-ready applications:

- **Logging System** - Multi-sink logging with file rotation and memory buffering
- **Configuration Management** - Type-safe settings with validation and persistence
- **Theme System** - Complete theming with hot-reloading
- **Security Layer** - Input validation, file access control, and sandboxing
- **State Persistence** - Application state saving with backup/restore and undo/redo
- **Plugin System** - Extensible architecture for third-party extensions
- **Performance Monitoring** - Operation tracking and metrics
- **Error Handling** - Resilient error handling with retry logic

---

## 1. Logging System

### Features
- Multiple log levels (Trace, Debug, Info, Warning, Error, Critical)
- Multiple sinks (File, Memory, extensible)
- Automatic file rotation
- Thread-safe operation
- Stack trace capture for errors
- Structured logging with properties

### Usage

```powershell
# Initialize logger
$logDir = Join-Path $env:LOCALAPPDATA "SuperTUI\Logs"
$fileLog = New-Object SuperTUI.Infrastructure.FileLogSink($logDir)
$memoryLog = New-Object SuperTUI.Infrastructure.MemoryLogSink(500)

[SuperTUI.Infrastructure.Logger]::Instance.AddSink($fileLog)
[SuperTUI.Infrastructure.Logger]::Instance.AddSink($memoryLog)

# Log messages
[SuperTUI.Infrastructure.Logger]::Instance.Info("MyCategory", "Application started")
[SuperTUI.Infrastructure.Logger]::Instance.Warning("MyCategory", "Potential issue detected")
[SuperTUI.Infrastructure.Logger]::Instance.Error("MyCategory", "Error occurred", $exception)

# With structured properties
$props = @{ UserId = "123"; Action = "Login" }
[SuperTUI.Infrastructure.Logger]::Instance.Log(
    [SuperTUI.Infrastructure.LogLevel]::Info,
    "Auth",
    "User logged in",
    $null,
    $props
)

# Get recent log entries (from memory sink)
$recentLogs = $memoryLog.GetEntries([SuperTUI.Infrastructure.LogLevel]::Warning, $null, 100)
```

### Log File Format
```
2025-10-24 14:32:15.123 [Info    ] [MyCategory] Application started
2025-10-24 14:32:16.456 [Warning ] [MyCategory] Potential issue detected
2025-10-24 14:32:17.789 [Error   ] [MyCategory] Error occurred
    Exception: InvalidOperationException: Something went wrong
    at MyClass.MyMethod() in C:\path\to\file.cs:line 42
```

### Configuration
- `App.LogLevel` - Minimum log level (default: Info)
- `App.LogDirectory` - Log file directory

---

## 2. Configuration Management

### Features
- Type-safe configuration values
- Default values and validation
- Hierarchical organization by category
- JSON persistence
- Change notifications
- Hot-reloading support

### Built-in Settings

#### Application
- `App.Title` - Application title
- `App.LogLevel` - Minimum log level
- `App.LogDirectory` - Log directory path
- `App.AutoSave` - Enable auto-save on exit
- `App.AutoSaveInterval` - Auto-save interval (seconds)

#### UI
- `UI.Theme` - Active theme name
- `UI.FontFamily` - Font family
- `UI.FontSize` - Font size (8-24)
- `UI.AnimationDuration` - Animation duration (ms)
- `UI.ShowLineNumbers` - Show line numbers in editors
- `UI.WordWrap` - Enable word wrap

#### Performance
- `Performance.MaxFPS` - Maximum FPS (1-144)
- `Performance.EnableVSync` - Enable vertical sync
- `Performance.LazyLoadThreshold` - Lazy loading threshold
- `Performance.VirtualizationEnabled` - UI virtualization

#### Security
- `Security.AllowScriptExecution` - Allow script execution
- `Security.ValidateFileAccess` - Validate file paths
- `Security.MaxFileSize` - Max file size (MB)
- `Security.AllowedExtensions` - Allowed file extensions

#### Backup
- `Backup.Enabled` - Enable automatic backups
- `Backup.Directory` - Backup directory
- `Backup.Interval` - Backup interval (seconds)
- `Backup.MaxBackups` - Maximum backup count
- `Backup.CompressBackups` - Compress backups

### Usage

```powershell
# Initialize
$configPath = Join-Path $env:LOCALAPPDATA "SuperTUI\config.json"
[SuperTUI.Infrastructure.ConfigurationManager]::Instance.Initialize($configPath)

# Get values
$theme = [SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get("UI.Theme", "Dark")
$fontSize = [SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get("UI.FontSize", 12)

# Set values
[SuperTUI.Infrastructure.ConfigurationManager]::Instance.Set("UI.Theme", "Light", $true)

# Register custom settings
[SuperTUI.Infrastructure.ConfigurationManager]::Instance.Register(
    "MyApp.Setting",
    "DefaultValue",
    "Description of the setting",
    "MyCategory",
    { param($value) $value -match "^[A-Z]" } # Validator
)

# Listen for changes
[SuperTUI.Infrastructure.ConfigurationManager]::Instance.add_ConfigChanged({
    param($sender, $args)
    Write-Host "Config changed: $($args.Key) = $($args.NewValue)"
})

# Get all settings in a category
$uiSettings = [SuperTUI.Infrastructure.ConfigurationManager]::Instance.GetCategory("UI")
```

---

## 3. Theme System

### Features
- Multiple built-in themes (Dark, Light)
- Custom theme loading from JSON
- Hot-reloading without restart
- Complete color palette (50+ colors)
- Syntax highlighting colors
- Theme change notifications

### Theme Structure

```csharp
public class Theme {
    // Identity
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

    // Backgrounds
    public Color Background { get; set; }
    public Color BackgroundSecondary { get; set; }
    public Color Surface { get; set; }
    public Color SurfaceHighlight { get; set; }

    // Text
    public Color Foreground { get; set; }
    public Color ForegroundSecondary { get; set; }
    public Color ForegroundDisabled { get; set; }

    // UI elements
    public Color Border { get; set; }
    public Color BorderActive { get; set; }
    public Color Focus { get; set; }
    public Color Selection { get; set; }
    public Color Hover { get; set; }
    public Color Active { get; set; }

    // Syntax highlighting
    public Color SyntaxKeyword { get; set; }
    public Color SyntaxString { get; set; }
    public Color SyntaxComment { get; set; }
    public Color SyntaxNumber { get; set; }
    public Color SyntaxFunction { get; set; }
    public Color SyntaxVariable { get; set; }
}
```

### Usage

```powershell
# Initialize
$themesDir = Join-Path $env:LOCALAPPDATA "SuperTUI\Themes"
[SuperTUI.Infrastructure.ThemeManager]::Instance.Initialize($themesDir)

# Get current theme
$theme = [SuperTUI.Infrastructure.ThemeManager]::Instance.CurrentTheme
$primaryColor = $theme.Primary

# Switch themes
[SuperTUI.Infrastructure.ThemeManager]::Instance.ApplyTheme("Light")
[SuperTUI.Infrastructure.ThemeManager]::Instance.ApplyTheme("Dark")

# List available themes
$themes = [SuperTUI.Infrastructure.ThemeManager]::Instance.GetAvailableThemes()
foreach ($t in $themes) {
    Write-Host "$($t.Name): $($t.Description)"
}

# Create custom theme
$customTheme = [SuperTUI.Infrastructure.Theme]::CreateDarkTheme()
$customTheme.Name = "MyTheme"
$customTheme.Primary = [System.Windows.Media.Color]::FromRgb(255, 100, 0)

# Register and save
[SuperTUI.Infrastructure.ThemeManager]::Instance.RegisterTheme($customTheme)
[SuperTUI.Infrastructure.ThemeManager]::Instance.SaveTheme($customTheme)

# Listen for theme changes
[SuperTUI.Infrastructure.ThemeManager]::Instance.add_ThemeChanged({
    param($sender, $args)
    Write-Host "Theme changed to: $($args.NewTheme.Name)"
    # Update UI colors here
})
```

### Custom Theme JSON Format

```json
{
  "Name": "MyTheme",
  "Description": "My custom theme",
  "IsDark": true,
  "Primary": {
    "R": 0,
    "G": 120,
    "B": 212,
    "A": 255
  },
  "Background": {
    "R": 12,
    "G": 12,
    "B": 12,
    "A": 255
  }
  // ... all other colors
}
```

---

## 4. Security Layer

### Features
- Input validation and sanitization
- File path validation
- Path traversal prevention
- File extension whitelist
- File size limits
- Script execution control

### Usage

```powershell
# Initialize
[SuperTUI.Infrastructure.SecurityManager]::Instance.Initialize()

# Add allowed directories
[SuperTUI.Infrastructure.SecurityManager]::Instance.AddAllowedDirectory("C:\Users\YourName\Documents")

# Validate file access
$path = "C:\Users\YourName\Documents\file.txt"
$isValid = [SuperTUI.Infrastructure.SecurityManager]::Instance.ValidateFileAccess($path)

if ($isValid) {
    # Safe to access file
    $content = Get-Content $path
}

# Input validation
$email = "user@example.com"
if ([SuperTUI.Infrastructure.ValidationHelper]::IsValidEmail($email)) {
    Write-Host "Valid email"
}

$filename = "my<>file|name?.txt"
$safe = [SuperTUI.Infrastructure.ValidationHelper]::SanitizeFilename($filename)
# Result: "my__file_name_.txt"

# Path validation
$userPath = "../../etc/passwd"  # Path traversal attempt
$isValid = [SuperTUI.Infrastructure.ValidationHelper]::IsValidPath($userPath)
# Result: $false

# Check if path is within allowed directory
$allowedDir = "C:\SafeArea"
$testPath = "C:\SafeArea\subfolder\file.txt"
$isWithin = [SuperTUI.Infrastructure.ValidationHelper]::IsWithinDirectory($testPath, $allowedDir)
# Result: $true
```

### Security Configuration

```powershell
# Disable script execution (recommended for production)
[SuperTUI.Infrastructure.ConfigurationManager]::Instance.Set(
    "Security.AllowScriptExecution",
    $false
)

# Set allowed file extensions
[SuperTUI.Infrastructure.ConfigurationManager]::Instance.Set(
    "Security.AllowedExtensions",
    @(".txt", ".md", ".json", ".csv")
)

# Set maximum file size (MB)
[SuperTUI.Infrastructure.ConfigurationManager]::Instance.Set(
    "Security.MaxFileSize",
    10
)
```

---

## 5. State Persistence

### Features
- Complete application state capture
- Workspace and widget state preservation
- Backup creation with compression
- Undo/redo support (50 levels)
- Automatic backup rotation
- Version tracking

### Usage

```powershell
# Initialize
$stateDir = Join-Path $env:LOCALAPPDATA "SuperTUI\State"
[SuperTUI.Extensions.StatePersistenceManager]::Instance.Initialize($stateDir)

# Capture current state
$snapshot = [SuperTUI.Extensions.StatePersistenceManager]::Instance.CaptureState(
    $workspaceManager,
    @{ CustomKey = "CustomValue" }
)

# Save state
[SuperTUI.Extensions.StatePersistenceManager]::Instance.SaveState($snapshot, $true)

# Load saved state
$savedState = [SuperTUI.Extensions.StatePersistenceManager]::Instance.LoadState()
if ($savedState) {
    [SuperTUI.Extensions.StatePersistenceManager]::Instance.RestoreState(
        $savedState,
        $workspaceManager
    )
}

# Undo/Redo
[SuperTUI.Extensions.StatePersistenceManager]::Instance.PushUndo($currentSnapshot)
$undoSnapshot = [SuperTUI.Extensions.StatePersistenceManager]::Instance.Undo()
$redoSnapshot = [SuperTUI.Extensions.StatePersistenceManager]::Instance.Redo()

# List available backups
$backups = [SuperTUI.Extensions.StatePersistenceManager]::Instance.GetAvailableBackups()
foreach ($backup in $backups) {
    Write-Host $backup
}

# Restore from backup
$backupPath = $backups[0]
$restoredState = [SuperTUI.Extensions.StatePersistenceManager]::Instance.RestoreFromBackup($backupPath)
```

### Auto-Save Implementation

```powershell
# Setup auto-save timer
$autoSaveInterval = [SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get(
    "App.AutoSaveInterval",
    300
)

if ($autoSaveInterval -gt 0) {
    $autoSaveTimer = New-Object System.Windows.Threading.DispatcherTimer
    $autoSaveTimer.Interval = [TimeSpan]::FromSeconds($autoSaveInterval)
    $autoSaveTimer.Add_Tick({
        $snapshot = [SuperTUI.Extensions.StatePersistenceManager]::Instance.CaptureState($workspaceManager)
        [SuperTUI.Extensions.StatePersistenceManager]::Instance.SaveState($snapshot, $true)
        [SuperTUI.Infrastructure.Logger]::Instance.Info("AutoSave", "State auto-saved")
    })
    $autoSaveTimer.Start()
}
```

---

## 6. Plugin System

### Features
- Dynamic plugin loading from DLLs
- Plugin metadata and versioning
- Dependency management
- Plugin isolation
- Hot-loading support

### Creating a Plugin

```csharp
using SuperTUI.Extensions;
using SuperTUI.Infrastructure;

public class MyPlugin : IPlugin
{
    public PluginMetadata Metadata => new PluginMetadata
    {
        Name = "MyPlugin",
        Version = "1.0.0",
        Author = "Your Name",
        Description = "My awesome plugin",
        Dependencies = new List<string>(),
        Enabled = true
    };

    private PluginContext context;

    public void Initialize(PluginContext context)
    {
        this.context = context;
        context.Logger.Info("MyPlugin", "Plugin initialized");

        // Access framework services
        var config = context.Config;
        var themes = context.Themes;
        var workspaces = context.Workspaces;

        // Register custom functionality
        // ...
    }

    public void Shutdown()
    {
        context.Logger.Info("MyPlugin", "Plugin shutting down");
        // Cleanup
    }
}
```

### Using Plugins

```powershell
# Initialize plugin system
$pluginsDir = Join-Path $env:LOCALAPPDATA "SuperTUI\Plugins"
$pluginContext = New-Object SuperTUI.Extensions.PluginContext
$pluginContext.Logger = [SuperTUI.Infrastructure.Logger]::Instance
$pluginContext.Config = [SuperTUI.Infrastructure.ConfigurationManager]::Instance
$pluginContext.Themes = [SuperTUI.Infrastructure.ThemeManager]::Instance
$pluginContext.Workspaces = $workspaceManager

[SuperTUI.Extensions.PluginManager]::Instance.Initialize($pluginsDir, $pluginContext)

# Load all plugins
[SuperTUI.Extensions.PluginManager]::Instance.LoadPlugins()

# Load specific plugin
[SuperTUI.Extensions.PluginManager]::Instance.LoadPlugin("C:\path\to\plugin.dll")

# Get plugin
$plugin = [SuperTUI.Extensions.PluginManager]::Instance.GetPlugin("MyPlugin")

# List all plugins
$plugins = [SuperTUI.Extensions.PluginManager]::Instance.GetAllPlugins()
foreach ($p in $plugins) {
    Write-Host "$($p.Metadata.Name) v$($p.Metadata.Version) by $($p.Metadata.Author)"
}

# Unload plugin
[SuperTUI.Extensions.PluginManager]::Instance.UnloadPlugin("MyPlugin")

# Listen for plugin events
[SuperTUI.Extensions.PluginManager]::Instance.add_PluginLoaded({
    param($sender, $args)
    Write-Host "Plugin loaded: $($args.Plugin.Metadata.Name)"
})
```

---

## 7. Performance Monitoring

### Features
- Operation timing with high precision
- Statistical analysis (min/max/avg)
- Automatic slow operation detection
- Performance reports

### Usage

```powershell
# Start monitoring an operation
[SuperTUI.Extensions.PerformanceMonitor]::Instance.StartOperation("DatabaseQuery")
# ... perform operation
[SuperTUI.Extensions.PerformanceMonitor]::Instance.StopOperation("DatabaseQuery")

# Get metrics
$counter = [SuperTUI.Extensions.PerformanceMonitor]::Instance.GetCounter("DatabaseQuery")
Write-Host "Last: $($counter.LastDuration.TotalMilliseconds) ms"
Write-Host "Average: $($counter.AverageDuration.TotalMilliseconds) ms"
Write-Host "Min: $($counter.MinDuration.TotalMilliseconds) ms"
Write-Host "Max: $($counter.MaxDuration.TotalMilliseconds) ms"
Write-Host "Samples: $($counter.SampleCount)"

# Generate full report
$report = [SuperTUI.Extensions.PerformanceMonitor]::Instance.GenerateReport()
Write-Host $report

# Reset all counters
[SuperTUI.Extensions.PerformanceMonitor]::Instance.ResetAll()
```

### Automatic Slow Operation Detection

Operations taking longer than 100ms are automatically logged as warnings.

---

## 8. Error Handling

### Features
- Global error handler
- Error severity levels
- Automatic retry with exponential backoff
- Error notifications
- Stack trace capture

### Usage

```powershell
# Handle errors
try {
    # Risky operation
} catch {
    [SuperTUI.Infrastructure.ErrorHandler]::Instance.HandleError(
        $_.Exception,
        "MyOperation",
        [SuperTUI.Infrastructure.ErrorSeverity]::Error,
        $true  # Show to user
    )
}

# Execute with automatic retry
$result = [SuperTUI.Infrastructure.ErrorHandler]::Instance.ExecuteWithRetry({
    # Operation that might fail temporarily
    Invoke-WebRequest "https://api.example.com/data"
}, 3, 100, "APICall")

# Listen for errors
[SuperTUI.Infrastructure.ErrorHandler]::Instance.add_ErrorOccurred({
    param($sender, $args)
    Write-Host "Error in $($args.Context): $($args.Exception.Message)"
})
```

---

## Integration Example

Here's a complete example of initializing all infrastructure systems:

```powershell
# ============================================================================
# Initialize All Infrastructure
# ============================================================================

function Initialize-SuperTUIInfrastructure {
    $baseDir = Join-Path $env:LOCALAPPDATA "SuperTUI"

    # 1. Logging
    $logDir = Join-Path $baseDir "Logs"
    $fileLog = New-Object SuperTUI.Infrastructure.FileLogSink($logDir, "supertui", 10, 10)
    $memoryLog = New-Object SuperTUI.Infrastructure.MemoryLogSink(1000)
    [SuperTUI.Infrastructure.Logger]::Instance.AddSink($fileLog)
    [SuperTUI.Infrastructure.Logger]::Instance.AddSink($memoryLog)
    [SuperTUI.Infrastructure.Logger]::Instance.SetMinLevel([SuperTUI.Infrastructure.LogLevel]::Info)

    # 2. Configuration
    $configPath = Join-Path $baseDir "config.json"
    [SuperTUI.Infrastructure.ConfigurationManager]::Instance.Initialize($configPath)

    # 3. Theme System
    $themesDir = Join-Path $baseDir "Themes"
    [SuperTUI.Infrastructure.ThemeManager]::Instance.Initialize($themesDir)
    $savedTheme = [SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get("UI.Theme", "Dark")
    [SuperTUI.Infrastructure.ThemeManager]::Instance.ApplyTheme($savedTheme)

    # 4. Security
    [SuperTUI.Infrastructure.SecurityManager]::Instance.Initialize()
    [SuperTUI.Infrastructure.SecurityManager]::Instance.AddAllowedDirectory([Environment]::GetFolderPath("MyDocuments"))

    # 5. State Persistence
    $stateDir = Join-Path $baseDir "State"
    [SuperTUI.Extensions.StatePersistenceManager]::Instance.Initialize($stateDir)

    # 6. Plugin System
    $pluginsDir = Join-Path $baseDir "Plugins"
    $pluginContext = New-Object SuperTUI.Extensions.PluginContext
    $pluginContext.Logger = [SuperTUI.Infrastructure.Logger]::Instance
    $pluginContext.Config = [SuperTUI.Infrastructure.ConfigurationManager]::Instance
    $pluginContext.Themes = [SuperTUI.Infrastructure.ThemeManager]::Instance
    [SuperTUI.Extensions.PluginManager]::Instance.Initialize($pluginsDir, $pluginContext)
    [SuperTUI.Extensions.PluginManager]::Instance.LoadPlugins()

    [SuperTUI.Infrastructure.Logger]::Instance.Info("Infrastructure", "All systems initialized")

    return @{
        Logger = [SuperTUI.Infrastructure.Logger]::Instance
        Config = [SuperTUI.Infrastructure.ConfigurationManager]::Instance
        Themes = [SuperTUI.Infrastructure.ThemeManager]::Instance
        Security = [SuperTUI.Infrastructure.SecurityManager]::Instance
        State = [SuperTUI.Extensions.StatePersistenceManager]::Instance
        Plugins = [SuperTUI.Extensions.PluginManager]::Instance
        Performance = [SuperTUI.Extensions.PerformanceMonitor]::Instance
        Errors = [SuperTUI.Infrastructure.ErrorHandler]::Instance
    }
}

# Initialize
$infrastructure = Initialize-SuperTUIInfrastructure

# Use in your application
$infrastructure.Logger.Info("App", "Application started")
```

---

## Best Practices

### Logging
1. Use appropriate log levels (don't log everything at Info)
2. Include context in log categories
3. Use structured properties for searchable data
4. Flush logs before application exit

### Configuration
1. Always provide default values
2. Use validators for important settings
3. Document all configuration keys
4. Save immediately for critical changes

### Themes
1. Test themes in both dark and light modes
2. Ensure sufficient contrast for accessibility
3. Use semantic color names (Primary, Success, etc.)

### Security
1. Always validate user input
2. Whitelist allowed directories
3. Limit file sizes
4. Sanitize filenames before file operations
5. Disable script execution in production

### State Persistence
1. Capture state before risky operations
2. Enable auto-save for long-running applications
3. Regularly clean old backups
4. Test restore functionality

### Performance
1. Monitor expensive operations
2. Review performance reports regularly
3. Set reasonable thresholds for warnings
4. Profile before optimizing

### Error Handling
1. Use appropriate severity levels
2. Provide context in error messages
3. Don't swallow exceptions silently
4. Log all errors for diagnostics

---

## Troubleshooting

### Logs not being written
- Check log directory permissions
- Verify sink is added to logger
- Check log level (messages below min level are discarded)

### Configuration not persisting
- Verify config file path is writable
- Check if `saveImmediately` parameter is true
- Ensure app has proper permissions

### Theme not applying
- Check theme name is correct
- Verify theme is registered
- Listen to ThemeChanged event and update UI

### Plugin not loading
- Check plugin DLL is in plugins directory
- Verify plugin implements IPlugin interface
- Check dependencies are satisfied
- Review logs for error messages

### State not restoring
- Verify state file exists
- Check JSON format is valid
- Ensure workspace/widget types match
- Review error logs

---

## Performance Considerations

- File logging has minimal overhead (~1-2ms per log entry)
- Memory logging is faster but limited in capacity
- Theme switching is instant (colors are just references)
- State capture scales with number of widgets
- Plugin loading happens once at startup
- Performance monitoring adds <0.1ms overhead per operation

---

## Security Considerations

- Never trust user input - always validate
- Use whitelists, not blacklists
- Validate file paths to prevent traversal attacks
- Limit file sizes to prevent DoS
- Disable script execution unless needed
- Regular security audits of allowed directories
- Keep plugins isolated with minimal permissions
- Log security-related events for auditing
