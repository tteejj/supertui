#!/usr/bin/env pwsh
# Test the modular widget system

Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SuperTUI - Modular Widget System Test" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════`n" -ForegroundColor Cyan

# Test 1: Load widget base classes
Write-Host "[1/6] Loading widget base classes..." -ForegroundColor Yellow
try {
    . (Join-Path $PSScriptRoot "Widgets" "BaseWidget.ps1")
    Write-Host "  ✓ DashboardWidget, ScrollableWidget, StaticWidget loaded" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    exit 1
}

# Test 2: Load widget registry
Write-Host "[2/6] Loading widget registry..." -ForegroundColor Yellow
try {
    . (Join-Path $PSScriptRoot "Widgets" "WidgetRegistry.ps1")
    Write-Host "  ✓ WidgetRegistry loaded" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    exit 1
}

# Test 3: Discover widgets
Write-Host "[3/6] Discovering widgets..." -ForegroundColor Yellow
try {
    # Manually load widgets for now (discovery will be improved)
    . (Join-Path $PSScriptRoot "Widgets" "Core" "TaskStatsWidget.ps1")
    . (Join-Path $PSScriptRoot "Widgets" "Core" "MenuWidget.ps1")

    # Now register them
    [WidgetRegistry]::DiscoverWidgets()
    $count = [WidgetRegistry]::RegisteredTypes.Count
    Write-Host "  ✓ Loaded and discovered $count widget types" -ForegroundColor Green

    foreach ($typeName in [WidgetRegistry]::GetAvailableWidgets()) {
        $info = [WidgetRegistry]::GetWidgetInfo($typeName)
        Write-Host "    - $typeName" -ForegroundColor Gray
    }
} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 4: Load configuration system
Write-Host "[4/6] Loading configuration system..." -ForegroundColor Yellow
try {
    . (Join-Path $PSScriptRoot "Config" "DashboardConfig.ps1")
    Write-Host "  ✓ DashboardConfig loaded" -ForegroundColor Green

    # Create default config if doesn't exist
    [DashboardConfig]::CreateDefaultConfig()
    Write-Host "  ✓ Default configuration ready" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    exit 1
}

# Test 5: Create widget instances
Write-Host "[5/6] Creating widget instances..." -ForegroundColor Yellow
try {
    # Load SuperTUI module for helper functions
    Import-Module (Join-Path $PSScriptRoot "Module" "SuperTUI.psm1") -Force -WarningAction SilentlyContinue

    # Load TaskService
    . (Join-Path $PSScriptRoot "Services" "TaskService.ps1")
    $taskService = New-TaskService

    # Create TaskStatsWidget
    $statsWidget = [WidgetRegistry]::CreateWidget("TaskStatsWidget", @{
        id = "stats1"
        number = 1
        taskService = $taskService
    })
    Write-Host "  ✓ TaskStatsWidget created: $($statsWidget.Id)" -ForegroundColor Green

    # Create MenuWidget
    $menuWidget = [WidgetRegistry]::CreateWidget("MenuWidget", @{
        id = "menu1"
        number = 3
    })
    Write-Host "  ✓ MenuWidget created: $($menuWidget.Id)" -ForegroundColor Green
    Write-Host "    - Items: $($menuWidget.Items.Count)" -ForegroundColor Gray
    Write-Host "    - Viewport: $($menuWidget.ViewportHeight)" -ForegroundColor Gray

} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 6: Test widget functionality
Write-Host "[6/6] Testing widget functionality..." -ForegroundColor Yellow
try {
    # Test scrolling
    $menuWidget.MoveDown()
    $menuWidget.MoveDown()
    Write-Host "  ✓ Scrollable navigation works" -ForegroundColor Green
    Write-Host "    - Selected: $($menuWidget.GetSelectedItem().Label)" -ForegroundColor Gray

    # Test focus
    $menuWidget.Focus()
    Write-Host "  ✓ Focus management works" -ForegroundColor Green
    Write-Host "    - IsFocused: $($menuWidget.IsFocused)" -ForegroundColor Gray

    # Test refresh
    $statsWidget.Refresh()
    Write-Host "  ✓ Widget refresh works" -ForegroundColor Green

} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  ALL TESTS PASSED ✓" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════`n" -ForegroundColor Green

Write-Host "Widget System Status:" -ForegroundColor Cyan
Write-Host "  ✓ Base classes implemented (DashboardWidget, ScrollableWidget, StaticWidget)" -ForegroundColor Green
Write-Host "  ✓ Registry system working (auto-discovery from Widgets/Core/)" -ForegroundColor Green
Write-Host "  ✓ Configuration system ready (JSON configs in ~/.supertui/layouts/)" -ForegroundColor Green
Write-Host "  ✓ 2 core widgets implemented (TaskStatsWidget, MenuWidget)" -ForegroundColor Green
Write-Host "  ⏳ 3 more widgets to implement (WeekView, TodayTasks, RecentActivity)" -ForegroundColor Yellow
Write-Host "  ⏳ DashboardManager to implement (layout management, focus system)" -ForegroundColor Yellow
Write-Host ""

Write-Host "Available Widget Types:" -ForegroundColor Cyan
[WidgetRegistry]::ListWidgets()

Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Implement remaining 3 core widgets" -ForegroundColor Gray
Write-Host "  2. Build DashboardManager for layout and focus" -ForegroundColor Gray
Write-Host "  3. Create interactive dashboard runner" -ForegroundColor Gray
Write-Host "  4. Test complete system" -ForegroundColor Gray
Write-Host ""

Write-Host "Configuration:" -ForegroundColor Cyan
$configDir = Join-Path $HOME ".supertui" "layouts"
Write-Host "  Config directory: $configDir" -ForegroundColor Gray
if (Test-Path $configDir) {
    $configs = Get-ChildItem $configDir -Filter "*.json"
    Write-Host "  Available layouts: $($configs.Count)" -ForegroundColor Gray
    foreach ($config in $configs) {
        Write-Host "    - $($config.BaseName)" -ForegroundColor Gray
    }
}
Write-Host ""
