using System;
using System.Collections.Generic;
using SuperTUI.Infrastructure;

namespace SuperTUI.DI
{
    // ============================================================================
    // MAXIMUM DI IMPLEMENTATION
    // ============================================================================
    //
    // Zero-dependency, security-hardened dependency injection container.
    // Built from scratch - no external packages required.
    //
    // SECURITY FEATURES:
    //   - Immutable after Lock() - prevents plugin tampering
    //   - Audit logging of all registrations
    //   - Explicit registration only (no reflection scanning)
    //   - Service isolation between scopes
    //   - Lifetime management (singleton, transient, scoped)
    //
    // PHASE 3: Maximum DI Migration (2025-10-25)
    // ============================================================================

    /// <summary>
    /// Service lifetime determines how instances are created and managed
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>
        /// Single instance shared across the entire application
        /// </summary>
        Singleton,

        /// <summary>
        /// New instance created every time the service is requested
        /// </summary>
        Transient,

        /// <summary>
        /// Single instance per scope (e.g., per workspace, per request)
        /// </summary>
        Scoped
    }

    /// <summary>
    /// Service descriptor containing registration information
    /// </summary>
    internal class ServiceDescriptor
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public object Instance { get; set; }
        public Func<IServiceProvider, object> Factory { get; set; }
        public ServiceLifetime Lifetime { get; set; }
    }

    /// <summary>
    /// Service provider interface for resolving dependencies
    /// </summary>
    public interface IServiceProvider
    {
        T GetService<T>();
        object GetService(Type serviceType);
        T GetRequiredService<T>();
        object GetRequiredService(Type serviceType);
    }

    /// <summary>
    /// Service scope for managing scoped service lifetimes
    /// </summary>
    public interface IServiceScope : IDisposable
    {
        IServiceProvider ServiceProvider { get; }
    }

    /// <summary>
    /// Maximum DI container with security features and lifetime management
    ///
    /// SECURITY FEATURES:
    /// - Lock() prevents modifications after startup
    /// - Audit logging for all registrations
    /// - No reflection-based auto-discovery
    /// - Isolated scopes prevent cross-contamination
    /// </summary>
    public class ServiceContainer : IServiceProvider, IDisposable
    {
        private readonly Dictionary<Type, ServiceDescriptor> services = new Dictionary<Type, ServiceDescriptor>();
        private readonly Dictionary<Type, object> singletonInstances = new Dictionary<Type, object>();
        private readonly object lockObject = new object();
        private bool isLocked = false;
        private bool disposed = false;

        /// <summary>
        /// Register a singleton service with an existing instance
        /// </summary>
        public void RegisterSingleton<TService, TImplementation>(TImplementation instance)
            where TImplementation : class, TService
        {
            ThrowIfLocked();

            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationType = typeof(TImplementation),
                    Instance = instance,
                    Lifetime = ServiceLifetime.Singleton
                };

                services[typeof(TService)] = descriptor;
                singletonInstances[typeof(TService)] = instance;

                Logger.Instance.Info("DI", $"Registered singleton: {typeof(TService).Name} -> {typeof(TImplementation).Name}");
            }
        }

        /// <summary>
        /// Register a singleton service with a factory function
        /// </summary>
        public void RegisterSingleton<TService>(Func<IServiceProvider, TService> factory)
            where TService : class
        {
            ThrowIfLocked();

            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    Factory = provider => factory(provider),
                    Lifetime = ServiceLifetime.Singleton
                };

                services[typeof(TService)] = descriptor;

                Logger.Instance.Info("DI", $"Registered singleton factory: {typeof(TService).Name}");
            }
        }

        /// <summary>
        /// Register a transient service (new instance each time)
        /// </summary>
        public void RegisterTransient<TService, TImplementation>()
            where TImplementation : class, TService, new()
        {
            ThrowIfLocked();

            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationType = typeof(TImplementation),
                    Lifetime = ServiceLifetime.Transient
                };

                services[typeof(TService)] = descriptor;

                Logger.Instance.Info("DI", $"Registered transient: {typeof(TService).Name} -> {typeof(TImplementation).Name}");
            }
        }

        /// <summary>
        /// Register a transient service with a factory function
        /// </summary>
        public void RegisterTransient<TService>(Func<IServiceProvider, TService> factory)
            where TService : class
        {
            ThrowIfLocked();

            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    Factory = provider => factory(provider),
                    Lifetime = ServiceLifetime.Transient
                };

                services[typeof(TService)] = descriptor;

                Logger.Instance.Info("DI", $"Registered transient factory: {typeof(TService).Name}");
            }
        }

        /// <summary>
        /// Register a scoped service (one instance per scope)
        /// </summary>
        public void RegisterScoped<TService, TImplementation>()
            where TImplementation : class, TService, new()
        {
            ThrowIfLocked();

            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationType = typeof(TImplementation),
                    Lifetime = ServiceLifetime.Scoped
                };

                services[typeof(TService)] = descriptor;

                Logger.Instance.Info("DI", $"Registered scoped: {typeof(TService).Name} -> {typeof(TImplementation).Name}");
            }
        }

        /// <summary>
        /// Lock the container to prevent further registrations
        /// SECURITY: Call this after startup to prevent plugin tampering
        /// </summary>
        public void Lock()
        {
            lock (lockObject)
            {
                if (isLocked) return;

                isLocked = true;
                Logger.Instance.Info("DI", "🔒 Service container LOCKED - no more registrations allowed");
            }
        }

        /// <summary>
        /// Check if service is registered
        /// </summary>
        public bool IsRegistered<TService>()
        {
            return services.ContainsKey(typeof(TService));
        }

        /// <summary>
        /// Get service (returns null if not found)
        /// </summary>
        public T GetService<T>()
        {
            return (T)GetService(typeof(T));
        }

        /// <summary>
        /// Get service (returns null if not found)
        /// </summary>
        public object GetService(Type serviceType)
        {
            ServiceDescriptor descriptor;

            // Only lock for descriptor lookup (minimal lock scope)
            lock (lockObject)
            {
                if (!services.TryGetValue(serviceType, out descriptor))
                {
                    return null;
                }
            }

            // Resolve outside of lock - ResolveService handles its own locking for singletons
            return ResolveService(descriptor, this);
        }

        /// <summary>
        /// Get required service (throws if not found)
        /// </summary>
        public T GetRequiredService<T>()
        {
            return (T)GetRequiredService(typeof(T));
        }

        /// <summary>
        /// Get required service (throws if not found)
        /// </summary>
        public object GetRequiredService(Type serviceType)
        {
            var service = GetService(serviceType);
            if (service == null)
            {
                throw new InvalidOperationException(
                    $"Service not registered: {serviceType.Name}. " +
                    $"Did you forget to register it in the container?");
            }
            return service;
        }

        /// <summary>
        /// Create a new service scope
        /// </summary>
        public IServiceScope CreateScope()
        {
            return new ServiceScope(this);
        }

        private object ResolveService(ServiceDescriptor descriptor, IServiceProvider provider)
        {
            switch (descriptor.Lifetime)
            {
                case ServiceLifetime.Singleton:
                    // THREAD-SAFE DOUBLE-CHECKED LOCKING PATTERN
                    // First check (fast path - no lock required)
                    if (singletonInstances.TryGetValue(descriptor.ServiceType, out var existingInstance))
                    {
                        return existingInstance;
                    }

                    // Acquire lock for singleton creation
                    lock (lockObject)
                    {
                        // Second check (another thread may have created it while we waited for lock)
                        if (singletonInstances.TryGetValue(descriptor.ServiceType, out existingInstance))
                        {
                            return existingInstance;
                        }

                        // Create new singleton (only one thread reaches here)
                        object instance;
                        if (descriptor.Instance != null)
                        {
                            instance = descriptor.Instance;
                        }
                        else if (descriptor.Factory != null)
                        {
                            instance = descriptor.Factory(provider);
                        }
                        else
                        {
                            instance = Activator.CreateInstance(descriptor.ImplementationType);
                        }

                        // Add to dictionary inside lock (thread-safe)
                        singletonInstances[descriptor.ServiceType] = instance;
                        return instance;
                    }

                case ServiceLifetime.Transient:
                    // Always create new instance
                    if (descriptor.Factory != null)
                    {
                        return descriptor.Factory(provider);
                    }
                    else
                    {
                        return Activator.CreateInstance(descriptor.ImplementationType);
                    }

                case ServiceLifetime.Scoped:
                    // Scoped services are handled by ServiceScope
                    throw new InvalidOperationException(
                        $"Cannot resolve scoped service {descriptor.ServiceType.Name} from root container. " +
                        $"Use CreateScope() and resolve from the scope instead.");

                default:
                    throw new NotSupportedException($"Unsupported lifetime: {descriptor.Lifetime}");
            }
        }

        private void ThrowIfLocked()
        {
            if (isLocked)
            {
                throw new InvalidOperationException(
                    "Service container is locked. No more registrations allowed. " +
                    "This is a security feature to prevent plugins from tampering with services.");
            }
        }

        public void Dispose()
        {
            if (disposed) return;

            lock (lockObject)
            {
                // Dispose all singleton instances that implement IDisposable
                foreach (var instance in singletonInstances.Values)
                {
                    if (instance is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Error("DI", $"Error disposing service: {ex.Message}", ex);
                        }
                    }
                }

                singletonInstances.Clear();
                services.Clear();
                disposed = true;

                Logger.Instance.Info("DI", "Service container disposed");
            }
        }

        /// <summary>
        /// Service scope implementation
        /// </summary>
        private class ServiceScope : IServiceScope
        {
            private readonly ServiceContainer rootContainer;
            private readonly Dictionary<Type, object> scopedInstances = new Dictionary<Type, object>();
            private bool disposed = false;

            public IServiceProvider ServiceProvider => new ScopedServiceProvider(this);

            public ServiceScope(ServiceContainer container)
            {
                this.rootContainer = container;
            }

            internal object ResolveScopedService(ServiceDescriptor descriptor)
            {
                lock (scopedInstances)
                {
                    // Check if already instantiated in this scope
                    if (scopedInstances.TryGetValue(descriptor.ServiceType, out var existingInstance))
                    {
                        return existingInstance;
                    }

                    // Create new instance for this scope
                    object instance;
                    if (descriptor.Factory != null)
                    {
                        instance = descriptor.Factory(ServiceProvider);
                    }
                    else
                    {
                        instance = Activator.CreateInstance(descriptor.ImplementationType);
                    }

                    scopedInstances[descriptor.ServiceType] = instance;
                    return instance;
                }
            }

            public void Dispose()
            {
                if (disposed) return;

                lock (scopedInstances)
                {
                    // Dispose all scoped instances
                    foreach (var instance in scopedInstances.Values)
                    {
                        if (instance is IDisposable disposable)
                        {
                            try
                            {
                                disposable.Dispose();
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error("DI", $"Error disposing scoped service: {ex.Message}", ex);
                            }
                        }
                    }

                    scopedInstances.Clear();
                    disposed = true;
                }
            }

            /// <summary>
            /// Service provider for scoped resolution
            /// </summary>
            private class ScopedServiceProvider : IServiceProvider
            {
                private readonly ServiceScope scope;

                public ScopedServiceProvider(ServiceScope scope)
                {
                    this.scope = scope;
                }

                public T GetService<T>()
                {
                    return (T)GetService(typeof(T));
                }

                public object GetService(Type serviceType)
                {
                    ServiceDescriptor descriptor;

                    // Only lock for descriptor lookup (minimal lock scope)
                    lock (scope.rootContainer.lockObject)
                    {
                        if (!scope.rootContainer.services.TryGetValue(serviceType, out descriptor))
                        {
                            return null;
                        }
                    }

                    // Resolve outside of lock - each resolver handles its own locking
                    switch (descriptor.Lifetime)
                    {
                        case ServiceLifetime.Singleton:
                            // Singletons come from root container
                            return scope.rootContainer.ResolveService(descriptor, this);

                        case ServiceLifetime.Transient:
                            // Transients always create new
                            return scope.rootContainer.ResolveService(descriptor, this);

                        case ServiceLifetime.Scoped:
                            // Scoped come from this scope
                            return scope.ResolveScopedService(descriptor);

                        default:
                            throw new NotSupportedException($"Unsupported lifetime: {descriptor.Lifetime}");
                    }
                }

                public T GetRequiredService<T>()
                {
                    return (T)GetRequiredService(typeof(T));
                }

                public object GetRequiredService(Type serviceType)
                {
                    var service = GetService(serviceType);
                    if (service == null)
                    {
                        throw new InvalidOperationException($"Service not registered: {serviceType.Name}");
                    }
                    return service;
                }
            }
        }
    }
}
