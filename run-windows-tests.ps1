#!/usr/bin/env pwsh
# Windows test runner with full diagnostics collection
# Runs all tests, collects logs/screenshots/dumps, auto-commits results

param(
    [switch]$SkipBuild,
    [switch]$SkipCommit,
    [string]$Filter = "*"
)

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SuperTUI Windows Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$StartTime = Get-Date

# Create test results directory
$ResultsDir = Join-Path $PSScriptRoot "test-results" (Get-Date -Format "yyyy-MM-dd_HH-mm-ss")
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
Write-Host "Results directory: $ResultsDir" -ForegroundColor Yellow
Write-Host ""

# Change to WPF directory
$WpfDir = Join-Path $PSScriptRoot "WPF"
Push-Location $WpfDir

try {
    # Build main project
    if (-not $SkipBuild) {
        Write-Host "Building SuperTUI..." -ForegroundColor Cyan
        dotnet build SuperTUI.csproj --configuration Release --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "✗ Build failed" -ForegroundColor Red
            exit 1
        }
        Write-Host "✓ Build succeeded" -ForegroundColor Green
        Write-Host ""
    }

    # Change to test directory
    $TestDir = Join-Path $WpfDir "Tests"
    Push-Location $TestDir

    # Restore and build tests
    Write-Host "Building tests..." -ForegroundColor Cyan
    dotnet restore --verbosity quiet
    dotnet build --configuration Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Test build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Test build succeeded" -ForegroundColor Green
    Write-Host ""

    # Run all tests
    Write-Host "Running test suite..." -ForegroundColor Cyan
    Write-Host "--------------------------------------------" -ForegroundColor Gray

    $TestResultsPath = Join-Path $ResultsDir "test-results.trx"

    dotnet test `
        --configuration Release `
        --no-build `
        --filter $Filter `
        --logger "trx;LogFileName=$TestResultsPath" `
        --logger "console;verbosity=detailed" `
        --collect:"XPlat Code Coverage" `
        --results-directory $ResultsDir

    $TestExitCode = $LASTEXITCODE

    Pop-Location # Back to WPF directory

} finally {
    Pop-Location # Back to root
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan

# Collect diagnostics
Write-Host "Collecting diagnostics..." -ForegroundColor Cyan

$DiagnosticsFile = Join-Path $ResultsDir "DIAGNOSTICS.txt"
$diagnosticsContent = @"
SuperTUI Windows Test Run
========================================
Date: $(Get-Date)
Machine: $env:COMPUTERNAME
User: $env:USERNAME
OS: $((Get-CimInstance Win32_OperatingSystem).Caption)
.NET Version: $(dotnet --version)
PowerShell Version: $($PSVersionTable.PSVersion)

Test Results Directory: $ResultsDir
Test Exit Code: $TestExitCode

========================================
Git Status:
$(git status --short)

========================================
Recent Commits:
$(git log -5 --oneline)

========================================
Build Directory Size:
$((Get-ChildItem -Path $WpfDir -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB) MB

========================================
Test Artifacts:
$(Get-ChildItem -Path $ResultsDir -Recurse | Format-Table Name, Length, LastWriteTime | Out-String)

"@

Set-Content -Path $DiagnosticsFile -Value $diagnosticsContent
Write-Host "✓ Diagnostics collected: $DiagnosticsFile" -ForegroundColor Green
Write-Host ""

# Check for application logs
$AppLogsDir = Join-Path $WpfDir "logs"
if (Test-Path $AppLogsDir) {
    Write-Host "Copying application logs..." -ForegroundColor Cyan
    Copy-Item -Path $AppLogsDir -Destination (Join-Path $ResultsDir "app-logs") -Recurse -Force
    Write-Host "✓ Application logs copied" -ForegroundColor Green
}

# Create summary
$Duration = (Get-Date) - $StartTime
$SummaryFile = Join-Path $ResultsDir "SUMMARY.txt"

if ($TestExitCode -eq 0) {
    $status = "✅ ALL TESTS PASSED"
    $statusColor = "Green"
} else {
    $status = "❌ TESTS FAILED"
    $statusColor = "Red"
}

$summaryContent = @"
$status
========================================
Duration: $($Duration.TotalSeconds.ToString("F2")) seconds
Exit Code: $TestExitCode
Results: $ResultsDir

"@

Set-Content -Path $SummaryFile -Value $summaryContent

Write-Host ""
Write-Host $status -ForegroundColor $statusColor
Write-Host "Duration: $($Duration.TotalSeconds.ToString("F2"))s" -ForegroundColor Gray
Write-Host ""

# Auto-commit results (if not skipped)
if (-not $SkipCommit) {
    Write-Host "Committing test results..." -ForegroundColor Cyan

    # Create .gitignore for test-results if it doesn't exist
    $GitignorePath = Join-Path $PSScriptRoot "test-results" ".gitignore"
    if (-not (Test-Path $GitignorePath)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $GitignorePath) | Out-Null
        @"
# Keep only latest 5 test runs
*
!.gitignore
!/latest/
"@ | Set-Content -Path $GitignorePath
    }

    # Copy to 'latest' for easy access
    $LatestDir = Join-Path $PSScriptRoot "test-results" "latest"
    if (Test-Path $LatestDir) {
        Remove-Item -Path $LatestDir -Recurse -Force
    }
    Copy-Item -Path $ResultsDir -Destination $LatestDir -Recurse -Force

    git add test-results/latest/
    git commit -m "Test results $(Get-Date -Format 'yyyy-MM-dd HH:mm') - Exit code: $TestExitCode" --quiet

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Test results committed to git" -ForegroundColor Green
        Write-Host ""
        Write-Host "You can now pull these results on Linux:" -ForegroundColor Yellow
        Write-Host "  git pull" -ForegroundColor Gray
        Write-Host "  cat test-results/latest/SUMMARY.txt" -ForegroundColor Gray
    } else {
        Write-Host "⚠ Failed to commit results (no changes?)" -ForegroundColor Yellow
    }
}

Write-Host ""
exit $TestExitCode
