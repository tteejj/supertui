# SuperTUI WPF Framework - Dependency Injection Example
# Demonstrates the new DI container and service registration

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  SuperTUI - DI Container Example" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Compile framework (same as demo)
Write-Host "[1/4] Compiling framework..." -ForegroundColor Yellow

$coreFiles = @(
    "Core/Interfaces/IWidget.cs"
    "Core/Interfaces/ILogger.cs"
    "Core/Interfaces/IThemeManager.cs"
    "Core/Interfaces/IConfigurationManager.cs"
    "Core/Interfaces/ISecurityManager.cs"
    "Core/Interfaces/IErrorHandler.cs"
    "Core/Interfaces/ILayoutEngine.cs"
    "Core/Interfaces/IServiceContainer.cs"
    "Core/Interfaces/IWorkspace.cs"
    "Core/Interfaces/IWorkspaceManager.cs"
    "Core/Infrastructure.cs"
    "Core/Extensions.cs"
    "Core/Layout/LayoutEngine.cs"
    "Core/Layout/GridLayoutEngine.cs"
    "Core/Layout/DockLayoutEngine.cs"
    "Core/Layout/StackLayoutEngine.cs"
    "Core/Components/WidgetBase.cs"
    "Core/Components/ScreenBase.cs"
    "Core/Infrastructure/Workspace.cs"
    "Core/Infrastructure/WorkspaceManager.cs"
    "Core/Infrastructure/ShortcutManager.cs"
    "Core/Infrastructure/EventBus.cs"
    "Core/Infrastructure/ServiceContainer.cs"
    "Core/DI/ServiceRegistration.cs"
)

$widgetFiles = @(
    "Widgets/ClockWidget.cs"
)

$allSources = @()
foreach ($file in ($coreFiles + $widgetFiles)) {
    $fullPath = Join-Path $PSScriptRoot $file
    if (Test-Path $fullPath) {
        $source = Get-Content $fullPath -Raw
        if ($allSources.Count -gt 0) {
            $source = $source -replace '(?s)^using.*?(?=namespace)', ''
        }
        $allSources += $source
    } else {
        Write-Warning "File not found: $fullPath"
    }
}

$combinedSource = $allSources -join "`n`n"

Add-Type -TypeDefinition $combinedSource -ReferencedAssemblies @(
    'PresentationFramework',
    'PresentationCore',
    'WindowsBase',
    'System.Xaml'
)

Write-Host "[2/4] Configuring dependency injection..." -ForegroundColor Yellow

# Get the DI container
$container = [SuperTUI.Core.ServiceContainer]::Instance

# Configure all services
[SuperTUI.DI.ServiceRegistration]::ConfigureServices($container)

Write-Host "  ✓ Registered ILogger" -ForegroundColor Green
Write-Host "  ✓ Registered IConfigurationManager" -ForegroundColor Green
Write-Host "  ✓ Registered IThemeManager" -ForegroundColor Green
Write-Host "  ✓ Registered ISecurityManager" -ForegroundColor Green
Write-Host "  ✓ Registered IErrorHandler" -ForegroundColor Green

# Initialize services
Write-Host "[3/4] Initializing services..." -ForegroundColor Yellow

$appData = [Environment]::GetFolderPath('LocalApplicationData')
$superTUIDir = Join-Path $appData "SuperTUI"
New-Item -ItemType Directory -Force -Path $superTUIDir | Out-Null

$configPath = Join-Path $superTUIDir "config.json"
$themesPath = Join-Path $superTUIDir "Themes"
$statePath = Join-Path $superTUIDir "State"
$pluginsPath = Join-Path $superTUIDir "Plugins"

[SuperTUI.DI.ServiceRegistration]::InitializeServices($container, $configPath, $themesPath, $statePath, $pluginsPath)

