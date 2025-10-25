using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// State snapshot for persistence with versioning support.
    /// Captures the complete application state at a point in time,
    /// including workspace configurations and widget states.
    ///
    /// PHASE 2 FIX: Added integrity verification with checksums
    /// </summary>
    public class StateSnapshot
    {
        /// <summary>
        /// Schema version of this snapshot. Format: "major.minor"
        /// Breaking changes increment major, compatible changes increment minor.
        /// </summary>
        public string Version { get; set; } = StateVersion.Current;

        /// <summary>
        /// Timestamp when this snapshot was captured
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// SHA256 checksum of the state data for integrity verification
        /// PHASE 2 FIX: Detect corrupted state files
        /// </summary>
        public string Checksum { get; set; }

        /// <summary>
        /// Application-level state data (e.g., current workspace index, global settings)
        /// </summary>
        public Dictionary<string, object> ApplicationState { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// State data for all workspaces
        /// </summary>
        public List<WorkspaceState> Workspaces { get; set; } = new List<WorkspaceState>();

        /// <summary>
        /// Custom user data that can be used by plugins or applications
        /// </summary>
        public Dictionary<string, object> UserData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Calculate SHA256 checksum for integrity verification
        /// PHASE 2 FIX: Verify state file hasn't been corrupted
        /// </summary>
        public void CalculateChecksum()
        {
            // Serialize the data (excluding checksum itself)
            var data = new
            {
                Version,
                Timestamp,
                ApplicationState,
                Workspaces,
                UserData
            };

            string json = JsonSerializer.Serialize(data);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            Checksum = Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Verify that checksum matches current data
        /// PHASE 2 FIX: Detect corrupted or tampered state files
        /// </summary>
        public bool VerifyChecksum()
        {
            if (string.IsNullOrEmpty(Checksum))
            {
                // Old state file without checksum - can't verify
                return true;
            }

            var originalChecksum = Checksum;
            CalculateChecksum();
            var newChecksum = Checksum;
            Checksum = originalChecksum;  // Restore original

            return originalChecksum == newChecksum;
        }
    }

    /// <summary>
    /// State version constants and comparison utilities
    /// </summary>
    public static class StateVersion
    {
        /// <summary>
        /// Current version of the state schema
        /// </summary>
        public const string Current = "1.0";

        // Historical versions for migration tracking
        /// <summary>
        /// Initial version (1.0)
        /// </summary>
        public const string V1_0 = "1.0";

        /// <summary>
        /// Compare two version strings (format: "major.minor")
        /// </summary>
        /// <returns>-1 if v1 &lt; v2, 0 if equal, 1 if v1 &gt; v2</returns>
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

    /// <summary>
    /// State data for a single workspace
    /// </summary>
    public class WorkspaceState
    {
        /// <summary>
        /// Name of the workspace
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Index of the workspace
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// State data for all widgets in this workspace
        /// </summary>
        public List<Dictionary<string, object>> WidgetStates { get; set; } = new List<Dictionary<string, object>>();

        /// <summary>
        /// Custom data specific to this workspace
        /// </summary>
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

        /// <summary>
        /// Initializes a new instance of StateMigrationManager
        /// </summary>
        public StateMigrationManager()
        {
            // Register migrations in order
            // Future migrations will be added here:
            // RegisterMigration(new Migration_1_0_to_1_1());
        }

        /// <summary>
        /// Register a migration to be used when migrating state
        /// </summary>
        public void RegisterMigration(IStateMigration migration)
        {
            migrations.Add(migration);
        }

        /// <summary>
        /// Migrate a state snapshot to the current version
        /// </summary>
        public StateSnapshot MigrateToCurrentVersion(StateSnapshot snapshot)
        {
            if (snapshot.Version == StateVersion.Current)
            {
                return snapshot;
            }

            // Check if version is compatible
            if (!StateVersion.IsCompatible(snapshot.Version))
            {
                throw new InvalidOperationException(
                    $"State version {snapshot.Version} is not compatible with current version {StateVersion.Current}");
            }

            // Build migration path
            var migrationPath = BuildMigrationPath(snapshot.Version, StateVersion.Current);
            if (migrationPath.Count == 0)
            {
                // No migration path found, return as-is
                return snapshot;
            }

            // Execute migrations in sequence
            var currentSnapshot = snapshot;
            foreach (var migration in migrationPath)
            {
                currentSnapshot = migration.Migrate(currentSnapshot);
                currentSnapshot.Version = migration.ToVersion;
            }

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
            while (currentVersion != toVersion)
            {
                var nextMigration = migrations.Find(m => m.FromVersion == currentVersion);
                if (nextMigration == null)
                {
                    break;
                }

                path.Add(nextMigration);
                currentVersion = nextMigration.ToVersion;

                // Prevent infinite loops
                if (path.Count > 100)
                {
                    throw new InvalidOperationException("Migration path contains circular dependency");
                }
            }

            return path;
        }

        /// <summary>
        /// Get all registered migrations
        /// </summary>
        public IReadOnlyList<IStateMigration> GetMigrations()
        {
            return migrations.AsReadOnly();
        }
    }
}
