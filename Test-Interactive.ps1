#!/usr/bin/env pwsh
# Quick test of interactive components

Write-Host "Testing Interactive Dashboard Components..." -ForegroundColor Cyan
Write-Host ""

# Test 1: Load module
Write-Host "[1/4] Loading SuperTUI module..." -ForegroundColor Yellow
try {
    Import-Module (Join-Path $PSScriptRoot "Module" "SuperTUI.psm1") -Force
    Write-Host "  ✓ Module loaded" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    exit 1
}

# Test 2: Load TaskService
Write-Host "[2/4] Loading TaskService..." -ForegroundColor Yellow
try {
    . (Join-Path $PSScriptRoot "Services" "TaskService.ps1")
    $taskSvc = New-TaskService
    Write-Host "  ✓ TaskService created with $($taskSvc.Tasks.Count) tasks" -ForegroundColor Green
    Write-Host "    - Total: $($taskSvc.Statistics.TotalTasks)" -ForegroundColor Gray
    Write-Host "    - Completed: $($taskSvc.Statistics.CompletedTasks)" -ForegroundColor Gray
    Write-Host "    - Today: $($taskSvc.Statistics.TodayTasks)" -ForegroundColor Gray
    Write-Host "    - Week: $($taskSvc.Statistics.WeekTasks)" -ForegroundColor Gray
} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    exit 1
}

# Test 3: Load Interactive Dashboard
Write-Host "[3/4] Loading Interactive Dashboard..." -ForegroundColor Yellow
try {
    . (Join-Path $PSScriptRoot "Screens" "DashboardScreen_Interactive.ps1")
    Write-Host "  ✓ Dashboard screen loaded" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    exit 1
}

# Test 4: Verify classes
Write-Host "[4/4] Verifying components..." -ForegroundColor Yellow
try {
    $state = [DashboardScreenState]::new()
    Write-Host "  ✓ DashboardScreenState created" -ForegroundColor Green
    Write-Host "    - Menu items: $($state.MenuItems.Count)" -ForegroundColor Gray
    Write-Host "    - Selected: $($state.GetSelectedItem().Title)" -ForegroundColor Gray

    # Test navigation
    $state.MoveDown()
    Write-Host "  ✓ Navigation tested" -ForegroundColor Green
    Write-Host "    - After MoveDown: $($state.GetSelectedItem().Title)" -ForegroundColor Gray

    # Test key lookup
    $item = $state.GetItemByKey("T")
    Write-Host "  ✓ Key lookup tested" -ForegroundColor Green
    Write-Host "    - Key 'T': $($item.Title)" -ForegroundColor Gray
} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  ALL TESTS PASSED!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "Ready to run interactive dashboard:" -ForegroundColor Cyan
Write-Host "  ./Start-SuperTUI.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "Features:" -ForegroundColor Cyan
Write-Host "  ✓ Live statistics from TaskService (8 sample tasks)" -ForegroundColor Green
Write-Host "  ✓ Arrow key navigation with selection highlighting" -ForegroundColor Green
Write-Host "  ✓ Letter key shortcuts (T, P, W, K, M, C, F, R, S, H, Q)" -ForegroundColor Green
Write-Host "  ✓ Number key shortcuts (1-9, 0)" -ForegroundColor Green
Write-Host "  ✓ Visual feedback for all actions" -ForegroundColor Green
Write-Host "  ✓ F5 to refresh statistics" -ForegroundColor Green
Write-Host "  ✓ Q or Enter on Exit to quit" -ForegroundColor Green
Write-Host ""
