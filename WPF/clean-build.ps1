# Clean and rebuild SuperTUI from scratch

Write-Host "Cleaning build artifacts..." -ForegroundColor Cyan

# Remove bin and obj directories
if (Test-Path "bin") {
    Remove-Item -Recurse -Force "bin"
    Write-Host "  Removed bin/" -ForegroundColor Gray
}

if (Test-Path "obj") {
    Remove-Item -Recurse -Force "obj"
    Write-Host "  Removed obj/" -ForegroundColor Gray
}

Write-Host "Clean complete!" -ForegroundColor Green
Write-Host ""

# Now build
Write-Host "Building SuperTUI..." -ForegroundColor Cyan
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
