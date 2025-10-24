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
    public class ServiceContainer
    {
        private static ServiceContainer instance;
        public static ServiceContainer Instance => instance ??= new ServiceContainer();

        private Dictionary<Type, object> services = new Dictionary<Type, object>();

        public void Register<T>(T service)
        {
            services[typeof(T)] = service;
        }

        public T Get<T>()
        {
            if (services.TryGetValue(typeof(T), out var service))
                return (T)service;

            return default(T);
        }

        public bool TryGet<T>(out T service)
        {
            if (services.TryGetValue(typeof(T), out var obj))
            {
                service = (T)obj;
                return true;
            }

            service = default(T);
            return false;
        }

        public void Clear()
        {
            services.Clear();
        }
    }

    // ============================================================================
    // EVENT BUS
    // ============================================================================

    /// <summary>
    /// Simple event bus for inter-widget communication
    /// Allows widgets to communicate without direct references
}
