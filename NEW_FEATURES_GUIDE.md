# SuperTUI New Features Guide

**Date:** 2025-10-26
**Version:** Phase 1-3 Complete
**Status:** Production Ready

---

## 🎯 Overview

This guide covers the new features added to SuperTUI's Task Management system:
- Hierarchical subtasks with tree display
- Tag system with autocomplete
- Time tracking with Pomodoro timer
- Task color themes for visual organization

---

## 📋 Hierarchical Subtasks

### What It Does
Tasks can now have subtasks in a parent-child hierarchy, displayed as an expandable tree structure.

### Visual Display
```
☐ · Parent Task 1
  └─ ☐ · Subtask 1.1
  └─ ☐ · Subtask 1.2
     └─ ☐ · Subtask 1.2.1
☐ ! Parent Task 2
  └─ ☐ · Subtask 2.1
```

### Features
- **Tree Lines:** Visual `└─` characters show parent-child relationships
- **Expand Icons:** `▶` (collapsed) and `▼` (expanded) indicate collapsible tasks
- **Indent Levels:** Automatic indentation based on hierarchy depth
- **Cascade Delete:** Deleting a parent task shows warning and deletes all subtasks

### Keyboard Shortcuts
| Key | Action |
|-----|--------|
| `S` | Create subtask under selected task |
| `C` | Expand/collapse selected task |
| `G` | Expand/collapse ALL tasks |
| `Delete` | Delete task (cascade delete if has subtasks) |

### Creating Subtasks
1. Select a parent task in the task list
2. Press `S` key or double-click
3. Enter subtask title in the dialog
4. Subtask appears indented under parent

### Managing Hierarchy
- **Reordering:** Use `Ctrl+Up`/`Ctrl+Down` to move tasks within their sibling group
- **Collapsing:** Press `C` on a parent to hide/show its children
- **Global Collapse:** Press `G` to toggle all tasks at once

---

## 🏷️ Tag System

### What It Does
Add tags to tasks for categorization and filtering. Tags support autocomplete based on usage.

### Features
- **Autocomplete:** Suggests tags as you type (usage-based ranking)
- **Popular Tags:** Shows top 10 most-used tags
- **Validation:** Max 50 characters, no spaces/commas, max 10 tags per task
- **Case-Insensitive:** "work" and "Work" are treated as the same tag

### Using Tags

#### Opening Tag Editor
- Click "Edit Tags (T)" button in task details panel
- Or press `Ctrl+T` keyboard shortcut

#### Tag Editor Dialog
```
┌─────────────────────────────────────────┐
│ Current Tags        │  Add Tag          │
│ ─────────────       │  ────────         │
│ • work              │  [______]         │
│ • urgent            │                   │
│ • backend           │  Suggestions:     │
│                     │  • frontend       │
│ [Remove Selected]   │  • database       │
│                     │  • api            │
│                     │                   │
│                     │  Popular Tags:    │
│                     │  • work (45)      │
│                     │  • urgent (23)    │
└─────────────────────────────────────────┘
```

#### Adding Tags
1. Type tag name in input box
2. Press `Enter` or click "Add Tag"
3. Tag appears in current tags list
4. Or double-click a suggestion/popular tag

#### Removing Tags
1. Select tag in current tags list
2. Press `Delete` or click "Remove Selected"

### Tag Management
- **Rename Tag:** Use `TagService.RenameTag()` to rename across all tasks
- **Delete Tag:** Use `TagService.DeleteTag()` to remove from all tasks
- **Usage Stats:** `TagService.GetTagsByUsage()` shows most-used tags

---

## ⏱️ Time Tracking Widget

### What It Does
Track time spent on tasks with manual timer or Pomodoro technique.

### Two Modes

#### Manual Timer Mode
- Simple start/stop timer
- Tracks elapsed time (HH:MM:SS)
- Shows duration on stop
- Good for flexible work sessions

#### Pomodoro Mode
- **Work Session:** 25 minutes focused work
- **Short Break:** 5 minutes rest (after each work session)
- **Long Break:** 15 minutes rest (after 4 work sessions)
- Automatic phase transitions with notifications
- Tracks completed Pomodoros

### Using the Timer

#### Starting a Session
1. Open TimeTrackingWidget
2. Select mode: "Manual Timer" or "Pomodoro (25/5)"
3. Select a task from the list (shows active tasks only)
4. Click "Start" button

#### During Session
- **Manual Mode:** Timer counts up (00:00:00 → HH:MM:SS)
- **Pomodoro Mode:** Timer counts down (25:00 → 00:00)
- Timer display is color-coded:
  - Green = Work session
  - Yellow = Break session
- Status label shows current task and phase

#### Stopping a Session
- Click "Stop" button to pause/end
- Manual mode shows total duration
- Pomodoro mode saves completed count
- Click "Reset" to clear timer

