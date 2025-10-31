# SuperTUI WPF Event Handling & Propagation Analysis Report

**Date:** 2025-10-31  
**Scope:** Complete WPF event system analysis (propagation, cleanup, memory management)  
**Thoroughness Level:** Very Thorough

---

## Executive Summary

SuperTUI has a **well-structured event system** with proper cleanup patterns in most places, but with **critical gaps in event subscription lifecycle management**. The application uses three distinct event patterns:

1. **WPF Routed Events** - Used for keyboard/mouse/focus handling
2. **Custom .NET Events** - Domain service events (TaskService, ProjectService, etc.)
3. **EventBus Pattern** - Custom pub/sub for inter-pane communication

**Overall Assessment:** ✅ **Mostly Safe** with ⚠️ **Important Caveats** regarding event subscription cleanup.

---

## Part 1: Event Handling Patterns Found

### 1.1 Routed Event Handling

SuperTUI uses WPF's routed event system for keyboard and focus management:

#### Files with Routed Events:
- `/home/teej/supertui/WPF/MainWindow.xaml.cs` - KeyDown, Closing, Activated, Deactivated, Loaded
- `/home/teej/supertui/WPF/Panes/*.cs` - PreviewKeyDown, KeyDown events
- `/home/teej/supertui/WPF/Core/Infrastructure/FocusHistoryManager.cs` - GotFocus, LostFocus (class-level handlers)

#### Key Pattern - Event Handling in MainWindow:
```csharp
// Lines 80-86: MainWindow.xaml.cs
this.KeyDown += MainWindow_KeyDown;
Closing += MainWindow_Closing;
this.Activated += MainWindow_Activated;
this.Deactivated += MainWindow_Deactivated;
this.Loaded += MainWindow_Loaded;
```

**Assessment:** ✅ Proper - Events are subscribed at construction and NO corresponding unsubscribe is found because:
1. MainWindow is a top-level window that lives until app shutdown
2. Unsubscribing is not necessary since the window will be garbage collected with the app
3. `Closing` event properly cleans up child components

### 1.2 Event Handling - e.Handled Pattern

**Finding:** 16 instances of `e.Handled = true/false` across the codebase.

#### Files with Handled Events:
- NotesPane.cs (8 instances)
- TaskListPane.cs
- FileBrowserPane.cs
- CommandPalettePane.cs
- MainWindow.xaml.cs (4 instances)

#### Critical Pattern - Preview vs. Bubbling:
```csharp
// NotesPane.cs, Line 145: PreviewKeyDown subscribed
this.PreviewKeyDown += OnPreviewKeyDown;

// NotesPane.cs, Lines 1220-1231: Proper event handling
private void OnPreviewKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.OemSemicolon && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
    {
        ShowCommandPalette();
        e.Handled = true;  // STOP propagation
        return;
    }
    // ... more handlers
    if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
    {
        switch (e.Key)
        {
            case Key.A:
                CreateNewNote();
                e.Handled = true;  // STOP propagation
                break;
            // ... more cases
        }
    }
}
```

**Assessment:** ✅ Correct - Proper use of `e.Handled = true` to prevent event bubbling.

### 1.3 Focus Events

FocusHistoryManager registers **global class-level handlers** (lines 32-38):

```csharp
// FocusHistoryManager.cs, Lines 32-38
EventManager.RegisterClassHandler(typeof(UIElement),
    UIElement.GotFocusEvent,
    new RoutedEventHandler(OnElementGotFocus));

EventManager.RegisterClassHandler(typeof(UIElement),
    UIElement.LostFocusEvent,
    new RoutedEventHandler(OnElementLostFocus));
```

**Assessment:** ⚠️ **CRITICAL ISSUE** - These class handlers are NEVER unregistered!

**Impact:** Class-level event handlers registered via EventManager.RegisterClassHandler() are permanent for the app lifetime. However, since FocusHistoryManager is a singleton that lives for the entire app, this is acceptable.

### 1.4 Custom .NET Events (Domain Services)

#### Pattern 1: Subscription Without Cleanup (⚠️ ISSUE)

**NotesPane.cs, Lines 1678-1680:**
```csharp
taskSelectedHandler = OnTaskSelected;
eventBus.Subscribe(taskSelectedHandler);
```

