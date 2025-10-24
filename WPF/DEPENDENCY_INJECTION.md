# Dependency Injection in SuperTUI

SuperTUI uses a custom lightweight dependency injection (DI) container to manage service lifetimes and dependencies.

## Quick Start

```csharp
// 1. Get the container
var container = ServiceContainer.Instance;

// 2. Configure services (usually done at app startup)
ServiceRegistration.ConfigureServices(container);
ServiceRegistration.InitializeServices(container);

// 3. Resolve services
var logger = container.Resolve<ILogger>();
var themeManager = container.Resolve<IThemeManager>();
```

## Service Lifetimes

### Singleton
Single instance for the entire application lifetime.

```csharp
// Register with existing instance
container.RegisterSingleton<ILogger>(Logger.Instance);

// Register with type
container.RegisterSingleton<ILogger, Logger>();

// Register with factory
container.RegisterSingleton<ILogger>(c => new Logger());
```

### Transient
New instance created every time.

```csharp
// Register with type
container.RegisterTransient<IWidget, ClockWidget>();

// Register with factory
container.RegisterTransient<IWidget>(c => new ClockWidget());
```

### Scoped
Not yet implemented. Reserved for future use.

## Constructor Injection

The container automatically resolves constructor dependencies:

```csharp
public class MyWidget : WidgetBase
{
    private readonly ILogger logger;
    private readonly IThemeManager themeManager;

    // Dependencies are automatically injected
    public MyWidget(ILogger logger, IThemeManager themeManager)
    {
        this.logger = logger;
        this.themeManager = themeManager;
    }
}

// Register the widget
container.RegisterTransient<MyWidget>();

// Resolve - dependencies are automatically injected
var widget = container.Resolve<MyWidget>();
```

**How it works:**
1. Container finds the constructor with the most parameters
2. Attempts to resolve each parameter from registered services
3. If all parameters resolve successfully, creates the instance
4. If any parameter fails, tries the next constructor

## Registration Patterns

### Interface to Implementation
```csharp
container.RegisterSingleton<ILogger, Logger>();
container.RegisterTransient<IWidget, ClockWidget>();
```

### Concrete Type
```csharp
container.RegisterTransient<ClockWidget>();
var widget = container.Resolve<ClockWidget>();
```

### Factory Function
```csharp
container.RegisterSingleton<ILogger>(c =>
{
    var logger = new Logger();
    logger.SetMinLevel(LogLevel.Debug);
    return logger;
});
```

### With Dependencies in Factory
```csharp
container.RegisterSingleton<ILogger, Logger>();
container.RegisterTransient<IWidget>(c =>
{
    var logger = c.Resolve<ILogger>();
    var widget = new ClockWidget();
    // Configure widget with logger
    return widget;
});
```

## Pre-Registered Services

These services are automatically registered by `ServiceRegistration.ConfigureServices()`:

### Infrastructure (Singletons)
- `ILogger` → `Logger.Instance`
- `IConfigurationManager` → `ConfigurationManager.Instance`
- `IThemeManager` → `ThemeManager.Instance`
- `ISecurityManager` → `SecurityManager.Instance`
- `IErrorHandler` → `ErrorHandler.Instance`

### State Management (Singletons)
- `StatePersistenceManager.Instance`
- `PerformanceMonitor.Instance`
- `PluginManager.Instance`

### Event System (Singleton)
- `EventBus.Instance`

## Resolving Services

### Standard Resolution
```csharp
var logger = container.Resolve<ILogger>();
```

Throws `InvalidOperationException` if service is not registered.

### Safe Resolution
```csharp
if (container.TryResolve<ILogger>(out var logger))
{
    logger.Info("App", "Service resolved successfully");
}
else
{
    Console.WriteLine("Logger not registered");
}
```

### Check Registration
```csharp
if (container.IsRegistered<ILogger>())
{
    var logger = container.Resolve<ILogger>();
}
```

