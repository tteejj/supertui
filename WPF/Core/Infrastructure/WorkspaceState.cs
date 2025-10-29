using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Represents the persisted state of a workspace
    /// Includes open panes, their order, and project context
    /// </summary>
    public class PaneWorkspaceState
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public List<string> OpenPaneTypes { get; set; } = new List<string>();
        public int FocusedPaneIndex { get; set; } = -1;
        public Guid? CurrentProjectId { get; set; }
        public DateTime LastModified { get; set; }

        public PaneWorkspaceState()
        {
        }

        public PaneWorkspaceState(string name, int index)
        {
            Name = name;
            Index = index;
            LastModified = DateTime.Now;
        }
    }

    /// <summary>
    /// Manages multiple workspaces with state persistence
    /// </summary>
    public class PaneWorkspaceManager
    {
        private readonly string stateFilePath;
        private readonly ILogger logger;
        private List<PaneWorkspaceState> workspaceStates = new List<PaneWorkspaceState>();
        private int currentWorkspaceIndex = 0;

        public int CurrentWorkspaceIndex => currentWorkspaceIndex;
        public PaneWorkspaceState CurrentWorkspace => GetWorkspace(currentWorkspaceIndex);
        public IReadOnlyList<PaneWorkspaceState> AllWorkspaces => workspaceStates.AsReadOnly();

        public event EventHandler<WorkspaceChangedEventArgs> WorkspaceChanged;

        public PaneWorkspaceManager(ILogger logger, string stateFilePath = null)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrEmpty(stateFilePath))
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var superTUIDir = Path.Combine(appData, "SuperTUI");
                Directory.CreateDirectory(superTUIDir);
                this.stateFilePath = Path.Combine(superTUIDir, "workspaces.json");
            }
            else
            {
                this.stateFilePath = stateFilePath;
            }

            LoadWorkspaces();
        }

        /// <summary>
        /// Get workspace by index, create if doesn't exist
        /// </summary>
        public PaneWorkspaceState GetWorkspace(int index)
        {
            // Ensure workspace exists
            while (workspaceStates.Count <= index)
            {
                var newWorkspace = new PaneWorkspaceState($"Workspace {workspaceStates.Count + 1}", workspaceStates.Count);
                workspaceStates.Add(newWorkspace);
            }

            return workspaceStates[index];
        }

        /// <summary>
        /// Switch to workspace by index
        /// </summary>
        public void SwitchToWorkspace(int index)
        {
            if (index < 0 || index >= 9) // Limit to 9 workspaces (Alt+1-9)
                return;

            var oldIndex = currentWorkspaceIndex;
            currentWorkspaceIndex = index;

            // Ensure workspace exists
            var workspace = GetWorkspace(index);

            logger.Log(LogLevel.Info, "WorkspaceStateManager", $"Switched from workspace {oldIndex} to {index} ({workspace.Name})");
            WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs(oldIndex, index));
        }

        /// <summary>
        /// Update current workspace state
        /// </summary>
        public void UpdateCurrentWorkspace(PaneWorkspaceState state)
        {
            workspaceStates[currentWorkspaceIndex] = state;
            state.LastModified = DateTime.Now;
            logger.Log(LogLevel.Debug, "WorkspaceStateManager", $"Updated workspace {currentWorkspaceIndex} state");
        }

        /// <summary>
        /// Save all workspaces to disk
        /// </summary>
        public void SaveWorkspaces()
        {
            try
            {
                var json = JsonSerializer.Serialize(workspaceStates, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(stateFilePath, json);
                logger.Log(LogLevel.Info, "WorkspaceStateManager", $"Saved {workspaceStates.Count} workspaces to {stateFilePath}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "WorkspaceStateManager", $"Failed to save workspaces: {ex.Message}");
            }
        }

        /// <summary>
        /// Load workspaces from disk
        /// </summary>
        private void LoadWorkspaces()
        {
            try
            {
                if (File.Exists(stateFilePath))
                {
                    var json = File.ReadAllText(stateFilePath);
                    workspaceStates = JsonSerializer.Deserialize<List<PaneWorkspaceState>>(json) ?? new List<PaneWorkspaceState>();
                    logger.Log(LogLevel.Info, "WorkspaceStateManager", $"Loaded {workspaceStates.Count} workspaces from {stateFilePath}");
                }
                else
                {
                    // Create default workspaces
                    workspaceStates = new List<PaneWorkspaceState>
                    {
                        new PaneWorkspaceState("Main", 0),
                        new PaneWorkspaceState("Tasks", 1),
                        new PaneWorkspaceState("Processing", 2)
                    };
                    logger.Log(LogLevel.Info, "WorkspaceStateManager", "Created default workspaces");
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "WorkspaceStateManager", $"Failed to load workspaces: {ex.Message}");
                workspaceStates = new List<PaneWorkspaceState>
                {
                    new PaneWorkspaceState("Main", 0)
                };
            }
        }
    }

    /// <summary>
    /// Event args for workspace change
    /// </summary>
    public class WorkspaceChangedEventArgs : EventArgs
    {
        public int OldIndex { get; }
        public int NewIndex { get; }

        public WorkspaceChangedEventArgs(int oldIndex, int newIndex)
        {
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }
    }
}
