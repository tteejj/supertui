# SuperTUI Testing Guide

**Version:** 1.0
**Date:** 2025-10-26
**Status:** Ready for Testing

---

## Overview

This guide provides comprehensive testing procedures for the newly implemented features:
- Hierarchical subtasks
- Tag system with autocomplete
- Time tracking (manual + Pomodoro)
- Task color themes

---

## Pre-Testing Setup

### Requirements
- ✅ Windows 10/11
- ✅ .NET 8.0 SDK installed
- ✅ PowerShell 7+ (optional)
- ✅ Git (for version control)

### Build Verification
```bash
cd /home/teej/supertui/WPF
dotnet build SuperTUI.csproj
```

**Expected:** Build succeeded, 0 errors

### Running the Application
```powershell
# Option 1: PowerShell script
pwsh SuperTUI.ps1

# Option 2: Direct execution
dotnet run --project WPF/SuperTUI.csproj
```

---

## Test Plan Overview

### Test Phases
1. **Unit Tests** - Individual component verification
2. **Integration Tests** - Feature interaction testing
3. **User Acceptance Tests** - Real-world scenario testing
4. **Performance Tests** - Load and stress testing
5. **Regression Tests** - Ensure existing features still work

### Test Priority
- 🔴 **Critical:** Must pass before deployment
- 🟡 **High:** Should pass, document if fails
- 🟢 **Medium:** Nice to have, can defer
- ⚪ **Low:** Future enhancement

---

## Phase 1: Unit Tests

### 1.1 TreeTaskListControl Tests 🔴

#### Test 1.1.1: Create Subtask
**Priority:** 🔴 Critical

**Steps:**
1. Launch application
2. Navigate to Task Management widget
3. Create a parent task: "Project Alpha"
4. Select "Project Alpha"
5. Press `S` key
6. Enter subtask name: "Design Phase"
7. Press OK

**Expected:**
- Subtask appears indented under parent
- Subtask shows `└─` tree line
- Parent shows `▼` expand icon

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.1.2: Multi-Level Hierarchy
**Priority:** 🔴 Critical

**Steps:**
1. Create task "A"
2. Select "A", press `S`, create subtask "A1"
3. Select "A1", press `S`, create subtask "A1.1"
4. Select "A1", press `S`, create subtask "A1.2"
5. Select "A", press `S`, create subtask "A2"

**Expected:**
```
☐ · A
  └─ ☐ · A1
     └─ ☐ · A1.1
     └─ ☐ · A1.2
  └─ ☐ · A2
```

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.1.3: Expand/Collapse Individual
**Priority:** 🔴 Critical

**Steps:**
1. Create parent task with 2 subtasks
2. Select parent task
3. Press `C` key
4. Observe subtasks disappear
5. Press `C` key again
6. Observe subtasks reappear

**Expected:**
- First `C` press: `▶` icon, subtasks hidden
- Second `C` press: `▼` icon, subtasks visible

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.1.4: Expand/Collapse All
**Priority:** 🔴 Critical

**Steps:**
1. Create 3 parent tasks, each with 2 subtasks
2. Press `G` key (collapse all)
3. Verify all tasks show `▶` icon
4. Press `G` key (expand all)
5. Verify all tasks show `▼` icon

**Expected:**
- All tasks collapse/expand together
- Icons update correctly

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.1.5: Cascade Delete
**Priority:** 🔴 Critical

**Steps:**
1. Create parent "X" with subtasks "X1", "X2"
2. Create sub-subtask "X1.1" under "X1"
3. Select parent "X"
4. Press `Delete`
5. Observe warning dialog showing count (3 subtasks)
6. Click Yes

**Expected:**
- Warning dialog shows correct count
- Parent and all subtasks deleted
- Task list updates

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 1.2 TagService Tests 🔴

#### Test 1.2.1: Add Tag
**Priority:** 🔴 Critical

**Steps:**
1. Select a task
2. Press `Ctrl+T`
3. Type "urgent" in input box
4. Press `Enter`
5. Verify tag appears in current tags list
6. Click OK
7. Verify tag shows in task details

**Expected:**
- Tag added to current tags
- Tag persisted after dialog close
- Tag visible in details panel

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.2.2: Tag Autocomplete
**Priority:** 🔴 Critical

**Steps:**
1. Add tag "work" to task A
2. Close tag editor
3. Select task B, press `Ctrl+T`
4. Type "wo" in input box
5. Observe suggestions list

