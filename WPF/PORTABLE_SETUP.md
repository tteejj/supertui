# SuperTUI Portable Setup

## Overview

SuperTUI is now **fully portable and self-contained**. All data files, logs, configuration, and state are stored in a `.data` directory within the WPF folder.

## Directory Structure

```
WPF/
├── .data/                          # ← All runtime data here (gitignored)
│   ├── supertui_test_*.log         # Log files
│   ├── config.json                 # Configuration
│   ├── migration_test.json         # Test files
│   └── SuperTUI/                   # SuperTUI data
│       ├── State/                  # Saved state files
│       ├── Logs/                   # Application logs
│       ├── Backups/                # State backups
│       ├── Themes/                 # Custom themes
│       ├── Plugins/                # Loaded plugins
│       └── todos.json              # Todo widget data
├── Core/                           # C# framework files
├── Widgets/                        # Widget implementations
├── SuperTUI_TestFixes.ps1          # Test demo
├── Test_StateMigration.ps1         # Migration test
└── .gitignore                      # Ignores .data/
```

## Portability

### ✅ What This Means

**1. No External Dependencies**
- Does not write to `%TEMP%`
- Does not write to `%LOCALAPPDATA%`
- Does not write to `%APPDATA%`
- Does not write to user directories

**2. Fully Self-Contained**
- Copy the WPF folder anywhere → it works
- Run from USB drive → it works
- Run from network share → it works
- Multiple copies can run independently

**3. Easy Cleanup**
- Delete `.data` folder → all runtime data gone
- No registry entries
- No system-wide changes

**4. Version Control Friendly**
- `.data/` is gitignored
- Only source code is tracked
- No user-specific files in git

## How It Works

### PowerShell Scripts

Test scripts create and use local `.data` directory:

```powershell
# Set up portable data directory
$dataDir = Join-Path $PSScriptRoot ".data"
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

# Set log file in local directory
$logFile = Join-Path $dataDir "supertui_test_*.log"
```

### C# Code

A new helper class provides portable paths:

**File:** `Core/Extensions.cs`

```csharp
public static class PortableDataDirectory
{
    private static string dataDirectory;

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
        set { /* ... */ }
    }

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
```

### Updated Files

All files that previously used external paths now use `PortableDataDirectory`:

**1. Core/Extensions.cs**
- ✅ `StatePersistenceManager` → Uses `.data/SuperTUI/State/`
- ✅ `PluginSystem` → Uses `.data/SuperTUI/Plugins/`

**2. Core/Infrastructure/ConfigurationManager.cs**
- ✅ `App.LogDirectory` → Uses `.data/SuperTUI/Logs/`
- ✅ `Backup.Directory` → Uses `.data/SuperTUI/Backups/`

**3. Core/Infrastructure/ThemeManager.cs**
- ✅ Themes directory → Uses `.data/SuperTUI/Themes/`

**4. Widgets/TodoWidget.cs**
- ✅ Todo data file → Uses `.data/SuperTUI/todos.json`

**5. SuperTUI_TestFixes.ps1**
- ✅ Log files → `.data/supertui_test_*.log`
- ✅ Config → `.data/config.json`
- ✅ Explicitly sets: `[SuperTUI.Extensions.PortableDataDirectory]::DataDirectory = $dataDir`

**6. Test_StateMigration.ps1**
- ✅ Test state → `.data/migration_test.json`
- ✅ Test log → `.data/migration_test.log`

## Usage

### Running the Application

```powershell
cd WPF
./SuperTUI_TestFixes.ps1
```

**Output:**
```
Data directory: C:\path\to\supertui\WPF\.data
Log file: C:\path\to\supertui\WPF\.data\supertui_test_20251024_123456.log
```

All data goes into `.data/` - nothing writes outside this directory.

### Moving the Application

```powershell
# Copy entire WPF folder
Copy-Item -Recurse C:\dev\supertui\WPF D:\portable\supertui

# Run from new location
cd D:\portable\supertui
./SuperTUI_TestFixes.ps1
```

Creates new `.data/` in the new location - completely independent.

