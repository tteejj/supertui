# SuperTUI Logger - Error tracking and debugging system
# Provides logging, error tracking, and diagnostics

class LogLevel {
    static [string]$DEBUG = "DEBUG"
    static [string]$INFO = "INFO"
    static [string]$WARN = "WARN"
    static [string]$ERROR = "ERROR"
    static [string]$FATAL = "FATAL"
}

class LogEntry {
    [DateTime]$Timestamp
    [string]$Level
    [string]$Component
    [string]$Message
    [object]$Data
    [string]$StackTrace

    LogEntry([string]$level, [string]$component, [string]$message, [object]$data, [string]$stack) {
        $this.Timestamp = Get-Date
        $this.Level = $level
        $this.Component = $component
        $this.Message = $message
        $this.Data = $data
        $this.StackTrace = $stack
    }

    [string] ToString() {
        $time = $this.Timestamp.ToString("HH:mm:ss.fff")
        $comp = $this.Component.PadRight(15)
        return "[$time] [$($this.Level.PadRight(5))] [$comp] $($this.Message)"
    }
}

class Logger {
    static [System.Collections.ArrayList]$Logs = [System.Collections.ArrayList]::new()
    static [string]$LogFile = ""
    static [string]$MinLevel = [LogLevel]::INFO
    static [bool]$ConsoleOutput = $false
    static [int]$MaxLogs = 1000
    static [bool]$Initialized = $false

    # Initialize logger
    static [void] Initialize([string]$logLevel, [bool]$console, [string]$logFile) {
        if ([Logger]::Initialized) { return }

        [Logger]::MinLevel = $logLevel
        [Logger]::ConsoleOutput = $console
        [Logger]::LogFile = $logFile

        # Ensure log directory exists
        if ($logFile) {
            $logDir = Split-Path $logFile
            if ($logDir -and -not (Test-Path $logDir)) {
                New-Item -ItemType Directory -Path $logDir -Force | Out-Null
            }
        }

        [Logger]::Initialized = $true
        [Logger]::Info("Logger", "Logger initialized (Level: $logLevel, Console: $console)")
    }

    # Log at specific level
    static [void] Log([string]$level, [string]$component, [string]$message, [object]$data, [string]$stack) {
        # Check if level meets minimum
        $levels = @([LogLevel]::DEBUG, [LogLevel]::INFO, [LogLevel]::WARN, [LogLevel]::ERROR, [LogLevel]::FATAL)
        $currentIndex = $levels.IndexOf($level)
        $minIndex = $levels.IndexOf([Logger]::MinLevel)

        if ($currentIndex -lt $minIndex) { return }

        # Create entry
        $entry = [LogEntry]::new($level, $component, $message, $data, $stack)

        # Add to in-memory log
        [Logger]::Logs.Add($entry) | Out-Null

        # Trim if too many
        if ([Logger]::Logs.Count -gt [Logger]::MaxLogs) {
            [Logger]::Logs.RemoveAt(0)
        }

        # Console output
        if ([Logger]::ConsoleOutput) {
            $color = switch ($level) {
                ([LogLevel]::DEBUG) { "Gray" }
                ([LogLevel]::INFO) { "White" }
                ([LogLevel]::WARN) { "Yellow" }
                ([LogLevel]::ERROR) { "Red" }
                ([LogLevel]::FATAL) { "DarkRed" }
                default { "White" }
            }
            Write-Host $entry.ToString() -ForegroundColor $color
        }

        # File output
        if ([Logger]::LogFile) {
            try {
                Add-Content -Path ([Logger]::LogFile) -Value $entry.ToString()
            } catch {
                # Silently fail on file write errors
            }
        }
    }

    # Convenience methods
    static [void] Debug([string]$component, [string]$message) {
        [Logger]::Debug($component, $message, $null)
    }

    static [void] Debug([string]$component, [string]$message, [object]$data) {
        [Logger]::Log([LogLevel]::DEBUG, $component, $message, $data, "")
    }

    static [void] Info([string]$component, [string]$message) {
        [Logger]::Info($component, $message, $null)
    }

    static [void] Info([string]$component, [string]$message, [object]$data) {
        [Logger]::Log([LogLevel]::INFO, $component, $message, $data, "")
    }

    static [void] Warn([string]$component, [string]$message) {
        [Logger]::Warn($component, $message, $null)
    }

    static [void] Warn([string]$component, [string]$message, [object]$data) {
        [Logger]::Log([LogLevel]::WARN, $component, $message, $data, "")
    }

    static [void] Error([string]$component, [string]$message) {
        [Logger]::Error($component, $message, $null)
    }

