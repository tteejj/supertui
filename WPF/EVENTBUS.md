# EventBus - Inter-Widget Communication

The EventBus provides a decoupled pub/sub messaging system for widgets and components to communicate without direct references.

## Quick Start

```csharp
// Subscribe to an event
EventBus.Instance.Subscribe<BranchChangedEvent>(evt =>
{
    Console.WriteLine($"Branch changed to: {evt.NewBranch}");
});

// Publish an event
EventBus.Instance.Publish(new BranchChangedEvent
{
    Repository = "C:\\MyRepo",
    NewBranch = "main",
    OldBranch = "feature/test"
});
```

## Features

- ✅ **Strongly-Typed Events** - Type-safe event handling
- ✅ **Weak References** - Prevent memory leaks from forgotten subscriptions
- ✅ **Priority Handling** - Control execution order
- ✅ **Thread-Safe** - Safe for concurrent access
- ✅ **Request/Response** - Query pattern for data retrieval
- ✅ **Statistics** - Track event delivery metrics
- ✅ **Named Events** - String-based events for dynamic scenarios

## Typed Events (Recommended)

### Publishing Events

```csharp
// Define your event
public class ThemeChangedEvent
{
    public string OldThemeName { get; set; }
    public string NewThemeName { get; set; }
    public DateTime ChangedAt { get; set; }
}

// Publish
EventBus.Instance.Publish(new ThemeChangedEvent
{
    OldThemeName = "Dark",
    NewThemeName = "Light",
    ChangedAt = DateTime.Now
});
```

### Subscribing to Events

```csharp
// Subscribe
EventBus.Instance.Subscribe<ThemeChangedEvent>(evt =>
{
    Console.WriteLine($"Theme changed from {evt.OldThemeName} to {evt.NewThemeName}");
});
```

### Unsubscribing

```csharp
// Keep a reference to the handler
Action<ThemeChangedEvent> handler = evt => { /* ... */ };

// Subscribe
EventBus.Instance.Subscribe(handler);

// Later... unsubscribe
EventBus.Instance.Unsubscribe(handler);
```

## Priority Handling

Control the order in which handlers are executed:

```csharp
// These will execute in order: Critical → High → Normal → Low
EventBus.Instance.Subscribe<ThemeChangedEvent>(
    evt => UpdateCriticalUI(),
    SubscriptionPriority.Critical);

EventBus.Instance.Subscribe<ThemeChangedEvent>(
    evt => UpdateMainUI(),
    SubscriptionPriority.High);

EventBus.Instance.Subscribe<ThemeChangedEvent>(
    evt => UpdateSecondaryUI(),
    SubscriptionPriority.Normal);

EventBus.Instance.Subscribe<ThemeChangedEvent>(
    evt => LogThemeChange(),
    SubscriptionPriority.Low);
```

## Weak vs Strong References

### Weak References (Default)
Prevents memory leaks by allowing garbage collection:

```csharp
// Weak reference - subscriber can be garbage collected
EventBus.Instance.Subscribe<MyEvent>(
    evt => { },
    useWeakReference: true  // DEFAULT
);
```

**Use when:**
- Widget lifetime is managed externally
- You might forget to unsubscribe
- Memory leaks are a concern

### Strong References
Keeps subscriber alive:

```csharp
// Strong reference - subscriber won't be collected
EventBus.Instance.Subscribe<MyEvent>(
    evt => { },
    useWeakReference: false
);
```

**Use when:**
- Subscriber needs to stay alive for app lifetime
- Explicit lifecycle management
- Performance is critical (weak refs have small overhead)

## Request/Response Pattern

Query data from other components:

```csharp
// Register a handler
EventBus.Instance.RegisterRequestHandler<GetSystemStatsRequest, GetSystemStatsResponse>(
    request => new GetSystemStatsResponse
    {
        CpuPercent = GetCpuUsage(),
        MemoryUsed = GetMemoryUsage(),
        MemoryTotal = GetTotalMemory()
    });

// Send a request
var stats = EventBus.Instance.Request<GetSystemStatsRequest, GetSystemStatsResponse>(
    new GetSystemStatsRequest());

Console.WriteLine($"CPU: {stats.CpuPercent}%");

// Or use TryRequest for safety
if (EventBus.Instance.TryRequest<GetSystemStatsRequest, GetSystemStatsResponse>(
    new GetSystemStatsRequest(), out var response))
{
    Console.WriteLine($"CPU: {response.CpuPercent}%");
}
```

## Built-in Events

See `Core/Infrastructure/Events.cs` for all pre-defined events:

### Workspace Events
- `WorkspaceChangedEvent`
- `WorkspaceCreatedEvent`
- `WorkspaceRemovedEvent`

### Widget Events
- `WidgetActivatedEvent`
- `WidgetDeactivatedEvent`
- `WidgetFocusReceivedEvent`
- `WidgetFocusLostEvent`

### Theme Events
- `ThemeChangedEvent`
- `ThemeLoadedEvent`

### File System Events
- `DirectoryChangedEvent`
- `FileSelectedEvent`
- `FileCreatedEvent`
- `FileDeletedEvent`

