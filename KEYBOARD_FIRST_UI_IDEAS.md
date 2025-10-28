# SuperTUI Keyboard-First Context-Aware UI
**Everything Via Keyboard, Nothing Requires Mouse**

---

## Core Principle

> **Every feature must be fully usable with keyboard only. Mouse is optional convenience, never required.**

---

## TIER 1: Super Easy Keyboard Wins

### Idea 1: Auto-Expanding Detail Panel (Keyboard Driven)

**Interaction:**
```
Arrow keys navigate task list → Detail panel auto-shows for selected task
No task selected? Panel hidden
ESC from detail panel → Return to list, panel stays visible
ESC again → Hide panel completely
```

**Keys:**
- `↑`/`↓` - Navigate tasks, auto-updates detail
- `Tab` - Cycle through detail panel fields
- `Shift+Tab` - Reverse cycle
- `Esc` - Back to list (first press) or hide panel (second press)
- `Enter` - Edit selected task (full dialog)

```csharp
private TaskItem selectedTask;
private bool isDetailVisible = false;
private int escapeCount = 0;

protected override void OnKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.Up || e.Key == Key.Down)
    {
        // Navigate task list
        MoveSelection(e.Key == Key.Up ? -1 : 1);

        if (selectedTask != null)
        {
            ShowDetailPanel(selectedTask);  // Auto-show on selection
            escapeCount = 0;  // Reset escape counter
        }

        e.Handled = true;
    }
    else if (e.Key == Key.Escape)
    {
        escapeCount++;

        if (escapeCount == 1 && focusedRegion == DetailPanel)
        {
            // First ESC: Return focus to list
            FocusTaskList();
        }
        else if (escapeCount == 2 || focusedRegion == TaskList)
        {
            // Second ESC or ESC from list: Hide detail panel
            HideDetailPanel();
            escapeCount = 0;
        }

        e.Handled = true;
    }
    else if (e.Key == Key.Tab)
    {
        // Tab between list and detail panel
        if (isDetailVisible)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
                FocusPreviousRegion();
            else
                FocusNextRegion();

            e.Handled = true;
        }
    }
}
```

**No Mouse Required!**

---

### Idea 2: Live Search with Keyboard Only

**Interaction:**
```
/ → Focus search box
Type "api" → Filter updates instantly, counts update
↓ → Jump to results
ESC → Clear search, return to full list
```

**Keys:**
- `/` - Focus search box (like vim)
- Type to filter
- `↓` - Jump from search box to filtered results
- `↑` - Jump back to search box
- `Esc` - Clear search AND unfocus box
- `Enter` - Jump to first result

```csharp
private TextBox searchBox;
private bool isSearchActive = false;

protected override void OnKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.OemQuestion && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) // Forward slash
    {
        // Activate search
        searchBox.Visibility = Visibility.Visible;
        searchBox.Focus();
        searchBox.SelectAll();
        isSearchActive = true;

        e.Handled = true;
    }
}

private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Escape)
    {
        // Clear search and hide
        searchBox.Text = "";
        searchBox.Visibility = Visibility.Collapsed;
        isSearchActive = false;

        // Refocus task list
        FocusTaskList();

        e.Handled = true;
    }
    else if (e.Key == Key.Down)
    {
        // Jump to first result
        if (taskCollectionView.Cast<object>().Any())
        {
            taskListBox.SelectedIndex = 0;
            taskListBox.Focus();
        }

        e.Handled = true;
    }
    else if (e.Key == Key.Enter)
    {
        // Jump to first result and close search
        if (taskCollectionView.Cast<object>().Any())
        {
            taskListBox.SelectedIndex = 0;
            searchBox.Visibility = Visibility.Collapsed;
            isSearchActive = false;
            taskListBox.Focus();
        }

        e.Handled = true;
    }
}

private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
{
    // Debounced filter (automatic)
    searchDebounce?.Stop();
    searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
    searchDebounce.Tick += (s, args) =>
    {
        searchDebounce.Stop();
        taskCollectionView.Refresh();  // Triggers filter
        UpdateFilterCounts();  // Update "[25]" counts
    };
    searchDebounce.Start();
}
```

