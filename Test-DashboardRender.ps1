#!/usr/bin/env pwsh
# Test dashboard rendering without event loop

Write-Host "Testing dashboard render..." -ForegroundColor Yellow

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
Write-Host "Creating dashboard..." -ForegroundColor Yellow
$dashboard = New-DashboardManager -ConfigName "default" -TaskService $taskSvc

Write-Host "Dashboard created with $($dashboard.Widgets.Count) widgets" -ForegroundColor Green
foreach ($w in $dashboard.Widgets) {
    Write-Host "  - $($w.Title)" -ForegroundColor Gray
}

Write-Host "`nRendering dashboard once..." -ForegroundColor Yellow

# Get theme
$theme = Get-Theme

# Create render context
$context = [SuperTUI.RenderContext]::new($theme, $dashboard.TerminalWidth, $dashboard.TerminalHeight)

# Render layout
$output = $dashboard.Layout.Render($context)

Write-Host "`nRendered output length: $($output.Length) characters" -ForegroundColor Cyan

# Show the output
Write-Host "`nDashboard Preview:" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host $output
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

Write-Host "`nPress Enter to exit..." -ForegroundColor Yellow
Read-Host
