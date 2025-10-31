using System;

namespace SuperTUI.Core.Commands
{
    /// <summary>
    /// Command pattern interface for undo/redo operations
    /// Each command encapsulates an action and its inverse
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Execute the command (perform the action)
        /// </summary>
        void Execute();

        /// <summary>
        /// Undo the command (reverse the action)
        /// </summary>
        void Undo();

        /// <summary>
        /// Human-readable description of the command
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Timestamp when command was executed
        /// </summary>
        DateTime ExecutedAt { get; }
    }
}
