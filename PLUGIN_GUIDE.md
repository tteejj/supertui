# SuperTUI Plugin Development Guide

**Version:** 1.1
**Last Updated:** 2025-10-27
**Audience:** Plugin developers, system integrators

---

## Overview

SuperTUI supports plugins to extend functionality beyond the core framework. This guide covers plugin development best practices, security considerations, and the plugin API.

**⚠️ READ SECURITY SECTION BEFORE DEVELOPING PLUGINS ⚠️**

---

## ⚠️ BREAKING CHANGES (2025-10-27)

### Parameterless Constructor Removal

As of October 27, 2025, the following widgets **NO LONGER** have parameterless constructors:
- `AgendaWidget`
- `KanbanBoardWidget`
- `ProjectStatsWidget`

**Impact:**
- ❌ `new AgendaWidget()` will fail with compile error
- ❌ Plugins that subclass these widgets must add DI constructors
- ✅ Use `WidgetFactory.CreateWidget<T>()` for proper dependency injection

**Migration Example:**

```csharp
// OLD (BROKEN):
var agenda = new AgendaWidget();

// NEW (CORRECT):
var agenda = context.WidgetFactory.CreateWidget<AgendaWidget>();
```

**Subclassing Example:**

```csharp
// If extending AgendaWidget, KanbanBoardWidget, or ProjectStatsWidget
public class MyCustomAgenda : AgendaWidget
{
    // REQUIRED: DI constructor matching base class
    public MyCustomAgenda(
        ILogger logger,
        IThemeManager themeManager,
        IConfigurationManager config,
        ITaskService taskService)
        : base(logger, themeManager, config, taskService)
    {
        // Your custom initialization
    }
}
```

**Note:** PluginContext now exposes `WidgetFactory` for creating widgets with proper dependency injection.

---

## Quick Start

### Minimal Plugin

```csharp
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace MyPlugin
{
    public class HelloWorldPlugin : IPlugin
    {
        public string Name => "HelloWorld";
        public string Version => "1.0.0";
        public string Author => "Your Name";
        public string Description => "A simple example plugin";

        public void Initialize(PluginContext context)
        {
            Logger.Instance.Info("HelloWorldPlugin", "Plugin initialized!");

            // Access framework services
            var config = context.Configuration;
            var workspaces = context.Workspaces;
            var eventBus = context.EventBus;

            // Register custom widget
            // Subscribe to events
            // Add menu items
        }

        public void Shutdown()
        {
            Logger.Instance.Info("HelloWorldPlugin", "Plugin shutting down");

            // Cleanup resources
            // Unsubscribe from events
            // Dispose of objects
        }
    }
}
```

### Building the Plugin

```bash
# Compile plugin DLL
csc /target:library /reference:SuperTUI.dll /out:MyPlugin.dll MyPlugin.cs

# Or use MSBuild
msbuild MyPlugin.csproj
```

### Loading the Plugin

```powershell
# PowerShell
$pluginManager = [SuperTUI.Extensions.PluginManager]::Instance
$pluginManager.Initialize("C:\MyApp\Plugins", $pluginContext)
$pluginManager.LoadPlugin("C:\MyApp\Plugins\MyPlugin.dll")
```

---

## ⚠️ CRITICAL SECURITY LIMITATIONS

### 1. Plugins Cannot Be Unloaded

**Problem:**
- Once loaded, plugins remain in memory until application restart
- `Assembly.LoadFrom()` loads into default AppDomain
- .NET Framework does not support assembly unloading

**Implications:**
- Malicious plugin = **permanent compromise** of application
- Memory leaks if plugins loaded repeatedly
- Cannot update plugin without restarting application

**Workaround:**
- Restart application to reload plugins
- Design plugins to be stateless where possible
- Use feature flags to disable plugin functionality without unloading

### 2. Plugins Have Full Access

**Problem:**
- No sandboxing or permission model
- Plugins can access all SuperTUI internals
- Plugins can call any .NET API

**Implications:**
- Malicious plugin can:
  - Read/write any file (subject to OS permissions)
  - Access network resources
  - Execute arbitrary code
  - Modify application state
  - Exfiltrate data
  - Crash the application

**Mitigation:**
- **Only load plugins from trusted sources**
- Code review all plugins before deployment
- Use digital signatures to verify plugin authenticity

### 3. No Built-In Signature Verification

**Problem:**
- Plugins are not validated by default
- Anyone can create a plugin DLL