**Keyboard Flow:**
1. `/` → search box appears
2. Type → results filter instantly
3. `↓` or `Enter` → jump to results
4. `Esc` → clear and close

---

### Idea 3: Quick Filter Shortcuts

**Interaction:**
```
1 → Show all tasks
2 → Show active only
3 → Show completed
4 → Show today's tasks
5 → Show overdue
6-9 → Custom filters
0 → Clear all filters
```

**Keys:**
- `1`-`9` - Quick filters (when not editing text)
- `0` - Clear filters (show all)
- Filter counts update automatically
- Works from anywhere (global shortcuts)

```csharp
protected override void OnPreviewKeyDown(KeyEventArgs e)
{
    // Only handle number keys when NOT in text input
    if (Keyboard.FocusedElement is TextBox)
        return;

    if (e.Key >= Key.D0 && e.Key <= Key.D9)
    {
        int filterIndex = e.Key == Key.D0 ? 0 : (int)(e.Key - Key.D0);

        ApplyQuickFilter(filterIndex);
        e.Handled = true;
    }
}

private void ApplyQuickFilter(int index)
{
    currentFilter = index switch
    {
        0 => TaskFilter.All,
        1 => TaskFilter.All,          // Same as 0
        2 => TaskFilter.Active,
        3 => TaskFilter.Completed,
        4 => TaskFilter.Today,
        5 => TaskFilter.Overdue,
        6 => TaskFilter.ThisWeek,
        7 => TaskFilter.HighPriority,
        8 => TaskFilter.NoProject,
        9 => TaskFilter.Custom,
        _ => TaskFilter.All
    };

    // Visual feedback: flash filter label
    FlashFilterLabel(index);

    // Apply filter
    taskCollectionView.Refresh();
    UpdateFilterCounts();
}

private void FlashFilterLabel(int index)
{
    var label = filterLabels[index];

    // Animate background color
    var anim = new ColorAnimation
    {
        From = theme.Accent,
        To = theme.Surface,
        Duration = TimeSpan.FromMilliseconds(300),
        AutoReverse = false
    };

    var brush = new SolidColorBrush(theme.Accent);
    label.Background = brush;
    brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
}
```

**Display filter hints in status bar:**
```
[1]All [2]Active [3]Done [4]Today [5]Overdue [6]Week [7]High [8]NoProj [0]Clear
```

---

### Idea 4: Context Actions (No Mouse)

**Interaction:**
```
Select task → Status bar shows context actions
Press letter key → Execute action
```

**Keys change based on selection:**
```
No selection:
[n]ew [f]ilter [/]search [s]ort [g]roup

Task selected:
[e]dit [d]elete [space]toggle [t]imer [p]riority [m]ove [c]opy

Project selected:
[v]iew tasks [r]eport [s]ettings [a]rchive
```

```csharp
private void UpdateContextActions()
{
    contextActions.Clear();

    if (selectedTask != null)
    {
        RegisterAction('e', "Edit", EditSelectedTask);
        RegisterAction('d', "Delete", DeleteSelectedTask);
        RegisterAction(' ', "Toggle", ToggleTaskStatus);  // Space bar!
        RegisterAction('t', "Timer", StartTimerForTask);
        RegisterAction('p', "Priority", CyclePriority);
        RegisterAction('m', "Move", MoveTaskToProject);
        RegisterAction('c', "Copy", CopyTask);

        if (selectedTask.HasSubtasks)
        {
            RegisterAction('x', "Expand All", ExpandAllSubtasks);
            RegisterAction('z', "Collapse All", CollapseAllSubtasks);
        }
    }
    else if (selectedProject != null)
    {
        RegisterAction('v', "View Tasks", ShowProjectTasks);
        RegisterAction('r', "Report", GenerateProjectReport);
        RegisterAction('s', "Settings", EditProjectSettings);
        RegisterAction('a', "Archive", ArchiveProject);
    }
    else
    {
        RegisterAction('n', "New Task", CreateNewTask);
        RegisterAction('f', "Filter", ShowFilterMenu);
        RegisterAction('/', "Search", ActivateSearch);
        RegisterAction('s', "Sort", ShowSortMenu);
        RegisterAction('g', "Group", ShowGroupMenu);
    }

    // Update status bar
    UpdateStatusBarHints();
}

protected override void OnKeyDown(KeyEventArgs e)
{
    // Check if key matches any context action
    char key = GetCharFromKey(e.Key);

    if (contextActions.TryGetValue(key, out var action))
    {
        action.Execute();
        e.Handled = true;
    }

    base.OnKeyDown(e);
}

private void UpdateStatusBarHints()
{
    var hints = contextActions.Select(a => $"[{a.Key}]{a.Label}");
    statusBar.Text = string.Join(" ", hints);
}
```

