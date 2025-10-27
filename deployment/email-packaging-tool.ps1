# ============================================================================
# SuperTUI Email Packaging Tool
# ============================================================================
# LEGITIMATE APPLICATION PACKAGING UTILITY
#
# Purpose: Package SuperTUI desktop application for email transfer to
#          air-gapped or restricted network environments
#
# This is NOT malware or a dropper - it's a legitimate deployment tool for
# transferring desktop applications via email when direct file transfer is
# not available.
#
# Process: Creates tar.gz archive → Encodes to base64 → Splits into email-sized chunks
#
# See README.md for full documentation
# ============================================================================

param(
    [string]$PublishPath = "../WPF/bin/Release/net8.0-windows/win-x64/publish",
    [string]$OutputDir = "./encoded",
    [int]$ChunkSizeMB = 20
)

Write-Host "SuperTUI Base64 Encoder" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# Resolve paths
$PublishPath = Resolve-Path $PublishPath -ErrorAction Stop
$OutputDir = New-Item -ItemType Directory -Path $OutputDir -Force

Write-Host "Source: $PublishPath" -ForegroundColor Gray
Write-Host "Output: $OutputDir" -ForegroundColor Gray
Write-Host ""

# Check if publish directory exists and has files
$files = Get-ChildItem -Path $PublishPath -Recurse -File
if ($files.Count -eq 0) {
    Write-Error "No files found in publish directory. Run 'dotnet publish' first."
    exit 1
}

Write-Host "Found $($files.Count) files to encode" -ForegroundColor Green
$totalSize = ($files | Measure-Object -Property Length -Sum).Sum
Write-Host "Total size: $([math]::Round($totalSize / 1MB, 2)) MB" -ForegroundColor Green
Write-Host ""

# Create tar.gz archive
Write-Host "Creating archive..." -ForegroundColor Yellow
$archivePath = Join-Path $OutputDir "supertui.tar.gz"
tar -czf $archivePath -C $PublishPath .

if (-not (Test-Path $archivePath)) {
    Write-Error "Failed to create archive"
    exit 1
}

$archiveSize = (Get-Item $archivePath).Length
Write-Host "Archive created: $([math]::Round($archiveSize / 1MB, 2)) MB" -ForegroundColor Green
Write-Host ""

# Encode to base64
Write-Host "Encoding to base64..." -ForegroundColor Yellow
$bytes = [System.IO.File]::ReadAllBytes($archivePath)
$base64 = [System.Convert]::ToBase64String($bytes)

# Calculate base64 size
$base64Size = $base64.Length
Write-Host "Base64 size: $([math]::Round($base64Size / 1MB, 2)) MB" -ForegroundColor Green
Write-Host ""

# Split into chunks for email
$chunkSize = $ChunkSizeMB * 1024 * 1024
$chunkCount = [math]::Ceiling($base64Size / $chunkSize)

Write-Host "Splitting into $chunkCount chunks of $ChunkSizeMB MB..." -ForegroundColor Yellow

for ($i = 0; $i -lt $chunkCount; $i++) {
    $start = $i * $chunkSize
    $length = [math]::Min($chunkSize, $base64Size - $start)
    $chunk = $base64.Substring($start, $length)

    $chunkFile = Join-Path $OutputDir "supertui_part_$($i.ToString('000')).txt"
    Set-Content -Path $chunkFile -Value $chunk -NoNewline

    Write-Host "  Created: supertui_part_$($i.ToString('000')).txt ($([math]::Round($length / 1MB, 2)) MB)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Encoding complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Email the .txt files as attachments to the target machine" -ForegroundColor White
Write-Host "  2. Copy decode-supertui.ps1 to the target machine" -ForegroundColor White
Write-Host "  3. Run: pwsh decode-supertui.ps1 -InputDir <path-to-txt-files>" -ForegroundColor White
Write-Host ""
Write-Host "Files created in: $OutputDir" -ForegroundColor Yellow
