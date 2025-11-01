using System;
using System.Linq;
using FluentAssertions;
using SuperTUI.Infrastructure;
using Xunit;


namespace SuperTUI.Tests.DI
{
    /// <summary>
    /// Tests for ServiceContainer - the core DI container
    /// Tests singleton, transient, and scoped lifetimes
    /// CRITICAL: These tests validate the foundation of the entire DI system
    /// </summary>
    [Trait("Category", "Linux")]
    [Trait("Category", "Critical")]
    [Trait("Priority", "High")]
    public class ServiceContainerTests : IDisposable
    {
        private SuperTUI.DI.ServiceContainer container;

        public ServiceContainerTests()
        {
            container = new SuperTUI.DI.ServiceContainer();
        }

        public void Dispose()
        {
            container?.Dispose();
        }

        #region Singleton Registration Tests

        [Fact]
        public void RegisterSingleton_WithInstance_ShouldSucceed()
        {
            // Arrange
            var logger = Logger.Instance;

            // Act
            container.RegisterSingleton<ILogger, Logger>(logger);

            // Assert
            container.IsRegistered<ILogger>().Should().BeTrue();
        }

        [Fact]
        public void RegisterSingleton_WithFactory_ShouldSucceed()
        {
            // Act
            container.RegisterSingleton<ILogger>(sp => Logger.Instance);

            // Assert
            container.IsRegistered<ILogger>().Should().BeTrue();
        }

        [Fact]
        public void GetService_Singleton_ShouldReturnSameInstance()
        {
            // Arrange
            container.RegisterSingleton<ILogger, Logger>(Logger.Instance);

            // Act
            var instance1 = container.GetService<ILogger>();
            var instance2 = container.GetService<ILogger>();

            // Assert
            instance1.Should().NotBeNull();
            instance2.Should().NotBeNull();
            instance1.Should().BeSameAs(instance2, "Singleton should return same instance");
        }

        #endregion

        #region Transient Registration Tests

        [Fact]
        public void RegisterTransient_ShouldSucceed()
        {
            // Act
            container.RegisterTransient<TestTransientService, TestTransientService>();

            // Assert
            container.IsRegistered<TestTransientService>().Should().BeTrue();
        }

        [Fact]
        public void GetService_Transient_ShouldReturnNewInstance()
        {
            // Arrange
            container.RegisterTransient<TestTransientService, TestTransientService>();

            // Act
            var instance1 = container.GetService<TestTransientService>();
            var instance2 = container.GetService<TestTransientService>();

            // Assert
            instance1.Should().NotBeNull();
            instance2.Should().NotBeNull();
            instance1.Should().NotBeSameAs(instance2, "Transient should return different instances");
        }

        [Fact]
        public void RegisterTransient_WithFactory_ShouldSucceed()
        {
            // Arrange
            int callCount = 0;

            // Act
            container.RegisterTransient<TestTransientService>(sp =>
            {
                callCount++;
                return new TestTransientService();
            });

            var instance1 = container.GetService<TestTransientService>();
            var instance2 = container.GetService<TestTransientService>();

            // Assert
            callCount.Should().Be(2, "Factory should be called for each resolution");
            instance1.Should().NotBeSameAs(instance2);
        }

        #endregion

        #region Scoped Registration Tests

        [Fact]
        public void RegisterScoped_ShouldSucceed()
        {
            // Act
            container.RegisterScoped<TestScopedService, TestScopedService>();

            // Assert
            container.IsRegistered<TestScopedService>().Should().BeTrue();
        }

        [Fact]
        public void GetService_Scoped_FromRootContainer_ShouldThrow()
        {
            // Arrange
            container.RegisterScoped<TestScopedService, TestScopedService>();

            // Act
            Action act = () => container.GetService<TestScopedService>();

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*scoped service*");
        }

        [Fact]
        public void GetService_Scoped_WithinScope_ShouldReturnSameInstance()
        {
            // Arrange
            container.RegisterScoped<TestScopedService, TestScopedService>();

            // Act
            using (var scope = container.CreateScope())
            {
                var instance1 = scope.ServiceProvider.GetService<TestScopedService>();
                var instance2 = scope.ServiceProvider.GetService<TestScopedService>();

                // Assert
                instance1.Should().NotBeNull();
                instance2.Should().NotBeNull();
                instance1.Should().BeSameAs(instance2, "Scoped should return same instance within scope");
            }
        }

