# SuperTUI Input Routing & Keyboard Handling - Executive Summary

**Date:** 2025-10-31  
**Status:** EXHAUSTIVE ANALYSIS COMPLETE  
**Full Analysis:** See `EXHAUSTIVE_INPUT_ROUTING_ANALYSIS.md`

---

## Quick Assessment

**Overall Grade: C+ (Functional but Fragile)**

- ✓ Architecture is sound in theory
- ✓ Works for normal use cases
- ✗ Has 5 critical design flaws
- ✗ Edge cases (modals, workspace switches) prone to bugs
- ✗ Code harder to understand than it should be

---

## Critical Issues Found (5 Total)

### 1. **MODAL INPUT NOT BLOCKED** - HIGH PRIORITY
   - **Problem:** CommandPalette modal doesn't block input to background panes
   - **Impact:** User can open panes while palette is open (unexpected)
   - **Location:** MainWindow.xaml.cs ShowCommandPalette()
   - **Fix:** Disable PaneCanvas or add InputManager

### 2. **FOCUS SCOPE TRAPS TAB KEY** - HIGH PRIORITY
   - **Problem:** PaneCanvas has IsFocusScope="True" + TabNavigation="Cycle"
   - **Impact:** Tab doesn't navigate to modal overlay or status bar
   - **Location:** MainWindow.xaml (lines 14-18)
   - **Fix:** Remove focus scope or add explicit TabIndex ordering

### 3. **DUAL FOCUS SYSTEM CONFUSION** - MEDIUM PRIORITY
   - **Problem:** Two separate focus trackers (custom bool + WPF bool)
   - **Impact:** Unclear which is authoritative, possible desync
   - **Location:** PaneManager.FocusPane() and PaneBase.IsFocused
   - **Fix:** Consolidate to single source of truth

### 4. **NO FOCUS FALLBACK STRATEGY** - MEDIUM PRIORITY
   - **Problem:** If focus element is garbage collected, focus lost
   - **Impact:** User loses keyboard after workspace switch sometimes
   - **Location:** FocusHistoryManager.RestorePaneFocus()
   - **Fix:** Add fallback chain (element → first child → pane)

### 5. **INPUT VALIDATION GAPS** - LOW PRIORITY
   - **Problem:** Doesn't check ComboBox or read-only TextBox
   - **Impact:** Shortcuts might fire in unexpected contexts
   - **Location:** ShortcutManager.IsTypingInTextInput()
   - **Fix:** Expand checks to ComboBox and IsReadOnly

---

## Input Flow Path (How Keystroke Routes)

```
User presses key
    ↓
PreviewKeyDown tunnel phase (top-down)
    ↓
Individual component handlers (e.g., TextBox)
    ↓
Pane-level handler (e.g., OnPreviewKeyDown)
    ↓
KeyDown bubble phase (bottom-up)
    ↓
MainWindow_KeyDown
    ↓
ShortcutManager.HandleKeyPress()
    ├─ Check IsTypingInTextInput() [CRITICAL]
    ├─ Try workspace shortcuts
    ├─ Try global shortcuts
    └─ Return true/false
    ↓
If handled: e.Handled = true, stop
If not handled: Continue routing (may be lost)
```

---

## Handler Locations (Complete List)

**MainWindow:** 4 handlers (KeyDown, Activated, Deactivated, Loaded)

**NotesPane:** 5+ PreviewKeyDown handlers
- Pane level, List level, Editor level, Command input, Command list

**TaskListPane:** 3+ KeyDown handlers
- Quick add fields (title, date, priority)

**FileBrowserPane:** 2 PreviewKeyDown handlers
- Pane level, File list level

**CommandPalettePane:** 2 PreviewKeyDown handlers
- Search box, Results list

**Total keyboard handlers:** ~189 instances across codebase

---

## Key Conflicts Identified

| Key | Handlers | Issue |
|-----|----------|-------|
| Arrow Keys | 4+ | Move pane (F12), Edit cursor, Menu nav, Pane nav (Ctrl+Shift) |
| Escape | 3+ | Close modal, Close palette, Mode exit |
| Ctrl+Z/Y | 2 | ShortcutManager + TextBox.Undo (double-handling risk) |
| A, D, E, S, F, C | Protected | Safe - guarded with TextBox checks |

---

## Focus System Complexity

### Current Architecture (CONFUSING):
- **WPF's IsFocused:** `UIElement.IsKeyboardFocusWithin`
- **Custom IsFocused:** `PaneBase.IsFocused` boolean property
- **Focus Tracking:** `PaneManager.focusedPane`
- **History Tracking:** `FocusHistoryManager.paneFocusMap`

