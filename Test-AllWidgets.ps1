#!/usr/bin/env pwsh
# Test all 5 widgets

Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SuperTUI - All Widgets Test" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════`n" -ForegroundColor Cyan

# Load dependencies
Write-Host "[1/4] Loading base classes..." -ForegroundColor Yellow
. (Join-Path $PSScriptRoot "Widgets" "BaseWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "WidgetRegistry.ps1")
Write-Host "  ✓ Base classes loaded" -ForegroundColor Green

# Load all widgets
Write-Host "[2/4] Loading all 5 core widgets..." -ForegroundColor Yellow
. (Join-Path $PSScriptRoot "Widgets" "Core" "TaskStatsWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "Core" "WeekViewWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "Core" "MenuWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "Core" "TodayTasksWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "Core" "RecentActivityWidget.ps1")
Write-Host "  ✓ All widgets loaded" -ForegroundColor Green

# Discover
Write-Host "[3/4] Registering widgets..." -ForegroundColor Yellow
[WidgetRegistry]::DiscoverWidgets()
$count = [WidgetRegistry]::RegisteredTypes.Count
Write-Host "  ✓ Registered $count widget types" -ForegroundColor Green

# Create instances
Write-Host "[4/4] Creating widget instances..." -ForegroundColor Yellow

# Load dependencies
Import-Module (Join-Path $PSScriptRoot "Module" "SuperTUI.psm1") -Force -WarningAction SilentlyContinue
. (Join-Path $PSScriptRoot "Services" "TaskService.ps1")
$taskSvc = New-TaskService

try {
    $widgets = @()

    $widgets += [WidgetRegistry]::CreateWidget("TaskStatsWidget", @{
        id = "stats"; number = 1; taskService = $taskSvc
    })
    Write-Host "  ✓ TaskStatsWidget created" -ForegroundColor Green

    $widgets += [WidgetRegistry]::CreateWidget("WeekViewWidget", @{
        id = "week"; number = 2; taskService = $taskSvc
    })
    Write-Host "  ✓ WeekViewWidget created" -ForegroundColor Green

    $widgets += [WidgetRegistry]::CreateWidget("MenuWidget", @{
        id = "menu"; number = 3
    })
    Write-Host "  ✓ MenuWidget created" -ForegroundColor Green

    $widgets += [WidgetRegistry]::CreateWidget("TodayTasksWidget", @{
        id = "tasks"; number = 4; taskService = $taskSvc
    })
    Write-Host "  ✓ TodayTasksWidget created" -ForegroundColor Green

    $widgets += [WidgetRegistry]::CreateWidget("RecentActivityWidget", @{
        id = "activity"; number = 5; taskService = $taskSvc
    })
    Write-Host "  ✓ RecentActivityWidget created" -ForegroundColor Green

    Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  ALL WIDGETS CREATED SUCCESSFULLY ✓" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════`n" -ForegroundColor Green

    Write-Host "Widget Details:" -ForegroundColor Cyan
    foreach ($widget in $widgets) {
        Write-Host "  [$($widget.Number)] $($widget.Title)" -ForegroundColor Yellow
        Write-Host "      ID: $($widget.Id)" -ForegroundColor Gray
        Write-Host "      Type: $($widget.GetType().Name)" -ForegroundColor Gray
        if ($widget.CanSelect) {
            Write-Host "      Items: $($widget.Items.Count)" -ForegroundColor Gray
        }
    }

    Write-Host "`nReady for dashboard!" -ForegroundColor Green

} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}
