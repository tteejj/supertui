# SuperTUI State Migration Test
# Tests that the state migration infrastructure works correctly

Add-Type -Path "$PSScriptRoot/Core/Extensions.cs" -ReferencedAssemblies @(
    "System.Windows.Forms",
    "PresentationFramework",
    "PresentationCore",
    "WindowsBase",
    "System.Xaml"
)

# Also load Infrastructure for Logger
Add-Type -Path "$PSScriptRoot/Core/Infrastructure.cs" -ReferencedAssemblies @(
    "PresentationFramework",
    "PresentationCore",
    "WindowsBase",
    "System.Xaml"
)

Write-Host "=== SuperTUI State Migration Test ===" -ForegroundColor Cyan
Write-Host ""

# Create test migration class
$migrationCode = @"
using System;
using System.Collections.Generic;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Test
{
    /// <summary>
    /// Test migration that adds a 'MigrationTestField' to ApplicationState
    /// </summary>
    public class TestMigration_1_0_to_1_1 : IStateMigration
    {
        public string FromVersion => "1.0";
        public string ToVersion => "1.1";

        public StateSnapshot Migrate(StateSnapshot snapshot)
        {
            Logger.Instance.Info("TestMigration", "Migrating from 1.0 to 1.1");

            // Add test field to ApplicationState
            if (!snapshot.ApplicationState.ContainsKey("MigrationTestField"))
            {
                snapshot.ApplicationState["MigrationTestField"] = "MigrationSuccessful";
                Logger.Instance.Info("TestMigration", "Added MigrationTestField");
            }

            // Add TestMigrationTimestamp to each workspace
            foreach (var workspace in snapshot.Workspaces)
            {
                if (!workspace.CustomData.ContainsKey("TestMigrationTimestamp"))
                {
                    workspace.CustomData["TestMigrationTimestamp"] = DateTime.Now;
                    Logger.Instance.Info("TestMigration", $"Added timestamp to workspace: {workspace.Name}");
                }
            }

            // Update version
            snapshot.Version = "1.1";

            Logger.Instance.Info("TestMigration", "Migration completed successfully");
            return snapshot;
        }
    }
}
"@

Add-Type -TypeDefinition $migrationCode -ReferencedAssemblies @(
    "$PSScriptRoot/Core/Infrastructure.cs",
    "$PSScriptRoot/Core/Extensions.cs",
    "PresentationFramework",
    "PresentationCore",
    "WindowsBase"
)

Write-Host "✓ Test migration class compiled" -ForegroundColor Green

# Set up portable data directory
$dataDir = Join-Path $PSScriptRoot ".data"
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

# Create a fake old state file (version 1.0) in local directory
$testStateFile = Join-Path $dataDir "migration_test.json"

$oldState = @{
    Version = "1.0"
    Timestamp = (Get-Date).ToString("o")
    ApplicationState = @{
        ExistingField = "ExistingValue"
    }
    Workspaces = @(
        @{
            Name = "Test Workspace 1"
            CustomData = @{
                ExistingWorkspaceField = 42
            }
            WidgetStates = @()
        }
        @{
            Name = "Test Workspace 2"
            CustomData = @{
                ExistingWorkspaceField = 99
            }
            WidgetStates = @()
        }
    )
    UserData = @{}
} | ConvertTo-Json -Depth 10

$oldState | Out-File -FilePath $testStateFile -Encoding utf8
Write-Host "✓ Created test state file: $testStateFile" -ForegroundColor Green
Write-Host "  Version: 1.0 (old version)" -ForegroundColor Yellow

# Initialize Logger in local directory
$logFile = Join-Path $dataDir "migration_test.log"
[SuperTUI.Infrastructure.Logger]::Instance.Initialize($logFile)

# Create StateMigrationManager and register test migration
$migrationManager = New-Object SuperTUI.Core.StateMigrationManager
$testMigration = New-Object SuperTUI.Core.Test.TestMigration_1_0_to_1_1
$migrationManager.RegisterMigration($testMigration)

Write-Host "✓ Registered test migration: 1.0 -> 1.1" -ForegroundColor Green
Write-Host ""

# Load the old state
Write-Host "Loading old state (version 1.0)..." -ForegroundColor Cyan
$json = Get-Content -Path $testStateFile -Raw
$snapshot = [System.Text.Json.JsonSerializer]::Deserialize(
    $json,
    [SuperTUI.Core.StateSnapshot]
)

Write-Host "✓ Loaded state" -ForegroundColor Green
Write-Host "  Version: $($snapshot.Version)" -ForegroundColor Yellow
Write-Host "  Workspaces: $($snapshot.Workspaces.Count)" -ForegroundColor Yellow
Write-Host "  ApplicationState keys: $($snapshot.ApplicationState.Keys.Count)" -ForegroundColor Yellow
Write-Host ""

# Check pre-migration state
Write-Host "Pre-migration checks:" -ForegroundColor Cyan
$hasMigrationField = $snapshot.ApplicationState.ContainsKey("MigrationTestField")
Write-Host "  MigrationTestField exists: $hasMigrationField" -ForegroundColor $(if ($hasMigrationField) { "Red" } else { "Green" })

# Apply migration
Write-Host ""
Write-Host "Applying migration..." -ForegroundColor Cyan
$migratedSnapshot = $migrationManager.MigrateToCurrentVersion($snapshot)

Write-Host "✓ Migration completed" -ForegroundColor Green
Write-Host "  New version: $($migratedSnapshot.Version)" -ForegroundColor Yellow
Write-Host ""

# Verify migration
Write-Host "Post-migration verification:" -ForegroundColor Cyan

$tests = @{
    "Version updated to 1.1" = $migratedSnapshot.Version -eq "1.1"
    "MigrationTestField added" = $migratedSnapshot.ApplicationState.ContainsKey("MigrationTestField")
    "MigrationTestField value correct" = $migratedSnapshot.ApplicationState["MigrationTestField"] -eq "MigrationSuccessful"
    "Workspace 1 has timestamp" = $migratedSnapshot.Workspaces[0].CustomData.ContainsKey("TestMigrationTimestamp")
    "Workspace 2 has timestamp" = $migratedSnapshot.Workspaces[1].CustomData.ContainsKey("TestMigrationTimestamp")
    "Original data preserved" = $migratedSnapshot.ApplicationState["ExistingField"] -eq "ExistingValue"
}

$allPassed = $true
foreach ($testName in $tests.Keys) {
    $result = $tests[$testName]
    $status = if ($result) { "✓" } else { "✗"; $allPassed = $false }
    $color = if ($result) { "Green" } else { "Red" }
    Write-Host "  $status $testName" -ForegroundColor $color
}

Write-Host ""
if ($allPassed) {
    Write-Host "=== ALL TESTS PASSED ===" -ForegroundColor Green
    Write-Host "State migration infrastructure is working correctly!" -ForegroundColor Green
} else {
    Write-Host "=== SOME TESTS FAILED ===" -ForegroundColor Red
    Write-Host "State migration infrastructure has issues!" -ForegroundColor Red
}

# Cleanup
Write-Host ""
Write-Host "Cleaning up test files..." -ForegroundColor Yellow
Remove-Item -Path $testStateFile -ErrorAction SilentlyContinue
Write-Host "✓ Test complete" -ForegroundColor Green
