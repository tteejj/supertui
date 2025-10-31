using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core.Interfaces;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Manages modal UI elements with stack-based modal system
    /// Features: Nested modals, automatic input blocking, focus save/restore, modal results
    /// Thread-safe for UI thread operations
    /// </summary>
    public class ModalManager : IModalManager
    {
        private readonly ILogger logger;
        private readonly Stack<ModalEntry> modalStack;
        private readonly Panel modalContainer;
        private readonly Panel backgroundContainer;

        // Focus management
        private readonly Stack<IInputElement> focusStack;

        // Events
        public event EventHandler<ModalEventArgs> ModalOpened;
        public event EventHandler<ModalEventArgs> ModalClosed;

        // Properties
        public IModal CurrentModal => modalStack.Count > 0 ? modalStack.Peek().Modal : null;
        public int ModalCount => modalStack.Count;
        public bool HasModals => modalStack.Count > 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Logger service</param>
        /// <param name="modalContainer">Container for modal overlays (e.g., ModalOverlay Grid)</param>
        /// <param name="backgroundContainer">Container to block input (e.g., PaneCanvas)</param>
        public ModalManager(ILogger logger, Panel modalContainer, Panel backgroundContainer)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.modalContainer = modalContainer ?? throw new ArgumentNullException(nameof(modalContainer));
            this.backgroundContainer = backgroundContainer ?? throw new ArgumentNullException(nameof(backgroundContainer));

            modalStack = new Stack<ModalEntry>();
            focusStack = new Stack<IInputElement>();

            logger.Log(LogLevel.Info, "ModalManager", "Initialized");
        }

        /// <summary>
        /// Show a modal (adds to stack, blocks background input)
        /// </summary>
        public void ShowModal(IModal modal)
        {
            if (modal == null)
            {
                throw new ArgumentNullException(nameof(modal));
            }

            try
            {
                logger.Log(LogLevel.Debug, "ModalManager", $"Showing modal: {modal.ModalName}");

                // Save current focus
                var currentFocus = Keyboard.FocusedElement;
                if (currentFocus != null)
                {
                    focusStack.Push(currentFocus);
                    logger.Log(LogLevel.Debug, "ModalManager", $"Saved focus: {currentFocus.GetType().Name}");
                }

                // Initialize modal
                modal.Initialize();

                // Subscribe to close request
                modal.CloseRequested += OnModalCloseRequested;

                // Create modal entry
                var entry = new ModalEntry
                {
                    Modal = modal,
                    Overlay = CreateModalOverlay(modal)
                };

                // Add to stack BEFORE adding to UI (so HasModals is correct)
                modalStack.Push(entry);

                // Add to UI
                modalContainer.Children.Add(entry.Overlay);
                modalContainer.Visibility = Visibility.Visible;

                // Block background input
                UpdateBackgroundInputBlocking();

                // Show modal
                modal.Show();

                // Raise event
                ModalOpened?.Invoke(this, new ModalEventArgs(modal));

                logger.Log(LogLevel.Info, "ModalManager", $"Modal shown: {modal.ModalName} (stack depth: {modalStack.Count})");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "ModalManager", $"Failed to show modal '{modal.ModalName}': {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Close the current modal (top of stack)
        /// </summary>
        public void CloseModal(ModalResult result = ModalResult.None, object customResult = null)
        {
            if (modalStack.Count == 0)
            {
                logger.Log(LogLevel.Warning, "ModalManager", "CloseModal called with no modals open");
                return;
            }

            var entry = modalStack.Peek();
            CloseModal(entry.Modal, result, customResult);
        }

        /// <summary>
        /// Close a specific modal (removes from stack)
        /// </summary>
        public void CloseModal(IModal modal, ModalResult result = ModalResult.None, object customResult = null)
        {
            if (modal == null)
            {
                throw new ArgumentNullException(nameof(modal));
            }

            try
            {
                logger.Log(LogLevel.Debug, "ModalManager", $"Closing modal: {modal.ModalName} (result: {result})");

                // Find modal in stack
                var entries = modalStack.ToList();
                var index = entries.FindIndex(e => e.Modal == modal);

                if (index == -1)
                {
                    logger.Log(LogLevel.Warning, "ModalManager", $"Modal not found in stack: {modal.ModalName}");
                    return;
                }

                // If not top of stack, log warning but continue
                if (index != 0)
                {
                    logger.Log(LogLevel.Warning, "ModalManager", $"Closing modal not at top of stack: {modal.ModalName} (position: {index})");
                }

                // Get entry
                var entry = entries[index];

                // Remove from stack (rebuild stack without this entry)
                var newStack = new Stack<ModalEntry>();
                foreach (var e in entries.AsEnumerable().Reverse())
                {
                    if (e != entry)
                    {
                        newStack.Push(e);
                    }
                }
                modalStack.Clear();
                foreach (var e in newStack.Reverse())
                {
                    modalStack.Push(e);
                }

                // Unsubscribe from close request
                modal.CloseRequested -= OnModalCloseRequested;

                // Hide modal
                modal.Hide();

                // Animate close if possible
                AnimateModalClose(entry.Overlay, () =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        // Remove from UI
                        modalContainer.Children.Remove(entry.Overlay);

                        // If no more modals, hide container
                        if (modalStack.Count == 0)
                        {
                            modalContainer.Visibility = Visibility.Collapsed;
                        }

                        // Update background input blocking
                        UpdateBackgroundInputBlocking();

                        // Restore focus
                        RestoreFocus();

                        // Dispose modal
                        modal.Dispose();

                        // Raise event
                        ModalClosed?.Invoke(this, new ModalEventArgs(modal, result, customResult));

                        logger.Log(LogLevel.Info, "ModalManager", $"Modal closed: {modal.ModalName} (stack depth: {modalStack.Count})");
                    });
                });
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "ModalManager", $"Failed to close modal '{modal.ModalName}': {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Close all modals
        /// </summary>
        public void CloseAllModals()
        {
            logger.Log(LogLevel.Debug, "ModalManager", $"Closing all modals ({modalStack.Count})");

            // Close from top to bottom
            while (modalStack.Count > 0)
            {
                CloseModal(ModalResult.None);
            }
        }

        /// <summary>
        /// Handle global key press (Escape, Enter)
        /// Returns true if key was handled by modal system
        /// </summary>
        public bool HandleKeyPress(Key key)
        {
            if (modalStack.Count == 0)
            {
                return false;
            }

            var modal = modalStack.Peek().Modal;

            try
            {
                bool handled = false;

                switch (key)
                {
                    case Key.Escape:
                        handled = modal.OnEscape();
                        if (handled)
                        {
                            logger.Log(LogLevel.Debug, "ModalManager", $"Modal handled Escape: {modal.ModalName}");
                        }
                        break;

                    case Key.Enter:
                        handled = modal.OnEnter();
                        if (handled)
                        {
                            logger.Log(LogLevel.Debug, "ModalManager", $"Modal handled Enter: {modal.ModalName}");
                        }
                        break;
                }

                return handled;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "ModalManager", $"Error handling key press in modal '{modal.ModalName}': {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Get all open modals (bottom to top)
        /// </summary>
        public IReadOnlyList<IModal> GetModalStack()
        {
            return modalStack.Select(e => e.Modal).Reverse().ToList().AsReadOnly();
        }

        /// <summary>
        /// Create modal overlay with semi-transparent background
        /// </summary>
        private Border CreateModalOverlay(IModal modal)
        {
            var overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(204, 0, 0, 0)), // Semi-transparent dark background
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = modal.ModalElement
            };

            return overlay;
        }

        /// <summary>
        /// Update background input blocking based on modal stack
        /// </summary>
        private void UpdateBackgroundInputBlocking()
        {
            if (modalStack.Count > 0)
            {
                // Block input to background
                backgroundContainer.IsHitTestVisible = false;
                backgroundContainer.Focusable = false;
                logger.Log(LogLevel.Debug, "ModalManager", "Background input blocked");
            }
            else
            {
                // Re-enable input to background
                backgroundContainer.IsHitTestVisible = true;
                backgroundContainer.Focusable = true;
                logger.Log(LogLevel.Debug, "ModalManager", "Background input restored");
            }
        }

        /// <summary>
        /// Restore focus to previously focused element
        /// </summary>
        private void RestoreFocus()
        {
            if (focusStack.Count > 0)
            {
                var previousFocus = focusStack.Pop();

                // Use dispatcher to ensure focus is set after close animation completes
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // Check element is still valid before focusing
                        if (previousFocus != null &&
                            (!(previousFocus is FrameworkElement fe) || fe.IsLoaded))
                        {
                            Keyboard.Focus(previousFocus);
                            logger.Log(LogLevel.Debug, "ModalManager", $"Restored focus to: {previousFocus.GetType().Name}");
                        }
                        else
                        {
                            logger.Log(LogLevel.Debug, "ModalManager", "Cannot restore focus - element no longer valid");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Log(LogLevel.Warning, "ModalManager", $"Failed to restore focus: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        /// <summary>
        /// Animate modal closing
        /// </summary>
        private void AnimateModalClose(UIElement element, Action onComplete)
        {
            try
            {
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(100)
                };
                fadeOut.Completed += (s, e) => onComplete?.Invoke();
                element.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, "ModalManager", $"Failed to animate modal close: {ex.Message}");
                // Fall back to immediate close
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Handle modal close request event
        /// </summary>
        private void OnModalCloseRequested(object sender, ModalClosedEventArgs e)
        {
            if (sender is IModal modal)
            {
                CloseModal(modal, e.Result, e.CustomResult);
            }
        }

        /// <summary>
        /// Modal entry in stack
        /// </summary>
        private class ModalEntry
        {
            public IModal Modal { get; set; }
            public Border Overlay { get; set; }
        }
    }
}
