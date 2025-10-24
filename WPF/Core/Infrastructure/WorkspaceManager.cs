using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    public class WorkspaceManager
    {
        public ObservableCollection<Workspace> Workspaces { get; private set; }
        public Workspace CurrentWorkspace { get; private set; }

        private ContentControl workspaceContainer;

        public event Action<Workspace> WorkspaceChanged;

        public WorkspaceManager(ContentControl container)
        {
            Workspaces = new ObservableCollection<Workspace>();
            workspaceContainer = container;
        }

        public void AddWorkspace(Workspace workspace)
        {
            Workspaces.Add(workspace);

            // First workspace becomes current
            if (Workspaces.Count == 1)
            {
                SwitchToWorkspace(workspace.Index);
            }
        }

        public void RemoveWorkspace(int index)
        {
            var workspace = Workspaces.FirstOrDefault(w => w.Index == index);
            if (workspace != null)
            {
                workspace.Deactivate();
                Workspaces.Remove(workspace);

                // Dispose of workspace resources
                workspace.Dispose();
            }
        }

        public void SwitchToWorkspace(int index)
        {
            var workspace = Workspaces.FirstOrDefault(w => w.Index == index);
            if (workspace != null && workspace != CurrentWorkspace)
            {
                // Deactivate current (preserves state)
                CurrentWorkspace?.Deactivate();

                // Activate new
                CurrentWorkspace = workspace;
                CurrentWorkspace.Activate();

                // Update UI
                workspaceContainer.Content = CurrentWorkspace.GetContainer();

                // Notify listeners
                WorkspaceChanged?.Invoke(CurrentWorkspace);
            }
        }

        public void SwitchToNext()
        {
            if (Workspaces.Count == 0) return;

            if (CurrentWorkspace == null)
            {
                SwitchToWorkspace(Workspaces[0].Index);
                return;
            }

            int currentIndex = Workspaces.IndexOf(CurrentWorkspace);
            if (currentIndex < 0) currentIndex = 0; // Fallback if not found
            int nextIndex = (currentIndex + 1) % Workspaces.Count;
            SwitchToWorkspace(Workspaces[nextIndex].Index);

            Logger.Instance?.Debug("WorkspaceManager", $"Switched to next workspace: {Workspaces[nextIndex].Name}");
        }

        public void SwitchToPrevious()
        {
            if (Workspaces.Count == 0) return;

            if (CurrentWorkspace == null)
            {
                SwitchToWorkspace(Workspaces[0].Index);
                return;
            }

            int currentIndex = Workspaces.IndexOf(CurrentWorkspace);
            if (currentIndex < 0) currentIndex = 0; // Fallback if not found
            int prevIndex = (currentIndex - 1 + Workspaces.Count) % Workspaces.Count;
            SwitchToWorkspace(Workspaces[prevIndex].Index);

            Logger.Instance?.Debug("WorkspaceManager", $"Switched to previous workspace: {Workspaces[prevIndex].Name}");
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            CurrentWorkspace?.HandleKeyDown(e);
        }

        /// <summary>
        /// Dispose all workspaces and cleanup resources
        /// Call this when the application is closing
        /// </summary>
        public void Dispose()
        {
            Logger.Instance?.Info("WorkspaceManager", "Disposing all workspaces");

            foreach (var workspace in Workspaces.ToList())
            {
                try
                {
                    workspace.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Instance?.Error("WorkspaceManager", $"Error disposing workspace {workspace.Name}: {ex.Message}", ex);
                }
            }

            Workspaces.Clear();
            CurrentWorkspace = null;
            workspaceContainer.Content = null;
        }
    }

    // ============================================================================
    // SERVICE CONTAINER
    // ============================================================================

    /// <summary>
    /// Simple dependency injection container for services
    /// Services are singleton and shared across all widgets/screens
}
