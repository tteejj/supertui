using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SuperTUI.Core;
using SuperTUI.Core.Events;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.DI;
using SuperTUI.Infrastructure;
using SuperTUI.Widgets.Overlays;

namespace SuperTUI
{
    public partial class MainWindow : Window
    {
        private readonly DI.ServiceContainer serviceContainer;
        private readonly WorkspaceManager workspaceManager;
        private Grid workspacePanel;

        public MainWindow(DI.ServiceContainer container)
        {
            serviceContainer = container ?? throw new ArgumentNullException(nameof(container));

            InitializeComponent();

            // Create Grid panel for workspace manager (Panel is required by OverlayManager)
            workspacePanel = new Grid();
            RootContainer.Children.Add(workspacePanel);

            // Create ContentControl within the panel for workspace content
            var workspaceContainer = new ContentControl();
            workspacePanel.Children.Add(workspaceContainer);

            // Initialize overlay manager with root grid and workspace panel
            var overlayManager = OverlayManager.Instance;
            overlayManager.Initialize(RootContainer, workspacePanel);

            // Initialize workspace manager
            workspaceManager = new WorkspaceManager(workspaceContainer);

            // Create default workspace with proper constructor
            var logger = serviceContainer.GetRequiredService<ILogger>();
            var themeManager = serviceContainer.GetRequiredService<IThemeManager>();
            var defaultWorkspace = new Workspace("Main", 0, null, logger, themeManager);

            workspaceManager.AddWorkspace(defaultWorkspace);

            // Create and add the Task Management workspace as the second workspace
            var taskWorkspace = CreateTaskWorkspace(1);
            workspaceManager.AddWorkspace(taskWorkspace);

            workspaceManager.SwitchToWorkspace(0);

            // Set up global keyboard handler for overlays
            this.KeyDown += MainWindow_KeyDown;

            // Set up window close handler
            Closing += MainWindow_Closing;
        }

        private Workspace CreateTaskWorkspace(int index)
        {
            var logger = serviceContainer.GetRequiredService<ILogger>();
            var themeManager = serviceContainer.GetRequiredService<IThemeManager>();
            var configManager = serviceContainer.GetRequiredService<IConfigurationManager>();
            var taskService = serviceContainer.GetRequiredService<ITaskService>();
            var projectService = serviceContainer.GetRequiredService<IProjectService>();
            var tagService = serviceContainer.GetRequiredService<ITagService>();

            // Use a simple layout for the fullscreen widget (1 row, 1 column for fullscreen)
            var layout = new Core.GridLayoutEngine(1, 1, false, logger, themeManager);
            var workspace = new Workspace($"Tasks", index, layout, logger, themeManager);

            var taskWidget = new Widgets.TaskManagementWidget(logger, themeManager, configManager, taskService, projectService, tagService);

            // Add the widget to the workspace with default layout parameters (it will fill the space)
            workspace.AddWidget(taskWidget, new Core.LayoutParams());

            return workspace;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            var overlayManager = OverlayManager.Instance;

            // Global overlay shortcuts - handle before workspace

            // Toggle Fullscreen: Alt+F
            if (e.Key == Key.F && e.KeyboardDevice.Modifiers == ModifierKeys.Alt)
            {
                workspaceManager.CurrentWorkspace?.ToggleFullscreen();
                e.Handled = true;
                return;
            }

            // Command Palette: : or Ctrl+P
            if ((e.Key == Key.OemSemicolon && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift)) ||
                (e.Key == Key.P && e.KeyboardDevice.Modifiers == ModifierKeys.Control))
            {
                var taskService = serviceContainer.GetRequiredService<ITaskService>();
                var projectService = serviceContainer.GetRequiredService<IProjectService>();

                var cmdPalette = new CommandPaletteOverlay(taskService, projectService);
                cmdPalette.CommandExecuted += OnCommandExecuted;
                cmdPalette.Cancelled += () => overlayManager.HideTopZone();
                overlayManager.ShowTopZone(cmdPalette);

                e.Handled = true;
                return;
            }

            // Jump to Anything: Ctrl+J
            if (e.Key == Key.J && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                var taskService = serviceContainer.GetRequiredService<ITaskService>();
                var projectService = serviceContainer.GetRequiredService<IProjectService>();

                // TODO: Get actual available widgets and workspace names from workspace manager
                var jumpOverlay = new JumpToAnythingOverlay(
                    taskService,
                    projectService,
                    null,  // availableWidgets
                    null   // workspaceNames
                );
                jumpOverlay.ItemSelected += OnJumpItemSelected;
                jumpOverlay.Cancelled += () => overlayManager.HideCenterZone();
                overlayManager.ShowCenterZone(jumpOverlay);

                e.Handled = true;
                return;
            }

