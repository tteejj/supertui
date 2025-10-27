using System;
using System.Collections.Generic;
using SuperTUI.Core.Models;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for project management service
    /// Matches actual ProjectService implementation
    /// </summary>
    public interface IProjectService
    {
        // Events
        event Action<Project> ProjectAdded;
        event Action<Project> ProjectUpdated;
        event Action<Guid> ProjectDeleted;
        event Action ProjectsReloaded;

        // Initialization
        void Initialize(string filePath = null);

        // Project retrieval
        List<Project> GetAllProjects(bool includeDeleted = false, bool includeArchived = false);
        List<Project> GetProjects(Func<Project, bool> predicate);
        Project GetProject(Guid id);
        Project GetProjectByNickname(string nickname);
        Project GetProjectById1(string id1);
        List<ProjectWithStats> GetProjectsWithStats(bool includeArchived = false);
        ProjectWithStats GetProjectWithStats(Guid projectId);
        int GetProjectCount(Func<Project, bool> predicate = null);

        // Project manipulation
        Project AddProject(Project project);
        bool UpdateProject(Project project);
        bool DeleteProject(Guid id, bool hardDelete = false);
        bool ArchiveProject(Guid id, bool archived = true);

        // Contacts
        ProjectContact AddContact(Guid projectId, ProjectContact contact);
        bool RemoveContact(Guid projectId, Guid contactId);

        // Notes
        ProjectNote AddNote(Guid projectId, string content, string author = null);
        bool RemoveNote(Guid projectId, Guid noteId);

        // Statistics
        ProjectTaskStats GetProjectStats(Guid projectId);

        // Bulk operations
        void Reload();
        void Clear();

        // Export
        bool ExportToJson(string filePath);
    }
}
