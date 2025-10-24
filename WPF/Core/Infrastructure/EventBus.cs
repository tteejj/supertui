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
    /// Subscription priority levels
    /// </summary>
    public enum SubscriptionPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Internal subscription holder with weak reference support
    /// </summary>
    internal class Subscription
    {
        public Type EventType { get; set; }
        public WeakReference HandlerReference { get; set; }
        public Delegate StrongHandler { get; set; }
        public SubscriptionPriority Priority { get; set; }
        public bool IsWeak { get; set; }

        public bool IsAlive => !IsWeak || HandlerReference?.IsAlive == true;

        public void Invoke(object eventData)
        {
            Delegate handler = IsWeak
                ? HandlerReference?.Target as Delegate
                : StrongHandler;

            handler?.DynamicInvoke(eventData);
        }
    }

    /// <summary>
    /// Enhanced event bus for inter-widget communication
    /// Supports strong typing, weak references, priorities, and filtering
    /// </summary>
    public class EventBus : IEventBus
    {
        private static EventBus instance;
        public static EventBus Instance => instance ??= new EventBus();

        private readonly Dictionary<Type, List<Subscription>> typedSubscriptions = new Dictionary<Type, List<Subscription>>();
        private readonly Dictionary<string, List<Subscription>> namedSubscriptions = new Dictionary<string, List<Subscription>>();
        private readonly object lockObject = new object();

        // Statistics
        private long totalPublished = 0;
        private long totalDelivered = 0;

        #region Typed Pub/Sub

        /// <summary>
        /// Subscribe to a strongly-typed event
        /// </summary>
        /// <param name="handler">Event handler callback</param>
        /// <param name="priority">Subscription priority (higher priority handlers are called first)</param>
        /// <param name="useWeakReference">Use weak reference (default false). WARNING: Weak references to lambdas/closures will be GC'd immediately!</param>
        /// <remarks>
        /// IMPORTANT: useWeakReference defaults to FALSE because:
        /// - Most subscriptions use lambdas or closures
        /// - Weak references to lambdas get garbage collected immediately
        /// - This would cause event handlers to mysteriously stop working
        ///
        /// Only use weak references when:
        /// - Handler is a method on a long-lived object
        /// - You explicitly maintain a strong reference to the delegate elsewhere
        ///
        /// For most use cases, use strong references and explicitly Unsubscribe when done.
        /// </remarks>
        public void Subscribe<TEvent>(Action<TEvent> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false)
        {
            lock (lockObject)
            {
                var eventType = typeof(TEvent);

                if (!typedSubscriptions.ContainsKey(eventType))
                    typedSubscriptions[eventType] = new List<Subscription>();

                var subscription = new Subscription
                {
                    EventType = eventType,
                    Priority = priority,
                    IsWeak = useWeakReference
                };

                if (useWeakReference)
                {
                    subscription.HandlerReference = new WeakReference(handler);
                }
                else
                {
                    subscription.StrongHandler = handler;
                }

                typedSubscriptions[eventType].Add(subscription);

                // Sort by priority (highest first)
                typedSubscriptions[eventType] = typedSubscriptions[eventType]
                    .OrderByDescending(s => s.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Unsubscribe from a strongly-typed event
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            lock (lockObject)
            {
                var eventType = typeof(TEvent);

                if (typedSubscriptions.ContainsKey(eventType))
                {
                    typedSubscriptions[eventType].RemoveAll(s =>
                    {
                        if (s.IsWeak)
                        {
                            var target = s.HandlerReference?.Target as Action<TEvent>;
                            return target == handler || !s.HandlerReference.IsAlive;
                        }
                        else
                        {
                            return s.StrongHandler as Action<TEvent> == handler;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Publish a strongly-typed event
        /// </summary>
        public void Publish<TEvent>(TEvent eventData)
        {
            lock (lockObject)
            {
                totalPublished++;
                var eventType = typeof(TEvent);

                if (!typedSubscriptions.ContainsKey(eventType))
                    return;

                var subs = typedSubscriptions[eventType].ToList();
                var deadSubscriptions = new List<Subscription>();

                foreach (var subscription in subs)
                {
                    if (!subscription.IsAlive)
                    {
                        deadSubscriptions.Add(subscription);
                        continue;
                    }

                    try
                    {
                        subscription.Invoke(eventData);
                        totalDelivered++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance?.Error("EventBus",
                            $"Error delivering event {eventType.Name}: {ex.Message}", ex);
                    }
                }

                // Clean up dead subscriptions
                if (deadSubscriptions.Count > 0)
                {
                    typedSubscriptions[eventType].RemoveAll(s => deadSubscriptions.Contains(s));
                }
            }
        }

        #endregion

        #region Named Events (String-based, backward compatibility)

        /// <summary>
        /// Subscribe to a named event (string-based)
        /// </summary>
        /// <param name="eventName">Name of the event</param>
        /// <param name="handler">Event handler callback</param>
        /// <param name="priority">Subscription priority</param>
        /// <param name="useWeakReference">Use weak reference (default false). See Subscribe&lt;TEvent&gt; remarks for details.</param>
        public void Subscribe(string eventName, Action<object> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false)
        {
            lock (lockObject)
            {
                if (!namedSubscriptions.ContainsKey(eventName))
                    namedSubscriptions[eventName] = new List<Subscription>();

                var subscription = new Subscription
                {
                    Priority = priority,
                    IsWeak = useWeakReference
                };

                if (useWeakReference)
                {
                    subscription.HandlerReference = new WeakReference(handler);
                }
                else
                {
                    subscription.StrongHandler = handler;
                }

                namedSubscriptions[eventName].Add(subscription);

                // Sort by priority
                namedSubscriptions[eventName] = namedSubscriptions[eventName]
                    .OrderByDescending(s => s.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Unsubscribe from a named event
        /// </summary>
        public void Unsubscribe(string eventName, Action<object> handler)
        {
            lock (lockObject)
            {
                if (namedSubscriptions.ContainsKey(eventName))
                {
                    namedSubscriptions[eventName].RemoveAll(s =>
                    {
                        if (s.IsWeak)
                        {
                            var target = s.HandlerReference?.Target as Action<object>;
                            return target == handler || !s.HandlerReference.IsAlive;
                        }
                        else
                        {
                            return s.StrongHandler as Action<object> == handler;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Publish a named event
        /// </summary>
        public void Publish(string eventName, object data = null)
        {
            lock (lockObject)
            {
                totalPublished++;

                if (!namedSubscriptions.ContainsKey(eventName))
                    return;

                var subs = namedSubscriptions[eventName].ToList();
                var deadSubscriptions = new List<Subscription>();

                foreach (var subscription in subs)
                {
                    if (!subscription.IsAlive)
                    {
                        deadSubscriptions.Add(subscription);
                        continue;
                    }

                    try
                    {
                        subscription.Invoke(data);
                        totalDelivered++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance?.Error("EventBus",
                            $"Error delivering named event '{eventName}': {ex.Message}", ex);
                    }
                }

                // Clean up dead subscriptions
                if (deadSubscriptions.Count > 0)
                {
                    namedSubscriptions[eventName].RemoveAll(s => deadSubscriptions.Contains(s));
                }
            }
        }

        #endregion

        #region Request/Response Pattern

        private readonly Dictionary<Type, Delegate> requestHandlers = new Dictionary<Type, Delegate>();

        /// <summary>
        /// Register a request handler
        /// </summary>
        public void RegisterRequestHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler)
        {
            lock (lockObject)
            {
                requestHandlers[typeof(TRequest)] = handler;
            }
        }

        /// <summary>
        /// Send a request and get a response
        /// </summary>
        public TResponse Request<TRequest, TResponse>(TRequest request)
        {
            lock (lockObject)
            {
                var requestType = typeof(TRequest);

                if (!requestHandlers.ContainsKey(requestType))
                {
                    throw new InvalidOperationException($"No handler registered for request type {requestType.Name}");
                }

                var handler = requestHandlers[requestType] as Func<TRequest, TResponse>;
                return handler(request);
            }
        }

        /// <summary>
        /// Try to send a request, returns false if no handler
        /// </summary>
        public bool TryRequest<TRequest, TResponse>(TRequest request, out TResponse response)
        {
            lock (lockObject)
            {
                var requestType = typeof(TRequest);

                if (!requestHandlers.ContainsKey(requestType))
                {
                    response = default(TResponse);
                    return false;
                }

                try
                {
                    var handler = requestHandlers[requestType] as Func<TRequest, TResponse>;
                    response = handler(request);
                    return true;
                }
                catch
                {
                    response = default(TResponse);
                    return false;
                }
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Remove all dead subscriptions (garbage collected handlers)
        /// </summary>
        public void CleanupDeadSubscriptions()
        {
            lock (lockObject)
            {
                // Clean typed subscriptions
                foreach (var kvp in typedSubscriptions.ToList())
                {
                    typedSubscriptions[kvp.Key].RemoveAll(s => !s.IsAlive);
                }

                // Clean named subscriptions
                foreach (var kvp in namedSubscriptions.ToList())
                {
                    namedSubscriptions[kvp.Key].RemoveAll(s => !s.IsAlive);
                }
            }
        }

        /// <summary>
        /// Get statistics about event bus usage
        /// </summary>
        public (long Published, long Delivered, int TypedSubscribers, int NamedSubscribers) GetStatistics()
        {
            lock (lockObject)
            {
                int typedCount = typedSubscriptions.Sum(kvp => kvp.Value.Count);
                int namedCount = namedSubscriptions.Sum(kvp => kvp.Value.Count);

                return (totalPublished, totalDelivered, typedCount, namedCount);
            }
        }

        /// <summary>
        /// Check if any subscribers exist for an event type
        /// </summary>
        public bool HasSubscribers<TEvent>()
        {
            lock (lockObject)
            {
                var eventType = typeof(TEvent);
                return typedSubscriptions.ContainsKey(eventType) &&
                       typedSubscriptions[eventType].Any(s => s.IsAlive);
            }
        }

        /// <summary>
        /// Check if any subscribers exist for a named event
        /// </summary>
        public bool HasSubscribers(string eventName)
        {
            lock (lockObject)
            {
                return namedSubscriptions.ContainsKey(eventName) &&
                       namedSubscriptions[eventName].Any(s => s.IsAlive);
            }
        }

        /// <summary>
        /// Clear all subscriptions
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                typedSubscriptions.Clear();
                namedSubscriptions.Clear();
                requestHandlers.Clear();
                totalPublished = 0;
                totalDelivered = 0;
            }
        }

        #endregion
    }

    // ============================================================================
    // KEYBOARD SHORTCUT MANAGER
    // ============================================================================
}