        [Fact]
        public void GetService_Scoped_DifferentScopes_ShouldReturnDifferentInstances()
        {
            // Arrange
            container.RegisterScoped<TestScopedService, TestScopedService>();

            // Act
            TestScopedService instance1, instance2;
            using (var scope1 = container.CreateScope())
            {
                instance1 = scope1.ServiceProvider.GetService<TestScopedService>();
            }
            using (var scope2 = container.CreateScope())
            {
                instance2 = scope2.ServiceProvider.GetService<TestScopedService>();
            }

            // Assert
            instance1.Should().NotBeNull();
            instance2.Should().NotBeNull();
            instance1.Should().NotBeSameAs(instance2, "Different scopes should have different instances");
        }

        #endregion

        #region Service Resolution Tests

        [Fact]
        public void GetService_UnregisteredService_ShouldReturnNull()
        {
            // Act
            var service = container.GetService<ILogger>();

            // Assert
            service.Should().BeNull();
        }

        [Fact]
        public void GetRequiredService_UnregisteredService_ShouldThrow()
        {
            // Act
            Action act = () => container.GetRequiredService<ILogger>();

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*not registered*");
        }

        [Fact]
        public void GetRequiredService_RegisteredService_ShouldReturn()
        {
            // Arrange
            container.RegisterSingleton<ILogger, Logger>(Logger.Instance);

            // Act
            var service = container.GetRequiredService<ILogger>();

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region Container Lock Tests

        [Fact]
        public void Lock_ShouldPreventFurtherRegistrations()
        {
            // Arrange
            container.Lock();

            // Act
            Action act = () => container.RegisterSingleton<ILogger, Logger>(Logger.Instance);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*locked*");
        }

        [Fact]
        public void Lock_AfterRegistration_ShouldStillAllowResolution()
        {
            // Arrange
            container.RegisterSingleton<ILogger, Logger>(Logger.Instance);
            container.Lock();

            // Act
            var service = container.GetService<ILogger>();

            // Assert
            service.Should().NotBeNull();
        }

        [Fact]
        public void Lock_CalledTwice_ShouldNotThrow()
        {
            // Act
            container.Lock();
            Action act = () => container.Lock();

            // Assert
            act.Should().NotThrow();
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public void Dispose_ShouldCleanupSingletons()
        {
            // Arrange
            var disposableService = new TestDisposableService();
            container.RegisterSingleton<TestDisposableService, TestDisposableService>(disposableService);

            // Act
            container.Dispose();

            // Assert
            disposableService.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void Dispose_ScopedService_ShouldCleanup()
        {
            // Arrange
            container.RegisterScoped<TestDisposableScopedService, TestDisposableScopedService>();
            TestDisposableScopedService scopedInstance = null;

            using (var scope = container.CreateScope())
            {
                scopedInstance = scope.ServiceProvider.GetService<TestDisposableScopedService>();
                scopedInstance.Should().NotBeNull();
            }

            // Assert
            scopedInstance.IsDisposed.Should().BeTrue("Scoped service should be disposed when scope is disposed");
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void GetService_Singleton_ConcurrentAccess_ShouldReturnSameInstance()
        {
            // Arrange
            container.RegisterSingleton<ILogger>(sp => Logger.Instance);
            var instances = new System.Collections.Concurrent.ConcurrentBag<ILogger>();

            // Act - simulate concurrent access
            System.Threading.Tasks.Parallel.For(0, 100, i =>
            {
                var instance = container.GetService<ILogger>();
                instances.Add(instance);
            });

            // Assert
            instances.Should().HaveCount(100);
            instances.Distinct().Should().HaveCount(1, "All concurrent resolutions should return same singleton instance");
        }

        #endregion

        #region Test Helper Classes

        public class TestTransientService
        {
            public Guid Id { get; } = Guid.NewGuid();
        }

        public class TestScopedService
        {
            public Guid Id { get; } = Guid.NewGuid();
        }

        public class TestDisposableService : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        public class TestDisposableScopedService : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        #endregion
    }
}
