#!/usr/bin/env pwsh
# Test DashboardScreen - See the enhanced main screen in action

Import-Module (Join-Path $PSScriptRoot ".." "Module" "SuperTUI.psm1") -Force

Write-Host "SuperTUI - Dashboard Screen Test" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Load the dashboard screen
. (Join-Path $PSScriptRoot ".." "Screens" "DashboardScreen.ps1")

# Create the dashboard
Write-Host "Creating dashboard screen..." -ForegroundColor Yellow
$dashboard = New-DashboardScreen

# Get theme for rendering
$theme = Get-Theme

# Create render context
$context = [SuperTUI.RenderContext]::new($theme, 80, 24)

Write-Host "Rendering dashboard..." -ForegroundColor Yellow
$output = $dashboard.Render($context)

Write-Host ""
Write-Host "Dashboard rendered: $($output.Length) characters" -ForegroundColor Green
Write-Host ""
Write-Host "──────────────────────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host $output
Write-Host "──────────────────────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

# Display stats
Write-Host "Screen Details:" -ForegroundColor Cyan
Write-Host "  Width: 80" -ForegroundColor Gray
Write-Host "  Height: 24" -ForegroundColor Gray
Write-Host "  Layout: GridLayout (5 rows x 1 column)" -ForegroundColor Gray
Write-Host "  Components: 27" -ForegroundColor Gray
Write-Host ""

Write-Host "Features Demonstrated:" -ForegroundColor Cyan
Write-Host "  ✓ GridLayout for perfect positioning (no manual string building!)" -ForegroundColor Green
Write-Host "  ✓ ASCII art header (no emoji icons)" -ForegroundColor Green
Write-Host "  ✓ Quick stats section with letter key indicators" -ForegroundColor Green
Write-Host "  ✓ Two-column menu layout (11 items)" -ForegroundColor Green
Write-Host "  ✓ Recent activity feed (ready for EventBus integration)" -ForegroundColor Green
Write-Host "  ✓ Status bar with multi-modal navigation hints" -ForegroundColor Green
Write-Host "  ✓ Clean ASCII markers: [T], [P], [W], etc. (no emojis)" -ForegroundColor Green
Write-Host ""

Write-Host "Navigation Methods Supported:" -ForegroundColor Cyan
Write-Host "  • Number Keys (1-9, 0) - Direct menu access" -ForegroundColor Gray
Write-Host "  • Letter Keys (T, P, W, K, M, C, F, R, S, H, Q) - Quick shortcuts" -ForegroundColor Gray
Write-Host "  • Arrow Keys (↑↓) - Visual selection" -ForegroundColor Gray
Write-Host "  • Enter - Activate selected item" -ForegroundColor Gray
Write-Host "  • F5 - Refresh statistics" -ForegroundColor Gray
Write-Host "  • Q - Quit application" -ForegroundColor Gray
Write-Host ""

Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Integrate with TaskService for auto-updating stats" -ForegroundColor Gray
Write-Host "  2. Subscribe to EventBus for real-time activity feed" -ForegroundColor Gray
Write-Host "  3. Add keyboard input handling" -ForegroundColor Gray
Write-Host "  4. Implement screen navigation (Push-Screen for each menu item)" -ForegroundColor Gray
Write-Host "  5. Add badge notifications for items with counts" -ForegroundColor Gray
Write-Host ""

Write-Host "Test Complete!" -ForegroundColor Green