**Mitigation:**
- Enable signature verification in production:
  ```json
  {
    "Security.RequireSignedPlugins": true
  }
  ```
- Sign plugins with Authenticode:
  ```bash
  signtool sign /f MyCert.pfx /p password MyPlugin.dll
  ```

---

## Security Best Practices

### DO ✅

1. **Code Review Every Plugin**
   - Review all source code before deployment
   - Use static analysis tools (ReSharper, SonarQube)
   - Check for suspicious API calls (Process.Start, File.Delete, etc.)

2. **Sign Your Plugins**
   ```bash
   # Create certificate (testing only)
   makecert -r -pe -n "CN=MyCompany Plugin Signing" -sky signature MyCert.cer -sv MyCert.pvk

   # Create PFX
   pvk2pfx -pvk MyCert.pvk -spc MyCert.cer -pfx MyCert.pfx

   # Sign plugin
   signtool sign /f MyCert.pfx /p password MyPlugin.dll

   # Verify signature
   signtool verify /pa MyPlugin.dll
   ```

3. **Use SecurityManager for File Access**
   ```csharp
   public void Initialize(PluginContext context)
   {
       var filePath = @"C:\Data\config.json";

       // ALWAYS validate file access
       if (!SecurityManager.Instance.ValidateFileAccess(filePath))
       {
           Logger.Instance.Error("MyPlugin", $"Access denied: {filePath}");
           return;
       }

       // Proceed with file operation
       var content = File.ReadAllText(filePath);
   }
   ```

4. **Log All Plugin Actions**
   ```csharp
   Logger.Instance.Info("MyPlugin", "Loading configuration");
   Logger.Instance.Warning("MyPlugin", "Invalid setting detected");
   Logger.Instance.Error("MyPlugin", "Failed to connect to service", exception);
   ```

5. **Handle Errors Gracefully**
   ```csharp
   public void Initialize(PluginContext context)
   {
       try
       {
           // Plugin initialization
       }
       catch (Exception ex)
       {
           Logger.Instance.Error("MyPlugin", "Initialization failed", ex);
           // Don't crash the application
       }
   }
   ```

6. **Dispose of Resources**
   ```csharp
   private DispatcherTimer timer;
   private HttpClient httpClient;

   public void Initialize(PluginContext context)
   {
       timer = new DispatcherTimer();
       httpClient = new HttpClient();
   }

   public void Shutdown()
   {
       timer?.Dispose();
       httpClient?.Dispose();
   }
   ```

### DON'T ❌

1. **Don't Trust User Input**
   ```csharp
   // BAD
   var userPath = GetUserInput();
   File.Delete(userPath);  // Can delete anything!

   // GOOD
   var userPath = GetUserInput();
   if (SecurityManager.Instance.ValidateFileAccess(userPath, checkWrite: true))
   {
       File.Delete(userPath);
   }
   ```

2. **Don't Hardcode Credentials**
   ```csharp
   // BAD
   var password = "MySecretPassword123";

   // GOOD
   var password = ConfigurationManager.Instance.Get<string>("MyPlugin.Password");
   // Store encrypted in config or use Windows Credential Manager
   ```

3. **Don't Use Reflection Without Validation**
   ```csharp
   // BAD
   var typeName = GetUserInput();
   var type = Type.GetType(typeName);  // Can instantiate any type!
   var instance = Activator.CreateInstance(type);

   // GOOD
   var allowedTypes = new[] { "MyApp.SafeType1", "MyApp.SafeType2" };
   if (allowedTypes.Contains(typeName))
   {
       var type = Type.GetType(typeName);
       var instance = Activator.CreateInstance(type);
   }
   ```

4. **Don't Execute Arbitrary Commands**
   ```csharp
   // BAD
   var command = GetUserInput();
   Process.Start(command);  // Can execute anything!

   // BETTER
   if (SecurityManager.Instance.ValidateScriptExecution())
   {
       // Still dangerous - validate command first
   }
   ```

5. **Don't Modify Core Framework State**
   ```csharp
   // BAD
   SecurityManager.Instance.Initialize(SecurityMode.Development);  // Throws exception anyway

   // BAD
   ConfigurationManager.Instance.Set("Security.ValidateFileAccess", false);  // Don't bypass security

   // GOOD
   ConfigurationManager.Instance.Set("MyPlugin.EnableFeature", true);  // Plugin-specific settings only
   ```

