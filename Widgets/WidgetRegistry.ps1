# Widget Registry and Discovery System
# Manages widget types, discovery, and instantiation

. (Join-Path $PSScriptRoot "BaseWidget.ps1")

class WidgetRegistry {
    static [hashtable]$RegisteredTypes = @{}
    static [string[]]$SearchPaths = @()
    static [bool]$Initialized = $false

    # Initialize search paths
    static [void] Initialize() {
        if ([WidgetRegistry]::Initialized) { return }

        $scriptRoot = Split-Path -Parent $PSCommandPath
        $homeDir = [Environment]::GetFolderPath('UserProfile')

        [WidgetRegistry]::SearchPaths = @(
            (Join-Path $scriptRoot "Core")                    # Built-in widgets
            (Join-Path $homeDir ".supertui" "widgets")        # User custom widgets
            (Join-Path $scriptRoot "Community")               # Community widgets
        )

        [WidgetRegistry]::Initialized = $true
    }

    # Discover and load all widgets from search paths
    static [void] DiscoverWidgets() {
        [WidgetRegistry]::Initialize()

        Write-Verbose "Discovering widgets from search paths..."

        foreach ($path in [WidgetRegistry]::SearchPaths) {
            if (Test-Path $path) {
                Write-Verbose "  Scanning: $path"

                $widgetFiles = Get-ChildItem -Path $path -Filter "*Widget.ps1" -File

                foreach ($file in $widgetFiles) {
                    try {
                        [WidgetRegistry]::RegisterWidgetFromFile($file.FullName)
                    } catch {
                        Write-Warning "Failed to register widget from $($file.Name): $_"
                    }
                }
            }
        }

        $count = [WidgetRegistry]::RegisteredTypes.Count
        Write-Verbose "Widget discovery complete. Registered: $count widgets"
    }

    # Register widget metadata from file (assumes widget is already loaded)
    static [void] RegisterWidgetFromFile([string]$path) {
        # Extract widget class names from file
        $content = Get-Content $path -Raw

        # Find all class definitions that inherit from DashboardWidget
        $matches = [regex]::Matches($content, 'class\s+(\w+Widget)\s*:\s*(DashboardWidget|ScrollableWidget|StaticWidget)')

        foreach ($match in $matches) {
            $widgetType = $match.Groups[1].Value

            # Skip base classes
            if ($widgetType -in @('DashboardWidget', 'ScrollableWidget', 'StaticWidget')) {
                continue
            }

            # Get metadata if available
            $metadata = $null
            try {
                $metadata = Invoke-Expression "[$widgetType]::GetMetadata()"
            } catch {
                $metadata = @{
                    Name = $widgetType
                    Description = "No description"
                    Author = "Unknown"
                    Version = "1.0.0"
                }
            }

            # Register the widget type
            [WidgetRegistry]::RegisteredTypes[$widgetType] = @{
                Path = $path
                Type = $widgetType
                Metadata = $metadata
                LoadedAt = Get-Date
            }

            Write-Verbose "  Registered: $widgetType"
        }
    }

    # Load a single widget file (for hot-reload scenarios)
    static [void] LoadWidget([string]$path) {
        # Dot-source the widget file
        # NOTE: Due to PowerShell class scoping, this may not make types available globally
        # For production use, widgets should be loaded in the main script scope
        . $path

        # Now register it
        [WidgetRegistry]::RegisterWidgetFromFile($path)
    }

    # Create widget instance by type name
    static [object] CreateWidget([string]$typeName, [hashtable]$config) {
        if (-not [WidgetRegistry]::RegisteredTypes.ContainsKey($typeName)) {
            throw "Widget type '$typeName' not registered. Available types: $([WidgetRegistry]::GetAvailableWidgets() -join ', ')"
        }

        try {
            # Use a script block to create instance (works around PowerShell class scope issues)
            # New-Object doesn't work from static methods due to type visibility
            $scriptBlock = [scriptblock]::Create("[$typeName]::new(`$args[0])")
            $widget = $scriptBlock.Invoke($config)

            return $widget
        } catch {
            throw "Failed to create widget '$typeName': $_"
        }
    }

    # Reload a specific widget type (for development)
    static [void] ReloadWidget([string]$typeName) {
        if ([WidgetRegistry]::RegisteredTypes.ContainsKey($typeName)) {
            $info = [WidgetRegistry]::RegisteredTypes[$typeName]
            Write-Verbose "Reloading widget: $typeName from $($info.Path)"

            [WidgetRegistry]::LoadWidget($info.Path)

            Write-Host "Widget reloaded: $typeName" -ForegroundColor Green
        } else {
            Write-Warning "Widget type '$typeName' not found in registry"
        }
    }

    # Get list of available widget types
    static [string[]] GetAvailableWidgets() {
        return [WidgetRegistry]::RegisteredTypes.Keys | Sort-Object
    }

    # Get widget metadata
    static [hashtable] GetWidgetInfo([string]$typeName) {
        if ([WidgetRegistry]::RegisteredTypes.ContainsKey($typeName)) {
            return [WidgetRegistry]::RegisteredTypes[$typeName]
        }
        return $null
    }

    # List all registered widgets with details
    static [void] ListWidgets() {
        Write-Host "`nRegistered Widgets:" -ForegroundColor Cyan
        Write-Host ("═" * 80) -ForegroundColor Gray

        foreach ($typeName in ([WidgetRegistry]::GetAvailableWidgets())) {
            $info = [WidgetRegistry]::GetWidgetInfo($typeName)
            $meta = $info.Metadata

            Write-Host "`n$typeName" -ForegroundColor Yellow
            Write-Host "  Name:        $($meta.Name)" -ForegroundColor Gray
            Write-Host "  Description: $($meta.Description)" -ForegroundColor Gray
            Write-Host "  Author:      $($meta.Author)" -ForegroundColor Gray
            Write-Host "  Version:     $($meta.Version)" -ForegroundColor Gray
            Write-Host "  Path:        $($info.Path)" -ForegroundColor DarkGray
        }

        Write-Host "`n$([WidgetRegistry]::RegisteredTypes.Count) widgets registered`n" -ForegroundColor Green
    }
}

# Helper function for scripts
function Get-AvailableWidgets {
    <#
    .SYNOPSIS
    Gets list of available widget types

    .EXAMPLE
    Get-AvailableWidgets
    #>
    return [WidgetRegistry]::GetAvailableWidgets()
}

function New-Widget {
    <#
    .SYNOPSIS
    Creates a widget instance

    .PARAMETER Type
    Widget type name

    .PARAMETER Config
    Widget configuration hashtable

    .EXAMPLE
    $widget = New-Widget -Type "MenuWidget" -Config @{ id = "menu1" }
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Type,

        [hashtable]$Config = @{}
    )

    return [WidgetRegistry]::CreateWidget($Type, $Config)
}
