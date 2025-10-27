using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

// Resolve ambiguity between SuperTUI.Core.Models.TaskStatus and System.Threading.Tasks.TaskStatus
using TaskStatus = SuperTUI.Core.Models.TaskStatus;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Project service for managing projects with JSON persistence
    /// Thread-safe singleton service with nickname and ID1 indexes
    /// </summary>
    public class ProjectService : IProjectService, IDisposable
    {
        private static ProjectService instance;
        public static ProjectService Instance => instance ??= new ProjectService();

        private Dictionary<Guid, Project> projects;
        private Hashtable nicknameIndex; // Nickname -> Guid (case-insensitive)
        private Hashtable id1Index;      // Id1 -> Guid (case-insensitive)
        private string dataFilePath;
        private readonly object lockObject = new object();

        // Save debouncing
        private Timer saveTimer;
        private bool pendingSave = false;
        private const int SAVE_DEBOUNCE_MS = 500;

        // Events for project changes
        public event Action<Project> ProjectAdded;
        public event Action<Project> ProjectUpdated;
        public event Action<Guid> ProjectDeleted;
        public event Action ProjectsReloaded;

        private ProjectService()
        {
            projects = new Dictionary<Guid, Project>();
            nicknameIndex = new Hashtable(StringComparer.OrdinalIgnoreCase);
            id1Index = new Hashtable(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initialize service with data file path
        /// </summary>
        public void Initialize(string filePath = null)
        {
            dataFilePath = filePath ?? Path.Combine(
                Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(),
                "projects.json");

            Logger.Instance?.Info("ProjectService", $"Initializing with data file: {dataFilePath}");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(dataFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Logger.Instance?.Info("ProjectService", $"Created data directory: {directory}");
            }

            // Initialize save timer
            saveTimer = new Timer(SaveTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Load existing projects
            LoadFromFile();
        }

        #region CRUD Operations

        /// <summary>
        /// Get all projects (excluding deleted unless includeDeleted is true)
        /// </summary>
        public List<Project> GetAllProjects(bool includeDeleted = false, bool includeArchived = false)
        {
            lock (lockObject)
            {
                return projects.Values
                    .Where(p => (includeDeleted || !p.Deleted) && (includeArchived || !p.Archived))
                    .OrderBy(p => p.Status)
                    .ThenByDescending(p => p.Priority)
                    .ThenBy(p => p.Name)
                    .ToList();
            }
        }

        /// <summary>
        /// Get projects by filter predicate
        /// </summary>
        public List<Project> GetProjects(Func<Project, bool> predicate)
        {
            lock (lockObject)
            {
                return projects.Values
                    .Where(predicate)
                    .OrderBy(p => p.Status)
                    .ThenByDescending(p => p.Priority)
                    .ThenBy(p => p.Name)
                    .ToList();
            }
        }

        /// <summary>
        /// Get project by ID
        /// </summary>
        public Project GetProject(Guid id)
        {
            lock (lockObject)
            {
                return projects.ContainsKey(id) ? projects[id] : null;
            }
        }

        /// <summary>
        /// Get project by nickname (case-insensitive)
        /// </summary>
        public Project GetProjectByNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return null;

            lock (lockObject)
            {
                if (nicknameIndex.ContainsKey(nickname))
                {
                    var id = (Guid)nicknameIndex[nickname];
                    return projects.ContainsKey(id) ? projects[id] : null;
                }
                return null;
            }
        }

        /// <summary>
        /// Get project by Id1 (case-insensitive)
        /// </summary>
        public Project GetProjectById1(string id1)
        {
            if (string.IsNullOrWhiteSpace(id1))
                return null;

            lock (lockObject)
            {
                if (id1Index.ContainsKey(id1))
                {
                    var id = (Guid)id1Index[id1];
                    return projects.ContainsKey(id) ? projects[id] : null;
                }
                return null;
            }
        }

        /// <summary>
        /// Add new project
        /// </summary>
        public Project AddProject(Project project)
        {
            lock (lockObject)
            {
                // Validate nickname uniqueness
                if (!string.IsNullOrWhiteSpace(project.Nickname) && nicknameIndex.ContainsKey(project.Nickname))
                {
                    Logger.Instance?.Warning("ProjectService", $"Cannot add project: Nickname '{project.Nickname}' already exists");
                    throw new InvalidOperationException($"Project with nickname '{project.Nickname}' already exists");
                }

                // Validate Id1 uniqueness
                if (!string.IsNullOrWhiteSpace(project.Id1) && id1Index.ContainsKey(project.Id1))
                {
                    Logger.Instance?.Warning("ProjectService", $"Cannot add project: Id1 '{project.Id1}' already exists");
                    throw new InvalidOperationException($"Project with Id1 '{project.Id1}' already exists");
                }

                project.Id = Guid.NewGuid();
                project.CreatedAt = DateTime.Now;
                project.UpdatedAt = DateTime.Now;
                project.Deleted = false;

                projects[project.Id] = project;

                // Update indexes
                if (!string.IsNullOrWhiteSpace(project.Nickname))
                    nicknameIndex[project.Nickname] = project.Id;
                if (!string.IsNullOrWhiteSpace(project.Id1))
                    id1Index[project.Id1] = project.Id;

                ScheduleSave();
                ProjectAdded?.Invoke(project);

                Logger.Instance?.Info("ProjectService", $"Added project: {project.Name} (ID: {project.Id})");
                return project;
            }
        }

        /// <summary>
        /// Update existing project
        /// </summary>
        public bool UpdateProject(Project project)
        {
            lock (lockObject)
            {
                if (!projects.ContainsKey(project.Id))
                {
                    Logger.Instance?.Warning("ProjectService", $"Cannot update non-existent project: {project.Id}");
                    return false;
                }

                var oldProject = projects[project.Id];

                // Check if nickname changed and validate uniqueness
                if (oldProject.Nickname != project.Nickname)
                {
                    if (!string.IsNullOrWhiteSpace(project.Nickname) && nicknameIndex.ContainsKey(project.Nickname))
                    {
                        var existingId = (Guid)nicknameIndex[project.Nickname];
                        if (existingId != project.Id)
                        {
                            Logger.Instance?.Warning("ProjectService", $"Cannot update project: Nickname '{project.Nickname}' already exists");
                            throw new InvalidOperationException($"Project with nickname '{project.Nickname}' already exists");
                        }
                    }

                    // Remove old nickname from index
                    if (!string.IsNullOrWhiteSpace(oldProject.Nickname))
                        nicknameIndex.Remove(oldProject.Nickname);

                    // Add new nickname to index
                    if (!string.IsNullOrWhiteSpace(project.Nickname))
                        nicknameIndex[project.Nickname] = project.Id;
                }

                // Check if Id1 changed and validate uniqueness
                if (oldProject.Id1 != project.Id1)
                {
                    if (!string.IsNullOrWhiteSpace(project.Id1) && id1Index.ContainsKey(project.Id1))
                    {
                        var existingId = (Guid)id1Index[project.Id1];
                        if (existingId != project.Id)
                        {
                            Logger.Instance?.Warning("ProjectService", $"Cannot update project: Id1 '{project.Id1}' already exists");
                            throw new InvalidOperationException($"Project with Id1 '{project.Id1}' already exists");
                        }
                    }

                    // Remove old Id1 from index
                    if (!string.IsNullOrWhiteSpace(oldProject.Id1))
                        id1Index.Remove(oldProject.Id1);

                    // Add new Id1 to index
                    if (!string.IsNullOrWhiteSpace(project.Id1))
                        id1Index[project.Id1] = project.Id;
                }

                project.UpdatedAt = DateTime.Now;
                projects[project.Id] = project;

                ScheduleSave();
                ProjectUpdated?.Invoke(project);

                Logger.Instance?.Debug("ProjectService", $"Updated project: {project.Name} (ID: {project.Id})");
                return true;
            }
        }

        /// <summary>
        /// Delete project (soft delete by default)
        /// </summary>
        public bool DeleteProject(Guid id, bool hardDelete = false)
        {
            lock (lockObject)
            {
                if (!projects.ContainsKey(id))
                {
                    Logger.Instance?.Warning("ProjectService", $"Cannot delete non-existent project: {id}");
                    return false;
                }

                var project = projects[id];

                if (hardDelete)
                {
                    // Remove from indexes
                    if (!string.IsNullOrWhiteSpace(project.Nickname))
                        nicknameIndex.Remove(project.Nickname);
                    if (!string.IsNullOrWhiteSpace(project.Id1))
                        id1Index.Remove(project.Id1);

                    projects.Remove(id);
                }
                else
                {
                    // Soft delete
                    project.Deleted = true;
                    project.UpdatedAt = DateTime.Now;
                }

                ScheduleSave();
                ProjectDeleted?.Invoke(id);

                Logger.Instance?.Info("ProjectService", $"Deleted project: {project.Name} (ID: {id}, Hard: {hardDelete})");
                return true;
            }
        }

        /// <summary>
        /// Archive project (toggle)
        /// </summary>
        public bool ArchiveProject(Guid id, bool archived = true)
        {
            lock (lockObject)
            {
                if (!projects.ContainsKey(id))
                {
                    Logger.Instance?.Warning("ProjectService", $"Cannot archive non-existent project: {id}");
                    return false;
                }

                var project = projects[id];
                project.Archived = archived;
                project.UpdatedAt = DateTime.Now;

                ScheduleSave();
                ProjectUpdated?.Invoke(project);

                Logger.Instance?.Info("ProjectService", $"{(archived ? "Archived" : "Unarchived")} project: {project.Name}");
                return true;
            }
        }

        #endregion

        #region Statistics & Integration

        /// <summary>
        /// Get project with integrated task statistics and time tracking
        /// </summary>
        public ProjectWithStats GetProjectWithStats(Guid projectId)
        {
            lock (lockObject)
            {
                var project = GetProject(projectId);
                if (project == null)
                    return null;

                var stats = GetProjectStats(projectId);
                var hoursLogged = TimeTrackingService.Instance?.GetProjectTotalHours(projectId) ?? 0;

                return new ProjectWithStats
                {
                    Project = project,
                    Stats = stats,
                    HoursLogged = hoursLogged
                };
            }
        }

        /// <summary>
        /// Get all projects with statistics
        /// </summary>
        public List<ProjectWithStats> GetProjectsWithStats(bool includeArchived = false)
        {
            lock (lockObject)
            {
                var activeProjects = GetAllProjects(includeDeleted: false, includeArchived: includeArchived);
                return activeProjects.Select(p => GetProjectWithStats(p.Id)).ToList();
            }
        }

        /// <summary>
        /// Get task statistics for a project
        /// </summary>
        public ProjectTaskStats GetProjectStats(Guid projectId)
        {
            var taskService = TaskService.Instance;
            if (taskService == null)
            {
                return new ProjectTaskStats { ProjectId = projectId };
            }

            var tasks = taskService.GetTasks(t => t.ProjectId == projectId && !t.Deleted);

            return new ProjectTaskStats
            {
                ProjectId = projectId,
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == TaskStatus.Completed),
                InProgressTasks = tasks.Count(t => t.Status == TaskStatus.InProgress),
                PendingTasks = tasks.Count(t => t.Status == TaskStatus.Pending),
                OverdueTasks = tasks.Count(t => t.IsOverdue),
                HighPriorityTasks = tasks.Count(t => t.Priority == TaskPriority.High && t.Status != TaskStatus.Completed)
            };
        }

        /// <summary>
        /// Get count of projects matching filter
        /// </summary>
        public int GetProjectCount(Func<Project, bool> predicate = null)
        {
            lock (lockObject)
            {
                if (predicate == null)
                    return projects.Values.Count(p => !p.Deleted);

                return projects.Values.Count(predicate);
            }
        }

        #endregion

        #region Notes & Contacts

        /// <summary>
        /// Add a note to a project
        /// </summary>
        public ProjectNote AddNote(Guid projectId, string content, string author = null)
        {
            lock (lockObject)
            {
                if (!projects.ContainsKey(projectId))
                {
                    Logger.Instance?.Warning("ProjectService", $"Cannot add note: project not found");
                    return null;
                }

                var project = projects[projectId];
                var note = new ProjectNote
                {
                    Content = content,
                    Author = author ?? string.Empty,
                    CreatedAt = DateTime.Now
                };

                project.Notes.Add(note);
                project.UpdatedAt = DateTime.Now;

                ScheduleSave();
                ProjectUpdated?.Invoke(project);

                Logger.Instance?.Info("ProjectService", $"Added note to project: {project.Name}");
                return note;
            }
        }

        /// <summary>
        /// Remove a note from a project
        /// </summary>
        public bool RemoveNote(Guid projectId, Guid noteId)
        {
            lock (lockObject)
            {
                if (!projects.ContainsKey(projectId))
                {
                    Logger.Instance?.Warning("ProjectService", $"Cannot remove note: project not found");
                    return false;
                }

                var project = projects[projectId];
                var note = project.Notes.FirstOrDefault(n => n.Id == noteId);
                if (note == null)
                {
                    Logger.Instance?.Debug("ProjectService", $"Note not found");
                    return false;
                }

                project.Notes.Remove(note);
                project.UpdatedAt = DateTime.Now;

                ScheduleSave();
                ProjectUpdated?.Invoke(project);

                Logger.Instance?.Info("ProjectService", $"Removed note from project: {project.Name}");
                return true;
            }
        }

        /// <summary>
        /// Add a contact to a project
        /// </summary>
        public ProjectContact AddContact(Guid projectId, ProjectContact contact)
        {
            lock (lockObject)
            {
                if (!projects.ContainsKey(projectId))
                {
                    Logger.Instance?.Warning("ProjectService", $"Cannot add contact: project not found");
                    return null;
                }

                var project = projects[projectId];
                contact.Id = Guid.NewGuid();
                project.Contacts.Add(contact);
                project.UpdatedAt = DateTime.Now;

                ScheduleSave();
                ProjectUpdated?.Invoke(project);

                Logger.Instance?.Info("ProjectService", $"Added contact to project: {project.Name}");
                return contact;
            }
        }

        /// <summary>
        /// Remove a contact from a project
        /// </summary>
        public bool RemoveContact(Guid projectId, Guid contactId)
        {
            lock (lockObject)
            {
                if (!projects.ContainsKey(projectId))
                {
                    Logger.Instance?.Warning("ProjectService", $"Cannot remove contact: project not found");
                    return false;
                }

                var project = projects[projectId];
                var contact = project.Contacts.FirstOrDefault(c => c.Id == contactId);
                if (contact == null)
                {
                    Logger.Instance?.Debug("ProjectService", $"Contact not found");
                    return false;
                }

                project.Contacts.Remove(contact);
                project.UpdatedAt = DateTime.Now;

                ScheduleSave();
                ProjectUpdated?.Invoke(project);

                Logger.Instance?.Info("ProjectService", $"Removed contact from project: {project.Name}");
                return true;
            }
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Schedule a debounced save operation
        /// </summary>
        private void ScheduleSave()
        {
            pendingSave = true;
            saveTimer?.Change(SAVE_DEBOUNCE_MS, Timeout.Infinite);
        }

        /// <summary>
        /// Timer callback for debounced save
        /// </summary>
        private void SaveTimerCallback(object state)
        {
            if (pendingSave)
            {
                pendingSave = false;
                Task.Run(async () => await SaveToFileAsync());
            }
        }

        /// <summary>
        /// Save projects to JSON file asynchronously
        /// </summary>
        private async Task SaveToFileAsync()
        {
            try
            {
                // Create backup before saving
                if (File.Exists(dataFilePath))
                {
                    var backupPath = $"{dataFilePath}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                    await Task.Run(() => File.Copy(dataFilePath, backupPath, overwrite: true));

                    // Keep only last 5 backups
                    var backupDir = Path.GetDirectoryName(dataFilePath);
                    var backupFiles = Directory.GetFiles(backupDir, "projects.json.*.bak")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .Skip(5)
                        .ToList();

                    foreach (var oldBackup in backupFiles)
                    {
                        try { File.Delete(oldBackup); } catch { }
                    }
                }

                List<Project> projectList;
                lock (lockObject)
                {
                    projectList = projects.Values.ToList();
                }

                var json = await Task.Run(() => JsonSerializer.Serialize(projectList, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

                await Task.Run(() => File.WriteAllText(dataFilePath, json));
                Logger.Instance?.Debug("ProjectService", $"Saved {projectList.Count} projects to {dataFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("ProjectService", $"Failed to save projects: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Load projects from JSON file
        /// </summary>
        private void LoadFromFile()
        {
            try
            {
                if (!File.Exists(dataFilePath))
                {
                    Logger.Instance?.Info("ProjectService", "No existing project file found, starting fresh");
                    return;
                }

                var json = File.ReadAllText(dataFilePath);
                var loadedProjects = JsonSerializer.Deserialize<List<Project>>(json);

                lock (lockObject)
                {
                    projects.Clear();
                    nicknameIndex.Clear();
                    id1Index.Clear();

                    foreach (var project in loadedProjects)
                    {
                        projects[project.Id] = project;

                        // Rebuild indexes
                        if (!string.IsNullOrWhiteSpace(project.Nickname))
                            nicknameIndex[project.Nickname] = project.Id;
                        if (!string.IsNullOrWhiteSpace(project.Id1))
                            id1Index[project.Id1] = project.Id;
                    }
                }

                Logger.Instance?.Info("ProjectService", $"Loaded {projects.Count} projects from {dataFilePath}");
                ProjectsReloaded?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("ProjectService", $"Failed to load projects: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reload projects from file (useful for external changes)
        /// </summary>
        public void Reload()
        {
            LoadFromFile();
        }

        /// <summary>
        /// Clear all projects (for testing)
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                projects.Clear();
                nicknameIndex.Clear();
                id1Index.Clear();
                ScheduleSave();
                ProjectsReloaded?.Invoke();
                Logger.Instance?.Info("ProjectService", "Cleared all projects");
            }
        }

        #endregion

        #region Export

        /// <summary>
        /// Export projects to JSON format
        /// </summary>
        public bool ExportToJson(string filePath)
        {
            try
            {
                lock (lockObject)
                {
                    var allProjects = projects.Values.Where(p => !p.Deleted).ToList();
                    var json = JsonSerializer.Serialize(allProjects, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(filePath, json);
                    Logger.Instance?.Info("ProjectService", $"Exported {allProjects.Count} projects to JSON: {filePath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance?.Error("ProjectService", $"Failed to export to JSON: {ex.Message}", ex);
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Dispose resources (timer)
        /// </summary>
        public void Dispose()
        {
            if (saveTimer != null)
            {
                // Ensure any pending save is executed before disposal
                if (pendingSave)
                {
                    SaveToFileAsync().Wait();
                }

                saveTimer.Dispose();
                saveTimer = null;
            }
        }
    }
}
