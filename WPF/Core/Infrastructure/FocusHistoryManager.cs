using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Manages focus history across the application for perfect focus restoration
    /// Tracks what control had focus in each pane and maintains a history stack
    /// UNIFIED FOCUS: Uses WPF's native focus events as single source of truth
    /// </summary>
    public class FocusHistoryManager : IDisposable
    {
        private readonly ILogger logger;
        private readonly Stack<FocusRecord> focusHistory = new Stack<FocusRecord>();
        private readonly Dictionary<string, FocusRecord> paneFocusMap = new Dictionary<string, FocusRecord>();
        private readonly Dictionary<string, List<WeakReference>> trackedPanes = new Dictionary<string, List<WeakReference>>();
        private readonly int maxHistorySize = 50;
        private bool isTrackingEnabled = true;
        private bool isDisposed = false;
        private FocusRecord currentFocus;

        public event EventHandler<FocusChangedEventArgs> FocusChanged;

        public FocusHistoryManager(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // UNIFIED FOCUS: Hook into WPF's native global focus events (single source of truth)
            // This ensures we observe actual keyboard focus changes, not custom tracking
            EventManager.RegisterClassHandler(typeof(UIElement),
                UIElement.GotFocusEvent,
                new RoutedEventHandler(OnElementGotFocus));

            EventManager.RegisterClassHandler(typeof(UIElement),
                UIElement.LostFocusEvent,
                new RoutedEventHandler(OnElementLostFocus));
        }

        /// <summary>
        /// Record that a control has gained focus
        /// </summary>
        public void RecordFocus(UIElement element, string paneId = null)
        {
            if (!isTrackingEnabled || element == null) return;

            var record = new FocusRecord
            {
                Element = new WeakReference(element),
                ElementType = element.GetType().Name,
                PaneId = paneId ?? GetPaneId(element),
                Timestamp = DateTime.Now,
                ControlState = CaptureControlState(element)
            };

            // Update current focus
            currentFocus = record;

            // Add to history
            if (focusHistory.Count >= maxHistorySize)
            {
                // Remove oldest entries
                var temp = focusHistory.ToArray().Take(maxHistorySize - 1).Reverse();
                focusHistory.Clear();
                foreach (var item in temp)
                    focusHistory.Push(item);
            }
            focusHistory.Push(record);

            // Update pane-specific focus map
            if (!string.IsNullOrEmpty(record.PaneId))
            {
                paneFocusMap[record.PaneId] = record;
            }

            logger.Log(LogLevel.Debug, "FocusHistory",
                $"Focus recorded: {record.ElementType} in {record.PaneId}");

            FocusChanged?.Invoke(this, new FocusChangedEventArgs(record.ElementType, record.PaneId, record.Timestamp));
        }

        /// <summary>
        /// Register a pane for tracking. This allows the manager to clean up when the pane is disposed.
        /// </summary>
        public void TrackPane(Components.PaneBase pane)
        {
            if (pane == null) throw new ArgumentNullException(nameof(pane));
            if (isDisposed) return;

            string paneId = pane.PaneName;
            if (string.IsNullOrEmpty(paneId))
            {
                logger.Log(LogLevel.Warning, "FocusHistory",
                    "Cannot track pane with empty name");
                return;
            }

            // Store weak reference to avoid keeping pane alive
            if (!trackedPanes.ContainsKey(paneId))
            {
                trackedPanes[paneId] = new List<WeakReference>();
            }

            // Check if already tracking this specific instance
            bool alreadyTracked = false;
            trackedPanes[paneId].RemoveAll(wr => !wr.IsAlive); // Clean up dead references
            foreach (var weakRef in trackedPanes[paneId])
            {
                if (weakRef.IsAlive && ReferenceEquals(weakRef.Target, pane))
                {
                    alreadyTracked = true;
                    break;
                }
            }

            if (!alreadyTracked)
            {
                trackedPanes[paneId].Add(new WeakReference(pane));
                logger.Log(LogLevel.Debug, "FocusHistory",
                    $"Now tracking pane: {paneId} (total instances: {trackedPanes[paneId].Count})");
            }
        }

        /// <summary>
        /// Unregister a pane from tracking and clean up its focus history.
        /// This should be called when a pane is disposed to prevent memory leaks.
        /// </summary>
        public void UntrackPane(Components.PaneBase pane)
        {
            if (pane == null) return;

            string paneId = pane.PaneName;
            UntrackPane(paneId);
        }

        /// <summary>
        /// Unregister a pane by ID from tracking and clean up its focus history.
        /// This should be called when a pane is disposed to prevent memory leaks.
        /// </summary>
        public void UntrackPane(string paneId)
        {
            if (string.IsNullOrEmpty(paneId)) return;
            if (isDisposed) return;

            // Remove from tracked panes
            if (trackedPanes.ContainsKey(paneId))
            {
                trackedPanes[paneId].RemoveAll(wr => !wr.IsAlive); // Clean up dead references
                if (trackedPanes[paneId].Count == 0)
                {
                    trackedPanes.Remove(paneId);
                }
            }

            // Check if any other instances of this pane ID are still alive
            bool hasLiveInstances = false;
            if (trackedPanes.TryGetValue(paneId, out var instances))
            {
                hasLiveInstances = instances.Any(wr => wr.IsAlive);
            }

            // Only clear history if no live instances remain
            if (!hasLiveInstances)
            {
                ClearPaneHistory(paneId);
                logger.Log(LogLevel.Debug, "FocusHistory",
                    $"Untracked and cleared history for pane: {paneId}");
            }
            else
            {
                logger.Log(LogLevel.Debug, "FocusHistory",
                    $"Untracked pane instance: {paneId} (other instances still alive)");
            }
        }

        /// <summary>
        /// Get the last focused control for a specific pane
        /// </summary>
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

        /// <summary>
        /// Apply focus to a pane using the fallback chain
        /// Attempts to focus the pane element itself, falling back through a chain
        /// to ensure focus is never lost
        /// Public API for PaneManager to use when setting focus
        /// </summary>
        /// <param name="pane">The pane to focus</param>
        /// <returns>True if focus was applied successfully (or via fallback)</returns>
        public bool ApplyFocusToPane(Components.PaneBase pane)
        {
            FocusDebugger.LogFocusOperation("FocusHistoryManager.ApplyFocusToPane", pane, pane?.PaneName);

            if (pane == null)
            {
                FocusDebugger.LogFocusResult("FocusHistoryManager.ApplyFocusToPane.Null", false, null, 0, "pane is null");
                return false;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool result = ApplyFocusWithFallback(pane, pane.PaneName, "ApplyFocusToPane");

            FocusDebugger.LogFocusResult("FocusHistoryManager.ApplyFocusToPane", result, pane, sw.ElapsedMilliseconds,
                result ? null : "All fallback attempts failed");

            return result;
        }

        /// <summary>
        /// Attempts to apply focus with a fallback chain:
        /// 1. Try the requested element (if available and valid)
        /// 2. Try the first focusable child of the pane
        /// 3. Try the pane itself (if focusable)
        /// 4. Try the main window as last resort
        /// This ensures focus never gets "lost" - there's always something focused
        /// </summary>
        private bool ApplyFocusWithFallback(UIElement requestedElement, string paneId, string source)
        {
            FocusDebugger.LogFocusOperation("FocusHistoryManager.ApplyFocusWithFallback", requestedElement, paneId,
                $"Source: {source}");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // REVERSED ORDER: Try children BEFORE container
            // Attempt 1: Find the pane and try its first focusable child FIRST
            if (!string.IsNullOrEmpty(paneId))
            {
                var paneElement = FindPaneById(paneId);

                FocusDebugger.LogFocusOperation("FocusHistoryManager.Fallback.Attempt1", paneElement, paneId,
                    $"Try first focusable child (NEW ORDER), pane found: {paneElement != null}");

                if (paneElement != null)
                {
                    var firstFocusable = FindFirstFocusableChild(paneElement);

                    FocusDebugger.LogFocusOperation("FocusHistoryManager.Fallback.Attempt1.Child", firstFocusable, paneId,
                        $"First focusable child: {firstFocusable?.GetType().Name ?? "null"}");

                    if (firstFocusable != null && TryFocusElement(firstFocusable))
                    {
                        logger.Log(LogLevel.Debug, "FocusHistory",
                            $"[{source}] Focus applied to first child of {paneId}: {firstFocusable.GetType().Name}");

                        FocusDebugger.LogFocusResult("FocusHistoryManager.Fallback.Attempt1", true, firstFocusable, sw.ElapsedMilliseconds,
                            $"First focusable child accepted focus: {firstFocusable.GetType().Name}");
                        return true;
                    }
                }
            }

            // Attempt 2: Try the requested element (usually the pane container) as fallback
            if (requestedElement != null)
            {
                FocusDebugger.LogFocusOperation("FocusHistoryManager.Fallback.Attempt2", requestedElement, paneId,
                    "Try pane container (fallback)");

                if (TryFocusElement(requestedElement))
                {
                    logger.Log(LogLevel.Debug, "FocusHistory",
                        $"[{source}] Focus fallback: Focused pane container: {requestedElement.GetType().Name}");

                    FocusDebugger.LogFocusResult("FocusHistoryManager.Fallback.Attempt2", true, requestedElement, sw.ElapsedMilliseconds,
                        "Pane container accepted focus (fallback)");
                    return true;
                }
                else
                {
                    FocusDebugger.LogFocusResult("FocusHistoryManager.Fallback.Attempt2", false, requestedElement, sw.ElapsedMilliseconds,
                        "Pane container rejected focus");
                }
            }

            // Attempt 3: Try the main window as last resort
            var mainWindow = Application.Current?.MainWindow;

            FocusDebugger.LogFocusOperation("FocusHistoryManager.Fallback.Attempt3.LastResort", mainWindow, paneId,
                $"Try MainWindow, found: {mainWindow != null}");

            if (mainWindow != null && TryFocusElement(mainWindow))
            {
                logger.Log(LogLevel.Warning, "FocusHistory",
                    $"[{source}] Focus fallback: Focused MainWindow (all other attempts failed)");

                FocusDebugger.LogFocusResult("FocusHistoryManager.Fallback.Attempt3.LastResort", true, mainWindow, sw.ElapsedMilliseconds,
                    "MainWindow accepted focus as last resort");
                return true;
            }

            logger.Log(LogLevel.Warning, "FocusHistory",
                $"[{source}] Focus fallback exhausted: Could not focus any element for {paneId}");

            FocusDebugger.LogFocusResult("FocusHistoryManager.ApplyFocusWithFallback.Failed", false, null, sw.ElapsedMilliseconds,
                "ALL fallback attempts exhausted - no element accepted focus");

            return false;
        }

        /// <summary>
        /// Safely attempt to focus an element with validation
        /// </summary>
        private bool TryFocusElement(UIElement element)
        {
            if (element == null) return false;

            try
            {
                // Check if it's a FrameworkElement and verify it's loaded
                var frameworkElement = element as FrameworkElement;
                if (frameworkElement != null && !frameworkElement.IsLoaded)
                {
                    return false;
                }

                // Attempt to focus
                element.Focus();
                Keyboard.Focus(element);
                return true;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Debug, "FocusHistory",
                    $"Failed to focus element {element.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find the first focusable child in the visual tree
        /// IMPORTANT: Skips the parent itself and only searches its children
        /// </summary>
        private UIElement FindFirstFocusableChild(UIElement parent)
        {
            if (parent == null) return null;

            var queue = new Queue<UIElement>();

            // Start with parent's children, NOT the parent itself
            // This prevents returning the pane container when we want child controls
            int initialChildCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < initialChildCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i) as UIElement;
                if (child != null)
                {
                    queue.Enqueue(child);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // Skip GridSplitters - they should never receive keyboard focus
                if (current is System.Windows.Controls.GridSplitter)
                {
                    continue;
                }

                // Check if this element can be focused
                if (current.Focusable)
                {
                    // Check if it's loaded (FrameworkElement)
                    var frameworkElement = current as FrameworkElement;
                    if (frameworkElement == null || frameworkElement.IsLoaded)
                    {
                        return current;
                    }
                }

                // Add children to queue for breadth-first search
                int childCount = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(current, i) as UIElement;
                    if (child != null)
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find a pane in the visual tree by its name
        /// </summary>
        private Components.PaneBase FindPaneById(string paneId)
        {
            if (string.IsNullOrEmpty(paneId)) return null;

            // Search through tracked panes first (most reliable)
            if (trackedPanes.TryGetValue(paneId, out var instances))
            {
                foreach (var weakRef in instances)
                {
                    if (weakRef.IsAlive)
                    {
                        var pane = weakRef.Target as Components.PaneBase;
                        if (pane != null)
                        {
                            return pane;
                        }
                    }
                }
            }

            // Fallback: search visual tree from main window
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                return FindPaneInVisualTree(mainWindow, paneId);
            }

            return null;
        }

        /// <summary>
        /// Recursively search visual tree for a pane by ID
        /// </summary>
        private Components.PaneBase FindPaneInVisualTree(DependencyObject parent, string paneId)
        {
            if (parent == null) return null;

            var pane = parent as Components.PaneBase;
            if (pane != null && pane.PaneName == paneId)
            {
                return pane;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindPaneInVisualTree(child, paneId);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Restore focus to the last focused control in a pane
        /// Uses ApplyFocusWithFallback to ensure focus never gets lost
        /// </summary>
        public bool RestorePaneFocus(string paneId)
        {
            var element = GetLastFocusedControl(paneId);

            // RACE CONDITION FIX: Check element is valid and loaded before focusing
            if (element == null)
            {
                logger.Log(LogLevel.Debug, "FocusHistory", $"No focused control found for pane {paneId}, using fallback chain");
                // Use fallback chain when no history exists
                return ApplyFocusWithFallback(null, paneId, "RestorePaneFocus");
            }

            // CRITICAL: Check if element is loaded before trying to focus
            // Without this check, we can trigger NullReferenceException during workspace switch
            // Cast to FrameworkElement to access IsLoaded property (UIElement doesn't have it)
            var frameworkElement = element as FrameworkElement;
            if (frameworkElement != null && !frameworkElement.IsLoaded)
            {
                logger.Log(LogLevel.Debug, "FocusHistory", $"Element not loaded yet for pane {paneId}, deferring focus");

                // Wait for element to be loaded before focusing
                RoutedEventHandler loadedHandler = null;
                loadedHandler = (s, e) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    logger.Log(LogLevel.Debug, "FocusHistory", $"Element now loaded for pane {paneId}, restoring focus");
                    RestorePaneFocus(paneId); // Recursive call now that element is loaded
                };
                frameworkElement.Loaded += loadedHandler;
                return false; // Not restored yet, waiting for load
            }

            try
            {
                // RACE CONDITION FIX: Double-check element is still valid
                if (element == null || (frameworkElement != null && !frameworkElement.IsLoaded))
                {
                    logger.Log(LogLevel.Debug, "FocusHistory", $"Element no longer valid for pane {paneId}, using fallback chain");
                    // Element was GC'd or unloaded - use fallback chain
                    return ApplyFocusWithFallback(null, paneId, "RestorePaneFocus_GCRecovery");
                }

                // Try to focus the element with full fallback chain support
                if (!ApplyFocusWithFallback(element, paneId, "RestorePaneFocus"))
                {
                    logger.Log(LogLevel.Warning, "FocusHistory", $"Failed to restore focus for pane {paneId}");
                    return false;
                }

                // Restore control state if possible
                if (paneFocusMap.TryGetValue(paneId, out var record))
                {
                    RestoreControlState(element, record.ControlState);
                }

                logger.Log(LogLevel.Debug, "FocusHistory",
                    $"Focus restored to {element.GetType().Name} in {paneId}");
                return true;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, "FocusHistory",
                    $"Failed to restore focus: {ex.Message}, using fallback chain");
                // Even if restoration fails, try fallback chain
                return ApplyFocusWithFallback(null, paneId, "RestorePaneFocus_Exception");
            }
        }

        /// <summary>
        /// Go back in focus history
        /// RACE CONDITION FIX: Check element is loaded before focusing
        /// </summary>
        public bool NavigateBack()
        {
            if (focusHistory.Count <= 1) return false;

            // Pop current focus
            focusHistory.Pop();

            // Get previous focus
            if (focusHistory.TryPeek(out var previous))
            {
                if (previous.Element.IsAlive)
                {
                    var element = previous.Element.Target as UIElement;

                    // RACE CONDITION FIX: Check element is valid and loaded before focusing
                    if (element == null)
                    {
                        logger.Log(LogLevel.Debug, "FocusHistory", "Previous element is null, cannot navigate back");
                        return false;
                    }

                    // CRITICAL: Check if element is loaded before trying to focus
                    // Cast to FrameworkElement to access IsLoaded property
                    var frameworkElement = element as FrameworkElement;
                    if (frameworkElement != null && !frameworkElement.IsLoaded)
                    {
                        logger.Log(LogLevel.Debug, "FocusHistory", "Previous element not loaded yet, deferring focus");

                        // Wait for element to load
                        RoutedEventHandler loadedHandler = null;
                        loadedHandler = (s, e) =>
                        {
                            frameworkElement.Loaded -= loadedHandler;
                            element.Focus();
                            Keyboard.Focus(element);
                            logger.Log(LogLevel.Debug, "FocusHistory", "Previous element now loaded, focus restored");
                        };
                        frameworkElement.Loaded += loadedHandler;
                        return false;
                    }

                    try
                    {
                        element.Focus();
                        Keyboard.Focus(element);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        logger.Log(LogLevel.Warning, "FocusHistory", $"Failed to navigate back: {ex.Message}");
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Clear focus history for a specific pane
        /// </summary>
        public void ClearPaneHistory(string paneId)
        {
            if (string.IsNullOrEmpty(paneId)) return;

            // Remove from pane focus map
            paneFocusMap.Remove(paneId);

            // Remove from history stack
            var newHistory = focusHistory.Where(r => r.PaneId != paneId).Reverse();
            focusHistory.Clear();
            foreach (var item in newHistory)
                focusHistory.Push(item);

            // Clear current focus if it belongs to this pane
            if (currentFocus?.PaneId == paneId)
            {
                currentFocus = null;
            }

            logger.Log(LogLevel.Debug, "FocusHistory",
                $"Cleared focus history for pane: {paneId}");
        }

        /// <summary>
        /// Save focus state for workspace switching
        /// </summary>
        public Dictionary<string, object> SaveWorkspaceState()
        {
            var state = new Dictionary<string, object>();

            foreach (var kvp in paneFocusMap)
            {
                if (kvp.Value.Element.IsAlive)
                {
                    state[$"Focus_{kvp.Key}"] = new
                    {
                        ElementType = kvp.Value.ElementType,
                        ControlState = kvp.Value.ControlState,
                        Timestamp = kvp.Value.Timestamp
                    };
                }
            }

            state["CurrentFocus"] = currentFocus?.PaneId;
            state["HistoryCount"] = focusHistory.Count;

            return state;
        }

        /// <summary>
        /// Restore focus state after workspace switch
        /// </summary>
        public void RestoreWorkspaceState(Dictionary<string, object> state)
        {
            if (state == null) return;

            // Clear current state
            focusHistory.Clear();
            paneFocusMap.Clear();

            // Restore current focus pane
            if (state.TryGetValue("CurrentFocus", out var currentPaneId))
            {
                // This will be restored when the pane is ready
                logger.Log(LogLevel.Debug, "FocusHistory",
                    $"Workspace focus will be restored to pane: {currentPaneId}");
            }
        }

        /// <summary>
        /// Temporarily disable focus tracking (useful during bulk UI updates)
        /// </summary>
        public IDisposable SuspendTracking()
        {
            isTrackingEnabled = false;
            return new TrackingResumer(() => isTrackingEnabled = true);
        }

        /// <summary>
        /// Clean up all tracked panes and event handlers
        /// </summary>
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            isTrackingEnabled = false;

            // Clear all focus history
            focusHistory.Clear();
            paneFocusMap.Clear();
            trackedPanes.Clear();
            currentFocus = null;

            // Clear event subscribers
            FocusChanged = null;

            logger.Log(LogLevel.Info, "FocusHistory",
                "FocusHistoryManager disposed, all event handlers cleared");
        }

        #region Private Methods

        private void OnElementGotFocus(object sender, RoutedEventArgs e)
        {
            if (!isTrackingEnabled || isDisposed) return;

            var element = e.OriginalSource as UIElement;
            if (element != null && ShouldTrackElement(element))
            {
                RecordFocus(element);
            }
        }

        private void OnElementLostFocus(object sender, RoutedEventArgs e)
        {
            if (isDisposed) return;
            // Can be used to save state before focus leaves
        }

        private bool ShouldTrackElement(UIElement element)
        {
            // Only track meaningful controls
            return element is TextBox ||
                   element is ListBox ||
                   element is ListBoxItem ||
                   element is TreeView ||
                   element is TreeViewItem ||
                   element is ComboBox ||
                   element is Button ||
                   element is CheckBox ||
                   element is RadioButton;
        }

        private string GetPaneId(UIElement element)
        {
            // Walk up the visual tree to find the containing pane
            DependencyObject current = element;
            while (current != null)
            {
                if (current is Components.PaneBase pane)
                {
                    return pane.PaneName;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return "Unknown";
        }

        private ControlState CaptureControlState(UIElement element)
        {
            var state = new ControlState();

            switch (element)
            {
                case TextBox textBox:
                    state.Text = textBox.Text;
                    state.CaretIndex = textBox.CaretIndex;
                    state.SelectionStart = textBox.SelectionStart;
                    state.SelectionLength = textBox.SelectionLength;
                    break;

                case ListBox listBox:
                    state.SelectedIndex = listBox.SelectedIndex;
                    state.ScrollPosition = GetScrollPosition(listBox);
                    break;

                case TreeView treeView:
                    state.SelectedItemPath = GetTreeViewPath(treeView.SelectedItem);
                    break;

                case ComboBox comboBox:
                    state.SelectedIndex = comboBox.SelectedIndex;
                    state.Text = comboBox.Text;
                    break;
            }

            return state;
        }

        private void RestoreControlState(UIElement element, ControlState state)
        {
            if (state == null) return;

            switch (element)
            {
                case TextBox textBox:
                    if (state.Text != null)
                        textBox.Text = state.Text;
                    if (state.CaretIndex.HasValue)
                        textBox.CaretIndex = state.CaretIndex.Value;
                    if (state.SelectionStart.HasValue)
                        textBox.SelectionStart = state.SelectionStart.Value;
                    if (state.SelectionLength.HasValue)
                        textBox.SelectionLength = state.SelectionLength.Value;
                    break;

                case ListBox listBox:
                    if (state.SelectedIndex.HasValue && state.SelectedIndex.Value < listBox.Items.Count)
                        listBox.SelectedIndex = state.SelectedIndex.Value;
                    if (state.ScrollPosition.HasValue)
                        SetScrollPosition(listBox, state.ScrollPosition.Value);
                    break;

                case ComboBox comboBox:
                    if (state.SelectedIndex.HasValue && state.SelectedIndex.Value < comboBox.Items.Count)
                        comboBox.SelectedIndex = state.SelectedIndex.Value;
                    break;
            }
        }

        private double? GetScrollPosition(ListBox listBox)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
            return scrollViewer?.VerticalOffset;
        }

        private void SetScrollPosition(ListBox listBox, double position)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
            scrollViewer?.ScrollToVerticalOffset(position);
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private string GetTreeViewPath(object selectedItem)
        {
            // Implementation depends on tree structure
            return selectedItem?.ToString();
        }

        #endregion

        #region Nested Types

        private class FocusRecord
        {
            public WeakReference Element { get; set; }
            public string ElementType { get; set; }
            public string PaneId { get; set; }
            public DateTime Timestamp { get; set; }
            public ControlState ControlState { get; set; }
        }

        private class ControlState
        {
            public string Text { get; set; }
            public int? CaretIndex { get; set; }
            public int? SelectionStart { get; set; }
            public int? SelectionLength { get; set; }
            public int? SelectedIndex { get; set; }
            public double? ScrollPosition { get; set; }
            public string SelectedItemPath { get; set; }
        }

        public class FocusChangedEventArgs : EventArgs
        {
            public string ElementType { get; }
            public string PaneId { get; }
            public DateTime Timestamp { get; }

            public FocusChangedEventArgs(string elementType, string paneId, DateTime timestamp)
            {
                ElementType = elementType;
                PaneId = paneId;
                Timestamp = timestamp;
            }
        }

        private class TrackingResumer : IDisposable
        {
            private readonly Action resumeAction;

            public TrackingResumer(Action resumeAction)
            {
                this.resumeAction = resumeAction;
            }

            public void Dispose()
            {
                resumeAction?.Invoke();
            }
        }

        #endregion
    }
}