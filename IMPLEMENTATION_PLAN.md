# SuperTUI Feature Implementation Plan
## Comprehensive Roadmap to Production-Ready Task Management

**Date:** 2025-10-26
**Author:** Technical Analysis
**Status:** DRAFT - Awaiting Approval
**Estimated Timeline:** 8-10 weeks (320-400 hours)

---

## Executive Summary

SuperTUI has **world-class infrastructure** (DI, error handling, logging, security) but **skeletal task management features**. This plan details how to implement the 70-75% missing functionality by porting features from `/home/teej/_tui/praxis-main` to SuperTUI.

**Current State:** Dashboard framework with basic task display
**Target State:** Production task/project management system matching _tui capabilities
**Approach:** Iterative development in 4 phases, each delivering working features

---

## Gap Analysis Summary

### What's Missing (Priority Order)

| Feature | Backend Status | UI Status | Impact | Effort |
|---------|---------------|-----------|--------|--------|
| Hierarchical Subtasks | ✅ Exists | ❌ Missing | Critical | High |
| Timer/Pomodoro Widget | ✅ Exists | ❌ Missing | Critical | Medium |
| Tag System (full) | ⚠️ Text field | ❌ Missing | Critical | Medium |
| Manual Task Reordering | ⚠️ Field exists | ❌ Missing | High | Low |
| Calendar Widget | ❌ Missing | ❌ Missing | High | High |
| Project CRUD UI | ✅ Exists | ❌ Missing | High | Medium |
| Task Color Themes | ❌ Missing | ❌ Missing | Medium | Low |
| Excel .xlsx I/O | ⚠️ Uncertain | ⚠️ Partial | High | High |
| Dependency Management | ❌ Missing | ❌ Missing | Medium | Medium |
| Advanced Filtering | ❌ Missing | ❌ Missing | Medium | Medium |
| Batch Operations | ❌ Missing | ❌ Missing | Medium | Low |
| Gap Buffer Editor | ❌ Missing | ❌ Missing | Low | High |

---

## Phase 1: Core Task Management (3 weeks, 120 hours)

**Goal:** Complete hierarchical task system with subtasks, tags, and manual ordering

### 1.1 Hierarchical Subtask UI (40 hours)

#### Current State
- `TaskService.GetSubtasks(Guid parentId)` exists
- `TaskItem.ParentTaskId` field exists
- TaskManagementWidget shows subtasks in detail panel (read-only)
- No UI to create/edit/delete subtasks

#### Implementation Plan

**A. Extend TaskItem Model (2 hours)**

Location: `/home/teej/supertui/WPF/Core/Models/TaskModels.cs`

```csharp
public class TaskItem
{
    // Add these properties if missing
    public int IndentLevel { get; set; } = 0;
    public bool IsExpanded { get; set; } = true;
    public bool HasSubtasks => SubtaskCount > 0;
    public int SubtaskCount { get; set; } = 0;

    // Add helper methods
    public bool IsSubtask => ParentTaskId != null && ParentTaskId != Guid.Empty;
}
```

**B. Create TreeTaskListControl (16 hours)**

Location: `/home/teej/supertui/WPF/Core/Components/TreeTaskListControl.cs`

New WPF UserControl replacing flat TaskListControl for hierarchical display.

**Key Features:**
- Visual tree with └─ characters (use TextBlock with "└─ ", "├─ ", "│  " prefixes)
- Expand/collapse triangles (▶/▼)
- Indentation based on IndentLevel
- Keyboard shortcuts: C (collapse/expand), G (collapse all)

**Data Structure:**
```csharp
public class TreeTaskItem
{
    public TaskItem Task { get; set; }
    public int IndentLevel { get; set; }
    public bool IsExpanded { get; set; }
    public ObservableCollection<TreeTaskItem> Children { get; set; }
}
```

**Rendering Logic:**
```csharp
private void RenderTree(ObservableCollection<TreeTaskItem> items)
{
    var flatList = new List<TreeTaskItem>();
    FlattenTree(items, flatList, 0);
    TaskListBox.ItemsSource = flatList;
}

private void FlattenTree(ObservableCollection<TreeTaskItem> items, List<TreeTaskItem> flat, int level)
{
    foreach (var item in items)
    {
        item.IndentLevel = level;
        flat.Add(item);

        if (item.IsExpanded && item.Children.Count > 0)
        {
            FlattenTree(item.Children, flat, level + 1);
        }
    }
}
```

**ItemTemplate:**
```xml
<DataTemplate>
    <StackPanel Orientation="Horizontal">
        <!-- Indentation spacer -->
        <TextBlock Width="{Binding IndentLevel, Converter={StaticResource IndentConverter}}" />

        <!-- Tree character -->
        <TextBlock Text="{Binding TreePrefix}" FontFamily="Consolas" Foreground="#666" />

        <!-- Expand/collapse triangle -->
        <TextBlock Text="{Binding ExpandIcon}" FontFamily="Consolas"
                   MouseDown="ToggleExpand" Cursor="Hand" />

        <!-- Checkbox -->
        <CheckBox IsChecked="{Binding Task.Status, Converter={StaticResource StatusToBoolConverter}}" />

        <!-- Task title -->
        <TextBlock Text="{Binding Task.Title}" />
    </StackPanel>
</DataTemplate>
```

**C. Add Subtask Operations to TaskManagementWidget (10 hours)**

Location: `/home/teej/supertui/WPF/Widgets/TaskManagementWidget.cs`

Add methods:
```csharp
private void CreateSubtask()
{
    var parentTask = GetSelectedTask();
    if (parentTask == null) return;

    var dialog = new TaskEditDialog(logger, themeManager)
    {
        Title = $"New Subtask of '{parentTask.Title}'"
    };

    if (dialog.ShowDialog() == true)
    {
        var subtask = new TaskItem
        {
            Title = dialog.TaskTitle,
            Description = dialog.TaskDescription,
            ParentTaskId = parentTask.Id,
            Status = TaskStatus.Pending,
            Priority = TaskPriority.Medium
        };

        taskService.AddTask(subtask);
        RefreshTaskList();
    }
}

private void DeleteTaskWithSubtasks()
{
    var task = GetSelectedTask();
    if (task == null) return;

    var subtasks = taskService.GetSubtasks(task.Id);
    var count = subtasks.Count;

    string message = count > 0
        ? $"Delete '{task.Title}' and {count} subtask(s)?"
        : $"Delete '{task.Title}'?";

    if (MessageBox.Show(message, "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
    {
        taskService.DeleteTask(task.Id); // Backend should cascade delete
        RefreshTaskList();
    }
}

private void ToggleExpanded()
{
    var item = GetSelectedTreeItem();
    if (item == null || !item.HasSubtasks) return;

    item.IsExpanded = !item.IsExpanded;
    RenderTree(); // Re-render to show/hide children
}
```

**Keyboard Shortcuts:**
- `S` - Create subtask under selected task
- `C` - Collapse/expand selected task
- `G` - Global collapse/expand all
- `Delete` - Delete task (with subtask cascade warning)

**D. Update TaskService Cascade Delete (4 hours)**

Location: `/home/teej/supertui/WPF/Core/Services/TaskService.cs`

```csharp
public bool DeleteTask(Guid id)
{
    lock (lockObject)
    {
        if (!tasks.ContainsKey(id))
        {
            Logger.Instance?.Warning("TaskService", $"Task not found: {id}");
            return false;
        }

        // Get all subtasks recursively
        var subtasksToDelete = GetAllSubtasksRecursive(id);

        // Delete subtasks first
        foreach (var subtaskId in subtasksToDelete)
        {
            tasks[subtaskId].Deleted = true;
        }

        // Delete parent
        tasks[id].Deleted = true;

        // Update subtask index
        if (subtaskIndex.ContainsKey(id))
        {
            subtaskIndex.Remove(id);
        }

        ScheduleSave();
        TaskDeleted?.Invoke(id);

        Logger.Instance?.Info("TaskService", $"Deleted task and {subtasksToDelete.Count} subtasks: {id}");
        return true;
    }
}

private List<Guid> GetAllSubtasksRecursive(Guid parentId)
{
    var result = new List<Guid>();

    if (!subtaskIndex.ContainsKey(parentId))
        return result;

    foreach (var childId in subtaskIndex[parentId])
    {
        if (tasks.ContainsKey(childId) && !tasks[childId].Deleted)
        {
            result.Add(childId);
            result.AddRange(GetAllSubtasksRecursive(childId)); // Recursive
        }
    }

    return result;
}
```