**Expected:**
- "work" appears in suggestions
- Can click to add
- Can press Enter to add

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.2.3: Popular Tags
**Priority:** 🟡 High

**Steps:**
1. Add tag "meeting" to 5 different tasks
2. Add tag "urgent" to 3 different tasks
3. Add tag "review" to 2 different tasks
4. Open tag editor on new task
5. Check popular tags section

**Expected:**
- "meeting (5)" appears first
- "urgent (3)" appears second
- "review (2)" appears third
- Can click to add

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.2.4: Tag Validation
**Priority:** 🔴 Critical

**Steps:**
1. Try to add tag with space: "my tag"
2. Try to add tag with comma: "tag,name"
3. Try to add 51-character tag
4. Try to add 11th tag (max is 10)

**Expected:**
- All invalid tags rejected
- Error messages shown
- Valid tags accepted

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.2.5: Remove Tag
**Priority:** 🔴 Critical

**Steps:**
1. Add 3 tags to a task
2. Press `Ctrl+T`
3. Select middle tag in current tags list
4. Press `Delete` key
5. Click OK
6. Reopen tag editor

**Expected:**
- Tag removed from list
- Other tags remain
- Change persisted

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 1.3 TimeTrackingWidget Tests 🔴

#### Test 1.3.1: Manual Timer Start/Stop
**Priority:** 🔴 Critical

**Steps:**
1. Open TimeTrackingWidget
2. Select "Manual Timer" mode
3. Select a task from list
4. Click "Start" button
5. Wait 10 seconds
6. Click "Stop" button
7. Observe duration dialog

**Expected:**
- Timer counts up: 00:00:00 → 00:00:10
- Timer display is green
- Stop shows duration dialog
- Duration is ~10 seconds

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.3.2: Pomodoro Complete Cycle
**Priority:** 🔴 Critical

**Steps:**
1. **WARNING:** This test takes 30 minutes
2. Open TimeTrackingWidget
3. Select "Pomodoro (25/5)" mode
4. Select a task
5. Click "Start"
6. Wait 25 minutes (work session)
7. Click OK on "Short break" notification
8. Wait 5 minutes (break)
9. Click OK on "Back to work" notification

**Expected:**
- Timer counts down: 25:00 → 00:00
- Work timer is green
- Break timer is yellow
- Notifications at transitions
- Completed count increments

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

**Note:** For faster testing, modify config:
```json
{
  "Pomodoro.WorkMinutes": 1,
  "Pomodoro.ShortBreakMinutes": 1
}
```

---

#### Test 1.3.3: Pomodoro Long Break
**Priority:** 🟡 High

**Steps:**
1. Set short durations in config (1 min each)
2. Complete 4 Pomodoro cycles
3. Observe 4th break notification

**Expected:**
- After 4th work session: "Long break" notification
- Timer shows 15:00 (or configured duration)
- Completed count = 4

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.3.4: Timer Reset
**Priority:** 🔴 Critical

**Steps:**
1. Start manual timer
2. Wait 30 seconds
3. Click "Reset" button
4. Observe timer display

**Expected:**
- Timer resets to 00:00:00
- Session cleared
- Can start new session

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.3.5: Mode Switching
**Priority:** 🟡 High

**Steps:**
1. Start manual timer
2. Let run for 10 seconds
3. Change mode to "Pomodoro (25/5)"
4. Observe timer state

**Expected:**
- Manual timer stops
- Pomodoro timer ready (25:00)
- No session active

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 1.4 Color Theme Tests 🔴

#### Test 1.4.1: Cycle Color
**Priority:** 🔴 Critical

**Steps:**
1. Select a task
2. Press `C` key 8 times
3. Observe color changes

**Expected Sequence:**
1. ⚪ None (Default)
2. 🔴 Red (Urgent/Critical)
3. 🔵 Blue (Work/Professional)
4. 🟢 Green (Personal/Health)
5. 🟡 Yellow (Learning/Development)
6. 🟣 Purple (Creative/Projects)
7. 🟠 Orange (Social/Events)
8. ⚪ None (cycles back)

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.4.2: Color Persistence
**Priority:** 🔴 Critical

**Steps:**
1. Set task to 🔴 Red
2. Close application
3. Reopen application
4. Select same task

**Expected:**
- Color is still 🔴 Red
- Color persisted across sessions

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

