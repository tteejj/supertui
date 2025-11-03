# Watch SuperTUI logs in real-time
$logPath = "$env:LOCALAPPDATA\SuperTUI\logs"

Write-Host "Watching for logs in: $logPath" -ForegroundColor Cyan
Write-Host "Waiting for log files..." -ForegroundColor Yellow
Write-Host ""

# Wait for log directory to exist
while (-not (Test-Path $logPath)) {
    Start-Sleep -Milliseconds 500
}

# Get the most recent log file
$logFile = Get-ChildItem $logPath -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($logFile) {
    Write-Host "Tailing: $($logFile.FullName)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Get-Content $logFile.FullName -Wait -Tail 50 | Where-Object { $_ -match "FileBrowser|Initialize|NavigateToDirectory|UpdateFileList|LoadFilesAsync|CommandPalette" }
} else {
    Write-Host "No log files found yet. Start SuperTUI first." -ForegroundColor Red
}
