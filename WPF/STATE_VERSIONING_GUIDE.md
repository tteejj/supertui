# State Versioning & Migration Guide

## Overview

SuperTUI implements a robust state versioning and migration system to ensure seamless upgrades across application versions. When the state schema changes, migrations automatically transform old state files to the new format.

## Version Format

State versions follow **semantic versioning** with format: `"major.minor"`

- **Major version**: Incremented for breaking changes (incompatible schema changes)
- **Minor version**: Incremented for backward-compatible additions

Current version: `1.0`

## Architecture

### Core Components

1. **StateVersion** - Version constants and comparison utilities
2. **IStateMigration** - Interface for defining migrations
3. **StateMigrationManager** - Orchestrates migration execution
4. **StatePersistenceManager** - Integrates migration into load/save flow

### Migration Flow

```
┌─────────────┐
│ Load State  │
└──────┬──────┘
       │
       ▼
  ┌─────────────────┐      ┌──────────────┐
  │ Version Check   │─────▶│ Same Version │─────▶ Use State
  └────────┬────────┘      └──────────────┘
           │
           ▼ Different
  ┌─────────────────┐
  │ Create Backup   │
  └────────┬────────┘
           │
           ▼
  ┌─────────────────┐
  │ Find Migration  │
  │     Path        │
  └────────┬────────┘
           │
           ▼
  ┌─────────────────┐
  │ Execute Migr.   │
  │   Sequentially  │
  └────────┬────────┘
           │
           ▼
  ┌─────────────────┐
  │ Save Migrated   │
  │     State       │
  └────────┬────────┘
           │
           ▼
     Use State
```

## Creating a Migration

### Step 1: Increment Version

Update `StateVersion.Current` in `Extensions.cs`:

```csharp
public static class StateVersion
{
    public const string Current = "1.1"; // Was: "1.0"

    // Historical versions
    public const string V1_0 = "1.0";
    public const string V1_1 = "1.1"; // New
}
```

### Step 2: Implement Migration

Create a migration class implementing `IStateMigration`:

```csharp
/// <summary>
/// Migrates state from version 1.0 to 1.1
/// Adds new "Theme" field to ApplicationState
/// </summary>
public class Migration_1_0_to_1_1 : IStateMigration
{
    public string FromVersion => StateVersion.V1_0;
    public string ToVersion => StateVersion.V1_1;

    public StateSnapshot Migrate(StateSnapshot snapshot)
    {
        Logger.Instance.Info("StateMigration", "Migrating from 1.0 to 1.1");

        // Add new field with default value
        if (!snapshot.ApplicationState.ContainsKey("Theme"))
        {
            snapshot.ApplicationState["Theme"] = "Dark";
        }

        return snapshot;
    }
}
```

### Step 3: Register Migration

In `StateMigrationManager` constructor:

```csharp
public StateMigrationManager()
{
    // Register migrations in chronological order
    RegisterMigration(new Migration_1_0_to_1_1());
    // RegisterMigration(new Migration_1_1_to_1_2()); // Future
}
```

## Migration Examples

### Example 1: Add New Field

```csharp
public StateSnapshot Migrate(StateSnapshot snapshot)
{
    // Add new application-level setting
    if (!snapshot.ApplicationState.ContainsKey("AutoSaveInterval"))
    {
        snapshot.ApplicationState["AutoSaveInterval"] = 300; // 5 minutes default
    }

    return snapshot;
}
```

### Example 2: Rename Field

```csharp
public StateSnapshot Migrate(StateSnapshot snapshot)
{
    // Rename workspace field
    foreach (var workspace in snapshot.Workspaces)
    {
        if (workspace.CustomData.ContainsKey("Layout"))
        {
            workspace.CustomData["LayoutType"] = workspace.CustomData["Layout"];
            workspace.CustomData.Remove("Layout");
        }
    }

    return snapshot;
}
```

### Example 3: Transform Widget States

```csharp
public StateSnapshot Migrate(StateSnapshot snapshot)
{
    // Convert old color format to new format
    foreach (var workspace in snapshot.Workspaces)
    {
        foreach (var widgetState in workspace.WidgetStates)
        {
            if (widgetState.ContainsKey("Color"))
            {
                var oldColor = widgetState["Color"] as string;
                widgetState["Color"] = ConvertColorFormat(oldColor);
            }
        }
    }

    return snapshot;
}

private object ConvertColorFormat(string hexColor)
{
    // Convert "#RRGGBB" to { R, G, B } dictionary
    return new Dictionary<string, int>
    {
        ["R"] = Convert.ToInt32(hexColor.Substring(1, 2), 16),
        ["G"] = Convert.ToInt32(hexColor.Substring(3, 2), 16),
        ["B"] = Convert.ToInt32(hexColor.Substring(5, 2), 16)
    };
}
```

### Example 4: Remove Deprecated Data

```csharp
public StateSnapshot Migrate(StateSnapshot snapshot)
{
    // Remove obsolete fields
    snapshot.ApplicationState.Remove("DeprecatedSetting");

    foreach (var workspace in snapshot.Workspaces)
    {
        workspace.CustomData.Remove("ObsoleteField");
    }

    return snapshot;
}
```

### Example 5: Major Version Change (Breaking)

