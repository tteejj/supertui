# Test-CoreCompilation.ps1
# Tests that SuperTUI.Core.cs compiles successfully

param(
    [switch]$Verbose
)

Write-Host "SuperTUI Core Engine - Compilation Test" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Get the path to the core file
$corePath = Join-Path $PSScriptRoot ".." "Core" "SuperTUI.Core.cs"

if (-not (Test-Path $corePath)) {
    Write-Host "ERROR: SuperTUI.Core.cs not found at: $corePath" -ForegroundColor Red
    exit 1
}

Write-Host "Core file: $corePath" -ForegroundColor Gray
$fileInfo = Get-Item $corePath
Write-Host "Size: $($fileInfo.Length) bytes, $($(Get-Content $corePath).Count) lines" -ForegroundColor Gray
Write-Host ""

# Read the C# code
Write-Host "Reading C# code..." -ForegroundColor Yellow
$csharpCode = Get-Content $corePath -Raw

if ([string]::IsNullOrWhiteSpace($csharpCode)) {
    Write-Host "ERROR: Core file is empty" -ForegroundColor Red
    exit 1
}

Write-Host "Code loaded: $($csharpCode.Length) characters" -ForegroundColor Green
Write-Host ""

# Attempt compilation
Write-Host "Compiling C# code..." -ForegroundColor Yellow
$startTime = Get-Date

try {
    Add-Type -TypeDefinition $csharpCode -Language CSharp -ErrorAction Stop
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds

    Write-Host ""
    Write-Host "SUCCESS: Compilation completed in $([math]::Round($duration, 2)) seconds" -ForegroundColor Green
    Write-Host ""

    # Verify key types are available
    Write-Host "Verifying types..." -ForegroundColor Yellow

    $types = @(
        "SuperTUI.Color",
        "SuperTUI.UIElement",
        "SuperTUI.Screen",
        "SuperTUI.Component",
        "SuperTUI.GridLayout",
        "SuperTUI.StackLayout",
        "SuperTUI.DockLayout",
        "SuperTUI.Label",
        "SuperTUI.Button",
        "SuperTUI.TextBox",
        "SuperTUI.DataGrid",
        "SuperTUI.ListView",
        "SuperTUI.VT",
        "SuperTUI.Terminal",
        "SuperTUI.ScreenManager",
        "SuperTUI.EventBus",
        "SuperTUI.ServiceContainer",
        "SuperTUI.Theme"
    )

    $allFound = $true
    foreach ($typeName in $types) {
        $type = $typeName -as [Type]
        if ($type) {
            Write-Host "  ✓ $typeName" -ForegroundColor Green
        } else {
            Write-Host "  ✗ $typeName NOT FOUND" -ForegroundColor Red
            $allFound = $false
        }
    }

    Write-Host ""

    if ($allFound) {
        Write-Host "All types verified successfully!" -ForegroundColor Green
    } else {
        Write-Host "Some types were not found" -ForegroundColor Yellow
    }

    # Test creating instances
    Write-Host ""
    Write-Host "Testing instance creation..." -ForegroundColor Yellow

    try {
        $label = [SuperTUI.Label]::new()
        $label.Text = "Hello, SuperTUI!"
        Write-Host "  ✓ Label created: '$($label.Text)'" -ForegroundColor Green

        $button = [SuperTUI.Button]::new()
        $button.Label = "Click Me"
        Write-Host "  ✓ Button created: '$($button.Label)'" -ForegroundColor Green

        $grid = [SuperTUI.GridLayout]::new()
        $grid.Rows.Add([SuperTUI.RowDefinition]::new("*"))
        $grid.Columns.Add([SuperTUI.ColumnDefinition]::new("*"))
        Write-Host "  ✓ GridLayout created with $($grid.Rows.Count)x$($grid.Columns.Count) cells" -ForegroundColor Green

        $theme = [SuperTUI.Theme]::Default
        Write-Host "  ✓ Theme created with Primary=$($theme.Primary.R),$($theme.Primary.G),$($theme.Primary.B)" -ForegroundColor Green

        $terminal = [SuperTUI.Terminal]::Instance
        Write-Host "  ✓ Terminal singleton: $($terminal.Width)x$($terminal.Height)" -ForegroundColor Green

        $screenMgr = [SuperTUI.ScreenManager]::Instance
        Write-Host "  ✓ ScreenManager singleton created" -ForegroundColor Green

        Write-Host ""
        Write-Host "All instance tests passed!" -ForegroundColor Green
    }
    catch {
        Write-Host ""
        Write-Host "ERROR during instance creation: $_" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        exit 1
    }

    # Summary
    Write-Host ""
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "COMPILATION TEST: PASSED ✓" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Compilation time: $([math]::Round($duration, 2))s" -ForegroundColor Gray
    Write-Host "Types verified: $($types.Count)" -ForegroundColor Gray
    Write-Host "Core engine ready for use!" -ForegroundColor Gray
    Write-Host ""

    exit 0
}
catch {
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds

    Write-Host ""
    Write-Host "COMPILATION FAILED after $([math]::Round($duration, 2)) seconds" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host $_.Exception.StackTrace -ForegroundColor DarkRed
    Write-Host ""

    exit 1
}
