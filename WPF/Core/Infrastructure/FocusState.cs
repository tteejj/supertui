using System;
using System.Collections.Generic;
using System.Windows;
using SuperTUI.Core.Components;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Type-safe container for focus state information.
    /// Replaces the previous Tag property pattern for storing focus context.
    /// </summary>
    public class FocusState
    {
        /// <summary>
        /// The UI element that previously had keyboard focus.
        /// </summary>
        public UIElement? PreviousElement { get; set; }

        /// <summary>
        /// The pane that previously had focus.
        /// </summary>
        public PaneBase? PreviousPane { get; set; }

        /// <summary>
        /// Additional context about the focused element (e.g., caret position, selection).
        /// </summary>
        public Dictionary<string, object>? Context { get; set; }

        /// <summary>
        /// Timestamp when focus state was captured.
        /// </summary>
        public DateTime CapturedAt { get; set; } = DateTime.Now;
    }
}
