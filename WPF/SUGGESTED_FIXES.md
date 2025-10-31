# SuperTUI Focus & Input System - Suggested Fixes

## Priority 1: Critical Issues (P0)

### Fix 1: FocusHistoryManager IDisposable + Event Cleanup

**File:** `Core/Infrastructure/FocusHistoryManager.cs`

**Current Code:**
```csharp
public FocusHistoryManager(ILogger logger)
{
    this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // Hook into global focus changes
    EventManager.RegisterClassHandler(typeof(UIElement),
        UIElement.GotFocusEvent,
        new RoutedEventHandler(OnElementGotFocus));

    EventManager.RegisterClassHandler(typeof(UIElement),
        UIElement.LostFocusEvent,
        new RoutedEventHandler(OnElementLostFocus));
}
```

**Suggested Fix:**
```csharp
public class FocusHistoryManager : IDisposable
{
    private readonly ILogger logger;
    private readonly RoutedEventHandler onGotFocusHandler;
    private readonly RoutedEventHandler onLostFocusHandler;
    private bool disposed = false;

    public FocusHistoryManager(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Store handler references for unregistration
        onGotFocusHandler = new RoutedEventHandler(OnElementGotFocus);
        onLostFocusHandler = new RoutedEventHandler(OnElementLostFocus);

        // Hook into global focus changes
        EventManager.RegisterClassHandler(typeof(UIElement),
            UIElement.GotFocusEvent,
            onGotFocusHandler);

        EventManager.RegisterClassHandler(typeof(UIElement),
            UIElement.LostFocusEvent,
            onLostFocusHandler);
    }

    public void Dispose()
    {
        if (disposed) return;

        // Unregister event handlers
        EventManager.UnregisterClassHandler(typeof(UIElement),
            UIElement.GotFocusEvent,
            onGotFocusHandler);

        EventManager.UnregisterClassHandler(typeof(UIElement),
            UIElement.LostFocusEvent,
            onLostFocusHandler);

        disposed = true;
        GC.SuppressFinalize(this);
    }
}
```

---

### Fix 2: Null-Check Before Deferred Focus Restoration

**File:** `MainWindow.xaml.cs:614-623`

**Current Code:**
```csharp
private void MainWindow_Activated(object sender, EventArgs e)
{
    if (paneManager?.FocusedPane != null)
    {
        logger.Log(LogLevel.Debug, "MainWindow", 
            $"Window activated, restoring focus to {paneManager.FocusedPane.PaneName}");

        Dispatcher.BeginInvoke(new Action(() =>
        {
            paneManager.FocusPane(paneManager.FocusedPane);  // <-- DANGER!
        }), System.Windows.Threading.DispatcherPriority.Input);
    }
}
```

**Suggested Fix:**
```csharp
private void MainWindow_Activated(object sender, EventArgs e)
{
    var currentFocused = paneManager?.FocusedPane;
    if (currentFocused != null)
    {
        logger.Log(LogLevel.Debug, "MainWindow", 
            $"Window activated, restoring focus to {currentFocused.PaneName}");

        var paneToFocus = currentFocused;  // Capture now, not later
        
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // Re-verify pane still exists in manager
            if (paneManager?.OpenPanes.Contains(paneToFocus) == true)
            {
                paneManager.FocusPane(paneToFocus);
            }
            else
            {
                logger.Log(LogLevel.Warning, "MainWindow", 
                    "Target pane no longer exists, focusing first available");
                if (paneManager?.PaneCount > 0)
                {
                    paneManager.FocusPane(paneManager.OpenPanes[0]);
                }
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }
}
```

---

### Fix 3: Weak Reference Synchronization

**File:** `Core/Infrastructure/FocusHistoryManager.cs:86-98`

**Current Code:**
```csharp
public UIElement GetLastFocusedControl(string paneId)
{
    if (string.IsNullOrEmpty(paneId)) return null;

    if (paneFocusMap.TryGetValue(paneId, out var record))
    {
        if (record.Element.IsAlive)
        {
            return record.Element.Target as UIElement;
        }
    }
    return null;
}
```

