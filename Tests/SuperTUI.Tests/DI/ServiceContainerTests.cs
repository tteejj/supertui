using System;
using FluentAssertions;
using SuperTUI.Core;
using Xunit;

namespace SuperTUI.Tests.DI
{
    public class ServiceContainerTests : IDisposable
    {
        private readonly ServiceContainer container;

        public ServiceContainerTests()
        {
            container = new ServiceContainer();
        }

        public void Dispose()
        {
            container.Clear();
        }

        #region Singleton Tests

        [Fact]
        public void RegisterSingleton_WithInstance_ReturnsSameInstance()
        {
            // Arrange
            var instance = new TestService();

            // Act
            container.RegisterSingleton<ITestService>(instance);
            var resolved1 = container.Resolve<ITestService>();
            var resolved2 = container.Resolve<ITestService>();

            // Assert
            resolved1.Should().BeSameAs(instance);
            resolved2.Should().BeSameAs(instance);
            resolved1.Should().BeSameAs(resolved2);
        }

        [Fact]
        public void RegisterSingleton_WithType_ReturnsSameInstance()
        {
            // Arrange & Act
            container.RegisterSingleton<ITestService, TestService>();
            var resolved1 = container.Resolve<ITestService>();
            var resolved2 = container.Resolve<ITestService>();

            // Assert
            resolved1.Should().NotBeNull();
            resolved1.Should().BeSameAs(resolved2);
        }

        [Fact]
        public void RegisterSingleton_WithFactory_ReturnsSameInstance()
        {
            // Arrange
            int callCount = 0;

            // Act
            container.RegisterSingleton<ITestService>(c =>
            {
                callCount++;
                return new TestService();
            });

            var resolved1 = container.Resolve<ITestService>();
            var resolved2 = container.Resolve<ITestService>();

            // Assert
            resolved1.Should().NotBeNull();
            resolved1.Should().BeSameAs(resolved2);
            callCount.Should().Be(1, "factory should only be called once for singleton");
        }

        #endregion

        #region Transient Tests

        [Fact]
        public void RegisterTransient_WithType_ReturnsDifferentInstances()
        {
            // Arrange & Act
            container.RegisterTransient<ITestService, TestService>();
            var resolved1 = container.Resolve<ITestService>();
            var resolved2 = container.Resolve<ITestService>();

            // Assert
            resolved1.Should().NotBeNull();
            resolved2.Should().NotBeNull();
            resolved1.Should().NotBeSameAs(resolved2);
        }

        [Fact]
        public void RegisterTransient_WithFactory_ReturnsDifferentInstances()
        {
            // Arrange
            int callCount = 0;

            // Act
            container.RegisterTransient<ITestService>(c =>
            {
                callCount++;
                return new TestService();
            });

            var resolved1 = container.Resolve<ITestService>();
            var resolved2 = container.Resolve<ITestService>();

            // Assert
            resolved1.Should().NotBeNull();
            resolved2.Should().NotBeNull();
            resolved1.Should().NotBeSameAs(resolved2);
            callCount.Should().Be(2, "factory should be called for each resolution");
        }

        #endregion

        #region Constructor Injection Tests

        [Fact]
        public void Resolve_WithDependencies_InjectsDependencies()
        {
            // Arrange
            container.RegisterSingleton<ITestService, TestService>();
            container.RegisterSingleton<ITestConsumer, TestConsumer>();

            // Act
            var consumer = container.Resolve<ITestConsumer>();

            // Assert
            consumer.Should().NotBeNull();
            consumer.Service.Should().NotBeNull();
            consumer.Service.Should().BeOfType<TestService>();
        }

        [Fact]
        public void Resolve_WithMultipleConstructors_ChoosesMostSpecific()
        {
            // Arrange
            container.RegisterSingleton<ITestService, TestService>();
            container.RegisterTransient<MultiConstructorService>();

            // Act
            var service = container.Resolve<MultiConstructorService>();

            // Assert
            service.Should().NotBeNull();
            service.UsedConstructorWithDI.Should().BeTrue("should use constructor with DI parameter");
        }

        [Fact]
        public void Resolve_WithMissingDependency_ThrowsException()
        {
            // Arrange - Register consumer but not its dependency
            container.RegisterSingleton<ITestConsumer, TestConsumer>();

            // Act & Assert
            Action act = () => container.Resolve<ITestConsumer>();
            act.Should().Throw<InvalidOperationException>();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void Resolve_UnregisteredService_ThrowsException()
        {
            // Act & Assert
            Action act = () => container.Resolve<ITestService>();
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*not registered*");
        }

        [Fact]
        public void TryResolve_UnregisteredService_ReturnsFalse()
        {
            // Act
            var result = container.TryResolve<ITestService>(out var service);

            // Assert
            result.Should().BeFalse();
            service.Should().BeNull();
        }

        [Fact]
        public void TryResolve_RegisteredService_ReturnsTrue()
        {
            // Arrange
            container.RegisterSingleton<ITestService, TestService>();

            // Act
            var result = container.TryResolve<ITestService>(out var service);

            // Assert
            result.Should().BeTrue();
            service.Should().NotBeNull();
        }

        #endregion

        #region Utility Tests

        [Fact]
        public void IsRegistered_WithRegisteredService_ReturnsTrue()
        {
            // Arrange
            container.RegisterSingleton<ITestService, TestService>();

            // Act & Assert
            container.IsRegistered<ITestService>().Should().BeTrue();
        }

        [Fact]
        public void IsRegistered_WithUnregisteredService_ReturnsFalse()
        {
            // Act & Assert
            container.IsRegistered<ITestService>().Should().BeFalse();
        }

        [Fact]
        public void Clear_RemovesAllRegistrations()
        {
            // Arrange
            container.RegisterSingleton<ITestService, TestService>();
            container.RegisterTransient<ITestConsumer, TestConsumer>();

            // Act
            container.Clear();

            // Assert
            container.IsRegistered<ITestService>().Should().BeFalse();
            container.IsRegistered<ITestConsumer>().Should().BeFalse();
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void Resolve_ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            container.RegisterSingleton<ITestService, TestService>();
            var tasks = new System.Threading.Tasks.Task[10];
            var instances = new ITestService[10];

            // Act - Resolve from multiple threads
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    instances[index] = container.Resolve<ITestService>();
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);

            // Assert - All should be same instance (singleton)
            for (int i = 1; i < instances.Length; i++)
            {
                instances[i].Should().BeSameAs(instances[0]);
            }
        }

        #endregion

        #region Test Classes

        public interface ITestService
        {
            void DoSomething();
        }

        public class TestService : ITestService
        {
            public void DoSomething() { }
        }

        public interface ITestConsumer
        {
            ITestService Service { get; }
        }

        public class TestConsumer : ITestConsumer
        {
            public ITestService Service { get; }

            public TestConsumer(ITestService service)
            {
                Service = service;
            }
        }

        public class MultiConstructorService
        {
            public bool UsedConstructorWithDI { get; }

            // Parameterless constructor
            public MultiConstructorService()
            {
                UsedConstructorWithDI = false;
            }

            // Constructor with DI
            public MultiConstructorService(ITestService service)
            {
                UsedConstructorWithDI = true;
            }
        }

        #endregion
    }
}
