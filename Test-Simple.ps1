#!/usr/bin/env pwsh
# Minimal test to isolate the issue

Write-Host "Loading..." -ForegroundColor Yellow

# Load dependencies in exact same order as Test-AllWidgets
. ./Widgets/BaseWidget.ps1
. ./Widgets/WidgetRegistry.ps1
. ./Widgets/Core/TaskStatsWidget.ps1
. ./Widgets/Core/WeekViewWidget.ps1
. ./Widgets/Core/MenuWidget.ps1
. ./Widgets/Core/TodayTasksWidget.ps1
. ./Widgets/Core/RecentActivityWidget.ps1

Write-Host "  ✓ Widgets loaded" -ForegroundColor Green

# Discover/register
[WidgetRegistry]::DiscoverWidgets()
Write-Host "  ✓ Registered $([WidgetRegistry]::RegisteredTypes.Count) widgets" -ForegroundColor Green

# Test direct creation
Write-Host "Creating widget directly..." -ForegroundColor Yellow
$testWidget = [WidgetRegistry]::CreateWidget("TaskStatsWidget", @{id="test"; number=1})
Write-Host "  ✓ Direct creation works: $($testWidget.Title)" -ForegroundColor Green

# Now load DashboardConfig
. ./Config/DashboardConfig.ps1
Write-Host "  ✓ Config loaded" -ForegroundColor Green

# Test creation after loading config
Write-Host "Creating widget after config load..." -ForegroundColor Yellow
try {
    $testWidget2 = [WidgetRegistry]::CreateWidget("MenuWidget", @{id="menu"; number=2})
    Write-Host "  ✓ After config load works: $($testWidget2.Title)" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed after config: $_" -ForegroundColor Red
}
