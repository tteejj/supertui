#!/usr/bin/env pwsh
# Test script to verify modal input blocking fix
# This script verifies the code changes are present and correct

Write-Host "=== Modal Input Blocking Fix Verification ===" -ForegroundColor Cyan
Write-Host ""

$mainWindowPath = "/home/teej/supertui/WPF/MainWindow.xaml.cs"

# Check if file exists
if (-not (Test-Path $mainWindowPath)) {
    Write-Host "ERROR: MainWindow.xaml.cs not found!" -ForegroundColor Red
    exit 1
}

Write-Host "File found: $mainWindowPath" -ForegroundColor Green
Write-Host ""

# Read the file
$content = Get-Content $mainWindowPath -Raw

# Test 1: ShowCommandPalette blocks input
Write-Host "[TEST 1] ShowCommandPalette blocks input..." -NoNewline
if ($content -match "ShowCommandPalette[\s\S]*?PaneCanvas\.IsHitTestVisible\s*=\s*false" -and
    $content -match "ShowCommandPalette[\s\S]*?PaneCanvas\.Focusable\s*=\s*false") {
    Write-Host " PASS" -ForegroundColor Green
    $test1 = $true
} else {
    Write-Host " FAIL" -ForegroundColor Red
    $test1 = $false
}

# Test 2: HideCommandPalette restores input
Write-Host "[TEST 2] HideCommandPalette restores input..." -NoNewline
if ($content -match "HideCommandPalette[\s\S]*?PaneCanvas\.IsHitTestVisible\s*=\s*true" -and
    $content -match "HideCommandPalette[\s\S]*?PaneCanvas\.Focusable\s*=\s*true") {
    Write-Host " PASS" -ForegroundColor Green
    $test2 = $true
} else {
    Write-Host " FAIL" -ForegroundColor Red
    $test2 = $false
}

# Test 3: ShowMovePaneModeOverlay blocks input
Write-Host "[TEST 3] ShowMovePaneModeOverlay blocks input..." -NoNewline
if ($content -match "ShowMovePaneModeOverlay[\s\S]*?PaneCanvas\.IsHitTestVisible\s*=\s*false" -and
    $content -match "ShowMovePaneModeOverlay[\s\S]*?PaneCanvas\.Focusable\s*=\s*false") {
    Write-Host " PASS" -ForegroundColor Green
    $test3 = $true
} else {
    Write-Host " FAIL" -ForegroundColor Red
    $test3 = $false
}

# Test 4: HideMovePaneModeOverlay restores input
Write-Host "[TEST 4] HideMovePaneModeOverlay restores input..." -NoNewline
if ($content -match "HideMovePaneModeOverlay[\s\S]*?PaneCanvas\.IsHitTestVisible\s*=\s*true" -and
    $content -match "HideMovePaneModeOverlay[\s\S]*?PaneCanvas\.Focusable\s*=\s*true") {
    Write-Host " PASS" -ForegroundColor Green
    $test4 = $true
} else {
    Write-Host " FAIL" -ForegroundColor Red
    $test4 = $false
}

# Test 5: ShowDebugOverlay blocks input
Write-Host "[TEST 5] ShowDebugOverlay blocks input..." -NoNewline
if ($content -match "ShowDebugOverlay[\s\S]*?PaneCanvas\.IsHitTestVisible\s*=\s*false" -and
    $content -match "ShowDebugOverlay[\s\S]*?PaneCanvas\.Focusable\s*=\s*false") {
    Write-Host " PASS" -ForegroundColor Green
    $test5 = $true
} else {
    Write-Host " FAIL" -ForegroundColor Red
    $test5 = $false
}

# Test 6: HideDebugOverlay restores input
Write-Host "[TEST 6] HideDebugOverlay restores input..." -NoNewline
if ($content -match "HideDebugOverlay[\s\S]*?PaneCanvas\.IsHitTestVisible\s*=\s*true" -and
    $content -match "HideDebugOverlay[\s\S]*?PaneCanvas\.Focusable\s*=\s*true") {
    Write-Host " PASS" -ForegroundColor Green
    $test6 = $true
} else {
    Write-Host " FAIL" -ForegroundColor Red
    $test6 = $false
}

# Test 7: Comments present
Write-Host "[TEST 7] CRITICAL FIX comments present..." -NoNewline
$commentCount = ([regex]::Matches($content, "CRITICAL FIX:")).Count
if ($commentCount -ge 10) {
    Write-Host " PASS ($commentCount comments)" -ForegroundColor Green
    $test7 = $true
} else {
    Write-Host " FAIL (only $commentCount comments)" -ForegroundColor Red
    $test7 = $false
}

# Test 8: Build succeeds
Write-Host "[TEST 8] Build succeeds..." -NoNewline
Push-Location "/home/teej/supertui/WPF"
$buildResult = dotnet build SuperTUI.csproj 2>&1 | Out-String
Pop-Location
if ($buildResult -match "Build succeeded" -and $buildResult -match "0 Error") {
    Write-Host " PASS" -ForegroundColor Green
    $test8 = $true
} else {
    Write-Host " FAIL" -ForegroundColor Red
    $test8 = $false
}

# Summary
Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
$passCount = @($test1, $test2, $test3, $test4, $test5, $test6, $test7, $test8) | Where-Object { $_ } | Measure-Object | Select-Object -ExpandProperty Count
$totalTests = 8

Write-Host "Passed: $passCount / $totalTests" -ForegroundColor $(if ($passCount -eq $totalTests) { "Green" } else { "Yellow" })

if ($passCount -eq $totalTests) {
    Write-Host ""
    Write-Host "SUCCESS: All modal input blocking fixes verified!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Test manually on Windows (requires Windows environment)" -ForegroundColor White
    Write-Host "2. Open command palette (Shift+;) and verify background panes don't receive input" -ForegroundColor White
    Write-Host "3. Activate move mode (F12) and verify only arrow keys work" -ForegroundColor White
    Write-Host "4. Show debug overlay (Ctrl+Shift+D) and verify background is blocked" -ForegroundColor White
    exit 0
} else {
    Write-Host ""
    Write-Host "FAILURE: Some tests failed. Please review the fixes." -ForegroundColor Red
    exit 1
}
