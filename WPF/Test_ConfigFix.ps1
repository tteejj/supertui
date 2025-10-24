# Test script for ConfigurationManager type system fix
# Tests that complex types (List<string>) can be saved and loaded correctly

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  ConfigurationManager Type System Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Compiling infrastructure..." -ForegroundColor Yellow

# Compile the actual infrastructure code
$sourceFiles = @(
    "Core/Infrastructure/Logger.cs"
    "Core/Infrastructure/ConfigurationManager.cs"
)

$sources = @()
foreach ($file in $sourceFiles) {
    $fullPath = Join-Path $PSScriptRoot $file
    if (Test-Path $fullPath) {
        $sources += Get-Content $fullPath -Raw
    } else {
        Write-Host "✗ File not found: $file" -ForegroundColor Red
        exit 1
    }
}

$combinedSource = $sources -join "`n`n"

try {
    Add-Type -TypeDefinition $combinedSource -ErrorAction Stop
    Write-Host "✓ Compilation successful`n" -ForegroundColor Green
}
catch {
    Write-Host "✗ Compilation failed!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host "Running tests...`n" -ForegroundColor Yellow

# Test script
$tempFile = [System.IO.Path]::GetTempFileName()

try {
    Write-Host "Test 1: Initialize config and check default List<string>..." -ForegroundColor Cyan
    $config = [SuperTUI.Infrastructure.ConfigurationManager]::Instance
    $config.Initialize($tempFile)

    # Verify default value
    $extensions = $config.Get([System.Collections.Generic.List[string]], "Security.AllowedExtensions")
    Write-Host "  Initial AllowedExtensions count: $($extensions.Count)"

    if ($extensions.Count -gt 0) {
        Write-Host "  Extensions: $($extensions -join ', ')"
        Write-Host "  ✓ Default value loaded correctly" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: Extensions is null or empty" -ForegroundColor Red
        exit 1
    }

    # Modify and save
    Write-Host "`nTest 2: Modify list and save to file..." -ForegroundColor Cyan
    $newExtensions = [System.Collections.Generic.List[string]]::new()
    $newExtensions.Add(".txt")
    $newExtensions.Add(".md")
    $newExtensions.Add(".json")
    $newExtensions.Add(".xml")
    $newExtensions.Add(".yaml")

    $config.Set([System.Collections.Generic.List[string]], "Security.AllowedExtensions", $newExtensions, $true)
    Write-Host "  Saved modified list (5 extensions)" -ForegroundColor Green

    # Read the saved file to see what it looks like
    Write-Host "`nTest 3: Inspect saved JSON..." -ForegroundColor Cyan
    $jsonContent = Get-Content $tempFile -Raw
    $jsonObj = $jsonContent | ConvertFrom-Json
    Write-Host "  Security.AllowedExtensions in file: $($jsonObj.'Security.AllowedExtensions' -join ', ')"

    # Load in new instance
    Write-Host "`nTest 4: Load config in new instance..." -ForegroundColor Cyan
    # Need to create new instance - but singleton pattern prevents this
    # So we'll just load the file again
    $config.LoadFromFile($tempFile)

    $loadedExtensions = $config.Get([System.Collections.Generic.List[string]], "Security.AllowedExtensions")
    Write-Host "  Loaded AllowedExtensions count: $($loadedExtensions.Count)"

    if ($loadedExtensions.Count -eq 5) {
        Write-Host "  Extensions: $($loadedExtensions -join ', ')"
        Write-Host "  ✓ Complex type loaded correctly from file!" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: Loaded extensions has wrong count" -ForegroundColor Red
        Write-Host "  Expected: 5, Got: $($loadedExtensions.Count)" -ForegroundColor Red
        exit 1
    }

    # Test other types
    Write-Host "`nTest 5: Test primitive types..." -ForegroundColor Cyan
    $maxFileSize = $config.Get([int], "Security.MaxFileSize", 10)
    $validateAccess = $config.Get([bool], "Security.ValidateFileAccess", $true)
    $theme = $config.Get([string], "UI.Theme", "Dark")

    Write-Host "  MaxFileSize: $maxFileSize (expected: 10)"
    Write-Host "  ValidateFileAccess: $validateAccess (expected: True)"
    Write-Host "  Theme: $theme (expected: Dark)"

    if ($maxFileSize -eq 10 -and $validateAccess -eq $true -and $theme -eq "Dark") {
        Write-Host "  ✓ All primitive types loaded correctly" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: Primitive types have wrong values" -ForegroundColor Red
        exit 1
    }

    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "ALL TESTS PASSED ✓" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Green

} finally {
    try { Remove-Item $tempFile -ErrorAction SilentlyContinue } catch { }
}

Write-Host "✓ ConfigurationManager type system fix verified!" -ForegroundColor Green
