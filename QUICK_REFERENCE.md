# SuperTUI Quick Reference Card

**Version:** 2025-10-27
**Features:** Hierarchical Tasks | Tags | Time Tracking | Color Themes
**Note:** All shortcuts use Ctrl (not Win key) for Windows compatibility

---

## 🎹 Keyboard Shortcuts

### Task Navigation
```
↑/↓         Navigate tasks
Enter       Edit task
Delete      Delete task (cascade if parent)
```

### Hierarchical Tasks
```
S           Create subtask under selected
C           Expand/collapse selected task
G           Expand/collapse ALL tasks
Ctrl+↑      Move task up
Ctrl+↓      Move task down
```

### Task Features
```
Ctrl+T      Edit tags (autocomplete dialog)
C           Cycle color theme
Ctrl+M      Focus note input
Ctrl+E      Export tasks
```

### Workspace & Navigation
```
Ctrl+1-9              Switch to workspace 1-9
Ctrl+N                Add widget / Command palette
Ctrl+Up/Down          Focus previous/next widget
Ctrl+Left/Right       Previous/next workspace
Ctrl+Shift+Arrows     Move widget
Ctrl+W                Close focused widget
F11                   Toggle fullscreen
Ctrl+A                Layout: Auto mode
Ctrl+Q                Exit application
Tab / Shift+Tab       Cycle focus between widgets
```

**Note:** Windows key (Win+) shortcuts not used due to OS conflicts.

---

## 🏷️ Tag System

### Quick Start
1. Select task
2. Press `Ctrl+T`
3. Type tag name → suggestions appear
4. Press `Enter` or double-click suggestion
5. Click OK

### Features
- **Autocomplete:** Type prefix, get suggestions
- **Popular Tags:** Top 10 most-used shown
- **Validation:** Max 50 chars, no spaces
- **Max:** 10 tags per task

### Tag Dialog
```
Current Tags     |  Add Tag
---------------  |  ---------
• work           |  [type here___]
• urgent         |
• backend        |  Suggestions:
                 |  • frontend
[Remove (Del)]   |  • database
                 |
                 |  Popular:
                 |  • work (45)
                 |  • urgent (23)
```

---

## 📋 Hierarchical Subtasks

### Visual Display
```
☐ · Parent Task
  └─ ☐ · Subtask 1
  └─ ☐ · Subtask 2
     └─ ☐ · Sub-subtask
```

### Icons
- `☐` Pending task
- `◐` In progress
- `☑` Completed
- `▶` Collapsed (has subtasks)
- `▼` Expanded (has subtasks)

### Creating
1. Select parent task
2. Press `S` key
3. Enter subtask title
4. Subtask appears indented

### Collapsing
- `C` - Toggle selected task
- `G` - Toggle all tasks

---

## ⏱️ Time Tracking

### Manual Timer
1. Open TimeTrackingWidget
2. Select "Manual Timer"
3. Select task
4. Click Start (timer counts up)
5. Click Stop (shows duration)

### Pomodoro Timer
1. Select "Pomodoro (25/5)"
2. Select task
3. Click Start
4. Work 25 minutes (green timer)
5. Break 5 minutes (yellow timer)
6. Repeat 4x → Long break (15 min)

### Pomodoro Cycle
```
Work (25m) → Short Break (5m) → Work (25m) → Short Break (5m)
→ Work (25m) → Short Break (5m) → Work (25m) → LONG BREAK (15m)
→ Repeat...
```

### Configuration
Edit config for custom durations:
```json
{
  "Pomodoro.WorkMinutes": 25,
  "Pomodoro.ShortBreakMinutes": 5,
  "Pomodoro.LongBreakMinutes": 15
}
```

---

## 🎨 Color Themes

### Available Themes
```
⚪ None    (Default)
🔴 Red     (Urgent/Critical)
🔵 Blue    (Work/Professional)
🟢 Green   (Personal/Health)
🟡 Yellow  (Learning/Development)
🟣 Purple  (Creative/Projects)
🟠 Orange  (Social/Events)
```

### Cycling Colors
1. Select task
2. Press `C` key (or click "Cycle Color" button)
3. Color cycles: None → Red → Blue → Green → Yellow → Purple → Orange → None

### Use Cases
- **Red:** Urgent deadlines, critical bugs
- **Blue:** Work projects, professional tasks
- **Green:** Exercise, health, personal goals
- **Yellow:** Learning, courses, documentation
- **Purple:** Creative projects, design work
- **Orange:** Social events, meetings

---

## 🚀 Quick Workflows

### Create Project with Subtasks
```
1. Create "Launch Website" task
2. Press S → "Design mockups"
3. Select "Design mockups", press S → "Homepage"
4. Select "Design mockups", press S → "Product page"
5. Select "Launch Website", press S → "Implement frontend"
Result:
☐ ! Launch Website
  └─ ☐ · Design mockups
     └─ ☐ · Homepage
     └─ ☐ · Product page
  └─ ☐ · Implement frontend
```

