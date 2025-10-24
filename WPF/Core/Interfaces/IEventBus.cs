using System;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for event bus - enables testing and mocking
    /// Provides pub/sub messaging for inter-component communication
    /// </summary>
    public interface IEventBus
    {
        // Typed pub/sub
        void Subscribe<TEvent>(Action<TEvent> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false);
        void Unsubscribe<TEvent>(Action<TEvent> handler);
        void Publish<TEvent>(TEvent eventData);

        // Named events (string-based, backward compatibility)
        void Subscribe(string eventName, Action<object> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false);
        void Unsubscribe(string eventName, Action<object> handler);
        void Publish(string eventName, object data = null);

        // Request/response pattern
        void RegisterRequestHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler);
        TResponse Request<TRequest, TResponse>(TRequest request);
        bool TryRequest<TRequest, TResponse>(TRequest request, out TResponse response);

        // Utilities
        void CleanupDeadSubscriptions();
        (long Published, long Delivered, int TypedSubscribers, int NamedSubscribers) GetStatistics();
        bool HasSubscribers<TEvent>();
        bool HasSubscribers(string eventName);
        void Clear();
    }
}
