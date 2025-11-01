using System;
using SuperTUI.Core;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;
using Xunit;


namespace SuperTUI.Tests.TestHelpers
{
    /// <summary>
    /// Base class for pane tests with common setup and teardown
    /// Provides access to all services needed for pane testing
    /// </summary>
    public abstract class PaneTestBase : IDisposable
    {
        protected SuperTUI.DI.ServiceContainer Container { get; private set; }

        // Infrastructure services
        protected ILogger Logger { get; private set; }
        protected IThemeManager ThemeManager { get; private set; }
        protected IConfigurationManager ConfigManager { get; private set; }
        protected IProjectContextManager ProjectContext { get; private set; }
        protected ISecurityManager SecurityManager { get; private set; }
        protected IEventBus EventBus { get; private set; }

        // Domain services
        protected ITaskService TaskService { get; private set; }
        protected IProjectService ProjectService { get; private set; }
        protected ITimeTrackingService TimeTrackingService { get; private set; }
        protected ITagService TagService { get; private set; }

        // Factories
        protected PaneFactory PaneFactory { get; private set; }

        protected PaneTestBase()
        {
            // Create and initialize container
            Container = MockServiceProvider.CreateAndInitializeTestContainer();

            // Resolve commonly used services
            Logger = Container.GetRequiredService<ILogger>();
            ThemeManager = Container.GetRequiredService<IThemeManager>();
            ConfigManager = Container.GetRequiredService<IConfigurationManager>();
            ProjectContext = Container.GetRequiredService<IProjectContextManager>();
            SecurityManager = Container.GetRequiredService<ISecurityManager>();
            EventBus = Container.GetRequiredService<IEventBus>();

            TaskService = Container.GetRequiredService<ITaskService>();
            ProjectService = Container.GetRequiredService<IProjectService>();
            TimeTrackingService = Container.GetRequiredService<ITimeTrackingService>();
            TagService = Container.GetRequiredService<ITagService>();

            PaneFactory = Container.GetRequiredService<PaneFactory>();
        }

        public virtual void Dispose()
        {
            // Container disposal will clean up all registered services
            Container?.Dispose();
        }
    }
}
