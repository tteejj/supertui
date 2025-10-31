using System;
using System.Collections.Generic;

namespace SuperTUI.Core.Interfaces
{
    /// <summary>
    /// Manages modal UI elements with stack-based modal system
    /// Handles automatic input blocking, focus management, and modal results
    /// </summary>
    public interface IModalManager
    {
        /// <summary>
        /// Current active modal (top of stack), or null if no modals open
        /// </summary>
        IModal CurrentModal { get; }

        /// <summary>
        /// Number of modals currently open
        /// </summary>
        int ModalCount { get; }

        /// <summary>
        /// Whether any modals are currently open
        /// </summary>
        bool HasModals { get; }

        /// <summary>
        /// Show a modal (adds to stack, blocks background input)
        /// </summary>
        /// <param name="modal">Modal to show</param>
        void ShowModal(IModal modal);

        /// <summary>
        /// Close the current modal (top of stack)
        /// </summary>
        /// <param name="result">Modal result</param>
        /// <param name="customResult">Custom result data</param>
        void CloseModal(ModalResult result = ModalResult.None, object customResult = null);

        /// <summary>
        /// Close a specific modal (removes from stack)
        /// </summary>
        /// <param name="modal">Modal to close</param>
        /// <param name="result">Modal result</param>
        /// <param name="customResult">Custom result data</param>
        void CloseModal(IModal modal, ModalResult result = ModalResult.None, object customResult = null);

        /// <summary>
        /// Close all modals
        /// </summary>
        void CloseAllModals();

        /// <summary>
        /// Handle global key press (Escape, Enter)
        /// Returns true if key was handled by modal system
        /// </summary>
        bool HandleKeyPress(System.Windows.Input.Key key);

        /// <summary>
        /// Get all open modals (bottom to top)
        /// </summary>
        IReadOnlyList<IModal> GetModalStack();

        /// <summary>
        /// Event raised when a modal is opened
        /// </summary>
        event EventHandler<ModalEventArgs> ModalOpened;

        /// <summary>
        /// Event raised when a modal is closed
        /// </summary>
        event EventHandler<ModalEventArgs> ModalClosed;
    }

    /// <summary>
    /// Event args for modal open/close events
    /// </summary>
    public class ModalEventArgs : EventArgs
    {
        public IModal Modal { get; set; }
        public ModalResult Result { get; set; }
        public object CustomResult { get; set; }

        public ModalEventArgs(IModal modal, ModalResult result = ModalResult.None, object customResult = null)
        {
            Modal = modal;
            Result = result;
            CustomResult = customResult;
        }
    }
}