#### Test 1.4.3: Color Button
**Priority:** 🟡 High

**Steps:**
1. Select a task
2. Click "Cycle Color (C)" button
3. Observe color change

**Expected:**
- Color cycles same as C key
- Button click works

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

## Phase 2: Integration Tests

### 2.1 Subtasks + Tags 🔴

**Steps:**
1. Create parent task "Project"
2. Add tags "work", "urgent"
3. Create subtask "Phase 1"
4. Add tags "design", "frontend"
5. Verify parent has 2 tags, subtask has 2 tags

**Expected:**
- Tags are independent
- Both tasks show their own tags
- No tag inheritance

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 2.2 Subtasks + Reordering 🔴

**Steps:**
1. Create parent "X" with 3 subtasks: "X1", "X2", "X3"
2. Select "X2"
3. Press `Ctrl+Down`
4. Verify order: "X1", "X3", "X2"
5. Press `Ctrl+Up` twice
6. Verify order: "X2", "X1", "X3"

**Expected:**
- Reordering works within siblings
- Parent stays in place
- Visual order updates

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 2.3 Time Tracking + Tags 🟡

**Steps:**
1. Create task with tags "work", "sprint-1"
2. Start Pomodoro timer on this task
3. Complete work session
4. Verify task still has tags

**Expected:**
- Tags persist during timing
- No interference between features

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 2.4 Color + Subtasks 🟡

**Steps:**
1. Create parent "Project" with color 🔵 Blue
2. Create subtask "Task 1" with color 🔴 Red
3. Create subtask "Task 2" with color 🟢 Green
4. Collapse parent
5. Expand parent

**Expected:**
- Each task retains its own color
- Colors visible in tree
- Collapse/expand preserves colors

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

## Phase 3: User Acceptance Tests

### 3.1 Real-World Scenario: Project Management 🔴

**Scenario:** Manage a website redesign project

**Steps:**
1. Create parent: "Website Redesign" (🟣 Purple, tags: "project", "web")
2. Create subtask: "Research Phase" (🟡 Yellow, tags: "research")
3. Create subtask: "Design Phase" (🟣 Purple, tags: "design")
4. Under "Design Phase", create:
   - "Homepage mockup" (tags: "design", "urgent")
   - "About page mockup" (tags: "design")
5. Create subtask: "Development Phase" (🔵 Blue, tags: "dev")
6. Start Pomodoro on "Homepage mockup"
7. Complete 2 Pomodoros
8. Mark "Homepage mockup" complete
9. Collapse "Design Phase"
10. Reorder subtasks if needed

**Expected:**
- Clear project hierarchy
- Tags help categorize
- Colors distinguish phases
- Time tracking works
- Collapse reduces clutter

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 3.2 Real-World Scenario: Daily Task Management 🔴

**Scenario:** Organize daily tasks by urgency and context

**Steps:**
1. Create tasks:
   - "Fix production bug" (🔴 Red, tags: "urgent", "bug")
   - "Review PRs" (🔵 Blue, tags: "work", "review")
   - "Gym workout" (🟢 Green, tags: "personal", "health")
   - "Read React docs" (🟡 Yellow, tags: "learning")
   - "Team meeting" (🟠 Orange, tags: "meeting", "work")
2. Start Pomodoro on "Fix production bug"
3. Complete 25-minute session
4. Break 5 minutes
5. Work on "Review PRs"
6. Use colors to identify task types at glance

**Expected:**
- Colors provide visual categorization
- Tags enable filtering (future)
- Pomodoro enforces focus
- Easy to scan priorities

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 3.3 Real-World Scenario: Learning Project 🟡

**Scenario:** Learn a new framework with structured subtasks

**Steps:**
1. Create "Learn Vue.js" (🟡 Yellow, tags: "learning", "vue")
2. Create subtasks:
   - "Complete tutorial" with sub-subtasks for each chapter
   - "Build sample app" with sub-subtasks for features
   - "Read best practices"
3. Use Pomodoro for each learning session
4. Track completed Pomodoros
5. Collapse completed sections

**Expected:**
- Clear learning path
- Progress tracking with Pomodoros
- Organized subtask structure
- Easy to resume where left off

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

## Phase 4: Performance Tests

### 4.1 Large Task List 🟡

**Test:** Create 500 tasks

