using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SuperTUI.Core.Components;
using SuperTUI.Core.Interfaces;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Default focus restoration strategy that handles common patterns.
    /// Tries to focus the last focused element, falls back to first focusable child, then pane itself.
    /// </summary>
    public class DefaultFocusRestorationStrategy : IFocusRestorationStrategy
    {
        private readonly ILogger logger;
        private const string FOCUSED_ELEMENT_KEY = "FocusedElementName";

        public DefaultFocusRestorationStrategy(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> RestoreFocusAsync(PaneBase pane, CancellationToken cancellationToken = default)
        {
            if (pane == null) return false;

            // Ensure pane is loaded
            if (!pane.IsLoaded)
            {
                await WaitForLoadedAsync(pane, cancellationToken);
            }

            // Try to focus the pane using standard WPF method
            bool success = false;
            await Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                // Try to focus first focusable child within the pane
                var firstFocusable = FindFirstFocusableChild(pane);
                if (firstFocusable != null)
                {
                    success = Keyboard.Focus(firstFocusable) != null;
                }
                else
                {
                    // Fall back to focusing the pane itself
                    success = Keyboard.Focus(pane) != null;
                }
            }, DispatcherPriority.Render);

            return success;
        }

        public void SaveFocusState(PaneBase pane, Dictionary<string, object> state)
        {
            if (pane == null || state == null) return;

            var focusedElement = Keyboard.FocusedElement as FrameworkElement;
            if (focusedElement != null && IsDescendantOf(focusedElement, pane))
            {
                state[FOCUSED_ELEMENT_KEY] = focusedElement.Name ?? "";
            }
        }

        public void RestoreFocusState(PaneBase pane, Dictionary<string, object> state)
        {
            if (pane == null || state == null) return;

            if (state.TryGetValue(FOCUSED_ELEMENT_KEY, out var elementName) && elementName is string name && !string.IsNullOrEmpty(name))
            {
                var element = pane.FindName(name) as UIElement;
                if (element != null && element.Focusable)
                {
                    Keyboard.Focus(element);
                }
            }
        }

        private async Task WaitForLoadedAsync(FrameworkElement element, CancellationToken cancellationToken)
        {
            if (element.IsLoaded) return;

            var tcs = new TaskCompletionSource<bool>();

            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                RoutedEventHandler handler = null;
                handler = (s, e) =>
                {
                    element.Loaded -= handler;
                    tcs.TrySetResult(true);
                };
                element.Loaded += handler;

                try
                {
                    await tcs.Task;
                }
                catch (TaskCanceledException)
                {
                    element.Loaded -= handler;
                    throw;
                }
            }
        }

        private UIElement? FindFirstFocusableChild(DependencyObject parent)
        {
            if (parent == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is UIElement uiElement && uiElement.Focusable && uiElement.IsVisible)
                {
                    return uiElement;
                }

                var result = FindFirstFocusableChild(child);
                if (result != null) return result;
            }

            return null;
        }

        private bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            var current = child;
            while (current != null)
            {
                if (current == parent) return true;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
