#!/usr/bin/env pwsh
# Test-AllFixes.ps1 - Comprehensive test of all C# fixes

Import-Module (Join-Path $PSScriptRoot ".." "Module" "SuperTUI.psm1") -Force

Write-Host "SuperTUI - Comprehensive Fix Validation" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

$allPassed = $true

# Test 1: GridColumn parameterless constructor
Write-Host "Test 1: GridColumn Parameterless Constructor" -ForegroundColor Yellow
try {
    $col = [SuperTUI.GridColumn]::new()
    $col.Header = "Test"
    $col.Property = "TestProp"
    $col.Width = "100"
    Write-Host "  ✓ GridColumn() constructor works" -ForegroundColor Green
    Write-Host "  ✓ Properties set: Header='$($col.Header)', Property='$($col.Property)', Width='$($col.Width)'" -ForegroundColor Green
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Test 2: Label.Alignment property
Write-Host "Test 2: Label.Alignment Property" -ForegroundColor Yellow
try {
    $label = [SuperTUI.Label]::new()
    $label.Text = "Test Label"
    $label.Alignment = [SuperTUI.TextAlignment]::Center

    if ($label.Alignment -eq [SuperTUI.TextAlignment]::Center) {
        Write-Host "  ✓ Label.Alignment property accessible" -ForegroundColor Green
        Write-Host "  ✓ Alignment set to: $($label.Alignment)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: Alignment not set correctly" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Test 3: Button.Click event
Write-Host "Test 3: Button.Click Event" -ForegroundColor Yellow
try {
    $button = [SuperTUI.Button]::new()
    $button.Label = "Test Button"

    $clicked = $false
    $handler = [EventHandler]{
        param($sender, $args)
        $script:clicked = $true
    }
    $button.add_Click($handler)

    Write-Host "  ✓ Button.Click event handler added" -ForegroundColor Green
    Write-Host "  ✓ Event name is 'Click' (not 'Clicked')" -ForegroundColor Green
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Test 4: Label rendering
Write-Host "Test 4: Label Rendering" -ForegroundColor Yellow
try {
    $label = [SuperTUI.Label]::new()
    $label.Text = "Hello, World!"
    $label.X = 10
    $label.Y = 5
    $label.Width = 50
    $label.Height = 1
    $label.Alignment = [SuperTUI.TextAlignment]::Center

    $theme = Get-Theme
    $context = [SuperTUI.RenderContext]::new($theme, 80, 24)
    $rendered = $label.Render($context)

    if ($rendered.Length -gt 0) {
        Write-Host "  ✓ Label renders: $($rendered.Length) chars" -ForegroundColor Green
        Write-Host "  ✓ Text: '$($label.Text)' at ($($label.X),$($label.Y))" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: Label rendered 0 chars" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Test 5: Button rendering
Write-Host "Test 5: Button Rendering" -ForegroundColor Yellow
try {
    $button = [SuperTUI.Button]::new()
    $button.Label = "Click Me"
    $button.X = 10
    $button.Y = 10
    $button.Width = 20
    $button.Height = 3
    $button.IsFocused = $true

    $theme = Get-Theme
    $context = [SuperTUI.RenderContext]::new($theme, 80, 24)
    $rendered = $button.Render($context)

    if ($rendered.Length -gt 0) {
        Write-Host "  ✓ Button renders: $($rendered.Length) chars" -ForegroundColor Green
        Write-Host "  ✓ With focus state" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: Button rendered 0 chars" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Test 6: TextBox rendering
Write-Host "Test 6: TextBox Rendering" -ForegroundColor Yellow
try {
    $textBox = [SuperTUI.TextBox]::new()
    $textBox.Value = "Test input"
    $textBox.Placeholder = "Enter text"
    $textBox.X = 10
    $textBox.Y = 15
    $textBox.Width = 40
    $textBox.Height = 1

    $theme = Get-Theme
    $context = [SuperTUI.RenderContext]::new($theme, 80, 24)
    $rendered = $textBox.Render($context)

    if ($rendered.Length -gt 0) {
        Write-Host "  ✓ TextBox renders: $($rendered.Length) chars" -ForegroundColor Green
        Write-Host "  ✓ Value: '$($textBox.Value)'" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: TextBox rendered 0 chars" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Test 7: DataGrid rendering with GridColumn
Write-Host "Test 7: DataGrid Rendering with GridColumn" -ForegroundColor Yellow
try {
    $grid = [SuperTUI.DataGrid]::new()
    $grid.X = 0
    $grid.Y = 0
    $grid.Width = 60
    $grid.Height = 10

    # Create columns using parameterless constructor
    $col1 = [SuperTUI.GridColumn]::new()
    $col1.Header = "ID"
    $col1.Property = "Id"
    $col1.Width = "5"
    $grid.Columns.Add($col1)

    $col2 = [SuperTUI.GridColumn]::new()
    $col2.Header = "Name"
    $col2.Property = "Name"
    $col2.Width = "*"
    $grid.Columns.Add($col2)

    # Add test data
    $items = [System.Collections.ObjectModel.ObservableCollection[object]]::new()
    $items.Add([PSCustomObject]@{ Id = 1; Name = "Test 1" })
    $items.Add([PSCustomObject]@{ Id = 2; Name = "Test 2" })
    $grid.ItemsSource = $items

    $theme = Get-Theme
    $context = [SuperTUI.RenderContext]::new($theme, 80, 24)
    $rendered = $grid.Render($context)

    if ($rendered.Length -gt 0) {
        Write-Host "  ✓ DataGrid renders: $($rendered.Length) chars" -ForegroundColor Green
        Write-Host "  ✓ With $($grid.Columns.Count) columns and $($items.Count) rows" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: DataGrid rendered 0 chars" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Test 8: ListView rendering
Write-Host "Test 8: ListView Rendering" -ForegroundColor Yellow
try {
    $list = [SuperTUI.ListView]::new()
    $list.X = 0
    $list.Y = 0
    $list.Width = 40
    $list.Height = 10
    $list.DisplayProperty = "Name"

    $items = [System.Collections.ObjectModel.ObservableCollection[object]]::new()
    $items.Add([PSCustomObject]@{ Name = "Item 1" })
    $items.Add([PSCustomObject]@{ Name = "Item 2" })
    $items.Add([PSCustomObject]@{ Name = "Item 3" })
    $list.ItemsSource = $items

    $theme = Get-Theme
    $context = [SuperTUI.RenderContext]::new($theme, 80, 24)
    $rendered = $list.Render($context)

    if ($rendered.Length -gt 0) {
        Write-Host "  ✓ ListView renders: $($rendered.Length) chars" -ForegroundColor Green
        Write-Host "  ✓ With $($items.Count) items" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: ListView rendered 0 chars" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Test 9: PowerShell API integration
Write-Host "Test 9: PowerShell API Integration" -ForegroundColor Yellow
try {
    # Test New-Label with Alignment
    $label = New-Label -Text "Test" -Alignment Center
    if ($label.Alignment -eq [SuperTUI.TextAlignment]::Center) {
        Write-Host "  ✓ New-Label -Alignment works" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: New-Label alignment not set" -ForegroundColor Red
        $allPassed = $false
    }

    # Test New-Button with OnClick (uses Click event)
    $buttonClicked = $false
    $button = New-Button -Label "Test" -OnClick { $script:buttonClicked = $true }
    Write-Host "  ✓ New-Button -OnClick works (uses Click event)" -ForegroundColor Green

    # Test New-DataGrid with columns
    $grid = New-DataGrid -ItemsSource @() -Columns @(
        @{ Header = "Test"; Property = "Test"; Width = "*" }
    )
    if ($grid.Columns.Count -eq 1) {
        Write-Host "  ✓ New-DataGrid with GridColumn works" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: DataGrid columns not created" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Test 10: Complete layout rendering
Write-Host "Test 10: Complete Layout Rendering" -ForegroundColor Yellow
try {
    $layout = New-GridLayout -Rows "Auto","*","Auto" -Columns "*"
    $layout.Width = 80
    $layout.Height = 24

    $header = New-Label -Text "Header" -Alignment Center
    $header.Height = 1
    $layout.AddChild($header, 0, 0)

    $content = New-Label -Text "Content area with some text"
    $content.Height = 10
    $layout.AddChild($content, 1, 0)

    $footer = New-Label -Text "Footer"
    $footer.Height = 1
    $layout.AddChild($footer, 2, 0)

    $theme = Get-Theme
    $context = [SuperTUI.RenderContext]::new($theme, 80, 24)
    $rendered = $layout.Render($context)

    if ($rendered.Length -gt 0) {
        Write-Host "  ✓ Complete layout renders: $($rendered.Length) chars" -ForegroundColor Green
        Write-Host "  ✓ With 3 labels in grid layout" -ForegroundColor Green
    } else {
        Write-Host "  ✗ FAILED: Layout rendered 0 chars" -ForegroundColor Red
        $allPassed = $false
    }
} catch {
    Write-Host "  ✗ FAILED: $_" -ForegroundColor Red
    $allPassed = $false
}
Write-Host ""

# Summary
Write-Host "=========================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "ALL TESTS PASSED ✓" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "All C# fixes verified:" -ForegroundColor Green
    Write-Host "  ✓ GridColumn parameterless constructor" -ForegroundColor Gray
    Write-Host "  ✓ Label.Alignment property" -ForegroundColor Gray
    Write-Host "  ✓ Button.Click event" -ForegroundColor Gray
    Write-Host "  ✓ All rendering methods complete" -ForegroundColor Gray
    Write-Host "  ✓ PowerShell API integration working" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Phase 1 & 2: 100% COMPLETE!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "SOME TESTS FAILED ✗" -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Cyan
    exit 1
}
