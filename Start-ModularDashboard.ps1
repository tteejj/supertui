#!/usr/bin/env pwsh
# Start Modular Dashboard - Full widget-based dashboard

param(
    [string]$Config = "default",
    [switch]$SkipBanner
)

if (-not $SkipBanner) {
    Clear-Host
    Write-Host ""
    Write-Host "  ╔═══════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║                                                                   ║" -ForegroundColor Cyan
    Write-Host "  ║   SUPERTUI  -  Modular Widget Dashboard                          ║" -ForegroundColor Cyan
    Write-Host "  ║                                                                   ║" -ForegroundColor Cyan
    Write-Host "  ║   Phase 1 & 2: COMPLETE ✓                                        ║" -ForegroundColor Green
    Write-Host "  ║   Phase 3: Modular Widgets - COMPLETE ✓                          ║" -ForegroundColor Green
    Write-Host "  ║   Option B: Plugin System - READY                                ║" -ForegroundColor Yellow
    Write-Host "  ║                                                                   ║" -ForegroundColor Cyan
    Write-Host "  ╚═══════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Loading modular dashboard system..." -ForegroundColor Gray
}

# Load SuperTUI module
$modulePath = Join-Path $PSScriptRoot "Module" "SuperTUI.psm1"
Import-Module $modulePath -Force -WarningAction SilentlyContinue

if (-not $SkipBanner) {
    Write-Host "  ✓ SuperTUI module loaded" -ForegroundColor Green
}

# Load TaskService
. (Join-Path $PSScriptRoot "Services" "TaskService.ps1")
$taskService = New-TaskService

if (-not $SkipBanner) {
    Write-Host "  ✓ TaskService loaded with $($taskService.Tasks.Count) sample tasks" -ForegroundColor Green
}

# Load widget system
. (Join-Path $PSScriptRoot "Widgets" "BaseWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "WidgetRegistry.ps1")

# Load all core widgets manually (required for PowerShell class scope)
. (Join-Path $PSScriptRoot "Widgets" "Core" "TaskStatsWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "Core" "WeekViewWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "Core" "MenuWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "Core" "TodayTasksWidget.ps1")
. (Join-Path $PSScriptRoot "Widgets" "Core" "RecentActivityWidget.ps1")

if (-not $SkipBanner) {
    Write-Host "  ✓ 5 core widgets loaded" -ForegroundColor Green
}

# Load configuration system
. (Join-Path $PSScriptRoot "Config" "DashboardConfig.ps1")

if (-not $SkipBanner) {
    Write-Host "  ✓ Configuration system loaded" -ForegroundColor Green
}

# Load Dashboard Manager (function-based)
. (Join-Path $PSScriptRoot "Screens" "Dashboard" "DashboardManager-Functions.ps1")

if (-not $SkipBanner) {
    Write-Host "  ✓ Dashboard Manager loaded" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Dashboard Configuration:" -ForegroundColor Cyan
    Write-Host "    Layout: $Config" -ForegroundColor Gray
    Write-Host "    Widgets: 5 (Stats, Week, Menu, Tasks, Activity)" -ForegroundColor Gray
    Write-Host "    Focus: Tab/Shift+Tab to cycle" -ForegroundColor Gray
    Write-Host "    Jump: Number keys (1-5)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Navigation:" -ForegroundColor Cyan
    Write-Host "    Tab/Shift+Tab  - Cycle focus between widgets" -ForegroundColor Gray
    Write-Host "    1-5            - Jump to specific widget" -ForegroundColor Gray
    Write-Host "    ↑↓             - Navigate within focused widget" -ForegroundColor Gray
    Write-Host "    Enter          - Activate/drill into widget" -ForegroundColor Gray
    Write-Host "    F5             - Refresh all widgets" -ForegroundColor Gray
    Write-Host "    Q/Esc          - Exit dashboard" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Widgets:" -ForegroundColor Cyan
    Write-Host "    [1] Task Stats      - Shows counts and statistics" -ForegroundColor Gray
    Write-Host "    [2] Week View       - 7-day calendar with task bars" -ForegroundColor Gray
    Write-Host "    [3] Menu            - Scrollable navigation (11 items)" -ForegroundColor Gray
    Write-Host "    [4] Today's Tasks   - All tasks due today (scrollable)" -ForegroundColor Gray
    Write-Host "    [5] Recent Activity - Activity feed (scrollable)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Press any key to launch dashboard..." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

Clear-Host

# Start dashboard
try {
    Start-Dashboard -ConfigName $Config -TaskService $taskService
} catch {
    Write-Host "`nError running dashboard: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}

# Cleanup
if (-not $SkipBanner) {
    Clear-Host
    Write-Host ""
    Write-Host "  Thank you for using SuperTUI Modular Dashboard!" -ForegroundColor Cyan
    Write-Host ""
}