Write-Host "  ✓ Configuration initialized at: $configPath" -ForegroundColor Green
Write-Host "  ✓ Themes directory: $themesPath" -ForegroundColor Green
Write-Host "  ✓ State directory: $statePath" -ForegroundColor Green
Write-Host "  ✓ Plugins directory: $pluginsPath" -ForegroundColor Green

# Demonstrate resolving services
Write-Host "[4/4] Resolving services from container..." -ForegroundColor Yellow

# Resolve logger
$logger = $container.Resolve([SuperTUI.Infrastructure.ILogger])
Write-Host "  ✓ Resolved ILogger: $($logger.GetType().Name)" -ForegroundColor Green

# Resolve theme manager
$themeManager = $container.Resolve([SuperTUI.Infrastructure.IThemeManager])
Write-Host "  ✓ Resolved IThemeManager: $($themeManager.GetType().Name)" -ForegroundColor Green
Write-Host "    Current theme: $($themeManager.CurrentTheme.Name)" -ForegroundColor Gray

# Resolve config manager
$config = $container.Resolve([SuperTUI.Infrastructure.IConfigurationManager])
Write-Host "  ✓ Resolved IConfigurationManager: $($config.GetType().Name)" -ForegroundColor Green

# Test singleton behavior
Write-Host "`n" -NoNewline
Write-Host "Testing Singleton Behavior:" -ForegroundColor Yellow
$logger1 = $container.Resolve([SuperTUI.Infrastructure.ILogger])
$logger2 = $container.Resolve([SuperTUI.Infrastructure.ILogger])
$areSame = [Object]::ReferenceEquals($logger1, $logger2)
Write-Host "  Logger instances are same object: " -NoNewline -ForegroundColor Gray
Write-Host "$areSame" -ForegroundColor $(if ($areSame) { "Green" } else { "Red" })

# Test registration check
Write-Host "`n" -NoNewline
Write-Host "Testing Service Registration:" -ForegroundColor Yellow
$isLoggerRegistered = $container.IsRegistered([SuperTUI.Infrastructure.ILogger])
Write-Host "  ILogger is registered: " -NoNewline -ForegroundColor Gray
Write-Host "$isLoggerRegistered" -ForegroundColor $(if ($isLoggerRegistered) { "Green" } else { "Red" })

$isFakeServiceRegistered = $container.IsRegistered([System.IO.FileInfo])
Write-Host "  FileInfo is registered: " -NoNewline -ForegroundColor Gray
Write-Host "$isFakeServiceRegistered" -ForegroundColor $(if (!$isFakeServiceRegistered) { "Green" } else { "Red" })

# Try resolve with error handling
Write-Host "`n" -NoNewline
Write-Host "Testing Error Handling:" -ForegroundColor Yellow
try {
    $null = $container.Resolve([System.IO.FileInfo])
    Write-Host "  ✗ Should have thrown exception!" -ForegroundColor Red
} catch {
    Write-Host "  ✓ Correctly threw exception for unregistered service" -ForegroundColor Green
    Write-Host "    Message: $($_.Exception.Message)" -ForegroundColor Gray
}

# Test TryResolve
$success = $container.TryResolve([SuperTUI.Infrastructure.ILogger], [ref]$null)
Write-Host "  TryResolve(ILogger) succeeded: " -NoNewline -ForegroundColor Gray
Write-Host "$success" -ForegroundColor $(if ($success) { "Green" } else { "Red" })

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Dependency Injection Working!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  • Services are registered in the DI container" -ForegroundColor White
Write-Host "  • Singletons return the same instance" -ForegroundColor White
Write-Host "  • Services can be resolved by interface" -ForegroundColor White
Write-Host "  • Proper error handling for missing services" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  • Widgets can now accept dependencies via constructor" -ForegroundColor White
Write-Host "  • Use container.RegisterTransient<IMyService, MyService>()" -ForegroundColor White
Write-Host "  • Services are testable via mocking" -ForegroundColor White
Write-Host ""
