using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    /// <summary>
    /// Service lifetime options
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>Single instance for entire application lifetime</summary>
        Singleton,
        /// <summary>New instance every time</summary>
        Transient,
        /// <summary>Single instance per scope (not implemented yet)</summary>
        Scoped
    }

    /// <summary>
    /// Service descriptor for registration
    /// </summary>
    internal class ServiceDescriptor
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public object ImplementationInstance { get; set; }
        public Func<ServiceContainer, object> ImplementationFactory { get; set; }
        public ServiceLifetime Lifetime { get; set; }
    }

    /// <summary>
    /// Dependency injection container
    /// Supports singleton and transient lifetimes, with factory functions
    /// </summary>
    public class ServiceContainer : IServiceContainer
    {
        private static ServiceContainer instance;
        public static ServiceContainer Instance => instance ??= new ServiceContainer();

        private readonly Dictionary<Type, ServiceDescriptor> descriptors = new Dictionary<Type, ServiceDescriptor>();
        private readonly Dictionary<Type, object> singletonInstances = new Dictionary<Type, object>();
        private readonly object lockObject = new object();
        private readonly HashSet<Type> resolvingTypes = new HashSet<Type>();

        /// <summary>
        /// Register a singleton service with an existing instance
        /// </summary>
        public void RegisterSingleton<TService>(TService instance)
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationInstance = instance,
                    Lifetime = ServiceLifetime.Singleton
                };

                descriptors[typeof(TService)] = descriptor;
                singletonInstances[typeof(TService)] = instance;
            }
        }

        /// <summary>
        /// Register a singleton service with a type
        /// </summary>
        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationType = typeof(TImplementation),
                    Lifetime = ServiceLifetime.Singleton
                };

                descriptors[typeof(TService)] = descriptor;
            }
        }

        /// <summary>
        /// Register a singleton service with a factory function
        /// </summary>
        public void RegisterSingleton<TService>(Func<ServiceContainer, TService> factory)
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationFactory = container => factory(container),
                    Lifetime = ServiceLifetime.Singleton
                };

                descriptors[typeof(TService)] = descriptor;
            }
        }

        /// <summary>
        /// Register a transient service (new instance each time)
        /// </summary>
        public void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationType = typeof(TImplementation),
                    Lifetime = ServiceLifetime.Transient
                };

                descriptors[typeof(TService)] = descriptor;
            }
        }

        /// <summary>
        /// Register a transient service with a factory function
        /// </summary>
        public void RegisterTransient<TService>(Func<ServiceContainer, TService> factory)
        {
            lock (lockObject)
            {
                var descriptor = new ServiceDescriptor
                {
                    ServiceType = typeof(TService),
                    ImplementationFactory = container => factory(container),
                    Lifetime = ServiceLifetime.Transient
                };

                descriptors[typeof(TService)] = descriptor;
            }
        }

        /// <summary>
        /// Resolve a service instance
        /// </summary>
        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        /// <summary>
        /// Resolve a service instance by type
        /// </summary>
        public object Resolve(Type serviceType)
        {
            lock (lockObject)
            {
                if (!descriptors.TryGetValue(serviceType, out var descriptor))
                {
                    // Better error message with suggestions
                    var registered = string.Join(", ", descriptors.Keys.Select(t => t.Name));
                    throw new InvalidOperationException(
                        $"Service of type '{serviceType.Name}' is not registered.\n" +
                        $"Registered services: {(descriptors.Count > 0 ? registered : "None")}\n" +
                        $"Did you forget to call ServiceRegistration.ConfigureServices()?");
                }

                // Circular dependency detection
                if (resolvingTypes.Contains(serviceType))
                {
                    var chain = string.Join(" -> ", resolvingTypes.Select(t => t.Name)) + " -> " + serviceType.Name;
                    throw new InvalidOperationException(
                        $"Circular dependency detected: {chain}\n" +
                        $"This usually means two services depend on each other. Consider using a factory or breaking the dependency.");
                }

                resolvingTypes.Add(serviceType);
                try
                {
                    // Singleton - return existing or create and cache
                    if (descriptor.Lifetime == ServiceLifetime.Singleton)
                    {
                        // Check if already instantiated
                        if (singletonInstances.TryGetValue(serviceType, out var existingInstance))
                        {
                            return existingInstance;
                        }

                        // Create new instance
                        var newInstance = CreateInstance(descriptor);
                        singletonInstances[serviceType] = newInstance;
                        return newInstance;
                    }

                    // Transient - always create new instance
                    if (descriptor.Lifetime == ServiceLifetime.Transient)
                    {
                        return CreateInstance(descriptor);
                    }

                    throw new NotImplementedException($"Service lifetime {descriptor.Lifetime} is not yet implemented");
                }
                finally
                {
                    resolvingTypes.Remove(serviceType);
                }
            }
        }

        /// <summary>
        /// Try to resolve a service, returns false if not found
        /// </summary>
        public bool TryResolve<T>(out T service)
        {
            lock (lockObject)
            {
                if (!descriptors.ContainsKey(typeof(T)))
                {
                    service = default(T);
                    return false;
                }

                try
                {
                    service = Resolve<T>();
                    return true;
                }
                catch
                {
                    service = default(T);
                    return false;
                }
            }
        }

        /// <summary>
        /// Check if a service is registered
        /// </summary>
        public bool IsRegistered<T>()
        {
            return descriptors.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Check if a service is registered by type
        /// </summary>
        public bool IsRegistered(Type serviceType)
        {
            return descriptors.ContainsKey(serviceType);
        }

        /// <summary>
        /// Get all registered service types (for debugging/diagnostics)
        /// </summary>
        public IEnumerable<Type> GetRegisteredServices()
        {
            return descriptors.Keys.ToList();
        }

        /// <summary>
        /// Get service registration info (for debugging/diagnostics)
        /// </summary>
        public string GetServiceInfo(Type serviceType)
        {
            if (!descriptors.TryGetValue(serviceType, out var descriptor))
            {
                return $"{serviceType.Name}: Not registered";
            }

            string impl = descriptor.ImplementationType?.Name ??
                         descriptor.ImplementationInstance?.GetType().Name ??
                         "Factory";

            string lifetime = descriptor.Lifetime.ToString();
            bool instantiated = descriptor.Lifetime == ServiceLifetime.Singleton &&
                               singletonInstances.ContainsKey(serviceType);

            return $"{serviceType.Name} -> {impl} ({lifetime}, {(instantiated ? "instantiated" : "not yet instantiated")})";
        }

        /// <summary>
        /// Clear all registrations
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                descriptors.Clear();
                singletonInstances.Clear();
            }
        }

        private object CreateInstance(ServiceDescriptor descriptor)
        {
            // If instance is already provided
            if (descriptor.ImplementationInstance != null)
            {
                return descriptor.ImplementationInstance;
            }

            // If factory is provided
            if (descriptor.ImplementationFactory != null)
            {
                return descriptor.ImplementationFactory(this);
            }

            // Create using reflection (basic constructor injection)
            if (descriptor.ImplementationType != null)
            {
                return CreateInstanceWithDI(descriptor.ImplementationType);
            }

            throw new InvalidOperationException($"Cannot create instance for service {descriptor.ServiceType.Name}");
        }

        private object CreateInstanceWithDI(Type implementationType)
        {
            // Get constructors ordered by parameter count (prefer ones with most params for DI)
            var constructors = implementationType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .ToList();

            if (constructors.Count == 0)
            {
                throw new InvalidOperationException($"No public constructors found for {implementationType.Name}");
            }

            // Try each constructor until one works
            foreach (var constructor in constructors)
            {
                try
                {
                    var parameters = constructor.GetParameters();
                    var parameterInstances = new object[parameters.Length];

                    // Resolve each parameter
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;

                        if (descriptors.ContainsKey(paramType))
                        {
                            parameterInstances[i] = Resolve(paramType);
                        }
                        else
                        {
                            // Parameter not registered, try next constructor
                            throw new InvalidOperationException($"Cannot resolve parameter {paramType.Name}");
                        }
                    }

                    // All parameters resolved, create instance
                    return constructor.Invoke(parameterInstances);
                }
                catch
                {
                    // Try next constructor
                    continue;
                }
            }

            // No constructor worked, throw error
            throw new InvalidOperationException($"Cannot create instance of {implementationType.Name}. All constructors failed.");
        }

        // Legacy methods for backward compatibility
        [Obsolete("Use RegisterSingleton<T>(T instance) instead")]
        public void Register<T>(T service)
        {
            RegisterSingleton(service);
        }

        [Obsolete("Use Resolve<T>() instead")]
        public T Get<T>()
        {
            return TryResolve<T>(out var service) ? service : default(T);
        }

        [Obsolete("Use TryResolve<T>(out T service) instead")]
        public bool TryGet<T>(out T service)
        {
            return TryResolve(out service);
        }
    }

    // ============================================================================
    // EVENT BUS
    // ============================================================================

    /// <summary>
    /// Simple event bus for inter-widget communication
    /// Allows widgets to communicate without direct references
}