**Result:** 4 separate focus tracking systems = maintenance nightmare

### Recommended: Single source of truth
- Remove custom `IsFocused` property
- Use only WPF's `IsKeyboardFocusWithin`
- Rename internal `_focusedPane` for clarity
- Document why custom tracking was needed

---

## Modal Input Problem Example

**Scenario:**
1. User opens CommandPalette (Shift+;)
2. User types Ctrl+Shift+T (open Tasks pane)
3. CommandPalette doesn't handle this combo
4. Event bubbles to MainWindow
5. ShortcutManager recognizes it
6. **UNEXPECTED:** New Tasks pane opens while palette still visible
7. User confused - palette should have "taken over" keyboard

**Why it happens:**
- ModalOverlay is visual only (doesn't block input)
- ShortcutManager still processes global shortcuts
- No "modal mode" in input system

---

## Test Results

**Build Status:** ✅ 0 Errors (325 warnings from obsolete attributes)

**Manual Testing (from INPUT_ROUTING_FIXED.md):**
- ✅ Typing in TextBox doesn't trigger shortcuts
- ✅ Ctrl+S/Z/Y/X/C/V work while typing
- ✅ Escape works to close modes
- ✅ Global shortcuts (Ctrl+1-9) work from anywhere
- ✅ Arrow keys work normally
- ⚠️ Not tested: Modal input blocking, Tab in modals

---

## Recommendations (Priority Order)

### IMMEDIATE (High Priority):
1. **Remove Focus Scope** - Delete `FocusManager.IsFocusScope="True"` from PaneCanvas
2. **Block Modal Input** - Add check to prevent shortcuts while CommandPalette open
3. **Simplify Focus** - Remove custom `IsFocused` property, use WPF only

### SHORT TERM (Medium Priority):
4. **Add Focus Fallback** - Implement fallback chain in RestorePaneFocus()
5. **Add Event Tracing** - Log keyboard events for debugging
6. **Document Architecture** - Add diagram showing focus hierarchy

### LONG TERM (Nice-to-Have):
7. **Input Validation Helper** - Create ValidateInputContext() utility
8. **Performance** - Cache Keyboard.FocusedElement checks
9. **Testing** - Add integration tests for input routing

---

## Files to Review/Fix

| File | Issue | Priority |
|------|-------|----------|
| MainWindow.xaml | Focus scope, no input blocking | HIGH |
| MainWindow.xaml.cs | Modal overlay not truly modal | HIGH |
| ShortcutManager.cs | Input validation gaps | LOW |
| PaneManager.cs | Dual focus system | MEDIUM |
| FocusHistoryManager.cs | No fallback strategy | MEDIUM |
| PaneBase.cs | Custom IsFocused property | MEDIUM |

---

## Code Review Checklist

Before deploying to production, verify:

- [ ] Focus Scope removed from PaneCanvas (or TabIndex ordered)
- [ ] CommandPalette blocks all non-modal shortcuts
- [ ] Focus system consolidated (WPF only)
- [ ] Focus fallback implemented in RestorePaneFocus
- [ ] Modal behavior tested (open/close/navigation)
- [ ] Tab key works in all modals
- [ ] Workspace switches restore focus correctly
- [ ] No focus lost after pane close
- [ ] Input validation covers all text controls
- [ ] Event handlers have proper error handling

---

## Full Analysis Location

For detailed breakdown of all 12 sections:

**File:** `/home/teej/supertui/WPF/EXHAUSTIVE_INPUT_ROUTING_ANALYSIS.md`

Sections:
1. Input Flow Path (complete keystroke route)
2. Event Handler Locations (all 189 instances)
3. Input Conflict Analysis (Ctrl+Z, Arrow, Escape, etc.)
4. Modal Input Handling (CommandPalette behavior)
5. Focus Scope Analysis (IsFocusScope impact)
6. Input Validation & Transformation (whitelist analysis)
7. Dead Zones (input loss scenarios)
8. Focus Restoration Issues (workspace switches)
9. Architecture Assessment (strengths/weaknesses)
10. Critical Issues Summary (5 issues detailed)
11. Recommended Improvements (5 specific fixes)
12. Conclusion (overall assessment)

---

## Quick Verdict

**Is the current model sound?**

**Functionally:** YES - Works for normal use cases

**Architecturally:** PARTIALLY - Multi-layer design is correct, but has execution flaws

**In production?** CAUTIOUSLY - Edge cases (modals, workspace switches) need fixes

**Recommended next step:** Fix HIGH priority issues #1 and #2, then audit focus system
