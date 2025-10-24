# State Migration Guide

## Overview

SuperTUI includes a state migration system that allows you to safely evolve your application's state schema over time without breaking existing saved states.

## How It Works

1. **Version Tracking**: Every saved state includes a `Version` field (e.g., "1.0", "1.1", "2.0")
2. **Migration Chain**: When loading old state, migrations are applied sequentially to bring it to the current version
3. **Automatic Application**: Migrations run automatically when `LoadState()` is called
4. **Backup Safety**: Always create backups before migrating production states

## When to Create a Migration

Create a migration when you:

- ✅ Add new required fields to StateSnapshot, WorkspaceState, or widget state
- ✅ Rename existing persisted fields
- ✅ Change data types (e.g., string → int)
- ✅ Restructure nested data
- ✅ Remove deprecated fields
- ✅ Change serialization format

Don't create a migration when you:

- ❌ Add optional fields with defaults (just handle null in your code)
- ❌ Change internal logic that doesn't affect state structure
- ❌ Modify UI appearance
- ❌ Refactor code without changing state schema

## Version Numbering

**Format:** `Major.Minor` (e.g., "1.0", "1.5", "2.0")

**Major Version (X.0):**
- Breaking changes
- Incompatible with previous versions
- Examples: Complete state restructure, fundamental architecture changes

**Minor Version (0.X):**
- Compatible changes
- Old states can be migrated automatically
- Examples: Add new fields, rename fields, add new widgets

**Current Version:** Check `StateVersion.Current` in `Extensions.cs:41`

## Creating a Migration

### Step 1: Update Current Version

Edit `WPF/Core/Extensions.cs`:

```csharp
public static class StateVersion
{
    public const string Current = "1.1"; // Changed from "1.0"

    public const string V1_0 = "1.0";
    public const string V1_1 = "1.1"; // Add new constant
}
```

### Step 2: Create Migration Class

See `WPF/Core/StateMigrationExamples.cs` for templates. Create your migration:

```csharp
public class Migration_1_0_to_1_1 : IStateMigration
{
    public string FromVersion => "1.0";
    public string ToVersion => "1.1";

    public StateSnapshot Migrate(StateSnapshot snapshot)
    {
        Logger.Instance.Info("StateMigration", "Migrating from 1.0 to 1.1");

        // Your migration logic here
        if (!snapshot.ApplicationState.ContainsKey("NewField"))
        {
            snapshot.ApplicationState["NewField"] = "DefaultValue";
        }

        snapshot.Version = ToVersion;
        return snapshot;
    }
}
```

### Step 3: Register Migration

Edit `WPF/Core/Extensions.cs` in `StateMigrationManager` constructor:

```csharp
public StateMigrationManager()
{
    RegisterMigration(new Migration_1_0_to_1_1()); // Add this line
    // Future migrations...
}
```

### Step 4: Test Migration

Run the test script:

```powershell
./WPF/Test_StateMigration.ps1
```

Create your own test:
1. Create old state JSON file with old version
2. Load it with `StatePersistenceManager.LoadState()`
3. Verify fields were added/transformed correctly

## Migration Examples

### Example 1: Add New Field

**Scenario:** Added a "LastOpened" timestamp to ApplicationState

```csharp
public StateSnapshot Migrate(StateSnapshot snapshot)
{
    if (!snapshot.ApplicationState.ContainsKey("LastOpened"))
    {
        snapshot.ApplicationState["LastOpened"] = DateTime.Now;
    }

    snapshot.Version = ToVersion;
    return snapshot;
}
```

### Example 2: Rename Field

**Scenario:** Renamed widget state field "Count" to "CounterValue"

```csharp
public StateSnapshot Migrate(StateSnapshot snapshot)
{
    foreach (var workspace in snapshot.Workspaces)
    {
        foreach (var widgetState in workspace.WidgetStates)
        {
            if (widgetState.ContainsKey("Count"))
            {
                widgetState["CounterValue"] = widgetState["Count"];
                widgetState.Remove("Count");
            }
        }
    }

    snapshot.Version = ToVersion;
    return snapshot;
}
```

### Example 3: Type Conversion

**Scenario:** Changed workspace "Priority" from string to int

```csharp
public StateSnapshot Migrate(StateSnapshot snapshot)
{
    foreach (var workspace in snapshot.Workspaces)
    {
        if (workspace.CustomData.ContainsKey("PriorityString"))
        {
            var stringValue = workspace.CustomData["PriorityString"]?.ToString();
            if (int.TryParse(stringValue, out int intValue))
            {
                workspace.CustomData["Priority"] = intValue;
                workspace.CustomData.Remove("PriorityString");
            }
            else
            {
                workspace.CustomData["Priority"] = 0; // Default
            }
        }
    }

    snapshot.Version = ToVersion;
    return snapshot;
}
```

### Example 4: Widget-Specific Migration

**Scenario:** ClockWidget changed from single "Format" to separate "DateFormat" and "TimeFormat"

```csharp
public StateSnapshot Migrate(StateSnapshot snapshot)
{
    foreach (var workspace in snapshot.Workspaces)
    {
        foreach (var widgetState in workspace.WidgetStates)
        {
            // Identify ClockWidget states by checking for known fields
            if (widgetState.ContainsKey("Format") &&
                widgetState.ContainsKey("ShowSeconds"))
            {
                var oldFormat = widgetState["Format"]?.ToString();

                // Split into date and time formats
                widgetState["DateFormat"] = "yyyy-MM-dd";
                widgetState["TimeFormat"] = "HH:mm:ss";
                widgetState.Remove("Format");
            }
        }
    }

    snapshot.Version = ToVersion;
    return snapshot;
}
```

