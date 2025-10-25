using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for plugin management - enables loading and managing plugins
    /// </summary>
    public interface IPluginManager
    {
        /// <summary>
        /// Event fired when a plugin is loaded
        /// </summary>
        event EventHandler<PluginEventArgs> PluginLoaded;

        /// <summary>
        /// Event fired when a plugin is unloaded
        /// </summary>
        event EventHandler<PluginEventArgs> PluginUnloaded;

        /// <summary>
        /// Initialize the plugin manager with plugins directory and context
        /// </summary>
        void Initialize(string pluginsDir, PluginContext context);

        /// <summary>
        /// Load all plugins from the plugins directory
        /// </summary>
        void LoadPlugins();

        /// <summary>
        /// Load a specific plugin from an assembly path
        /// </summary>
        void LoadPlugin(string assemblyPath);

        /// <summary>
        /// Unload a plugin by name
        /// </summary>
        void UnloadPlugin(string pluginName);

        /// <summary>
        /// Get a loaded plugin by name
        /// </summary>
        IPlugin GetPlugin(string name);

        /// <summary>
        /// Get all loaded plugins
        /// </summary>
        IReadOnlyDictionary<string, IPlugin> GetLoadedPlugins();

        /// <summary>
        /// Check if a plugin is loaded
        /// </summary>
        bool IsPluginLoaded(string name);

        /// <summary>
        /// Execute a plugin command
        /// </summary>
        void ExecutePluginCommand(string pluginName, string command, params object[] args);
    }
}
