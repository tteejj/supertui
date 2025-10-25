using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuperTUI.Core;
using SuperTUI.Extensions;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for state persistence - enables save/restore of application state
    /// </summary>
    public interface IStatePersistenceManager
    {
        /// <summary>
        /// Event fired when state changes
        /// </summary>
        event EventHandler<StateChangedEventArgs> StateChanged;

        /// <summary>
        /// Gets the migration manager for registering custom migrations
        /// </summary>
        StateMigrationManager MigrationManager { get; }

        /// <summary>
        /// Initialize the persistence manager with state directory
        /// </summary>
        void Initialize(string stateDir = null);

        /// <summary>
        /// Capture current state from workspace manager
        /// </summary>
        StateSnapshot CaptureState(WorkspaceManager workspaceManager, Dictionary<string, object> customData = null);

        /// <summary>
        /// Restore state from a snapshot
        /// </summary>
        void RestoreState(StateSnapshot snapshot, WorkspaceManager workspaceManager);

        /// <summary>
        /// Save state synchronously
        /// </summary>
        void SaveState(StateSnapshot snapshot, bool createBackup = false);

        /// <summary>
        /// Save state asynchronously
        /// </summary>
        Task SaveStateAsync(StateSnapshot snapshot, bool createBackup = false);

        /// <summary>
        /// Load state synchronously
        /// </summary>
        StateSnapshot LoadState();

        /// <summary>
        /// Load state asynchronously
        /// </summary>
        Task<StateSnapshot> LoadStateAsync();

        /// <summary>
        /// Create a backup of the current state file
        /// </summary>
        void CreateBackup();

        /// <summary>
        /// Restore from a backup file
        /// </summary>
        void RestoreFromBackup(string backupFilePath);

        /// <summary>
        /// Push current state to undo history
        /// </summary>
        void PushUndoState(StateSnapshot snapshot);

        /// <summary>
        /// Undo to previous state
        /// </summary>
        StateSnapshot Undo();

        /// <summary>
        /// Redo to next state
        /// </summary>
        StateSnapshot Redo();

        /// <summary>
        /// Check if undo is available
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Check if redo is available
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// Clear undo/redo history
        /// </summary>
        void ClearHistory();
    }
}