**Everything keyboard-accessible, context-aware!**

---

### Idea 5: Quick Task Creation (No Dialogs)

**Interaction:**
```
n → Inline creation mode
Type title → Enter to create
Optional: title | project | +3 (due in 3 days) | high
ESC → Cancel
```

**Smart parsing:**
```
"Fix bug" → Simple task
"Fix bug | Work" → Task in Work project
"Fix bug | Work | +3" → Task due in 3 days
"Fix bug | Work | +3 | high" → High priority
"Fix bug | Work | 2025-11-01 | high" → Specific date
```

```csharp
private void OnCreateTaskInline(object sender, KeyEventArgs e)
{
    if (e.Key == Key.N && !isEditingText)
    {
        // Show inline creation box
        inlineCreateBox.Visibility = Visibility.Visible;
        inlineCreateBox.Text = "";
        inlineCreateBox.Focus();

        // Show hint
        statusBar.Text = "Format: title | project | due | priority  (ESC to cancel)";

        e.Handled = true;
    }
}

private void OnInlineCreateKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Enter)
    {
        // Parse and create task
        var task = ParseTaskFromInlineText(inlineCreateBox.Text);

        if (task != null)
        {
            taskService.AddTask(task);

            // Visual feedback
            FlashSuccess($"Created: {task.Title}");

            // Clear and hide
            inlineCreateBox.Text = "";
            inlineCreateBox.Visibility = Visibility.Collapsed;

            // Refocus list
            taskListBox.Focus();
        }

        e.Handled = true;
    }
    else if (e.Key == Key.Escape)
    {
        // Cancel
        inlineCreateBox.Text = "";
        inlineCreateBox.Visibility = Visibility.Collapsed;
        taskListBox.Focus();

        e.Handled = true;
    }
}

private TaskItem ParseTaskFromInlineText(string text)
{
    var parts = text.Split('|').Select(p => p.Trim()).ToArray();

    var task = new TaskItem
    {
        Title = parts[0],
        Status = TaskStatus.Pending,
        Priority = TaskPriority.Medium,
        CreatedDate = DateTime.Now
    };

    // Parse optional parts
    for (int i = 1; i < parts.Length; i++)
    {
        var part = parts[i];

        // Check if it's a date
        if (DateTime.TryParse(part, out var date))
        {
            task.DueDate = date;
        }
        // Check if it's a relative date (+3, +7, etc.)
        else if (part.StartsWith("+") && int.TryParse(part.Substring(1), out var days))
        {
            task.DueDate = DateTime.Now.AddDays(days);
        }
        // Check if it's a priority
        else if (Enum.TryParse<TaskPriority>(part, true, out var priority))
        {
            task.Priority = priority;
        }
        // Otherwise assume it's a project name
        else
        {
            var project = projectService.GetProjects().FirstOrDefault(p =>
                p.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

            if (project != null)
                task.ProjectId = project.Id;
        }
    }

    return task;
}
```

**Keyboard Flow:**
1. `n` → inline box appears
2. Type: `Fix auth bug | Backend | +3 | high`
3. `Enter` → task created
4. No dialog, no mouse, lightning fast!

---

## TIER 2: Advanced Keyboard Features

### Idea 6: Vim-Style Command Mode