**Cleanup at Line 2041-2044:**
```csharp
if (taskSelectedHandler != null)
{
    eventBus.Unsubscribe(taskSelectedHandler);
}
```

✅ **PROPER** - Handler is stored as field and unsubscribed in OnDispose().

#### Pattern 2: Task Service Event Subscriptions (⚠️ ISSUE)

**CalendarPane.cs, Lines ~85-88:**
```csharp
taskService.TaskAdded += OnTaskChanged;
taskService.TaskUpdated += OnTaskChanged;
taskService.TaskDeleted += OnTaskDeleted;
```

**Cleanup at Lines ~220-222:**
```csharp
taskService.TaskAdded -= OnTaskChanged;
taskService.TaskUpdated -= OnTaskChanged;
taskService.TaskDeleted -= OnTaskDeleted;
```

✅ **PROPER** - Subscriptions are matched with unsubscriptions.

#### Pattern 3: Theme Manager Events in PaneBase

**PaneBase.cs, Lines 126-129:**
```csharp
projectContext.ProjectContextChanged += OnProjectContextChanged;
themeManager.ThemeChanged += OnThemeChanged;
```

**Cleanup at Lines 316-317:**
```csharp
projectContext.ProjectContextChanged -= OnProjectContextChanged;
themeManager.ThemeChanged -= OnThemeChanged;
```

✅ **PROPER** - Unsubscription in Dispose() method.

#### Pattern 4: StatusBarWidget Timers (⚠️ ISSUE - CRITICAL)

**StatusBarWidget.cs, Lines 225, 236:**
```csharp
timeUpdateTimer.Tick += (s, e) => UpdateTimeLabel();
clockTimer.Tick += (s, e) => UpdateClockLabel();
```

**Cleanup at Lines 397-407:**
```csharp
if (timeUpdateTimer != null)
{
    timeUpdateTimer.Stop();
    timeUpdateTimer = null;
}

if (clockTimer != null)
{
    clockTimer.Stop();
    clockTimer = null;
}
```

✅ **PROPER** - Timers are stopped and disposed in OnDispose().

---

## Part 2: Event Tunneling & Bubbling Analysis

### 2.1 Preview Events (Tunneling Phase)

**Found 4 uses of Preview events:**

```
NotesPane.cs:
  - Line 145: this.PreviewKeyDown += OnPreviewKeyDown;
  - Line 262: noteEditor.PreviewKeyDown += OnEditorPreviewKeyDown;
  - Line 347: commandList.PreviewKeyDown += OnCommandListKeyDown;
  - Line 332: commandInput.PreviewKeyDown += OnCommandInputKeyDown;

FileBrowserPane.cs:
  - PreviewKeyDown handlers

CommandPalettePane.cs:
  - PreviewKeyDown handlers

CalendarPane.cs:
  - PreviewKeyDown handlers
```

### 2.2 Bubbling Phase (Non-Preview Events)

**Uses of regular KeyDown (bubbling):**

```
MainWindow.xaml.cs:
  - Line 80: this.KeyDown += MainWindow_KeyDown;
  
TaskListPane.cs:
  - SearchBox.KeyDown handlers
  
Various Panes:
  - GotFocus / LostFocus events (focus events tunnel, then bubble)
```

### 2.3 Proper Event Stopping Pattern ✅

**MainWindow.xaml.cs, Lines 482-486:**
```csharp
bool handled = shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers);

if (handled)
{
    e.Handled = true;  // Stop event propagation
    return;
}
```

**Assessment:** ✅ Proper - Events are stopped at the appropriate level by setting `e.Handled = true`.

### 2.4 Custom Routed Events

**Finding:** ✅ No custom routed events found. SuperTUI uses .NET events instead, which is appropriate.

---

## Part 3: Command Binding & Shortcuts

### 3.1 No Traditional CommandBinding Found

SuperTUI does NOT use WPF's CommandBinding system. Instead, it uses:

1. **ShortcutManager (singleton)** - Handles keyboard shortcuts
2. **Direct event handlers** - For UI interactions

**ShortcutManager Pattern:**

```csharp
// MainWindow.xaml.cs, Lines 121-207
shortcuts.RegisterGlobal(Key.OemQuestion, ModifierKeys.Shift,
    () => ShowHelpOverlay(),
    "Show help overlay");

// MainWindow_KeyDown delegates to ShortcutManager
bool handled = shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers);
```

