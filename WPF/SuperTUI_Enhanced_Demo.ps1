# SuperTUI WPF Framework - Enhanced Demo with Logging, Config, Themes, Security
# Demonstrates all infrastructure features

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  SuperTUI Enhanced Framework Demo" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# ============================================================================
# COMPILE FRAMEWORK
# ============================================================================

Write-Host "[1/4] Compiling framework..." -ForegroundColor Yellow

$frameworkSource = Get-Content "$PSScriptRoot/Core/Framework.cs" -Raw
$infrastructureSource = Get-Content "$PSScriptRoot/Core/Infrastructure.cs" -Raw
$extensionsSource = Get-Content "$PSScriptRoot/Core/Extensions.cs" -Raw

# Combine all sources - remove duplicate using statements from later files
$infrastructureSource = $infrastructureSource -replace '(?s)^using.*?(?=namespace)', ''
$extensionsSource = $extensionsSource -replace '(?s)^using.*?(?=namespace)', ''

$combinedSource = @"
$frameworkSource

$infrastructureSource

$extensionsSource
"@

try {
    Add-Type -TypeDefinition $combinedSource -ReferencedAssemblies @(
        'PresentationFramework',
        'PresentationCore',
        'WindowsBase',
        'System.Xaml'
    ) -ErrorAction Stop

    Write-Host "    ✓ Framework compiled successfully" -ForegroundColor Green
} catch {
    Write-Host "    ✗ Framework compilation failed!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

# ============================================================================
# INITIALIZE INFRASTRUCTURE
# ============================================================================

Write-Host "[2/4] Initializing infrastructure..." -ForegroundColor Yellow

# Initialize logger with file and memory sinks
$logDir = Join-Path $env:LOCALAPPDATA "SuperTUI\Logs"
$fileLogSink = New-Object SuperTUI.Infrastructure.FileLogSink($logDir, "supertui", 5, 10)
$memoryLogSink = New-Object SuperTUI.Infrastructure.MemoryLogSink(500)

[SuperTUI.Infrastructure.Logger]::Instance.AddSink($fileLogSink)
[SuperTUI.Infrastructure.Logger]::Instance.AddSink($memoryLogSink)
[SuperTUI.Infrastructure.Logger]::Instance.SetMinLevel([SuperTUI.Infrastructure.LogLevel]::Debug)

[SuperTUI.Infrastructure.Logger]::Instance.Info("Demo", "SuperTUI Enhanced Demo starting")
Write-Host "    ✓ Logging system initialized" -ForegroundColor Green

# Initialize configuration
$configPath = Join-Path $env:LOCALAPPDATA "SuperTUI\config.json"
[SuperTUI.Infrastructure.ConfigurationManager]::Instance.Initialize($configPath)
Write-Host "    ✓ Configuration system initialized" -ForegroundColor Green

# Initialize theme system
$themesDir = Join-Path $env:LOCALAPPDATA "SuperTUI\Themes"
[SuperTUI.Infrastructure.ThemeManager]::Instance.Initialize($themesDir)
Write-Host "    ✓ Theme system initialized" -ForegroundColor Green

# Initialize security manager
[SuperTUI.Infrastructure.SecurityManager]::Instance.Initialize()
Write-Host "    ✓ Security system initialized" -ForegroundColor Green

# Initialize state persistence
$stateDir = Join-Path $env:LOCALAPPDATA "SuperTUI\State"
[SuperTUI.Extensions.StatePersistenceManager]::Instance.Initialize($stateDir)
Write-Host "    ✓ State persistence initialized" -ForegroundColor Green

# Initialize plugin system
$pluginsDir = Join-Path $env:LOCALAPPDATA "SuperTUI\Plugins"
$pluginContext = New-Object SuperTUI.Extensions.PluginContext
$pluginContext.Logger = [SuperTUI.Infrastructure.Logger]::Instance
$pluginContext.Config = [SuperTUI.Infrastructure.ConfigurationManager]::Instance
$pluginContext.Themes = [SuperTUI.Infrastructure.ThemeManager]::Instance

[SuperTUI.Extensions.PluginManager]::Instance.Initialize($pluginsDir, $pluginContext)
Write-Host "    ✓ Plugin system initialized" -ForegroundColor Green

# Initialize performance monitor
[SuperTUI.Extensions.PerformanceMonitor]::Instance | Out-Null
Write-Host "    ✓ Performance monitor initialized" -ForegroundColor Green

[SuperTUI.Infrastructure.Logger]::Instance.Info("Demo", "All infrastructure systems initialized")

# ============================================================================
# SHOW INFRASTRUCTURE STATUS
# ============================================================================

Write-Host "`n[3/4] Infrastructure status:" -ForegroundColor Yellow

# Configuration
Write-Host "  Configuration:" -ForegroundColor Cyan
Write-Host "    - Config file: $configPath" -ForegroundColor Gray
Write-Host "    - App title: $([SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get('App.Title', ''))" -ForegroundColor Gray
Write-Host "    - Log level: $([SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get('App.LogLevel', ''))" -ForegroundColor Gray
Write-Host "    - Theme: $([SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get('UI.Theme', ''))" -ForegroundColor Gray

# Theme
$currentTheme = [SuperTUI.Infrastructure.ThemeManager]::Instance.CurrentTheme
Write-Host "  Theme:" -ForegroundColor Cyan
Write-Host "    - Active theme: $($currentTheme.Name)" -ForegroundColor Gray
Write-Host "    - Type: $(if ($currentTheme.IsDark) { 'Dark' } else { 'Light' })" -ForegroundColor Gray
Write-Host "    - Available themes: $([SuperTUI.Infrastructure.ThemeManager]::Instance.GetAvailableThemes().Count)" -ForegroundColor Gray

# Logging
Write-Host "  Logging:" -ForegroundColor Cyan
Write-Host "    - Log directory: $logDir" -ForegroundColor Gray
Write-Host "    - Memory buffer: $($memoryLogSink.GetEntries().Count) entries" -ForegroundColor Gray

# Security
Write-Host "  Security:" -ForegroundColor Cyan
Write-Host "    - File validation: $([SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get('Security.ValidateFileAccess', ''))" -ForegroundColor Gray
Write-Host "    - Script execution: $([SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get('Security.AllowScriptExecution', ''))" -ForegroundColor Gray
Write-Host "    - Max file size: $([SuperTUI.Infrastructure.ConfigurationManager]::Instance.Get('Security.MaxFileSize', '')) MB" -ForegroundColor Gray

# State
$backupCount = [SuperTUI.Extensions.StatePersistenceManager]::Instance.GetAvailableBackups().Count
Write-Host "  State Persistence:" -ForegroundColor Cyan
Write-Host "    - State directory: $stateDir" -ForegroundColor Gray
Write-Host "    - Backups available: $backupCount" -ForegroundColor Gray

# Plugins
Write-Host "  Plugins:" -ForegroundColor Cyan
Write-Host "    - Plugins directory: $pluginsDir" -ForegroundColor Gray
Write-Host "    - Loaded plugins: $([SuperTUI.Extensions.PluginManager]::Instance.GetAllPlugins().Count)" -ForegroundColor Gray

# ============================================================================
# DEMONSTRATE FEATURES
# ============================================================================

Write-Host "`n[4/4] Demonstrating features..." -ForegroundColor Yellow

# Test configuration
Write-Host "  Testing configuration changes..." -ForegroundColor Cyan
[SuperTUI.Infrastructure.ConfigurationManager]::Instance.Set("UI.FontSize", 14, $true)
Write-Host "    ✓ Config changed and saved" -ForegroundColor Green

# Test logging levels
Write-Host "  Testing logging levels..." -ForegroundColor Cyan
[SuperTUI.Infrastructure.Logger]::Instance.Trace("Demo", "This is a trace message")
[SuperTUI.Infrastructure.Logger]::Instance.Debug("Demo", "This is a debug message")
[SuperTUI.Infrastructure.Logger]::Instance.Info("Demo", "This is an info message")
[SuperTUI.Infrastructure.Logger]::Instance.Warning("Demo", "This is a warning message")
[SuperTUI.Infrastructure.Logger]::Instance.Error("Demo", "This is an error message")
Write-Host "    ✓ Logged messages at all levels" -ForegroundColor Green

# Test validation
Write-Host "  Testing input validation..." -ForegroundColor Cyan
$validEmail = [SuperTUI.Infrastructure.ValidationHelper]::IsValidEmail("test@example.com")
$invalidEmail = [SuperTUI.Infrastructure.ValidationHelper]::IsValidEmail("not-an-email")
$safeFilename = [SuperTUI.Infrastructure.ValidationHelper]::SanitizeFilename("file<>name|test?.txt")
Write-Host "    ✓ Email validation: valid=$validEmail, invalid=$invalidEmail" -ForegroundColor Green
Write-Host "    ✓ Sanitized filename: '$safeFilename'" -ForegroundColor Green

# Test performance monitoring
Write-Host "  Testing performance monitoring..." -ForegroundColor Cyan
[SuperTUI.Extensions.PerformanceMonitor]::Instance.StartOperation("TestOperation")
Start-Sleep -Milliseconds 50
[SuperTUI.Extensions.PerformanceMonitor]::Instance.StopOperation("TestOperation")
$counter = [SuperTUI.Extensions.PerformanceMonitor]::Instance.GetCounter("TestOperation")
Write-Host "    ✓ Operation took $($counter.LastDuration.TotalMilliseconds) ms" -ForegroundColor Green

# Test theme switching
Write-Host "  Testing theme switching..." -ForegroundColor Cyan
$originalTheme = [SuperTUI.Infrastructure.ThemeManager]::Instance.CurrentTheme.Name
[SuperTUI.Infrastructure.ThemeManager]::Instance.ApplyTheme("Light")
Write-Host "    ✓ Switched to Light theme" -ForegroundColor Green
[SuperTUI.Infrastructure.ThemeManager]::Instance.ApplyTheme($originalTheme)
Write-Host "    ✓ Restored to $originalTheme theme" -ForegroundColor Green

# Test error handling
Write-Host "  Testing error handling..." -ForegroundColor Cyan
try {
    [SuperTUI.Infrastructure.ErrorHandler]::Instance.ExecuteWithRetry({
        throw "Simulated error"
    }, 2, 10, "TestOperation")
} catch {
    Write-Host "    ✓ Error caught and logged (expected)" -ForegroundColor Green
}

# ============================================================================
# GENERATE REPORTS
# ============================================================================

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Infrastructure Demo Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "AVAILABLE FEATURES:" -ForegroundColor Yellow
Write-Host "  ✓ Comprehensive logging (file + memory)" -ForegroundColor Gray
Write-Host "  ✓ Configuration management with validation" -ForegroundColor Gray
Write-Host "  ✓ Theme system with Dark/Light themes" -ForegroundColor Gray
Write-Host "  ✓ Input validation and sanitization" -ForegroundColor Gray
Write-Host "  ✓ Security manager with file access control" -ForegroundColor Gray
Write-Host "  ✓ State persistence with backups" -ForegroundColor Gray
Write-Host "  ✓ Plugin/extension system" -ForegroundColor Gray
Write-Host "  ✓ Performance monitoring" -ForegroundColor Gray
Write-Host "  ✓ Error handling with retry logic" -ForegroundColor Gray
Write-Host "  ✓ Undo/redo support" -ForegroundColor Gray

Write-Host "`nLOG FILES:" -ForegroundColor Yellow
Write-Host "  Location: $logDir" -ForegroundColor Gray
if (Test-Path $logDir) {
    $logFiles = Get-ChildItem $logDir -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 3
    foreach ($file in $logFiles) {
        Write-Host "    - $($file.Name) ($([math]::Round($file.Length / 1KB, 2)) KB)" -ForegroundColor Gray
    }
}

Write-Host "`nCONFIGURATION CATEGORIES:" -ForegroundColor Yellow
$categories = [SuperTUI.Infrastructure.ConfigurationManager]::Instance.GetCategories()
foreach ($cat in $categories) {
    $settings = [SuperTUI.Infrastructure.ConfigurationManager]::Instance.GetCategory($cat)
    Write-Host "  $cat`: $($settings.Count) settings" -ForegroundColor Gray
}

Write-Host "`nPERFORMANCE METRICS:" -ForegroundColor Yellow
$counters = [SuperTUI.Extensions.PerformanceMonitor]::Instance.GetAllCounters()
foreach ($kvp in $counters.GetEnumerator()) {
    Write-Host "  $($kvp.Key): Avg $($kvp.Value.AverageDuration.TotalMilliseconds) ms" -ForegroundColor Gray
}

Write-Host "`nRECENT LOG ENTRIES:" -ForegroundColor Yellow
$recentLogs = $memoryLogSink.GetEntries([SuperTUI.Infrastructure.LogLevel]::Info, $null, 10)
foreach ($log in $recentLogs) {
    $color = switch ($log.Level) {
        ([SuperTUI.Infrastructure.LogLevel]::Error) { "Red" }
        ([SuperTUI.Infrastructure.LogLevel]::Warning) { "Yellow" }
        ([SuperTUI.Infrastructure.LogLevel]::Info) { "Cyan" }
        default { "Gray" }
    }
    Write-Host "  [$($log.Level)] $($log.Message)" -ForegroundColor $color
}

Write-Host "`nPress any key to exit..." -ForegroundColor Gray
[SuperTUI.Infrastructure.Logger]::Instance.Info("Demo", "SuperTUI Enhanced Demo completed")
[SuperTUI.Infrastructure.Logger]::Instance.Flush()

$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
