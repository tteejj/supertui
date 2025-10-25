using System;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Plugin interface that all plugins must implement.
    /// Plugins extend SuperTUI functionality with custom widgets, services, or behaviors.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Plugin metadata (name, version, author, dependencies, etc.)
        /// </summary>
        PluginMetadata Metadata { get; }

        /// <summary>
        /// Initialize the plugin with the provided context.
        /// Called once when the plugin is loaded.
        /// </summary>
        /// <param name="context">Context providing access to SuperTUI services</param>
        void Initialize(PluginContext context);

        /// <summary>
        /// Shutdown the plugin and clean up resources.
        /// Called when the plugin is unloaded or application exits.
        /// </summary>
        void Shutdown();
    }
}
