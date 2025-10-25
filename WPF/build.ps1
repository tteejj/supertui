# Build SuperTUI.dll

Write-Host "Building SuperTUI..." -ForegroundColor Cyan

# Check if dotnet SDK is installed
$dotnetExists = Get-Command dotnet -ErrorAction SilentlyContinue

if (-not $dotnetExists) {
    Write-Host ""
    Write-Host "ERROR: .NET SDK not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "SuperTUI requires .NET 8.0 SDK or later to build." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To install .NET SDK:" -ForegroundColor Cyan
    Write-Host "  1. Visit: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
    Write-Host "  2. Download and install the .NET 8.0 SDK (not just the runtime)" -ForegroundColor White
    Write-Host "  3. Restart your PowerShell session" -ForegroundColor White
    Write-Host "  4. Run this script again" -ForegroundColor White
    Write-Host ""
    Write-Host "Quick install (run as Administrator):" -ForegroundColor Cyan
    Write-Host "  winget install Microsoft.DotNet.SDK.8" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

# Verify .NET version
$dotnetVersion = & dotnet --version 2>$null
Write-Host "Found .NET SDK version: $dotnetVersion" -ForegroundColor Gray

try {
    dotnet build SuperTUI.csproj -c Release
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "Build successful!" -ForegroundColor Green
        Write-Host "DLL: bin/Release/net8.0-windows/SuperTUI.dll" -ForegroundColor Gray
        Write-Host ""
    } else {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit 1
    }
} catch {
    Write-Error "Build error: $_"
    exit 1
}