---

## Plugin Manifest (Recommended)

Create a `manifest.json` alongside your plugin DLL:

```json
{
  "name": "MyPlugin",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Description of plugin functionality",
  "homepage": "https://github.com/you/myplugin",
  "license": "MIT",

  "assembly": "MyPlugin.dll",
  "class": "MyPlugin.MyPluginClass",

  "signature": {
    "algorithm": "SHA256",
    "hash": "abcd1234567890...",
    "certificate": "CN=MyCompany Plugin Signing"
  },

  "permissions": [
    "FileAccess:Documents",
    "FileAccess:AppData",
    "Network:Outbound",
    "Configuration:Read"
  ],

  "dependencies": [
    {
      "name": "SuperTUI.Core",
      "version": ">=1.0.0"
    },
    {
      "name": "Newtonsoft.Json",
      "version": ">=13.0.0"
    }
  ],

  "compatibility": {
    "minFrameworkVersion": "1.0.0",
    "maxFrameworkVersion": "2.0.0",
    "platforms": ["Windows"]
  },

  "metadata": {
    "tags": ["utility", "automation"],
    "screenshots": ["screenshot1.png", "screenshot2.png"],
    "documentation": "README.md"
  }
}
```

**Note:** Manifest validation is optional but highly recommended for production deployments.

---

## Plugin API Reference

### IPlugin Interface

```csharp
public interface IPlugin
{
    /// <summary>Plugin name (unique identifier)</summary>
    string Name { get; }

    /// <summary>Plugin version (SemVer format)</summary>
    string Version { get; }

    /// <summary>Plugin author</summary>
    string Author { get; }

    /// <summary>Plugin description</summary>
    string Description { get; }

    /// <summary>
    /// Initialize plugin with framework context.
    /// Called once when plugin is loaded.
    /// </summary>
    void Initialize(PluginContext context);

    /// <summary>
    /// Shutdown plugin and cleanup resources.
    /// Called when application exits (NOT when plugin is "unloaded" - plugins can't be unloaded).
    /// </summary>
    void Shutdown();
}
```

### PluginContext

```csharp
public class PluginContext
{
    /// <summary>Configuration manager (read/write app settings)</summary>
    public IConfigurationManager Configuration { get; set; }

    /// <summary>Theme manager (access current theme)</summary>
    public IThemeManager ThemeManager { get; set; }

    /// <summary>Logger (write to application logs)</summary>
    public ILogger Logger { get; set; }

    /// <summary>Event bus (publish/subscribe events)</summary>
    public IEventBus EventBus { get; set; }

    /// <summary>Security manager (validate file access)</summary>
    public ISecurityManager SecurityManager { get; set; }

    /// <summary>Workspace manager (access workspaces/widgets)</summary>
    public WorkspaceManager Workspaces { get; set; }

    /// <summary>Shared data dictionary for inter-plugin communication</summary>
    public Dictionary<string, object> SharedData { get; set; }
}
```

### Common Plugin Patterns

#### 1. Custom Widget Plugin

```csharp
public class CustomWidgetPlugin : IPlugin
{
    public void Initialize(PluginContext context)
    {
        // Note: As of 2025-10-27, use WidgetFactory for proper dependency injection
        // Direct instantiation with new() is deprecated for widgets requiring services

        var currentWorkspace = context.Workspaces.CurrentWorkspace;

        // If your widget has dependencies (ILogger, IThemeManager, etc.):
        // var widget = context.WidgetFactory.CreateWidget<MyCustomWidget>();

        // If your widget has no dependencies (simple widget):
        var widget = new MyCustomWidget();
        widget.Initialize();

        currentWorkspace.AddWidget(widget, new LayoutParams { Row = 0, Column = 0 });
    }
}
```

#### 2. Event Listener Plugin

```csharp
public class EventListenerPlugin : IPlugin
{
    private IEventBus eventBus;

    public void Initialize(PluginContext context)
    {
        eventBus = context.EventBus;

        // Subscribe to events
        eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
        eventBus.Subscribe<WorkspaceChangedEvent>(OnWorkspaceChanged);
    }

    private void OnFileSelected(FileSelectedEvent evt)
    {
        Logger.Instance.Info("EventListener", $"File selected: {evt.FilePath}");
    }

    private void OnWorkspaceChanged(WorkspaceChangedEvent evt)
    {
        Logger.Instance.Info("EventListener", $"Workspace changed to: {evt.WorkspaceName}");
    }

    public void Shutdown()
    {
        // Unsubscribe from events
        eventBus.Unsubscribe<FileSelectedEvent>(OnFileSelected);
        eventBus.Unsubscribe<WorkspaceChangedEvent>(OnWorkspaceChanged);
    }
}
```

