using System.Windows.Controls;
using System.Windows.Input;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Base class for all overlay controls with keyboard handling and lifecycle methods
    /// </summary>
    public abstract class OverlayBase : UserControl
    {
        /// <summary>
        /// Called when the overlay is shown. Use this to set initial focus.
        /// </summary>
        public virtual void OnShown()
        {
            // Default: focus the overlay itself
            this.Focus();
        }

        /// <summary>
        /// Handle keyboard input for this overlay.
        /// Return true if the key was handled, false to allow propagation.
        /// </summary>
        public virtual bool HandleKeyDown(KeyEventArgs e)
        {
            // Default: ESC closes overlay (handled by OverlayManager)
            return false;
        }
    }
}
