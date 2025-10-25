using System;
using System.Windows.Input;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Represents a keyboard shortcut with associated action and description.
    /// Used by ShortcutManager to manage global and workspace-specific shortcuts.
    /// </summary>
    public class KeyboardShortcut
    {
        /// <summary>
        /// The key to trigger this shortcut
        /// </summary>
        public Key Key { get; set; }

        /// <summary>
        /// The modifier keys required (Ctrl, Alt, Shift, etc.)
        /// </summary>
        public ModifierKeys Modifiers { get; set; }

        /// <summary>
        /// Alias for Modifiers property (for backward compatibility)
        /// </summary>
        public ModifierKeys Modifier => Modifiers;

        /// <summary>
        /// Action to execute when shortcut is triggered
        /// </summary>
        public Action Action { get; set; }

        /// <summary>
        /// Human-readable description of what this shortcut does
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Checks if the provided key and modifiers match this shortcut
        /// </summary>
        public bool Matches(Key key, ModifierKeys modifiers)
        {
            return Key == key && Modifiers == modifiers;
        }

        /// <summary>
        /// Returns a human-readable string representation of this shortcut
        /// </summary>
        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();

            if ((Modifiers & ModifierKeys.Control) != 0)
                parts.Add("Ctrl");
            if ((Modifiers & ModifierKeys.Alt) != 0)
                parts.Add("Alt");
            if ((Modifiers & ModifierKeys.Shift) != 0)
                parts.Add("Shift");
            if ((Modifiers & ModifierKeys.Windows) != 0)
                parts.Add("Win");

            parts.Add(Key.ToString());

            return string.Join("+", parts);
        }
    }
}
