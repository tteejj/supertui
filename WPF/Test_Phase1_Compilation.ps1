#!/usr/bin/env pwsh
# Test script to verify Phase 1 compilation

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Phase 1 Compilation Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Define source files
$sourceFiles = @(
    # Infrastructure
    "$scriptDir/Core/Infrastructure/Logger.cs"
    "$scriptDir/Core/Extensions.cs"

    # Models
    "$scriptDir/Core/Models/TaskModels.cs"
    "$scriptDir/Core/Models/ProjectModels.cs"
    "$scriptDir/Core/Models/TimeTrackingModels.cs"

    # Services
    "$scriptDir/Core/Services/TaskService.cs"
    "$scriptDir/Core/Services/ProjectService.cs"
    "$scriptDir/Core/Services/TimeTrackingService.cs"
)

# Check that all files exist
Write-Host "Checking source files..." -ForegroundColor Yellow
$missing = @()
foreach ($file in $sourceFiles) {
    if (Test-Path $file) {
        Write-Host "  ✓ $($file | Split-Path -Leaf)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $($file | Split-Path -Leaf) - NOT FOUND" -ForegroundColor Red
        $missing += $file
    }
}

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "ERROR: Missing files!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Compiling C# code..." -ForegroundColor Yellow

try {
    # Compile all source files
    # Note: We skip WPF assemblies since we're on Linux - these files don't actually depend on WPF
    Add-Type -Path $sourceFiles -ReferencedAssemblies @(
        "System.Linq"
        "System.Collections"
        "System.Runtime"
    ) -ErrorAction Stop

    Write-Host "  ✓ Compilation successful!" -ForegroundColor Green

    Write-Host ""
    Write-Host "Verifying types..." -ForegroundColor Yellow

    # Check that types were loaded
    $types = @(
        # Models
        "SuperTUI.Core.Models.Project"
        "SuperTUI.Core.Models.ProjectStatus"
        "SuperTUI.Core.Models.ProjectWithStats"
        "SuperTUI.Core.Models.ProjectTaskStats"
        "SuperTUI.Core.Models.AuditPeriod"
        "SuperTUI.Core.Models.ProjectContact"
        "SuperTUI.Core.Models.ProjectNote"
        "SuperTUI.Core.Models.TimeEntry"
        "SuperTUI.Core.Models.WeeklyTimeReport"
        "SuperTUI.Core.Models.ProjectTimeAggregate"
        "SuperTUI.Core.Models.FiscalYearSummary"

        # Services
        "SuperTUI.Core.Services.ProjectService"
        "SuperTUI.Core.Services.TimeTrackingService"
    )

    $allTypesFound = $true
    foreach ($typeName in $types) {
        $type = [Type]::GetType($typeName)
        if ($type) {
            Write-Host "  ✓ $typeName" -ForegroundColor Green
        } else {
            Write-Host "  ✗ $typeName - NOT FOUND" -ForegroundColor Red
            $allTypesFound = $false
        }
    }

    if (-not $allTypesFound) {
        Write-Host ""
        Write-Host "ERROR: Some types were not loaded!" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Testing service initialization..." -ForegroundColor Yellow

    # Test ProjectService singleton
    $projectService = [SuperTUI.Core.Services.ProjectService]::Instance
    if ($projectService) {
        Write-Host "  ✓ ProjectService singleton created" -ForegroundColor Green
    } else {
        Write-Host "  ✗ ProjectService singleton failed" -ForegroundColor Red
        exit 1
    }

    # Test TimeTrackingService singleton
    $timeService = [SuperTUI.Core.Services.TimeTrackingService]::Instance
    if ($timeService) {
        Write-Host "  ✓ TimeTrackingService singleton created" -ForegroundColor Green
    } else {
        Write-Host "  ✗ TimeTrackingService singleton failed" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Testing model instantiation..." -ForegroundColor Yellow

    # Test Project model
    $project = [SuperTUI.Core.Models.Project]::new()
    $project.Name = "Test Project"
    $project.Nickname = "TEST001"
    $project.Priority = [SuperTUI.Core.Models.TaskPriority]::High
    if ($project.Name -eq "Test Project") {
        Write-Host "  ✓ Project model works" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Project model failed" -ForegroundColor Red
        exit 1
    }

    # Test TimeEntry model
    $entry = [SuperTUI.Core.Models.TimeEntry]::new()
    $entry.Hours = 8.5
    $entry.ProjectId = $project.Id
    if ($entry.TotalHours -eq 8.5) {
        Write-Host "  ✓ TimeEntry model works" -ForegroundColor Green
    } else {
        Write-Host "  ✗ TimeEntry model failed" -ForegroundColor Red
        exit 1
    }

    # Test AuditPeriod model
    $period = [SuperTUI.Core.Models.AuditPeriod]::new()
    $period.StartDate = Get-Date "2025-04-01"
    $period.EndDate = Get-Date "2026-03-31"
    if ($period.FiscalYear -eq 2026) {
        Write-Host "  ✓ AuditPeriod fiscal year calculation works" -ForegroundColor Green
    } else {
        Write-Host "  ✗ AuditPeriod fiscal year calculation failed (got $($period.FiscalYear), expected 2026)" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Testing helper methods..." -ForegroundColor Yellow

    # Test week ending calculation
    $weekEnding = [SuperTUI.Core.Services.TimeTrackingService]::GetCurrentWeekEnding()
    if ($weekEnding.DayOfWeek -eq [DayOfWeek]::Sunday) {
        Write-Host "  ✓ GetCurrentWeekEnding returns Sunday" -ForegroundColor Green
    } else {
        Write-Host "  ✗ GetCurrentWeekEnding failed (returned $($weekEnding.DayOfWeek))" -ForegroundColor Red
        exit 1
    }

    # Test fiscal year calculation
    $fy = [SuperTUI.Core.Services.TimeTrackingService]::GetCurrentFiscalYear()
    $currentMonth = (Get-Date).Month
    $expectedFY = if ($currentMonth -ge 4) { (Get-Date).Year + 1 } else { (Get-Date).Year }
    if ($fy -eq $expectedFY) {
        Write-Host "  ✓ Fiscal year calculation works (FY $fy)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Fiscal year calculation failed (got $fy, expected $expectedFY)" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "✓ ALL TESTS PASSED!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Phase 1 implementation is complete and functional." -ForegroundColor Cyan
    Write-Host ""

    # Summary
    Write-Host "Created Files:" -ForegroundColor Cyan
    Write-Host "  - Core/Models/ProjectModels.cs" -ForegroundColor White
    Write-Host "  - Core/Models/TimeTrackingModels.cs" -ForegroundColor White
    Write-Host "  - Core/Services/ProjectService.cs" -ForegroundColor White
    Write-Host "  - Core/Services/TimeTrackingService.cs" -ForegroundColor White
    Write-Host ""
    Write-Host "Updated Files:" -ForegroundColor Cyan
    Write-Host "  - Core/Services/TaskService.cs (added GetTasksForProject, GetProjectStats)" -ForegroundColor White
    Write-Host ""

    exit 0

} catch {
    Write-Host ""
    Write-Host "ERROR: Compilation failed!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
    exit 1
}
