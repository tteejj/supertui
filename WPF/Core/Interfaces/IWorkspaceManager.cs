using System;
using System.Collections.ObjectModel;

namespace SuperTUI.Core
{
    /// <summary>
    /// Interface for workspace manager - enables testing and mocking
    /// </summary>
    public interface IWorkspaceManager
    {
        ObservableCollection<Workspace> Workspaces { get; }
        Workspace CurrentWorkspace { get; }
        int CurrentWorkspaceIndex { get; }

        void AddWorkspace(Workspace workspace);
        void RemoveWorkspace(int index);
        void SwitchToWorkspace(int index);
        void NextWorkspace();
        void PreviousWorkspace();
    }
}
