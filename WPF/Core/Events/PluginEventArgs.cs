using System;
using SuperTUI.Extensions;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Event arguments for plugin-related events (loaded, unloaded, etc.)
    /// Contains the plugin instance that triggered the event.
    /// </summary>
    public class PluginEventArgs : EventArgs
    {
        /// <summary>
        /// The plugin that was loaded, unloaded, or otherwise affected
        /// </summary>
        public IPlugin Plugin { get; set; }
    }
}
