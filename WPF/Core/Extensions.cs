using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using SuperTUI.Core;
using SuperTUI.Infrastructure;

namespace SuperTUI.Extensions
{
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
                    Directory.CreateDirectory(dataDirectory);
                }

                return dataDirectory;
            }
            set
            {
                dataDirectory = value;
                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
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
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }

    // ============================================================================
    // STATE PERSISTENCE SYSTEM
    // ============================================================================

    /// <summary>
    /// State snapshot for persistence with versioning support
    /// </summary>
    public class StateSnapshot
    {
        /// <summary>
        /// Schema version of this state snapshot. Format: "major.minor"
        /// Breaking changes increment major, compatible changes increment minor.
        /// </summary>
        public string Version { get; set; } = StateVersion.Current;

        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> ApplicationState { get; set; } = new Dictionary<string, object>();
        public List<WorkspaceState> Workspaces { get; set; } = new List<WorkspaceState>();
        public Dictionary<string, object> UserData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// State version constants and comparison utilities
    /// </summary>
    public static class StateVersion
    {
        public const string Current = "1.0";

        // Historical versions for migration tracking
        public const string V1_0 = "1.0"; // Initial version

        /// <summary>
        /// Compare two version strings (format: "major.minor")
        /// </summary>
        /// <returns>-1 if v1 < v2, 0 if equal, 1 if v1 > v2</returns>
        public static int Compare(string v1, string v2)
        {
            if (string.IsNullOrEmpty(v1)) v1 = "1.0";
            if (string.IsNullOrEmpty(v2)) v2 = "1.0";

            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');

            int major1 = int.Parse(parts1[0]);
            int minor1 = parts1.Length > 1 ? int.Parse(parts1[1]) : 0;

            int major2 = int.Parse(parts2[0]);
            int minor2 = parts2.Length > 1 ? int.Parse(parts2[1]) : 0;

            if (major1 != major2) return major1.CompareTo(major2);
            return minor1.CompareTo(minor2);
        }

        /// <summary>
        /// Check if a version is compatible with the current version
        /// (same major version, minor version can be lower)
        /// </summary>
        public static bool IsCompatible(string version)
        {
            if (string.IsNullOrEmpty(version)) return true; // Assume compatible for missing version

            var parts1 = version.Split('.');
            var parts2 = Current.Split('.');

            int major1 = int.Parse(parts1[0]);
            int major2 = int.Parse(parts2[0]);

            // Compatible if same major version
            return major1 == major2;
        }
    }

    public class WorkspaceState
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public List<Dictionary<string, object>> WidgetStates { get; set; } = new List<Dictionary<string, object>>();
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Interface for state migration from one version to another
    /// </summary>
    public interface IStateMigration
    {
        /// <summary>
        /// Version this migration migrates FROM
        /// </summary>
        string FromVersion { get; }

        /// <summary>
        /// Version this migration migrates TO
        /// </summary>
        string ToVersion { get; }

        /// <summary>
        /// Perform the migration
        /// </summary>
        StateSnapshot Migrate(StateSnapshot snapshot);
    }

    /// <summary>
    /// Manages state migrations across versions
    /// </summary>
    public class StateMigrationManager
    {
        private readonly List<IStateMigration> migrations = new List<IStateMigration>();

        public StateMigrationManager()
        {
            // Register migrations in order
            // RegisterMigration(new Migration_1_0_to_1_1());
            // ^ Migration 1.0 to 1.1 adds WidgetId to all widgets
            // ^ Uncomment when needed (currently at 1.0)

            // Future migrations will be added here:
            // RegisterMigration(new Migration_1_1_to_2_0());
        }

        public void RegisterMigration(IStateMigration migration)
        {
            migrations.Add(migration);
            Logger.Instance.Debug("StateMigration", $"Registered migration: {migration.FromVersion} -> {migration.ToVersion}");
        }

        /// <summary>
        /// Migrate a state snapshot to the current version
        /// </summary>
        public StateSnapshot MigrateToCurrentVersion(StateSnapshot snapshot)
        {
            if (snapshot.Version == StateVersion.Current)
            {
                Logger.Instance.Debug("StateMigration", "State is already at current version");
                return snapshot;
            }

            // Check if version is compatible
            if (!StateVersion.IsCompatible(snapshot.Version))
            {
                Logger.Instance.Warning("StateMigration",
                    $"State version {snapshot.Version} is not compatible with current version {StateVersion.Current}. " +
                    "Migration may fail or produce unexpected results.");
            }

            Logger.Instance.Info("StateMigration", $"Migrating state from {snapshot.Version} to {StateVersion.Current}");

            // Build migration path
            var migrationPath = BuildMigrationPath(snapshot.Version, StateVersion.Current);
            if (migrationPath.Count == 0)
            {
                Logger.Instance.Warning("StateMigration",
                    $"No migration path found from {snapshot.Version} to {StateVersion.Current}. " +
                    "State will be loaded as-is, which may cause errors.");
                return snapshot;
            }

            // Execute migrations in sequence
            var currentSnapshot = snapshot;
            foreach (var migration in migrationPath)
            {
                try
                {
                    Logger.Instance.Info("StateMigration", $"Applying migration: {migration.FromVersion} -> {migration.ToVersion}");
                    currentSnapshot = migration.Migrate(currentSnapshot);
                    currentSnapshot.Version = migration.ToVersion;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("StateMigration",
                        $"Migration failed: {migration.FromVersion} -> {migration.ToVersion}", ex);
                    throw new InvalidOperationException(
                        $"State migration failed at version {migration.FromVersion}. Cannot proceed.", ex);
                }
            }

            Logger.Instance.Info("StateMigration", $"State successfully migrated to {StateVersion.Current}");
            return currentSnapshot;
        }

        /// <summary>
        /// Build a migration path from source version to target version
        /// </summary>
        private List<IStateMigration> BuildMigrationPath(string fromVersion, string toVersion)
        {
            var path = new List<IStateMigration>();
            var currentVersion = fromVersion;

            // Simple linear search for migration path
            // For more complex version graphs, implement a proper pathfinding algorithm (BFS/Dijkstra)
            while (currentVersion != toVersion)
            {
                var nextMigration = migrations.FirstOrDefault(m => m.FromVersion == currentVersion);
                if (nextMigration == null)
                {
                    Logger.Instance.Warning("StateMigration",
                        $"No migration found from {currentVersion}. Migration path incomplete.");
                    break;
                }

                path.Add(nextMigration);
                currentVersion = nextMigration.ToVersion;

                // Prevent infinite loops
                if (path.Count > 100)
                {
                    Logger.Instance.Error("StateMigration", "Migration path too long (>100 steps). Possible circular dependency.");
                    throw new InvalidOperationException("Migration path contains circular dependency");
                }
            }

            return path;
        }

        /// <summary>
        /// Get all registered migrations
        /// </summary>
        public IReadOnlyList<IStateMigration> GetMigrations() => migrations.AsReadOnly();
    }

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
    public class StatePersistenceManager
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

            Directory.CreateDirectory(stateDirectory);
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
                        Logger.Instance.Warning("StatePersistence", $"Failed to save widget state: {ex.Message}");
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
                                Logger.Instance.Error("StatePersistence",
                                    $"Failed to restore widget state: {ex.Message}", ex);
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
                Logger.Instance.Error("StatePersistence", $"Failed to restore state: {ex.Message}", ex);
            }
        }

        public void SaveState(StateSnapshot snapshot = null, bool createBackup = false)
        {
            // Synchronous wrapper for backward compatibility
            SaveStateAsync(snapshot, createBackup).GetAwaiter().GetResult();
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

                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(currentStateFile, json, Encoding.UTF8);

                Logger.Instance.Info("StatePersistence", $"State saved to {currentStateFile}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to save state: {ex.Message}", ex);
            }
        }

        public StateSnapshot LoadState()
        {
            // Synchronous wrapper for backward compatibility
            return LoadStateAsync().GetAwaiter().GetResult();
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

                Logger.Instance.Info("StatePersistence", $"State loaded successfully (version {snapshot.Version})");
                return snapshot;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to load state: {ex.Message}", ex);
                return null;
            }
        }

        public void PushUndo(StateSnapshot snapshot)
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

        private void CreateBackup()
        {
            // Synchronous wrapper for backward compatibility
            CreateBackupAsync().GetAwaiter().GetResult();
        }

        private async Task CreateBackupAsync()
        {
            if (!ConfigurationManager.Instance.Get<bool>("Backup.Enabled", true))
                return;

            try
            {
                string backupDir = ConfigurationManager.Instance.Get<string>("Backup.Directory");
                Directory.CreateDirectory(backupDir);

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
                    try { File.Delete(old); } catch { }
                }

                Logger.Instance.Info("StatePersistence", $"Backup created: {backupFile}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to create backup: {ex.Message}", ex);
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
                Logger.Instance.Error("StatePersistence", $"Failed to get backups: {ex.Message}", ex);
                return new List<string>();
            }
        }

        public StateSnapshot RestoreFromBackup(string backupPath)
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
                Logger.Instance.Info("StatePersistence", $"Restored from backup: {backupPath}");
                return snapshot;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StatePersistence", $"Failed to restore from backup: {ex.Message}", ex);
                return null;
            }
        }
    }

    public class StateChangedEventArgs : EventArgs
    {
        public StateSnapshot Snapshot { get; set; }
    }

    // ============================================================================
    // PLUGIN / EXTENSION SYSTEM
    // ============================================================================

    /// <summary>
    /// Plugin metadata
    /// </summary>
    public class PluginMetadata
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Plugin interface that all plugins must implement
    /// </summary>
    public interface IPlugin
    {
        PluginMetadata Metadata { get; }
        void Initialize(PluginContext context);
        void Shutdown();
    }

    /// <summary>
    /// Context provided to plugins
    /// </summary>
    public class PluginContext
    {
        public Logger Logger { get; set; }
        public ConfigurationManager Config { get; set; }
        public ThemeManager Themes { get; set; }
        public WorkspaceManager Workspaces { get; set; }
        public Dictionary<string, object> SharedData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Plugin manager for loading and managing extensions
    /// </summary>
    public class PluginManager
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

            Directory.CreateDirectory(pluginsDirectory);

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
                Logger.Instance.Error("PluginManager", $"Failed to load plugins: {ex.Message}", ex);
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
                        Logger.Instance.Error("PluginManager", $"Failed to instantiate plugin {pluginType.Name}: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("PluginManager", $"Failed to load plugin assembly {assemblyPath}: {ex.Message}", ex);
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
                Logger.Instance.Error("PluginManager", $"Error unloading plugin {pluginName}: {ex.Message}", ex);
            }
        }

        public IPlugin GetPlugin(string name)
        {
            return plugins.TryGetValue(name, out var plugin) ? plugin : null;
        }

        public List<IPlugin> GetAllPlugins()
        {
            return plugins.Values.ToList();
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

    public class PluginEventArgs : EventArgs
    {
        public IPlugin Plugin { get; set; }
    }

    // ============================================================================
    // PERFORMANCE MONITORING
    // ============================================================================

    /// <summary>
    /// Performance counter for monitoring operations
    /// </summary>
    public class PerformanceCounter
    {
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private readonly Queue<TimeSpan> samples = new Queue<TimeSpan>();
        private readonly int maxSamples;

        public string Name { get; }
        public TimeSpan LastDuration { get; private set; }
        public TimeSpan AverageDuration => samples.Count > 0 ? TimeSpan.FromTicks((long)samples.Average(s => s.Ticks)) : TimeSpan.Zero;
        public TimeSpan MinDuration => samples.Count > 0 ? TimeSpan.FromTicks(samples.Min(s => s.Ticks)) : TimeSpan.Zero;
        public TimeSpan MaxDuration => samples.Count > 0 ? TimeSpan.FromTicks(samples.Max(s => s.Ticks)) : TimeSpan.Zero;
        public int SampleCount => samples.Count;

        public PerformanceCounter(string name, int maxSamples = 100)
        {
            Name = name;
            this.maxSamples = maxSamples;
        }

        public void Start()
        {
            stopwatch.Restart();
        }

        public void Stop()
        {
            stopwatch.Stop();
            LastDuration = stopwatch.Elapsed;

            if (samples.Count >= maxSamples)
            {
                samples.Dequeue();
            }
            samples.Enqueue(LastDuration);
        }

        public void Reset()
        {
            samples.Clear();
            LastDuration = TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Performance monitor for tracking various metrics
    /// </summary>
    public class PerformanceMonitor
    {
        private static PerformanceMonitor instance;
        public static PerformanceMonitor Instance => instance ??= new PerformanceMonitor();

        private readonly Dictionary<string, PerformanceCounter> counters = new Dictionary<string, PerformanceCounter>();

        public PerformanceCounter GetCounter(string name)
        {
            if (!counters.TryGetValue(name, out var counter))
            {
                counter = new PerformanceCounter(name);
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

        public Dictionary<string, PerformanceCounter> GetAllCounters()
        {
            return new Dictionary<string, PerformanceCounter>(counters);
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
