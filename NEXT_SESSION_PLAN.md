# Next Session: Quick Action Plan

## 🎯 Goal: Visible UX Improvements in 4-6 Hours

Based on your terminal-aesthetic, keyboard-driven vision and current code review.

---

## Quick Win #1: Fix Space Key (30 minutes)

**File:** `/home/teej/supertui/WPF/Panes/TaskListPane.cs`

**Problem:** Status bar says "Space: toggle" but code does "indent"

**Fix:**
```csharp
// Line 670 - Change this:
case Key.Space:
    if (selectedTask != null && !inlineEditBox.IsVisible)
    {
        IndentTask();  // ❌ Wrong
        e.Handled = true;
    }
    break;

// To this:
case Key.Space:
    if (selectedTask != null && !inlineEditBox.IsVisible)
    {
        ToggleTaskCompletion();  // ✅ Correct
        e.Handled = true;
    }
    break;
```

**Also update status bar (line 612):**
```csharp
// Change from:
"Ctrl+N: new | F2: edit | Del: delete | Space: toggle | Tab: indent"

// To:
"A: new | E: edit | D: delete | Space: toggle | C: complete | Tab: indent"
// (Make it match actual shortcuts)
```

---

## Quick Win #2: Enhance Status Bar (2 hours)

**File:** `/home/teej/supertui/WPF/Widgets/StatusBarWidget.cs`

**Goal:** Match your mockup: `[Project Alpha] [3 Tasks] [⏱️2h]`

### Current State (line 99-143):
- Project name is small
- Time is largest element
- Task count buried in middle

### Target State:
```
📁 Project Alpha  |  ✓ 3/15 Tasks  |  ⏱️ 2h 35m  |  Workspace 2
```

**Changes:**

1. **Make project name prominent:**
```csharp
// Line 99-115: Update projectLabel
projectLabel = new TextBlock
{
    Text = "No Project",
    FontSize = 20,  // Increase from 18
    FontWeight = FontWeights.Bold,  // Add bold
    Foreground = fgBrush,
    VerticalAlignment = VerticalAlignment.Center,
    Margin = new Thickness(8, 0, 16, 0)
};
```

2. **Add workspace indicator:**
```csharp
// Add after line 143
workspaceLabel = new TextBlock
{
    Text = "Workspace 1",
    FontSize = 16,
    Foreground = fgBrush,
    VerticalAlignment = VerticalAlignment.Center,
    Margin = new Thickness(8, 0, 8, 0)
};
statusPanel.Children.Add(workspaceLabel);
```

3. **Update project label format:**
```csharp
// Line 272: UpdateProjectLabel
private void UpdateProjectLabel()
{
    var project = projectContext.CurrentProject;
    projectLabel.Text = project != null
        ? $"📁 {project.Name}"  // Add icon
        : "📂 No Project";
}
```

4. **Subscribe to workspace changes** (add field):
```csharp
// In constructor, subscribe to workspace events
// This will require adding IEventBus to constructor
// And subscribing to WorkspaceSwitchRequestedEvent
```

---

## Quick Win #3: TaskListPane Publishes Events (2 hours)

**File:** `/home/teej/supertui/WPF/Panes/TaskListPane.cs`

**Goal:** Enable cross-highlighting (your vision requirement)

### Step 1: Add IEventBus to constructor

```csharp
// Line 23-30: Add field
private readonly IEventBus eventBus;
private Action<TaskSelectedEvent> taskSelectedHandler;

// Line 74: Update constructor
public TaskListPane(
    ILogger logger,
    IThemeManager themeManager,
    IConfigurationManager config,
    ITaskService taskService,
    IProjectContextManager projectContext,
    IEventBus eventBus)  // Add this
    : base(logger, themeManager, config, projectContext)
{
    this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
    this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    // ...
}
```

### Step 2: Publish on selection change

```csharp
// Find OnTaskSelectionChanged() or similar method
// Around line 400-500 where taskListBox.SelectionChanged is handled

private void OnTaskSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (taskListBox.SelectedItem is TaskViewModel vm)
    {
        selectedTask = vm;

        // Publish event for other panes
        eventBus.Publish(new TaskSelectedEvent
        {
            TaskId = vm.Task.Id,
            ProjectId = vm.Task.ProjectId,
            Task = vm.Task
        });

        UpdateStatusBar();
    }
}
```

### Step 3: Cleanup in OnDispose

```csharp
// Line 1063-1069: Update OnDispose
protected override void OnDispose()
{
    // Existing cleanup
    if (taskService != null)
    {
        taskService.TaskAdded -= OnTaskAdded;
        taskService.TaskUpdated -= OnTaskUpdated;
        taskService.TaskDeleted -= OnTaskDeleted;
    }

    // Add event bus cleanup
    if (taskSelectedHandler != null)
    {
        eventBus?.Unsubscribe(taskSelectedHandler);
        taskSelectedHandler = null;
    }

    base.OnDispose();
}
```