### Git Events
- `BranchChangedEvent`
- `CommitCreatedEvent`
- `RepositoryStatusChangedEvent`

### Terminal Events
- `CommandExecutedEvent`
- `TerminalOutputEvent`
- `WorkingDirectoryChangedEvent`

### System Events
- `SystemResourcesChangedEvent`
- `NetworkActivityEvent`

### Task Events
- `TaskCreatedEvent`
- `TaskCompletedEvent`
- `TaskStatusChangedEvent`

### Notification Events
- `NotificationEvent`

## Real-World Examples

### Example 1: Git Status → Terminal Output

```csharp
// GitStatusWidget publishes when branch changes
public class GitStatusWidget : WidgetBase
{
    private void OnBranchChanged(string newBranch)
    {
        EventBus.Instance.Publish(new BranchChangedEvent
        {
            Repository = CurrentRepository,
            NewBranch = newBranch,
            OldBranch = _previousBranch,
            ChangedAt = DateTime.Now
        });
    }
}

// TerminalWidget subscribes and shows notification
public class TerminalWidget : WidgetBase
{
    public override void Initialize()
    {
        EventBus.Instance.Subscribe<BranchChangedEvent>(evt =>
        {
            WriteLine($"[Git] Branch changed to: {evt.NewBranch}", ConsoleColor.Cyan);
        });
    }
}
```

### Example 2: File Explorer → Multiple Widgets

```csharp
// FileExplorerWidget publishes directory changes
public class FileExplorerWidget : WidgetBase
{
    private void ChangeDirectory(string newPath)
    {
        EventBus.Instance.Publish(new DirectoryChangedEvent
        {
            OldPath = CurrentPath,
            NewPath = newPath,
            ChangedAt = DateTime.Now
        });

        CurrentPath = newPath;
    }
}

// GitStatusWidget updates for new directory
public class GitStatusWidget : WidgetBase
{
    public override void Initialize()
    {
        EventBus.Instance.Subscribe<DirectoryChangedEvent>(evt =>
        {
            CheckIfGitRepository(evt.NewPath);
            Refresh();
        }, SubscriptionPriority.High);
    }
}

// Terminal widget updates prompt
public class TerminalWidget : WidgetBase
{
    public override void Initialize()
    {
        EventBus.Instance.Subscribe<DirectoryChangedEvent>(evt =>
        {
            UpdatePrompt(evt.NewPath);
        }, SubscriptionPriority.Normal);
    }
}
```

### Example 3: System Monitor → Notification

```csharp
// SystemMonitorWidget publishes resource stats
public class SystemMonitorWidget : WidgetBase
{
    private void OnStatsUpdated()
    {
        EventBus.Instance.Publish(new SystemResourcesChangedEvent
        {
            CpuUsagePercent = GetCpuUsage(),
            MemoryUsedBytes = GetMemoryUsed(),
            MemoryTotalBytes = GetMemoryTotal(),
            Timestamp = DateTime.Now
        });

        // Publish notification if CPU is high
        if (GetCpuUsage() > 90)
        {
            EventBus.Instance.Publish(new NotificationEvent
            {
                Title = "High CPU Usage",
                Message = $"CPU usage is at {GetCpuUsage():F1}%",
                Level = NotificationLevel.Warning,
                Duration = TimeSpan.FromSeconds(5)
            });
        }
    }
}

// NotificationWidget subscribes
public class NotificationWidget : WidgetBase
{
    public override void Initialize()
    {
        EventBus.Instance.Subscribe<NotificationEvent>(evt =>
        {
            ShowNotification(evt.Title, evt.Message, evt.Level);
            if (evt.Duration.HasValue)
            {
                AutoDismissAfter(evt.Duration.Value);
            }
        }, SubscriptionPriority.Critical); // Show notifications immediately
    }
}
```

## Named Events (Legacy/Dynamic)

For scenarios where types aren't known at compile time:

```csharp
// Subscribe
EventBus.Instance.Subscribe("workspace.changed", data =>
{
    var workspaceName = data as string;
    Console.WriteLine($"Workspace: {workspaceName}");
});

// Publish
EventBus.Instance.Publish("workspace.changed", "Dashboard");
```

## Utilities

### Check for Subscribers
```csharp
if (EventBus.Instance.HasSubscribers<ThemeChangedEvent>())
{
    // Only create expensive event object if someone is listening
    var evt = CreateExpensiveEvent();
    EventBus.Instance.Publish(evt);
}
```

### Get Statistics
```csharp
var stats = EventBus.Instance.GetStatistics();
Console.WriteLine($"Published: {stats.Published}");
Console.WriteLine($"Delivered: {stats.Delivered}");
Console.WriteLine($"Typed Subscribers: {stats.TypedSubscribers}");
Console.WriteLine($"Named Subscribers: {stats.NamedSubscribers}");
```

### Cleanup Dead Subscriptions
```csharp
// Manually cleanup dead weak references (usually not needed)
EventBus.Instance.CleanupDeadSubscriptions();
```

### Clear All
```csharp
// Remove all subscriptions (testing/shutdown)
EventBus.Instance.Clear();
```

