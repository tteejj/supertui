using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    public class ShortcutManager : IShortcutManager
    {
        private static readonly Lazy<ShortcutManager> instance =
            new Lazy<ShortcutManager>(() => new ShortcutManager());
        public static ShortcutManager Instance => instance.Value;

        private readonly object shortcutsLock = new object();
        private List<KeyboardShortcut> globalShortcuts = new List<KeyboardShortcut>();
        private Dictionary<string, List<KeyboardShortcut>> workspaceShortcuts = new Dictionary<string, List<KeyboardShortcut>>();
        private Dictionary<string, List<KeyboardShortcut>> paneShortcuts = new Dictionary<string, List<KeyboardShortcut>>();

        public void RegisterGlobal(Key key, ModifierKeys modifiers, Action action, string description = "")
        {
            lock (shortcutsLock)
            {
                // Check for duplicates (conflict detection)
                if (globalShortcuts.Any(s => s.Key == key && s.Modifiers == modifiers))
                {
                    System.Diagnostics.Debug.WriteLine($"Shortcut conflict: {key}+{modifiers} already registered in global shortcuts");
                    return; // Skip duplicate registration
                }

                globalShortcuts.Add(new KeyboardShortcut
                {
                    Key = key,
                    Modifiers = modifiers,
                    Action = action,
                    Description = description
                });
            }
        }

        public void RegisterForWorkspace(string workspaceName, Key key, ModifierKeys modifiers, Action action, string description = "")
        {
            lock (shortcutsLock)
            {
                if (!workspaceShortcuts.ContainsKey(workspaceName))
                    workspaceShortcuts[workspaceName] = new List<KeyboardShortcut>();

                // Check for duplicates (conflict detection)
                if (workspaceShortcuts[workspaceName].Any(s => s.Key == key && s.Modifiers == modifiers))
                {
                    System.Diagnostics.Debug.WriteLine($"Shortcut conflict: {key}+{modifiers} already registered for workspace '{workspaceName}'");
                    return; // Skip duplicate registration
                }

                workspaceShortcuts[workspaceName].Add(new KeyboardShortcut
                {
                    Key = key,
                    Modifiers = modifiers,
                    Action = action,
                    Description = description
                });
            }
        }

        public void RegisterForPane(string paneName, Key key, ModifierKeys modifiers, Action action, string description = "")
        {
            lock (shortcutsLock)
            {
                if (!paneShortcuts.ContainsKey(paneName))
                    paneShortcuts[paneName] = new List<KeyboardShortcut>();

                // Check for duplicates (conflict detection)
                if (paneShortcuts[paneName].Any(s => s.Key == key && s.Modifiers == modifiers))
                {
                    System.Diagnostics.Debug.WriteLine($"Shortcut conflict: {key}+{modifiers} already registered for pane '{paneName}'");
                    return; // Skip duplicate registration
                }

                paneShortcuts[paneName].Add(new KeyboardShortcut
                {
                    Key = key,
                    Modifiers = modifiers,
                    Action = action,
                    Description = description
                });
            }
        }

        public bool HandleKeyDown(Key key, ModifierKeys modifiers, string currentWorkspace, string focusedPaneName = null)
        {
            // CRITICAL: Try pane-specific shortcuts FIRST (highest priority)
            // This allows each pane to decide what shortcuts work while typing in that pane
            // (e.g., NotesPane allows D, O, W to close/save while editor is focused)
            if (!string.IsNullOrEmpty(focusedPaneName) && paneShortcuts.ContainsKey(focusedPaneName))
            {
                foreach (var shortcut in paneShortcuts[focusedPaneName])
                {
                    if (shortcut.Matches(key, modifiers))
                    {
                        shortcut.Action?.Invoke();
                        return true;
                    }
                }
            }

            // Check if user is typing in a text input control
            // This prevents workspace/global shortcuts from firing when the user is typing
            // (but pane shortcuts were already checked above, giving panes control)
            if (IsTypingInTextInput() && !IsAllowedWhileTyping(key, modifiers))
            {
                // User is typing, don't process workspace/global shortcuts
                return false;
            }

            // Try workspace-specific shortcuts second
            if (!string.IsNullOrEmpty(currentWorkspace) && workspaceShortcuts.ContainsKey(currentWorkspace))
            {
                foreach (var shortcut in workspaceShortcuts[currentWorkspace])
                {
                    if (shortcut.Matches(key, modifiers))
                    {
                        shortcut.Action?.Invoke();
                        return true;
                    }
                }
            }

            // Try global shortcuts last
            foreach (var shortcut in globalShortcuts)
            {
                if (shortcut.Matches(key, modifiers))
                {
                    shortcut.Action?.Invoke();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if the user is currently typing in a text input control
        /// </summary>
        private bool IsTypingInTextInput()
        {
            var focused = Keyboard.FocusedElement;
            return focused is TextBox ||
                   focused is System.Windows.Controls.Primitives.TextBoxBase ||
                   focused is RichTextBox ||
                   focused is PasswordBox;
        }

        /// <summary>
        /// Determine if a shortcut should work even while typing
        /// </summary>
        private bool IsAllowedWhileTyping(Key key, ModifierKeys modifiers)
        {
            // These shortcuts should work even when typing:
            // - Ctrl+S (Save)
            // - Ctrl+Z (Undo - for text editing)
            // - Ctrl+Y (Redo - for text editing)
            // - Ctrl+X/C/V (Cut/Copy/Paste)
            // - Ctrl+A (Select All)
            // - Escape (often used to exit edit mode)

            if (modifiers == ModifierKeys.Control)
            {
                switch (key)
                {
                    case Key.S: // Save
                    case Key.Z: // Undo
                    case Key.Y: // Redo
                    case Key.X: // Cut
                    case Key.C: // Copy
                    case Key.V: // Paste
                    case Key.A: // Select All
                        return true;
                }
            }

            if (key == Key.Escape && modifiers == ModifierKeys.None)
            {
                return true; // Escape should work to exit edit modes
            }

            // Block all other shortcuts while typing
            return false;
        }

        public List<KeyboardShortcut> GetAllShortcuts()
        {
            lock (shortcutsLock)
            {
                var all = new List<KeyboardShortcut>(globalShortcuts);
                foreach (var kvp in workspaceShortcuts)
                {
                    all.AddRange(kvp.Value);
                }
                foreach (var kvp in paneShortcuts)
                {
                    all.AddRange(kvp.Value);
                }
                return all;
            }
        }

        public bool HandleKeyDown(Key key, ModifierKeys modifiers)
        {
            return HandleKeyDown(key, modifiers, null, null);
        }

        // Interface implementation - HandleKeyPress is an alias for HandleKeyDown
        public bool HandleKeyPress(Key key, ModifierKeys modifiers, string currentWorkspace = null, string focusedPaneName = null)
        {
            return HandleKeyDown(key, modifiers, currentWorkspace, focusedPaneName);
        }

        public IReadOnlyList<KeyboardShortcut> GetGlobalShortcuts()
        {
            lock (shortcutsLock)
            {
                return new List<KeyboardShortcut>(globalShortcuts);
            }
        }

        public IReadOnlyList<KeyboardShortcut> GetWorkspaceShortcuts(string workspaceName)
        {
            lock (shortcutsLock)
            {
                if (workspaceShortcuts.TryGetValue(workspaceName, out var shortcuts))
                {
                    return new List<KeyboardShortcut>(shortcuts);
                }
                return new List<KeyboardShortcut>();
            }
        }

        public IReadOnlyList<KeyboardShortcut> GetPaneShortcuts(string paneName)
        {
            lock (shortcutsLock)
            {
                if (paneShortcuts.TryGetValue(paneName, out var shortcuts))
                {
                    return new List<KeyboardShortcut>(shortcuts);
                }
                return new List<KeyboardShortcut>();
            }
        }

        public IReadOnlyDictionary<string, IReadOnlyList<KeyboardShortcut>> GetAllPaneShortcuts()
        {
            lock (shortcutsLock)
            {
                var result = new Dictionary<string, IReadOnlyList<KeyboardShortcut>>();
                foreach (var kvp in paneShortcuts)
                {
                    result[kvp.Key] = new List<KeyboardShortcut>(kvp.Value);
                }
                return result;
            }
        }

        public void ClearAll()
        {
            lock (shortcutsLock)
            {
                globalShortcuts.Clear();
                workspaceShortcuts.Clear();
                paneShortcuts.Clear();
            }
        }

        public void ClearWorkspace(string workspaceName)
        {
            lock (shortcutsLock)
            {
                if (workspaceShortcuts.ContainsKey(workspaceName))
                {
                    workspaceShortcuts.Remove(workspaceName);
                }
            }
        }

        public void ClearPane(string paneName)
        {
            lock (shortcutsLock)
            {
                if (paneShortcuts.ContainsKey(paneName))
                {
                    paneShortcuts.Remove(paneName);
                }
            }
        }

        /// <summary>
        /// Public utility method for panes to check if user is typing
        /// </summary>
        public bool IsUserTyping()
        {
            return IsTypingInTextInput();
        }
    }
}