**Interaction:**
```
: → Enter command mode
:filter active → Apply filter
:create Fix bug | Backend → Create task
:goto kanban → Switch to kanban view
:project Work → Switch to Work project
:sort priority → Sort by priority
:group status → Group by status
ESC → Exit command mode
```

```csharp
private TextBox commandBox;
private bool isCommandMode = false;

protected override void OnKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.OemSemicolon && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) // Colon (:)
    {
        // Enter command mode
        commandBox.Visibility = Visibility.Visible;
        commandBox.Text = ":";
        commandBox.SelectionStart = 1;  // Cursor after colon
        commandBox.Focus();
        isCommandMode = true;

        e.Handled = true;
    }
}

private void OnCommandBoxKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Enter)
    {
        // Execute command
        var command = commandBox.Text.Substring(1);  // Remove leading ":"
        ExecuteCommand(command);

        // Clear and hide
        commandBox.Text = "";
        commandBox.Visibility = Visibility.Collapsed;
        isCommandMode = false;

        taskListBox.Focus();

        e.Handled = true;
    }
    else if (e.Key == Key.Escape)
    {
        // Cancel
        commandBox.Text = "";
        commandBox.Visibility = Visibility.Collapsed;
        isCommandMode = false;

        taskListBox.Focus();

        e.Handled = true;
    }
    else if (e.Key == Key.Tab)
    {
        // Autocomplete
        var partial = commandBox.Text.Substring(1);
        var suggestions = GetCommandSuggestions(partial);

        if (suggestions.Any())
        {
            commandBox.Text = ":" + suggestions.First();
            commandBox.SelectionStart = commandBox.Text.Length;
        }

        e.Handled = true;
    }
}

private void ExecuteCommand(string command)
{
    var parts = command.Split(' ');
    var verb = parts[0].ToLower();
    var args = parts.Skip(1).ToArray();

    switch (verb)
    {
        case "filter":
        case "f":
            if (args.Any())
                ApplyFilter(string.Join(" ", args));
            break;

        case "create":
        case "new":
            if (args.Any())
            {
                var task = ParseTaskFromInlineText(string.Join(" ", args));
                taskService.AddTask(task);
                FlashSuccess($"Created: {task.Title}");
            }
            break;

        case "goto":
        case "g":
            if (args.Any())
                SwitchView(string.Join(" ", args));
            break;

        case "project":
        case "proj":
            if (args.Any())
                SetCurrentProject(string.Join(" ", args));
            break;

        case "sort":
            if (args.Any())
                ApplySort(string.Join(" ", args));
            break;

        case "group":
            if (args.Any())
                ApplyGrouping(string.Join(" ", args));
            break;

        default:
            FlashError($"Unknown command: {verb}");
            break;
    }
}

private List<string> GetCommandSuggestions(string partial)
{
    var commands = new List<string>
    {
        "filter active",
        "filter completed",
        "filter today",
        "filter overdue",
        "create ",
        "goto kanban",
        "goto list",
        "goto timeline",
        "project ",
        "sort priority",
        "sort duedate",
        "sort title",
        "group status",
        "group priority",
        "group project"
    };

    return commands
        .Where(c => c.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
        .ToList();
}
```

**Power user paradise!**

---

### Idea 7: View Mode Switching (Keyboard)

**Interaction:**
```
Alt+1 → List view
Alt+2 → Kanban view
Alt+3 → Timeline view
Alt+4 → Calendar view
Alt+5 → Table view

All views keyboard-navigable
```

