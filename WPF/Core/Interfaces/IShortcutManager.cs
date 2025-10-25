using System;
using System.Collections.Generic;
using System.Windows.Input;
using SuperTUI.Core;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for keyboard shortcut management
    /// </summary>
    public interface IShortcutManager
    {
        /// <summary>
        /// Register a global keyboard shortcut
        /// </summary>
        void RegisterGlobal(Key key, ModifierKeys modifiers, Action action, string description = "");

        /// <summary>
        /// Register a workspace-specific keyboard shortcut
        /// </summary>
        void RegisterForWorkspace(string workspaceName, Key key, ModifierKeys modifiers, Action action, string description = "");

        /// <summary>
        /// Handle a key press event
        /// </summary>
        bool HandleKeyPress(Key key, ModifierKeys modifiers, string currentWorkspace = null);

        /// <summary>
        /// Get all global shortcuts
        /// </summary>
        IReadOnlyList<KeyboardShortcut> GetGlobalShortcuts();

        /// <summary>
        /// Get shortcuts for a specific workspace
        /// </summary>
        IReadOnlyList<KeyboardShortcut> GetWorkspaceShortcuts(string workspaceName);

        /// <summary>
        /// Clear all shortcuts
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Clear shortcuts for a specific workspace
        /// </summary>
        void ClearWorkspace(string workspaceName);
    }
}