**Suggested Fix:**
```csharp
public UIElement GetLastFocusedControl(string paneId)
{
    if (string.IsNullOrEmpty(paneId)) return null;

    if (paneFocusMap.TryGetValue(paneId, out var record))
    {
        try
        {
            // Check IsAlive and get Target atomically within try block
            if (record.Element.IsAlive)
            {
                var target = record.Element.Target as UIElement;
                if (target != null)
                {
                    return target;
                }
                else
                {
                    logger.Log(LogLevel.Debug, "FocusHistory",
                        $"Weak reference target was null for pane {paneId}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, "FocusHistory",
                $"Error accessing weak reference: {ex.Message}");
        }
    }
    return null;
}
```

---

### Fix 4: Focus Restoration During Workspace Switch

**File:** `MainWindow.xaml.cs:336-404`

**Current Code:**
```csharp
private void RestoreWorkspaceState()
{
    var state = workspaceManager.CurrentWorkspace;

    // Close all current panes
    paneManager.CloseAll();

    // ... restore panes ...

    // CRITICAL: Restore focus history after panes are loaded
    if (state.FocusState != null)
    {
        focusHistory.RestoreWorkspaceState(state.FocusState);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (paneManager.FocusedPane != null)
            {
                var paneName = paneManager.FocusedPane.PaneName;
                focusHistory.RestorePaneFocus(paneName);
                logger.Log(LogLevel.Debug, "MainWindow", 
                    $"Restored focus to {paneName} after workspace switch");
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
```

**Suggested Fix:**
```csharp
private void RestoreWorkspaceState()
{
    var state = workspaceManager.CurrentWorkspace;

    // Close all current panes
    paneManager.CloseAll();

    // Restore panes and wait for initialization
    var panesToRestore = new List<Core.Components.PaneBase>();
    foreach (var paneTypeName in state.OpenPaneTypes)
    {
        try
        {
            var pane = paneFactory.CreatePane(paneTypeName);
            panesToRestore.Add(pane);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, "MainWindow", 
                $"Failed to restore pane '{paneTypeName}': {ex.Message}");
        }
    }

    if (panesToRestore.Count > 0)
    {
        var paneState = new PaneManagerState
        {
            OpenPaneTypes = state.OpenPaneTypes,
            FocusedPaneIndex = state.FocusedPaneIndex
        };
        paneManager.RestoreState(paneState, panesToRestore);

        // CRITICAL: Restore focus AFTER pane initialization is complete
        if (state.FocusState != null)
        {
            focusHistory.RestoreWorkspaceState(state.FocusState);

            // Increase dispatcher priority to ensure this runs AFTER pane initialization
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var focusedPane = paneManager.FocusedPane;
                if (focusedPane != null)
                {
                    // Re-check that pane is still valid
                    if (paneManager.OpenPanes.Contains(focusedPane))
                    {
                        focusHistory.RestorePaneFocus(focusedPane.PaneName);
                        logger.Log(LogLevel.Debug, "MainWindow", 
                            $"Restored focus to {focusedPane.PaneName} after workspace switch");
                    }
                    else
                    {
                        logger.Log(LogLevel.Warning, "MainWindow", 
                            $"Target pane was removed before focus restoration");
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Input);  // Higher priority than Loaded
        }
    }
}
```

---

### Fix 5: Weak Reference Null Check in NavigateBack

**File:** `Core/Infrastructure/FocusHistoryManager.cs:135-155`

**Current Code:**
```csharp
public bool NavigateBack()
{
    if (focusHistory.Count <= 1) return false;

    focusHistory.Pop();

    if (focusHistory.TryPeek(out var previous))
    {
        if (previous.Element.IsAlive)
        {
            var element = previous.Element.Target as UIElement;
            element?.Focus();
            Keyboard.Focus(element);  // <-- DANGER: element could be null
            return true;
        }
    }

    return false;
}
```

