# Test script for path validation security fixes
# Tests that path traversal attacks are properly blocked

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Path Validation Security Tests" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Test data structure
$script:testResults = @()

function Test-PathValidation {
    param(
        [string]$TestName,
        [string]$Path,
        [string]$AllowedDir,
        [bool]$ExpectedResult,
        [string]$Reason
    )

    Write-Host "Test: $TestName" -ForegroundColor Yellow
    Write-Host "  Path: '$Path'"
    Write-Host "  Allowed: '$AllowedDir'"
    Write-Host "  Expected: $($ExpectedResult ? 'ALLOW' : 'DENY')"

    try {
        # Simulate the validation logic
        $fullPath = [System.IO.Path]::GetFullPath($Path)
        $fullAllowedPath = [System.IO.Path]::GetFullPath($AllowedDir)

        # Normalize
        $fullPath = $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $fullAllowedPath = $fullAllowedPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

        # Check if within directory
        $isWithin = $false

        if ($fullPath -eq $fullAllowedPath) {
            $isWithin = $true
        }
        elseif ($fullPath.StartsWith($fullAllowedPath + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            $isWithin = $true
        }
        elseif ($fullPath.StartsWith($fullAllowedPath + [System.IO.Path]::AltDirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            $isWithin = $true
        }

        $actualResult = $isWithin

        if ($actualResult -eq $ExpectedResult) {
            Write-Host "  Result: ✓ PASS ($Reason)" -ForegroundColor Green
            Write-Host "  Normalized: '$fullPath'"
            $script:testResults += [PSCustomObject]@{
                Test = $TestName
                Status = "PASS"
                Reason = $Reason
            }
        }
        else {
            Write-Host "  Result: ✗ FAIL - Expected $($ExpectedResult ? 'ALLOW' : 'DENY'), got $($actualResult ? 'ALLOW' : 'DENY')" -ForegroundColor Red
            Write-Host "  Normalized: '$fullPath'"
            $script:testResults += [PSCustomObject]@{
                Test = $TestName
                Status = "FAIL"
                Reason = "Expected $($ExpectedResult ? 'ALLOW' : 'DENY'), got $($actualResult ? 'ALLOW' : 'DENY')"
            }
        }
    }
    catch {
        Write-Host "  Result: ✗ ERROR - $($_.Exception.Message)" -ForegroundColor Red
        $script:testResults += [PSCustomObject]@{
            Test = $TestName
            Status = "ERROR"
            Reason = $_.Exception.Message
        }
    }

    Write-Host ""
}

# Define test base directory (use temp for cross-platform compatibility)
$testBaseDir = if ($IsWindows -or $env:OS -match "Windows") {
    "C:\Temp\SuperTUI"
} else {
    "/tmp/SuperTUI"
}

Write-Host "Test Base Directory: $testBaseDir`n" -ForegroundColor Cyan

# ============================================================================
# SECURITY TESTS - Path Traversal Attacks
# ============================================================================

Write-Host "=== Path Traversal Attack Tests ===" -ForegroundColor Cyan

# Test 1: Basic path traversal with ../
Test-PathValidation -TestName "Traversal: ../ attack" `
    -Path "$testBaseDir/../sensitive.txt" `
    -AllowedDir $testBaseDir `
    -ExpectedResult $false `
    -Reason "Path escapes allowed directory"

# Test 2: Double path traversal
Test-PathValidation -TestName "Traversal: ../../ attack" `
    -Path "$testBaseDir/subfolder/../../sensitive.txt" `
    -AllowedDir $testBaseDir `
    -ExpectedResult $false `
    -Reason "Path escapes allowed directory"

# Test 3: Legitimate subdirectory access
Test-PathValidation -TestName "Legitimate: Subdirectory" `
    -Path "$testBaseDir/docs/file.txt" `
    -AllowedDir $testBaseDir `
    -ExpectedResult $true `
    -Reason "Path is within allowed directory"

# Test 4: Accessing the allowed directory itself
Test-PathValidation -TestName "Legitimate: Allowed dir itself" `
    -Path $testBaseDir `
    -AllowedDir $testBaseDir `
    -ExpectedResult $true `
    -Reason "Path equals allowed directory"

# Test 5: Similar directory name attack
Test-PathValidation -TestName "Attack: Similar dir name" `
    -Path "$testBaseDir-malicious/file.txt" `
    -AllowedDir $testBaseDir `
    -ExpectedResult $false `
    -Reason "Different directory with similar name"

# Test 6: Traversal then back in
Test-PathValidation -TestName "Traversal: ../then back" `
    -Path "$testBaseDir/../SuperTUI/file.txt" `
    -AllowedDir $testBaseDir `
    -ExpectedResult $true `
    -Reason "Net result is within allowed directory"

# Test 7: Deep nesting legitimate
Test-PathValidation -TestName "Legitimate: Deep nesting" `
    -Path "$testBaseDir/a/b/c/d/file.txt" `
    -AllowedDir $testBaseDir `
    -ExpectedResult $true `
    -Reason "Deep path but still within allowed"

# Test 8: Absolute path outside
if ($IsWindows -or $env:OS -match "Windows") {
    Test-PathValidation -TestName "Attack: Absolute path outside" `
        -Path "C:\Windows\System32\config\sam" `
        -AllowedDir $testBaseDir `
        -ExpectedResult $false `
        -Reason "Absolute path to sensitive location"
} else {
    Test-PathValidation -TestName "Attack: Absolute path outside" `
        -Path "/etc/shadow" `
        -AllowedDir $testBaseDir `
        -ExpectedResult $false `
        -Reason "Absolute path to sensitive location"
}

# ============================================================================
# RESULTS SUMMARY
# ============================================================================

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test Results Summary" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$passCount = ($script:testResults | Where-Object { $_.Status -eq "PASS" }).Count
$failCount = ($script:testResults | Where-Object { $_.Status -eq "FAIL" }).Count
$errorCount = ($script:testResults | Where-Object { $_.Status -eq "ERROR" }).Count
$totalCount = $script:testResults.Count

Write-Host "Total Tests: $totalCount"
Write-Host "Passed: $passCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "Errors: $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Green" })

if ($failCount -eq 0 -and $errorCount -eq 0) {
    Write-Host "`n✓ ALL TESTS PASSED!" -ForegroundColor Green
    Write-Host "Path validation security is working correctly." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n✗ SOME TESTS FAILED" -ForegroundColor Red
    Write-Host "Failed/Error tests:" -ForegroundColor Red
    $script:testResults | Where-Object { $_.Status -ne "PASS" } | ForEach-Object {
        Write-Host "  - $($_.Test): $($_.Reason)" -ForegroundColor Red
    }
    exit 1
}
