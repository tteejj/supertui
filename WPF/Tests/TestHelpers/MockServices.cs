using System;
using System.Collections.Generic;
using System.Windows.Input;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.TestHelpers
{
    /// <summary>
    /// Mock implementation of IEventBus for testing
    /// Note: IEventBus is in SuperTUI.Core namespace
    /// </summary>
    public class MockEventBus : SuperTUI.Core.IEventBus
    {
        public void Subscribe<TEvent>(Action<TEvent> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false) { }
        public void Unsubscribe<TEvent>(Action<TEvent> handler) { }
        public void Publish<TEvent>(TEvent eventData) { }
        public void Subscribe(string eventName, Action<object> handler, SubscriptionPriority priority = SubscriptionPriority.Normal, bool useWeakReference = false) { }
        public void Unsubscribe(string eventName, Action<object> handler) { }
        public void Publish(string eventName, object data = null) { }
        public void RegisterRequestHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler) { }
        public TResponse Request<TRequest, TResponse>(TRequest request) => default(TResponse);
        public bool TryRequest<TRequest, TResponse>(TRequest request, out TResponse response)
        {
            response = default(TResponse);
            return false;
        }
        public void CleanupDeadSubscriptions() { }
        public (long Published, long Delivered, int TypedSubscribers, int NamedSubscribers) GetStatistics() => (0, 0, 0, 0);
        public bool HasSubscribers<TEvent>() => false;
        public bool HasSubscribers(string eventName) => false;
        public void Clear() { }
    }

    /// <summary>
    /// Mock implementation of IShortcutManager for testing
    /// </summary>
    public class MockShortcutManager : IShortcutManager
    {
        public void RegisterGlobal(Key key, ModifierKeys modifiers, Action action, string description = "") { }
        public void RegisterForWorkspace(string workspaceName, Key key, ModifierKeys modifiers, Action action, string description = "") { }
        public void RegisterForPane(string paneName, Key key, ModifierKeys modifiers, Action action, string description = "") { }
        public bool HandleKeyPress(Key key, ModifierKeys modifiers, string currentWorkspace = null, string focusedPaneName = null) => false;
        public IReadOnlyList<KeyboardShortcut> GetGlobalShortcuts() => new List<KeyboardShortcut>();
        public IReadOnlyList<KeyboardShortcut> GetWorkspaceShortcuts(string workspaceName) => new List<KeyboardShortcut>();
        public IReadOnlyList<KeyboardShortcut> GetPaneShortcuts(string paneName) => new List<KeyboardShortcut>();
        public List<KeyboardShortcut> GetAllShortcuts() => new List<KeyboardShortcut>();
        public void ClearAll() { }
        public void ClearWorkspace(string workspaceName) { }
        public void ClearPane(string paneName) { }
        public bool IsUserTyping() => false;
    }
}