## Advanced Scenarios

### Multiple Implementations
```csharp
// Register multiple widgets
container.RegisterTransient("clock", () => new ClockWidget());
container.RegisterTransient("counter", () => new CounterWidget());

// Resolve by key (not directly supported yet - use factory pattern)
container.RegisterTransient<WidgetFactory>();

public class WidgetFactory
{
    public IWidget Create(string type)
    {
        return type switch
        {
            "clock" => new ClockWidget(),
            "counter" => new CounterWidget(),
            _ => throw new ArgumentException("Unknown widget type")
        };
    }
}
```

### Decorators
```csharp
// Register base service
container.RegisterSingleton<ILogger, Logger>();

// Register decorator
container.RegisterSingleton<ILogger>(c =>
{
    var innerLogger = new Logger();
    return new CachedLogger(innerLogger);
});
```

### Lazy Initialization
```csharp
container.RegisterSingleton<Lazy<ILogger>>(c =>
    new Lazy<ILogger>(() => c.Resolve<ILogger>())
);

// Use lazy
var lazyLogger = container.Resolve<Lazy<ILogger>>();
var logger = lazyLogger.Value; // Only created when accessed
```

## Testing with DI

### Unit Test Example
```csharp
[Fact]
public void MyWidget_WithMockLogger_LogsCorrectly()
{
    // Arrange
    var mockLogger = new Mock<ILogger>();
    var container = new ServiceContainer();
    container.RegisterSingleton<ILogger>(mockLogger.Object);

    // Act
    var widget = container.Resolve<MyWidget>();
    widget.Initialize();

    // Assert
    mockLogger.Verify(l => l.Info("Widget", "Initialized"), Times.Once);
}
```

### Integration Test Example
```csharp
[Fact]
public void Application_WithRealServices_StartsCorrectly()
{
    // Arrange
    var container = ServiceContainer.Instance;
    ServiceRegistration.ConfigureServices(container);
    ServiceRegistration.InitializeServices(container);

    // Act
    var logger = container.Resolve<ILogger>();
    var themes = container.Resolve<IThemeManager>();

    // Assert
    logger.Should().NotBeNull();
    themes.Should().NotBeNull();
    themes.CurrentTheme.Should().NotBeNull();
}
```

## PowerShell Usage

```powershell
# Get container
$container = [SuperTUI.Core.ServiceContainer]::Instance

# Configure services
[SuperTUI.DI.ServiceRegistration]::ConfigureServices($container)
[SuperTUI.DI.ServiceRegistration]::InitializeServices($container)

# Resolve service
$logger = $container.Resolve([SuperTUI.Infrastructure.ILogger])
$logger.Info("PowerShell", "Hello from PowerShell!")

# Check if registered
$isRegistered = $container.IsRegistered([SuperTUI.Infrastructure.ILogger])
Write-Host "Logger registered: $isRegistered"

# Try resolve
$success = $container.TryResolve([SuperTUI.Infrastructure.ILogger], [ref]$null)
if ($success) {
    Write-Host "Logger resolved successfully"
}
```

## Best Practices

### ✅ Do
- Register all services at application startup
- Use interfaces for dependencies
- Prefer constructor injection
- Use singletons for stateful services (logger, config, theme)
- Use transients for stateless services (widgets)
- Keep constructors simple - just assign dependencies

### ❌ Don't
- Don't use `ServiceLocator` anti-pattern (resolving in constructors)
- Don't create circular dependencies
- Don't resolve services in static constructors
- Don't mix manual `new` with DI for the same types
- Don't register mutable singletons without thread safety

### Constructor Anti-Pattern
```csharp
// ❌ BAD - Service Locator anti-pattern
public class MyWidget : WidgetBase
{
    private ILogger logger;

    public MyWidget()
    {
        // Don't do this!
        logger = ServiceContainer.Instance.Resolve<ILogger>();
    }
}

// ✅ GOOD - Constructor injection
public class MyWidget : WidgetBase
{
    private readonly ILogger logger;

    public MyWidget(ILogger logger)
    {
        this.logger = logger;
    }
}
```

