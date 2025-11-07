# ConsoleUI Debug Logger
# Simple debug logging system for troubleshooting

# Consolidated debug output: forward to central Write-PmcDebug
# The standalone ConsoleUI no longer writes its own logfile; it routes
# messages into the centralized debug system configured via deps/Debug.ps1.

# Optional opt-in level boost via PMC_CONSOLEUI_DEBUG
try {
    if ($env:PMC_CONSOLEUI_DEBUG -and ($env:PMC_CONSOLEUI_DEBUG -match '^(?i:1|true|yes|on)$')) {
        try { Initialize-PmcDebugSystem -Level 1 } catch {}
    }
} catch {}

function Write-ConsoleUIDebug {
    param(
        [string]$Message,
        [string]$Category = "INFO"
    )

    try {
        Write-PmcDebug -Level 1 -Category ("ConsoleUI:" + $Category) -Message $Message
    } catch {}
}

function Clear-ConsoleUIDebugLog {
    # No-op: central debug handles rotation/clearing; keep for compatibility
    Write-ConsoleUIDebug "Debug initialized" "SYSTEM"
}

function Get-ConsoleUIDebugLog {
    param([int]$Lines = 50)

    # Read central debug log path if available
    try {
        $path = Get-PmcDebugLogPath
        if (Test-Path $path) { Get-Content $path -Tail $Lines } else { Write-Host "No debug log found at: $path" -ForegroundColor Yellow }
    } catch { Write-Host "Debug log unavailable" -ForegroundColor Yellow }
}

# Centralized logging takes precedence
