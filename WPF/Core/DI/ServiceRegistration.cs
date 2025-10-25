using System;
using SuperTUI.Core;
using SuperTUI.Infrastructure;
using SuperTUI.Extensions;

namespace SuperTUI.DI
{
    /// <summary>
    /// Helper class to configure dependency injection
    /// Call ConfigureServices() at application startup
    ///
    /// PHASE 3: Maximum DI Migration (2025-10-25)
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// Configure all services for dependency injection
        /// PHASE 3: Maximum DI - Register all infrastructure services
        /// </summary>
        public static void ConfigureServices(ServiceContainer container)
        {
            Logger.Instance.Info("DI", "🔧 Configuring services for dependency injection...");

            // PHASE 3: Register existing singletons with their interfaces
            container.RegisterSingleton<ILogger, Logger>(Logger.Instance);
            container.RegisterSingleton<IConfigurationManager, ConfigurationManager>(ConfigurationManager.Instance);
            container.RegisterSingleton<IThemeManager, ThemeManager>(ThemeManager.Instance);
            container.RegisterSingleton<ISecurityManager, SecurityManager>(SecurityManager.Instance);
            container.RegisterSingleton<IErrorHandler, ErrorHandler>(ErrorHandler.Instance);
            container.RegisterSingleton<IStatePersistenceManager, StatePersistenceManager>(StatePersistenceManager.Instance);
            container.RegisterSingleton<IPerformanceMonitor, PerformanceMonitor>(PerformanceMonitor.Instance);
            container.RegisterSingleton<IPluginManager, PluginManager>(PluginManager.Instance);
            container.RegisterSingleton<IEventBus, EventBus>(EventBus.Instance);
            container.RegisterSingleton<IShortcutManager, ShortcutManager>(ShortcutManager.Instance);
            // HotReloadManager not yet implemented - skip for now

            Logger.Instance.Info("DI", $"✅ Registered {10} infrastructure services");
        }

        /// <summary>
        /// Initialize all singleton services
        /// Call this after ConfigureServices()
        /// PHASE 3: Initialize via DI container
        /// </summary>
        public static void InitializeServices(ServiceContainer container, string configPath = null, string themesPath = null)
        {
            Logger.Instance.Info("DI", "🚀 Initializing services...");

            // Initialize configuration
            var config = container.GetRequiredService<IConfigurationManager>() as ConfigurationManager;
            config?.Initialize(configPath ?? GetDefaultConfigPath());

            // Initialize theme manager
            var themes = container.GetRequiredService<IThemeManager>() as ThemeManager;
            themes?.Initialize(themesPath);

            // Initialize security manager with strict mode (production default)
            var security = container.GetRequiredService<ISecurityManager>() as SecurityManager;
            security?.Initialize(SecurityMode.Strict);

            Logger.Instance.Info("DI", "✅ All services initialized");
        }

        private static string GetDefaultConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configDir = System.IO.Path.Combine(appData, "SuperTUI");
            System.IO.Directory.CreateDirectory(configDir);
            return System.IO.Path.Combine(configDir, "config.json");
        }
    }
}
