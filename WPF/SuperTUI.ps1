# SuperTUI - Production Entry Point

# Platform check
if ($PSVersionTable.Platform -eq 'Unix') {
    Write-Error "SuperTUI requires Windows (WPF is Windows-only)"
    exit 1
}

# Load WPF assemblies (required for PowerShell Core / pwsh)
# Windows PowerShell 5.1 loads these automatically, but PowerShell 7+ does not
Write-Host "Loading WPF assemblies..." -ForegroundColor Cyan
try {
    Add-Type -AssemblyName PresentationFramework
    Add-Type -AssemblyName PresentationCore
    Add-Type -AssemblyName WindowsBase
    Add-Type -AssemblyName System.Xaml
    Write-Host "WPF assemblies loaded" -ForegroundColor Green
} catch {
    Write-Error "Failed to load WPF assemblies. Ensure you're running on Windows with .NET Desktop Runtime installed."
    Write-Error "Error: $_"
    exit 1
}

# Load SuperTUI.dll
$dllPath = Join-Path $PSScriptRoot "bin/Release/net8.0-windows/SuperTUI.dll"

if (-not (Test-Path $dllPath)) {
    Write-Host "SuperTUI.dll not found. Building..." -ForegroundColor Yellow
    & "$PSScriptRoot/build.ps1"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
}

Write-Host "Loading SuperTUI..." -ForegroundColor Cyan

try {
    Add-Type -Path $dllPath
    Write-Host "Loaded successfully" -ForegroundColor Green
} catch {
    Write-Error "Failed to load SuperTUI.dll: $_"
    exit 1
}

# ============================================================================
# INITIALIZE INFRASTRUCTURE WITH DEPENDENCY INJECTION
# ============================================================================

Write-Host "`nInitializing SuperTUI infrastructure with DI..." -ForegroundColor Cyan

# Initialize ServiceContainer and register all services
Write-Host "Setting up ServiceContainer..." -ForegroundColor Cyan
$supertuiDataDir = Join-Path $PSScriptRoot ".supertui"
if (-not (Test-Path $supertuiDataDir)) {
    New-Item -Path $supertuiDataDir -ItemType Directory -Force | Out-Null
}
$configPath = Join-Path $supertuiDataDir "config.json"
$serviceContainer = [SuperTUI.DI.ServiceRegistration]::RegisterAllServices($configPath, $null)
Write-Host "ServiceContainer initialized and services registered" -ForegroundColor Green
Write-Host "  Config location: $configPath" -ForegroundColor Gray

# Get services from container (already initialized by RegisterAllServices)
$logger = $serviceContainer.GetService([SuperTUI.Infrastructure.ILogger])
$configManager = $serviceContainer.GetService([SuperTUI.Infrastructure.IConfigurationManager])
$themeManager = $serviceContainer.GetService([SuperTUI.Infrastructure.IThemeManager])
$securityManager = $serviceContainer.GetService([SuperTUI.Infrastructure.ISecurityManager])
$errorHandler = $serviceContainer.GetService([SuperTUI.Infrastructure.IErrorHandler])
$eventBus = $serviceContainer.GetService([SuperTUI.Core.IEventBus])
$stateManager = $serviceContainer.GetService([SuperTUI.Infrastructure.IStatePersistenceManager])
Write-Host "Core services resolved from container" -ForegroundColor Green

# Enable DEBUG level logging
$logger.SetMinLevel([SuperTUI.Infrastructure.LogLevel]::Debug)
Write-Host "Logger set to DEBUG level" -ForegroundColor Yellow

# Add file log sink to supertui folder
$logPath = Join-Path $supertuiDataDir "logs"
if (-not (Test-Path $logPath)) {
    New-Item -Path $logPath -ItemType Directory -Force | Out-Null
}
$fileLogSink = New-Object SuperTUI.Infrastructure.FileLogSink($logPath, "SuperTUI")
$logger.AddSink($fileLogSink)
Write-Host "File log sink added: $logPath\SuperTUI_*.log" -ForegroundColor Green

