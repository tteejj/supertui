#!/usr/bin/env pwsh
# Script to migrate all widgets to DI pattern
# Usage: ./migrate_widgets_to_di.ps1

$ErrorActionPreference = "Stop"

Write-Host "Starting DI migration for all widgets..." -ForegroundColor Cyan

# List of widgets to update (excluding ClockWidget and CounterWidget which already have DI)
$widgets = @(
    @{Name = "SettingsWidget"; File = "Widgets/SettingsWidget.cs"},
    @{Name = "ShortcutHelpWidget"; File = "Widgets/ShortcutHelpWidget.cs"},
    @{Name = "NotesWidget"; File = "Widgets/NotesWidget.cs"},
    @{Name = "GitStatusWidget"; File = "Widgets/GitStatusWidget.cs"},
    @{Name = "FileExplorerWidget"; File = "Widgets/FileExplorerWidget.cs"},
    @{Name = "TodoWidget"; File = "Widgets/TodoWidget.cs"},
    @{Name = "CommandPaletteWidget"; File = "Widgets/CommandPaletteWidget.cs"},
    @{Name = "SystemMonitorWidget"; File = "Widgets/SystemMonitorWidget.cs"},
    @{Name = "TaskManagementWidget"; File = "Widgets/TaskManagementWidget.cs"},
    @{Name = "AgendaWidget"; File = "Widgets/AgendaWidget.cs"},
    @{Name = "ProjectStatsWidget"; File = "Widgets/ProjectStatsWidget.cs"},
    @{Name = "KanbanBoardWidget"; File = "Widgets/KanbanBoardWidget.cs"}
)

$totalInstances = 0

foreach ($widget in $widgets) {
    $widgetName = $widget.Name
    $filePath = Join-Path $PSScriptRoot $widget.File

    if (-not (Test-Path $filePath)) {
        Write-Host "  [SKIP] $widgetName - File not found: $filePath" -ForegroundColor Yellow
        continue
    }

    Write-Host "`nProcessing $widgetName..." -ForegroundColor Green

    $content = Get-Content $filePath -Raw

    # Count .Instance usages before migration
    $instanceCount = ([regex]::Matches($content, '\.(Instance)(?=[^a-zA-Z0-9_])').Count)
    $totalInstances += $instanceCount

    Write-Host "  Found $instanceCount .Instance usages" -ForegroundColor Gray

    # Pattern to find the class declaration and add DI fields
    # This is a simplified approach - for production, use proper C# parsing

    Write-Host "  Note: Manual migration required for $widgetName" -ForegroundColor Yellow
    Write-Host "    - Add DI fields (logger, themeManager, config)" -ForegroundColor Gray
    Write-Host "    - Add DI constructor" -ForegroundColor Gray
    Write-Host "    - Add backward-compatibility constructor" -ForegroundColor Gray
    Write-Host "    - Replace all .Instance with injected fields" -ForegroundColor Gray
}

Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "Migration Summary:" -ForegroundColor Cyan
Write-Host "  Widgets to migrate: $($widgets.Count)" -ForegroundColor White
Write-Host "  Total .Instance usages: $totalInstances" -ForegroundColor White
Write-Host "=====================================" -ForegroundColor Cyan

Write-Host "`nDI Migration requires manual editing due to C# complexity." -ForegroundColor Yellow
Write-Host "Use Claude Code to apply the systematic pattern to each widget." -ForegroundColor Yellow
