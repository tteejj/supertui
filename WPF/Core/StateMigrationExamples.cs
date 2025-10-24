using System;
using System.Collections.Generic;
using System.Linq;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Migrations
{
    /// <summary>
    /// Example migration from version 1.0 to 1.1
    ///
    /// HOW TO USE THIS FILE:
    /// 1. Copy one of the example classes below
    /// 2. Rename it to match your version transition (e.g., Migration_1_1_to_1_2)
    /// 3. Update FromVersion and ToVersion properties
    /// 4. Implement your migration logic in the Migrate() method
    /// 5. Register it in StateMigrationManager constructor in Extensions.cs
    ///
    /// WHEN TO CREATE A MIGRATION:
    /// - When changing the schema of StateSnapshot, WorkspaceState, or widget state
    /// - When renaming fields that are persisted
    /// - When adding new required fields
    /// - When changing data types or formats
    ///
    /// VERSION NUMBERING:
    /// - Major version (X.0): Breaking changes, incompatible with old versions
    /// - Minor version (0.X): Compatible changes, old states can be migrated
    /// </summary>
    public class Migration_1_0_to_1_1 : IStateMigration
    {
        public string FromVersion => "1.0";
        public string ToVersion => "1.1";

        public StateSnapshot Migrate(StateSnapshot snapshot)
        {
            Logger.Instance.Info("StateMigration", "Migrating from 1.0 to 1.1");

            // Example 1: Add new field to ApplicationState
            // Use case: You added a new feature that needs global app-level state
            if (!snapshot.ApplicationState.ContainsKey("NewFeatureEnabled"))
            {
                snapshot.ApplicationState["NewFeatureEnabled"] = false;
                Logger.Instance.Debug("StateMigration", "Added NewFeatureEnabled field");
            }

            // Example 2: Add new field to all workspaces
            // Use case: All workspaces now track a new piece of data
            foreach (var workspace in snapshot.Workspaces)
            {
                if (!workspace.CustomData.ContainsKey("LastModified"))
                {
                    workspace.CustomData["LastModified"] = DateTime.Now;
                    Logger.Instance.Debug("StateMigration", $"Added LastModified to workspace: {workspace.Name}");
                }
            }

            // Example 3: Rename a field in widget states
            // Use case: You refactored widget state and renamed a field
            foreach (var workspace in snapshot.Workspaces)
            {
                foreach (var widgetState in workspace.WidgetStates)
                {
                    if (widgetState.ContainsKey("OldFieldName"))
                    {
                        widgetState["NewFieldName"] = widgetState["OldFieldName"];
                        widgetState.Remove("OldFieldName");
                        Logger.Instance.Debug("StateMigration", "Renamed widget field: OldFieldName -> NewFieldName");
                    }
                }
            }

            // Example 4: Transform data type
            // Use case: Changed from string to int
            foreach (var workspace in snapshot.Workspaces)
            {
                if (workspace.CustomData.ContainsKey("CountString"))
                {
                    var stringValue = workspace.CustomData["CountString"]?.ToString();
                    if (int.TryParse(stringValue, out int intValue))
                    {
                        workspace.CustomData["CountInt"] = intValue;
                        workspace.CustomData.Remove("CountString");
                        Logger.Instance.Debug("StateMigration", "Converted CountString to CountInt");
                    }
                }
            }

            // Update version to new version
            snapshot.Version = ToVersion;

            Logger.Instance.Info("StateMigration", "Migration from 1.0 to 1.1 completed successfully");
            return snapshot;
        }
    }

    /// <summary>
    /// Example migration from version 1.1 to 2.0 (breaking change)
    ///
    /// Use this pattern when making breaking changes that can't be automatically migrated.
    /// </summary>
    public class Migration_1_1_to_2_0 : IStateMigration
    {
        public string FromVersion => "1.1";
        public string ToVersion => "2.0";

        public StateSnapshot Migrate(StateSnapshot snapshot)
        {
            Logger.Instance.Info("StateMigration", "Migrating from 1.1 to 2.0 (breaking change)");

            // Example 1: Remove deprecated fields
            snapshot.ApplicationState.Remove("DeprecatedField");

            // Example 2: Restructure workspace layout
            // Old: Workspaces had flat list of widgets
            // New: Workspaces have nested widget containers
            foreach (var workspace in snapshot.Workspaces)
            {
                // Check if already migrated
                if (!workspace.CustomData.ContainsKey("LayoutVersion"))
                {
                    // Perform migration
                    workspace.CustomData["LayoutVersion"] = "2.0";

                    Logger.Instance.Debug("StateMigration", $"Migrated workspace layout: {workspace.Name}");
                }
            }

            // Example 3: Add new required UserData
            if (!snapshot.UserData.ContainsKey("UserId"))
            {
                snapshot.UserData["UserId"] = Guid.NewGuid().ToString();
                Logger.Instance.Debug("StateMigration", "Generated new UserId for UserData");
            }

            snapshot.Version = ToVersion;

            Logger.Instance.Info("StateMigration", "Migration from 1.1 to 2.0 completed successfully");
            return snapshot;
        }
    }

    /// <summary>
    /// Example migration for widget-specific changes
    ///
    /// Use this pattern when you need to migrate state for a specific widget type.
    /// </summary>
    public class Migration_WidgetSpecific_Example : IStateMigration
    {
        public string FromVersion => "2.0";
        public string ToVersion => "2.1";

        public StateSnapshot Migrate(StateSnapshot snapshot)
        {
            Logger.Instance.Info("StateMigration", "Migrating from 2.0 to 2.1 (widget-specific changes)");

            // Find all ClockWidget states and update them
            foreach (var workspace in snapshot.Workspaces)
            {
                foreach (var widgetState in workspace.WidgetStates)
                {
                    // Check if this is a ClockWidget state (you might need a better way to identify widget types)
                    if (widgetState.ContainsKey("ClockFormat"))
                    {
                        // Old ClockWidget had "Format" field
                        // New ClockWidget has "DateFormat" and "TimeFormat" separately
                        if (widgetState.ContainsKey("Format"))
                        {
                            var oldFormat = widgetState["Format"]?.ToString();

                            // Split format into date and time parts (simplified example)
                            widgetState["DateFormat"] = "yyyy-MM-dd";
                            widgetState["TimeFormat"] = "HH:mm:ss";
                            widgetState.Remove("Format");

                            Logger.Instance.Debug("StateMigration", "Migrated ClockWidget format");
                        }
                    }
                }
            }

            snapshot.Version = ToVersion;

            Logger.Instance.Info("StateMigration", "Migration from 2.0 to 2.1 completed successfully");
            return snapshot;
        }
    }

    /// <summary>
    /// Example migration with error handling and validation
    ///
    /// Use this pattern for complex migrations that might fail.
    /// </summary>
    public class Migration_WithErrorHandling_Example : IStateMigration
    {
        public string FromVersion => "2.1";
        public string ToVersion => "2.2";

        public StateSnapshot Migrate(StateSnapshot snapshot)
        {
            Logger.Instance.Info("StateMigration", "Migrating from 2.1 to 2.2 with validation");

            try
            {
                // Validate pre-conditions
                if (snapshot.Workspaces == null || snapshot.Workspaces.Count == 0)
                {
                    Logger.Instance.Warning("StateMigration", "No workspaces found, skipping workspace migration");
                }
                else
                {
                    // Perform migration with error handling
                    foreach (var workspace in snapshot.Workspaces)
                    {
                        try
                        {
                            MigrateWorkspace(workspace);
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Error("StateMigration",
                                $"Failed to migrate workspace '{workspace.Name}': {ex.Message}");

                            // Mark workspace as failed migration (optional)
                            workspace.CustomData["MigrationError"] = ex.Message;
                        }
                    }
                }

                snapshot.Version = ToVersion;
                Logger.Instance.Info("StateMigration", "Migration from 2.1 to 2.2 completed");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("StateMigration", $"Migration failed: {ex.Message}");
                throw; // Re-throw to allow StateMigrationManager to handle it
            }

            return snapshot;
        }

        private void MigrateWorkspace(WorkspaceState workspace)
        {
            // Complex migration logic here
            if (!workspace.CustomData.ContainsKey("MigrationVersion"))
            {
                workspace.CustomData["MigrationVersion"] = "2.2";
            }
        }
    }

    /// <summary>
    /// Example data transformation migration
    ///
    /// Use this pattern when you need to transform complex data structures.
    /// </summary>
    public class Migration_DataTransform_Example : IStateMigration
    {
        public string FromVersion => "2.2";
        public string ToVersion => "2.3";

        public StateSnapshot Migrate(StateSnapshot snapshot)
        {
            Logger.Instance.Info("StateMigration", "Migrating from 2.2 to 2.3 (data transformation)");

            // Example: Convert list of strings to list of objects
            if (snapshot.ApplicationState.ContainsKey("RecentFiles"))
            {
                var recentFiles = snapshot.ApplicationState["RecentFiles"];

                // Old format: List<string> of file paths
                // New format: List<RecentFileInfo> with path, timestamp, etc.

                // Note: This is pseudocode - actual implementation depends on your serialization
                // In practice, you might need to use JsonElement and manipulate the raw data

                Logger.Instance.Debug("StateMigration", "Transformed RecentFiles data structure");
            }

            snapshot.Version = ToVersion;

            Logger.Instance.Info("StateMigration", "Migration from 2.2 to 2.3 completed successfully");
            return snapshot;
        }
    }
}
