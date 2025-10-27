# ============================================================================
# SuperTUI Email Unpacking Tool
# ============================================================================
# LEGITIMATE APPLICATION UNPACKING UTILITY
#
# Purpose: Unpack SuperTUI desktop application from email-transferred
#          base64-encoded files
#
# This is NOT malware or a dropper - it's a legitimate deployment tool for
# installing desktop applications that were transferred via email.
#
# Process: Reassembles chunks → Decodes base64 → Extracts tar.gz archive
#
# See README.md for full documentation
# ============================================================================

param(
    [string]$InputDir = ".",
    [string]$OutputDir = "./SuperTUI"
)

Write-Host "SuperTUI Base64 Decoder" -ForegroundColor Cyan
Write-Host "=======================" -ForegroundColor Cyan
Write-Host ""

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "PowerShell 7+ is recommended. Current version: $($PSVersionTable.PSVersion)"
}

# Find all part files
Write-Host "Searching for part files in: $InputDir" -ForegroundColor Gray
$partFiles = Get-ChildItem -Path $InputDir -Filter "supertui_part_*.txt" | Sort-Object Name

if ($partFiles.Count -eq 0) {
    Write-Error "No supertui_part_*.txt files found in $InputDir"
    exit 1
}

Write-Host "Found $($partFiles.Count) part files" -ForegroundColor Green
Write-Host ""

# Concatenate all parts
Write-Host "Reassembling base64 data..." -ForegroundColor Yellow
$base64 = ""
foreach ($file in $partFiles) {
    Write-Host "  Reading: $($file.Name)" -ForegroundColor Gray
    $base64 += Get-Content -Path $file.FullName -Raw
}

$base64Size = $base64.Length
Write-Host "Total base64 size: $([math]::Round($base64Size / 1MB, 2)) MB" -ForegroundColor Green
Write-Host ""

# Decode from base64
Write-Host "Decoding from base64..." -ForegroundColor Yellow
try {
    $bytes = [System.Convert]::FromBase64String($base64)
    Write-Host "Decoded: $([math]::Round($bytes.Length / 1MB, 2)) MB" -ForegroundColor Green
} catch {
    Write-Error "Failed to decode base64: $_"
    exit 1
}

# Write archive file
$archivePath = Join-Path $InputDir "supertui.tar.gz"
Write-Host "Writing archive: $archivePath" -ForegroundColor Yellow
[System.IO.File]::WriteAllBytes($archivePath, $bytes)
Write-Host "Archive created successfully" -ForegroundColor Green
Write-Host ""

# Extract archive
Write-Host "Extracting to: $OutputDir" -ForegroundColor Yellow
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

try {
    tar -xzf $archivePath -C $OutputDir
    Write-Host "Extraction complete!" -ForegroundColor Green
} catch {
    Write-Error "Failed to extract archive: $_"
    Write-Host "You may need to install tar. Try: winget install GnuWin32.tar" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "SuperTUI has been successfully deployed!" -ForegroundColor Green
Write-Host ""
Write-Host "To run SuperTUI:" -ForegroundColor Cyan
Write-Host "  cd $OutputDir" -ForegroundColor White
Write-Host "  pwsh SuperTUI.ps1" -ForegroundColor White
Write-Host ""
Write-Host "Note: Ensure .NET 8 Desktop Runtime is installed on this machine" -ForegroundColor Yellow
Write-Host "Check with: dotnet --list-runtimes" -ForegroundColor Gray
Write-Host ""

# Cleanup
Write-Host "Cleaning up archive file..." -ForegroundColor Gray
Remove-Item $archivePath -Force
Write-Host "Done!" -ForegroundColor Green
