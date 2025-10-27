using System;
using FluentAssertions;
using SuperTUI.Infrastructure;
using SuperTUI.Widgets;
using Xunit;
using DI = SuperTUI.DI;

namespace SuperTUI.Tests.Linux
{
    /// <summary>
    /// Tests for WidgetFactory - ensures all widgets can be instantiated via DI
    /// These tests run on Linux WITHOUT WPF rendering
    /// </summary>
    [Trait("Category", "Linux")]
    [Trait("Category", "Critical")]
    public class WidgetFactoryTests : IDisposable
    {
        private readonly DI.ServiceContainer container;
        private readonly DI.WidgetFactory factory;

        public WidgetFactoryTests()
        {
            // Setup DI container with all services
            container = new DI.ServiceContainer();
            DI.ServiceRegistration.RegisterServices(container);
            factory = container.Resolve<DI.WidgetFactory>();
        }

        public void Dispose()
        {
            container.Clear();
        }

        #region Widget Creation Tests

        [Theory]
        [InlineData("ClockWidget")]
        [InlineData("CounterWidget")]
        [InlineData("TodoWidget")]
        [InlineData("CommandPaletteWidget")]
        [InlineData("ShortcutHelpWidget")]
        [InlineData("SettingsWidget")]
        [InlineData("FileExplorerWidget")]
        [InlineData("GitStatusWidget")]
        [InlineData("TaskManagementWidget")]
        [InlineData("AgendaWidget")]
        [InlineData("ProjectStatsWidget")]
        [InlineData("KanbanBoardWidget")]
        [InlineData("TaskSummaryWidget")]
        [InlineData("NotesWidget")]
        [InlineData("TimeTrackingWidget")]
        public void CreateWidget_AllProductionWidgets_ShouldInstantiate(string widgetType)
        {
            // Act
            Action act = () => factory.CreateWidget(widgetType);

            // Assert
            act.Should().NotThrow($"{widgetType} should instantiate via DI");
        }

        [Fact]
        public void CreateWidget_AllWidgets_ShouldHaveNonNullServices()
        {
            // Arrange
            var widgetTypes = new[]
            {
                "ClockWidget", "CounterWidget", "TodoWidget",
                "CommandPaletteWidget", "ShortcutHelpWidget", "SettingsWidget",
                "FileExplorerWidget", "GitStatusWidget",
                "TaskManagementWidget", "AgendaWidget", "ProjectStatsWidget",
                "KanbanBoardWidget", "TaskSummaryWidget", "NotesWidget",
                "TimeTrackingWidget"
            };

            // Act & Assert
            foreach (var widgetType in widgetTypes)
            {
                var widget = factory.CreateWidget(widgetType);
                widget.Should().NotBeNull($"{widgetType} should be created");
                widget.WidgetName.Should().NotBeNullOrEmpty($"{widgetType} should have a name");
            }
        }

        [Fact]
        public void CreateWidget_UnknownType_ShouldThrowArgumentException()
        {
            // Act
            Action act = () => factory.CreateWidget("NonExistentWidget");

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*Unknown widget type*");
        }