**E. Testing & Polish (8 hours)**
- Test expand/collapse with 100+ tasks
- Test cascade delete with 3-level nesting
- Test keyboard navigation in tree
- Visual polish (tree lines, indentation)
- Performance testing with large hierarchies

---

### 1.2 Full Tag System (24 hours)

#### Current State
- TaskItem.Tags field exists (string)
- TaskManagementWidget has Tags text input
- No tag editor, no autocomplete, no filtering

#### Implementation Plan

**A. Tag Data Model (2 hours)**

Location: `/home/teej/supertui/WPF/Core/Models/TagModels.cs`

```csharp
public class Tag
{
    public string Name { get; set; }
    public Color Color { get; set; }
    public int UsageCount { get; set; }
    public DateTime LastUsed { get; set; }
}

public static class TagExtensions
{
    public static List<string> ParseTags(this string tagString)
    {
        if (string.IsNullOrWhiteSpace(tagString))
            return new List<string>();

        return tagString.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLower())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();
    }

    public static string JoinTags(this IEnumerable<string> tags)
    {
        return string.Join(", ", tags.OrderBy(t => t));
    }
}
```

**B. TagService (8 hours)**

Location: `/home/teej/supertui/WPF/Core/Services/TagService.cs`

```csharp
public class TagService
{
    private static TagService instance;
    public static TagService Instance => instance ??= new TagService();

    private Dictionary<string, Tag> tags = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
    private readonly object lockObject = new object();

    // Events
    public event Action<Tag> TagAdded;
    public event Action<Tag> TagUpdated;
    public event Action<string> TagDeleted;

    public void Initialize(string filePath = null)
    {
        // Load tags from JSON
    }

    public List<Tag> GetAllTags()
    {
        lock (lockObject)
        {
            return tags.Values.OrderByDescending(t => t.UsageCount).ToList();
        }
    }

    public List<string> GetTagSuggestions(string prefix)
    {
        lock (lockObject)
        {
            return tags.Keys
                .Where(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => tags[t].UsageCount)
                .Take(10)
                .ToList();
        }
    }

    public void IncrementTagUsage(string tagName)
    {
        lock (lockObject)
        {
            if (!tags.ContainsKey(tagName))
            {
                tags[tagName] = new Tag
                {
                    Name = tagName,
                    Color = GenerateRandomColor(),
                    UsageCount = 0
                };
            }

            tags[tagName].UsageCount++;
            tags[tagName].LastUsed = DateTime.Now;

            ScheduleSave();
            TagUpdated?.Invoke(tags[tagName]);
        }
    }

    private Color GenerateRandomColor()
    {
        var colors = new[]
        {
            Colors.CornflowerBlue, Colors.Coral, Colors.MediumSeaGreen,
            Colors.Orchid, Colors.Gold, Colors.Tomato
        };
        return colors[new Random().Next(colors.Length)];
    }
}
```

**C. TagEditorDialog (10 hours)**

Location: `/home/teej/supertui/WPF/Core/Dialogs/TagEditorDialog.xaml.cs`

New WPF Window for tag editing.

**UI Layout:**
```
┌─────────────────────────────────────┐
│ Edit Tags                      [X]  │
├─────────────────────────────────────┤
│                                     │
│ Selected Tags:                      │
│ ┌─────────────────────────────────┐ │
│ │ [X] work    [X] urgent          │ │
│ │ [X] client-abc                  │ │
│ └─────────────────────────────────┘ │
│                                     │
│ Add New Tag:                        │
│ ┌─────────────────────────────────┐ │
│ │ #_                              │ │
│ └─────────────────────────────────┘ │
│                                     │
│ Suggestions:                        │
│ ┌─────────────────────────────────┐ │
│ │ • work (23 uses)                │ │
│ │ • urgent (15 uses)              │ │
│ │ • personal (12 uses)            │ │
│ └─────────────────────────────────┘ │
│                                     │
│         [OK]         [Cancel]       │
└─────────────────────────────────────┘
```

