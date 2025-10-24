# Script to add IThemeable support to widgets that don't have it
# Run this to systematically add theme support to all widgets

$widgetsToFix = @(
    @{
        File = "GitStatusWidget.cs"
        ClassName = "GitStatusWidget"
    },
    @{
        File = "FileExplorerWidget.cs"
        ClassName = "FileExplorerWidget"
    },
    @{
        File = "TerminalWidget.cs"
        ClassName = "TerminalWidget"
    },
    @{
        File = "TodoWidget.cs"
        ClassName = "TodoWidget"
    },
    @{
        File = "CommandPaletteWidget.cs"
        ClassName = "CommandPaletteWidget"
    }
)

$widgetsPath = Join-Path $PSScriptRoot "../Widgets"

foreach ($widget in $widgetsToFix) {
    $filePath = Join-Path $widgetsPath $widget.File
    $className = $widget.ClassName

    Write-Host "Processing $className..." -ForegroundColor Yellow

    if (!(Test-Path $filePath)) {
        Write-Host "  File not found: $filePath" -ForegroundColor Red
        continue
    }

    $content = Get-Content $filePath -Raw

    # Check if already implements IThemeable
    if ($content -match "class $className.*: WidgetBase, IThemeable") {
        Write-Host "  Already implements IThemeable" -ForegroundColor Green
        continue
    }

    # Add using statement if not present
    if ($content -notmatch "using SuperTUI\.Infrastructure;") {
        $content = $content -replace "(using SuperTUI\.Core;)", "`$1`nusing SuperTUI.Infrastructure;"
        Write-Host "  Added using SuperTUI.Infrastructure" -ForegroundColor Cyan
    }

    # Add IThemeable to class declaration
    $content = $content -replace "(class $className\s*:\s*WidgetBase)", "`$1, IThemeable"
    Write-Host "  Added IThemeable to class declaration" -ForegroundColor Cyan

    # Check if ApplyTheme method exists
    if ($content -notmatch "public void ApplyTheme\(\)") {
        # Find the last method (usually OnDeactivated or OnDispose)
        # Add ApplyTheme method before the closing brace

        $applyThemeMethod = @"

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            // TODO: Update UI element colors based on theme
            // Example:
            // if (titleText != null)
            //     titleText.Foreground = new SolidColorBrush(theme.Foreground);
            // if (containerBorder != null)
            //     containerBorder.Background = new SolidColorBrush(theme.BackgroundSecondary);

            Logger.Instance?.Warning("Theme", "$className.ApplyTheme() not fully implemented yet");
        }
"@

        # Insert before final closing brace
        $content = $content -replace "(\s+)\}\s*\}\s*$", "$applyThemeMethod`n`$1    }`n}"

        Write-Host "  Added ApplyTheme() stub method" -ForegroundColor Cyan
    }

    # Save the modified content
    Set-Content $filePath -Value $content -NoNewline

    Write-Host "  ✓ $className updated successfully" -ForegroundColor Green
}

Write-Host "`nDone! Theme support added to all widgets." -ForegroundColor Green
Write-Host "Note: ApplyTheme() methods are stubs - you need to implement the actual theme application logic." -ForegroundColor Yellow
