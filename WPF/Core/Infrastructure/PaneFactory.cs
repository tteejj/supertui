using System;
using System.Collections.Generic;
using System.Linq;
using SuperTUI.Core.Commands;
using SuperTUI.Core.Components;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;
using SuperTUI.Panes;
using SuperTUI.Widgets;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Metadata for pane types
    /// </summary>
    public class PaneMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public Func<PaneBase> Creator { get; set; }
        public bool HiddenFromPalette { get; set; } = false;
    }

    /// <summary>
    /// Factory for creating panes by name
    /// Used by command palette and keyboard shortcuts
    /// </summary>
    public class PaneFactory
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IProjectContextManager projectContext;
        private readonly IConfigurationManager configManager;
        private readonly ISecurityManager securityManager;
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly ITimeTrackingService timeTrackingService;
        private readonly ITagService tagService;
        private readonly IEventBus eventBus;
        private readonly CommandHistory commandHistory;
        private readonly FocusHistoryManager focusHistory;

        private readonly Dictionary<string, PaneMetadata> paneRegistry;

        public PaneFactory(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager configManager,
            ISecurityManager securityManager,
            ITaskService taskService,
            IProjectService projectService,
            ITimeTrackingService timeTrackingService,
            ITagService tagService,
            IEventBus eventBus,
            CommandHistory commandHistory,
            FocusHistoryManager focusHistory)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
            this.configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            this.securityManager = securityManager ?? throw new ArgumentNullException(nameof(securityManager));
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.timeTrackingService = timeTrackingService ?? throw new ArgumentNullException(nameof(timeTrackingService));
            this.tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.commandHistory = commandHistory ?? throw new ArgumentNullException(nameof(commandHistory));
            this.focusHistory = focusHistory ?? throw new ArgumentNullException(nameof(focusHistory));

            paneRegistry = new Dictionary<string, PaneMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["tasks"] = new PaneMetadata
                {
                    Name = "tasks",
                    Description = "View and manage tasks",
                    Icon = "✓",
                    Creator = () =>
                    {
                        var pane = new TaskListPane(logger, themeManager, projectContext, taskService, eventBus, commandHistory);
                        SetFocusHistory(pane);
                        return pane;
                    }
                },
                ["notes"] = new PaneMetadata
                {
                    Name = "notes",
                    Description = "Browse and edit notes",
                    Icon = "📝",
                    Creator = () =>
                    {
                        var pane = new NotesPane(logger, themeManager, projectContext, configManager, eventBus);
                        SetFocusHistory(pane);
                        return pane;
                    }
                },
                ["files"] = new PaneMetadata
                {
                    Name = "files",
                    Description = "Browse and select files/directories (internal use only)",
                    Icon = "📁",
                    Creator = () =>
                    {
                        var pane = new FileBrowserPane(logger, themeManager, projectContext, configManager, securityManager);
                        SetFocusHistory(pane);
                        return pane;
                    },
                    HiddenFromPalette = true
                },
                ["projects"] = new PaneMetadata
                {
                    Name = "projects",
                    Description = "Manage projects with full CRUD and Excel integration",
                    Icon = "📊",
                    Creator = () =>
                    {
                        var pane = new ProjectsPane(logger, themeManager, projectContext, configManager, projectService, eventBus);
                        SetFocusHistory(pane);
                        return pane;
                    }
                },
                ["excel-import"] = new PaneMetadata
                {
                    Name = "excel-import",
                    Description = "Import projects from Excel clipboard",
                    Icon = "📋",
                    Creator = () =>
                    {
                        var pane = new ExcelImportPane(logger, themeManager, projectContext, projectService, ExcelMappingService.Instance, eventBus);
                        SetFocusHistory(pane);
                        return pane;
                    }
                },
                ["help"] = new PaneMetadata
                {
                    Name = "help",
                    Description = "Keyboard shortcuts reference",
                    Icon = "⌨️",
                    Creator = () =>
                    {
                        var pane = new HelpPane(logger, themeManager, projectContext);
                        SetFocusHistory(pane);
                        return pane;
                    }
                },
                ["calendar"] = new PaneMetadata
                {
                    Name = "calendar",
                    Description = "Calendar view of tasks by due date",
                    Icon = "📅",
                    Creator = () =>
                    {
                        var pane = new CalendarPane(logger, themeManager, projectContext, taskService, projectService);
                        SetFocusHistory(pane);
                        return pane;
                    }
                }
            };
        }

        /// <summary>
        /// Set focus history on a pane using reflection (since focusHistory is optional in PaneBase constructor)
        /// </summary>
        private void SetFocusHistory(PaneBase pane)
        {
            var field = typeof(PaneBase).GetField("focusHistory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(pane, focusHistory);
            }
        }

        /// <summary>
        /// Create a pane by name
        /// </summary>
        public PaneBase CreatePane(string paneName)
        {
            if (string.IsNullOrWhiteSpace(paneName))
                throw new ArgumentException("Pane name cannot be empty", nameof(paneName));

            paneName = paneName.Trim().ToLowerInvariant();

            if (paneRegistry.TryGetValue(paneName, out var metadata))
            {
                var pane = metadata.Creator();
                logger.Log(LogLevel.Info, "PaneFactory", $"Created pane: {paneName}");
                return pane;
            }

            logger.Log(LogLevel.Warning, "PaneFactory", $"Unknown pane type: {paneName}");
            throw new ArgumentException($"Unknown pane type: {paneName}");
        }

        /// <summary>
        /// Get all available pane types
        /// </summary>
        public IEnumerable<string> GetAvailablePaneTypes()
        {
            return paneRegistry.Keys;
        }

        public IEnumerable<string> GetPaletteVisiblePaneTypes()
        {
            return paneRegistry.Where(kvp => !kvp.Value.HiddenFromPalette).Select(kvp => kvp.Key);
        }

        /// <summary>
        /// Get all pane metadata
        /// </summary>
        public IEnumerable<PaneMetadata> GetAllPaneMetadata()
        {
            return paneRegistry.Values;
        }

        /// <summary>
        /// Get metadata for a specific pane type
        /// </summary>
        public PaneMetadata GetPaneMetadata(string paneName)
        {
            if (string.IsNullOrWhiteSpace(paneName))
                return null;

            paneName = paneName.Trim().ToLowerInvariant();
            return paneRegistry.TryGetValue(paneName, out var metadata) ? metadata : null;
        }

        /// <summary>
        /// Check if a pane type exists
        /// </summary>
        public bool HasPaneType(string paneName)
        {
            if (string.IsNullOrWhiteSpace(paneName))
                return false;

            return paneRegistry.ContainsKey(paneName.Trim().ToLowerInvariant());
        }

        /// <summary>
        /// Register a custom pane type with metadata
        /// </summary>
        public void RegisterPaneType(string paneName, string description, string icon, Func<PaneBase> creator)
        {
            if (string.IsNullOrWhiteSpace(paneName))
                throw new ArgumentException("Pane name cannot be empty", nameof(paneName));

            if (creator == null)
                throw new ArgumentNullException(nameof(creator));

            paneName = paneName.Trim().ToLowerInvariant();
            paneRegistry[paneName] = new PaneMetadata
            {
                Name = paneName,
                Description = description ?? $"Open {paneName} pane",
                Icon = icon ?? "📄",
                Creator = creator
            };

            logger.Log(LogLevel.Info, "PaneFactory", $"Registered custom pane type: {paneName}");
        }

        /// <summary>
        /// Register a custom pane creator (legacy, no metadata)
        /// </summary>
        public void RegisterPaneType(string paneName, Func<PaneBase> creator)
        {
            RegisterPaneType(paneName, null, null, creator);
        }
    }
}
