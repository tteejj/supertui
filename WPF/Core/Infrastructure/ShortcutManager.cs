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
    public class KeyboardShortcut
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public ModifierKeys Modifier => Modifiers; // Alias for consistency
        public Action Action { get; set; }
        public string Description { get; set; }

        public bool Matches(Key key, ModifierKeys modifiers)
        {
            return Key == key && Modifiers == modifiers;
        }
    }

    public class ShortcutManager
    {
        private static readonly Lazy<ShortcutManager> instance =
            new Lazy<ShortcutManager>(() => new ShortcutManager());
        public static ShortcutManager Instance => instance.Value;

        private List<KeyboardShortcut> globalShortcuts = new List<KeyboardShortcut>();
        private Dictionary<string, List<KeyboardShortcut>> workspaceShortcuts = new Dictionary<string, List<KeyboardShortcut>>();

        public void RegisterGlobal(Key key, ModifierKeys modifiers, Action action, string description = "")
        {
            globalShortcuts.Add(new KeyboardShortcut
            {
                Key = key,
                Modifiers = modifiers,
                Action = action,
                Description = description
            });
        }

        public void RegisterForWorkspace(string workspaceName, Key key, ModifierKeys modifiers, Action action, string description = "")
        {
            if (!workspaceShortcuts.ContainsKey(workspaceName))
                workspaceShortcuts[workspaceName] = new List<KeyboardShortcut>();

            workspaceShortcuts[workspaceName].Add(new KeyboardShortcut
            {
                Key = key,
                Modifiers = modifiers,
                Action = action,
                Description = description
            });
        }

        public bool HandleKeyDown(Key key, ModifierKeys modifiers, string currentWorkspace)
        {
            // Try workspace-specific shortcuts first
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

            // Try global shortcuts
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

        public List<KeyboardShortcut> GetAllShortcuts()
        {
            var all = new List<KeyboardShortcut>(globalShortcuts);
            foreach (var kvp in workspaceShortcuts)
            {
                all.AddRange(kvp.Value);
            }
            return all;
        }

        public bool HandleKeyDown(Key key, ModifierKeys modifiers)
        {
            return HandleKeyDown(key, modifiers, null);
        }
    }
}
