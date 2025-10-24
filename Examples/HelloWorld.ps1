#!/usr/bin/env pwsh
# HelloWorld.ps1 - Simple SuperTUI example

# Compile the core engine
$corePath = Join-Path $PSScriptRoot ".." "Core" "SuperTUI.Core.cs"
$csharpCode = Get-Content $corePath -Raw
Add-Type -TypeDefinition $csharpCode -Language CSharp

Write-Host "SuperTUI Hello World Example" -ForegroundColor Cyan
Write-Host ""

# Create a simple concrete screen class
class HelloScreen : SuperTUI.Screen {
    HelloScreen() : base() {
        $this.Title = "Hello World"
        $this.Width = 80
        $this.Height = 24
    }
}

# Create the screen instance
$screen = [HelloScreen]::new()

# Create grid layout
$layout = [SuperTUI.GridLayout]::new()
$layout.Rows.Add([SuperTUI.RowDefinition]::new("Auto"))
$layout.Rows.Add([SuperTUI.RowDefinition]::new("*"))
$layout.Rows.Add([SuperTUI.RowDefinition]::new("Auto"))
$layout.Columns.Add([SuperTUI.ColumnDefinition]::new("*"))
$layout.Width = 80
$layout.Height = 24

# Create header label
$header = [SuperTUI.Label]::new()
$header.Text = "Welcome to SuperTUI!"
$header.Style = "Title"
$header.Height = 1
$layout.AddChild($header, 0, 0)

# Create content label
$content = [SuperTUI.Label]::new()
$content.Text = "This is a simple example demonstrating the SuperTUI framework.`nPress Ctrl+C to exit."
$content.Height = 10
$layout.AddChild($content, 1, 0)

# Create footer
$footer = [SuperTUI.Label]::new()
$footer.Text = "Press any key to continue..."
$footer.Style = "Subtitle"
$footer.Height = 1
$layout.AddChild($footer, 2, 0)

# Add layout to screen
$screen.Children.Add($layout)

# Create render context
$theme = [SuperTUI.Theme]::Default
$context = [SuperTUI.RenderContext]::new($theme, 80, 24)

# Render the screen
Write-Host "Rendering screen..." -ForegroundColor Yellow
$output = $screen.Render($context)

Write-Host "Rendered output:" -ForegroundColor Green
Write-Host $output

Write-Host ""
Write-Host "Screen structure:" -ForegroundColor Yellow
Write-Host "  Screen: $($screen.Title)"
Write-Host "  Children: $($screen.Children.Count)"
Write-Host "  Layout: GridLayout with $($layout.Rows.Count) rows x $($layout.Columns.Count) columns"
Write-Host "  Components: $($layout._children.Count) elements" -ErrorAction SilentlyContinue
Write-Host ""

Write-Host "Core engine is working!" -ForegroundColor Green
