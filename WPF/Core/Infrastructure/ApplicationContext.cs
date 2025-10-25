using System;
using SuperTUI.Core;
using SuperTUI.Core.Models;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Global application context that tracks application-wide state
    /// Widgets can subscribe to changes and coordinate behavior
    /// </summary>
    public class ApplicationContext
    {
        private static ApplicationContext instance;
        public static ApplicationContext Instance => instance ??= new ApplicationContext();

        private Project currentProject;
        private TaskFilterType currentFilter;
        private Workspace currentWorkspace;

        // Events for state changes
        public event Action<Project> ProjectChanged;
        public event Action<TaskFilterType> FilterChanged;
        public event Action<Workspace> WorkspaceChanged;
        public event Action<string, object> NavigationRequested;

        private ApplicationContext()
        {
            // Initialize with default filter
            currentFilter = TaskFilterType.All;
        }

        /// <summary>
        /// Currently selected project (null = all projects)
        /// </summary>
        public Project CurrentProject
        {
            get => currentProject;
            set
            {
                if (currentProject != value)
                {
                    currentProject = value;
                    ProjectChanged?.Invoke(currentProject);
                    Logger.Instance?.Debug("ApplicationContext",
                        $"Current project changed: {currentProject?.Name ?? "(All Projects)"}");
                }
            }
        }

        /// <summary>
        /// Currently active task filter type
        /// </summary>
        public TaskFilterType CurrentFilter
        {
            get => currentFilter;
            set
            {
                if (currentFilter != value)
                {
                    currentFilter = value;
                    FilterChanged?.Invoke(currentFilter);
                    Logger.Instance?.Debug("ApplicationContext",
                        $"Current filter changed: {currentFilter}");
                }
            }
        }

        /// <summary>
        /// Currently active workspace
        /// </summary>
        public Workspace CurrentWorkspace
        {
            get => currentWorkspace;
            set
            {
                if (currentWorkspace != value)
                {
                    currentWorkspace = value;
                    WorkspaceChanged?.Invoke(currentWorkspace);
                    Logger.Instance?.Debug("ApplicationContext",
                        $"Current workspace changed: {currentWorkspace?.Name}");
                }
            }
        }

        /// <summary>
        /// Request navigation to a specific widget with optional context
        /// </summary>
        /// <param name="targetWidgetType">Type name of target widget (e.g., "KanbanBoard")</param>
        /// <param name="context">Optional context object (e.g., TaskItem to select)</param>
        public void RequestNavigation(string targetWidgetType, object context = null)
        {
            NavigationRequested?.Invoke(targetWidgetType, context);
            Logger.Instance?.Debug("ApplicationContext",
                $"Navigation requested: {targetWidgetType} with context: {context?.GetType().Name ?? "none"}");
        }

        /// <summary>
        /// Clear all context (reset to defaults)
        /// </summary>
        public void Reset()
        {
            CurrentProject = null;
            CurrentFilter = TaskFilterType.All;
            Logger.Instance?.Info("ApplicationContext", "Application context reset");
        }
    }

    /// <summary>
    /// Predefined task filter types (for global app filtering)
    /// Note: This is different from Core.Models.TaskFilter which is used for task list filtering
    /// </summary>
    public enum TaskFilterType
    {
        All,
        Active,
        Completed,
        Overdue,
        DueToday,
        DueThisWeek,
        NoDueDate,
        HighPriority
    }
}