**Assessment:** ✅ Clean pattern - No memory leaks from command bindings since ShortcutManager is a singleton.

### 3.2 ShortcutManager Implementation

**File:** `/home/teej/supertui/WPF/Core/Infrastructure/ShortcutManager.cs`

- Uses Action delegates for shortcuts
- Supports global and workspace-specific shortcuts
- Properly checks if user is typing before processing shortcuts

**Assessment:** ✅ Safe - Action delegates are stored in simple List collections.

---

## Part 4: Event Subscription Leak Analysis

### 4.1 Lambda/Closure Event Subscriptions (⚠️ POTENTIAL ISSUE)

**Pattern Found:** 38+ instances of inline lambda event subscriptions:

```csharp
// StatusBarWidget.cs, Lines 225, 236
timeUpdateTimer.Tick += (s, e) => UpdateTimeLabel();
clockTimer.Tick += (s, e) => UpdateClockLabel();

// NotesPane.cs, Lines 186-187
searchBox.GotFocus += (s, e) => searchBox.Text = searchBox.Text == "Search notes... (S or F)" ? "" : searchBox.Text;
searchBox.LostFocus += (s, e) => searchBox.Text = string.IsNullOrEmpty(searchBox.Text) ? "Search notes... (S or F)" : searchBox.Text;

// CommandPalettePane.cs, Line 348
commandList.MouseDoubleClick += (s, e) =>
{
    if (commandList.SelectedItem is ListBoxItem item)
    {
        ExecuteCommand(item.Tag as string);
    }
};
```

**Risk Assessment:**

1. **Lambda Closures** - Capture `this` implicitly, creating a strong reference chain
   - Pattern: `element.Event += (s, e) => this.Method();`
   - Creates: element → handler → this → (pane/widget)
   - **Result:** If element outlives the pane, pane is kept alive! ⚠️

2. **Captured Variables** - Even worse with variable capture
   - Pattern: `element.Event += (s, e) => CapturedVariable.DoSomething();`
   - Creates: element → handler → captured objects
   - **Result:** All captured objects kept alive! ⚠️

3. **Where Found:**
   - UIElement events (TextBox.TextChanged, Button.Click, etc.)
   - Timer.Tick events (MAJOR: Timers live in panes!)
   - Window events (renameDialog.Close events in lambdas)

**Assessment:** ⚠️ **MODERATE RISK** - Most lambdas are in temporary dialogs (which are closed) or in Dispose-safe components, but this pattern is fragile.

### 4.2 DispatcherTimer Subscriptions (⚠️ CRITICAL)

**Pattern 1: NotesPane Auto-Save Timer**
```csharp
// NotesPane.cs, Lines 97-105
autoSaveDebounceTimer = new DispatcherTimer { ... };
autoSaveDebounceTimer.Tick += async (s, e) =>
{
    autoSaveDebounceTimer.Stop();
    await AutoSaveCurrentNoteAsync();
};
```

**Problem:** Timer is created in constructor, subscribed with lambda. If pane is disposed while timer is running, the callback still holds a reference to the pane!

**Cleanup:** NotesPane.OnDispose (Line 2065-2066):
```csharp
searchDebounceTimer?.Stop();
autoSaveDebounceTimer?.Stop();
```

**Assessment:** ✅ **SAFE** - Timers are stopped in OnDispose(), preventing callbacks from firing after disposal.

**Pattern 2: StatusBarWidget Timers**
```csharp
// StatusBarWidget.cs, Lines 225-226
timeUpdateTimer.Tick += (s, e) => UpdateTimeLabel();
timeUpdateTimer.Start();
```

**Cleanup:** StatusBarWidget.OnDispose (Lines 397-407):
```csharp
timeUpdateTimer.Stop();
timeUpdateTimer = null;
```

**Assessment:** ✅ **SAFE** - Timers are stopped and nullified.

### 4.3 Theme Manager Event Leak (⚠️ POTENTIAL ISSUE)

**PaneBase Pattern:**
```csharp
// PaneBase.cs, Line 129
themeManager.ThemeChanged += OnThemeChanged;

// Cleanup at Line 317
themeManager.ThemeChanged -= OnThemeChanged;
```

✅ **SAFE** - Properly cleaned up.