## Best Practices

### ✅ Do
- Use typed events for type safety
- Use weak references by default
- Unsubscribe when disposing widgets
- Use priority for execution order control
- Keep event handlers short and fast
- Publish events for significant state changes
- Use descriptive event names

### ❌ Don't
- Don't publish events in tight loops (performance)
- Don't do heavy work in event handlers
- Don't use events for return values (use Request/Response)
- Don't create circular event dependencies
- Don't assume event delivery order (unless using priorities)
- Don't store event data if you need it later (it may change)

### Event Handler Pattern
```csharp
public class MyWidget : WidgetBase
{
    public override void Initialize()
    {
        // Subscribe
        EventBus.Instance.Subscribe<ThemeChangedEvent>(OnThemeChanged);
    }

    private void OnThemeChanged(ThemeChangedEvent evt)
    {
        // Handle event
        ApplyTheme(evt.NewThemeName);
    }

    protected override void OnDispose()
    {
        // Unsubscribe (if using strong references)
        EventBus.Instance.Unsubscribe<ThemeChangedEvent>(OnThemeChanged);
        base.OnDispose();
    }
}
```

## Thread Safety

The EventBus is fully thread-safe:

```csharp
// Safe to publish from any thread
Task.Run(() =>
{
    EventBus.Instance.Publish(new MyEvent());
});

// Safe to subscribe from any thread
Parallel.For(0, 100, i =>
{
    EventBus.Instance.Subscribe<MyEvent>(evt => { });
});
```

**Note:** Event handlers execute on the same thread that published the event. If you need UI thread marshalling:

```csharp
EventBus.Instance.Subscribe<MyEvent>(evt =>
{
    Dispatcher.BeginInvoke(() =>
    {
        // Update UI
        UpdateUIElement();
    });
});
```

## Performance

- **Subscription**: O(1) + sorting O(n log n) where n = subscribers for that event type
- **Publishing**: O(n) where n = subscribers for that event type
- **Weak References**: Small overhead (~10% slower than strong references)
- **Thread Safety**: Lock-based, low contention

**Optimization tip:** If an event is published very frequently (>1000/sec), consider batching:

```csharp
// Instead of publishing on every mouse move:
private DateTime lastPublish = DateTime.MinValue;

void OnMouseMove(Point position)
{
    if (DateTime.Now - lastPublish > TimeSpan.FromMilliseconds(100))
    {
        EventBus.Instance.Publish(new MouseMovedEvent { Position = position });
        lastPublish = DateTime.Now;
    }
}
```

## Testing

### Unit Test Example
```csharp
[Fact]
public void Widget_PublishesEventOnAction()
{
    // Arrange
    var eventBus = new EventBus();
    ThemeChangedEvent receivedEvent = null;
    eventBus.Subscribe<ThemeChangedEvent>(evt => receivedEvent = evt);

    // Act
    var widget = new MyWidget();
    widget.ChangeTheme("Dark");

    // Assert
    receivedEvent.Should().NotBeNull();
    receivedEvent.NewThemeName.Should().Be("Dark");

    // Cleanup
    eventBus.Clear();
}
```

## PowerShell Usage

```powershell
# Get EventBus instance
$eventBus = [SuperTUI.Core.EventBus]::Instance

# Subscribe to typed event
$handler = {
    param($evt)
    Write-Host "Theme changed to: $($evt.NewThemeName)"
}

$eventBus.Subscribe([SuperTUI.Core.Events.ThemeChangedEvent], $handler)

# Publish event
$evt = New-Object SuperTUI.Core.Events.ThemeChangedEvent
$evt.NewThemeName = "Dark"
$evt.OldThemeName = "Light"
$eventBus.Publish($evt)

# Named events (easier in PowerShell)
$eventBus.Subscribe("my.event", { param($data) Write-Host "Data: $data" })
$eventBus.Publish("my.event", "Hello from PowerShell")
```

## Migration from Direct Coupling

### Before (Tight Coupling)
```csharp
public class GitStatusWidget : WidgetBase
{
    private TerminalWidget terminal; // Direct reference!

    public void OnBranchChanged(string branch)
    {
        terminal.WriteLine($"Branch changed to {branch}");
    }
}
```

### After (EventBus)
```csharp
public class GitStatusWidget : WidgetBase
{
    public void OnBranchChanged(string branch)
    {
        EventBus.Instance.Publish(new BranchChangedEvent
        {
            NewBranch = branch
        });
    }
}

public class TerminalWidget : WidgetBase
{
    public override void Initialize()
    {
        EventBus.Instance.Subscribe<BranchChangedEvent>(evt =>
        {
            WriteLine($"Branch changed to {evt.NewBranch}");
        });
    }
}
```

**Benefits:**
- No direct coupling
- TerminalWidget can be added/removed without affecting GitStatusWidget
- Other widgets can also react to branch changes
- Easier to test in isolation

---

**Related:**
- `Core/Infrastructure/Events.cs` - Pre-defined event types
- `DEPENDENCY_INJECTION.md` - DI container documentation
- `Tests/SuperTUI.Tests/Infrastructure/EventBusTests.cs` - Unit tests