**Suggested Fix:**
```csharp
public bool NavigateBack()
{
    if (focusHistory.Count <= 1) return false;

    focusHistory.Pop();

    if (focusHistory.TryPeek(out var previous))
    {
        try
        {
            if (previous.Element.IsAlive)
            {
                var element = previous.Element.Target as UIElement;
                if (element != null)
                {
                    element.Focus();
                    Keyboard.Focus(element);
                    return true;
                }
                else
                {
                    logger.Log(LogLevel.Debug, "FocusHistory",
                        "Weak reference target was null during navigate back");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, "FocusHistory",
                $"Error navigating back in focus history: {ex.Message}");
        }
    }

    return false;
}
```

---

## Priority 2: High-Risk Issues (P1)

### Fix 6: PaneManager State Synchronization

**File:** `Core/Infrastructure/PaneManager.cs`

Add subscription to pane focus changes:

```csharp
public void FocusPane(PaneBase pane)
{
    if (pane == null || !openPanes.Contains(pane))
        return;

    // Unfocus previous
    if (focusedPane != null && focusedPane != pane)
    {
        focusedPane.SetActive(false);
        focusedPane.IsFocused = false;
        focusedPane.OnFocusChanged();
        
        // NEW: Unsubscribe from previous pane's LostFocus
        focusedPane.LostFocus -= OnPaneLostFocus;
    }

    // Focus new
    focusedPane = pane;
    focusedPane.SetActive(true);
    focusedPane.IsFocused = true;
    focusedPane.OnFocusChanged();

    // NEW: Subscribe to detect if WPF steals focus (Tab key, etc.)
    focusedPane.LostFocus += OnPaneLostFocus;

    // ... rest of focus restoration code ...
}

// NEW: Handler to detect unexpected focus loss
private void OnPaneLostFocus(object sender, RoutedEventArgs e)
{
    if (focusedPane == sender && focusedPane != null)
    {
        logger.Log(LogLevel.Debug, "PaneManager",
            $"Focus lost from {focusedPane.PaneName} via WPF event");
        // Could reset focusedPane = null here, or update state
    }
}
```

---

### Fix 7: DispatcherTimer Exception Safety

**File:** `Panes/NotesPane.cs:86-105`

**Current Code:**
```csharp
searchDebounceTimer = new DispatcherTimer
{
    Interval = TimeSpan.FromMilliseconds(SEARCH_DEBOUNCE_MS)
};
searchDebounceTimer.Tick += (s, e) =>
{
    searchDebounceTimer.Stop();
    FilterNotes();
};
```

**Suggested Fix:**
```csharp
searchDebounceTimer = new DispatcherTimer
{
    Interval = TimeSpan.FromMilliseconds(SEARCH_DEBOUNCE_MS)
};
searchDebounceTimer.Tick += (s, e) =>
{
    try
    {
        searchDebounceTimer.Stop();
        FilterNotes();
    }
    catch (Exception ex)
    {
        ErrorHandlingPolicy.Handle(
            ErrorCategory.Internal,
            ex,
            "Search debounce timer filter notes",
            logger);
        // Ensure timer is stopped even after exception
        searchDebounceTimer.Stop();
    }
};
```

---

## Testing Strategy

1. **Unit Tests:** Add FocusHistoryManager.IDisposable test
2. **Integration Tests:** Rapid workspace switches with focus restoration
3. **Stress Tests:** Rapid pane closure during animations
4. **Manual Tests:** Use test checklist in EDGE_CASES_EXECUTIVE_SUMMARY.md

---

## Implementation Order

1. Fix FocusHistoryManager IDisposable (P0-1)
2. Fix null-checks in deferred focus (P0-2,4)
3. Fix weak reference races (P0-3,5)
4. Fix DispatcherTimer exception handling (P1-7)
5. Add state synchronization (P1-6)
6. Add logging to focus operations (P1-9)
7. Address remaining medium-risk issues

---

## Estimated Effort

- P0 fixes: 2-3 hours (5 fixes)
- P1 fixes: 4-6 hours (4 fixes)
- Testing: 2-4 hours
- **Total: 8-13 hours**