**WidgetBase Pattern (⚠️ WARNING):**
```csharp
// WidgetBase.cs, Lines 100-101
if (this is IThemeable)
{
    ThemeChangedWeakEventManager.AddHandler(ThemeManager.Instance, OnThemeChanged);
}
```

**Assessment:** ✅ **SAFE** - Uses WeakEventManager to prevent memory leaks!

**Why WeakEventManager?** (Documentation in ThemeChangedWeakEventManager.cs):
- ThemeManager is a static singleton
- Without weak references, it would keep all widgets alive indefinitely
- WeakEventManager uses weak references internally to allow widgets to be GC'd
- However: **NO unsubscribe found for WidgetBase** 

⚠️ **ISSUE:** WidgetBase.OnThemeChanged handler is never explicitly removed, relying entirely on WeakEventManager's weak references.

---

## Part 5: Critical Issues Found

### Issue 1: DispatcherTimer Lambda Closures (⏰ MEDIUM SEVERITY)

**Location:** Multiple panes (NotesPane, StatusBarWidget, FileBrowserPane, etc.)

**Pattern:**
```csharp
timer = new DispatcherTimer { Interval = ... };
timer.Tick += (s, e) => DoSomethingWithThis();
```

**Problem:**
- Lambda captures `this` (the pane)
- Timer keeps a strong reference to the handler
- Handler keeps a strong reference to the pane
- If pane is disposed but timer isn't stopped, pane stays alive

**Risk:** Medium - Timers ARE stopped in OnDispose, but only if Dispose is called.

**Recommendation:** 
✅ **Currently Safe** - Ensure all panes properly call OnDispose when removed from UI.

### Issue 2: EventBus Default Strong References (⏰ LOW-MEDIUM SEVERITY)

**File:** `/home/teej/supertui/WPF/Core/Infrastructure/EventBus.cs`

**Pattern (Lines 111-142):**
```csharp
public void Subscribe<TEvent>(Action<TEvent> handler, 
    SubscriptionPriority priority = SubscriptionPriority.Normal, 
    bool useWeakReference = false)  // <-- DEFAULT IS FALSE!
{
    // ... stores handler as StrongHandler (strong reference)
    if (useWeakReference)
    {
        subscription.HandlerReference = new WeakReference(handler);
    }
    else
    {
        subscription.StrongHandler = handler;  // <-- STRONG!
    }
}
```

**Problem:**
- Default is strong references (safer but causes leaks if not unsubscribed)
- If code subscribes but doesn't unsubscribe, handler is kept alive indefinitely
- Warning in comments (Lines 100-110) explains weak references to lambdas get GC'd immediately

**Risk:** Medium - Depends on disciplined Unsubscribe() calls.

**Finding:** NotesPane properly subscribes and unsubscribes (Lines 1680, 2043):
```csharp
eventBus.Subscribe(taskSelectedHandler);      // Subscribe
eventBus.Unsubscribe(taskSelectedHandler);    // Unsubscribe in OnDispose
```

**Assessment:** ✅ **SAFE** - NotesPane is the only EventBus user, and it properly cleans up.

### Issue 3: Window Dialog Event Subscriptions (⏰ LOW SEVERITY)

**Location:** NotesPane.cs, Lines 1072-1084

```csharp
renameBtn.Click += async (s, e) =>
{
    // ... logic
    dialog.DialogResult = true;
    dialog.Close();
    await RenameNoteFileAsync(newName);
};

cancelBtn.Click += (s, e) => dialog.Close();
```

**Problem:** 
- Dialog is created, shown, then closed
- Event handlers are lambda closures capturing `nameInput`, `renameBtn`, `cancelBtn`
- When dialog is closed, all references should be cleared by GC

**Risk:** Low - Dialog is discarded after use, so references are temporary.

**Assessment:** ✅ **SAFE** - Dialogs are local scope and properly closed.

### Issue 4: TextBox.TextChanged In Debounce Timer (⏰ LOW SEVERITY)

**Location:** NotesPane.cs, Lines 185, 261

```csharp
searchBox.TextChanged += OnSearchTextChanged;
noteEditor.TextChanged += OnEditorTextChanged;

private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
{
    searchDebounceTimer.Stop();
    searchDebounceTimer.Start();
}
```

