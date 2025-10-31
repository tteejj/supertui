using System;

namespace SuperTUI.Core.Events
{
    // ============================================================================
    // WORKSPACE EVENTS
    // ============================================================================

    public class WorkspaceChangedEvent
    {
        public string WorkspaceName { get; set; }
        public int WorkspaceIndex { get; set; }
        public int WidgetCount { get; set; }
    }

    public class WorkspaceCreatedEvent
    {
        public string WorkspaceName { get; set; }
        public int WorkspaceIndex { get; set; }
    }

    public class WorkspaceRemovedEvent
    {
        public string WorkspaceName { get; set; }
        public int WorkspaceIndex { get; set; }
    }

    // ============================================================================
    // WIDGET EVENTS
    // ============================================================================

    public class WidgetActivatedEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
        public string WorkspaceName { get; set; }
    }

    public class WidgetDeactivatedEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
    }

    public class WidgetFocusReceivedEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
    }

    public class WidgetFocusLostEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
    }

    public class WidgetRefreshedEvent
    {
        public string WidgetName { get; set; }
        public Guid WidgetId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // ============================================================================
    // THEME EVENTS
    // ============================================================================

    public class ThemeChangedEvent
    {
        public string OldThemeName { get; set; }
        public string NewThemeName { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class ThemeLoadedEvent
    {
        public string ThemeName { get; set; }
        public string FilePath { get; set; }
    }

    // ============================================================================
    // FILE SYSTEM EVENTS
    // ============================================================================

    public class DirectoryChangedEvent
    {
        public string OldPath { get; set; }
        public string NewPath { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class FileSelectedEvent
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public DateTime SelectedAt { get; set; }
    }

    public class FileCreatedEvent
    {
        public string FilePath { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class FileDeletedEvent
    {
        public string FilePath { get; set; }
        public DateTime DeletedAt { get; set; }
    }

    // ============================================================================
    // GIT EVENTS
    // ============================================================================

    public class BranchChangedEvent
    {
        public string Repository { get; set; }
        public string OldBranch { get; set; }
        public string NewBranch { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class CommitCreatedEvent
    {
        public string Repository { get; set; }
        public string CommitHash { get; set; }
        public string Message { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RepositoryStatusChangedEvent
    {
        public string Repository { get; set; }
        public int FilesModified { get; set; }
        public int FilesAdded { get; set; }
        public int FilesDeleted { get; set; }
        public bool HasUncommittedChanges { get; set; }
        public DateTime ChangedAt { get; set; }

        // Additional properties for detailed status
        public string Branch { get; set; }
        public int ModifiedFiles { get; set; }
        public int StagedFiles { get; set; }
        public int UntrackedFiles { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // ============================================================================
    // TERMINAL EVENTS
    // ============================================================================

    public class CommandExecutedEvent
    {
        public string Command { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public DateTime ExecutedAt { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class TerminalOutputEvent
    {
        public string Output { get; set; }
        public bool IsError { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class WorkingDirectoryChangedEvent
    {
        public string OldDirectory { get; set; }
        public string NewDirectory { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    // ============================================================================
    // SYSTEM EVENTS
    // ============================================================================

    public class SystemResourcesChangedEvent
    {
        public double CpuUsagePercent { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long MemoryTotalBytes { get; set; }
        public long DiskFreeBytes { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class NetworkActivityEvent
    {
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // ============================================================================
    // TASK/TODO EVENTS
    // ============================================================================

    public class TaskCreatedEvent
    {
        public Guid TaskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TaskCompletedEvent
    {
        public Guid TaskId { get; set; }
        public string Title { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    public class TaskDeletedEvent
    {
        public Guid TaskId { get; set; }
        public DateTime DeletedAt { get; set; }
    }

    public class TaskSelectedEvent
    {
        public Guid TaskId { get; set; }
        public Guid? ProjectId { get; set; }
        public Core.Models.TaskItem Task { get; set; }
        public string SourceWidget { get; set; }
    }

    public class TaskUpdatedEvent
    {
        public Core.Models.TaskItem Task { get; set; }
    }

    public class TaskStatusChangedEvent
    {
        public Guid TaskId { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }

        // Summary properties for bulk status updates
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // ============================================================================
    // NOTIFICATION EVENTS
    // ============================================================================

    public class NotificationEvent
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationLevel Level { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan? Duration { get; set; } // Auto-dismiss duration
    }

    public enum NotificationLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    // ============================================================================
    // COMMAND PALETTE EVENTS
    // ============================================================================

    public class CommandPaletteOpenedEvent
    {
        public DateTime OpenedAt { get; set; }
    }

    public class CommandPaletteClosedEvent
    {
        public DateTime ClosedAt { get; set; }
    }

    public class CommandExecutedFromPaletteEvent
    {
        public string CommandName { get; set; }
        public string CommandCategory { get; set; }
        public DateTime ExecutedAt { get; set; }
    }

    // ============================================================================
    // STATE EVENTS
    // ============================================================================

    public class StateSavedEvent
    {
        public string FilePath { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime SavedAt { get; set; }
    }

    public class StateLoadedEvent
    {
        public string FilePath { get; set; }
        public string Version { get; set; }
        public DateTime LoadedAt { get; set; }
    }

    public class UndoPerformedEvent
    {
        public DateTime PerformedAt { get; set; }
    }

    public class RedoPerformedEvent
    {
        public DateTime PerformedAt { get; set; }
    }

    // ============================================================================
    // CONFIGURATION EVENTS
    // ============================================================================

    public class ConfigurationChangedEvent
    {
        public string Key { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class ConfigurationSavedEvent
    {
        public string FilePath { get; set; }
        public int SettingsCount { get; set; }
        public DateTime SavedAt { get; set; }
    }

    // ============================================================================
    // REQUEST/RESPONSE PATTERNS
    // ============================================================================

    // Request current system stats
    public class GetSystemStatsRequest { }
    public class GetSystemStatsResponse
    {
        public double CpuPercent { get; set; }
        public long MemoryUsed { get; set; }
        public long MemoryTotal { get; set; }
    }

    // Request git status
    public class GetGitStatusRequest
    {
        public string RepositoryPath { get; set; }
    }
    public class GetGitStatusResponse
    {
        public string Branch { get; set; }
        public int FilesChanged { get; set; }
        public bool HasChanges { get; set; }
    }

    // Request task list
    public class GetTaskListRequest
    {
        public bool IncludeCompleted { get; set; }
    }
    public class GetTaskListResponse
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
    }

    // ============================================================================
    // NAVIGATION & PROJECT EVENTS (for Phase 2 EventBus integration)
    // ============================================================================

    public class NavigationRequestedEvent
    {
        public string TargetWidgetType { get; set; }
        public object Context { get; set; }
        public string SourceWidget { get; set; }
    }

    public class ProjectSelectedEvent
    {
        public Core.Models.Project Project { get; set; }
        public string SourceWidget { get; set; }
    }

    public class ProjectChangedEvent
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public ProjectChangeType ChangeType { get; set; }
        public string Source { get; set; }
        public Core.Models.Project Project { get; set; }
    }

    public enum ProjectChangeType
    {
        Created,
        Updated,
        Deleted
    }

    public class RefreshRequestedEvent
    {
        public string TargetWidget { get; set; } // null = all widgets
        public string Reason { get; set; }
    }

    public class FilterChangedEvent
    {
        public SuperTUI.Infrastructure.TaskFilterType Filter { get; set; }
        public string SourceWidget { get; set; }
    }
}