```csharp
/// <summary>
/// Migration from 1.x to 2.0 - Major breaking change
/// Restructures workspace data model
/// </summary>
public class Migration_1_9_to_2_0 : IStateMigration
{
    public string FromVersion => "1.9";
    public string ToVersion => "2.0";

    public StateSnapshot Migrate(StateSnapshot snapshot)
    {
        Logger.Instance.Warning("StateMigration",
            "Performing major version migration 1.9 -> 2.0. This may be slow.");

        // Complete restructure of workspace model
        var newWorkspaces = new List<WorkspaceState>();

        foreach (var oldWorkspace in snapshot.Workspaces)
        {
            var newWorkspace = new WorkspaceState
            {
                Name = oldWorkspace.Name,
                Index = oldWorkspace.Index,
                // New structure with grouped widgets
                CustomData = new Dictionary<string, object>
                {
                    ["Groups"] = GroupWidgetsByType(oldWorkspace.WidgetStates)
                }
            };

            newWorkspaces.Add(newWorkspace);
        }

        snapshot.Workspaces = newWorkspaces;
        return snapshot;
    }

    private object GroupWidgetsByType(List<Dictionary<string, object>> widgets)
    {
        // Implementation details...
        return new Dictionary<string, List<Dictionary<string, object>>>();
    }
}
```

## Migration Path Resolution

The system automatically finds the shortest path from loaded version to current version:

```
1.0 → 1.1 → 1.2 → 2.0
```

If loading a `1.0` state file while current is `2.0`, it will execute:
1. Migration_1_0_to_1_1
2. Migration_1_1_to_1_2
3. Migration_1_2_to_2_0

## Error Handling

### Incompatible Versions

When loading a state from an incompatible major version:

```
[WARNING] State version 2.0 is not compatible with current version 1.5.
Migration may fail or produce unexpected results.
```

The system will attempt migration but logs a warning.

### Missing Migration Path

If no migration exists from loaded version to current:

```
[WARNING] No migration path found from 0.9 to 1.5.
State will be loaded as-is, which may cause errors.
```

The state is loaded without migration (best effort).

### Migration Failure

If a migration throws an exception:

```
[ERROR] Migration failed: 1.2 -> 1.3
State migration failed at version 1.2. Cannot proceed.
```

The load operation fails and the original state file remains intact (backup exists).

## Backup System

Before any migration:
1. Current state is backed up to `state_backup_YYYYMMDD_HHmmss.json`
2. Backups are stored in the same directory as state files
3. Manual restoration: Copy backup over `current_state.json`

## Testing Migrations

### Unit Test Template

```csharp
[Fact]
public void Migration_1_0_to_1_1_ShouldAddThemeField()
{
    // Arrange
    var migration = new Migration_1_0_to_1_1();
    var oldSnapshot = new StateSnapshot
    {
        Version = "1.0",
        ApplicationState = new Dictionary<string, object>()
    };

    // Act
    var newSnapshot = migration.Migrate(oldSnapshot);

    // Assert
    Assert.True(newSnapshot.ApplicationState.ContainsKey("Theme"));
    Assert.Equal("Dark", newSnapshot.ApplicationState["Theme"]);
}

[Fact]
public void Migration_1_0_to_1_1_ShouldPreserveExistingData()
{
    // Arrange
    var migration = new Migration_1_0_to_1_1();
    var oldSnapshot = new StateSnapshot
    {
        Version = "1.0",
        ApplicationState = new Dictionary<string, object>
        {
            ["ExistingKey"] = "ExistingValue"
        }
    };

    // Act
    var newSnapshot = migration.Migrate(oldSnapshot);

    // Assert
    Assert.Equal("ExistingValue", newSnapshot.ApplicationState["ExistingKey"]);
}
```

## Best Practices

1. **Always increment version** when changing state schema
2. **Write migration immediately** - don't defer
3. **Test with real data** - use production-like state files
4. **Keep migrations simple** - one purpose per migration
5. **Log extensively** - migrations run rarely, logs are critical
6. **Never delete old migrations** - users may skip versions
7. **Document breaking changes** in migration comments
8. **Consider performance** - migrations run on app startup
9. **Validate after migration** - check state integrity
10. **Test migration chains** - verify 1.0→1.1→1.2 works

## Version Strategy

### Minor Version (1.0 → 1.1)
- Adding new optional fields
- Adding new workspace CustomData
- New widget state properties (with defaults)

### Major Version (1.x → 2.0)
- Removing required fields
- Changing field types
- Restructuring workspace model
- Renaming core properties

## Registering Custom Migrations at Runtime

Applications can register migrations dynamically:

```csharp
var stateMgr = StatePersistenceManager.Instance;
stateMgr.MigrationManager.RegisterMigration(new MyCustomMigration());
```

## Inspecting Migration State

```csharp
var migrations = stateMgr.MigrationManager.GetMigrations();
foreach (var migration in migrations)
{
    Console.WriteLine($"{migration.FromVersion} → {migration.ToVersion}");
}
```

## Future Enhancements

- [ ] Migration rollback support
- [ ] Dry-run mode (preview migration without applying)
- [ ] Migration analytics (track success rates)
- [ ] Parallel migration execution (if independent)
- [ ] Migration checksums (verify integrity)
- [ ] Schema validation (JSON Schema)

## Related Files

- `/home/teej/supertui/WPF/Core/Extensions.cs` - Migration implementation (lines 19-277)
- `/home/teej/supertui/WPF/Tests/Infrastructure/StateMigrationTests.cs` - Test suite
- `/home/teej/supertui/WPF/MEDIUM_PRIORITY_FIXES_APPLIED.md` - Fix #15 documentation
