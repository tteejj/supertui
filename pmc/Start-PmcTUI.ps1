# Start-PmcTUI - Entry point for new PMC TUI architecture
# Replaces old ConsoleUI.Core.ps1 monolithic approach

# Setup logging
$global:PmcTuiLogFile = "/tmp/pmc-tui-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

function Write-PmcTuiLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logLine = "[$timestamp] [$Level] $Message"
    Add-Content -Path $global:PmcTuiLogFile -Value $logLine
    if ($Level -eq "ERROR") {
        Write-Host $logLine -ForegroundColor Red
    }
}

Write-PmcTuiLog "Loading PMC module..." "INFO"

try {
    # Import PMC module for data functions
    Import-Module "$PSScriptRoot/../Pmc.Strict.psd1" -Force -ErrorAction Stop
    Write-PmcTuiLog "PMC module loaded" "INFO"
} catch {
    Write-PmcTuiLog "Failed to load PMC module: $_" "ERROR"
    Write-PmcTuiLog $_.ScriptStackTrace "ERROR"
    throw
}

Write-PmcTuiLog "Loading dependencies (FieldSchemas, etc.)..." "INFO"

try {
    . "$PSScriptRoot/DepsLoader.ps1"
    Write-PmcTuiLog "Dependencies loaded" "INFO"
} catch {
    Write-PmcTuiLog "Failed to load dependencies: $_" "ERROR"
    Write-PmcTuiLog $_.ScriptStackTrace "ERROR"
    throw
}

Write-PmcTuiLog "Loading PmcApplication..." "INFO"

try {
    . "$PSScriptRoot/PmcApplication.ps1"
    Write-PmcTuiLog "PmcApplication loaded" "INFO"
} catch {
    Write-PmcTuiLog "Failed to load PmcApplication: $_" "ERROR"
    Write-PmcTuiLog $_.ScriptStackTrace "ERROR"
    throw
}

Write-PmcTuiLog "Loading PmcScreen base class..." "INFO"

try {
    . "$PSScriptRoot/PmcScreen.ps1"
    Write-PmcTuiLog "PmcScreen loaded" "INFO"
} catch {
    Write-PmcTuiLog "Failed to load PmcScreen: $_" "ERROR"
    Write-PmcTuiLog $_.ScriptStackTrace "ERROR"
    throw
}

Write-PmcTuiLog "Loading screens..." "INFO"

try {
    . "$PSScriptRoot/screens/TaskListScreen.ps1"
    Write-PmcTuiLog "TaskListScreen loaded" "INFO"
    . "$PSScriptRoot/screens/BlockedTasksScreen.ps1"
    Write-PmcTuiLog "BlockedTasksScreen loaded" "INFO"
} catch {
    Write-PmcTuiLog "Failed to load screens: $_" "ERROR"
    Write-PmcTuiLog $_.ScriptStackTrace "ERROR"
    throw
}

<#
.SYNOPSIS
Start PMC TUI with new architecture

.DESCRIPTION
Entry point for SpeedTUI-based PMC interface.
Creates application and launches screens.

.PARAMETER StartScreen
Which screen to launch (default: BlockedTasks)

.EXAMPLE
Start-PmcTUI
Start-PmcTUI -StartScreen BlockedTasks
#>
function Start-PmcTUI {
    param(
        [string]$StartScreen = "TaskList"
    )

    Write-Host "Starting PMC TUI (SpeedTUI Architecture)..." -ForegroundColor Cyan
    Write-PmcTuiLog "Starting PMC TUI with screen: $StartScreen" "INFO"

    try {
        # Create application
        Write-PmcTuiLog "Creating PmcApplication..." "INFO"
        $global:PmcApp = [PmcApplication]::new()
        Write-PmcTuiLog "PmcApplication created" "INFO"

        # Launch requested screen
        Write-PmcTuiLog "Launching screen: $StartScreen" "INFO"
        switch ($StartScreen) {
            'TaskList' {
                Write-PmcTuiLog "Creating TaskListScreen..." "INFO"
                $screen = [TaskListScreen]::new()
                Write-PmcTuiLog "Pushing screen to app..." "INFO"
                $global:PmcApp.PushScreen($screen)
                Write-PmcTuiLog "Screen pushed successfully" "INFO"
            }
            'BlockedTasks' {
                Write-PmcTuiLog "Creating BlockedTasksScreen..." "INFO"
                $screen = [BlockedTasksScreen]::new()
                Write-PmcTuiLog "Pushing screen to app..." "INFO"
                $global:PmcApp.PushScreen($screen)
                Write-PmcTuiLog "Screen pushed successfully" "INFO"
            }
            'Demo' {
                Write-PmcTuiLog "Loading DemoScreen..." "INFO"
                . "$PSScriptRoot/DemoScreen.ps1"
                $screen = [DemoScreen]::new()
                $global:PmcApp.PushScreen($screen)
                Write-PmcTuiLog "Demo screen pushed" "INFO"
            }
            default {
                Write-PmcTuiLog "Unknown screen: $StartScreen" "ERROR"
                throw "Unknown screen: $StartScreen"
            }
        }

        # Run event loop
        Write-PmcTuiLog "Starting event loop..." "INFO"
        $global:PmcApp.Run()
        Write-PmcTuiLog "Event loop exited normally" "INFO"

    } catch {
        Write-PmcTuiLog "EXCEPTION: $_" "ERROR"
        Write-PmcTuiLog "Stack trace: $($_.ScriptStackTrace)" "ERROR"
        Write-PmcTuiLog "Exception details: $($_.Exception | Out-String)" "ERROR"

        Write-Host "`e[?25h"  # Show cursor
        Write-Host "`e[2J`e[H"  # Clear screen
        Write-Host "PMC TUI Error: $_" -ForegroundColor Red
        Write-Host "Log file: $global:PmcTuiLogFile" -ForegroundColor Yellow
        Write-Host $_.ScriptStackTrace -ForegroundColor Red
        throw
    } finally {
        # Cleanup
        Write-PmcTuiLog "Cleanup - showing cursor and resetting terminal" "INFO"
        Write-Host "`e[?25h"  # Show cursor
        Write-Host "`e[0m"    # Reset colors
        Write-Host "Log saved to: $global:PmcTuiLogFile" -ForegroundColor Gray
    }
}

# Allow direct execution
if ($MyInvocation.InvocationName -match 'Start-PmcTUI') {
    Start-PmcTUI @args
}