## Best Practices

### 1. Always Log Migration Actions

```csharp
Logger.Instance.Info("StateMigration", "Migrating from 1.0 to 1.1");
Logger.Instance.Debug("StateMigration", $"Added field to workspace: {workspace.Name}");
```

### 2. Check Before Modifying

```csharp
// Good: Check if field exists
if (!snapshot.ApplicationState.ContainsKey("NewField"))
{
    snapshot.ApplicationState["NewField"] = "DefaultValue";
}

// Bad: Assume field doesn't exist
snapshot.ApplicationState["NewField"] = "DefaultValue";
```

### 3. Provide Sensible Defaults

```csharp
// Good: Provide context-aware default
workspace.CustomData["CreatedDate"] = DateTime.Now;

// Bad: Use null when a real default is needed
workspace.CustomData["CreatedDate"] = null;
```

### 4. Handle Errors Gracefully

```csharp
try
{
    MigrateWorkspace(workspace);
}
catch (Exception ex)
{
    Logger.Instance.Error("StateMigration",
        $"Failed to migrate workspace '{workspace.Name}': {ex.Message}");

    // Mark workspace as failed (optional)
    workspace.CustomData["MigrationError"] = ex.Message;
}
```

### 5. Test With Real Data

- Create test states from actual usage
- Test migration chain: 1.0 → 1.1 → 1.2
- Verify original data is preserved
- Check that application runs correctly after migration

## Testing Migrations

### Automated Test

Run the included test script:

```powershell
cd WPF
./Test_StateMigration.ps1
```

This validates:
- ✅ Migration infrastructure works
- ✅ Version is updated correctly
- ✅ New fields are added
- ✅ Original data is preserved

### Manual Testing

1. **Create Old State:**
   ```powershell
   # Run app with old version, save state
   ./SuperTUI_Demo.ps1
   # State saved to ~/.supertui/state.json
   ```

2. **Update Code:**
   - Change `StateVersion.Current` to new version
   - Add migration class
   - Register migration

3. **Test Migration:**
   ```powershell
   # Run app with new version
   ./SuperTUI_Demo.ps1
   # Should automatically migrate on load
   ```

4. **Verify:**
   - Check log for migration messages
   - Verify app loads correctly
   - Verify all widgets work
   - Inspect state file to confirm new fields

## Migration Chain Example

If you have multiple migrations:

```
Version 1.0 (Initial)
    ↓
Migration_1_0_to_1_1 (Add LastOpened field)
    ↓
Version 1.1
    ↓
Migration_1_1_to_1_2 (Add widget IDs)
    ↓
Version 1.2
    ↓
Migration_1_2_to_2_0 (Restructure layout)
    ↓
Version 2.0 (Breaking change)
```

StateMigrationManager automatically applies all migrations in order:
- Loading 1.0 state → applies 1.0→1.1, 1.1→1.2, 1.2→2.0
- Loading 1.1 state → applies 1.1→1.2, 1.2→2.0
- Loading 2.0 state → no migrations needed

## Troubleshooting

### Migration Not Running

**Problem:** Old state loads but migration doesn't run

**Solutions:**
1. Check `StateVersion.Current` is updated
2. Verify migration is registered in `StateMigrationManager` constructor
3. Check `FromVersion` matches the old state version exactly
4. Look for errors in the log file

### Migration Fails

**Problem:** Exception thrown during migration

**Solutions:**
1. Add try-catch blocks in migration code
2. Check for null values before accessing
3. Use `ContainsKey()` before accessing dictionary values
4. Add logging to identify where it fails

### Data Loss

**Problem:** Original data is missing after migration

**Solutions:**
1. Always create backups before testing migrations
2. Use `StatePersistenceManager.SaveState(snapshot, createBackup: true)`
3. Test with copies of production data
4. Never remove fields without verifying they're not needed

### Version Incompatibility

**Problem:** "State version X.X is not compatible with current version Y.Y"

**Solutions:**
1. Create migration chain from X.X to Y.Y
2. If truly incompatible, handle in `StateMigrationManager.MigrateToCurrentVersion()`
3. Consider providing migration tool for users

## Files Reference

- **Core Migration System:** `WPF/Core/Extensions.cs` (lines 22-281)
- **Migration Examples:** `WPF/Core/StateMigrationExamples.cs`
- **Test Script:** `WPF/Test_StateMigration.ps1`
- **This Guide:** `WPF/STATE_MIGRATION_GUIDE.md`

## FAQ

**Q: What happens if a user skips versions (e.g., upgrades from 1.0 to 2.0)?**
A: All intermediate migrations are applied in order (1.0→1.1→2.0).

**Q: Can I run migrations manually?**
A: Yes, create a `StateMigrationManager` and call `MigrateToCurrentVersion(snapshot)`.

**Q: What if my migration is complex and takes a long time?**
A: Consider showing a progress dialog. Migrations block the UI during state load.

**Q: Should I delete old migration code after everyone has upgraded?**
A: Keep migrations for at least 1 major version. Users might have very old state files.

**Q: How do I handle incompatible breaking changes?**
A: Increment major version and document that old states can't be migrated automatically.

## Next Steps

1. ✅ Read this guide
2. ✅ Review examples in `StateMigrationExamples.cs`
3. ✅ Run `Test_StateMigration.ps1` to see it work
4. ✅ Create your first migration
5. ✅ Test thoroughly before releasing
