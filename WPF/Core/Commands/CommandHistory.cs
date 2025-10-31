using System;
using System.Collections.Generic;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Commands
{
    /// <summary>
    /// Manages command history for undo/redo operations
    /// Thread-safe command stack with configurable max history
    /// </summary>
    public class CommandHistory
    {
        private readonly Stack<ICommand> undoStack;
        private readonly Stack<ICommand> redoStack;
        private readonly int maxHistorySize;
        private readonly object lockObject = new object();
        private readonly ILogger logger;

        // Events
        public event Action HistoryChanged;

        public CommandHistory(ILogger logger = null, int maxHistorySize = 50)
        {
            this.logger = logger;
            this.maxHistorySize = maxHistorySize;
            undoStack = new Stack<ICommand>(maxHistorySize);
            redoStack = new Stack<ICommand>(maxHistorySize);
        }

        /// <summary>
        /// Execute a command and add it to the undo stack
        /// Clears redo stack (new branch in history)
        /// </summary>
        public void Execute(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            lock (lockObject)
            {
                try
                {
                    command.Execute();
                    undoStack.Push(command);
                    redoStack.Clear();

                    // Limit stack size
                    if (undoStack.Count > maxHistorySize)
                    {
                        var temp = new Stack<ICommand>();
                        for (int i = 0; i < maxHistorySize; i++)
                        {
                            temp.Push(undoStack.Pop());
                        }
                        undoStack.Clear();
                        while (temp.Count > 0)
                        {
                            undoStack.Push(temp.Pop());
                        }
                    }

                    logger?.Log(LogLevel.Debug, "CommandHistory", $"Executed: {command.Description}");
                    HistoryChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    logger?.Log(LogLevel.Error, "CommandHistory", $"Failed to execute command: {ex.Message}", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Undo the last command
        /// </summary>
        public void Undo()
        {
            lock (lockObject)
            {
                if (undoStack.Count == 0)
                {
                    logger?.Log(LogLevel.Debug, "CommandHistory", "Nothing to undo");
                    return;
                }

                try
                {
                    var command = undoStack.Pop();
                    command.Undo();
                    redoStack.Push(command);

                    logger?.Log(LogLevel.Info, "CommandHistory", $"Undone: {command.Description}");
                    HistoryChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    logger?.Log(LogLevel.Error, "CommandHistory", $"Failed to undo command: {ex.Message}", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Redo the last undone command
        /// </summary>
        public void Redo()
        {
            lock (lockObject)
            {
                if (redoStack.Count == 0)
                {
                    logger?.Log(LogLevel.Debug, "CommandHistory", "Nothing to redo");
                    return;
                }

                try
                {
                    var command = redoStack.Pop();
                    command.Execute();
                    undoStack.Push(command);

                    logger?.Log(LogLevel.Info, "CommandHistory", $"Redone: {command.Description}");
                    HistoryChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    logger?.Log(LogLevel.Error, "CommandHistory", $"Failed to redo command: {ex.Message}", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Can undo?
        /// </summary>
        public bool CanUndo
        {
            get
            {
                lock (lockObject)
                {
                    return undoStack.Count > 0;
                }
            }
        }

        /// <summary>
        /// Can redo?
        /// </summary>
        public bool CanRedo
        {
            get
            {
                lock (lockObject)
                {
                    return redoStack.Count > 0;
                }
            }
        }

        /// <summary>
        /// Get the description of the next undo command
        /// </summary>
        public string GetUndoDescription()
        {
            lock (lockObject)
            {
                return undoStack.Count > 0 ? undoStack.Peek().Description : null;
            }
        }

        /// <summary>
        /// Get the description of the next redo command
        /// </summary>
        public string GetRedoDescription()
        {
            lock (lockObject)
            {
                return redoStack.Count > 0 ? redoStack.Peek().Description : null;
            }
        }

        /// <summary>
        /// Clear all history
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                undoStack.Clear();
                redoStack.Clear();
                logger?.Log(LogLevel.Debug, "CommandHistory", "History cleared");
                HistoryChanged?.Invoke();
            }
        }

        /// <summary>
        /// Get undo stack size
        /// </summary>
        public int UndoCount
        {
            get
            {
                lock (lockObject)
                {
                    return undoStack.Count;
                }
            }
        }

        /// <summary>
        /// Get redo stack size
        /// </summary>
        public int RedoCount
        {
            get
            {
                lock (lockObject)
                {
                    return redoStack.Count;
                }
            }
        }
    }
}
