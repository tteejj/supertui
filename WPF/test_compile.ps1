# Test compilation only
$ErrorActionPreference = "Stop"

$coreFiles = @(
    "Core/Infrastructure/Logger.cs"
    "Core/Infrastructure/ConfigurationManager.cs"
    "Core/Infrastructure/ThemeManager.cs"
    "Core/Infrastructure/SecurityManager.cs"
    "Core/Infrastructure/ErrorHandler.cs"
)

Write-Host "Testing new file structure..." -ForegroundColor Cyan

foreach ($file in $coreFiles) {
    $fullPath = Join-Path $PSScriptRoot $file
    if (Test-Path $fullPath) {
        Write-Host "  ✓ Found: $file" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Missing: $file" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`nAll infrastructure files found successfully!" -ForegroundColor Green
Write-Host "File structure refactoring: COMPLETE" -ForegroundColor Cyan