### Tag and Color Task
```
1. Select task
2. Press Ctrl+T → Add tags: "web", "frontend", "urgent"
3. Press C → Cycle to Purple (Creative)
Result: 🟣 ☐ · Task Title [web, frontend, urgent]
```

### Pomodoro Focus Session
```
1. Open TimeTrackingWidget
2. Select "Pomodoro (25/5)"
3. Select "Implement authentication"
4. Click Start
5. Work until notification (25 min)
6. Take break until notification (5 min)
7. Repeat 4 times
8. Take long break (15 min)
```

---

## 📊 Task Display Symbols

### Status Icons
```
☐  Pending
◐  In Progress
☑  Completed
✗  Cancelled
```

### Priority Icons
```
!  High priority
●  Medium priority
·  Low priority
```

### Special Indicators
```
⛔  Blocked (dependencies not met)
▶  Has collapsed subtasks
▼  Has expanded subtasks
└─ Tree branch
```

---

## 🔧 Common Operations

### Reorder Tasks
```
1. Select task
2. Ctrl+↑ to move up
3. Ctrl+↓ to move down
Note: Reorders within sibling group
```

### Delete with Subtasks
```
1. Select parent task
2. Press Delete
3. Confirm cascade delete
Warning: Deletes all nested subtasks!
```

### Collapse Large Hierarchy
```
1. Press G (collapse all)
2. Navigate to task
3. Press C (expand just that task)
```

### Bulk Tag Tasks
```
1. Select task
2. Ctrl+T → Add "project-x"
3. Select next task
4. Ctrl+T → Type "pro" → Select "project-x" (autocomplete)
5. Repeat for all related tasks
```

---

## 📁 File Locations

### New Features
```
/Core/Components/TreeTaskListControl.cs    - Hierarchical display
/Core/Services/TagService.cs               - Tag management
/Core/Dialogs/TagEditorDialog.cs           - Tag editor UI
/Widgets/TimeTrackingWidget.cs             - Timer widget
```

### Documentation
```
/NEW_FEATURES_GUIDE.md          - Complete user guide
/IMPLEMENTATION_SUMMARY.md      - Technical details
/PROGRESS_REPORT.md             - Development progress
/QUICK_REFERENCE.md             - This file
```

---

## ⚡ Performance Tips

### Subtask Performance
- Keep hierarchy 2-3 levels max
- Collapse completed tasks
- Use filters to reduce visible tasks

### Tag Performance
- Reuse existing tags (autocomplete)
- Limit to 3-5 tags per task
- Use consistent naming (lowercase)

### Timer Performance
- Close timer when not in use
- Reset after long sessions
- One active session at a time

---

## 🐛 Troubleshooting

### Subtasks Not Visible
```
Problem: Can't see subtasks
Fix: Press G to expand all
```

### Tag Autocomplete Empty
```
Problem: No suggestions
Fix: Tags must be used once first
```

### Pomodoro Stuck at 00:00
```
Problem: Timer not transitioning
Fix: Click Stop, then Reset, then Start
```

### Color Not Saving
```
Problem: Color reverts
Fix: Colors auto-save (check if task service running)
```

### Keyboard Shortcuts Not Working
```
Problem: Keys don't respond
Fix: Click widget to focus it first
```

---

## 📞 Getting Help

### Documentation
1. **NEW_FEATURES_GUIDE.md** - Full feature documentation
2. **IMPLEMENTATION_SUMMARY.md** - Technical reference
3. This quick reference

### Testing
- Build project: `dotnet build SuperTUI.csproj`
- Run demo: `pwsh SuperTUI.ps1`
- Check logs: Look for "TaskWidget", "TagService", "TimeTracking" entries

### Reporting Issues
Include:
- Which feature (subtasks/tags/timer/colors)
- Steps to reproduce
- Expected vs actual behavior
- Build/run logs if relevant

---

## 🎓 Learning Path

### Day 1: Basic Tasks
1. Create tasks
2. Add subtasks (press S)
3. Collapse/expand (press C/G)
4. Delete task (press Delete)

### Day 2: Organization
1. Add tags (Ctrl+T)
2. Apply colors (press C to cycle)
3. Reorder tasks (Ctrl+Up/Down)

### Day 3: Time Tracking
1. Try manual timer
2. Run Pomodoro session
3. Complete 4 Pomodoros

### Day 4: Advanced
1. Multi-level hierarchy
2. Bulk tagging workflow
3. Color coding system
4. Export data (Ctrl+E)

---

## 💡 Pro Tips

1. **Use G liberally** - Collapse all when task list gets long
2. **Tag autocomplete** - Start typing, select from suggestions
3. **Pomodoro for focus** - Deep work sessions with enforced breaks
4. **Color by context** - Blue for work, Green for personal, etc.
5. **Keyboard > Mouse** - Learn shortcuts for speed
6. **Subtasks for breaking down** - Large tasks into actionable steps
7. **Popular tags** - Check popular tags before creating new ones
8. **Collapse completed** - Hide finished parent tasks

---

**Print this card or keep handy for quick reference!**

**Version:** 1.0 | **Last Updated:** 2025-10-26