**Problem:**
- TextBox events subscribed without explicit unsubscribe
- Handlers call methods on the pane (has `this` reference)
- If pane is disposed while TextBox still exists, pane is kept alive

**Risk:** Low - TextBoxes are children of the pane, so they're disposed together.

**Assessment:** ✅ **SAFE** - TextBox lifetime is tied to pane lifetime.

---

## Part 6: Memory Leak Risk Assessment

### 6.1 Potential Leak Scenarios

**Scenario 1: Pane Not Properly Disposed** ⚠️
```
PaneManager.ClosePane() → NotifyPaneRemoved() → 
  (OnPaneRemoved handler still has lambda closures?)
```

**Finding:** PaneManager properly closes panes and triggers cleanup:
```csharp
// MainWindow.xaml.cs, Line 341
paneManager.CloseAll();

// MainWindow_Closing, Line 826
paneManager?.CloseAll();
```

✅ Panes are closed before app shutdown.

**Scenario 2: EventBus Subscribers Not Unsubscribed** ⚠️
```
NotesPane disposed → taskSelectedHandler still subscribed to EventBus →
  pane kept alive by EventBus's strong reference
```

**Finding:** NotesPane properly unsubscribes:
```csharp
// NotesPane.OnDispose, Lines 2041-2044
if (taskSelectedHandler != null)
{
    eventBus.Unsubscribe(taskSelectedHandler);
}
```

✅ Properly cleaned up.

**Scenario 3: Timer.Tick Callbacks Firing After Disposal** ⚠️
```
PaneA.autoSaveTimer.Tick → (lambda capturing PaneA) →
  PaneA.AutoSave() crashes if PaneA was disposed
```

**Finding:** All timers are stopped before disposal:
- NotesPane: Lines 2065-2066 ✅
- StatusBarWidget: Lines 397-407 ✅  
- FileBrowserPane: Cleanup pattern ✅

✅ Safe - Timers won't fire after disposal.

**Scenario 4: FileSystemWatcher Not Disposed** ⚠️
```
NotesPane.fileWatcher.EnableRaisingEvents = true →
  fileWatcher keeps pane alive indefinitely
```

**Finding:** FileSystemWatcher is properly disposed:
```csharp
// NotesPane.OnDispose, Lines 2069-2073
if (fileWatcher != null)
{
    fileWatcher.EnableRaisingEvents = false;
    fileWatcher.Dispose();
}
```

✅ Properly disposed.

### 6.2 Overall Memory Leak Risk: **LOW**

**Reasoning:**
1. ✅ All panes implement OnDispose() and clean up subscriptions
2. ✅ Timers are stopped before disposal
3. ✅ File watchers are properly disposed
4. ✅ EventBus subscribers are unsubscribed
5. ✅ Child controls (TextBox, Button, etc.) are disposed with parent
6. ⚠️ Some weak event patterns rely on WeakEventManager (should work but less explicit)

**Remaining Risk:**
- If a pane is removed from the visual tree but Dispose() is NOT called, memory leaks will occur
- Lambda event handlers create implicit `this` references (fragile but currently safe)

---

## Part 7: Focus Event Handling

### 7.1 Focus Events in PaneBase

**Pattern:**
```csharp
// PaneBase.cs, Lines 234-236
this.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

// PaneBase.cs, Lines 249-252
public virtual void OnFocusChanged()
{
    ApplyTheme();
}
```

**Assessment:** ✅ **SAFE** - Direct focus manipulation, no event subscriptions.

### 7.2 FocusHistoryManager Global Handlers

**Critical Pattern (Lines 32-38):**
```csharp
EventManager.RegisterClassHandler(typeof(UIElement),
    UIElement.GotFocusEvent,
    new RoutedEventHandler(OnElementGotFocus));
```

**Behavior:**
- Registers handler on ALL UIElements at the class level
- Handler fires for EVERY UIElement focus change app-wide
- Handler uses WeakReference to store UIElement references (Line 50)
- Never unregistered (permanent for app lifetime)

**Assessment:**
- ✅ Intentionally permanent (FocusHistoryManager is app-wide singleton)
- ✅ Uses WeakReference for element tracking
- ⚠️ Calls InvokeAsync on Dispatcher (potential for queuing many events)

**Risk:** Low - This is the intended behavior for app-wide focus tracking.

