using System;
using SuperTUI.Core;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;
using SuperTUI.Extensions;

namespace SuperTUI.DI
{
    /// <summary>
    /// Helper class to configure dependency injection
    /// Call RegisterAllServices() at application startup
    ///
    /// PHASE 3: Maximum DI Migration (2025-10-25)
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// One-shot method to register and initialize all services
        /// This is the main entry point for DI setup
        /// Returns the configured ServiceContainer
        /// </summary>
        public static ServiceContainer RegisterAllServices(string configPath = null, string themesPath = null)
        {
            var container = new ServiceContainer();
            ConfigureServices(container);
            InitializeServices(container, configPath, themesPath);
            return container;
        }

        /// <summary>
        /// Configure all services for dependency injection
        /// PHASE 3: Maximum DI - Register all infrastructure and domain services
        /// </summary>
        public static void ConfigureServices(ServiceContainer container)
        {
            Logger.Instance.Info("DI", "🔧 Configuring services for dependency injection...");

            // PHASE 3: Register existing infrastructure singletons with their interfaces
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

            // Register domain services with their interfaces
            container.RegisterSingleton<ITaskService, Core.Services.TaskService>(Core.Services.TaskService.Instance);
            container.RegisterSingleton<IProjectService, Core.Services.ProjectService>(Core.Services.ProjectService.Instance);
            container.RegisterSingleton<ITimeTrackingService, Core.Services.TimeTrackingService>(Core.Services.TimeTrackingService.Instance);
            container.RegisterSingleton<IExcelMappingService, Core.Services.ExcelMappingService>(Core.Services.ExcelMappingService.Instance);
            container.RegisterSingleton<ITagService, Core.Services.TagService>(Core.Services.TagService.Instance);

            Logger.Instance.Info("DI", $"✅ Registered {5} domain services");
        }

        /// <summary>
        /// Initialize all singleton services with proper dependency order
        /// Call this after ConfigureServices()
        /// PHASE 3: Initialize via DI container with dependency order enforcement
        ///
        /// INITIALIZATION ORDER:
        /// 1. Logger (no dependencies - already initialized via singleton)
        /// 2. ConfigurationManager (depends on Logger only)
        /// 3. SecurityManager (validates paths from config)
        /// 4. ThemeManager (uses config for theme settings)
        /// 5. Domain services (depend on config for data paths)
        /// </summary>
        public static void InitializeServices(ServiceContainer container, string configPath = null, string themesPath = null)
        {
            Logger.Instance.Info("DI", "🚀 Initializing services in dependency order...");

            // STEP 1: Logger is already initialized (singleton pattern)
            // No action needed - Logger.Instance is ready
            Logger.Instance.Info("DI", "✅ Logger ready (singleton)");

            // STEP 2: Initialize ConfigurationManager FIRST
            // Other services depend on configuration being available
            var config = container.GetRequiredService<IConfigurationManager>() as ConfigurationManager;
            if (config == null)
            {
                throw new InvalidOperationException(
                    "ConfigurationManager not registered in service container. " +
                    "Ensure ConfigureServices() was called before InitializeServices().");
            }

            config.Initialize(configPath ?? GetDefaultConfigPath());

            // CRITICAL CHECK: Verify ConfigurationManager is actually initialized
            if (!config.IsInitialized)
            {
                throw new InvalidOperationException(
                    "ConfigurationManager.Initialize() completed but IsInitialized is false. " +
                    "This indicates a critical initialization failure. Check logs for errors.");
            }
            Logger.Instance.Info("DI", "✅ ConfigurationManager initialized and verified");

            // STEP 3: Initialize SecurityManager SECOND
            // SecurityManager validates paths that may come from configuration
            var security = container.GetRequiredService<ISecurityManager>() as SecurityManager;
            if (security == null)
            {
                throw new InvalidOperationException("SecurityManager not registered in service container.");
            }

            security.Initialize(SecurityMode.Strict);
            Logger.Instance.Info("DI", "✅ SecurityManager initialized (Strict mode)");

            // STEP 4: Initialize ThemeManager THIRD
            // ThemeManager may use configuration for theme settings
            var themes = container.GetRequiredService<IThemeManager>() as ThemeManager;
            if (themes == null)
            {
                throw new InvalidOperationException("ThemeManager not registered in service container.");
            }

            themes.Initialize(themesPath);
            Logger.Instance.Info("DI", "✅ ThemeManager initialized");

            // STEP 5: Initialize domain services LAST
            // Domain services use GetSuperTUIDataDirectory() which may be influenced by config
            // All domain services depend on ConfigurationManager being fully initialized

            Logger.Instance.Info("DI", "Initializing domain services (depend on ConfigurationManager)...");

            var taskService = container.GetRequiredService<ITaskService>();
            if (taskService == null)
            {
                throw new InvalidOperationException("ITaskService not registered in service container.");
            }
            taskService.Initialize();
            Logger.Instance.Info("DI", "✅ TaskService initialized");

            var projectService = container.GetRequiredService<IProjectService>();
            if (projectService == null)
            {
                throw new InvalidOperationException("IProjectService not registered in service container.");
            }
            projectService.Initialize();
            Logger.Instance.Info("DI", "✅ ProjectService initialized");

            var timeTrackingService = container.GetRequiredService<ITimeTrackingService>();
            if (timeTrackingService == null)
            {
                throw new InvalidOperationException("ITimeTrackingService not registered in service container.");
            }
            timeTrackingService.Initialize();
            Logger.Instance.Info("DI", "✅ TimeTrackingService initialized");

            var excelMappingService = container.GetRequiredService<IExcelMappingService>();
            if (excelMappingService == null)
            {
                throw new InvalidOperationException("IExcelMappingService not registered in service container.");
            }
            excelMappingService.Initialize();
            Logger.Instance.Info("DI", "✅ ExcelMappingService initialized");

            var tagService = container.GetRequiredService<ITagService>();
            if (tagService == null)
            {
                throw new InvalidOperationException("ITagService not registered in service container.");
            }
            // TagService doesn't have Initialize method - it's ready to use immediately
            Logger.Instance.Info("DI", "✅ TagService ready (no initialization needed)");

            Logger.Instance.Info("DI", "✅ All services initialized successfully in proper dependency order");
        }

        private static string GetDefaultConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configDir = System.IO.Path.Combine(appData, "SuperTUI");
            configDir = Extensions.DirectoryHelper.CreateDirectoryWithFallback(configDir, "Config");
            return System.IO.Path.Combine(configDir, "config.json");
        }
    }
}