```csharp
protected override void OnKeyDown(KeyEventArgs e)
{
    if (Keyboard.Modifiers == ModifierKeys.Alt)
    {
        TaskViewMode? newMode = e.Key switch
        {
            Key.D1 => TaskViewMode.List,
            Key.D2 => TaskViewMode.Kanban,
            Key.D3 => TaskViewMode.Timeline,
            Key.D4 => TaskViewMode.Calendar,
            Key.D5 => TaskViewMode.Table,
            _ => null
        };

        if (newMode.HasValue)
        {
            SwitchViewMode(newMode.Value);

            // Visual feedback
            FlashModeIndicator(newMode.Value);

            e.Handled = true;
        }
    }
}

private void SwitchViewMode(TaskViewMode mode)
{
    // Save current view state
    var state = SaveCurrentViewState();

    // Clear current view
    contentPanel.Children.Clear();

    // Create new view
    currentViewMode = mode;
    currentView = mode switch
    {
        TaskViewMode.List => CreateListView(),
        TaskViewMode.Kanban => CreateKanbanView(),
        TaskViewMode.Timeline => CreateTimelineView(),
        TaskViewMode.Calendar => CreateCalendarView(),
        TaskViewMode.Table => CreateTableView(),
        _ => CreateListView()
    };

    contentPanel.Children.Add(currentView);

    // Restore state (selection, scroll position)
    RestoreViewState(state);

    // Fade in
    FadeIn(currentView);

    // Update status bar
    statusBar.Text = $"View: {mode}  [Alt+1-5 to switch]";
}

// In Kanban view - keyboard navigation
private void OnKanbanKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.Left)
    {
        // Move to previous column
        MoveToPreviousColumn();
        e.Handled = true;
    }
    else if (e.Key == Key.Right)
    {
        // Move to next column
        MoveToNextColumn();
        e.Handled = true;
    }
    else if (e.Key == Key.Up || e.Key == Key.Down)
    {
        // Navigate within column
        MoveWithinColumn(e.Key == Key.Up ? -1 : 1);
        e.Handled = true;
    }
    else if (e.Key == Key.Space)
    {
        // Toggle status (moves to next column)
        ToggleSelectedTaskStatus();
        e.Handled = true;
    }
}

// In Timeline view - keyboard navigation
private void OnTimelineKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.Left || e.Key == Key.Right)
    {
        // Navigate between weeks
        ScrollWeeks(e.Key == Key.Left ? -1 : 1);
        e.Handled = true;
    }
    else if (e.Key == Key.Up || e.Key == Key.Down)
    {
        // Navigate tasks within week
        MoveTaskSelection(e.Key == Key.Up ? -1 : 1);
        e.Handled = true;
    }
    else if (e.Key == Key.PageUp || e.Key == Key.PageDown)
    {
        // Jump months
        ScrollWeeks(e.Key == Key.PageUp ? -4 : 4);
        e.Handled = true;
    }
}
```

**Every view fully keyboard-accessible!**

---

### Idea 8: Multi-Selection (Keyboard)

**Interaction:**
```
Space → Toggle selection on current task
Ctrl+A → Select all
Ctrl+Shift+A → Deselect all
Selected? Status bar shows bulk actions:
[d]elete all [m]ove all [p]riority [s]tatus [t]ag
```

```csharp
private HashSet<Guid> selectedTaskIds = new HashSet<Guid>();

protected override void OnKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.Space && !isEditingText)
    {
        // Toggle selection on current task
        if (highlightedTask != null)
        {
            if (selectedTaskIds.Contains(highlightedTask.Id))
                selectedTaskIds.Remove(highlightedTask.Id);
            else
                selectedTaskIds.Add(highlightedTask.Id);

            // Visual update
            RefreshTaskDisplay(highlightedTask);

            // Update action bar
            UpdateBulkActions();
        }

        e.Handled = true;
    }
    else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
    {
        // Select all
        selectedTaskIds.Clear();
        foreach (var task in visibleTasks)
            selectedTaskIds.Add(task.Id);

        RefreshAllTasks();
        UpdateBulkActions();

        e.Handled = true;
    }
    else if (e.Key == Key.A && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
    {
        // Deselect all
        selectedTaskIds.Clear();
        RefreshAllTasks();
        UpdateBulkActions();

        e.Handled = true;
    }
}

private void UpdateBulkActions()
{
    if (selectedTaskIds.Count > 1)
    {
        contextActions.Clear();

        RegisterAction('d', $"Delete {selectedTaskIds.Count}", DeleteSelectedTasks);
        RegisterAction('m', "Move to Project", MoveTasksToProject);
        RegisterAction('p', "Set Priority", SetBulkPriority);
        RegisterAction('s', "Set Status", SetBulkStatus);
        RegisterAction('t', "Add Tag", AddTagToTasks);
        RegisterAction('x', "Deselect All", () => {
            selectedTaskIds.Clear();
            RefreshAllTasks();
            UpdateBulkActions();
        });

        UpdateStatusBarHints();
    }
    else
    {
        UpdateContextActions();  // Back to single-selection actions
    }
}

// Visual indicator for selected tasks
private void RefreshTaskDisplay(TaskItem task)
{
    var item = GetListBoxItem(task);
    if (item != null)
    {
        if (selectedTaskIds.Contains(task.Id))
        {
            // Show selection indicator
            item.Background = new SolidColorBrush(theme.Selection);
            item.BorderBrush = new SolidColorBrush(theme.Accent);
            item.BorderThickness = new Thickness(2);
        }
        else
        {
            // Normal display
            item.Background = new SolidColorBrush(theme.Surface);
            item.BorderBrush = new SolidColorBrush(theme.Border);
            item.BorderThickness = new Thickness(1);
        }
    }
}
```