**Features:**
- Tag pills with X button to remove
- Autocomplete from TagService suggestions
- Click suggestion to add tag
- Visual tag colors
- Real-time validation (#work not work#)

**Implementation:**
```csharp
public partial class TagEditorDialog : Window
{
    private readonly ILogger logger;
    private readonly IThemeManager themeManager;
    private readonly TagService tagService;

    private ObservableCollection<string> selectedTags;
    private ObservableCollection<Tag> suggestions;

    public string ResultTags { get; private set; }

    public TagEditorDialog(ILogger logger, IThemeManager themeManager, TagService tagService, string initialTags)
    {
        this.logger = logger;
        this.themeManager = themeManager;
        this.tagService = tagService;

        selectedTags = new ObservableCollection<string>(initialTags.ParseTags());
        suggestions = new ObservableCollection<Tag>(tagService.GetAllTags().Take(10));

        InitializeComponent();
        DataContext = this;
    }

    private void TagInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        var input = TagInput.Text;

        if (string.IsNullOrWhiteSpace(input))
        {
            suggestions.Clear();
            foreach (var tag in tagService.GetAllTags().Take(10))
                suggestions.Add(tag);
            return;
        }

        // Remove # prefix if present
        var searchText = input.TrimStart('#');

        // Get suggestions
        var tagSuggestions = tagService.GetTagSuggestions(searchText);

        suggestions.Clear();
        foreach (var tagName in tagSuggestions)
        {
            suggestions.Add(tagService.GetTag(tagName));
        }
    }

    private void TagInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Space || e.Key == Key.Comma)
        {
            AddCurrentTag();
            e.Handled = true;
        }
    }

    private void AddCurrentTag()
    {
        var input = TagInput.Text.TrimStart('#').Trim();

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (!selectedTags.Contains(input, StringComparer.OrdinalIgnoreCase))
        {
            selectedTags.Add(input);
            tagService.IncrementTagUsage(input);
        }

        TagInput.Clear();
    }

    private void RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var tag = button.Tag as string;
        selectedTags.Remove(tag);
    }

    private void Suggestion_Click(object sender, MouseButtonEventArgs e)
    {
        var tag = (sender as FrameworkElement).DataContext as Tag;

        if (!selectedTags.Contains(tag.Name, StringComparer.OrdinalIgnoreCase))
        {
            selectedTags.Add(tag.Name);
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        ResultTags = selectedTags.JoinTags();
        DialogResult = true;
        Close();
    }
}
```

**D. Tag Filtering in TaskManagementWidget (4 hours)**

Add filter dropdown:
```csharp
private void PopulateTagFilter()
{
    var allTags = tagService.GetAllTags();

    TagFilterComboBox.Items.Clear();
    TagFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Tags", Tag = null });

    foreach (var tag in allTags)
    {
        var item = new ComboBoxItem
        {
            Content = $"#{tag.Name} ({tag.UsageCount})",
            Tag = tag.Name
        };
        TagFilterComboBox.Items.Add(item);
    }
}

private void TagFilter_Changed(object sender, SelectionChangedEventArgs e)
{
    var selectedItem = TagFilterComboBox.SelectedItem as ComboBoxItem;
    var filterTag = selectedItem?.Tag as string;

    if (string.IsNullOrEmpty(filterTag))
    {
        currentFilter = null;
    }
    else
    {
        currentFilter = task => task.Tags.ParseTags().Contains(filterTag, StringComparer.OrdinalIgnoreCase);
    }

    RefreshTaskList();
}
```

**E. Register TagService in DI (2 hours)**

Location: `/home/teej/supertui/WPF/Core/DI/ServiceRegistration.cs`

```csharp
container.RegisterSingleton<Core.Services.TagService, Core.Services.TagService>(Core.Services.TagService.Instance);

// Initialize
var tagService = container.GetRequiredService<Core.Services.TagService>();
tagService?.Initialize();
```

---

### 1.3 Manual Task Reordering (16 hours)

#### Current State
- TaskItem.SortOrder field exists but unused
- No UI to manually reorder tasks
- Tasks sorted by default criteria (date, priority, etc.)

#### Implementation Plan

**A. Update TaskService with Reordering (4 hours)**

Location: `/home/teej/supertui/WPF/Core/Services/TaskService.cs`

```csharp
public void MoveTaskUp(Guid taskId)
{
    lock (lockObject)
    {
        if (!tasks.ContainsKey(taskId))
            return;

        var task = tasks[taskId];
        var siblingTasks = GetSiblingTasks(task);

        // Find current position
        int currentIndex = siblingTasks.FindIndex(t => t.Id == taskId);
        if (currentIndex <= 0)
            return; // Already at top

        // Swap sort orders
        var previousTask = siblingTasks[currentIndex - 1];
        int tempOrder = task.SortOrder;
        task.SortOrder = previousTask.SortOrder;
        previousTask.SortOrder = tempOrder;

        task.ModifiedAt = DateTime.Now;
        previousTask.ModifiedAt = DateTime.Now;

        ScheduleSave();
        TaskUpdated?.Invoke(task);
        TaskUpdated?.Invoke(previousTask);

        Logger.Instance?.Info("TaskService", $"Moved task up: {task.Title}");
    }
}

public void MoveTaskDown(Guid taskId)
{
    lock (lockObject)
    {
        if (!tasks.ContainsKey(taskId))
            return;

        var task = tasks[taskId];
        var siblingTasks = GetSiblingTasks(task);

        // Find current position
        int currentIndex = siblingTasks.FindIndex(t => t.Id == taskId);
        if (currentIndex < 0 || currentIndex >= siblingTasks.Count - 1)
            return; // Already at bottom

        // Swap sort orders
        var nextTask = siblingTasks[currentIndex + 1];
        int tempOrder = task.SortOrder;
        task.SortOrder = nextTask.SortOrder;
        nextTask.SortOrder = tempOrder;

        task.ModifiedAt = DateTime.Now;
        nextTask.ModifiedAt = DateTime.Now;

        ScheduleSave();
        TaskUpdated?.Invoke(task);
        TaskUpdated?.Invoke(nextTask);

        Logger.Instance?.Info("TaskService", $"Moved task down: {task.Title}");
    }
}

private List<TaskItem> GetSiblingTasks(TaskItem task)
{
    // Get all tasks with same parent
    Func<TaskItem, bool> siblingFilter = t =>
        t.ParentTaskId == task.ParentTaskId &&
        !t.Deleted;

    return tasks.Values
        .Where(siblingFilter)
        .OrderBy(t => t.SortOrder)
        .ThenBy(t => t.CreatedAt)
        .ToList();
}

public void NormalizeSortOrders(Guid? parentTaskId = null)
{
    lock (lockObject)
    {
        // Get all tasks with this parent
        var tasksToNormalize = tasks.Values
            .Where(t => t.ParentTaskId == parentTaskId && !t.Deleted)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        // Assign sequential sort orders (0, 100, 200, ...)
        for (int i = 0; i < tasksToNormalize.Count; i++)
        {
            tasksToNormalize[i].SortOrder = i * 100;
        }

        ScheduleSave();
        Logger.Instance?.Info("TaskService", $"Normalized sort orders for {tasksToNormalize.Count} tasks");
    }
}
```

**B. Add Keyboard Shortcuts to TaskManagementWidget (6 hours)**

Location: `/home/teej/supertui/WPF/Widgets/TaskManagementWidget.cs`

```csharp
protected override void OnWidgetKeyDown(KeyEventArgs e)
{
    var isCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

    if (isCtrl && e.Key == Key.Up)
    {
        MoveSelectedTaskUp();
        e.Handled = true;
    }
    else if (isCtrl && e.Key == Key.Down)
    {
        MoveSelectedTaskDown();
        e.Handled = true;
    }

    base.OnWidgetKeyDown(e);
}

private void MoveSelectedTaskUp()
{
    var task = GetSelectedTask();
    if (task == null) return;

    int previousIndex = taskListControl.SelectedIndex;

    taskService.MoveTaskUp(task.Id);

    // Refresh and restore selection to moved task
    RefreshTaskList();

    if (previousIndex > 0)
        taskListControl.SelectedIndex = previousIndex - 1;
}

private void MoveSelectedTaskDown()
{
    var task = GetSelectedTask();
    if (task == null) return;

    int previousIndex = taskListControl.SelectedIndex;

    taskService.MoveTaskDown(task.Id);

    // Refresh and restore selection to moved task
    RefreshTaskList();

    if (previousIndex < taskListControl.Items.Count - 1)
        taskListControl.SelectedIndex = previousIndex + 1;
}
```

**C. Visual Feedback During Move (4 hours)**

Add temporary highlight animation when task moves:

```csharp
private async void MoveSelectedTaskUp()
{
    var task = GetSelectedTask();
    if (task == null) return;

    int previousIndex = taskListControl.SelectedIndex;

    // Highlight task being moved
    HighlightTask(task, Colors.Yellow);

    taskService.MoveTaskUp(task.Id);

    await Task.Delay(100); // Brief pause for visual feedback

    RefreshTaskList();

    if (previousIndex > 0)
        taskListControl.SelectedIndex = previousIndex - 1;

    // Flash new position
    HighlightTask(task, Colors.Green);
    await Task.Delay(200);
    ClearHighlight(task);
}

private void HighlightTask(TaskItem task, Color color)
{
    var listBoxItem = GetListBoxItemForTask(task);
    if (listBoxItem != null)
    {
        var originalBg = listBoxItem.Background;
        listBoxItem.Background = new SolidColorBrush(color);
        listBoxItem.Tag = originalBg; // Store for restore
    }
}

private void ClearHighlight(TaskItem task)
{
    var listBoxItem = GetListBoxItemForTask(task);
    if (listBoxItem != null && listBoxItem.Tag is Brush originalBg)
    {
        listBoxItem.Background = originalBg;
    }
}
```

**D. Update StandardWidgetFrame Shortcuts (2 hours)**

Add to keyboard shortcuts footer:
```csharp
frame.SetStandardShortcuts("Ctrl+↑/↓: Move | Enter: Edit | Space: Toggle | S: Subtask | R: Tags | Del: Delete | F5: Refresh | ?: Help");
```

---

### 1.4 Integration & Testing (40 hours)

**A. Update TaskEditDialog (8 hours)**

Location: `/home/teej/supertui/WPF/Core/Dialogs/TaskEditDialog.xaml.cs`

Add tag editor button:
```xml
<Label Content="Tags:" />
<StackPanel Orientation="Horizontal">
    <TextBox x:Name="TagsTextBox" Width="250" />
    <Button Content="Edit..." Click="EditTags_Click" />
</StackPanel>
```

```csharp
private void EditTags_Click(object sender, RoutedEventArgs e)
{
    var dialog = new TagEditorDialog(logger, themeManager, tagService, TagsTextBox.Text);

    if (dialog.ShowDialog() == true)
    {
        TagsTextBox.Text = dialog.ResultTags;
    }
}
```

**B. End-to-End Testing (20 hours)**

Test scenarios:
1. Create task with 3 levels of subtasks (parent → child → grandchild)
2. Expand/collapse at each level
3. Add tags via tag editor, verify autocomplete works
4. Filter by tag, verify correct tasks shown
5. Move task up/down, verify sort order persists
6. Move task with subtasks, verify children move together
7. Delete parent task, verify cascade delete prompts and works
8. Create 100 tasks, verify tree rendering performance
9. Switch workspaces, verify subtask state preserved
10. Export to CSV, verify subtasks and tags included

**C. Performance Optimization (8 hours)**

- Profile TreeTaskListControl rendering with 500+ tasks
- Implement virtualization if needed (VirtualizingStackPanel)
- Cache flattened tree to avoid recalculation on each render
- Optimize tag autocomplete with debouncing (300ms delay)
- Add loading indicators for slow operations

**D. Documentation (4 hours)**

Update `/home/teej/supertui/WPF/KEYBOARD_IMPLEMENTATION_GUIDE.md`:
- Ctrl+Up/Down - Move task up/down
- S - Create subtask
- C - Collapse/expand task
- G - Collapse/expand all
- R - Edit tags

---

## Phase 2: Time Tracking & Projects (2 weeks, 80 hours)

**Goal:** Add timer/Pomodoro widget and project CRUD UI

### 2.1 TimeTrackingWidget (40 hours)

#### Current State
- TimeTrackingService fully implemented (673 lines)
- Timer start/stop, manual entry, reports, CSV export all work
- ProjectStatsWidget shows time totals (read-only)
- NO widget to interact with TimeTrackingService

#### Implementation Plan

**A. TimeTrackingWidget UI Design (8 hours)**

Location: `/home/teej/supertui/WPF/Widgets/TimeTrackingWidget.cs`

**Layout:**
```
┌─────────────────────────────────────────────────┐
│ TIME TRACKING                              [?]  │
├─────────────────────────────────────────────────┤
│                                                 │
│ ┌───────────────────────────────────────────┐   │
│ │ ACTIVE TIMER                              │   │
│ │                                           │   │
│ │   Project: [Work - PROJ-123        ▼]    │   │
│ │   Description: Code review meeting        │   │
│ │                                           │   │
│ │        ⏱ 01:23:45                         │   │
│ │                                           │   │
│ │      [⏸ Stop]     [⏱ Lap]                │   │
│ └───────────────────────────────────────────┘   │
│                                                 │
│ ┌───────────────────────────────────────────┐   │
│ │ QUICK ACTIONS                             │   │
│ │                                           │   │
│ │  [🍅 Pomodoro (25m)]  [☕ Break (5m)]    │   │
│ │  [📝 Log Time]        [📊 View Report]    │   │
│ └───────────────────────────────────────────┘   │
│                                                 │
│ ┌───────────────────────────────────────────┐   │
│ │ RECENT ENTRIES                            │   │
│ │                                           │   │
│ │ Today                          Total: 6.5h│   │
│ │ ─────────────────────────────────────────│   │
│ │ 01:23 Work/PROJ-123    Code review  2.5h │   │
│ │ 09:45 Personal         Email check  0.5h │   │
│ │ 11:30 Work/PROJ-456    Bug fixes    3.5h │   │
│ │                                           │   │
│ │ Yesterday                      Total: 7.0h│   │
│ │ ─────────────────────────────────────────│   │
│ │ 10:00 Work/PROJ-123    Planning     2.0h │   │
│ │ 14:00 Work/PROJ-123    Development  5.0h │   │
│ │                                           │   │
│ │                            [Load More...] │   │
│ └───────────────────────────────────────────┘   │
│                                                 │
│ ↑/↓: Navigate | Enter: Edit | Del: Delete      │
│ T: Start Timer | P: Pomodoro | L: Log Time     │
└─────────────────────────────────────────────────┘
```

**B. Timer State Management (8 hours)**

```csharp
public class TimeTrackingWidget : WidgetBase, IThemeable
{
    private readonly ILogger logger;
    private readonly IThemeManager themeManager;
    private readonly TimeTrackingService timeService;
    private readonly ProjectService projectService;

    private Timer updateTimer;
    private TimeEntry activeTimer;
    private DateTime timerStartTime;
    private PomodoroState pomodoroState;

    public TimeTrackingWidget(
        ILogger logger,
        IThemeManager themeManager,
        TimeTrackingService timeService,
        ProjectService projectService)
    {
        this.logger = logger;
        this.themeManager = themeManager;
        this.timeService = timeService;
        this.projectService = projectService;

        WidgetName = "Time Tracking";
        BuildUI();

        // Update timer display every second
        updateTimer = new Timer(UpdateTimerDisplay, null, 1000, 1000);
    }

    private void StartTimer()
    {
        if (activeTimer != null)
        {
            logger.Warning("TimeTracking", "Timer already running");
            return;
        }

        var selectedProject = ProjectComboBox.SelectedItem as Project;
        var description = DescriptionTextBox.Text;

        if (selectedProject == null)
        {
            MessageBox.Show("Please select a project", "Error");
            return;
        }

        activeTimer = new TimeEntry
        {
            Id = Guid.NewGuid(),
            ProjectId = selectedProject.Id,
            Date = DateTime.Today,
            Description = description,
            StartTime = DateTime.Now
        };

        timerStartTime = DateTime.Now;

        // Update UI
        TimerPanel.Visibility = Visibility.Visible;
        StartButton.Visibility = Visibility.Collapsed;
        StopButton.Visibility = Visibility.Visible;

        logger.Info("TimeTracking", $"Started timer for {selectedProject.Name}");
    }

    private void StopTimer()
    {
        if (activeTimer == null)
            return;

        var elapsed = DateTime.Now - timerStartTime;

        activeTimer.EndTime = DateTime.Now;
        activeTimer.Duration = elapsed.TotalHours;

        // Save to TimeTrackingService
        timeService.AddEntry(activeTimer);

        // Update UI
        TimerPanel.Visibility = Visibility.Collapsed;
        StartButton.Visibility = Visibility.Visible;
        StopButton.Visibility = Visibility.Collapsed;

        // Refresh recent entries
        RefreshRecentEntries();

        logger.Info("TimeTracking", $"Stopped timer: {elapsed.TotalHours:F2}h");

        activeTimer = null;
    }

    private void UpdateTimerDisplay(object state)
    {
        if (activeTimer == null)
            return;

        Dispatcher.Invoke(() =>
        {
            var elapsed = DateTime.Now - timerStartTime;
            TimerLabel.Text = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

            // Update pomodoro progress if active
            if (pomodoroState != null && pomodoroState.IsActive)
            {
                UpdatePomodoroDisplay(elapsed);
            }
        });
    }
}
```

**C. Pomodoro Timer Implementation (12 hours)**

```csharp
public class PomodoroState
{
    public bool IsActive { get; set; }
    public PomodoroPhase Phase { get; set; } // Work, ShortBreak, LongBreak
    public int CompletedPomodoros { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public TimeSpan TotalTime { get; set; }
}

public enum PomodoroPhase
{
    Work,        // 25 minutes
    ShortBreak,  // 5 minutes
    LongBreak    // 15 minutes (after 4 pomodoros)
}

private void StartPomodoro()
{
    pomodoroState = new PomodoroState
    {
        IsActive = true,
        Phase = PomodoroPhase.Work,
        CompletedPomodoros = 0,
        TotalTime = TimeSpan.FromMinutes(25),
        RemainingTime = TimeSpan.FromMinutes(25)
    };

    StartTimer(); // Start underlying timer

    PomodoroPanel.Visibility = Visibility.Visible;
    UpdatePomodoroDisplay(TimeSpan.Zero);
}

private void UpdatePomodoroDisplay(TimeSpan elapsed)
{
    var remaining = pomodoroState.TotalTime - elapsed;

    if (remaining <= TimeSpan.Zero)
    {
        // Pomodoro phase complete
        CompletePomodoro();
        return;
    }

    pomodoroState.RemainingTime = remaining;

    // Update UI
    PomodoroTimeLabel.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    PomodoroPhaseLabel.Text = pomodoroState.Phase == PomodoroPhase.Work ? "🍅 WORK" : "☕ BREAK";
    PomodoroProgressBar.Value = (elapsed.TotalSeconds / pomodoroState.TotalTime.TotalSeconds) * 100;
    PomodoroCountLabel.Text = $"{pomodoroState.CompletedPomodoros}/4";
}

private void CompletePomodoro()
{
    StopTimer(); // Stop and log time

    if (pomodoroState.Phase == PomodoroPhase.Work)
    {
        pomodoroState.CompletedPomodoros++;

        // Play notification sound
        SystemSounds.Asterisk.Play();

        // Show notification
        MessageBox.Show("Pomodoro complete! Time for a break.", "🍅 Pomodoro", MessageBoxButton.OK);

        // Auto-start break
        if (pomodoroState.CompletedPomodoros % 4 == 0)
        {
            StartLongBreak();
        }
        else
        {
            StartShortBreak();
        }
    }
    else
    {
        // Break complete
        MessageBox.Show("Break complete! Ready to work?", "☕ Break", MessageBoxButton.OK);
        pomodoroState = null;
        PomodoroPanel.Visibility = Visibility.Collapsed;
    }
}

private void StartShortBreak()
{
    pomodoroState.Phase = PomodoroPhase.ShortBreak;
    pomodoroState.TotalTime = TimeSpan.FromMinutes(5);
    pomodoroState.RemainingTime = TimeSpan.FromMinutes(5);

    StartTimer();
}

private void StartLongBreak()
{
    pomodoroState.Phase = PomodoroPhase.LongBreak;
    pomodoroState.TotalTime = TimeSpan.FromMinutes(15);
    pomodoroState.RemainingTime = TimeSpan.FromMinutes(15);

    StartTimer();
}
```

**D. Manual Time Entry Dialog (8 hours)**

Location: `/home/teej/supertui/WPF/Core/Dialogs/TimeEntryDialog.xaml.cs`

```xml
<Window x:Class="TimeEntryDialog">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <Label Grid.Row="0" Content="Date:" />
        <DatePicker Grid.Row="0" x:Name="DatePicker" Margin="0,0,0,10" />

        <Label Grid.Row="1" Content="Project:" />
        <ComboBox Grid.Row="1" x:Name="ProjectComboBox" Margin="0,0,0,10" />

        <Label Grid.Row="2" Content="Duration (hours):" />
        <TextBox Grid.Row="2" x:Name="DurationTextBox" Text="1.0" Margin="0,0,0,10" />

        <Label Grid.Row="3" Content="Description:" />
        <TextBox Grid.Row="4" x:Name="DescriptionTextBox"
                 TextWrapping="Wrap" AcceptsReturn="True"
                 MinHeight="100" Margin="0,0,0,10" />

        <StackPanel Grid.Row="6" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Save" Click="Save_Click" Width="80" Margin="0,0,10,0" />
            <Button Content="Cancel" Click="Cancel_Click" Width="80" />
        </StackPanel>
    </Grid>
</Window>
```

```csharp
private void Save_Click(object sender, RoutedEventArgs e)
{
    if (ProjectComboBox.SelectedItem == null)
    {
        MessageBox.Show("Please select a project", "Validation Error");
        return;
    }

    if (!double.TryParse(DurationTextBox.Text, out double duration) || duration <= 0)
    {
        MessageBox.Show("Please enter a valid duration", "Validation Error");
        return;
    }

    var project = ProjectComboBox.SelectedItem as Project;

    var entry = new TimeEntry
    {
        Id = Guid.NewGuid(),
        ProjectId = project.Id,
        Date = DatePicker.SelectedDate ?? DateTime.Today,
        Duration = duration,
        Description = DescriptionTextBox.Text,
        StartTime = DateTime.Now, // Approximate
        EndTime = DateTime.Now.AddHours(duration)
    };

    ResultEntry = entry;
    DialogResult = true;
    Close();
}
```

**E. Testing & Integration (4 hours)**

Test scenarios:
1. Start timer, verify display updates every second
2. Stop timer after 5 minutes, verify entry saved to TimeTrackingService
3. Start Pomodoro, verify 25-minute countdown
4. Complete Pomodoro, verify notification and auto-break start
5. Log manual time entry, verify added to recent entries list
6. Switch projects while timer running, verify prompt to stop first
7. Close application with timer running, verify auto-save on exit
8. View time report, verify CSV export works

---

### 2.2 Project CRUD UI (40 hours)

#### Current State
- ProjectService fully implemented (753 lines)
- All CRUD operations work
- Projects only created via Excel import
- NO UI to create/edit projects

#### Implementation Plan

**A. ProjectManagementWidget (20 hours)**

Location: `/home/teej/supertui/WPF/Widgets/ProjectManagementWidget.cs`

**Layout:**
```
┌─────────────────────────────────────────────────┐
│ PROJECT MANAGEMENT                         [?]  │
├─────────────────────────────────────────────────┤
│                                                 │
│ [New Project]  [Archive]  [Filter: All    ▼]   │
│                                                 │
│ ┌───────────────────────────────────────────┐   │
│ │ ● PROJ-123 - Website Redesign            │   │
│ │   Client: Acme Corp | Status: Active     │   │
│ │   Tasks: 15 (8 done) | Time: 45.5h       │   │
│ │                                           │   │
│ │ ● PROJ-456 - Mobile App                  │   │
│ │   Client: Beta Inc | Status: Active      │   │
│ │   Tasks: 23 (12 done) | Time: 67.2h      │   │
│ │                                           │   │
│ │ ○ PROJ-789 - Database Migration          │   │
│ │   Client: Gamma LLC | Status: Archived   │   │
│ │   Tasks: 45 (45 done) | Time: 120.0h     │   │
│ └───────────────────────────────────────────┘   │
│                                                 │
│ ┌───────────────────────────────────────────┐   │
│ │ PROJECT DETAILS                           │   │
│ │                                           │   │
│ │ ID: PROJ-123                              │   │
│ │ Name: Website Redesign                    │   │
│ │ Status: Active                            │   │
│ │ Priority: High                            │   │
│ │                                           │   │
│ │ Client: Acme Corp                         │   │
│ │ ID2: ACME-2024-WEB                        │   │
│ │ CAAName: Operations                       │   │
│ │                                           │   │
│ │ Description:                              │   │
│ │ Complete redesign of company website     │   │
│ │ with modern UI/UX and responsive design.  │   │
│ │                                           │   │
│ │ Dates:                                    │   │
│ │ Start: 2025-09-15                         │   │
│ │ Due: 2025-12-31                           │   │
│ │ Progress: 53%  [████████░░░░░░░░]         │   │
│ │                                           │   │
│ │         [Edit]      [Delete]              │   │
│ └───────────────────────────────────────────┘   │
│                                                 │
│ Enter: Edit | Del: Delete | A: Archive         │
└─────────────────────────────────────────────────┘
```

**Implementation:**
```csharp
public class ProjectManagementWidget : WidgetBase, IThemeable
{
    private readonly ILogger logger;
    private readonly IThemeManager themeManager;
    private readonly ProjectService projectService;
    private readonly TaskService taskService;
    private readonly TimeTrackingService timeService;

    private StandardWidgetFrame frame;
    private ListBox projectListBox;
    private StackPanel detailPanel;
    private ObservableCollection<ProjectDisplayItem> projects;

    public ProjectManagementWidget(
        ILogger logger,
        IThemeManager themeManager,
        ProjectService projectService,
        TaskService taskService,
        TimeTrackingService timeService)
    {
        this.logger = logger;
        this.themeManager = themeManager;
        this.projectService = projectService;
        this.taskService = taskService;
        this.timeService = timeService;

        WidgetName = "Project Management";
        BuildUI();
    }

    public class ProjectDisplayItem
    {
        public Project Project { get; set; }
        public int TaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public double TotalHours { get; set; }
        public string DisplayText { get; set; }
    }

    private void RefreshProjectList()
    {
        var allProjects = projectService.GetAllProjects(includeArchived: ShowArchivedCheckBox.IsChecked ?? false);

        projects.Clear();

        foreach (var project in allProjects)
        {
            var tasks = taskService.GetTasks(t => t.ProjectId == project.Id);
            var timeEntries = timeService.GetEntriesForProject(project.Id);

            var item = new ProjectDisplayItem
            {
                Project = project,
                TaskCount = tasks.Count,
                CompletedTaskCount = tasks.Count(t => t.Status == TaskStatus.Completed),
                TotalHours = timeEntries.Sum(e => e.Duration),
                DisplayText = $"{project.Id1} - {project.Name}"
            };

            projects.Add(item);
        }

        logger.Info("ProjectManagement", $"Loaded {projects.Count} projects");
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProjectEditDialog(logger, themeManager);

        if (dialog.ShowDialog() == true)
        {
            var project = dialog.ResultProject;
            projectService.AddProject(project);

            RefreshProjectList();
            logger.Info("ProjectManagement", $"Created project: {project.Name}");
        }
    }

    private void EditProject_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = projectListBox.SelectedItem as ProjectDisplayItem;
        if (selectedItem == null) return;

        var dialog = new ProjectEditDialog(logger, themeManager, selectedItem.Project);

        if (dialog.ShowDialog() == true)
        {
            var updatedProject = dialog.ResultProject;
            projectService.UpdateProject(updatedProject);

            RefreshProjectList();
            logger.Info("ProjectManagement", $"Updated project: {updatedProject.Name}");
        }
    }

    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = projectListBox.SelectedItem as ProjectDisplayItem;
        if (selectedItem == null) return;

        var project = selectedItem.Project;

        var result = MessageBox.Show(
            $"Delete project '{project.Name}'?\n\nWarning: Tasks associated with this project will lose their project reference.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            projectService.DeleteProject(project.Id);
            RefreshProjectList();
            logger.Info("ProjectManagement", $"Deleted project: {project.Name}");
        }
    }

    private void ArchiveProject_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = projectListBox.SelectedItem as ProjectDisplayItem;
        if (selectedItem == null) return;

        var project = selectedItem.Project;
        project.Archived = !project.Archived;

        projectService.UpdateProject(project);
        RefreshProjectList();

        logger.Info("ProjectManagement", $"{(project.Archived ? "Archived" : "Unarchived")} project: {project.Name}");
    }
}
```

**B. ProjectEditDialog (16 hours)**

Location: `/home/teej/supertui/WPF/Core/Dialogs/ProjectEditDialog.xaml.cs`

Complete form with all Project fields:
- Name, Description
- Status (Active, On Hold, Completed, Cancelled)
- Priority (Low, Medium, High, Critical)
- ID1, ID2, Nickname, CAAName (custom fields)
- Start Date, Due Date, Completion Date
- Budget, Estimated Hours
- Client Name, Client Contact

```csharp
public partial class ProjectEditDialog : Window
{
    public Project ResultProject { get; private set; }

    public ProjectEditDialog(ILogger logger, IThemeManager themeManager, Project existingProject = null)
    {
        InitializeComponent();

        if (existingProject != null)
        {
            // Edit mode - populate fields
            Title = "Edit Project";
            PopulateFields(existingProject);
        }
        else
        {
            // New mode
            Title = "New Project";
            ResultProject = new Project { Id = Guid.NewGuid() };
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Project name is required", "Validation Error");
            return;
        }

        if (string.IsNullOrWhiteSpace(ID1TextBox.Text))
        {
            MessageBox.Show("Project ID is required", "Validation Error");
            return;
        }

        // Build project object
        ResultProject.Name = NameTextBox.Text;
        ResultProject.Description = DescriptionTextBox.Text;
        ResultProject.Status = (ProjectStatus)StatusComboBox.SelectedItem;
        ResultProject.Priority = (ProjectPriority)PriorityComboBox.SelectedItem;
        ResultProject.Id1 = ID1TextBox.Text;
        ResultProject.Id2 = ID2TextBox.Text;
        ResultProject.Nickname = NicknameTextBox.Text;
        ResultProject.CAAName = CAANameTextBox.Text;
        ResultProject.StartDate = StartDatePicker.SelectedDate;
        ResultProject.DueDate = DueDatePicker.SelectedDate;
        ResultProject.CompletionDate = CompletionDatePicker.SelectedDate;

        if (double.TryParse(BudgetTextBox.Text, out double budget))
            ResultProject.Budget = budget;

        if (double.TryParse(EstimatedHoursTextBox.Text, out double hours))
            ResultProject.EstimatedHours = hours;

        ResultProject.ClientName = ClientNameTextBox.Text;
        ResultProject.ClientContact = ClientContactTextBox.Text;

        DialogResult = true;
        Close();
    }
}
```

**C. Integration with TaskManagementWidget (4 hours)**

Add project selector to TaskEditDialog:
```xml
<Label Content="Project:" />
<ComboBox x:Name="ProjectComboBox" DisplayMemberPath="Name" />
```

Update TaskManagementWidget to filter by project:
```csharp
private void ProjectFilter_Changed(object sender, SelectionChangedEventArgs e)
{
    var selectedProject = ProjectFilterComboBox.SelectedItem as Project;

    if (selectedProject == null)
    {
        currentFilter = null;
    }
    else
    {
        currentFilter = task => task.ProjectId == selectedProject.Id;
    }

    RefreshTaskList();
}
```

---

## Phase 3: Calendar & Advanced Features (2 weeks, 80 hours)

**Goal:** Calendar widget, task color themes, enhanced Excel integration

### 3.1 CalendarWidget (40 hours)

#### Implementation Plan

**A. Calendar UI (24 hours)**

Location: `/home/teej/supertui/WPF/Widgets/CalendarWidget.cs`

**Month View Layout:**
```
┌─────────────────────────────────────────────────┐
│ CALENDAR                      October 2025 [?] │
├─────────────────────────────────────────────────┤
│                                                 │
│  [◀ Prev]  [Today]  [Next ▶]   [View: Month ▼] │
│                                                 │
│ ┌───────────────────────────────────────────┐   │
│ │ Sun   Mon   Tue   Wed   Thu   Fri   Sat  │   │
│ │                  1     2     3     4    5 │   │
│ │   6     7     8     9    10    11   12   │   │
│ │  13    14    15    16    17    18   19   │   │
│ │  20    21    22    23    24    25 • 26   │   │
│ │  27    28    29    30    31              │   │
│ └───────────────────────────────────────────┘   │
│                                                 │
│ ┌───────────────────────────────────────────┐   │
│ │ October 26, 2025 (Today)                  │   │
│ │                                           │   │
│ │ ⚠ OVERDUE (2)                             │   │
│ │   ☐ Fix login bug                        │   │
│ │   ☐ Update documentation                 │   │
│ │                                           │   │
│ │ 📌 TODAY (4)                              │   │
│ │   ☐ Code review                          │   │
│ │   ☐ Team meeting @ 2pm                   │   │
│ │   ☑ Write tests                          │   │
│ │   ☐ Deploy to staging                    │   │
│ │                                           │   │
│ │ 📅 UPCOMING (1)                           │   │
│ │   ☐ Oct 28: Client presentation          │   │
│ └───────────────────────────────────────────┘   │
│                                                 │
│ ←/→: Change Month | ↑/↓: Select Day | Enter    │
└─────────────────────────────────────────────────┘
```

**Implementation:**
```csharp
public class CalendarWidget : WidgetBase, IThemeable
{
    private readonly ILogger logger;
    private readonly IThemeManager themeManager;
    private readonly TaskService taskService;

    private StandardWidgetFrame frame;
    private Grid calendarGrid;
    private StackPanel taskListPanel;

    private DateTime currentMonth;
    private DateTime selectedDate;
    private CalendarView currentView = CalendarView.Month;

    public enum CalendarView
    {
        Month,
        Week,
        Day
    }

    public CalendarWidget(
        ILogger logger,
        IThemeManager themeManager,
        TaskService taskService)
    {
        this.logger = logger;
        this.themeManager = themeManager;
        this.taskService = taskService;

        WidgetName = "Calendar";
        currentMonth = DateTime.Today;
        selectedDate = DateTime.Today;

        BuildUI();
    }

    private void BuildUI()
    {
        frame = new StandardWidgetFrame(themeManager)
        {
            Title = "CALENDAR"
        };

        var mainPanel = new DockPanel { Margin = new Thickness(15) };

        // Navigation bar
        var navPanel = CreateNavigationBar();
        DockPanel.SetDock(navPanel, Dock.Top);
        mainPanel.Children.Add(navPanel);

        // Calendar grid
        calendarGrid = CreateCalendarGrid();
        DockPanel.SetDock(calendarGrid, Dock.Top);
        mainPanel.Children.Add(calendarGrid);

        // Task list for selected date
        taskListPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        mainPanel.Children.Add(taskListPanel);

        frame.Content = mainPanel;
        this.Content = frame;

        RenderCalendar();
    }

    private Grid CreateCalendarGrid()
    {
        var grid = new Grid();

        // 7 columns for days of week
        for (int i = 0; i < 7; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // 7 rows (header + 6 weeks max)
        for (int i = 0; i < 7; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        // Day headers
        var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        for (int i = 0; i < 7; i++)
        {
            var header = new TextBlock
            {
                Text = dayNames[i],
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(5)
            };
            Grid.SetColumn(header, i);
            Grid.SetRow(header, 0);
            grid.Children.Add(header);
        }

        return grid;
    }

    private void RenderCalendar()
    {
        // Clear existing day cells (keep headers)
        var toRemove = calendarGrid.Children.Cast<UIElement>()
            .Where(e => Grid.GetRow(e) > 0)
            .ToList();

        foreach (var element in toRemove)
            calendarGrid.Children.Remove(element);

        // Get first day of month and number of days
        var firstDay = new DateTime(currentMonth.Year, currentMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month);
        var startDayOfWeek = (int)firstDay.DayOfWeek;

        // Render days
        int row = 1;
        int col = startDayOfWeek;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(currentMonth.Year, currentMonth.Month, day);
            var cell = CreateDayCell(date);

            Grid.SetColumn(cell, col);
            Grid.SetRow(cell, row);
            calendarGrid.Children.Add(cell);

            col++;
            if (col >= 7)
            {
                col = 0;
                row++;
            }
        }

        // Update selected date display
        UpdateSelectedDateTasks();
    }

    private Border CreateDayCell(DateTime date)
    {
        var theme = themeManager.CurrentTheme;

        // Get tasks for this date
        var tasks = taskService.GetTasks(t =>
            t.DueDate.HasValue &&
            t.DueDate.Value.Date == date.Date &&
            !t.Deleted);

        var isToday = date.Date == DateTime.Today;
        var isSelected = date.Date == selectedDate.Date;
        var hasTasks = tasks.Count > 0;

        var cell = new Border
        {
            BorderBrush = isSelected ? new SolidColorBrush(theme.Primary) : new SolidColorBrush(theme.Border),
            BorderThickness = isSelected ? new Thickness(2) : new Thickness(1),
            Background = isToday ? new SolidColorBrush(Color.FromArgb(30, 100, 200, 255)) : Brushes.Transparent,
            Padding = new Thickness(5),
            Margin = new Thickness(2),
            Cursor = Cursors.Hand,
            Tag = date
        };

        cell.MouseDown += DayCell_Click;

        var panel = new StackPanel();

        // Day number
        var dayLabel = new TextBlock
        {
            Text = date.Day.ToString(),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 14,
            FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
            Foreground = new SolidColorBrush(theme.Foreground)
        };
        panel.Children.Add(dayLabel);

        // Task indicator
        if (hasTasks)
        {
            var taskIndicator = new TextBlock
            {
                Text = $"• {tasks.Count}",
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.Info),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(taskIndicator);
        }

        cell.Child = panel;
        return cell;
    }

    private void DayCell_Click(object sender, MouseButtonEventArgs e)
    {
        var cell = sender as Border;
        var date = (DateTime)cell.Tag;

        selectedDate = date;
        RenderCalendar(); // Re-render to update selection
    }

    private void UpdateSelectedDateTasks()
    {
        taskListPanel.Children.Clear();

        // Header
        var header = new TextBlock
        {
            Text = selectedDate.ToString("MMMM d, yyyy") + (selectedDate.Date == DateTime.Today ? " (Today)" : ""),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        taskListPanel.Children.Add(header);

        // Get tasks
        var overdue = taskService.GetTasks(t =>
            t.DueDate.HasValue &&
            t.DueDate.Value.Date < selectedDate.Date &&
            t.Status != TaskStatus.Completed &&
            !t.Deleted);

        var today = taskService.GetTasks(t =>
            t.DueDate.HasValue &&
            t.DueDate.Value.Date == selectedDate.Date &&
            !t.Deleted);

        var upcoming = taskService.GetTasks(t =>
            t.DueDate.HasValue &&
            t.DueDate.Value.Date > selectedDate.Date &&
            t.DueDate.Value.Date <= selectedDate.Date.AddDays(7) &&
            !t.Deleted);

        // Render sections
        if (overdue.Count > 0)
            RenderTaskSection("⚠ OVERDUE", overdue, Colors.Red);

        if (today.Count > 0)
            RenderTaskSection("📌 TODAY", today, Colors.Orange);

        if (upcoming.Count > 0)
            RenderTaskSection("📅 UPCOMING", upcoming, Colors.Blue);

        if (overdue.Count == 0 && today.Count == 0 && upcoming.Count == 0)
        {
            var noTasks = new TextBlock
            {
                Text = "No tasks for this date",
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray
            };
            taskListPanel.Children.Add(noTasks);
        }
    }

    private void RenderTaskSection(string title, List<TaskItem> tasks, Color color)
    {
        var sectionHeader = new TextBlock
        {
            Text = $"{title} ({tasks.Count})",
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(color),
            Margin = new Thickness(0, 5, 0, 5)
        };
        taskListPanel.Children.Add(sectionHeader);

        foreach (var task in tasks.Take(5)) // Show first 5
        {
            var taskPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 2, 0, 2)
            };

            var checkbox = new CheckBox
            {
                IsChecked = task.Status == TaskStatus.Completed,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkbox.Checked += (s, e) => ToggleTaskComplete(task);
            checkbox.Unchecked += (s, e) => ToggleTaskComplete(task);

            var titleText = new TextBlock
            {
                Text = task.Title,
                Margin = new Thickness(5, 0, 0, 0),
                TextDecorations = task.Status == TaskStatus.Completed ? TextDecorations.Strikethrough : null
            };

            taskPanel.Children.Add(checkbox);
            taskPanel.Children.Add(titleText);
            taskListPanel.Children.Add(taskPanel);
        }

        if (tasks.Count > 5)
        {
            var moreLink = new TextBlock
            {
                Text = $"   ... and {tasks.Count - 5} more",
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray,
                Margin = new Thickness(10, 2, 0, 2)
            };
            taskListPanel.Children.Add(moreLink);
        }
    }

    private void ToggleTaskComplete(TaskItem task)
    {
        task.Status = task.Status == TaskStatus.Completed ? TaskStatus.Pending : TaskStatus.Completed;
        task.ModifiedAt = DateTime.Now;

        if (task.Status == TaskStatus.Completed)
        {
            task.CompletionDate = DateTime.Now;
            task.Progress = 100;
        }
        else
        {
            task.CompletionDate = null;
        }

        taskService.UpdateTask(task);
        UpdateSelectedDateTasks();
        RenderCalendar(); // Update task indicators
    }

    protected override void OnWidgetKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Left)
        {
            PreviousMonth();
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            NextMonth();
            e.Handled = true;
        }
        else if (e.Key == Key.T)
        {
            GoToToday();
            e.Handled = true;
        }

        base.OnWidgetKeyDown(e);
    }

    private void PreviousMonth()
    {
        currentMonth = currentMonth.AddMonths(-1);
        RenderCalendar();
    }

    private void NextMonth()
    {
        currentMonth = currentMonth.AddMonths(1);
        RenderCalendar();
    }

    private void GoToToday()
    {
        currentMonth = DateTime.Today;
        selectedDate = DateTime.Today;
        RenderCalendar();
    }
}
```

**B. Week View (8 hours)**

Add week view rendering:
```csharp
private void RenderWeekView()
{
    // 7-day horizontal display
    // Similar to month but only one row of days
}
```

**C. Integration & Testing (8 hours)**

- Add to default workspace layouts
- Test with 100+ tasks across multiple dates
- Performance testing with date range queries
- Visual polish and theme application

---

### 3.2 Task Color Themes (16 hours)

**A. Add Theme Field to TaskItem (2 hours)**

```csharp
public class TaskItem
{
    public TaskColorTheme ColorTheme { get; set; } = TaskColorTheme.Default;
}

public enum TaskColorTheme
{
    Default,   // Gray
    Urgent,    // Red
    Work,      // Blue
    Personal,  // Green
    Project,   // Purple
    Completed  // Strikethrough gray
}
```

**B. Update TaskListControl Rendering (8 hours)**

```csharp
private Brush GetTaskBackgroundBrush(TaskItem task)
{
    var theme = themeManager.CurrentTheme;

    return task.ColorTheme switch
    {
        TaskColorTheme.Urgent => new SolidColorBrush(Color.FromArgb(30, 255, 0, 0)),
        TaskColorTheme.Work => new SolidColorBrush(Color.FromArgb(30, 0, 100, 255)),
        TaskColorTheme.Personal => new SolidColorBrush(Color.FromArgb(30, 0, 200, 100)),
        TaskColorTheme.Project => new SolidColorBrush(Color.FromArgb(30, 150, 0, 255)),
        TaskColorTheme.Completed => new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
        _ => Brushes.Transparent
    };
}
```

**C. Add Theme Cycling Shortcut (4 hours)**

```csharp
protected override void OnWidgetKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.T && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
    {
        CycleTaskTheme();
        e.Handled = true;
    }

    base.OnWidgetKeyDown(e);
}

private void CycleTaskTheme()
{
    var task = GetSelectedTask();
    if (task == null) return;

    var themes = Enum.GetValues(typeof(TaskColorTheme)).Cast<TaskColorTheme>().ToList();
    var currentIndex = themes.IndexOf(task.ColorTheme);
    var nextIndex = (currentIndex + 1) % themes.Count;

    task.ColorTheme = themes[nextIndex];
    task.ModifiedAt = DateTime.Now;

    taskService.UpdateTask(task);
    RefreshTaskList();

    statusText.Text = $"Theme: {task.ColorTheme}";
}
```

**D. Testing (2 hours)**

---

### 3.3 Enhanced Excel Integration (24 hours)

**A. Excel File Reading (12 hours)**

Add Microsoft.Office.Interop.Excel NuGet package:
```bash
dotnet add package Microsoft.Office.Interop.Excel
```

Update ExcelImportWidget to read .xlsx files:
```csharp
private void BrowseFile_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog
    {
        Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
        Title = "Select Excel File"
    };

    if (dialog.ShowDialog() == true)
    {
        SourceFileTextBox.Text = dialog.FileName;
        LoadExcelFile(dialog.FileName);
    }
}

private void LoadExcelFile(string filePath)
{
    try
    {
        var excelApp = new Microsoft.Office.Interop.Excel.Application();
        var workbook = excelApp.Workbooks.Open(filePath);
        var worksheet = (Microsoft.Office.Interop.Excel.Worksheet)workbook.Sheets[1];

        // Read data using ExcelMappingService profile
        var profile = excelMappingService.GetActiveProfile();
        var data = new Dictionary<string, string>();

        foreach (var mapping in profile.FieldMappings)
        {
            var cellValue = worksheet.Range[mapping.SourceCell].Value2?.ToString() ?? "";
            data[mapping.FieldName] = cellValue;
        }

        // Display preview
        PreviewDataGrid.ItemsSource = data.Select(kvp => new { Field = kvp.Key, Value = kvp.Value });

        // Cleanup
        workbook.Close(false);
        excelApp.Quit();

        System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
        System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
        System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
    }
    catch (Exception ex)
    {
        logger.Error("ExcelImport", $"Failed to read Excel file: {ex.Message}", ex);
        MessageBox.Show($"Error reading Excel file: {ex.Message}", "Error");
    }
}
```

**B. Field Mapping UI (12 hours)**

Create ExcelFieldMappingWidget to edit mappings in UI (3-column grid similar to _tui).

---

## Phase 4: Advanced Features (1 week, 40 hours)

**Goal:** Dependency management, advanced filtering, batch operations

### 4.1 Dependency Management (20 hours)

**A. Add Dependency Fields to TaskItem (2 hours)**

```csharp
public class TaskItem
{
    public List<Guid> BlockedBy { get; set; } = new List<Guid>();
    public List<Guid> Blocks { get; set; } = new List<Guid>();
}
```

**B. DependencyService (8 hours)**

```csharp
public class DependencyService
{
    public void AddDependency(Guid taskId, Guid blockerTaskId) { }
    public void RemoveDependency(Guid taskId, Guid blockerTaskId) { }
    public List<TaskItem> GetBlockers(Guid taskId) { }
    public List<TaskItem> GetBlocked(Guid taskId) { }
    public bool HasCircularDependency(Guid taskId, Guid blockerTaskId) { }
    public List<(Guid, Guid)> GetDependencyGraph() { }
}
```

**C. DependencyEditorDialog (8 hours)**

UI to add/remove blockers for selected task.

**D. Testing (2 hours)**

---

### 4.2 Advanced Filtering (12 hours)

**A. FilterBuilder UI (8 hours)**

Dialog with:
- Add criterion button
- Multiple AND/OR conditions
- Field selector (Status, Priority, Tags, DueDate, etc.)
- Operator selector (Equals, Contains, Before, After, etc.)
- Value input
- Save filter preset

**B. Integration (4 hours)**

Add to TaskManagementWidget filter dropdown.

---

### 4.3 Batch Operations (8 hours)

**A. Multi-Select in TaskListControl (4 hours)**

Enable Ctrl+Click and Shift+Click selection.

**B. Batch Action Menu (4 hours)**

Right-click menu:
- Set Priority (all selected)
- Set Status (all selected)
- Add Tag (all selected)
- Delete (all selected)

---

## Testing Strategy

### Unit Testing (20 hours total)

**Test Files to Create:**
1. `TaskServiceTests.cs` - Subtask operations, cascade delete
2. `TagServiceTests.cs` - Tag CRUD, autocomplete
3. `TimeTrackingServiceTests.cs` - Timer, manual entry, reports
4. `ProjectServiceTests.cs` - Project CRUD
5. `DependencyServiceTests.cs` - Dependency graph, circular detection

**Coverage Goals:**
- 80% code coverage for services
- 100% coverage for critical paths (cascade delete, dependency checks)

### Integration Testing (16 hours total)

**Test Scenarios:**
1. Create task → add subtask → delete parent (verify cascade)
2. Start timer → close app → restart (verify persistence)
3. Create 1000 tasks → test tree rendering performance
4. Import Excel → edit projects → export Excel (round-trip)
5. Add dependencies → detect circular (verify validation)

### Manual Testing (24 hours total)

**Test Plan:**
1. Full workflow testing (create project → tasks → subtasks → time tracking → reports)
2. Keyboard shortcut testing (all combinations)
3. Visual polish testing (themes, colors, layouts)
4. Performance testing (large datasets: 10k tasks, 500 projects)
5. Error handling testing (corrupt data, Excel failures, etc.)

---

## Timeline & Resource Allocation

### Phase 1: Core Task Management (3 weeks)
- Week 1: Hierarchical subtasks (40h)
- Week 2: Tag system (24h) + Manual reordering (16h)
- Week 3: Integration & testing (40h)

### Phase 2: Time Tracking & Projects (2 weeks)
- Week 4: TimeTrackingWidget with Pomodoro (40h)
- Week 5: ProjectManagementWidget (40h)

### Phase 3: Calendar & Advanced (2 weeks)
- Week 6: CalendarWidget (40h)
- Week 7: Task themes (16h) + Excel enhancement (24h)

### Phase 4: Advanced Features (1 week)
- Week 8: Dependencies (20h) + Filtering (12h) + Batch ops (8h)

**Total Estimated Time:** 8 weeks = 320 hours

---

## Risk Mitigation

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Excel COM automation fails on Linux | High | Medium | Graceful degradation to clipboard mode |
| Tree rendering performance issues | Medium | High | Implement virtualization early |
| Circular dependency bugs | Medium | High | Extensive testing, graph validation |
| WPF threading issues with timers | Low | Medium | Use Dispatcher for all UI updates |

### Schedule Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Scope creep | High | High | Strict phase boundaries, feature freeze |
| Unforeseen bugs | Medium | Medium | 20% time buffer in each phase |
| Testing takes longer | Medium | Medium | Automated tests where possible |

---

## Success Criteria

### Phase 1 Complete When:
- [ ] Can create 3-level task hierarchy with visual tree
- [ ] Expand/collapse works with keyboard (C, G keys)
- [ ] Cascade delete prompts and works correctly
- [ ] Tag editor has autocomplete from usage history
- [ ] Tag filtering works in TaskManagementWidget
- [ ] Ctrl+Up/Down moves tasks with visual feedback
- [ ] 100+ task performance is acceptable (<500ms render)

### Phase 2 Complete When:
- [ ] Timer starts/stops and logs time entries
- [ ] Pomodoro 25-minute countdown with notifications
- [ ] Manual time entry dialog saves to TimeTrackingService
- [ ] Recent entries list shows last 20 entries
- [ ] Projects can be created/edited with all fields
- [ ] Project list shows task counts and time totals
- [ ] Tasks can be associated with projects

### Phase 3 Complete When:
- [ ] Calendar shows month view with task indicators
- [ ] Clicking date shows tasks for that day
- [ ] Navigation (prev/next month) works
- [ ] Task color themes cycle with T key
- [ ] Excel .xlsx files can be read (if on Windows)
- [ ] Field mapping UI allows editing mappings

### Phase 4 Complete When:
- [ ] Task dependencies can be added/removed
- [ ] Circular dependency detection works
- [ ] Filter builder creates complex filters
- [ ] Batch operations work on multi-select
- [ ] All features tested and documented

---

## Deliverables

### Code
- [ ] 12 new/updated widgets
- [ ] 5 new dialogs
- [ ] 3 new services (TagService, DependencyService, enhanced TimeTrackingService UI)
- [ ] Updated TaskService, ProjectService
- [ ] 50+ unit tests
- [ ] 20+ integration tests

### Documentation
- [ ] Updated KEYBOARD_IMPLEMENTATION_GUIDE.md
- [ ] New FEATURE_GUIDE.md (user-facing)
- [ ] Updated ARCHITECTURE.md
- [ ] API documentation for new services
- [ ] Test documentation

### Training Materials
- [ ] Video walkthrough of new features (30 min)
- [ ] Quick reference card (1-page PDF)
- [ ] Migration guide from basic to advanced usage

---

## Post-Implementation

### Maintenance (Ongoing)
- Bug triage and fixes
- Performance monitoring
- User feedback incorporation
- Security updates

### Future Enhancements (Phase 5+)
- Gap buffer text editor (40h)
- Command library/macros (24h)
- Activity logging system (16h)
- Advanced reporting (charts, graphs) (32h)
- Mobile companion app (out of scope)

---

## Conclusion

This implementation plan provides a **detailed, actionable roadmap** to transform SuperTUI from a dashboard framework into a production-ready task/project management system matching _tui capabilities.

**Key Success Factors:**
1. Iterative development with working features each phase
2. Extensive testing at each phase boundary
3. Focus on core features first (subtasks, timer, projects)
4. Leverage existing infrastructure (DI, error handling, logging)
5. Port proven patterns from _tui where applicable

**Estimated Effort:** 320-400 hours (8-10 weeks at 40 hours/week)
**Recommended Team:** 2 developers + 1 tester
**Target Completion:** 10 weeks from start

This plan is **realistic, detailed, and executable** with clear success criteria and risk mitigation strategies.
