#!/usr/bin/env pwsh
# Start-SuperTUI.ps1 - Interactive launcher for SuperTUI Dashboard

param(
    [switch]$SkipBanner
)

# Import SuperTUI module
$modulePath = Join-Path $PSScriptRoot "Module" "SuperTUI.psm1"
Import-Module $modulePath -Force

if (-not $SkipBanner) {
    Clear-Host
    Write-Host ""
    Write-Host "  ╔═══════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║                                                                   ║" -ForegroundColor Cyan
    Write-Host "  ║   SUPERTUI  -  Terminal User Interface Framework                 ║" -ForegroundColor Cyan
    Write-Host "  ║                                                                   ║" -ForegroundColor Cyan
    Write-Host "  ║   Phase 1 & 2: COMPLETE                                          ║" -ForegroundColor Green
    Write-Host "  ║   Phase 3: Interactive Dashboard - READY                         ║" -ForegroundColor Yellow
    Write-Host "  ║                                                                   ║" -ForegroundColor Cyan
    Write-Host "  ╚═══════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Loading services..." -ForegroundColor Gray
}

# Load TaskService
. (Join-Path $PSScriptRoot "Services" "TaskService.ps1")
$taskService = New-TaskService

if (-not $SkipBanner) {
    Write-Host "  ✓ TaskService loaded with $($taskService.Tasks.Count) sample tasks" -ForegroundColor Green
}

# Register in service container
Register-Service "TaskService" -Instance $taskService

if (-not $SkipBanner) {
    Write-Host "  ✓ Services registered" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Statistics:" -ForegroundColor Cyan
    Write-Host "    Total Tasks:     $($taskService.Statistics.TotalTasks)" -ForegroundColor Gray
    Write-Host "    Completed:       $($taskService.Statistics.CompletedTasks)" -ForegroundColor Green
    Write-Host "    In Progress:     $($taskService.Statistics.InProgressTasks)" -ForegroundColor Yellow
    Write-Host "    Pending:         $($taskService.Statistics.PendingTasks)" -ForegroundColor Gray
    Write-Host "    Due Today:       $($taskService.Statistics.TodayTasks)" -ForegroundColor Yellow
    Write-Host "    Due This Week:   $($taskService.Statistics.WeekTasks)" -ForegroundColor Gray
    Write-Host "    Overdue:         $($taskService.Statistics.OverdueTasks)" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Navigation:" -ForegroundColor Cyan
    Write-Host "    Arrow Keys (↑↓)  - Navigate menu" -ForegroundColor Gray
    Write-Host "    Enter            - Select item" -ForegroundColor Gray
    Write-Host "    Letter Keys      - Quick shortcuts (T, P, W, K, M, C, F, R, S, H, Q)" -ForegroundColor Gray
    Write-Host "    Number Keys      - Direct access (1-9, 0)" -ForegroundColor Gray
    Write-Host "    F5               - Refresh statistics" -ForegroundColor Gray
    Write-Host "    Q                - Quit" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Press any key to launch dashboard..." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

# Load and show interactive dashboard
. (Join-Path $PSScriptRoot "Screens" "DashboardScreen_Interactive.ps1")

Clear-Host
Show-DashboardScreen -TaskService $taskService

# Cleanup
if (-not $SkipBanner) {
    Clear-Host
    Write-Host ""
    Write-Host "  Thank you for using SuperTUI!" -ForegroundColor Cyan
    Write-Host ""
}
