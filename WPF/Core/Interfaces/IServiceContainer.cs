using System;
using System.Collections.Generic;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for service container (DI) - enables testing and mocking
    /// </summary>
    public interface IServiceContainer
    {
        // Registration methods
        void RegisterSingleton<TService>(TService instance);
        void RegisterSingleton<TService, TImplementation>() where TImplementation : TService;
        void RegisterSingleton<TService>(Func<ServiceContainer, TService> factory);
        void RegisterTransient<TService, TImplementation>() where TImplementation : TService;
        void RegisterTransient<TService>(Func<ServiceContainer, TService> factory);

        // Resolution methods
        T Resolve<T>();
        object Resolve(Type serviceType);
        bool TryResolve<T>(out T service);

        // Query methods
        bool IsRegistered<T>();
        bool IsRegistered(Type serviceType);
        IEnumerable<Type> GetRegisteredServices();
        string GetServiceInfo(Type serviceType);

        // Management methods
        void Clear();
    }
}