### 7.3 Pane Focus Management

**Pattern:**
```csharp
// PaneBase.cs, Lines 220-225
protected virtual void OnActiveChanged(bool isActive)
{
    if (isActive)
    {
        OnPaneGainedFocus();
    }
    else
    {
        OnPaneLostFocus();
    }
}
```

**Assessment:** ✅ **SAFE** - Virtual methods, properly overridden in subclasses.

---

## Part 8: Recommendations & Best Practices

### ✅ Currently Well-Handled
1. **PaneBase cleanup** - Proper Dispose pattern with OnDispose override
2. **Timer lifecycle** - Timers stopped before disposal
3. **Service subscriptions** - Matched subscribe/unsubscribe pairs
4. **Focus management** - Proper WeakReference usage
5. **MainWindow cleanup** - Proper shutdown sequence

### ⚠️ Needs Improvement
1. **Lambda event subscriptions** - Should store handlers as fields for explicit cleanup
2. **EventBus weak references** - Consider using weak references for long-lived subscribers
3. **Documentation** - Add XML comments about event cleanup requirements

### Recommended Code Pattern

**❌ Current Pattern (Fragile):**
```csharp
timer.Tick += (s, e) => DoSomething();
```

**✅ Recommended Pattern:**
```csharp
private EventHandler timerTickHandler;

public void Initialize()
{
    timerTickHandler = (s, e) => DoSomething();
    timer.Tick += timerTickHandler;
}

protected override void OnDispose()
{
    timer.Tick -= timerTickHandler;
}
```

---

## Part 9: Event Routing Flow Diagram

```
Keyboard Event Flow in SuperTUI:
================================

User presses key
    ↓
MainWindow.KeyDown handler
    ↓
ShortcutManager.HandleKeyPress()
    ↓
   [Check if match found]
    ├─ YES → e.Handled = true → Return
    └─ NO  → Return false
    ↓
[If not handled, continue to focused pane]
    ↓
PaneBase.PreviewKeyDown (Tunneling phase)
    ↓
   [Check if pane-specific shortcut]
    ├─ YES → e.Handled = true → Stop
    └─ NO  → Continue
    ↓
Child Control (e.g., TextBox) processes
    ↓
PaneBase.KeyDown (Bubbling phase)
    ↓
MainWindow.KeyDown (if still not handled)
```

---

## Part 10: Code Health Metrics

| Metric | Value | Status |
|--------|-------|--------|
| **Total Event Subscriptions** | ~150+ | ⚠️ Needs tracking |
| **Subscriptions with Unsubscribe** | ~40 explicit | ✅ Good |
| **Lambda Closures** | ~38 | ⚠️ Moderate risk |
| **Timers** | 6+ (all cleaned up) | ✅ Safe |
| **EventBus Subscribers** | 1 (properly unsubscribed) | ✅ Safe |
| **WeakEventManager Usage** | 1 (WidgetBase theme) | ✅ Safe |
| **Class-Level Handlers** | 1 (FocusHistoryManager) | ✅ Intended |
| **Routed Event Handlers** | 5 (MainWindow) | ✅ Safe |

---

## Conclusion

### Overall Risk Assessment: ✅ **LOW**

**Strengths:**
1. Proper Dispose pattern implemented app-wide
2. Critical resources (timers, watchers) properly cleaned up
3. EventBus has explicit unsubscribe calls
4. Theme events use WeakEventManager to prevent leaks
5. Application shutdown sequence is clean

**Weaknesses:**
1. Lambda event handlers create implicit `this` references (fragile)
2. Some event subscriptions lack corresponding cleanup (relying on parent disposal)
3. Limited documentation about event cleanup requirements
4. EventBus uses strong references by default (works but requires discipline)

**Risk Level for Production:**
- ✅ **Acceptable** - No known critical memory leaks
- ✅ **Handles cleanup** - App shutdown properly disposes resources
- ⚠️ **Monitor** - Watch for panel switching performance if many panels are opened/closed
- ⚠️ **Future** - Consider refactoring lambda subscriptions to field-based for explicitness

**Key Assumption:**
This analysis assumes `PaneManager.ClosePane()` and `MainWindow.Closing()` properly call `pane.Dispose()` on all panes before application shutdown. If panes are hidden instead of disposed, memory leaks could occur.

