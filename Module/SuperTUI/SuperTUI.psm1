# SuperTUI PowerShell Module
# Fluent API for creating terminal-style desktop UIs

#region Module Variables

# Store compiled framework types
$script:FrameworkLoaded = $false
$script:WorkspaceBuilder = $null
$script:ServiceContainer = $null

#endregion

#region Framework Compilation

function Initialize-SuperTUIFramework {
    <#
    .SYNOPSIS
    Compiles and initializes the SuperTUI C# framework

    .DESCRIPTION
    Loads all C# source files and compiles them into memory.
    This must be called before using any SuperTUI functions.
    #>

    if ($script:FrameworkLoaded) {
        return
    }

    Write-Verbose "Loading SuperTUI framework..."

    # Add required assemblies
    Add-Type -AssemblyName PresentationFramework -ErrorAction Stop
    Add-Type -AssemblyName PresentationCore -ErrorAction Stop
    Add-Type -AssemblyName WindowsBase -ErrorAction Stop

    # Find the WPF directory (relative to module)
    $moduleRoot = $PSScriptRoot
    $wpfRoot = Join-Path (Split-Path (Split-Path $moduleRoot)) "WPF"

    if (-not (Test-Path $wpfRoot)) {
        throw "SuperTUI WPF framework not found at: $wpfRoot"
    }

    # Collect all C# source files
    $coreFiles = @(
        "Core/Interfaces/IWidget.cs"
        "Core/Interfaces/ILogger.cs"
        "Core/Interfaces/IThemeManager.cs"
        "Core/Interfaces/IConfigurationManager.cs"
        "Core/Interfaces/ISecurityManager.cs"
        "Core/Interfaces/IErrorHandler.cs"
        "Core/Interfaces/ILayoutEngine.cs"
        "Core/Interfaces/IServiceContainer.cs"
        "Core/Interfaces/IWorkspace.cs"
        "Core/Interfaces/IWorkspaceManager.cs"
        "Core/Infrastructure.cs"
        "Core/Extensions.cs"
        "Core/Layout/LayoutEngine.cs"
        "Core/Layout/GridLayoutEngine.cs"
        "Core/Layout/DockLayoutEngine.cs"
        "Core/Layout/StackLayoutEngine.cs"
        "Core/Components/WidgetBase.cs"
        "Core/Components/ScreenBase.cs"
        "Core/Components/EditableListControl.cs"
        "Core/Infrastructure/Workspace.cs"
        "Core/Infrastructure/WorkspaceManager.cs"
        "Core/Infrastructure/ShortcutManager.cs"
        "Core/Infrastructure/EventBus.cs"
        "Core/Infrastructure/ServiceContainer.cs"
        "Core/Infrastructure/Events.cs"
        "Core/Infrastructure/WorkspaceTemplate.cs"
        "Core/Infrastructure/HotReloadManager.cs"
        "Core/DI/ServiceRegistration.cs"
    )

    $widgetFiles = @(
        "Widgets/ClockWidget.cs"
        "Widgets/CounterWidget.cs"
        "Widgets/NotesWidget.cs"
        "Widgets/TaskSummaryWidget.cs"
        "Widgets/SystemMonitorWidget.cs"
        "Widgets/GitStatusWidget.cs"
        "Widgets/TodoWidget.cs"
        "Widgets/FileExplorerWidget.cs"
        "Widgets/TerminalWidget.cs"
        "Widgets/CommandPaletteWidget.cs"
    )

    # Combine and load sources
    $allSources = @()
    foreach ($file in ($coreFiles + $widgetFiles)) {
        $fullPath = Join-Path $wpfRoot $file
        if (Test-Path $fullPath) {
            $source = Get-Content $fullPath -Raw
            # Remove using statements except from first file
            if ($allSources.Count -gt 0) {
                $source = $source -replace '(?s)^using.*?(?=namespace)', ''
            }
            $allSources += $source
        } else {
            Write-Warning "Source file not found: $fullPath"
        }
    }

    $combinedSource = $allSources -join "`n`n"

    # Compile
    try {
        Add-Type -TypeDefinition $combinedSource -ReferencedAssemblies @(
            'PresentationFramework',
            'PresentationCore',
            'WindowsBase',
            'System.Xaml',
            'System.Management',
            'System.Management.Automation'
        ) -ErrorAction Stop

        $script:FrameworkLoaded = $true
        Write-Verbose "SuperTUI framework loaded successfully"
    } catch {
        throw "Failed to compile SuperTUI framework: $_"
    }
}

