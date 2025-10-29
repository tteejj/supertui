using System;
using System.Collections.Generic;
using SuperTUI.Core;
using SuperTUI.DI;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Metadata about a plugin (name, version, author, dependencies, etc.)
    /// </summary>
    public class PluginMetadata
    {
        /// <summary>
        /// Plugin name (should be unique)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Plugin version (semantic versioning recommended)
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Plugin author/creator
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Human-readable description of plugin functionality
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// List of plugin names this plugin depends on
        /// </summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>
        /// Whether this plugin is currently enabled
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Context provided to plugins when they are initialized.
    /// Gives plugins access to core SuperTUI services and shared data.
    /// </summary>
    public class PluginContext
    {
        /// <summary>
        /// Access to the logging system
        /// </summary>
        public Logger Logger { get; set; }

        /// <summary>
        /// Access to configuration management
        /// </summary>
        public ConfigurationManager Config { get; set; }

        /// <summary>
        /// Access to theme management
        /// </summary>
        public ThemeManager Themes { get; set; }

        // Note: Old workspace and widget infrastructure removed during pane system migration
        // Plugins should use pane system instead
        // /// <summary>
        // /// Access to workspace management
        // /// </summary>
        // public PaneWorkspaceManager Workspaces { get; set; }

        // /// <summary>
        // /// Pane factory for creating panes with dependency injection.
        // /// </summary>
        // public PaneFactory PaneFactory { get; set; }

        /// <summary>
        /// Shared data dictionary for inter-plugin communication
        /// </summary>
        public Dictionary<string, object> SharedData { get; set; } = new Dictionary<string, object>();
    }
}
