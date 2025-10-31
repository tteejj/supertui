# SuperTUI Focus & Input System - Executive Summary

**Date:** 2025-10-31  
**Report:** FOCUS_INPUT_EDGE_CASES_ANALYSIS.md (907 lines)  
**Issues Found:** 27 distinct edge cases across 7 categories

---

## Critical Issues (MUST FIX IMMEDIATELY)

### 1. Memory Leak: FocusHistoryManager Event Handlers
- **File:** `FocusHistoryManager.cs:32-38`
- **Problem:** Global event handlers registered in constructor, never unregistered
- **Impact:** Every UI element's focus change triggers handler for disposed manager
- **Fix:** Implement IDisposable, unregister handlers in Dispose()

### 2. Use-After-Free: Focus During Pane Disposal  
- **File:** `PaneManager.cs:76-100`
- **Problem:** Focus restoration happens AFTER pane disposal; pending dispatcher callbacks execute on disposed pane
- **Impact:** NullReferenceException when accessing disposed controls
- **Fix:** Drain dispatcher queue for closed pane before disposal

### 3. Race Condition: Focus Restoration During Workspace Switch
- **File:** `MainWindow.xaml.cs:390-398`
- **Problem:** Focus restored at `Loaded` priority before panes finish initializing
- **Impact:** Focus restored to pane that's still loading, fails silently
- **Fix:** Wait for pane.Initialize() to complete before restoring focus

### 4. Null Pointer: Deferred Focus Restoration
- **File:** `MainWindow.xaml.cs:614-623`
- **Problem:** FocusedPane null-checked at queue time, but not at execution time
- **Impact:** FocusPane(null) called if pane closed between check and execution
- **Fix:** Null-check paneManager.FocusedPane again inside deferred action

### 5. TOCTOU Race: Weak Reference Garbage Collection
- **File:** `FocusHistoryManager.cs:92-94, 145-149`
- **Problem:** Weak reference can be collected between IsAlive check and .Target access
- **Impact:** NullReferenceException in focus restoration
- **Fix:** Use lock or try-finally around weak reference operations

---

## High-Risk Issues (SHOULD FIX SOON)

### 6. State Desynchronization: PaneManager.focusedPane vs WPF Focus
- **Problem:** WPF can move focus (Tab key) without notifying PaneManager
- **Impact:** Navigation commands use stale focus state
- **Fix:** Subscribe to pane.LostFocus events, update PaneManager.focusedPane

### 7. Dispatcher Priority Inversion
- **Problem:** Focus queued at Input priority, but other events at Loaded priority
- **Impact:** Wrong pane gets final focus during activation
- **Fix:** Use consistent priority (Input) for all focus operations

### 8. Data Corruption: Concurrent Async Loads
- **File:** `NotesPane.cs:596-604`
- **Problem:** Flag-based re-entrance can't prevent concurrent loads
- **Impact:** Editor text corrupted with mixed content
- **Fix:** Use proper async locking (SemaphoreSlim) or disable input during load

### 9. Silent Failure: Focus Operations With No Logging
- **File:** `PaneManager.cs:149-169`
- **Problem:** Focus attempts fail silently with no indication why
- **Impact:** Impossible to debug focus problems
- **Fix:** Add logging for focus failures and reasons

---

## Medium-Risk Issues (FIX EVENTUALLY)

### 10-18. Other Resource Leaks & Null Checks
- DispatcherTimer not stopped on exception (NotesPane.cs:86-105)
- FileSystemWatcher disposed inconsistently (NotesPane.cs:1494-1523)
- Keyboard.Focus(null) can throw (multiple locations)
- Application.Current null coalescing silently fails
- WeakReference.Target null checks missing (2 locations)
- FocusHistoryManager not tracking all control types
- CommandPalette callback captures disposed MainWindow

---

## Root Causes

1. **No IDisposable for FocusHistoryManager** - Can't unregister event handlers
2. **Multiple dispatcher queues at different priorities** - Race conditions
3. **Deferred focus restoration without re-validation** - Stale state
4. **Weak references without proper synchronization** - GC races
5. **No synchronization between pane disposal and focus restoration** - Use-after-free
6. **Flag-based re-entrance for async operations** - Not safe for concurrent access

---

## Severity Breakdown

| Severity | Count | Time to Fix |
|----------|-------|-------------|
| CRITICAL (P0) | 5 | 2-3 hours |
| HIGH (P1) | 4 | 4-6 hours |
| MEDIUM (P2) | 10 | 8-12 hours |
| LOW (P3) | 8 | 4-8 hours |

---

## Testing Recommendations

1. **Rapid pane switching:** Ctrl+Shift+T/N/P/F mashed repeatedly
2. **Workspace switching with focus:** Ctrl+1 to switch, verify focus restores
3. **Close pane during animation:** Ctrl+Shift+Q while opening overlay
4. **Long-running operations:** Load large notes, switch workspace mid-load
5. **Hot reload:** Change themes while focus is in complex control
6. **Window activation:** Alt+Tab away and back rapidly

---

## Full Analysis

See **FOCUS_INPUT_EDGE_CASES_ANALYSIS.md** for complete details on all 27 issues with code examples and impact analysis.