#endregion

#region Core Functions

function Initialize-SuperTUI {
    <#
    .SYNOPSIS
    Initializes the SuperTUI environment

    .DESCRIPTION
    Sets up the dependency injection container and initializes services.

    .PARAMETER ConfigPath
    Path to configuration file. Defaults to %LOCALAPPDATA%\SuperTUI\config.json

    .PARAMETER ThemesPath
    Path to themes directory

    .PARAMETER StatePath
    Path to state directory

    .EXAMPLE
    Initialize-SuperTUI

    .EXAMPLE
    Initialize-SuperTUI -ConfigPath "C:\MyConfig\supertui.json"
    #>
    [CmdletBinding()]
    param(
        [string]$ConfigPath,
        [string]$ThemesPath,
        [string]$StatePath,
        [string]$PluginsPath
    )

    # Ensure framework is loaded
    Initialize-SuperTUIFramework

    # Get service container
    $script:ServiceContainer = [SuperTUI.Core.ServiceContainer]::Instance

    # Configure services
    [SuperTUI.DI.ServiceRegistration]::ConfigureServices($script:ServiceContainer)

    # Initialize services with provided paths
    if (-not $ConfigPath) {
        $appData = [Environment]::GetFolderPath('LocalApplicationData')
        $superTUIDir = Join-Path $appData "SuperTUI"
        New-Item -ItemType Directory -Force -Path $superTUIDir | Out-Null
        $ConfigPath = Join-Path $superTUIDir "config.json"
        $ThemesPath = Join-Path $superTUIDir "Themes"
        $StatePath = Join-Path $superTUIDir "State"
        $PluginsPath = Join-Path $superTUIDir "Plugins"
    }

    [SuperTUI.DI.ServiceRegistration]::InitializeServices(
        $script:ServiceContainer,
        $ConfigPath,
        $ThemesPath,
        $StatePath,
        $PluginsPath
    )

    Write-Verbose "SuperTUI initialized at: $ConfigPath"
}

#endregion

#region Workspace Builder

class WorkspaceBuilder {
    [string]$Name
    [int]$Index
    [object]$Layout
    [System.Collections.ArrayList]$Widgets
    [hashtable]$LayoutParams

    WorkspaceBuilder([string]$name, [int]$index) {
        $this.Name = $name
        $this.Index = $index
        $this.Widgets = [System.Collections.ArrayList]::new()
        $this.LayoutParams = @{}
    }

    [WorkspaceBuilder] UseGridLayout([int]$rows, [int]$cols, [bool]$splitters) {
        $this.Layout = New-Object SuperTUI.Core.GridLayoutEngine($rows, $cols, $splitters)
        return $this
    }

    [WorkspaceBuilder] UseDockLayout() {
        $this.Layout = New-Object SuperTUI.Core.DockLayoutEngine
        return $this
    }

    [WorkspaceBuilder] UseStackLayout([string]$orientation) {
        $orient = if ($orientation -eq "Horizontal") {
            [System.Windows.Controls.Orientation]::Horizontal
        } else {
            [System.Windows.Controls.Orientation]::Vertical
        }
        $this.Layout = New-Object SuperTUI.Core.StackLayoutEngine($orient)
        return $this
    }

    [WorkspaceBuilder] AddWidget([object]$widget, [hashtable]$params) {
        $this.Widgets.Add(@{
            Widget = $widget
            Params = $params
        }) | Out-Null
        return $this
    }

    [object] Build() {
        if (-not $this.Layout) {
            throw "Layout must be specified before building workspace"
        }

        $workspace = New-Object SuperTUI.Core.Workspace($this.Name, $this.Index, $this.Layout)

        foreach ($item in $this.Widgets) {
            $layoutParams = New-Object SuperTUI.Core.LayoutParams

            # Set layout parameters from hashtable
            foreach ($key in $item.Params.Keys) {
                $layoutParams.$key = $item.Params[$key]
            }

            $workspace.AddWidget($item.Widget, $layoutParams)
        }

        return $workspace
    }
}

