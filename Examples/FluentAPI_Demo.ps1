# SuperTUI Fluent API Demo
# Shows how to create workspaces using the PowerShell module

# Import the module
$modulePath = Join-Path $PSScriptRoot "..\Module\SuperTUI"
Import-Module $modulePath -Force

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "  SuperTUI - Fluent API Demo" -ForegroundColor Cyan
Write-Host "============================================`n" -ForegroundColor Cyan

# Initialize SuperTUI
Write-Host "[1/4] Initializing SuperTUI..." -ForegroundColor Yellow
Initialize-SuperTUI -Verbose

# Create Workspace 1: Dashboard with Grid Layout
Write-Host "[2/4] Creating Dashboard workspace..." -ForegroundColor Yellow

$workspace1 = New-SuperTUIWorkspace "Dashboard" -Index 1 |
    Use-GridLayout -Rows 2 -Columns 3 -Splitters |
    Add-ClockWidget -Row 0 -Column 0 |
    Add-SystemMonitorWidget -Row 0 -Column 1 |
    Add-TaskSummaryWidget -Row 0 -Column 2 |
    Add-NotesWidget -Row 1 -Column 0 -ColumnSpan 2 |
    Add-CounterWidget -Name "Actions" -Row 1 -Column 2

$builtWorkspace1 = $workspace1.Build()
Write-Host "  ✓ Dashboard: 2x3 grid with 5 widgets (with System Monitor)" -ForegroundColor Green

# Create Workspace 2: Focus Test with Grid Layout
Write-Host "[3/4] Creating Focus Test workspace..." -ForegroundColor Yellow

$workspace2 = New-SuperTUIWorkspace "Focus Test" -Index 2 |
    Use-GridLayout -Rows 2 -Columns 2 -Splitters |
    Add-CounterWidget -Name "Counter A" -Row 0 -Column 0 |
    Add-CounterWidget -Name "Counter B" -Row 0 -Column 1 |
    Add-CounterWidget -Name "Counter C" -Row 1 -Column 0 |
    Add-CounterWidget -Name "Counter D" -Row 1 -Column 1

$builtWorkspace2 = $workspace2.Build()
Write-Host "  ✓ Focus Test: 2x2 grid with 4 counters" -ForegroundColor Green

# Create Workspace 3: Dock Layout Demo
Write-Host "[4/4] Creating Dock Layout workspace..." -ForegroundColor Yellow

$workspace3 = New-SuperTUIWorkspace "Dock Demo" -Index 3 |
    Use-DockLayout |
    Add-ClockWidget -Dock Top -Height 150 |
    Add-TaskSummaryWidget -Dock Left -Width 300 |
    Add-NotesWidget

$builtWorkspace3 = $workspace3.Build()
Write-Host "  ✓ Dock Demo: Top + Left + Fill layout" -ForegroundColor Green

Write-Host "`n" -NoNewline
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Workspaces Created Successfully!" -ForegroundColor Green
Write-Host "============================================`n" -ForegroundColor Green

Write-Host "Created Workspaces:" -ForegroundColor Cyan
Write-Host "  1. Dashboard    - 5 widgets in 2x3 grid (Clock, System Monitor, Tasks, Notes, Counter)" -ForegroundColor White
Write-Host "  2. Focus Test   - 4 counters in 2x2 grid" -ForegroundColor White
Write-Host "  3. Dock Demo    - 3 widgets with dock layout" -ForegroundColor White

Write-Host "`nFluen API Example:" -ForegroundColor Yellow
Write-Host @"
  `$workspace = New-SuperTUIWorkspace "MyWorkspace" -Index 1 |
      Use-GridLayout -Rows 2 -Columns 2 |
      Add-ClockWidget -Row 0 -Column 0 |
      Add-CounterWidget -Row 0 -Column 1 |
      Add-NotesWidget -Row 1 -Column 0 -ColumnSpan 2

  `$workspace.Build()
"@ -ForegroundColor Gray

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "  • Create a window and workspace manager" -ForegroundColor White
Write-Host "  • Add keyboard shortcuts" -ForegroundColor White
Write-Host "  • Call .ShowDialog() to display" -ForegroundColor White

Write-Host "`nModule Functions Available:" -ForegroundColor Cyan
Write-Host "  Core:" -ForegroundColor Yellow
Write-Host "    Initialize-SuperTUI" -ForegroundColor Gray
Write-Host "  Workspaces:" -ForegroundColor Yellow
Write-Host "    New-SuperTUIWorkspace (alias: New-Workspace)" -ForegroundColor Gray
Write-Host "  Layouts:" -ForegroundColor Yellow
Write-Host "    Use-GridLayout, Use-DockLayout, Use-StackLayout" -ForegroundColor Gray
Write-Host "  Widgets:" -ForegroundColor Yellow
Write-Host "    Add-ClockWidget, Add-CounterWidget, Add-NotesWidget, Add-TaskSummaryWidget, Add-SystemMonitorWidget" -ForegroundColor Gray
Write-Host "  Configuration:" -ForegroundColor Yellow
Write-Host "    Get-SuperTUIConfig, Set-SuperTUIConfig" -ForegroundColor Gray
Write-Host "  Theme:" -ForegroundColor Yellow
Write-Host "    Get-SuperTUITheme, Set-SuperTUITheme" -ForegroundColor Gray
Write-Host "  Utility:" -ForegroundColor Yellow
Write-Host "    Get-SuperTUIStatistics" -ForegroundColor Gray

Write-Host ""

# Show statistics
Write-Host "EventBus Statistics:" -ForegroundColor Cyan
$stats = Get-SuperTUIStatistics
Write-Host "  Events Published:    $($stats.EventsPublished)" -ForegroundColor White
Write-Host "  Events Delivered:    $($stats.EventsDelivered)" -ForegroundColor White
Write-Host "  Typed Subscribers:   $($stats.TypedSubscribers)" -ForegroundColor White
Write-Host "  Named Subscribers:   $($stats.NamedSubscribers)" -ForegroundColor White

Write-Host ""