#### 3. Background Service Plugin

```csharp
public class BackgroundServicePlugin : IPlugin
{
    private DispatcherTimer timer;

    public void Initialize(PluginContext context)
    {
        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        timer.Tick += OnTimer;
        timer.Start();

        Logger.Instance.Info("BackgroundService", "Background service started");
    }

    private void OnTimer(object sender, EventArgs e)
    {
        try
        {
            // Perform background task
            SyncData();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("BackgroundService", "Background task failed", ex);
        }
    }

    public void Shutdown()
    {
        timer?.Stop();
        timer?.Dispose();
    }
}
```

#### 4. Configuration Plugin

```csharp
public class ConfigurationPlugin : IPlugin
{
    public void Initialize(PluginContext context)
    {
        var config = context.Configuration;

        // Register plugin settings with defaults
        config.Register("MyPlugin.Enabled", true, "Enable MyPlugin functionality");
        config.Register("MyPlugin.ApiKey", "", "API key for external service", "MyPlugin");
        config.Register("MyPlugin.RefreshInterval", 60, "Refresh interval in seconds", "MyPlugin",
            value => (int)value >= 10 && (int)value <= 3600);

        // Read settings
        var isEnabled = config.Get<bool>("MyPlugin.Enabled");
        var apiKey = config.Get<string>("MyPlugin.ApiKey");
        var interval = config.Get<int>("MyPlugin.RefreshInterval");

        Logger.Instance.Info("MyPlugin", $"Plugin enabled: {isEnabled}, Interval: {interval}s");
    }
}
```

---

## Testing Plugins

### Unit Testing

```csharp
using Xunit;
using Moq;

public class MyPluginTests
{
    [Fact]
    public void Initialize_ShouldRegisterWidget()
    {
        // Arrange
        var mockContext = new Mock<PluginContext>();
        var mockWorkspaces = new Mock<WorkspaceManager>();
        mockContext.Setup(c => c.Workspaces).Returns(mockWorkspaces.Object);

        var plugin = new MyPlugin();

        // Act
        plugin.Initialize(mockContext.Object);

        // Assert
        mockWorkspaces.Verify(w => w.AddWidget(It.IsAny<WidgetBase>(), It.IsAny<LayoutParams>()), Times.Once);
    }

    [Fact]
    public void OnFileSelected_WithValidFile_ShouldProcessFile()
    {
        // Arrange
        var plugin = new MyPlugin();
        var evt = new FileSelectedEvent { FilePath = "C:\\test.txt" };

        // Act
        plugin.OnFileSelected(evt);

        // Assert
        // Verify file was processed
    }
}
```

### Integration Testing

```powershell
# PowerShell integration test
$pluginPath = "C:\MyPlugin\bin\Release\MyPlugin.dll"

# Initialize framework
$container = New-Object SuperTUI.Core.ServiceContainer
$container.RegisterCoreServices()

# Load plugin
$pluginManager = [SuperTUI.Extensions.PluginManager]::Instance
$pluginContext = New-Object SuperTUI.Extensions.PluginContext
$pluginContext.Configuration = $container.Resolve([SuperTUI.Infrastructure.IConfigurationManager])
$pluginContext.Logger = $container.Resolve([SuperTUI.Infrastructure.ILogger])

$pluginManager.Initialize("C:\MyPlugin\bin\Release", $pluginContext)
$pluginManager.LoadPlugin($pluginPath)

# Verify plugin loaded
$loadedPlugins = $pluginManager.GetLoadedPlugins()
Write-Host "Loaded plugins: $($loadedPlugins.Count)"
```

---

## Deployment

### Plugin Directory Structure

```
C:\MyApp\Plugins\
├── MyPlugin\
│   ├── MyPlugin.dll           # Main plugin assembly
│   ├── manifest.json          # Plugin manifest (recommended)
│   ├── README.md              # Documentation
│   ├── LICENSE.txt            # License file
│   └── dependencies\          # Plugin dependencies
│       ├── Newtonsoft.Json.dll
│       └── OtherLibrary.dll
```

### Installation