#endregion

#region Workspace Functions

function New-SuperTUIWorkspace {
    <#
    .SYNOPSIS
    Creates a new workspace builder

    .DESCRIPTION
    Starts building a new workspace with fluent API support.
    Use pipeline to chain layout and widget additions.

    .PARAMETER Name
    Name of the workspace

    .PARAMETER Index
    Index number for keyboard shortcuts (Ctrl+1, Ctrl+2, etc.)

    .EXAMPLE
    $workspace = New-SuperTUIWorkspace "Dashboard" -Index 1 |
        Use-GridLayout -Rows 2 -Columns 2 |
        Add-ClockWidget -Row 0 -Column 0 |
        Add-CounterWidget -Row 0 -Column 1
    #>
    [CmdletBinding()]
    [OutputType([WorkspaceBuilder])]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Name,

        [Parameter(Position = 1)]
        [int]$Index = 1
    )

    Initialize-SuperTUIFramework
    return [WorkspaceBuilder]::new($Name, $Index)
}

New-Alias -Name "New-Workspace" -Value "New-SuperTUIWorkspace" -Force

#endregion

#region Layout Functions

function Use-GridLayout {
    <#
    .SYNOPSIS
    Configures a grid layout for the workspace

    .PARAMETER InputObject
    Workspace builder from pipeline

    .PARAMETER Rows
    Number of rows

    .PARAMETER Columns
    Number of columns

    .PARAMETER Splitters
    Enable resizable splitters
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [Parameter(Mandatory)]
        [int]$Rows,

        [Parameter(Mandatory)]
        [Alias("Cols")]
        [int]$Columns,

        [switch]$Splitters
    )

    process {
        $InputObject.UseGridLayout($Rows, $Columns, $Splitters.IsPresent)
        return $InputObject
    }
}

function Use-DockLayout {
    <#
    .SYNOPSIS
    Configures a dock layout for the workspace
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject
    )

    process {
        $InputObject.UseDockLayout()
        return $InputObject
    }
}

function Use-StackLayout {
    <#
    .SYNOPSIS
    Configures a stack layout for the workspace

    .PARAMETER Orientation
    Stack orientation: Vertical or Horizontal
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [ValidateSet("Vertical", "Horizontal")]
        [string]$Orientation = "Vertical"
    )

    process {
        $InputObject.UseStackLayout($Orientation)
        return $InputObject
    }
}

#endregion

#region Widget Functions

