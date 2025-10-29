#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Manages the global project context that flows through all views.
    /// When a project is set, all context-aware panes filter/scope to that project.
    /// </summary>
    public interface IProjectContextManager
    {
        /// <summary>
        /// Currently selected project context (null = no filter)
        /// </summary>
        Project? CurrentProject { get; }

        /// <summary>
        /// Fired when project context changes
        /// </summary>
        event EventHandler<ProjectContextChangedEventArgs>? ProjectContextChanged;

        /// <summary>
        /// Set the current project context
        /// </summary>
        void SetProject(Project? project);

        /// <summary>
        /// Clear project context (show all)
        /// </summary>
        void ClearProject();

        /// <summary>
        /// Search projects with fuzzy matching
        /// </summary>
        List<ProjectSearchResult> SearchProjects(string query);

        /// <summary>
        /// Get project by ID
        /// </summary>
        Project? GetProjectById(int projectId);
    }

    public class ProjectContextManager : IProjectContextManager
    {
        private static readonly Lazy<ProjectContextManager> instance =
            new Lazy<ProjectContextManager>(() => new ProjectContextManager());

        public static ProjectContextManager Instance => instance.Value;

        private readonly IProjectService projectService;
        private readonly ILogger logger;
        private Project? currentProject;

        public Project? CurrentProject => currentProject;

        public event EventHandler<ProjectContextChangedEventArgs>? ProjectContextChanged;

        // DI constructor
        public ProjectContextManager(IProjectService projectService, ILogger logger)
        {
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Singleton constructor
        private ProjectContextManager()
            : this(ProjectService.Instance, Logger.Instance)
        {
        }

        public void SetProject(Project? project)
        {
            var oldProject = currentProject;
            currentProject = project;

            logger.Log(LogLevel.Info, "ProjectContext",
                project != null
                    ? $"Context changed to project: {project.Name} (ID: {project.Id})"
                    : "Context cleared (showing all projects)");

            ProjectContextChanged?.Invoke(this, new ProjectContextChangedEventArgs(oldProject, project));
        }

        public void ClearProject()
        {
            SetProject(null);
        }

        public List<ProjectSearchResult> SearchProjects(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // Return all projects with default score
                return projectService.GetAllProjects()
                    .Select(p => new ProjectSearchResult(p, 0))
                    .ToList();
            }

            var allProjects = projectService.GetAllProjects();
            var results = new List<ProjectSearchResult>();

            foreach (var project in allProjects)
            {
                int score = CalculateFuzzyScore(query.ToLower(), project.Name.ToLower());
                if (score > 0)
                {
                    results.Add(new ProjectSearchResult(project, score));
                }
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        public Project? GetProjectById(int projectId)
        {
            // Note: Project model uses Guid for Id, not int
            // This method signature may need updating
            return null;
        }

        /// <summary>
        /// Fuzzy matching algorithm (inspired by fzf/Sublime Text)
        /// Scores based on:
        /// - Character matches (higher = better)
        /// - Consecutive matches (bonus)
        /// - Match at word start (bonus)
        /// - Case match (bonus)
        /// </summary>
        private int CalculateFuzzyScore(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target))
                return 0;

            int score = 0;
            int queryIndex = 0;
            int consecutiveMatches = 0;
            bool previousWasMatch = false;

            for (int targetIndex = 0; targetIndex < target.Length && queryIndex < query.Length; targetIndex++)
            {
                char queryChar = query[queryIndex];
                char targetChar = target[targetIndex];

                if (queryChar == targetChar)
                {
                    // Base score for match
                    score += 10;

                    // Consecutive match bonus
                    if (previousWasMatch)
                    {
                        consecutiveMatches++;
                        score += consecutiveMatches * 5;
                    }
                    else
                    {
                        consecutiveMatches = 1;
                    }

                    // Word start bonus (after space or at beginning)
                    if (targetIndex == 0 || target[targetIndex - 1] == ' ')
                    {
                        score += 20;
                    }

                    // Case-sensitive match bonus (if original strings had matching case)
                    // We're already comparing lowercase, so skip this for now

                    previousWasMatch = true;
                    queryIndex++;
                }
                else
                {
                    previousWasMatch = false;
                    consecutiveMatches = 0;
                }
            }

            // Must match all query characters
            if (queryIndex != query.Length)
                return 0;

            // Penalize longer targets (prefer shorter, more precise matches)
            score -= (target.Length - query.Length) * 2;

            return Math.Max(0, score);
        }
    }

    public class ProjectContextChangedEventArgs : EventArgs
    {
        public Project? OldProject { get; }
        public Project? NewProject { get; }

        public ProjectContextChangedEventArgs(Project? oldProject, Project? newProject)
        {
            OldProject = oldProject;
            NewProject = newProject;
        }
    }

    public class ProjectSearchResult
    {
        public Project Project { get; }
        public int Score { get; }

        public ProjectSearchResult(Project project, int score)
        {
            Project = project;
            Score = score;
        }
    }
}