# Apply saved theme (or default to "Cyberpunk" to showcase new effects)
$savedThemeName = $configManager.Get("UI.Theme", "Cyberpunk")
$themeManager.ApplyTheme($savedThemeName)
Write-Host "Applied theme: $savedThemeName" -ForegroundColor Green

# Get ApplicationContext (singleton, not in DI container)
$appContext = [SuperTUI.Infrastructure.ApplicationContext]::Instance
Write-Host "ApplicationContext initialized" -ForegroundColor Green

# ============================================================================
# PANE-BASED ARCHITECTURE - No WidgetFactory needed
# ============================================================================
# MainWindow now uses PaneFactory (DI-based) for creating panes
# Available panes: :tasks, :notes, :files
# Open via command palette (Ctrl+Space or :)
Write-Host "Pane-based architecture initialized (no widget registration needed)" -ForegroundColor Green

Write-Host "Infrastructure initialized with DI" -ForegroundColor Green

# ============================================================================
# OVERLAYS REMOVED - PANE-BASED ARCHITECTURE
# ============================================================================
# Old overlays (ShortcutOverlay, QuickJumpOverlay, CRTEffectsOverlay) removed
# MainWindow now uses CommandPalettePane (Ctrl+Space or :) for all commands
# Pane navigation via Alt+Arrows, workspace switching via Alt+1-9
# See MainWindow.xaml.cs for keyboard shortcuts
Write-Host "Pane-based architecture (no overlays needed)" -ForegroundColor Green

# ============================================================================
# GLOBAL EXCEPTION HANDLER
# ============================================================================

# Catch unhandled exceptions as last resort
[System.AppDomain]::CurrentDomain.add_UnhandledException({
    param($sender, $e)
    $exception = $e.ExceptionObject

    # Log critical error with full details
    $logger.Critical("Application",
        "UNHANDLED EXCEPTION CAUGHT`n" +
        "  Message: $($exception.Message)`n" +
        "  Type: $($exception.GetType().FullName)`n" +
        "  Stack Trace:`n$($exception.StackTrace)", $exception)

    # Show user-friendly error dialog
    $logPath = "$env:TEMP\SuperTUI\Logs"
    [System.Windows.MessageBox]::Show(
        "A critical error occurred. The application will attempt to continue but may be unstable.`n`n" +
        "Error: $($exception.Message)`n`n" +
        "Details have been logged to:`n$logPath`n`n" +
        "Please report this issue with the log files.",
        "SuperTUI - Critical Error",
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Error)

    # Check if CLR is terminating
    if ($e.IsTerminating)
    {
        $logger.Critical("Application", "CLR is terminating. Application will exit.")

        # Save state before exit (best effort)
        try
        {
            Write-Host "Attempting emergency state save..." -ForegroundColor Yellow
            # StatePersistenceManager would go here if initialized
        }
        catch
        {
            $logger.Error("Application", "Emergency state save failed: $_")
        }
    }
})

Write-Host "Global exception handler registered" -ForegroundColor Green

# ============================================================================
# CREATE MAIN WINDOW
# ============================================================================

Write-Host "`nCreating MainWindow..." -ForegroundColor Cyan
$window = New-Object SuperTUI.MainWindow($serviceContainer)
Write-Host "MainWindow created" -ForegroundColor Green

# ============================================================================
# SHOW WINDOW
# ============================================================================

Write-Host "`nStarting SuperTUI..." -ForegroundColor Green
Write-Host "Keyboard Shortcuts:" -ForegroundColor Yellow
Write-Host "  Ctrl+Space or :       Command palette" -ForegroundColor Cyan
Write-Host "  Ctrl+T                Open tasks pane" -ForegroundColor Gray
Write-Host "  Ctrl+N                Open notes pane" -ForegroundColor Gray
Write-Host "  Ctrl+P                Open processing pane" -ForegroundColor Gray
Write-Host "  Alt+1-9               Switch workspaces" -ForegroundColor Gray
Write-Host "  Alt+Arrows            Navigate between panes" -ForegroundColor Gray
Write-Host "  Alt+Shift+Arrows      Move panes" -ForegroundColor Gray
Write-Host "  Ctrl+Shift+Q          Close focused pane" -ForegroundColor Gray
Write-Host ""

$window.ShowDialog() | Out-Null