---

## Quick Win #4: NotesPane Subscribes to Events (1 hour)

**File:** `/home/teej/supertui/WPF/Panes/NotesPane.cs`

**Goal:** Filter notes when task selected

### Step 1: Add IEventBus

```csharp
// Add to constructor (similar to TaskListPane above)
private readonly IEventBus eventBus;
private Action<TaskSelectedEvent> taskSelectedHandler;
```

### Step 2: Subscribe in Initialize

```csharp
// In Initialize() method
public override void Initialize()
{
    base.Initialize();

    // Existing initialization...

    // Subscribe to task selection events
    taskSelectedHandler = OnTaskSelected;
    eventBus.Subscribe(taskSelectedHandler);
}
```

### Step 3: Handle event

```csharp
// Add new method
private void OnTaskSelected(TaskSelectedEvent evt)
{
    // Filter notes with tag matching task ID
    var taskTag = $"task:{evt.TaskId}";

    var matchingNotes = allNotes
        .Where(n => n.Tags != null && n.Tags.Contains(taskTag))
        .ToList();

    if (matchingNotes.Any())
    {
        // Show filtered notes
        filteredNotes = matchingNotes;
        RefreshNotesList();

        // Update status bar to show filter
        ShowStatus($"Filtered to {matchingNotes.Count} notes for task: {evt.Task.Title}");
    }
    else
    {
        // Show info message
        ShowStatus($"No notes found for task: {evt.Task.Title}", isError: false);
    }
}
```

### Step 4: Cleanup

```csharp
// In OnDispose() - around line 1549
protected override void OnDispose()
{
    // Existing cleanup...

    // Add EventBus cleanup
    if (taskSelectedHandler != null)
    {
        eventBus?.Unsubscribe(taskSelectedHandler);
        taskSelectedHandler = null;
    }

    base.OnDispose();
}
```

---

## 🧪 Testing Plan

### After Each Quick Win

1. **Build:**
   ```bash
   cd /home/teej/supertui/WPF
   dotnet build SuperTUI.csproj
   ```

2. **Fix #1 - Space Key:**
   - Open TaskListPane (Ctrl+Shift+T)
   - Select a task
   - Press Space → should toggle completion
   - Press Tab → should indent

3. **Fix #2 - Status Bar:**
   - Look at bottom of window
   - Project name should be prominent
   - Workspace number should show
   - Switch workspace (Ctrl+2) → number updates

4. **Fix #3+4 - Events:**
   - Open TaskListPane and NotesPane side-by-side
   - Select a task in TaskListPane
   - NotesPane should filter to task-related notes
   - Check logs: `%APPDATA%/SuperTUI/logs/supertui.log`
     - Should see event published/subscribed

---

## 🚀 Expected Outcome

After these 4 quick wins (4-6 hours total):

✅ **Space key works correctly** - Matches user expectation
✅ **Status bar matches your vision** - Project prominent, workspace shown
✅ **Cross-highlighting works** - Select task → notes filter automatically
✅ **Inter-pane communication enabled** - Foundation for more features

**User-Visible Impact:** Your app will feel more cohesive and polished. The "terminal-aesthetic workspace system" vision becomes tangible.

---

## 📋 Checklist

- [ ] Fix Space key in TaskListPane
- [ ] Update status bar shortcuts text
- [ ] Enhance status bar (project prominent, add workspace)
- [ ] Add IEventBus to TaskListPane constructor
- [ ] Publish TaskSelectedEvent on selection change
- [ ] Add IEventBus to NotesPane constructor
- [ ] Subscribe to TaskSelectedEvent in NotesPane
- [ ] Implement OnTaskSelected filter in NotesPane
- [ ] Add OnDispose cleanup to both panes
- [ ] Build and test each fix
- [ ] Verify logs show event publish/subscribe
- [ ] Test cross-highlighting with multiple panes

---

## 🗺️ After Quick Wins: Next Big Features

Once these quick wins are done, you'll be ready for:

**GridSplitter Implementation** - Manual pane resizing
**Flyout System** - Quick edit overlays, timer, notifications
**Visual Polish** - Animations, glow effects, scanlines

See `FIXES_COMPLETE_2025-10-30.md` for full roadmap.

---

**Created:** 2025-10-30
**Time Estimate:** 4-6 hours for all quick wins
**Difficulty:** Easy (mostly adding existing patterns)
**Impact:** High (visible UX improvements, enables your vision)
