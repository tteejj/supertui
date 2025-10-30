# Pane EventBus Usage Guide

## Critical Pattern: Subscribe + Unsubscribe

**Rule:** Every `EventBus.Subscribe()` call MUST have a matching `Unsubscribe()` in `OnDispose()`.

### Pattern 1: Method Reference (Recommended)

```csharp
public class MyPane : PaneBase
{
    private readonly IEventBus eventBus;

    // Store reference to handler for unsubscribe
    private Action<TaskSelectedEvent> taskSelectedHandler;

    public MyPane(IEventBus eventBus, ...other services...)
        : base(...)
    {
        this.eventBus = eventBus;
    }

    public override void Initialize()
    {
        base.Initialize();

        // Create handler once, store reference
        taskSelectedHandler = OnTaskSelected;

        // Subscribe with strong reference (default)
        eventBus.Subscribe(taskSelectedHandler);
    }

    private void OnTaskSelected(TaskSelectedEvent evt)
    {
        // Handle event
        Log($"Task selected: {evt.Task.Title}");
    }

    protected override void OnDispose()
    {
        // CRITICAL: Unsubscribe to prevent memory leak
        if (taskSelectedHandler != null)
        {
            eventBus.Unsubscribe(taskSelectedHandler);
            taskSelectedHandler = null;
        }

        base.OnDispose();
    }
}
```

### Pattern 2: Multiple Event Subscriptions

```csharp
public class NotesPane : PaneBase
{
    private readonly IEventBus eventBus;

    // Track all handlers for cleanup
    private Action<TaskSelectedEvent> taskSelectedHandler;
    private Action<ProjectChangedEvent> projectChangedHandler;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to multiple events
        taskSelectedHandler = OnTaskSelected;
        eventBus.Subscribe(taskSelectedHandler);

        projectChangedHandler = OnProjectChanged;
        eventBus.Subscribe(projectChangedHandler);
    }

    private void OnTaskSelected(TaskSelectedEvent evt)
    {
        // Filter notes to task context
        var taskNotes = allNotes.Where(n => n.Tags.Contains($"task:{evt.TaskId}"));
        DisplayFilteredNotes(taskNotes);
    }

    private void OnProjectChanged(ProjectChangedEvent evt)
    {
        // Switch to project notes folder
        LoadProjectNotes(evt.ProjectId);
    }

    protected override void OnDispose()
    {
        // CRITICAL: Unsubscribe ALL handlers
        if (taskSelectedHandler != null)
        {
            eventBus.Unsubscribe(taskSelectedHandler);
            taskSelectedHandler = null;
        }

        if (projectChangedHandler != null)
        {
            eventBus.Unsubscribe(projectChangedHandler);
            projectChangedHandler = null;
        }

        base.OnDispose();
    }
}
```

### Anti-Pattern: Lambda Subscriptions (AVOID)

```csharp
// ❌ BAD: Can't unsubscribe from lambda
eventBus.Subscribe<TaskSelectedEvent>(evt => {
    Log($"Task: {evt.Task.Title}");
});
// No way to unsubscribe! Memory leak!

// ✅ GOOD: Use method reference
taskSelectedHandler = OnTaskSelected;
eventBus.Subscribe(taskSelectedHandler);
```

## Publishing Events

### From TaskListPane

```csharp
private void OnTaskSelectionChanged()
{
    if (selectedTask != null)
    {
        eventBus.Publish(new TaskSelectedEvent
        {
            TaskId = selectedTask.Id,
            ProjectId = selectedTask.ProjectId,
            Task = selectedTask
        });
    }
}
```

### From NotesPane

```csharp
private void OnNoteSelected(NoteInfo note)
{
    eventBus.Publish(new NoteSelectedEvent
    {
        NotePath = note.Path,
        NoteName = note.Name
    });
}
```

### From FileBrowserPane

```csharp
private void OnFileSelected(FileSystemItem item)
{
    eventBus.Publish(new FileSelectedEvent
    {
        FilePath = item.FullPath,
        IsDirectory = item.IsDirectory
    });
}
```

## Cross-Pane Interactions

### Example: Task Selection Filters Notes

**TaskListPane publishes:**
```csharp
eventBus.Publish(new TaskSelectedEvent { TaskId = task.Id });
```

**NotesPane subscribes:**
```csharp
private void OnTaskSelected(TaskSelectedEvent evt)
{
    // Filter notes with tag "task:123"
    var taskNotes = allNotes.Where(n =>
        n.Tags.Contains($"task:{evt.TaskId}"));
    DisplayFilteredNotes(taskNotes);
}
```

### Example: Project Switch Updates All Panes

**MainWindow publishes:**
```csharp
eventBus.Publish(new ProjectChangedEvent
{
    ProjectId = project.Id,
    ProjectName = project.Name
});
```

**Panes subscribe:**
```csharp
private void OnProjectChanged(ProjectChangedEvent evt)
{
    // TaskListPane: Filter to project tasks
    // NotesPane: Switch to project notes folder
    // FileBrowserPane: Navigate to project directory
}
```

## Memory Leak Detection

### Symptom
Application memory grows unbounded over workspace switches.

### Cause
Panes subscribe to EventBus but don't unsubscribe in OnDispose().

### Diagnosis
```csharp
// EventBus should have stats
var stats = eventBus.GetStatistics();
Log($"Active subscriptions: {stats.TotalSubscriptions}");
// If this grows with workspace switches, you have leaks
```

### Fix
Review all `eventBus.Subscribe()` calls and ensure matching `Unsubscribe()` in `OnDispose()`.

## Testing Event Subscriptions

```csharp
[Test]
public void TaskListPane_Disposes_UnsubscribesFromEventBus()
{
    var eventBus = new EventBus(logger);
    var pane = new TaskListPane(logger, themeManager, projectContext, taskService, eventBus);

    pane.Initialize();

    var initialSubscriptions = eventBus.GetStatistics().TotalSubscriptions;

    pane.Dispose();

    var finalSubscriptions = eventBus.GetStatistics().TotalSubscriptions;

    Assert.Equal(0, finalSubscriptions - initialSubscriptions);
    // Pane should have cleaned up all subscriptions
}
```

## Best Practices

1. **Always use method references** - Store handler in field for unsubscribe
2. **Unsubscribe in OnDispose()** - Prevent memory leaks
3. **Null-check handlers** - Defensive coding
4. **Document event flow** - Comment which events pane publishes/subscribes
5. **Use weak references sparingly** - Only for long-lived objects with explicit delegate storage
6. **Publish from UI thread** - WPF controls require Dispatcher
7. **Keep handlers fast** - EventBus delivers synchronously, slow handlers block publisher

## Current Event Inventory

### Published By
- **TaskListPane**: TaskSelectedEvent (when selection changes)
- **NotesPane**: NoteSelectedEvent (when note opened)
- **FileBrowserPane**: FileSelectedEvent (when file selected)
- **MainWindow**: ProjectChangedEvent (when project context changes)

### Subscribed By
- **NotesPane**: TaskSelectedEvent (filter notes by task), ProjectChangedEvent (switch folders)
- **FileBrowserPane**: ProjectChangedEvent (navigate to project folder)
- **TaskListPane**: ProjectChangedEvent (filter tasks by project)

## Migration Checklist

- [ ] Add IEventBus to pane constructor
- [ ] Store event handler references in fields
- [ ] Subscribe in Initialize()
- [ ] Unsubscribe in OnDispose()
- [ ] Test memory leak with repeated pane create/dispose
- [ ] Document published/subscribed events in pane header comment