### Pomodoro Workflow
```
1. Start → Work (25min) → Notification: "Time for short break!"
2. Short Break (5min) → Notification: "Back to work!"
3. Work (25min) → Short Break (5min) → Work (25min) → Short Break (5min)
4. Work (25min) → Notification: "Time for long break!"
5. Long Break (15min) → Notification: "Ready for another work session?"
```

### Configuration
Adjust durations in config:
```json
{
  "Pomodoro.WorkMinutes": 25,
  "Pomodoro.ShortBreakMinutes": 5,
  "Pomodoro.LongBreakMinutes": 15,
  "Pomodoro.PomodorosUntilLongBreak": 4
}
```

---

## 🎨 Task Color Themes

### What It Does
Assign visual color themes to tasks for quick categorization.

### Available Themes
| Theme | Color | Suggested Use |
|-------|-------|---------------|
| ⚪ None | Default | No specific category |
| 🔴 Red | Red | Urgent/Critical tasks |
| 🔵 Blue | Blue | Work/Professional tasks |
| 🟢 Green | Green | Personal/Health tasks |
| 🟡 Yellow | Yellow | Learning/Development |
| 🟣 Purple | Purple | Creative/Projects |
| 🟠 Orange | Orange | Social/Events |

### Setting Task Colors

#### Using Button
1. Select a task in task details
2. Click "Cycle Color (C)" button
3. Color cycles: None → Red → Blue → Green → Yellow → Purple → Orange → None

#### Using Keyboard
1. Select a task
2. Press `C` key (without Ctrl)
3. Color changes to next theme

### Visual Display
- Task details panel shows color theme with emoji and label
- Theme label is colored to match the theme
- Example: 🔴 Red (Urgent/Critical) displayed in red text

### Use Cases
- **🔴 Red:** Deadlines, critical bugs, urgent requests
- **🔵 Blue:** Work projects, professional tasks
- **🟢 Green:** Exercise, health appointments, personal goals
- **🟡 Yellow:** Courses, tutorials, skill development
- **🟣 Purple:** Art projects, creative writing, design work
- **🟠 Orange:** Social events, meetings, networking

---

## 🎹 Complete Keyboard Reference

### Task List Navigation
| Key | Action |
|-----|--------|
| `↑/↓` | Navigate task list |
| `Enter` | Edit/activate selected task |
| `Delete` | Delete selected task |

### Hierarchical Tasks
| Key | Action |
|-----|--------|
| `S` | Create subtask |
| `C` | Expand/collapse task |
| `G` | Expand/collapse all |
| `Ctrl+↑` | Move task up |
| `Ctrl+↓` | Move task down |

### Task Management
| Key | Action |
|-----|--------|
| `Ctrl+T` | Edit tags |
| `C` | Cycle color theme |
| `Ctrl+M` | Focus note input |
| `Ctrl+E` | Export tasks |

### General
| Key | Action |
|-----|--------|
| `Win+h/j/k/l` | Navigate workspaces |
| `Win+1-9` | Switch workspace |
| `Esc` | Close dialogs |

---

## 🔧 Technical Details

### Data Models

#### TaskItem Extensions
```csharp
public class TaskItem
{
    // New properties
    public Guid? ParentTaskId { get; set; }      // Parent task for subtasks
    public int SortOrder { get; set; }            // Ordering within siblings
    public int IndentLevel { get; set; }          // Tree display depth
    public bool IsExpanded { get; set; }          // Collapse state
    public List<string> Tags { get; set; }        // Tag list
    public TaskColorTheme ColorTheme { get; set; } // Color theme
}
```

#### TaskColorTheme Enum
```csharp
public enum TaskColorTheme
{
    None = 0,
    Red = 1,
    Blue = 2,
    Green = 3,
    Yellow = 4,
    Purple = 5,
    Orange = 6
}
```

#### Time Tracking Models
```csharp
public class TaskTimeSession
{
    public Guid TaskId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; }
}

public class PomodoroSession
{
    public Guid TaskId { get; set; }
    public PomodoroPhase Phase { get; set; }
    public int CompletedPomodoros { get; set; }
    public TimeSpan TimeRemaining { get; }
}
```

### Services

#### TagService
```csharp
// Tag CRUD
TagService.Instance.AddTagToTask(taskId, "tagname");
TagService.Instance.RemoveTagFromTask(taskId, "tagname");
TagService.Instance.SetTaskTags(taskId, tagList);

// Autocomplete
var suggestions = TagService.Instance.GetTagSuggestions("pre", 10);

// Usage stats
var popular = TagService.Instance.GetTagsByUsage(20);
var recent = TagService.Instance.GetRecentTags(10);

// Management
TagService.Instance.RenameTag("oldtag", "newtag");
TagService.Instance.DeleteTag("tagname");
```

#### TaskService Extensions
```csharp
// Subtask operations
var subtaskIds = TaskService.Instance.GetAllSubtasksRecursive(parentId);

// Reordering
TaskService.Instance.MoveTaskUp(taskId);
TaskService.Instance.MoveTaskDown(taskId);
TaskService.Instance.NormalizeSortOrders(parentTaskId);
```