**Bulk operations, zero mouse!**

---

### Idea 9: Jump to Anything

**Interaction:**
```
Ctrl+J → Jump dialog
Type partial match
↓/↑ → Navigate results
Enter → Jump to selected
ESC → Cancel

Supports:
- Tasks (by title)
- Projects (by name)
- Widgets (by name)
- Workspaces (by name)
```

```csharp
private void OnJumpToAnything(object sender, KeyEventArgs e)
{
    if (e.Key == Key.J && Keyboard.Modifiers == ModifierKeys.Control)
    {
        // Show jump dialog
        ShowJumpDialog();
        e.Handled = true;
    }
}

private void ShowJumpDialog()
{
    var dialog = new JumpDialog(taskService, projectService, workspaceService);

    dialog.OnItemSelected += item =>
    {
        if (item is TaskItem task)
        {
            // Jump to task
            ApplicationContext.Instance.RequestNavigation("Tasks", task);
        }
        else if (item is Project project)
        {
            // Switch to project
            ApplicationContext.Instance.SetCurrentProject(project);
        }
        else if (item is WidgetInfo widget)
        {
            // Activate widget
            ApplicationContext.Instance.ActivateWidget(widget.Type);
        }
        else if (item is Workspace workspace)
        {
            // Switch workspace
            ApplicationContext.Instance.SwitchToWorkspace(workspace);
        }

        // Close dialog
        dialog.Close();
    };

    dialog.Show();
}

// JumpDialog implementation
public class JumpDialog : Window
{
    private TextBox searchBox;
    private ListBox resultsBox;

    public event Action<object> OnItemSelected;

    public JumpDialog(ITaskService taskService, IProjectService projectService, IWorkspaceService workspaceService)
    {
        // Build searchable items
        var allItems = new List<JumpItem>();

        // Add tasks
        foreach (var task in taskService.GetTasks())
            allItems.Add(new JumpItem { Type = "Task", Name = task.Title, Data = task });

        // Add projects
        foreach (var project in projectService.GetProjects())
            allItems.Add(new JumpItem { Type = "Project", Name = project.Name, Data = project });

        // ... Add widgets, workspaces

        // Setup UI
        searchBox.TextChanged += (s, e) =>
        {
            var searchText = searchBox.Text.ToLower();
            var matches = allItems
                .Where(item => item.Name.ToLower().Contains(searchText))
                .OrderByDescending(item => FuzzyScore(item.Name, searchText))
                .Take(15);

            resultsBox.ItemsSource = matches;
        };

        searchBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Down)
            {
                resultsBox.Focus();
                resultsBox.SelectedIndex = 0;
            }
            else if (e.Key == Key.Enter)
            {
                if (resultsBox.Items.Count > 0)
                {
                    var selected = (JumpItem)resultsBox.Items[0];
                    OnItemSelected?.Invoke(selected.Data);
                }
            }
            else if (e.Key == Key.Escape)
            {
                this.Close();
            }
        };

        resultsBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                var selected = (JumpItem)resultsBox.SelectedItem;
                OnItemSelected?.Invoke(selected.Data);
            }
            else if (e.Key == Key.Escape)
            {
                this.Close();
            }
        };
    }
}
```

