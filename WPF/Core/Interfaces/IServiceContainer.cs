using System;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for service container (DI) - enables testing and mocking
    /// </summary>
    public interface IServiceContainer
    {
        void Register<TInterface, TImplementation>() where TImplementation : TInterface, new();
        void RegisterInstance<TInterface>(TInterface instance);
        void RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface, new();
        TInterface Resolve<TInterface>();
        bool IsRegistered<TInterface>();
    }
}
