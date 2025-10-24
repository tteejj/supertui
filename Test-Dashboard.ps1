#!/usr/bin/env pwsh
# Test complete dashboard system with logging

Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SuperTUI - Complete Dashboard Test" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════`n" -ForegroundColor Cyan

# Test with detailed error tracking
try {
    Write-Host "[1/7] Loading logger..." -ForegroundColor Yellow
    . (Join-Path $PSScriptRoot "Core" "Logger.ps1")
    [Logger]::Initialize("DEBUG", $true, "")
    [Logger]::Info("Test", "Logger initialized")
    Write-Host "  ✓ Logger ready" -ForegroundColor Green

    Write-Host "[2/7] Loading SuperTUI module..." -ForegroundColor Yellow
    Import-Module (Join-Path $PSScriptRoot "Module" "SuperTUI.psm1") -Force -WarningAction SilentlyContinue
    [Logger]::Info("Test", "SuperTUI module loaded")
    Write-Host "  ✓ SuperTUI module loaded" -ForegroundColor Green

    Write-Host "[3/7] Loading TaskService..." -ForegroundColor Yellow
    . (Join-Path $PSScriptRoot "Services" "TaskService.ps1")
    $taskSvc = New-TaskService
    [Logger]::Info("Test", "TaskService created with $($taskSvc.Tasks.Count) tasks")
    Write-Host "  ✓ TaskService loaded ($($taskSvc.Tasks.Count) tasks)" -ForegroundColor Green

    Write-Host "[4/7] Loading widget system...  " -ForegroundColor Yellow
    . (Join-Path $PSScriptRoot "Widgets" "BaseWidget.ps1")
    . (Join-Path $PSScriptRoot "Widgets" "WidgetRegistry.ps1")

    # Load all core widgets manually (required for PowerShell class scope)
    . (Join-Path $PSScriptRoot "Widgets" "Core" "TaskStatsWidget.ps1")
    . (Join-Path $PSScriptRoot "Widgets" "Core" "WeekViewWidget.ps1")
    . (Join-Path $PSScriptRoot "Widgets" "Core" "MenuWidget.ps1")
    . (Join-Path $PSScriptRoot "Widgets" "Core" "TodayTasksWidget.ps1")
    . (Join-Path $PSScriptRoot "Widgets" "Core" "RecentActivityWidget.ps1")

    [Logger]::Info("Test", "Widget system loaded")
    Write-Host "  ✓ Widget base classes and 5 core widgets loaded" -ForegroundColor Green

    Write-Host "[5/7] Loading configuration system..." -ForegroundColor Yellow
    . (Join-Path $PSScriptRoot "Config" "DashboardConfig.ps1")
    [DashboardConfig]::CreateDefaultConfig()
    [Logger]::Info("Test", "Config system ready")
    Write-Host "  ✓ Configuration system ready" -ForegroundColor Green

    Write-Host "[6/7] Loading Dashboard Manager..." -ForegroundColor Yellow
    . (Join-Path $PSScriptRoot "Screens" "Dashboard" "DashboardManager.ps1")
    [Logger]::Info("Test", "Dashboard Manager loaded")
    Write-Host "  ✓ Dashboard Manager loaded" -ForegroundColor Green

    Write-Host "[7/7] Testing dashboard creation..." -ForegroundColor Yellow
    $dashboard = [DashboardManager]::new("default", $taskSvc)
    [Logger]::Info("Test", "Dashboard created with $($dashboard.Widgets.Count) widgets")
    Write-Host "  ✓ Dashboard created with $($dashboard.Widgets.Count) widgets" -ForegroundColor Green

    Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  ALL TESTS PASSED ✓" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════`n" -ForegroundColor Green

    Write-Host "Dashboard Details:" -ForegroundColor Cyan
    Write-Host "  Layout: $($dashboard.Layout.Rows.Count) rows x $($dashboard.Layout.Columns.Count) columns" -ForegroundColor Gray
    Write-Host "  Widgets:" -ForegroundColor Gray
    foreach ($widget in $dashboard.Widgets) {
        Write-Host "    [$($widget.Number)] $($widget.Title) ($($widget.GetType().Name))" -ForegroundColor Gray
    }

    Write-Host "`nLogger Statistics:" -ForegroundColor Cyan
    $logs = Get-Logs -Count 20
    Write-Host "  Total log entries: $($logs.Count)" -ForegroundColor Gray
    Write-Host "  Recent logs:" -ForegroundColor Gray
    foreach ($log in ($logs | Select-Object -Last 5)) {
        Write-Host "    $($log.ToString())" -ForegroundColor DarkGray
    }

    Write-Host "`nReady to run dashboard!" -ForegroundColor Green
    Write-Host "  ./Start-ModularDashboard.ps1" -ForegroundColor Yellow

} catch {
    Write-Host "`n✗ TEST FAILED" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nStack Trace:" -ForegroundColor Yellow
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray

    if ([Logger]::Initialized) {
        [Logger]::Exception("Test", "Test failed", $_.Exception)
        Write-Host "`nRecent Logs:" -ForegroundColor Yellow
        Get-Logs -Count 10 | ForEach-Object {
            Write-Host $_.ToString() -ForegroundColor Gray
        }
    }

    exit 1
}