**Manual:**
1. Copy plugin DLL and dependencies to plugin directory
2. Restart SuperTUI
3. Plugin loads automatically on startup

**Automated:**
```powershell
# PowerShell installer script
param(
    [string]$PluginZip,
    [string]$PluginDir = "C:\MyApp\Plugins"
)

# Extract plugin
Expand-Archive -Path $PluginZip -DestinationPath "$PluginDir\$(Split-Path $PluginZip -LeafBase)"

# Verify manifest
$manifest = Get-Content "$PluginDir\$(Split-Path $PluginZip -LeafBase)\manifest.json" | ConvertFrom-Json

Write-Host "Installed: $($manifest.name) v$($manifest.version)"
Write-Host "Restart SuperTUI to load plugin."
```

### Uninstallation

**⚠️ Warning:** Plugins cannot be unloaded without restarting!

```powershell
# Remove plugin files
Remove-Item -Recurse -Force "C:\MyApp\Plugins\MyPlugin"

# Restart SuperTUI for changes to take effect
```

---

## Troubleshooting

### Plugin Not Loading

**Check:**
1. DLL is in correct directory
2. DLL is not blocked (Right-click → Properties → Unblock)
3. Plugin implements `IPlugin` interface
4. Plugin constructor doesn't throw exceptions
5. Dependencies are present
6. Signature verification passes (if enabled)

**Debug:**
```csharp
// Enable debug logging
Logger.Instance.SetMinLevel(LogLevel.Debug);

// Check plugin load errors
var errors = PluginManager.Instance.GetLoadErrors();
foreach (var error in errors)
{
    Console.WriteLine($"Plugin load error: {error}");
}
```

### Plugin Crashes Application

**Prevent:**
1. Wrap all plugin code in try-catch
2. Test thoroughly before deployment
3. Use ErrorHandler.ExecuteWithRetry for critical operations

```csharp
public void Initialize(PluginContext context)
{
    ErrorHandler.Instance.ExecuteWithRetry(() =>
    {
        // Plugin initialization code
    }, maxRetries: 3, retryDelay: TimeSpan.FromSeconds(1));
}
```

### Memory Leaks

**Common Causes:**
- Event handlers not unsubscribed
- Timers not disposed
- HTTP clients not disposed
- Large objects cached indefinitely

**Fix:**
```csharp
public class MyPlugin : IPlugin, IDisposable
{
    private DispatcherTimer timer;
    private HttpClient httpClient;

    public void Initialize(PluginContext context)
    {
        timer = new DispatcherTimer();
        httpClient = new HttpClient();

        context.EventBus.Subscribe<SomeEvent>(OnEvent);
    }

    public void Shutdown()
    {
        Dispose();
    }

    public void Dispose()
    {
        timer?.Dispose();
        httpClient?.Dispose();

        // Unsubscribe events
        EventBus.Instance.Unsubscribe<SomeEvent>(OnEvent);
    }
}
```

---

## Future Improvements

### Planned Features

1. **AssemblyLoadContext Support** (.NET 6+)
   - True plugin unloading
   - Isolated plugin dependencies
   - Plugin hot-reload

2. **Permission Model**
   - Declarative permissions in manifest
   - Runtime permission checks
   - User approval for sensitive operations

3. **Plugin Sandboxing**
   - Separate process isolation
   - Limited API access
   - Resource quotas (CPU, memory)

4. **Plugin Marketplace**
   - Centralized plugin repository
   - Automated updates
   - Community ratings/reviews

5. **Enhanced Security**
   - Automated malware scanning
   - Required code signing
   - Security audit logs

---

## Support

**Questions:** [GitHub Discussions](https://github.com/you/supertui/discussions)
**Bug Reports:** [GitHub Issues](https://github.com/you/supertui/issues)
**Security Issues:** security@yourcompany.com (private disclosure)

---

## License

Plugins inherit SuperTUI's license unless specified otherwise in manifest.

**SuperTUI License:** MIT (or your license)

---

## Additional Resources

- [SECURITY.md](SECURITY.md) - Security model documentation
- [API Documentation](https://supertui.readthedocs.io) - Full API reference
- [Example Plugins](https://github.com/you/supertui-plugins) - Sample plugins
- [Plugin Template](https://github.com/you/supertui-plugin-template) - Starter template

---

**Version:** 1.0
**Last Updated:** 2025-10-24
**Contributors:** SuperTUI Development Team