## Thread Safety

The `ServiceContainer` is thread-safe:
- All registration methods use locks
- Singleton instance creation is thread-safe
- Multiple threads can resolve services simultaneously
- Singleton instances are cached safely

```csharp
// Safe to call from multiple threads
Parallel.For(0, 100, i =>
{
    var logger = container.Resolve<ILogger>();
    logger.Info("Thread", $"Thread {i}");
});
```

## Performance Considerations

### Singleton vs Transient
- **Singleton**: Faster on subsequent resolutions (cached)
- **Transient**: Small overhead for reflection + construction

### Constructor Selection
The container tries constructors in order of parameter count (most to least):
1. Constructor with 3 params (tries first)
2. Constructor with 2 params
3. Constructor with 0 params (parameterless)

To avoid overhead, provide a parameterless constructor when DI isn't needed.

### Avoiding Reflection
For hot paths, consider caching resolved services:

```csharp
public class MyHotPathService
{
    private readonly ILogger logger;

    public MyHotPathService()
    {
        // Resolve once and cache
        logger = ServiceContainer.Instance.Resolve<ILogger>();
    }
}
```

## Migration Guide

### From Singleton Pattern
```csharp
// OLD
var logger = Logger.Instance;

// NEW
var logger = container.Resolve<ILogger>();
```

### From Manual new
```csharp
// OLD
var widget = new ClockWidget();

// NEW
container.RegisterTransient<ClockWidget>();
var widget = container.Resolve<ClockWidget>();
```

### Gradual Migration
You can use both patterns during migration:

```csharp
// Widgets still use Logger.Instance
public class ClockWidget : WidgetBase
{
    public void Initialize()
    {
        Logger.Instance.Info("Clock", "Initialized");
    }
}

// New widgets use DI
public class SystemMonitorWidget : WidgetBase
{
    private readonly ILogger logger;

    public SystemMonitorWidget(ILogger logger)
    {
        this.logger = logger;
    }
}
```

## Troubleshooting

### "Service of type X is not registered"
**Solution:** Register the service before resolving:
```csharp
container.RegisterSingleton<IMyService, MyService>();
```

### "Cannot resolve parameter X"
**Cause:** Constructor parameter is not registered in container.
**Solution:** Register all dependencies:
```csharp
container.RegisterSingleton<IDependency, Dependency>();
container.RegisterTransient<MyService>(); // Now can resolve
```

### "Cannot create instance... All constructors failed"
**Cause:** No constructor could have all parameters resolved.
**Solution:**
1. Ensure all constructor parameters are registered
2. Or provide a parameterless constructor as fallback

### Circular Dependencies
```csharp
// ❌ This will cause stack overflow
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}

public class ServiceB
{
    public ServiceB(ServiceA a) { }
}
```

**Solution:** Refactor to break the cycle:
- Use interfaces
- Introduce a mediator
- Use lazy initialization

## Examples

See:
- `WPF/SuperTUI_DI_Example.ps1` - PowerShell DI demo
- `Tests/SuperTUI.Tests/DI/ServiceContainerTests.cs` - Unit tests
- `WPF/Core/DI/ServiceRegistration.cs` - Service registration helper

## Future Enhancements

- [ ] Scoped lifetime support
- [ ] Named registrations (multiple implementations)
- [ ] Open generic support
- [ ] Decorator pattern support
- [ ] Child containers/scopes
- [ ] Service lifetime validation
- [ ] Circular dependency detection
- [ ] Performance profiling

---

**Related:**
- `ENHANCEMENTS_PROGRESS.md` - Overall enhancement tracking
- `FIXES_APPLIED.md` - Previous critical fixes
