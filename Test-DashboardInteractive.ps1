#!/usr/bin/env pwsh
# Test interactive dashboard with mock input

Write-Host "Starting interactive dashboard test..." -ForegroundColor Yellow

# Load everything
Import-Module ./Module/SuperTUI.psm1 -WarningAction SilentlyContinue
. ./Services/TaskService.ps1
. ./Widgets/BaseWidget.ps1
. ./Widgets/WidgetRegistry.ps1
. ./Widgets/Core/TaskStatsWidget.ps1
. ./Widgets/Core/WeekViewWidget.ps1
. ./Widgets/Core/MenuWidget.ps1
. ./Widgets/Core/TodayTasksWidget.ps1
. ./Widgets/Core/RecentActivityWidget.ps1
. ./Config/DashboardConfig.ps1
. ./Screens/Dashboard/DashboardManager-Functions.ps1

$taskSvc = New-TaskService
$dashboard = New-DashboardManager -ConfigName "default" -TaskService $taskSvc

Write-Host "`nDashboard created with $($dashboard.Widgets.Count) widgets" -ForegroundColor Green

# Render once
$theme = Get-Theme
$context = [SuperTUI.RenderContext]::new($theme, $dashboard.TerminalWidth, $dashboard.TerminalHeight)
$output = $dashboard.Layout.Render($context)

# Clear screen and show dashboard
Clear-Host
Write-Host $output

Write-Host "`n`n"
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Dashboard rendered successfully!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "`nNavigation:" -ForegroundColor Yellow
Write-Host "  Tab/Shift+Tab  - Cycle focus" -ForegroundColor Gray
Write-Host "  1-5            - Jump to widget" -ForegroundColor Gray
Write-Host "  ↑↓             - Navigate within widget" -ForegroundColor Gray
Write-Host "  F5             - Refresh" -ForegroundColor Gray
Write-Host "  Q/Esc          - Exit" -ForegroundColor Gray

Write-Host "`nTo run full interactive mode:" -ForegroundColor Yellow
Write-Host "  pwsh ./Start-ModularDashboard.ps1" -ForegroundColor White
