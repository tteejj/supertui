using System;
using System.Collections.Generic;
using System.Windows.Input;
using SuperTUI.Core;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for keyboard shortcut management
    /// Supports global, workspace-specific, and pane-context shortcuts
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
        /// Register a pane-context shortcut (only executes when specified pane is focused)
        /// </summary>
        void RegisterForPane(string paneName, Key key, ModifierKeys modifiers, Action action, string description = "");

        /// <summary>
        /// Handle a key press event
        /// </summary>
        bool HandleKeyPress(Key key, ModifierKeys modifiers, string currentWorkspace = null, string focusedPaneName = null);

        /// <summary>
        /// Get all global shortcuts
        /// </summary>
        IReadOnlyList<KeyboardShortcut> GetGlobalShortcuts();

        /// <summary>
        /// Get shortcuts for a specific workspace
        /// </summary>
        IReadOnlyList<KeyboardShortcut> GetWorkspaceShortcuts(string workspaceName);

        /// <summary>
        /// Get shortcuts for a specific pane
        /// </summary>
        IReadOnlyList<KeyboardShortcut> GetPaneShortcuts(string paneName);

        /// <summary>
        /// Get all pane shortcuts organized by pane name
        /// </summary>
        IReadOnlyDictionary<string, IReadOnlyList<KeyboardShortcut>> GetAllPaneShortcuts();

        /// <summary>
        /// Get all shortcuts for all contexts
        /// </summary>
        List<KeyboardShortcut> GetAllShortcuts();

        /// <summary>
        /// Clear all shortcuts
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Clear shortcuts for a specific workspace
        /// </summary>
        void ClearWorkspace(string workspaceName);

        /// <summary>
        /// Clear shortcuts for a specific pane
        /// </summary>
        void ClearPane(string paneName);

        /// <summary>
        /// Utility: Check if user is typing in a text input control
        /// </summary>
        bool IsUserTyping();
    }
}
