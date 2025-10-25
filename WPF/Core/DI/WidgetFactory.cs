using System;
using System.Collections.Generic;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.DI
{
    /// <summary>
    /// Factory for creating widgets with dependency injection
    /// PHASE 3: Maximum DI - Widget constructor injection
    /// </summary>
    public class WidgetFactory
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Dictionary<string, Type> registeredWidgets = new Dictionary<string, Type>();

        public WidgetFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
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
        /// </summary>
        public TWidget CreateWidget<TWidget>() where TWidget : WidgetBase, new()
        {
            // For now, widgets don't have dependencies, so just create with new()
            // Future: Use constructor injection when widgets need services
            return new TWidget();
        }

        /// <summary>
        /// Create widget by name
        /// </summary>
        public WidgetBase CreateWidget(string name)
        {
            if (!registeredWidgets.TryGetValue(name, out var widgetType))
            {
                throw new InvalidOperationException($"Widget not registered: {name}");
            }

            // Create instance - for now using Activator
            // Future: Support constructor injection
            return (WidgetBase)Activator.CreateInstance(widgetType);
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