**Ctrl+J, type "auth", Enter → Instantly jump to "Fix auth bug" task**

---

## TIER 3: Keyboard Power Features

### Idea 10: Macro Recording

**Interaction:**
```
Ctrl+M → Start recording macro
Perform actions (filter, select, edit, etc.)
Ctrl+M → Stop recording, prompt for name
Ctrl+Shift+M → Play last macro
Ctrl+Alt+M → Macro menu (list saved macros)
```

**Example Macro:**
1. Filter to "Today"
2. Sort by priority
3. Select first task
4. Start timer
5. Switch to Kanban view

**Playback:** One keystroke replays entire sequence!

---

## STATUS BAR: The Key to Discoverability

**Always show available actions:**

```
╔════════════════════════════════════════════════════════════╗
║  [n]ew [e]dit [d]el [space]toggle [/]search [1-5]filter   ║
╚════════════════════════════════════════════════════════════╝
```

**Context changes status bar:**
```
No selection:
[n]ew task [f]ilter [/]search [s]ort [g]roup [:]cmd [Ctrl+J]jump

Task selected:
[e]dit [d]elete [space]toggle [t]imer [p]priority [m]move [c]opy

Search active:
Type to filter | [↓]results [Esc]clear | [Enter]jump to first

Multi-select:
5 selected | [d]elete all [m]move [p]priority [s]status [x]clear

Command mode:
:filter :create :goto :project :sort :group | [Tab]complete [Esc]cancel
```

**Status bar is the UI - no hidden features!**

---

## SUMMARY: Keyboard-First Principles

### ✅ Every Feature Keyboard-Accessible
- Search: `/`
- Quick filters: `1`-`9`
- Actions: Letter keys (`n`, `e`, `d`, etc.)
- View modes: `Alt+1`-`5`
- Command mode: `:`
- Jump anywhere: `Ctrl+J`
- Multi-select: `Space`, `Ctrl+A`

### ✅ No Mouse Required
- Navigation: Arrow keys
- Selection: Space, Enter
- Context actions: Letter keys
- Bulk operations: Multi-select
- View switching: Alt+Number

### ✅ Visual Feedback
- Flash animations for actions
- Color changes for state
- Status bar updates for context
- Clear selection indicators

### ✅ Esc Always Works
- Close dialogs
- Clear search
- Exit command mode
- Deselect
- Cancel operations

### ✅ Tab for Autocomplete
- Command mode autocomplete
- Jump dialog filtering
- Form field navigation

---

## IMPLEMENTATION PRIORITY (Keyboard-First)

### Week 1 (Essentials):
1. `/` Quick search (6h)
2. `1`-`9` Quick filters (3h)
3. Context action keys (`n`, `e`, `d`, etc.) (4h)
4. Status bar hints (2h)
5. Inline task creation with `n` (4h)

**Total: 19 hours**

### Week 2 (Power Features):
6. `:` Command mode (12h)
7. `Alt+1-5` View switching (16h)
8. `Ctrl+J` Jump to anything (8h)

**Total: 36 hours**

### Week 3 (Advanced):
9. `Space` Multi-selection (6h)
10. Bulk operations (8h)
11. Macro recording (16h)

**Total: 30 hours**

---

## BOTTOM LINE

**SuperTUI should be 100% keyboard-driven with mouse as optional convenience.**

Every feature accessible via:
- Single letter keys (context-aware)
- Number keys (quick filters)
- Alt+Number (view modes)
- Ctrl+Letter (global actions)
- `:` Command mode (power users)
- `/` Search (universal)
- `Space` Multi-select (bulk ops)

**Status bar shows ALL available actions - no hidden features.**

**This is how you beat terminal TUIs: Better keyboard UX in a graphical environment.**