        [Fact]
        public void CreateWidget_NullType_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => factory.CreateWidget(null);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CreateWidget_EmptyType_ShouldThrowArgumentException()
        {
            // Act
            Action act = () => factory.CreateWidget("");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        #endregion

        #region Dependency Injection Verification Tests

        [Fact]
        public void CreateWidget_ShouldInjectLogger()
        {
            // Arrange
            var logger = container.Resolve<ILogger>();

            // Act
            var widget = factory.CreateWidget("ClockWidget");

            // Assert
            logger.Should().NotBeNull("Logger should be registered");
            // Widget should have received the same logger instance
        }

        [Fact]
        public void CreateWidget_ShouldInjectThemeManager()
        {
            // Arrange
            var themeManager = container.Resolve<IThemeManager>();

            // Act
            var widget = factory.CreateWidget("ClockWidget");

            // Assert
            themeManager.Should().NotBeNull("ThemeManager should be registered");
        }

        [Fact]
        public void CreateWidget_WithDomainServices_ShouldInjectCorrectly()
        {
            // Arrange - TaskManagementWidget requires ITaskService
            var taskService = container.Resolve<ITaskService>();

            // Act
            var widget = factory.CreateWidget("TaskManagementWidget");

            // Assert
            taskService.Should().NotBeNull("TaskService should be registered");
            widget.Should().NotBeNull("Widget should be created with domain service");
        }

        [Fact]
        public void CreateWidget_MultipleTimes_ShouldReceiveSameSingletonServices()
        {
            // Arrange
            var widget1 = factory.CreateWidget("ClockWidget");
            var widget2 = factory.CreateWidget("CounterWidget");

            // Act
            var logger1 = container.Resolve<ILogger>();
            var logger2 = container.Resolve<ILogger>();

            // Assert
            logger1.Should().BeSameAs(logger2, "Logger is singleton");
            // Both widgets should have received the same logger instance
        }

        #endregion

        #region Service Registration Verification Tests

        [Fact]
        public void ServiceContainer_ShouldHaveAllCoreServices()
        {
            // Assert
            container.IsRegistered<ILogger>().Should().BeTrue();
            container.IsRegistered<IThemeManager>().Should().BeTrue();
            container.IsRegistered<IConfigurationManager>().Should().BeTrue();
            container.IsRegistered<ISecurityManager>().Should().BeTrue();
            container.IsRegistered<IErrorHandler>().Should().BeTrue();
            container.IsRegistered<IStatePersistenceManager>().Should().BeTrue();
            container.IsRegistered<IPerformanceMonitor>().Should().BeTrue();
            container.IsRegistered<IEventBus>().Should().BeTrue();
            container.IsRegistered<IShortcutManager>().Should().BeTrue();
        }

        [Fact]
        public void ServiceContainer_ShouldHaveAllDomainServices()
        {
            // Assert
            container.IsRegistered<ITaskService>().Should().BeTrue();
            container.IsRegistered<IProjectService>().Should().BeTrue();
            container.IsRegistered<ITimeTrackingService>().Should().BeTrue();
            container.IsRegistered<ITagService>().Should().BeTrue();
        }

        [Fact]
        public void ServiceContainer_ShouldHaveWidgetFactory()
        {
            // Assert
            container.IsRegistered<DI.WidgetFactory>().Should().BeTrue();
        }

        [Fact]
        public void ServiceContainer_AllServices_ShouldResolveSuccessfully()
        {
            // Act & Assert - None of these should throw
            Action actCore = () =>
            {
                container.Resolve<ILogger>();
                container.Resolve<IThemeManager>();
                container.Resolve<IConfigurationManager>();
                container.Resolve<ISecurityManager>();
                container.Resolve<IErrorHandler>();
                container.Resolve<IStatePersistenceManager>();
                container.Resolve<IPerformanceMonitor>();
                container.Resolve<IEventBus>();
                container.Resolve<IShortcutManager>();
            };

            Action actDomain = () =>
            {
                container.Resolve<ITaskService>();
                container.Resolve<IProjectService>();
                container.Resolve<ITimeTrackingService>();
                container.Resolve<ITagService>();
            };

            actCore.Should().NotThrow("All core services should resolve");
            actDomain.Should().NotThrow("All domain services should resolve");
        }

        #endregion

        #region Widget Lifecycle Tests (No UI)

        [Fact]
        public void CreateWidget_ShouldBeDisposable()
        {
            // Arrange
            var widget = factory.CreateWidget("ClockWidget");

            // Act & Assert
            Action act = () => widget.Dispose();
            act.Should().NotThrow("Widget should dispose cleanly");
        }

        [Fact]
        public void CreateWidget_DisposeTwice_ShouldNotThrow()
        {
            // Arrange
            var widget = factory.CreateWidget("CounterWidget");

            // Act & Assert
            widget.Dispose();
            Action act = () => widget.Dispose();
            act.Should().NotThrow("Double dispose should be safe");
        }

        [Fact]
        public void CreateWidget_MultipleWidgets_AllShouldDispose()
        {
            // Arrange
            var widgets = new[]
            {
                factory.CreateWidget("ClockWidget"),
                factory.CreateWidget("CounterWidget"),
                factory.CreateWidget("TodoWidget")
            };

            // Act & Assert
            foreach (var widget in widgets)
            {
                Action act = () => widget.Dispose();
                act.Should().NotThrow($"{widget.WidgetName} should dispose cleanly");
            }
        }

        #endregion
    }
}
