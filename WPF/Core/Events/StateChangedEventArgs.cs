using System;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Event arguments for state change events.
    /// Contains the state snapshot that was changed.
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The state snapshot that was captured or changed
        /// </summary>
        public StateSnapshot Snapshot { get; set; }
    }
}
