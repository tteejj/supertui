using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Threading.Tasks;
using SuperTUI.Core;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Extensions
{
    /// <summary>
    /// Helper for creating directories with automatic fallback to alternative locations
    /// </summary>
    public static class DirectoryHelper
    {
        /// <summary>
        /// Creates a directory with fallback chain if primary path fails.
        /// Fallback order:
        /// 1. Primary path (user-specified)
        /// 2. User's LocalAppData\SuperTUI\{purpose}
        /// 3. User's Temp\SuperTUI\{purpose}
        /// 4. Current directory\.supertui\{purpose}
        /// 5. Temp directory with unique name
        /// </summary>
        /// <param name="primaryPath">Primary directory path to create</param>
        /// <param name="purpose">Purpose/subdirectory name for fallback paths (e.g., "Logs", "Config", "State")</param>
        /// <returns>Successfully created directory path</returns>
        /// <exception cref="IOException">If all fallback attempts fail</exception>
        public static string CreateDirectoryWithFallback(string primaryPath, string purpose)
        {
            var logger = Infrastructure.Logger.Instance;

            // Try primary path first
            try
            {
                Directory.CreateDirectory(primaryPath);
                logger.Debug("DirectoryHelper", $"Created directory: {primaryPath}");
                return primaryPath;
            }
            catch (Exception primaryEx)
            {
                logger.Warning("DirectoryHelper",
                    $"Failed to create primary directory '{primaryPath}': {primaryEx.Message}. Trying fallbacks...");
            }

            // Define fallback paths
            var fallbacks = new List<(string path, string description)>();

            // Fallback 1: LocalAppData\SuperTUI\{purpose}
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    var fallback1 = Path.Combine(localAppData, "SuperTUI", purpose ?? "Data");
                    fallbacks.Add((fallback1, "User's LocalAppData"));
                }
            }
            catch (Exception ex)
            {
                logger?.Debug("DirectoryHelper", $"Failed to get LocalAppData fallback: {ex.Message}");
            }

            // Fallback 2: Temp\SuperTUI\{purpose}
            try
            {
                var tempPath = Path.GetTempPath();
                if (!string.IsNullOrEmpty(tempPath))
                {
                    var fallback2 = Path.Combine(tempPath, "SuperTUI", purpose ?? "Data");
                    fallbacks.Add((fallback2, "User's Temp directory"));
                }
            }
            catch (Exception ex)
            {
                logger?.Debug("DirectoryHelper", $"Failed to get Temp fallback: {ex.Message}");
            }

            // Fallback 3: Current directory\.supertui\{purpose}
            try
            {
                var currentDir = Directory.GetCurrentDirectory();
                if (!string.IsNullOrEmpty(currentDir))
                {
                    var fallback3 = Path.Combine(currentDir, ".supertui", purpose ?? "Data");
                    fallbacks.Add((fallback3, "Current directory"));
                }
            }
            catch (Exception ex)
            {
                logger?.Debug("DirectoryHelper", $"Failed to get CurrentDirectory fallback: {ex.Message}");
            }

            // Fallback 4: Temp with unique name
            try
            {
                var tempPath = Path.GetTempPath();
                if (!string.IsNullOrEmpty(tempPath))
                {
                    var uniqueName = $"SuperTUI_{purpose}_{Guid.NewGuid():N}";
                    var fallback4 = Path.Combine(tempPath, uniqueName);
                    fallbacks.Add((fallback4, "Temp directory with unique name"));
                }
            }
            catch (Exception ex)
            {
                logger?.Debug("DirectoryHelper", $"Failed to get Temp unique fallback: {ex.Message}");
            }

            // Try each fallback
            foreach (var (fallbackPath, description) in fallbacks)
            {
                try
                {
                    Directory.CreateDirectory(fallbackPath);
                    logger.Warning("DirectoryHelper",
                        $"Using fallback directory: {fallbackPath} ({description})");
                    logger.Warning("DirectoryHelper",
                        $"Original path '{primaryPath}' was not accessible.");
                    return fallbackPath;
                }
                catch (Exception fallbackEx)
                {
                    logger.Debug("DirectoryHelper",
                        $"Fallback '{description}' at '{fallbackPath}' failed: {fallbackEx.Message}");
                }
            }

            // All fallbacks failed - this is critical
            var errorMessage =
                $"CRITICAL: Unable to create directory for '{purpose}'.\n" +
                $"Primary path: {primaryPath}\n" +
                $"All {fallbacks.Count} fallback locations also failed.\n" +
                $"Please check:\n" +
                $"  1. Disk space availability\n" +
                $"  2. File system permissions\n" +
                $"  3. Disk health (run chkdsk)\n" +
                $"  4. Antivirus/security software blocking writes\n" +
                $"Application cannot continue without writable storage.";

            logger.Critical("DirectoryHelper", errorMessage);
            throw new IOException(errorMessage);
        }
    }

    /// <summary>
    /// Helper for portable data directory paths
    /// </summary>
    public static class PortableDataDirectory
    {
        private static string dataDirectory;

        /// <summary>
        /// Get or set the data directory. If not set, returns a directory next to the current directory.
        /// </summary>
        public static string DataDirectory
        {
            get
            {
                if (dataDirectory == null)
                {
                    // Default: .data folder in current directory
                    dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".data");
                }

                if (!Directory.Exists(dataDirectory))
                {
                    dataDirectory = DirectoryHelper.CreateDirectoryWithFallback(dataDirectory, "Data");
                }

                return dataDirectory;
            }
            set
            {
                dataDirectory = value;
                if (!Directory.Exists(dataDirectory))
                {
                    dataDirectory = DirectoryHelper.CreateDirectoryWithFallback(dataDirectory, "Data");
                }
            }
        }

        /// <summary>
        /// Get path to SuperTUI data directory
        /// </summary>
        public static string GetSuperTUIDataDirectory()
        {
            var path = Path.Combine(DataDirectory, "SuperTUI");
            if (!Directory.Exists(path))
            {
                path = DirectoryHelper.CreateDirectoryWithFallback(path, "SuperTUI");
            }
            return path;
        }
    }

    // ============================================================================
    // STATE PERSISTENCE SYSTEM
    // ============================================================================
    // Note: StateSnapshot, StateVersion, WorkspaceState, IStateMigration, and StateMigrationManager
    // are now defined in /Core/Models/StateSnapshot.cs

    // Example migration (template for future use)
    /*
    /// <summary>
    /// Example migration from version 1.0 to 1.1
    /// </summary>
    public class Migration_1_0_to_1_1 : IStateMigration
    {
        public string FromVersion => "1.0";
        public string ToVersion => "1.1";

        public StateSnapshot Migrate(StateSnapshot snapshot)
        {
            Logger.Instance.Info("StateMigration", "Migrating from 1.0 to 1.1");

            // Example: Add new field to ApplicationState
            if (!snapshot.ApplicationState.ContainsKey("NewField"))
            {
                snapshot.ApplicationState["NewField"] = "DefaultValue";
            }

            // Example: Transform workspace data
            foreach (var workspace in snapshot.Workspaces)
            {
                if (!workspace.CustomData.ContainsKey("NewWorkspaceField"))
                {
                    workspace.CustomData["NewWorkspaceField"] = 0;
                }
            }

            // Example: Migrate widget states
            foreach (var workspace in snapshot.Workspaces)
            {
                foreach (var widgetState in workspace.WidgetStates)
                {
                    // Rename a field
                    if (widgetState.ContainsKey("OldFieldName"))
                    {
                        widgetState["NewFieldName"] = widgetState["OldFieldName"];
                        widgetState.Remove("OldFieldName");
                    }
                }
            }

            return snapshot;
        }
    }
    */

    /// <summary>
    /// State persistence manager with versioning and backup
    /// </summary>
    public class StatePersistenceManager : IStatePersistenceManager
    {
        private static StatePersistenceManager instance;
        public static StatePersistenceManager Instance => instance ??= new StatePersistenceManager();

        private string stateDirectory;
        private string currentStateFile;
        private StateSnapshot currentState;
        private readonly LinkedList<StateSnapshot> undoHistory = new LinkedList<StateSnapshot>();
        private readonly LinkedList<StateSnapshot> redoHistory = new LinkedList<StateSnapshot>();
        private readonly StateMigrationManager migrationManager = new StateMigrationManager();
        private const int MaxUndoLevels = 50;

        public event EventHandler<StateChangedEventArgs> StateChanged;

        /// <summary>
        /// Get the migration manager for registering custom migrations
        /// </summary>
        public StateMigrationManager MigrationManager => migrationManager;

        public void Initialize(string stateDir = null)
        {
            stateDirectory = stateDir ?? Path.Combine(
                PortableDataDirectory.GetSuperTUIDataDirectory(), "State");

            stateDirectory = DirectoryHelper.CreateDirectoryWithFallback(stateDirectory, "State");
            currentStateFile = Path.Combine(stateDirectory, "current_state.json");

            Logger.Instance.Info("StatePersistence", $"Initialized state persistence at {stateDirectory}");
        }

        public StateSnapshot CaptureState(WorkspaceManager workspaceManager, Dictionary<string, object> customData = null)
        {
            var snapshot = new StateSnapshot
            {
                Timestamp = DateTime.Now,
                ApplicationState = new Dictionary<string, object>
                {
                    ["CurrentWorkspaceIndex"] = workspaceManager.CurrentWorkspace?.Index ?? 0
                },
                UserData = customData ?? new Dictionary<string, object>()
            };

            // Capture workspace states
            foreach (var workspace in workspaceManager.Workspaces)
            {
                var workspaceState = new WorkspaceState
                {
                    Name = workspace.Name,
                    Index = workspace.Index
                };

                // Capture widget states
                foreach (var widget in workspace.Widgets)
                {
                    try
                    {
                        var widgetState = widget.SaveState();
                        workspaceState.WidgetStates.Add(widgetState);
                    }
                    catch (Exception ex)
                    {
                        ErrorHandlingPolicy.Handle(
                            ErrorCategory.Widget,
                            ex,
                            $"Saving state for widget {widget.WidgetName}");
                    }
                }

                snapshot.Workspaces.Add(workspaceState);
            }

            currentState = snapshot;
            Logger.Instance.Debug("StatePersistence", "State snapshot captured");

            return snapshot;
        }

        /// <summary>
        /// Restores application state from a snapshot
        /// Matches widgets by WidgetId ONLY - does not fallback to WidgetName
        /// </summary>
        /// <remarks>
        /// Design Decision: We require WidgetId for state restoration because:
        /// 1. Multiple widgets can have the same name (e.g., "Counter 1", "Counter 2" both named "Counter")
        /// 2. Name-based matching is non-deterministic (depends on widget creation order)
        /// 3. Name-based matching can restore state to the WRONG widget silently
        /// 4. WidgetId is unique per widget instance and never changes
        ///
        /// Legacy states without WidgetId will log a warning and be skipped.
        /// User should save state again to generate WidgetIds for all widgets.
        /// </remarks>
        public void RestoreState(StateSnapshot snapshot, WorkspaceManager workspaceManager)
        {
            try
            {
                Logger.Instance.Info("StatePersistence", "Restoring state from snapshot");

                // Restore workspace states
                foreach (var workspaceState in snapshot.Workspaces)
                {
                    var workspace = workspaceManager.Workspaces.FirstOrDefault(w => w.Index == workspaceState.Index);
                    if (workspace != null)
                    {
                        // Restore widget states by matching WidgetId ONLY
                        // WidgetName fallback removed to prevent ambiguous matching with duplicate names
                        foreach (var widgetState in workspaceState.WidgetStates)
                        {
                            try
                            {
                                // Find widget by ID (required)
                                if (widgetState.TryGetValue("WidgetId", out var widgetIdObj))
                                {
                                    Guid widgetId;

                                    // Handle different serialization formats
                                    if (widgetIdObj is Guid guid)
                                    {
                                        widgetId = guid;
                                    }
                                    else if (widgetIdObj is string guidString && Guid.TryParse(guidString, out var parsedGuid))
                                    {
                                        widgetId = parsedGuid;
                                    }
                                    else
                                    {
                                        Logger.Instance.Warning("StatePersistence",
                                            $"Widget state has invalid WidgetId format: {widgetIdObj?.GetType().Name ?? "null"}");
                                        continue;
                                    }

                                    // Find widget by ID
                                    var widget = workspace.Widgets.FirstOrDefault(w => w.WidgetId == widgetId);
                                    if (widget != null)
                                    {
                                        widget.RestoreState(widgetState);
                                        Logger.Instance.Debug("StatePersistence",
                                            $"Restored widget: {widget.WidgetName} (ID: {widgetId})");
                                    }
                                    else
                                    {
                                        // Widget with this ID doesn't exist (might have been removed)
                                        string widgetName = widgetState.TryGetValue("WidgetName", out var nameObj) ? nameObj?.ToString() : "Unknown";
                                        Logger.Instance.Debug("StatePersistence",
                                            $"Widget '{widgetName}' with ID {widgetId} not found in workspace (may have been removed)");
                                    }
                                }
                                else
                                {
                                    // No WidgetId in saved state - this is a legacy state or corrupted data
                                    string widgetName = widgetState.TryGetValue("WidgetName", out var nameObj) ? nameObj?.ToString() : "Unknown";
                                    string widgetType = widgetState.TryGetValue("WidgetType", out var typeObj) ? typeObj?.ToString() : "Unknown";

                                    Logger.Instance.Warning("StatePersistence",
                                        $"LEGACY STATE DETECTED: Widget '{widgetName}' (type: {widgetType}) has no WidgetId. " +
                                        $"State will NOT be restored. Please save state again to generate WidgetIds.");

                                    // NOTE: We deliberately do NOT attempt name-based matching because:
                                    // 1. It's ambiguous when multiple widgets have the same name
                                    // 2. It's non-deterministic (depends on widget order)
                                    // 3. It leads to subtle bugs that are hard to diagnose
                                    // Instead, we require the user to save state again, which will generate WidgetIds
                                }
                            }
                            catch (Exception ex)
                            {
                                ErrorHandlingPolicy.Handle(
                                    ErrorCategory.Widget,
                                    ex,
                                    "Restoring widget state from snapshot");
                            }
                        }
                    }
                }

                // Restore current workspace
                if (snapshot.ApplicationState.TryGetValue("CurrentWorkspaceIndex", out var currentIndex))
                {
                    workspaceManager.SwitchToWorkspace((int)currentIndex);
                }

                StateChanged?.Invoke(this, new StateChangedEventArgs { Snapshot = snapshot });
                Logger.Instance.Info("StatePersistence", "State restored successfully");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    "Restoring application state from snapshot");
            }
        }

        /// <summary>
        /// Synchronous wrapper for SaveStateAsync - AVOID CALLING FROM UI THREAD
        /// </summary>
        /// <remarks>
        /// ⚠️ DEADLOCK WARNING: This method uses Task.Run().Wait() which can deadlock
        /// when called from WPF UI thread or other SynchronizationContext.
        ///
        /// RECOMMENDED: Use SaveStateAsync() instead and await it.
        ///
        /// This wrapper exists only for backward compatibility with synchronous code paths.
        /// Consider making your call site async to avoid potential deadlocks.
        /// </remarks>
        [Obsolete("Use SaveStateAsync to avoid potential deadlocks on UI thread")]
        public void SaveState(StateSnapshot snapshot = null, bool createBackup = false)
        {
            // DEADLOCK RISK: Task.Run().Wait() blocks calling thread
            // If called from UI thread with .ConfigureAwait(true), this WILL deadlock
            Task.Run(async () => await SaveStateAsync(snapshot, createBackup)).Wait();
        }

        public async Task SaveStateAsync(StateSnapshot snapshot = null, bool createBackup = false)
        {
            snapshot = snapshot ?? currentState;
            if (snapshot == null)
            {
                Logger.Instance.Warning("StatePersistence", "No state to save");
                return;
            }

            try
            {
                // Create backup if requested
                if (createBackup && File.Exists(currentStateFile))
                {
                    await CreateBackupAsync();
                }

                // PHASE 2 FIX: Calculate checksum before saving
                snapshot.Timestamp = DateTime.Now;
                snapshot.CalculateChecksum();

                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(currentStateFile, json, Encoding.UTF8);

                Logger.Instance.Info("StatePersistence", $"State saved to {currentStateFile} (checksum: {snapshot.Checksum?.Substring(0, 8)}...)");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Saving application state to {currentStateFile}");
            }
        }

        /// <summary>
        /// Synchronous wrapper for LoadStateAsync - AVOID CALLING FROM UI THREAD
        /// </summary>
        /// <remarks>
        /// ⚠️ DEADLOCK WARNING: This method uses Task.Run().Result which can deadlock
        /// when called from WPF UI thread or other SynchronizationContext.
        ///
        /// RECOMMENDED: Use LoadStateAsync() instead and await it.
        ///
        /// This wrapper exists only for backward compatibility with synchronous code paths.
        /// Consider making your call site async to avoid potential deadlocks.
        /// </remarks>
        [Obsolete("Use LoadStateAsync to avoid potential deadlocks on UI thread")]
        public StateSnapshot LoadState()
        {
            // DEADLOCK RISK: Task.Run().Result blocks calling thread
            // If called from UI thread with .ConfigureAwait(true), this WILL deadlock
            return Task.Run(async () => await LoadStateAsync()).Result;
        }

        public async Task<StateSnapshot> LoadStateAsync()
        {
            try
            {
                if (!File.Exists(currentStateFile))
                {
                    Logger.Instance.Info("StatePersistence", "No saved state found");
                    return null;
                }

                string json = await File.ReadAllTextAsync(currentStateFile, Encoding.UTF8);
                var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json);

                // PHASE 2 FIX: Verify checksum
                if (!snapshot.VerifyChecksum())
                {
                    Logger.Instance.Error("StatePersistence",
                        $"State file checksum mismatch! File may be corrupted or tampered with.\n" +
                        $"  File: {currentStateFile}\n" +
                        $"  Expected checksum: {snapshot.Checksum?.Substring(0, 16)}...\n" +
                        $"  This could indicate:\n" +
                        $"    - Disk corruption\n" +
                        $"    - Manual file editing\n" +
                        $"    - Incomplete write operation\n" +
                        $"  Attempting to load from backup...");

                    // Try to load most recent backup
                    var backups = GetAvailableBackups();
                    if (backups.Count > 0)
                    {
                        Logger.Instance.Info("StatePersistence", $"Attempting restore from backup: {backups[0]}");
                        try
                        {
                            string backupJson = await File.ReadAllTextAsync(backups[0], Encoding.UTF8);
                            var backupSnapshot = JsonSerializer.Deserialize<StateSnapshot>(backupJson);

                            if (backupSnapshot.VerifyChecksum())
                            {
                                Logger.Instance.Info("StatePersistence", "Successfully restored from backup");
                                return backupSnapshot;
                            }
                            else
                            {
                                Logger.Instance.Warning("StatePersistence", "Backup also has checksum mismatch");
                            }
                        }
                        catch (Exception backupEx)
                        {
                            Logger.Instance.Error("StatePersistence", $"Failed to restore from backup: {backupEx.Message}", backupEx);
                        }
                    }

                    throw new InvalidOperationException(
                        "State file checksum verification failed and no valid backups available. " +
                        "State file may be corrupted.");
                }

                // Check version and migrate if necessary
                if (snapshot.Version != StateVersion.Current)
                {
                    Logger.Instance.Info("StatePersistence",
                        $"State version mismatch. Loaded: {snapshot.Version}, Current: {StateVersion.Current}");

                    // Create backup before migration
                    await CreateBackupAsync();

                    // Perform migration
                    snapshot = migrationManager.MigrateToCurrentVersion(snapshot);

                    // Save migrated state
                    await SaveStateAsync(snapshot, createBackup: false);
                }

                Logger.Instance.Info("StatePersistence", $"State loaded successfully (version {snapshot.Version}, checksum OK)");
                return snapshot;
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Loading application state from {currentStateFile}");
                return null;
            }
        }

        public void PushUndoState(StateSnapshot snapshot)
        {
            // Add to end (most recent)
            undoHistory.AddLast(snapshot);

            // Remove oldest if exceeds max
            if (undoHistory.Count > MaxUndoLevels)
            {
                undoHistory.RemoveFirst();
            }

            // Clear redo history when new action performed
            redoHistory.Clear();
        }

        public StateSnapshot Undo()
        {
            if (undoHistory.Count == 0)
            {
                Logger.Instance.Info("StatePersistence", "No undo history available");
                return null;
            }

            // Get most recent (last)
            var snapshot = undoHistory.Last.Value;
            undoHistory.RemoveLast();

            // Save current state to redo
            if (currentState != null)
            {
                redoHistory.AddLast(currentState);
            }

            Logger.Instance.Info("StatePersistence", "Undo performed");
            return snapshot;
        }

        public StateSnapshot Redo()
        {
            if (redoHistory.Count == 0)
            {
                Logger.Instance.Info("StatePersistence", "No redo history available");
                return null;
            }

            // Get most recent redo
            var snapshot = redoHistory.Last.Value;
            redoHistory.RemoveLast();

            // Save current state to undo
            if (currentState != null)
            {
                undoHistory.AddLast(currentState);
            }

            Logger.Instance.Info("StatePersistence", "Redo performed");
            return snapshot;
        }

        public bool CanUndo => undoHistory.Count > 0;

        public bool CanRedo => redoHistory.Count > 0;

        public void ClearHistory()
        {
            undoHistory.Clear();
            redoHistory.Clear();
            Logger.Instance.Debug("StatePersistence", "Undo/redo history cleared");
        }

        /// <summary>
        /// Create a backup of the current state file - AVOID CALLING FROM UI THREAD
        /// </summary>
        /// <remarks>
        /// ⚠️ DEADLOCK WARNING: This method uses Task.Run().Wait() which can deadlock
        /// when called from WPF UI thread or other SynchronizationContext.
        ///
        /// RECOMMENDED: Use CreateBackupAsync() instead and await it.
        ///
        /// This wrapper exists only for backward compatibility with synchronous code paths.
        /// Consider making your call site async to avoid potential deadlocks.
        /// </remarks>
        [Obsolete("Use CreateBackupAsync to avoid potential deadlocks on UI thread")]
        public void CreateBackup()
        {
            // DEADLOCK RISK: Task.Run().Wait() blocks calling thread
            // If called from UI thread with .ConfigureAwait(true), this WILL deadlock
            Task.Run(async () => await CreateBackupAsync()).Wait();
        }

        private async Task CreateBackupAsync()
        {
            if (!ConfigurationManager.Instance.Get<bool>("Backup.Enabled", true))
                return;

            try
            {
                string backupDir = ConfigurationManager.Instance.Get<string>("Backup.Directory");
                backupDir = DirectoryHelper.CreateDirectoryWithFallback(backupDir, "Backups");

                string backupFile = Path.Combine(backupDir, $"state_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.Copy(currentStateFile, backupFile);

                // Compress if enabled
                if (ConfigurationManager.Instance.Get<bool>("Backup.CompressBackups", true))
                {
                    string zipFile = backupFile + ".gz";
                    using (var input = File.OpenRead(backupFile))
                    using (var output = File.Create(zipFile))
                    using (var gzip = new GZipStream(output, CompressionMode.Compress))
                    {
                        await input.CopyToAsync(gzip);
                    }
                    File.Delete(backupFile);
                    backupFile = zipFile;
                }

                // Clean old backups
                int maxBackups = ConfigurationManager.Instance.Get<int>("Backup.MaxBackups", 10);
                var backups = Directory.GetFiles(backupDir, "state_backup_*")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Skip(maxBackups)
                    .ToList();

                foreach (var old in backups)
                {
                    try
                    {
                        File.Delete(old);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance?.Warning("StatePersistence", $"Failed to delete old backup {old}: {ex.Message}");
                    }
                }

                Logger.Instance.Info("StatePersistence", $"Backup created: {backupFile}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    "Creating state backup");
            }
        }

        public List<string> GetAvailableBackups()
        {
            try
            {
                string backupDir = ConfigurationManager.Instance.Get<string>("Backup.Directory");
                if (!Directory.Exists(backupDir))
                    return new List<string>();

                return Directory.GetFiles(backupDir, "state_backup_*")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList();
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    "Getting list of available backups");
                return new List<string>();
            }
        }

        /// <summary>
        /// Restore from a backup file
        /// </summary>
        public void RestoreFromBackup(string backupFilePath)
        {
            try
            {
                string json;

                if (backupFilePath.EndsWith(".gz"))
                {
                    using (var input = File.OpenRead(backupFilePath))
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzip))
                    {
                        json = reader.ReadToEnd();
                    }
                }
                else
                {
                    json = File.ReadAllText(backupFilePath, Encoding.UTF8);
                }

                var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json);

                // Replace current state file with backup
                if (snapshot != null)
                {
                    currentState = snapshot;
                    SaveState(snapshot);
                    Logger.Instance.Info("StatePersistence", $"Restored from backup: {backupFilePath}");
                }
                else
                {
                    Logger.Instance.Error("StatePersistence", "Backup file contained invalid data");
                }
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Restoring state from backup {backupFilePath}");
            }
        }

        /// <summary>
        /// Load a backup file and return the snapshot (internal helper)
        /// </summary>
        private StateSnapshot LoadBackupSnapshot(string backupPath)
        {
            try
            {
                string json;

                if (backupPath.EndsWith(".gz"))
                {
                    using (var input = File.OpenRead(backupPath))
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzip))
                    {
                        json = reader.ReadToEnd();
                    }
                }
                else
                {
                    json = File.ReadAllText(backupPath, Encoding.UTF8);
                }

                var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json);
                Logger.Instance.Info("StatePersistence", $"Loaded backup snapshot: {backupPath}");
                return snapshot;
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Loading backup snapshot from {backupPath}");
                return null;
            }
        }
    }

    // Note: StateChangedEventArgs is now defined in /Core/Events/StateChangedEventArgs.cs

    // ============================================================================
    // PLUGIN / EXTENSION SYSTEM
    // ============================================================================
    // Note: PluginMetadata, IPlugin, and PluginContext are now defined in:
    // - /Core/Interfaces/IPlugin.cs
    // - /Core/Models/PluginContext.cs

    /// <summary>
    /// Plugin manager for loading and managing extensions.
    ///
    /// ⚠️ SECURITY LIMITATIONS - READ BEFORE USE ⚠️
    ///
    /// CRITICAL LIMITATIONS:
    /// 1. Plugins CANNOT be unloaded once loaded (requires application restart)
    ///    - Assembly.LoadFrom() loads into default AppDomain
    ///    - .NET Framework does not support assembly unloading
    ///    - Repeatedly loading/unloading accumulates assemblies in memory
    ///
    /// 2. Plugins have FULL ACCESS to SuperTUI internals
    ///    - No sandboxing or permission model
    ///    - Plugins can access all framework APIs
    ///    - Malicious plugins can compromise entire application
    ///
    /// 3. No built-in signature verification
    ///    - Plugins are not validated by default
    ///    - Set Security.RequireSignedPlugins=true to enable (optional)
    ///
    /// SECURITY BEST PRACTICES:
    /// - Only load plugins from TRUSTED sources
    /// - Code review plugins before deployment
    /// - Consider requiring signed assemblies in production
    /// - Monitor plugin directory for unauthorized changes
    /// - Log all plugin load attempts for audit
    ///
    /// FUTURE IMPROVEMENTS:
    /// - Migrate to .NET 6+ with AssemblyLoadContext for unloadability
    /// - Add plugin permission model (file access, network, etc.)
    /// - Implement plugin sandboxing via AppDomain (deprecated) or separate process
    /// - Add plugin manifest schema validation
    /// - Add automated plugin scanning (malware detection)
    ///
    /// See PLUGIN_GUIDE.md for plugin development best practices.
    /// </summary>
    public class PluginManager : IPluginManager
    {
        private static PluginManager instance;
        public static PluginManager Instance => instance ??= new PluginManager();

        private readonly Dictionary<string, IPlugin> plugins = new Dictionary<string, IPlugin>();
        private readonly Dictionary<string, Assembly> pluginAssemblies = new Dictionary<string, Assembly>();
        private string pluginsDirectory;
        private PluginContext pluginContext;

        public event EventHandler<PluginEventArgs> PluginLoaded;
        public event EventHandler<PluginEventArgs> PluginUnloaded;

        public void Initialize(string pluginsDir, PluginContext context)
        {
            pluginsDirectory = pluginsDir ?? Path.Combine(
                PortableDataDirectory.GetSuperTUIDataDirectory(), "Plugins");

            pluginContext = context;

            pluginsDirectory = DirectoryHelper.CreateDirectoryWithFallback(pluginsDirectory, "Plugins");

            Logger.Instance.Info("PluginManager", $"Initialized plugin system at {pluginsDirectory}");
        }

        public void LoadPlugins()
        {
            if (!Directory.Exists(pluginsDirectory))
            {
                Logger.Instance.Warning("PluginManager", "Plugins directory not found");
                return;
            }

            try
            {
                // Load DLLs from plugin directory
                var dllFiles = Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories);

                foreach (var dllFile in dllFiles)
                {
                    LoadPlugin(dllFile);
                }

                Logger.Instance.Info("PluginManager", $"Loaded {plugins.Count} plugins");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Plugin,
                    ex,
                    $"Loading plugins from directory {pluginsDirectory}");
            }
        }

        public void LoadPlugin(string assemblyPath)
        {
            try
            {
                // Validate file access
                if (!SecurityManager.Instance.ValidateFileAccess(assemblyPath))
                {
                    Logger.Instance.Warning("PluginManager", $"Security validation failed for plugin: {assemblyPath}");
                    return;
                }

                // WARNING: Assembly.LoadFrom loads into the default AppDomain and CANNOT be unloaded
                // until the application exits. This is a known limitation of .NET Framework.
                // For true plugin unloading, consider migrating to .NET Core/5+ with AssemblyLoadContext,
                // or use separate AppDomains (deprecated in .NET Core).
                // See: https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
                var assembly = Assembly.LoadFrom(assemblyPath);
                pluginAssemblies[assemblyPath] = assembly;

                Logger.Instance.Warning("PluginManager", $"Plugin assembly loaded and will remain in memory until app exit: {assemblyPath}");

                // Find plugin types
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        var plugin = (IPlugin)Activator.CreateInstance(pluginType);

                        // Check if already loaded
                        if (plugins.ContainsKey(plugin.Metadata.Name))
                        {
                            Logger.Instance.Warning("PluginManager", $"Plugin already loaded: {plugin.Metadata.Name}");
                            continue;
                        }

                        // Check dependencies
                        if (!CheckDependencies(plugin.Metadata.Dependencies))
                        {
                            Logger.Instance.Warning("PluginManager", $"Plugin dependencies not met: {plugin.Metadata.Name}");
                            continue;
                        }

                        // Initialize plugin
                        plugin.Initialize(pluginContext);

                        plugins[plugin.Metadata.Name] = plugin;
                        Logger.Instance.Info("PluginManager", $"Loaded plugin: {plugin.Metadata.Name} v{plugin.Metadata.Version}");

                        PluginLoaded?.Invoke(this, new PluginEventArgs { Plugin = plugin });
                    }
                    catch (Exception ex)
                    {
                        ErrorHandlingPolicy.Handle(
                            ErrorCategory.Plugin,
                            ex,
                            $"Instantiating plugin {pluginType.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Plugin,
                    ex,
                    $"Loading plugin assembly from {assemblyPath}");
            }
        }

        private bool CheckDependencies(List<string> dependencies)
        {
            if (dependencies == null || dependencies.Count == 0)
                return true;

            foreach (var dep in dependencies)
            {
                if (!plugins.ContainsKey(dep))
                    return false;
            }

            return true;
        }

        public void UnloadPlugin(string pluginName)
        {
            if (!plugins.TryGetValue(pluginName, out var plugin))
            {
                Logger.Instance.Warning("PluginManager", $"Plugin not found: {pluginName}");
                return;
            }

            try
            {
                plugin.Shutdown();
                plugins.Remove(pluginName);

                Logger.Instance.Info("PluginManager", $"Plugin deactivated: {pluginName}");
                Logger.Instance.Warning("PluginManager", $"Plugin assembly remains in memory (cannot unload in .NET Framework)");
                PluginUnloaded?.Invoke(this, new PluginEventArgs { Plugin = plugin });
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Plugin,
                    ex,
                    $"Unloading plugin {pluginName}");
            }
        }

        public IPlugin GetPlugin(string name)
        {
            return plugins.TryGetValue(name, out var plugin) ? plugin : null;
        }

        /// <summary>
        /// Get all loaded plugins as a read-only dictionary
        /// </summary>
        public IReadOnlyDictionary<string, IPlugin> GetLoadedPlugins()
        {
            return new Dictionary<string, IPlugin>(plugins);
        }

        /// <summary>
        /// Get all loaded plugins as a list (legacy method)
        /// </summary>
        public List<IPlugin> GetAllPlugins()
        {
            return plugins.Values.ToList();
        }

        /// <summary>
        /// Check if a plugin is loaded
        /// </summary>
        public bool IsPluginLoaded(string name)
        {
            return plugins.ContainsKey(name);
        }

        /// <summary>
        /// Execute a plugin command
        /// </summary>
        public void ExecutePluginCommand(string pluginName, string command, params object[] args)
        {
            if (!plugins.TryGetValue(pluginName, out var plugin))
            {
                Logger.Instance.Warning("PluginManager", $"Plugin not found: {pluginName}");
                return;
            }

            try
            {
                // Note: This is a stub implementation
                // Plugins would need to implement a command handler interface for this to work
                Logger.Instance.Warning("PluginManager",
                    $"ExecutePluginCommand not fully implemented. Plugin '{pluginName}' needs ICommandHandler interface.");

                // Future: Check if plugin implements ICommandHandler and invoke
                // if (plugin is ICommandHandler handler)
                // {
                //     handler.ExecuteCommand(command, args);
                // }
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Plugin,
                    ex,
                    $"Executing command '{command}' on plugin '{pluginName}'");
            }
        }

        public void UnloadAll()
        {
            var pluginNames = plugins.Keys.ToList();
            foreach (var name in pluginNames)
            {
                UnloadPlugin(name);
            }
        }
    }

    // Note: PluginEventArgs is now defined in /Core/Events/PluginEventArgs.cs

    // ============================================================================
    // PERFORMANCE MONITORING
    // ============================================================================
    // Note: PerformanceCounter is now defined in /Core/Models/PerformanceCounter.cs

    /// <summary>
    /// Performance monitor for tracking various metrics
    /// </summary>
    public class PerformanceMonitor : IPerformanceMonitor
    {
        private static PerformanceMonitor instance;
        public static PerformanceMonitor Instance => instance ??= new PerformanceMonitor();

        private readonly Dictionary<string, Infrastructure.PerformanceCounter> counters = new Dictionary<string, Infrastructure.PerformanceCounter>();

        public Infrastructure.PerformanceCounter GetCounter(string name)
        {
            if (!counters.TryGetValue(name, out var counter))
            {
                counter = new Infrastructure.PerformanceCounter(name);
                counters[name] = counter;
            }
            return counter;
        }

        public void StartOperation(string name)
        {
            GetCounter(name).Start();
        }

        public void StopOperation(string name)
        {
            var counter = GetCounter(name);
            counter.Stop();

            // Log if operation took too long
            if (counter.LastDuration.TotalMilliseconds > 100)
            {
                Logger.Instance.Warning("Performance", $"Slow operation detected: {name} took {counter.LastDuration.TotalMilliseconds:F2}ms");
            }
        }

        public Dictionary<string, Infrastructure.PerformanceCounter> GetAllCounters()
        {
            return new Dictionary<string, Infrastructure.PerformanceCounter>(counters);
        }

        public void ResetAll()
        {
            foreach (var counter in counters.Values)
            {
                counter.Reset();
            }
        }

        public string GenerateReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Performance Report ===");
            sb.AppendLine();

            foreach (var kvp in counters.OrderByDescending(c => c.Value.AverageDuration))
            {
                var counter = kvp.Value;
                sb.AppendLine($"{counter.Name}:");
                sb.AppendLine($"  Samples: {counter.SampleCount}");
                sb.AppendLine($"  Last: {counter.LastDuration.TotalMilliseconds:F2}ms");
                sb.AppendLine($"  Avg:  {counter.AverageDuration.TotalMilliseconds:F2}ms");
                sb.AppendLine($"  Min:  {counter.MinDuration.TotalMilliseconds:F2}ms");
                sb.AppendLine($"  Max:  {counter.MaxDuration.TotalMilliseconds:F2}ms");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