    static [void] Error([string]$component, [string]$message, [object]$data) {
        $stack = (Get-PSCallStack | Select-Object -Skip 1 | ForEach-Object { $_.Command }) -join " → "
        [Logger]::Log([LogLevel]::ERROR, $component, $message, $data, $stack)
    }

    static [void] Fatal([string]$component, [string]$message) {
        [Logger]::Fatal($component, $message, $null)
    }

    static [void] Fatal([string]$component, [string]$message, [object]$data) {
        $stack = (Get-PSCallStack | Select-Object -Skip 1 | ForEach-Object { $_.Command }) -join " → "
        [Logger]::Log([LogLevel]::FATAL, $component, $message, $data, $stack)
    }

    # Exception logging
    static [void] Exception([string]$component, [string]$message, [Exception]$exception) {
        $data = @{
            Exception = $exception.GetType().Name
            Message = $exception.Message
            StackTrace = $exception.StackTrace
        }
        [Logger]::Log([LogLevel]::ERROR, $component, $message, $data, $exception.StackTrace)
    }

    # Get recent logs
    static [LogEntry[]] GetRecent([int]$count) {
        $start = [Math]::Max(0, [Logger]::Logs.Count - $count)
        return [Logger]::Logs[$start..([Logger]::Logs.Count - 1)]
    }

    # Get logs by level
    static [LogEntry[]] GetByLevel([string]$level) {
        return [Logger]::Logs | Where-Object { $_.Level -eq $level }
    }

    # Get logs by component
    static [LogEntry[]] GetByComponent([string]$component) {
        return [Logger]::Logs | Where-Object { $_.Component -eq $component }
    }

    # Clear logs
    static [void] Clear() {
        [Logger]::Logs.Clear()
    }

    # Save logs to file
    static [void] SaveLogs([string]$path) {
        $content = [Logger]::Logs | ForEach-Object { $_.ToString() }
        Set-Content -Path $path -Value ($content -join "`n")
    }

    # Get error summary
    static [hashtable] GetErrorSummary() {
        $errors = [Logger]::GetByLevel([LogLevel]::ERROR)
        $warnings = [Logger]::GetByLevel([LogLevel]::WARN)

        return @{
            TotalErrors = $errors.Count
            TotalWarnings = $warnings.Count
            RecentErrors = $errors | Select-Object -Last 5
            RecentWarnings = $warnings | Select-Object -Last 5
            Components = ($errors | Group-Object Component | Sort-Object Count -Descending | Select-Object -First 5)
        }
    }
}

# Helper functions for easy access
function Write-Log {
    param(
        [Parameter(Mandatory)]
        [string]$Component,

        [Parameter(Mandatory)]
        [string]$Message,

        [string]$Level = "INFO",

        [object]$Data = $null
    )

    switch ($Level.ToUpper()) {
        "DEBUG" { [Logger]::Debug($Component, $Message, $Data) }
        "INFO" { [Logger]::Info($Component, $Message, $Data) }
        "WARN" { [Logger]::Warn($Component, $Message, $Data) }
        "ERROR" { [Logger]::Error($Component, $Message, $Data) }
        "FATAL" { [Logger]::Fatal($Component, $Message, $Data) }
    }
}

function Get-Logs {
    param(
        [int]$Count = 50,
        [string]$Level,
        [string]$Component
    )

    if ($Level) {
        return [Logger]::GetByLevel($Level)
    } elseif ($Component) {
        return [Logger]::GetByComponent($Component)
    } else {
        return [Logger]::GetRecent($Count)
    }
}

function Show-ErrorSummary {
    $summary = [Logger]::GetErrorSummary()

    Write-Host "`nError Summary:" -ForegroundColor Red
    Write-Host "  Total Errors:   $($summary.TotalErrors)" -ForegroundColor Yellow
    Write-Host "  Total Warnings: $($summary.TotalWarnings)" -ForegroundColor Yellow

    if ($summary.RecentErrors.Count -gt 0) {
        Write-Host "`nRecent Errors:" -ForegroundColor Red
        foreach ($err in $summary.RecentErrors) {
            Write-Host "  [$($err.Timestamp.ToString('HH:mm:ss'))] $($err.Component): $($err.Message)" -ForegroundColor Gray
        }
    }

    if ($summary.Components.Count -gt 0) {
        Write-Host "`nTop Error Components:" -ForegroundColor Red
        foreach ($comp in $summary.Components) {
            Write-Host "  $($comp.Name): $($comp.Count) errors" -ForegroundColor Gray
        }
    }
}

# Initialize with defaults
$logDir = Join-Path ([Environment]::GetFolderPath('UserProfile')) ".supertui" "logs"
$logFile = Join-Path $logDir "supertui-$(Get-Date -Format 'yyyy-MM-dd').log"
[Logger]::Initialize("INFO", $false, $logFile)