function Add-ClockWidget {
    <#
    .SYNOPSIS
    Adds a clock widget to the workspace
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        $widget = New-Object SuperTUI.Widgets.ClockWidget
        $widget.WidgetName = "Clock"
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-CounterWidget {
    <#
    .SYNOPSIS
    Adds a counter widget to the workspace
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [string]$Name = "Counter",
        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        $widget = New-Object SuperTUI.Widgets.CounterWidget
        $widget.WidgetName = $Name
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-NotesWidget {
    <#
    .SYNOPSIS
    Adds a notes widget to the workspace
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [string]$Name = "Notes",
        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        $widget = New-Object SuperTUI.Widgets.NotesWidget
        $widget.WidgetName = $Name
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-TaskSummaryWidget {
    <#
    .SYNOPSIS
    Adds a task summary widget to the workspace
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [string]$Name = "Tasks",
        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        $widget = New-Object SuperTUI.Widgets.TaskSummaryWidget
        $widget.WidgetName = $Name
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-SystemMonitorWidget {
    <#
    .SYNOPSIS
    Adds a system monitor widget to the workspace

    .DESCRIPTION
    Displays real-time CPU, RAM, and Network statistics. Updates every second.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        $widget = New-Object SuperTUI.Widgets.SystemMonitorWidget
        $widget.WidgetName = "System Monitor"
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-GitStatusWidget {
    <#
    .SYNOPSIS
    Adds a Git repository status widget to the workspace

    .DESCRIPTION
    Displays branch, commit, and file status for a Git repository. Updates every 5 seconds.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [string]$RepositoryPath,
        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        if ($RepositoryPath) {
            $widget = New-Object SuperTUI.Widgets.GitStatusWidget($RepositoryPath)
        } else {
            $widget = New-Object SuperTUI.Widgets.GitStatusWidget
        }
        $widget.WidgetName = "Git Status"
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-TodoWidget {
    <#
    .SYNOPSIS
    Adds a Todo list widget to the workspace

    .DESCRIPTION
    Interactive todo list with add, edit, delete, and completion toggle.
    Press Space to toggle completion. Data persists to file.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [string]$DataFile,
        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        if ($DataFile) {
            $widget = New-Object SuperTUI.Widgets.TodoWidget($DataFile)
        } else {
            $widget = New-Object SuperTUI.Widgets.TodoWidget
        }
        $widget.WidgetName = "Todo List"
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-FileExplorerWidget {
    <#
    .SYNOPSIS
    Adds a file explorer widget to the workspace

    .DESCRIPTION
    Navigate directories and open files. Enter to open, Backspace to go up, F5 to refresh.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [string]$InitialPath,
        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        if ($InitialPath) {
            $widget = New-Object SuperTUI.Widgets.FileExplorerWidget($InitialPath)
        } else {
            $widget = New-Object SuperTUI.Widgets.FileExplorerWidget
        }
        $widget.WidgetName = "File Explorer"
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-TerminalWidget {
    <#
    .SYNOPSIS
    Adds an embedded PowerShell terminal widget to the workspace

    .DESCRIPTION
    Full PowerShell terminal with persistent runspace, command history, and output streams.
    Up/Down for history navigation.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        $widget = New-Object SuperTUI.Widgets.TerminalWidget
        $widget.WidgetName = "Terminal"
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-CommandPaletteWidget {
    <#
    .SYNOPSIS
    Adds a command palette widget to the workspace

    .DESCRIPTION
    Fuzzy search for commands and actions. Up/Down to navigate, Enter to execute.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        $widget = New-Object SuperTUI.Widgets.CommandPaletteWidget
        $widget.WidgetName = "Command Palette"
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-ShortcutHelpWidget {
    <#
    .SYNOPSIS
    Adds a keyboard shortcut help widget to the workspace

    .DESCRIPTION
    Displays all registered keyboard shortcuts with search functionality.
    Press F5 to refresh, Escape to clear search.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        $widget = New-Object SuperTUI.Widgets.ShortcutHelpWidget
        $widget.WidgetName = "Keyboard Shortcuts"
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

function Add-SettingsWidget {
    <#
    .SYNOPSIS
    Adds a settings/configuration widget to the workspace

    .DESCRIPTION
    Displays all configuration options grouped by category.
    Allows editing and saving settings. Ctrl+S to save, F5 to refresh.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, Mandatory)]
        [WorkspaceBuilder]$InputObject,

        [int]$Row,
        [int]$Column,
        [int]$RowSpan = 1,
        [int]$ColumnSpan = 1,
        [ValidateSet("Top", "Bottom", "Left", "Right")]
        [string]$Dock,
        [double]$Width,
        [double]$Height
    )

    process {
        $widget = New-Object SuperTUI.Widgets.SettingsWidget
        $widget.WidgetName = "Settings"
        $widget.Initialize()

        $params = @{}
        if ($PSBoundParameters.ContainsKey('Row')) { $params.Row = $Row }
        if ($PSBoundParameters.ContainsKey('Column')) { $params.Column = $Column }
        if ($PSBoundParameters.ContainsKey('RowSpan')) { $params.RowSpan = $RowSpan }
        if ($PSBoundParameters.ContainsKey('ColumnSpan')) { $params.ColumnSpan = $ColumnSpan }
        if ($PSBoundParameters.ContainsKey('Dock')) {
            $params.Dock = [System.Windows.Controls.Dock]::$Dock
        }
        if ($PSBoundParameters.ContainsKey('Width')) { $params.Width = $Width }
        if ($PSBoundParameters.ContainsKey('Height')) { $params.Height = $Height }

        $InputObject.AddWidget($widget, $params)
        return $InputObject
    }
}

#endregion

#region Workspace Template Functions

function Get-SuperTUITemplate {
    <#
    .SYNOPSIS
    Gets workspace templates

    .DESCRIPTION
    Lists available templates or gets a specific template by name.
    #>
    [CmdletBinding()]
    param(
        [string]$Name,
        [switch]$ListAvailable
    )

    $templateManager = [SuperTUI.Core.Infrastructure.WorkspaceTemplateManager]::Instance

    if ($ListAvailable) {
        return $templateManager.ListTemplates()
    }
    elseif ($Name) {
        return $templateManager.LoadTemplate($Name)
    }
    else {
        Write-Host "Available templates:"
        $templates = $templateManager.ListTemplates()
        foreach ($template in $templates) {
            Write-Host "  $($template.Name) - $($template.Description)"
        }
    }
}

function Save-SuperTUITemplate {
    <#
    .SYNOPSIS
    Saves a workspace template

    .DESCRIPTION
    Creates a new workspace template definition.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [string]$Description = "",
        [string]$Author = $env:USERNAME,

        [Parameter(Mandatory)]
        [ValidateSet("Grid", "Dock", "Stack")]
        [string]$LayoutType,

        [Parameter(Mandatory)]
        [hashtable]$LayoutConfig,

        [Parameter(Mandatory)]
        [array]$Widgets
    )

    $template = New-Object SuperTUI.Core.Infrastructure.WorkspaceTemplate
    $template.Name = $Name
    $template.Description = $Description
    $template.Author = $Author
    $template.LayoutType = [SuperTUI.Core.Infrastructure.LayoutType]::$LayoutType

    # Convert layout config
    foreach ($key in $LayoutConfig.Keys) {
        $template.LayoutConfig.Add($key, $LayoutConfig[$key])
    }

    # Convert widgets
    foreach ($widget in $Widgets) {
        $widgetDef = New-Object SuperTUI.Core.Infrastructure.WidgetDefinition
        $widgetDef.WidgetType = $widget.WidgetType
        $widgetDef.Name = $widget.Name

        if ($widget.Parameters) {
            foreach ($key in $widget.Parameters.Keys) {
                $widgetDef.Parameters.Add($key, $widget.Parameters[$key])
            }
        }

        if ($widget.LayoutParameters) {
            foreach ($key in $widget.LayoutParameters.Keys) {
                $widgetDef.LayoutParameters.Add($key, $widget.LayoutParameters[$key])
            }
        }

        $template.Widgets.Add($widgetDef)
    }

    $templateManager = [SuperTUI.Core.Infrastructure.WorkspaceTemplateManager]::Instance
    $templateManager.SaveTemplate($template)

    Write-Host "Template '$Name' saved successfully" -ForegroundColor Green
}

function Remove-SuperTUITemplate {
    <#
    .SYNOPSIS
    Deletes a workspace template

    .DESCRIPTION
    Removes a template from the templates directory.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    $templateManager = [SuperTUI.Core.Infrastructure.WorkspaceTemplateManager]::Instance
    $templateManager.DeleteTemplate($Name)

    Write-Host "Template '$Name' deleted" -ForegroundColor Yellow
}

function Export-SuperTUITemplate {
    <#
    .SYNOPSIS
    Exports a template to a file

    .DESCRIPTION
    Exports a workspace template to a JSON file at the specified path.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$ExportPath
    )

    $templateManager = [SuperTUI.Core.Infrastructure.WorkspaceTemplateManager]::Instance
    $templateManager.ExportTemplate($Name, $ExportPath)

    Write-Host "Template exported to: $ExportPath" -ForegroundColor Green
}

function Import-SuperTUITemplate {
    <#
    .SYNOPSIS
    Imports a template from a file

    .DESCRIPTION
    Imports a workspace template from a JSON file.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ImportPath
    )

    $templateManager = [SuperTUI.Core.Infrastructure.WorkspaceTemplateManager]::Instance
    $template = $templateManager.ImportTemplate($ImportPath)

    Write-Host "Template '$($template.Name)' imported successfully" -ForegroundColor Green
    return $template
}

function Initialize-SuperTUIBuiltInTemplates {
    <#
    .SYNOPSIS
    Creates built-in templates

    .DESCRIPTION
    Creates default workspace templates (Developer, Productivity).
    #>
    [CmdletBinding()]
    param()

    $templateManager = [SuperTUI.Core.Infrastructure.WorkspaceTemplateManager]::Instance
    $templateManager.CreateBuiltInTemplates()

    Write-Host "Built-in templates created" -ForegroundColor Green
}

#endregion

#region Theme Functions

function Get-SuperTUITheme {
    <#
    .SYNOPSIS
    Gets the current theme or list of available themes
    #>
    [CmdletBinding()]
    param(
        [switch]$ListAvailable
    )

    Initialize-SuperTUIFramework

    $themeManager = $script:ServiceContainer.Resolve([SuperTUI.Infrastructure.IThemeManager])

    if ($ListAvailable) {
        return $themeManager.GetAvailableThemes()
    } else {
        return $themeManager.CurrentTheme
    }
}

function Set-SuperTUITheme {
    <#
    .SYNOPSIS
    Sets the active theme
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ThemeName
    )

    Initialize-SuperTUIFramework

    $themeManager = $script:ServiceContainer.Resolve([SuperTUI.Infrastructure.IThemeManager])
    $themeManager.SetTheme($ThemeName)
}

#endregion

#region Configuration Functions

function Get-SuperTUIConfig {
    <#
    .SYNOPSIS
    Gets a configuration value
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Key,

        [object]$DefaultValue
    )

    Initialize-SuperTUIFramework

    $config = $script:ServiceContainer.Resolve([SuperTUI.Infrastructure.IConfigurationManager])

    if ($DefaultValue) {
        return $config.Get($Key, $DefaultValue)
    } else {
        return $config.Get($Key)
    }
}

function Set-SuperTUIConfig {
    <#
    .SYNOPSIS
    Sets a configuration value
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Key,

        [Parameter(Mandatory)]
        [object]$Value
    )

    Initialize-SuperTUIFramework

    $config = $script:ServiceContainer.Resolve([SuperTUI.Infrastructure.IConfigurationManager])
    $config.Set($Key, $Value)
}

#endregion

#region Utility Functions

function Get-SuperTUIStatistics {
    <#
    .SYNOPSIS
    Gets EventBus statistics
    #>
    [CmdletBinding()]
    param()

    Initialize-SuperTUIFramework

    $eventBus = [SuperTUI.Core.EventBus]::Instance
    $stats = $eventBus.GetStatistics()

    return [PSCustomObject]@{
        EventsPublished = $stats.Item1
        EventsDelivered = $stats.Item2
        TypedSubscribers = $stats.Item3
        NamedSubscribers = $stats.Item4
    }
}

function Enable-SuperTUIHotReload {
    <#
    .SYNOPSIS
    Enables hot reload for development

    .DESCRIPTION
    Watches source directories for changes and triggers reload events.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$WatchPaths
    )

    Initialize-SuperTUIFramework

    $hotReload = [SuperTUI.Core.Infrastructure.HotReloadManager]::Instance
    $hotReload.Enable($WatchPaths)

    Write-Host "Hot reload enabled for:" -ForegroundColor Green
    foreach ($path in $WatchPaths) {
        Write-Host "  $path" -ForegroundColor Gray
    }
}

function Disable-SuperTUIHotReload {
    <#
    .SYNOPSIS
    Disables hot reload
    #>
    [CmdletBinding()]
    param()

    Initialize-SuperTUIFramework

    $hotReload = [SuperTUI.Core.Infrastructure.HotReloadManager]::Instance
    $hotReload.Disable()

    Write-Host "Hot reload disabled" -ForegroundColor Yellow
}

function Get-SuperTUIHotReloadStats {
    <#
    .SYNOPSIS
    Gets hot reload statistics
    #>
    [CmdletBinding()]
    param()

    Initialize-SuperTUIFramework

    $hotReload = [SuperTUI.Core.Infrastructure.HotReloadManager]::Instance
    $stats = $hotReload.GetStatistics()

    return [PSCustomObject]@{
        IsEnabled = $stats.IsEnabled
        WatchedPaths = $stats.WatchedPaths
        PendingChanges = $stats.PendingChanges
    }
}

#endregion

# Export module members
Export-ModuleMember -Function * -Alias *
