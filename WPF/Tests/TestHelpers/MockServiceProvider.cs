using System;
using SuperTUI.Core.Commands;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Interfaces;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;


namespace SuperTUI.Tests.TestHelpers
{
    /// <summary>
    /// Helper to create test service containers with mock or real services
    /// Simplifies test setup and ensures consistent service configuration
    /// </summary>
    public static class MockServiceProvider
    {
        /// <summary>
        /// Create a fully configured test container with all services
        /// </summary>
        public static SuperTUI.DI.ServiceContainer CreateTestContainer()
        {
            var container = new SuperTUI.DI.ServiceContainer();

            // Register infrastructure services (use real singletons for tests)
            container.RegisterSingleton<ILogger, Logger>(Logger.Instance);
            container.RegisterSingleton<IConfigurationManager, ConfigurationManager>(ConfigurationManager.Instance);
            container.RegisterSingleton<IThemeManager, MockThemeManager>(new MockThemeManager());
            container.RegisterSingleton<ISecurityManager, SecurityManager>(SecurityManager.Instance);
            container.RegisterSingleton<IErrorHandler, ErrorHandler>(ErrorHandler.Instance);
            // Use mock implementations for services that are internal or excluded from test builds
            container.RegisterSingleton<SuperTUI.Core.IEventBus, MockEventBus>(new MockEventBus());
            container.RegisterSingleton<IShortcutManager, MockShortcutManager>(new MockShortcutManager());
            container.RegisterSingleton<IProjectContextManager, ProjectContextManager>(ProjectContextManager.Instance);
            container.RegisterSingleton<INotificationManager, NotificationManager>(NotificationManager.Instance);

            // Register CommandHistory
            container.RegisterSingleton<CommandHistory>(sp => new CommandHistory(Logger.Instance, maxHistorySize: 50));

            // Register FocusHistoryManager
            container.RegisterSingleton<FocusHistoryManager>(sp => new FocusHistoryManager(Logger.Instance));

            // Register domain services
            container.RegisterSingleton<ITaskService, TaskService>(TaskService.Instance);
            container.RegisterSingleton<IProjectService, ProjectService>(ProjectService.Instance);
            container.RegisterSingleton<ITimeTrackingService, TimeTrackingService>(TimeTrackingService.Instance);
            // Note: ExcelMappingService excluded from test builds
            // container.RegisterSingleton<IExcelMappingService, ExcelMappingService>(ExcelMappingService.Instance);
            container.RegisterSingleton<ITagService, TagService>(TagService.Instance);

            // Register PaneFactory
            container.RegisterSingleton<PaneFactory>(sp => new PaneFactory(
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<IThemeManager>(),
                sp.GetRequiredService<IProjectContextManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<ISecurityManager>(),
                sp.GetRequiredService<IShortcutManager>(),
                sp.GetRequiredService<ITaskService>(),
                sp.GetRequiredService<IProjectService>(),
                sp.GetRequiredService<ITimeTrackingService>(),
                sp.GetRequiredService<ITagService>(),
                sp.GetRequiredService<SuperTUI.Core.IEventBus>(),
                sp.GetRequiredService<CommandHistory>(),
                sp.GetRequiredService<FocusHistoryManager>()
            ));

            return container;
        }

        /// <summary>
        /// Initialize services in the container (minimal initialization for tests)
        /// </summary>
        public static void InitializeTestServices(SuperTUI.DI.ServiceContainer container)
        {
            // Initialize ConfigurationManager with a test config path
            var config = container.GetRequiredService<IConfigurationManager>() as ConfigurationManager;
            if (config != null && !config.IsInitialized)
            {
                var testConfigPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "SuperTUI_TestConfig",
                    "config.json");
                config.Initialize(testConfigPath);
            }

            // Initialize SecurityManager in permissive mode for tests
            var security = container.GetRequiredService<ISecurityManager>() as SecurityManager;
            if (security != null)
            {
                // Reset for testing to allow re-initialization (DEBUG builds only)
                // Use lock to prevent race conditions during parallel test execution
                security.ResetForTesting();
                try
                {
                    security.Initialize(SecurityMode.Development);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("already initialized"))
                {
                    // Another test already initialized it - this is expected in parallel test execution
                    // The SecurityManager is a singleton, so this is safe to ignore
                }
            }

            // Initialize ThemeManager (MockThemeManager is safe for tests)
            var themes = container.GetRequiredService<IThemeManager>();
            themes?.Initialize(null); // MockThemeManager handles this safely

            // Initialize domain services
            var taskService = container.GetRequiredService<ITaskService>();
            if (taskService != null)
            {
                taskService.Initialize();
            }

            var projectService = container.GetRequiredService<IProjectService>();
            if (projectService != null)
            {
                projectService.Initialize();
            }

            var timeTrackingService = container.GetRequiredService<ITimeTrackingService>();
            if (timeTrackingService != null)
            {
                timeTrackingService.Initialize();
            }

            // ExcelMappingService excluded from test builds
            // var excelMappingService = container.GetRequiredService<IExcelMappingService>();
            // if (excelMappingService != null)
            // {
            //     excelMappingService.Initialize();
            // }
        }

        /// <summary>
        /// Create and initialize a test container in one call
        /// </summary>
        public static SuperTUI.DI.ServiceContainer CreateAndInitializeTestContainer()
        {
            var container = CreateTestContainer();
            InitializeTestServices(container);
            return container;
        }
    }
}
