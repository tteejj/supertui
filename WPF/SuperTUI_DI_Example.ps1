#!/usr/bin/env pwsh
<#
.SYNOPSIS
    SuperTUI Example Application with Dependency Injection

.DESCRIPTION
    Demonstrates how to use the DI container with SuperTUI.
    This is a minimal example showing DI integration.

.NOTES
    Requires: .NET 8.0, Microsoft.Extensions.DependencyInjection NuGet package
#>

# ============================================================================
# PHASE 1: COMPILE C# CODE
# ============================================================================

Write-Host "SuperTUI with Dependency Injection - Starting..." -ForegroundColor Cyan

# Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$coreFiles = @(
    "$scriptDir/Core/Models/*.cs",
    "$scriptDir/Core/Interfaces/*.cs",
    "$scriptDir/Core/Infrastructure/*.cs",
    "$scriptDir/Core/Components/*.cs",
    "$scriptDir/Core/Layouts/*.cs",
    "$scriptDir/Core/Services/*.cs",
    "$scriptDir/Core/ViewModels/*.cs",
    "$scriptDir/Core/DependencyInjection/*.cs",  # NEW: DI files
    "$scriptDir/Core/Extensions.cs",
    "$scriptDir/Widgets/*.cs"
)

# Find all C# files
$csFiles = $coreFiles | ForEach-Object { Get-ChildItem $_ -ErrorAction SilentlyContinue } | Where-Object { $_.Extension -eq ".cs" }

if ($csFiles.Count -eq 0) {
    Write-Error "No C# files found!"
    exit 1
}

Write-Host "Found $($csFiles.Count) C# files" -ForegroundColor Green

# NuGet package paths
$userProfile = $env:USERPROFILE
$nugetPackages = "$userProfile\.nuget\packages"
$diPackagePath = "$nugetPackages\microsoft.extensions.dependencyinjection\8.0.0\lib\net8.0"
$diAbstractionsPath = "$nugetPackages\microsoft.extensions.dependencyinjection.abstractions\8.0.0\lib\net8.0"

# Check if DI packages exist
$diDll = "$diPackagePath\Microsoft.Extensions.DependencyInjection.dll"
$diAbstractionsDll = "$diAbstractionsPath\Microsoft.Extensions.DependencyInjection.Abstractions.dll"

if (-not (Test-Path $diDll)) {
    Write-Warning "Microsoft.Extensions.DependencyInjection not found at $diDll"
    Write-Warning "Please run: dotnet add package Microsoft.Extensions.DependencyInjection"
    Write-Host "Continuing with traditional singleton pattern..." -ForegroundColor Yellow
    $useDI = $false
} else {
    Write-Host "DI package found: $diDll" -ForegroundColor Green
    $useDI = $true
}

# Referenced assemblies
$referencedAssemblies = @(
    "System",
    "System.Core",
    "System.Xml",
    "System.Xml.Linq",
    "PresentationCore",
    "PresentationFramework",
    "WindowsBase",
    "System.Xaml"
)

# Add DI assemblies if available
if ($useDI) {
    $referencedAssemblies += $diDll
    $referencedAssemblies += $diAbstractionsDll
}

# Compile C# code
Write-Host "Compiling C# code..." -ForegroundColor Cyan

