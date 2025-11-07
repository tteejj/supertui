# ConsoleUI - Standalone console user interface
# This copy removes CLI coupling and uses local deps only.

param(
    [switch]$Start
)

# Require PowerShell 7+ for null-coalescing operator (??) and ANSI handling
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "ConsoleUI requires PowerShell 7+. Detected: $($PSVersionTable.PSVersion)" -ForegroundColor Red
    Write-Host "Please run with 'pwsh' (PowerShell 7) instead of 'powershell'." -ForegroundColor Yellow
    return
}

. "$PSScriptRoot/Debug.ps1"
Write-Host "Loading ConsoleUI..." -ForegroundColor Cyan

# Load main UI implementation (core only). Start-PmcConsoleUI is provided by Core.
. "$PSScriptRoot/ConsoleUI.Core.ps1"

# Convenience alias for discoverability
function Start-ConsoleUI { [CmdletBinding()] param() Start-PmcConsoleUI }

# Optional auto-start when invoked with -Start
if ($Start) { Start-PmcConsoleUI }
