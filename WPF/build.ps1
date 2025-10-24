# Build SuperTUI.dll

Write-Host "Building SuperTUI..." -ForegroundColor Cyan

try {
    dotnet build SuperTUI.csproj -c Release
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful!" -ForegroundColor Green
        Write-Host "DLL: bin/Release/net8.0-windows/SuperTUI.dll" -ForegroundColor Gray
    } else {
        Write-Error "Build failed"
        exit 1
    }
} catch {
    Write-Error "Build error: $_"
    exit 1
}