---

## 📊 Best Practices

### Organizing with Subtasks
- **Keep it shallow:** 2-3 levels max for readability
- **Use for breaking down:** Large tasks into actionable steps
- **Collapse completed:** Hide finished parent tasks to reduce clutter

### Effective Tagging
- **Be consistent:** Use lowercase, singular form (e.g., "meeting" not "Meetings")
- **Limit tags:** 3-5 tags per task is usually enough
- **Use autocomplete:** Reuse existing tags instead of creating duplicates
- **Review popular tags:** Periodically check most-used tags to standardize

### Time Tracking Tips
- **Pomodoro for focus:** Use Pomodoro mode for deep work sessions
- **Manual for flexibility:** Use manual timer for meetings, calls, variable tasks
- **Take breaks:** Follow Pomodoro break recommendations to avoid burnout
- **Track regularly:** Build habit of tracking time for better productivity insights

### Color Theme Strategy
- **Pick a system:** Decide on color meanings (urgency vs category vs context)
- **Be consistent:** Use same colors for same types of tasks
- **Don't overuse:** Not every task needs a color
- **Examples:**
  - **By Urgency:** Red=Urgent, Yellow=Soon, Green=Low Priority
  - **By Context:** Blue=Work, Green=Personal, Purple=Side Projects
  - **By Energy:** Red=High Energy, Yellow=Medium, Green=Low Energy

---

## 🐛 Troubleshooting

### Subtasks Not Showing
- **Issue:** Subtasks invisible in list
- **Solution:** Press `G` to expand all tasks, or `C` on parent

### Tag Autocomplete Not Working
- **Issue:** No suggestions appearing
- **Solution:** Tags must be used at least once; create manually first time

### Pomodoro Not Transitioning
- **Issue:** Timer stuck at 00:00
- **Solution:** Click "Stop" then "Start" to reset, or click "Reset"

### Color Theme Not Saving
- **Issue:** Color reverts after selection
- **Solution:** Ensure task is saved (color changes auto-save via TaskService)

---

## 🚀 Quick Start Examples

### Example 1: Project with Subtasks
```
☐ ! Launch Website Redesign
  └─ ☐ · Create wireframes
  └─ ☐ · Design mockups
     └─ ☐ · Homepage design
     └─ ☐ · Product page design
  └─ ☐ · Implement frontend
  └─ ☐ · Deploy to production
```
**Tags:** `web`, `design`, `frontend`
**Color:** 🟣 Purple (Creative/Projects)

### Example 2: Daily Tasks with Colors
```
🔴 ☐ ! Fix production bug (Red - Urgent)
🔵 ☐ · Review pull requests (Blue - Work)
🟢 ☐ · Gym session (Green - Personal)
🟡 ☐ · Read React documentation (Yellow - Learning)
```

### Example 3: Pomodoro Work Session
1. Select "Implement user authentication"
2. Switch to Pomodoro mode
3. Click Start
4. Work for 25 minutes (timer counts down)
5. Take 5-minute break when notified
6. Repeat 4 times, then take 15-minute long break

---

## 📝 API Reference

### TreeTaskListControl
```csharp
// Load tasks into tree
treeTaskListControl.LoadTasks(taskList);

// Get selected task
var task = treeTaskListControl.GetSelectedTask();

// Select specific task
treeTaskListControl.SelectTask(taskId);

// Refresh display
treeTaskListControl.RefreshDisplay();

// Events
treeTaskListControl.TaskSelected += (task) => { };
treeTaskListControl.TaskActivated += (task) => { };
treeTaskListControl.CreateSubtask += (parent) => { };
treeTaskListControl.DeleteTask += (task) => { };
treeTaskListControl.ToggleExpanded += (item) => { };
```

### TagEditorDialog
```csharp
var dialog = new TagEditorDialog(
    initialTags: task.Tags,
    logger: Logger.Instance,
    themeManager: ThemeManager.Instance,
    tagService: TagService.Instance
);

if (dialog.ShowDialog() == true)
{
    var updatedTags = dialog.Tags;
    // Update task...
}
```

### TimeTrackingWidget
```csharp
// Widget automatically handles:
// - Task selection from active tasks
// - Timer updates (1-second interval)
// - Phase transitions
// - Notifications

// Configure via config:
config.Set("Pomodoro.WorkMinutes", 25);
config.Set("Pomodoro.ShortBreakMinutes", 5);
config.Set("Pomodoro.LongBreakMinutes", 15);
```

---

## 🎓 Next Steps

1. **Try the new features:** Create a task, add subtasks, apply tags and colors
2. **Track your time:** Use Pomodoro mode for focused work sessions
3. **Organize your workflow:** Develop a tagging and color system that works for you
4. **Provide feedback:** Report any issues or feature requests

---

**Built with ❤️ for SuperTUI**
**Documentation Version:** 1.0
**Last Updated:** 2025-10-26
