using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SuperTUI.Core.Components;

namespace SuperTUI.Core.Interfaces
{
    /// <summary>
    /// Strategy interface for pane-specific focus restoration logic.
    /// Allows each pane type to customize how focus is saved and restored.
    /// </summary>
    public interface IFocusRestorationStrategy
    {
        /// <summary>
        /// Restore focus to the appropriate element within the pane.
        /// </summary>
        /// <param name="pane">The pane to restore focus to</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>True if focus was successfully restored</returns>
        Task<bool> RestoreFocusAsync(PaneBase pane, CancellationToken cancellationToken = default);

        /// <summary>
        /// Save the current focus state for this pane.
        /// </summary>
        /// <param name="pane">The pane to save focus state from</param>
        /// <param name="state">Dictionary to save state into</param>
        void SaveFocusState(PaneBase pane, Dictionary<string, object> state);

        /// <summary>
        /// Restore focus state from saved data.
        /// </summary>
        /// <param name="pane">The pane to restore focus state to</param>
        /// <param name="state">Dictionary containing saved state</param>
        void RestoreFocusState(PaneBase pane, Dictionary<string, object> state);
    }
}