### Cleanup

```powershell
# Delete all runtime data
Remove-Item -Recurse .data

# Or keep data but clean logs
Remove-Item .data\*.log
```

### Multiple Instances

You can run multiple copies independently:

```
D:\projects\supertui-dev\WPF\.data\       # Dev instance
D:\projects\supertui-test\WPF\.data\      # Test instance
E:\portable\supertui\WPF\.data\           # Portable instance
```

Each has its own configuration, logs, and state.

## Migration from Old Setup

If you previously ran SuperTUI and it created files in `%LOCALAPPDATA%`:

**Old Location:**
```
%LOCALAPPDATA%\SuperTUI\
├── Logs\
├── State\
├── Backups\
└── todos.json
```

**New Location:**
```
WPF\.data\SuperTUI\
├── Logs\
├── State\
├── Backups\
└── todos.json
```

**To Migrate:**

```powershell
# Copy old data to new location
$oldData = "$env:LOCALAPPDATA\SuperTUI"
$newData = "WPF\.data\SuperTUI"

if (Test-Path $oldData) {
    Copy-Item -Recurse $oldData $newData
    Write-Host "Migrated data from $oldData to $newData"
}

# Optional: Delete old data
Remove-Item -Recurse $oldData
```

## Development

### .gitignore

The `.data/` directory is gitignored:

```gitignore
# SuperTUI portable data directory
.data/

# Test output files
*.log
*_test_*.json
```

### Testing

All test scripts use `.data/`:

```powershell
# Test fixes
./SuperTUI_TestFixes.ps1
# Creates: .data/supertui_test_*.log, .data/config.json

# Test migration
./Test_StateMigration.ps1
# Creates: .data/migration_test.json, .data/migration_test.log
```

After testing, you can:
- Keep `.data/` to preserve settings
- Delete `.data/` for clean slate
- Commit code without committing data

## Advanced: Custom Data Directory

You can override the data directory location in code:

```powershell
# PowerShell: Set before initializing
[SuperTUI.Extensions.PortableDataDirectory]::DataDirectory = "D:\my-custom-location"

# Now all data goes to D:\my-custom-location\SuperTUI\
```

```csharp
// C#: Set at startup
PortableDataDirectory.DataDirectory = @"D:\my-custom-location";
```

## Benefits

### For Users
- ✅ No installation required
- ✅ Run from anywhere (USB, network share, etc.)
- ✅ No system pollution
- ✅ Easy backup (copy folder)
- ✅ Easy cleanup (delete folder)

### For Developers
- ✅ Clean git history (no user data)
- ✅ Consistent development environment
- ✅ Easy testing (multiple independent copies)
- ✅ No conflicts between versions

### For Deployment
- ✅ Portable package (zip and go)
- ✅ No installer needed
- ✅ No uninstaller needed
- ✅ Multi-user friendly (each user's copy is independent)

## Verification

To verify portability:

**1. Check for external file access:**
```powershell
# Search for hardcoded external paths
Select-String -Path *.cs,*.ps1 -Pattern "TEMP|LOCALAPPDATA|APPDATA" -Exclude .gitignore
```

Should only find:
- This documentation
- Comments/historical references

**2. Run with file monitoring:**
```powershell
# Enable Process Monitor or similar
# Run SuperTUI_TestFixes.ps1
# Verify all file I/O is within WPF\.data\
```

**3. Test portability:**
```powershell
# Copy to new location
Copy-Item -Recurse WPF D:\test-portable

# Run from new location
cd D:\test-portable
./SuperTUI_TestFixes.ps1

# Verify .data created in new location, not old location
Test-Path D:\test-portable\.data  # Should be True
```

## Summary

SuperTUI is now **100% portable and self-contained**:

- ✅ All data in `.data/` subdirectory
- ✅ No writes to system directories
- ✅ No writes to temp directories
- ✅ No writes to user directories
- ✅ Copy anywhere and run
- ✅ Delete `.data/` for clean reset
- ✅ `.gitignore` prevents committing user data

**The entire application lives in the WPF folder and nowhere else.**
