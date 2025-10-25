using System;
using Microsoft.Extensions.DependencyInjection;
using SuperTUI.Infrastructure;
using SuperTUI.Core;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Widgets;

namespace SuperTUI.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring SuperTUI services in the DI container
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add all SuperTUI infrastructure services to the service collection
        /// </summary>
        public static IServiceCollection AddSuperTUIServices(this IServiceCollection services)
        {
            // Register infrastructure services as singletons
            services.AddSingleton<ILogger>(Logger.Instance);
            services.AddSingleton<IConfigurationManager>(ConfigurationManager.Instance);
            services.AddSingleton<IThemeManager>(ThemeManager.Instance);
            services.AddSingleton<ISecurityManager>(SecurityManager.Instance);
            services.AddSingleton<IStatePersistenceManager>(StatePersistenceManager.Instance);
            services.AddSingleton<IPluginManager>(PluginManager.Instance);
            services.AddSingleton<IPerformanceMonitor>(PerformanceMonitor.Instance);
            services.AddSingleton<IShortcutManager>(ShortcutManager.Instance);
            services.AddSingleton<IHotReloadManager>(HotReloadManager.Instance);
            services.AddSingleton<IEventBus>(EventBus.Instance);
            services.AddSingleton<IWorkspaceManager>(WorkspaceManager.Instance);

            return services;
        }

        /// <summary>
        /// Add all SuperTUI widgets to the service collection as transient services
        /// </summary>
        public static IServiceCollection AddSuperTUIWidgets(this IServiceCollection services)
        {
            // Register widgets as transient (new instance each time)
            services.AddTransient<ClockWidget>();
            services.AddTransient<CounterWidget>();
            services.AddTransient<FileExplorerWidget>();
            services.AddTransient<TodoWidget>();
            services.AddTransient<TaskManagementWidget>();
            services.AddTransient<TerminalWidget>();
            services.AddTransient<SystemMonitorWidget>();
            services.AddTransient<CommandPaletteWidget>();
            services.AddTransient<NotesWidget>();
            services.AddTransient<TaskSummaryWidget>();
            services.AddTransient<WidgetPickerWidget>();

            return services;
        }

        /// <summary>
        /// Add all SuperTUI services and widgets in one call
        /// </summary>
        public static IServiceCollection AddSuperTUI(this IServiceCollection services)
        {
            return services
                .AddSuperTUIServices()
                .AddSuperTUIWidgets();
        }
    }

    /// <summary>
    /// Factory for creating the SuperTUI service provider
    /// </summary>
    public static class ServiceProviderFactory
    {
        /// <summary>
        /// Create a fully configured service provider for SuperTUI
        /// </summary>
        /// <param name="additionalConfiguration">Optional additional service configuration</param>
        public static IServiceProvider CreateServiceProvider(Action<IServiceCollection> additionalConfiguration = null)
        {
            var services = new ServiceCollection();

            // Add SuperTUI services
            services.AddSuperTUI();

            // Allow caller to add additional services
            additionalConfiguration?.Invoke(services);

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Create a service provider with custom services (for testing)
        /// </summary>
        public static IServiceProvider CreateTestServiceProvider(
            ILogger logger = null,
            IThemeManager themeManager = null,
            IConfigurationManager configManager = null)
        {
            var services = new ServiceCollection();

            // Use provided instances or defaults
            services.AddSingleton<ILogger>(logger ?? Logger.Instance);
            services.AddSingleton<IThemeManager>(themeManager ?? ThemeManager.Instance);
            services.AddSingleton<IConfigurationManager>(configManager ?? ConfigurationManager.Instance);
            services.AddSingleton<ISecurityManager>(SecurityManager.Instance);
            services.AddSingleton<IStatePersistenceManager>(StatePersistenceManager.Instance);
            services.AddSingleton<IPluginManager>(PluginManager.Instance);
            services.AddSingleton<IPerformanceMonitor>(PerformanceMonitor.Instance);
            services.AddSingleton<IShortcutManager>(ShortcutManager.Instance);
            services.AddSingleton<IHotReloadManager>(HotReloadManager.Instance);
            services.AddSingleton<IEventBus>(EventBus.Instance);
            services.AddSingleton<IWorkspaceManager>(WorkspaceManager.Instance);

            // Add widgets
            services.AddSuperTUIWidgets();

            return services.BuildServiceProvider();
        }
    }
}
