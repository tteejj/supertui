using System;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.DI
{
    /// <summary>
    /// Helper class to configure dependency injection
    /// Call ConfigureServices() at application startup
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// Configure all services for dependency injection
        /// </summary>
        public static void ConfigureServices(ServiceContainer container)
        {
            // Infrastructure services (Singletons)
            container.RegisterSingleton<ILogger>(Logger.Instance);
            container.RegisterSingleton<IConfigurationManager>(ConfigurationManager.Instance);
            container.RegisterSingleton<IThemeManager>(ThemeManager.Instance);
            container.RegisterSingleton<ISecurityManager>(SecurityManager.Instance);
            container.RegisterSingleton<IErrorHandler>(ErrorHandler.Instance);

            // Event system (Singleton) - registered with interface
            container.RegisterSingleton<IEventBus>(EventBus.Instance);

            // State management (Singleton)
            container.RegisterSingleton(StatePersistenceManager.Instance);
            container.RegisterSingleton(PerformanceMonitor.Instance);
            container.RegisterSingleton(PluginManager.Instance);

            // Workspace management (Transient - multiple workspaces possible)
            // Note: WorkspaceManager requires ContentControl in constructor, so we use factory
            container.RegisterTransient<WorkspaceManager>(services =>
            {
                throw new InvalidOperationException(
                    "WorkspaceManager requires a ContentControl parameter. " +
                    "Create it manually: new WorkspaceManager(contentControl)");
            });

            // Widgets (Transient - create new instances)
            // Widgets will be registered dynamically as they're discovered
            // For now, they can be created with 'new' since they don't have dependencies
        }

        /// <summary>
        /// Initialize all singleton services
        /// Call this after ConfigureServices()
        /// </summary>
        public static void InitializeServices(ServiceContainer container, string configPath = null, string themesPath = null, string statePath = null, string pluginsPath = null)
        {
            // Initialize configuration
            var config = container.Resolve<IConfigurationManager>() as ConfigurationManager;
            config?.Initialize(configPath ?? GetDefaultConfigPath());

            // Initialize theme manager
            var themes = container.Resolve<IThemeManager>() as ThemeManager;
            themes?.Initialize(themesPath);

            // Initialize security manager
            var security = container.Resolve<ISecurityManager>() as SecurityManager;
            security?.Initialize();

            // Initialize state persistence
            var state = container.Resolve<StatePersistenceManager>();
            state?.Initialize(statePath);

            // Initialize plugin manager
            var plugins = container.Resolve<PluginManager>();
            var pluginContext = new PluginContext
            {
                Logger = container.Resolve<ILogger>() as Logger,
                Config = container.Resolve<IConfigurationManager>() as ConfigurationManager,
                Themes = container.Resolve<IThemeManager>() as ThemeManager,
                Workspaces = null, // Set after WorkspaceManager is created
                SharedData = new System.Collections.Generic.Dictionary<string, object>()
            };
            plugins?.Initialize(pluginsPath, pluginContext);
        }

        private static string GetDefaultConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configDir = System.IO.Path.Combine(appData, "SuperTUI");
            System.IO.Directory.CreateDirectory(configDir);
            return System.IO.Path.Combine(configDir, "config.json");
        }

        /// <summary>
        /// Quick setup for testing/demos
        /// </summary>
        public static ServiceContainer QuickSetup()
        {
            var container = ServiceContainer.Instance;
            ConfigureServices(container);
            InitializeServices(container);
            return container;
        }
    }
}