            // Check if overlay should handle key first
            if (overlayManager.IsAnyOverlayVisible)
            {
                if (overlayManager.HandleKeyDown(e))
                {
                    e.Handled = true;
                    return;  // Overlay consumed the key
                }
            }

            // Delegate to workspace keyboard handling (existing i3-style navigation)
            workspaceManager.CurrentWorkspace?.HandleKeyDown(e);
        }

        private void OnCommandExecuted(Command command)
        {
            var logger = serviceContainer.GetService<ILogger>();
            logger?.Info("MainWindow", $"Executing command: {command.Name}");

            // Hide command palette
            OverlayManager.Instance.HideTopZone();

            // Execute based on command name
            var taskService = serviceContainer.GetService<ITaskService>();
            var projectService = serviceContainer.GetService<IProjectService>();

            switch (command.Name.ToLower())
            {
                // Task commands
                case "create task":
                    if (taskService != null && projectService != null)
                    {
                        var quickAdd = new QuickAddTaskOverlay(taskService, projectService);
                        quickAdd.TaskCreated += (task) => logger?.Info("MainWindow", $"Created task: {task.Title}");
                        quickAdd.Cancelled += () => OverlayManager.Instance.HideBottomZone();
                        OverlayManager.Instance.ShowBottomZone(quickAdd);
                    }
                    break;

                // Filter commands
                case "filter active":
                    if (taskService != null && projectService != null)
                    {
                        var filter = new FilterPanelOverlay(taskService, projectService);
                        filter.FilterChanged += (predicate) => logger?.Info("MainWindow", "Filter applied");
                        OverlayManager.Instance.ShowLeftZone(filter);
                        // Auto-select "Active" filter
                    }
                    break;

                case "filter completed":
                case "filter today":
                case "filter overdue":
                case "filter high":
                    if (taskService != null && projectService != null)
                    {
                        var filter = new FilterPanelOverlay(taskService, projectService);
                        filter.FilterChanged += (predicate) => logger?.Info("MainWindow", $"Filter applied: {command.Name}");
                        OverlayManager.Instance.ShowLeftZone(filter);
                    }
                    break;

                case "clear filters":
                    logger?.Info("MainWindow", "Filters cleared");
                    // TODO: Broadcast event to clear filters
                    break;

                // Navigation commands
                case "goto tasks":
                case "goto projects":
                case "goto kanban":
                case "goto agenda":
                    logger?.Info("MainWindow", $"Navigation to: {command.Name}");
                    // TODO: Implement workspace/widget navigation
                    break;

                case "jump":
                    if (taskService != null && projectService != null)
                    {
                        var jumpOverlay = new JumpToAnythingOverlay(taskService, projectService, null, null);
                        jumpOverlay.ItemSelected += OnJumpItemSelected;
                        jumpOverlay.Cancelled += () => OverlayManager.Instance.HideCenterZone();
                        OverlayManager.Instance.ShowCenterZone(jumpOverlay);
                    }
                    break;

                // View commands
                case "view list":
                case "view kanban":
                case "view timeline":
                case "view calendar":
                case "view table":
                    logger?.Info("MainWindow", $"View change to: {command.Name}");
                    // TODO: Implement view switching
                    break;

                // Workspace commands
                case string s when s.StartsWith("workspace "):
                    if (int.TryParse(s.Substring("workspace ".Length), out int wsIndex) && wsIndex >= 1 && wsIndex <= 9)
                    {
                        workspaceManager?.SwitchToWorkspace(wsIndex - 1);
                        logger?.Info("MainWindow", $"Switched to workspace {wsIndex}");
                    }
                    break;

                // Sort commands
                case "sort priority":
                case "sort duedate":
                case "sort title":
                case "sort created":
                case "sort updated":
                    logger?.Info("MainWindow", $"Sort applied: {command.Name}");
                    // TODO: Broadcast sort event
                    break;

                // Group commands
                case "group status":
                case "group priority":
                case "group project":
                case "group none":
                    logger?.Info("MainWindow", $"Grouping applied: {command.Name}");
                    // TODO: Broadcast grouping event
                    break;

                // Project commands
                case string s when s.StartsWith("project "):
                    var projectName = s.Substring("project ".Length);
                    logger?.Info("MainWindow", $"Switched to project: {projectName}");
                    // TODO: Apply project filter
                    break;

                // System commands
                case "help":
                    logger?.Info("MainWindow", "Show help requested");
                    MessageBox.Show(
                        "SuperTUI Keyboard Shortcuts:\n\n" +
                        "Global:\n" +
                        "  : or Ctrl+P - Command Palette\n" +
                        "  Ctrl+J - Jump to Anything\n" +
                        "  Esc - Close Overlay\n\n" +
                        "Task Widget:\n" +
                        "  n - Quick Add Task\n" +
                        "  / - Filter Panel\n" +
                        "  ↑↓ - Navigate + Show Detail\n\n" +
                        "i3-Style:\n" +
                        "  Alt+Arrow - Navigate Widgets\n" +
                        "  Alt+Shift+Arrow - Move Widgets\n" +
                        "  Alt+1-9 - Switch Workspace\n" +
                        "  Tab - Cycle Widget Focus",
                        "SuperTUI Help",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                case "settings":
                    logger?.Info("MainWindow", "Settings requested");
                    // TODO: Show settings overlay
                    break;

                case "theme":
                    logger?.Info("MainWindow", "Theme picker requested");
                    // TODO: Show theme picker overlay
                    break;

                default:
                    logger?.Warning("MainWindow", $"Unknown command: {command.Name}");
                    break;
            }
        }

        private void OnJumpItemSelected(JumpItem item)
        {
            var logger = serviceContainer.GetService<ILogger>();
            logger?.Info("MainWindow", $"Jumping to: {item.Type} - {item.Name}");

            // Hide jump overlay
            OverlayManager.Instance.HideCenterZone();

            // Navigate based on item type
            switch (item.Type)
            {
                case JumpItemType.Task:
                    if (item.Data is TaskItem task)
                    {
                        // Broadcast navigation event to TaskManagementWidget
                        Core.EventBus.Instance.Publish(new NavigationRequestedEvent
                        {
                            TargetWidgetType = "TaskManagement",
                            Context = task
                        });

                        // Show task detail overlay
                        var detail = new TaskDetailOverlay(task);
                        OverlayManager.Instance.ShowRightZone(detail);

                        logger?.Info("MainWindow", $"Navigated to task: {task.Title}");
                    }
                    break;

                case JumpItemType.Project:
                    if (item.Data is Project project)
                    {
                        // Apply project filter via FilterPanelOverlay
                        var taskService = serviceContainer.GetService<ITaskService>();
                        var projectService = serviceContainer.GetService<IProjectService>();

                        if (taskService != null && projectService != null)
                        {
                            var filter = new FilterPanelOverlay(taskService, projectService);
                            filter.FilterChanged += (predicate) =>
                            {
                                logger?.Info("MainWindow", $"Filtered to project: {project.Name}");
                            };
                            OverlayManager.Instance.ShowLeftZone(filter);
                            // TODO: Auto-select this project in filter
                        }

                        logger?.Info("MainWindow", $"Navigated to project: {project.Name}");
                    }
                    break;

                case JumpItemType.Widget:
                    if (item.Data is WidgetBase widget)
                    {
                        // Focus widget in current workspace
                        var currentWorkspace = workspaceManager?.CurrentWorkspace;
                        if (currentWorkspace != null)
                        {
                            // TODO: Implement widget focus by reference
                            logger?.Info("MainWindow", $"Navigated to widget: {widget.WidgetName}");
                        }
                    }
                    break;

                case JumpItemType.Workspace:
                    if (item.Data is int workspaceIndex)
                    {
                        workspaceManager?.SwitchToWorkspace(workspaceIndex);
                        logger?.Info("MainWindow", $"Navigated to workspace {workspaceIndex + 1}");
                    }
                    break;

                default:
                    logger?.Warning("MainWindow", $"Unknown jump item type: {item.Type}");
                    break;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Save state before closing
                var statePersistence = serviceContainer.GetService<IStatePersistenceManager>();
                if (statePersistence != null)
                {
                    // Use Task.Run to avoid UI thread deadlock, then wait
                    var saveTask = Task.Run(async () => await statePersistence.SaveStateAsync(null, false));

                    // Show a brief progress dialog if save takes too long
                    if (!saveTask.Wait(TimeSpan.FromSeconds(5)))
                    {
                        var result = MessageBox.Show(
                            "Saving state is taking longer than expected.\n\nWait for save to complete?",
                            "SuperTUI",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            saveTask.Wait(); // Wait indefinitely
                        }
                        else
                        {
                            // Let user exit without waiting
                            // Note: Save will continue in background but may not complete
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = serviceContainer.GetService<ILogger>();
                logger?.Error("MainWindow", $"Error saving state on close: {ex.Message}", ex);

                var result = MessageBox.Show(
                    $"Failed to save application state:\n\n{ex.Message}\n\nExit anyway?",
                    "SuperTUI Save Error",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