**Steps:**
1. Create 100 parent tasks
2. Add 4 subtasks each = 500 total
3. Add 3 tags to each task
4. Set random colors
5. Navigate task list
6. Expand/collapse tasks
7. Search for tasks

**Expected:**
- UI remains responsive (<1s actions)
- No lag on expand/collapse
- Memory usage reasonable (<500MB)

**Actual:**
- [ ] Pass
- [ ] Fail (describe metrics)

---

### 4.2 Deep Hierarchy 🟡

**Test:** Create 10-level deep hierarchy

**Steps:**
1. Create task "L0"
2. Create subtask "L1" under "L0"
3. Continue to "L10"
4. Collapse "L0"
5. Expand "L0"
6. Navigate to "L10"

**Expected:**
- UI handles deep nesting
- Expand/collapse works
- No stack overflow errors

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 4.3 Tag Performance 🟡

**Test:** Create 1000 unique tags

**Steps:**
1. Create 1000 tasks with unique tags
2. Open tag editor
3. Type common prefix
4. Observe autocomplete performance

**Expected:**
- Autocomplete responds <500ms
- Popular tags load quickly
- No UI freezing

**Actual:**
- [ ] Pass
- [ ] Fail (describe metrics)

---

### 4.4 Long-Running Timer 🟡

**Test:** Run timer for 8 hours

**Steps:**
1. Start manual timer
2. Let run for 8 hours (workday)
3. Observe timer display
4. Stop timer
5. Check duration

**Expected:**
- Timer accurate (±1 minute)
- No overflow errors
- UI remains responsive
- Memory stable

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

## Phase 5: Regression Tests

### 5.1 Existing Task CRUD 🔴

**Test:** Ensure basic task operations still work

**Steps:**
1. Create task
2. Edit task
3. Mark complete
4. Delete task

**Expected:**
- All operations work as before
- No regressions

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 5.2 Workspace Navigation 🔴

**Test:** Ensure i3-style navigation works

**Steps:**
1. Press `Win+1` through `Win+9`
2. Press `Win+h/j/k/l`
3. Press `Win+Shift+h/j/k/l`

**Expected:**
- Workspace switching works
- Widget focusing works
- No interference from new features

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

### 5.3 Theme Switching 🟡

**Test:** Ensure theme changes work

**Steps:**
1. Switch theme
2. Verify new feature UIs update
3. Check TreeTaskListControl colors
4. Check TagEditorDialog colors
5. Check TimeTrackingWidget colors

**Expected:**
- All widgets respect theme
- Colors update correctly
- No visual glitches

**Actual:**
- [ ] Pass
- [ ] Fail (describe issue)

---

## Test Results Summary

### Critical Tests (Must Pass)
- [ ] TreeTaskListControl (5 tests)
- [ ] TagService (5 tests)
- [ ] TimeTrackingWidget (5 tests)
- [ ] Color Themes (3 tests)
- [ ] Integration (4 tests)
- [ ] UAT (2 scenarios)
- [ ] Regression (2 tests)

**Total Critical:** 26 tests

### High Priority Tests
- [ ] Advanced features (5 tests)
- [ ] Performance (4 tests)

**Total High:** 9 tests

### Overall Score
- Critical Passed: __ / 26 (must be 26/26)
- High Passed: __ / 9
- Total Passed: __ / 35

---

## Issue Reporting Template

### Issue Report Format
```markdown
## Issue #XXX: [Brief Description]

**Severity:** Critical / High / Medium / Low
**Test:** [Test ID, e.g., 1.1.1]
**Component:** TreeTaskListControl / TagService / TimeTrackingWidget / ColorThemes

### Steps to Reproduce
1.
2.
3.

### Expected Behavior


### Actual Behavior


### Screenshots
[Attach if applicable]

### Environment
- OS: Windows 10/11
- .NET Version:
- Build:

### Logs
[Paste relevant logs]
```

---

## Sign-Off Criteria

### Ready for Production
- ✅ All critical tests pass (26/26)
- ✅ 90%+ high priority tests pass (8+/9)
- ✅ No critical bugs
- ✅ Performance acceptable (<1s response)
- ✅ No regressions
- ✅ Documentation complete

### Sign-Off
- [ ] Tester: _________________ Date: _______
- [ ] Developer: ______________ Date: _______
- [ ] Product Owner: __________ Date: _______

---

**Testing Complete:** [ ] YES [ ] NO
**Production Ready:** [ ] YES [ ] NO
**Notes:**


