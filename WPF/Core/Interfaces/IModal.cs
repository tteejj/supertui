using System;
using System.Windows;

namespace SuperTUI.Core.Interfaces
{
    /// <summary>
    /// Result returned when a modal closes
    /// </summary>
    public enum ModalResult
    {
        None,       // Modal was dismissed without action
        OK,         // User confirmed (Enter, OK button)
        Cancel,     // User cancelled (Escape, Cancel button)
        Custom      // Custom result (see CustomResult property)
    }

    /// <summary>
    /// Interface for modal UI elements (panes, overlays, dialogs)
    /// Modals block input to background UI and support result patterns
    /// </summary>
    public interface IModal
    {
        /// <summary>
        /// The result of the modal interaction
        /// </summary>
        ModalResult Result { get; }

        /// <summary>
        /// Custom result data (used when Result == ModalResult.Custom)
        /// </summary>
        object CustomResult { get; }

        /// <summary>
        /// The modal UI element to display
        /// </summary>
        UIElement ModalElement { get; }

        /// <summary>
        /// Modal name for logging/debugging
        /// </summary>
        string ModalName { get; }

        /// <summary>
        /// Initialize the modal (called before showing)
        /// </summary>
        void Initialize();

        /// <summary>
        /// Show the modal (called when modal becomes visible)
        /// </summary>
        void Show();

        /// <summary>
        /// Hide the modal (called when modal is closed)
        /// </summary>
        void Hide();

        /// <summary>
        /// Handle Escape key press (default: close with Cancel result)
        /// Return true to close modal, false to prevent closing
        /// </summary>
        bool OnEscape();

        /// <summary>
        /// Handle Enter key press (default: close with OK result)
        /// Return true to close modal, false to prevent closing
        /// </summary>
        bool OnEnter();

        /// <summary>
        /// Clean up modal resources
        /// </summary>
        void Dispose();

        /// <summary>
        /// Event raised when modal requests to be closed
        /// </summary>
        event EventHandler<ModalClosedEventArgs> CloseRequested;
    }

    /// <summary>
    /// Event args for modal close request
    /// </summary>
    public class ModalClosedEventArgs : EventArgs
    {
        public ModalResult Result { get; set; }
        public object CustomResult { get; set; }

        public ModalClosedEventArgs(ModalResult result, object customResult = null)
        {
            Result = result;
            CustomResult = customResult;
        }
    }
}