try {
    Add-Type -TypeDefinition ($csFiles | Get-Content -Raw | Out-String) `
        -ReferencedAssemblies $referencedAssemblies `
        -Language CSharp `
        -IgnoreWarnings

    Write-Host "Compilation successful!" -ForegroundColor Green
} catch {
    Write-Error "Compilation failed: $_"
    Write-Error $_.Exception.Errors
    exit 1
}

# ============================================================================
# PHASE 2: INITIALIZE INFRASTRUCTURE
# ============================================================================

Write-Host "`nInitializing infrastructure..." -ForegroundColor Cyan

# Initialize logger
$logger = [SuperTUI.Infrastructure.Logger]::Instance
$memoryLogSink = [SuperTUI.Infrastructure.MemoryLogSink]::new()
$logger.AddSink($memoryLogSink)
$logger.SetMinLevel([SuperTUI.Infrastructure.LogLevel]::Debug)

$logger.Info("Startup", "SuperTUI starting with DI support")

# Initialize configuration
$configPath = "$env:TEMP\SuperTUI\config.json"
$configManager = [SuperTUI.Infrastructure.ConfigurationManager]::Instance
$configManager.Initialize($configPath)

# Initialize theme manager
$themeManager = [SuperTUI.Infrastructure.ThemeManager]::Instance
$themeManager.Initialize($null) # Use built-in themes

# Initialize security
$securityManager = [SuperTUI.Infrastructure.SecurityManager]::Instance
$securityManager.Initialize([SuperTUI.Infrastructure.SecurityMode]::Permissive)

# Initialize state persistence
$statePath = "$env:TEMP\SuperTUI\state.json"
$persistence = [SuperTUI.Infrastructure.StatePersistenceManager]::Instance
$persistence.Initialize($statePath)

# Initialize workspace manager
$workspaceManager = [SuperTUI.Infrastructure.WorkspaceManager]::Instance

Write-Host "Infrastructure initialized" -ForegroundColor Green

# ============================================================================
# PHASE 3: CREATE SERVICE PROVIDER (DI)
# ============================================================================

if ($useDI) {
    Write-Host "`nCreating dependency injection container..." -ForegroundColor Cyan

    try {
        # Create service provider
        $serviceProvider = [SuperTUI.DependencyInjection.ServiceProviderFactory]::CreateServiceProvider()

        Write-Host "Service provider created successfully!" -ForegroundColor Green

        # Verify services
        $diLogger = $serviceProvider.GetService([SuperTUI.Infrastructure.ILogger])
        $diTheme = $serviceProvider.GetService([SuperTUI.Infrastructure.IThemeManager])

        if ($diLogger -and $diTheme) {
            Write-Host "✓ Services resolved from DI container" -ForegroundColor Green
            $logger.Info("DI", "Dependency injection container operational")
        } else {
            Write-Warning "Services not found in DI container"
            $useDI = $false
        }
    } catch {
        Write-Warning "Failed to create service provider: $_"
        Write-Host "Falling back to singleton pattern" -ForegroundColor Yellow
        $useDI = $false
    }
}

# ============================================================================
# PHASE 4: CREATE WIDGETS
# ============================================================================

Write-Host "`nCreating widgets..." -ForegroundColor Cyan

if ($useDI) {
    # Create widgets using DI (preferred)
    Write-Host "Using DI container to create widgets" -ForegroundColor Green

    $clockWidget = $serviceProvider.GetService([SuperTUI.Widgets.ClockWidget])
    $counterWidget = $serviceProvider.GetService([SuperTUI.Widgets.CounterWidget])

    $logger.Info("Widgets", "Widgets created via DI container")
} else {
    # Create widgets using traditional method (backward compatible)
    Write-Host "Using traditional singleton pattern" -ForegroundColor Yellow

    $clockWidget = [SuperTUI.Widgets.ClockWidget]::new()
    $counterWidget = [SuperTUI.Widgets.CounterWidget]::new()

    $logger.Info("Widgets", "Widgets created via parameterless constructors")
}

# Initialize widgets
$clockWidget.WidgetName = "Clock"
$clockWidget.Initialize()

$counterWidget.WidgetName = "Counter"
$counterWidget.Initialize()

Write-Host "✓ Widgets created and initialized" -ForegroundColor Green

# ============================================================================
# PHASE 5: CREATE WINDOW
# ============================================================================

Write-Host "`nCreating application window..." -ForegroundColor Cyan

# Create WPF window
$window = [System.Windows.Window]@{
    Title = if ($useDI) { "SuperTUI - with DI" } else { "SuperTUI - Singleton Mode" }
    Width = 1200
    Height = 800
    WindowStartupLocation = "CenterScreen"
}

# Create grid layout
$grid = [System.Windows.Controls.Grid]::new()

# Add row definitions
$row1 = [System.Windows.Controls.RowDefinition]::new()
$row1.Height = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star)

$row2 = [System.Windows.Controls.RowDefinition]::new()
$row2.Height = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star)

$grid.RowDefinitions.Add($row1)
$grid.RowDefinitions.Add($row2)

# Add widgets to grid
[System.Windows.Controls.Grid]::SetRow($clockWidget.Root, 0)
[System.Windows.Controls.Grid]::SetRow($counterWidget.Root, 1)

$grid.Children.Add($clockWidget.Root)
$grid.Children.Add($counterWidget.Root)

$window.Content = $grid

# Apply theme
$theme = $themeManager.CurrentTheme
$window.Background = [System.Windows.Media.SolidColorBrush]::new($theme.Background)

Write-Host "✓ Window created" -ForegroundColor Green

# ============================================================================
# PHASE 6: DISPLAY STATUS
# ============================================================================

Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "SuperTUI Status Report" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

Write-Host "`nDependency Injection:" -ForegroundColor Yellow
if ($useDI) {
    Write-Host "  ✓ DI Container: ENABLED" -ForegroundColor Green
    Write-Host "  ✓ Widget Creation: Via ServiceProvider" -ForegroundColor Green
    Write-Host "  ✓ Service Registration: All services registered" -ForegroundColor Green
} else {
    Write-Host "  ⨯ DI Container: DISABLED (fallback to singletons)" -ForegroundColor Yellow
    Write-Host "  ⨯ Widget Creation: Via parameterless constructors" -ForegroundColor Yellow
}

Write-Host "`nInfrastructure Services:" -ForegroundColor Yellow
Write-Host "  ✓ Logger: Initialized" -ForegroundColor Green
Write-Host "  ✓ Configuration: Initialized" -ForegroundColor Green
Write-Host "  ✓ Theme Manager: Initialized ($($themeManager.CurrentTheme.Name) theme)" -ForegroundColor Green
Write-Host "  ✓ Security Manager: Initialized ($($securityManager.Mode) mode)" -ForegroundColor Green
Write-Host "  ✓ State Persistence: Initialized" -ForegroundColor Green

Write-Host "`nWidgets:" -ForegroundColor Yellow
Write-Host "  ✓ ClockWidget: Created" -ForegroundColor Green
Write-Host "  ✓ CounterWidget: Created" -ForegroundColor Green

Write-Host "`nLogs (last 5):" -ForegroundColor Yellow
$logs = $memoryLogSink.GetLogs()
$logs | Select-Object -Last 5 | ForEach-Object {
    Write-Host "  $_" -ForegroundColor Gray
}

Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan

# ============================================================================
# PHASE 7: SHOW WINDOW
# ============================================================================

Write-Host "`nLaunching SuperTUI window..." -ForegroundColor Cyan
Write-Host "Press Ctrl+C in console to exit`n" -ForegroundColor Yellow

# Show window
$window.ShowDialog() | Out-Null

# Cleanup
Write-Host "`nCleaning up..." -ForegroundColor Cyan
$clockWidget.Dispose()
$counterWidget.Dispose()
$logger.Info("Shutdown", "SuperTUI shutdown complete")

Write-Host "SuperTUI closed successfully" -ForegroundColor Green
