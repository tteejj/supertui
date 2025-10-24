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
    public class EventBus
    {
        private static EventBus instance;
        public static EventBus Instance => instance ??= new EventBus();

        private Dictionary<string, List<Action<object>>> subscribers = new Dictionary<string, List<Action<object>>>();

        public void Subscribe(string eventName, Action<object> handler)
        {
            if (!subscribers.ContainsKey(eventName))
                subscribers[eventName] = new List<Action<object>>();

            subscribers[eventName].Add(handler);
        }

        public void Unsubscribe(string eventName, Action<object> handler)
        {
            if (subscribers.ContainsKey(eventName))
                subscribers[eventName].Remove(handler);
        }

        public void Publish(string eventName, object data = null)
        {
            if (subscribers.ContainsKey(eventName))
            {
                foreach (var handler in subscribers[eventName].ToList())
                {
                    handler(data);
                }
            }
        }

        public void Clear()
        {
            subscribers.Clear();
        }
    }

    // ============================================================================
    // KEYBOARD SHORTCUT MANAGER
    // ============================================================================
}
