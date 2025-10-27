using System;
using System.Collections.Generic;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.DI
{
    /// <summary>
    /// Factory for creating widgets with dependency injection
    /// Uses constructor injection to resolve dependencies from ServiceContainer
    /// </summary>
    public class WidgetFactory
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Dictionary<string, Type> registeredWidgets = new Dictionary<string, Type>();
        private readonly Dictionary<Type, System.Reflection.ConstructorInfo> constructorCache = new Dictionary<Type, System.Reflection.ConstructorInfo>();

        public WidgetFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Register a widget type for creation
        /// </summary>
        public void RegisterWidget<TWidget>(string name) where TWidget : WidgetBase
        {
            registeredWidgets[name] = typeof(TWidget);
            Logger.Instance.Debug("WidgetFactory", $"Registered widget: {name} -> {typeof(TWidget).Name}");
        }

        /// <summary>
        /// Create widget instance with dependency injection
        /// Automatically resolves constructor parameters from the service container
        /// </summary>
        public TWidget CreateWidget<TWidget>() where TWidget : WidgetBase
        {
            return (TWidget)CreateWidgetInternal(typeof(TWidget));
        }

        /// <summary>
        /// Create widget by name with dependency injection
        /// </summary>
        public WidgetBase CreateWidget(string name)
        {
            if (!registeredWidgets.TryGetValue(name, out var widgetType))
            {
                throw new InvalidOperationException($"Widget not registered: {name}");
            }

            return (WidgetBase)CreateWidgetInternal(widgetType);
        }

        /// <summary>
        /// Internal method to create widget with constructor injection
        /// </summary>
        private object CreateWidgetInternal(Type widgetType)
        {
            // Find the best constructor to use
            var constructor = GetBestConstructor(widgetType);

            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"No suitable constructor found for {widgetType.Name}. " +
                    $"Widget must have either a DI constructor with interface parameters or a parameterless constructor.");
            }

            // Resolve constructor parameters
            var parameters = constructor.GetParameters();
            var parameterInstances = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var service = serviceProvider.GetService(paramType);

                if (service == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot resolve parameter '{parameters[i].Name}' of type '{paramType.Name}' " +
                        $"for widget '{widgetType.Name}'. Service not registered in container.");
                }

                parameterInstances[i] = service;
            }

            // Create instance with resolved dependencies
            try
            {
                return constructor.Invoke(parameterInstances);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create widget {widgetType.Name}: {ex.InnerException?.Message ?? ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Get the best constructor for dependency injection
        /// Prefers DI constructor with parameters, falls back to parameterless
        /// </summary>
        private System.Reflection.ConstructorInfo GetBestConstructor(Type widgetType)
        {
            // Check cache first
            if (constructorCache.TryGetValue(widgetType, out var cached))
            {
                return cached;
            }

            var constructors = widgetType.GetConstructors(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            System.Reflection.ConstructorInfo best = null;
            int maxParams = -1;

            // Prefer constructor with most parameters (DI constructor)
            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();

                // Skip if any parameters are not interfaces or registered types
                bool allResolvable = true;
                foreach (var param in parameters)
                {
                    // Check if parameter is interface (always resolvable) or registered concrete type
                    if (!param.ParameterType.IsInterface)
                    {
                        var service = serviceProvider.GetService(param.ParameterType);
                        if (service == null || !service.GetType().IsAssignableFrom(param.ParameterType))
                        {
                            allResolvable = false;
                            break;
                        }
                    }
                }

                if (allResolvable && parameters.Length > maxParams)
                {
                    maxParams = parameters.Length;
                    best = ctor;
                }
            }

            // Cache the result
            constructorCache[widgetType] = best;
            return best;
        }

        /// <summary>
        /// Get all registered widget names
        /// </summary>
        public IReadOnlyCollection<string> GetRegisteredWidgets()
        {
            return registeredWidgets.Keys;
        }
    }
}
