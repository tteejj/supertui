# SuperTUI.psm1 - PowerShell API Module for SuperTUI Framework
# Version: 0.1.0
# Author: SuperTUI Project
# Description: Declarative TUI framework with .NET core and PowerShell API

#Requires -Version 5.1

using namespace System
using namespace System.Collections
using namespace System.Collections.Generic
using namespace System.Collections.ObjectModel

# Module-level variables
$script:SuperTUILoaded = $false
$script:SuperTUICorePath = $null

#region Core Compilation

<#
.SYNOPSIS
Compiles the SuperTUI C# core engine.

.DESCRIPTION
Loads and compiles SuperTUI.Core.cs via Add-Type. This is called automatically
on module import, but can be called manually if needed.

.EXAMPLE
Initialize-SuperTUICore
#>
function Initialize-SuperTUICore {
    [CmdletBinding()]
    param()

    if ($script:SuperTUILoaded) {
        Write-Verbose "SuperTUI core already loaded"
        return
    }

    # Find the core file
    $modulePath = $PSScriptRoot
    $corePath = Join-Path (Split-Path $modulePath -Parent) "Core" "SuperTUI.Core.cs"

    if (-not (Test-Path $corePath)) {
        throw "SuperTUI.Core.cs not found at: $corePath"
    }

    Write-Verbose "Loading SuperTUI core from: $corePath"
    $script:SuperTUICorePath = $corePath

    # Read and compile the C# code
    try {
        $csharpCode = Get-Content $corePath -Raw -ErrorAction Stop
        $startTime = Get-Date

        Add-Type -TypeDefinition $csharpCode -Language CSharp -ErrorAction Stop

        $duration = (Get-Date) - $startTime
        Write-Verbose "SuperTUI core compiled in $([math]::Round($duration.TotalSeconds, 2))s"

        $script:SuperTUILoaded = $true
    }
    catch {
        throw "Failed to compile SuperTUI core: $_"
    }
}

#endregion

#region Layout Builders

<#
.SYNOPSIS
Creates a new GridLayout with row and column definitions.

.DESCRIPTION
Creates a CSS Grid-inspired layout where children are positioned in cells.
Supports "Auto" (content-sized), "*" (fill), and pixel values.

.PARAMETER Rows
Array of row height definitions. Values: "Auto", "*", or pixel number.

.PARAMETER Columns
Array of column width definitions. Values: "Auto", "*", or pixel number.

.EXAMPLE
$layout = New-GridLayout -Rows "Auto","*","40" -Columns "200","*"
Creates a 3x2 grid with auto header, fill content, 40px footer, 200px sidebar, and fill main area.

.EXAMPLE
$layout = New-GridLayout -Rows "*" -Columns "*"
Creates a simple 1x1 fill grid.
#>
function New-GridLayout {
    [CmdletBinding()]
    [OutputType([SuperTUI.GridLayout])]
    param(
        [Parameter(Mandatory)]
        [string[]]$Rows,

        [Parameter(Mandatory)]
        [string[]]$Columns
    )

    $grid = [SuperTUI.GridLayout]::new()

    foreach ($row in $Rows) {
        $rowDef = [SuperTUI.RowDefinition]::new($row)
        $grid.Rows.Add($rowDef)
    }

    foreach ($col in $Columns) {
        $colDef = [SuperTUI.ColumnDefinition]::new($col)
        $grid.Columns.Add($colDef)
    }

    return $grid
}

<#
.SYNOPSIS
Creates a new StackLayout for horizontal or vertical arrangement.

.DESCRIPTION
Arranges children in a stack, either horizontally or vertically, with optional spacing.

.PARAMETER Orientation
Stack orientation: Horizontal or Vertical.

.PARAMETER Spacing
Space between children in characters/lines. Default: 0.

.EXAMPLE
$stack = New-StackLayout -Orientation Vertical -Spacing 1
$stack.AddChild($label1)
$stack.AddChild($label2)
#>
function New-StackLayout {
    [CmdletBinding()]
    [OutputType([SuperTUI.StackLayout])]
    param(
        [Parameter()]
        [SuperTUI.Orientation]$Orientation = [SuperTUI.Orientation]::Vertical,

        [Parameter()]
        [int]$Spacing = 0
    )

    $stack = [SuperTUI.StackLayout]::new()
    $stack.Orientation = $Orientation
    $stack.Spacing = $Spacing

    return $stack
}

<#
.SYNOPSIS
Creates a new DockLayout for edge-docking children.

.DESCRIPTION
Docks children to edges (Top, Bottom, Left, Right) with one Fill element.

.EXAMPLE
$dock = New-DockLayout
$dock.AddChild($toolbar, [SuperTUI.Dock]::Top)
$dock.AddChild($statusBar, [SuperTUI.Dock]::Bottom)
$dock.AddChild($content, [SuperTUI.Dock]::Fill)
#>
function New-DockLayout {
    [CmdletBinding()]
    [OutputType([SuperTUI.DockLayout])]
    param()

    return [SuperTUI.DockLayout]::new()
}

#endregion

#region Component Builders

<#
.SYNOPSIS
Creates a new Label component for text display.

.DESCRIPTION
Creates a text display component with optional styling and alignment.

.PARAMETER Text
The text to display.

.PARAMETER Style
Named style from theme (e.g., "Title", "Subtitle", "Success", "Error").

.PARAMETER Alignment
Text alignment: Left, Center, or Right. Default: Left.

.PARAMETER Height
Fixed height in lines. Default: 1.

.EXAMPLE
$label = New-Label -Text "Hello, World!" -Style "Title"

.EXAMPLE
$header = New-Label -Text "My Application" -Style "Title" -Alignment Center -Height 3
#>
function New-Label {
    [CmdletBinding()]
    [OutputType([SuperTUI.Label])]
    param(
        [Parameter(Mandatory)]
        [string]$Text,

        [Parameter()]
        [string]$Style = $null,

        [Parameter()]
        [SuperTUI.TextAlignment]$Alignment = [SuperTUI.TextAlignment]::Left,

        [Parameter()]
        [int]$Height = 1
    )

    $label = [SuperTUI.Label]::new()
    $label.Text = $Text
    $label.Alignment = $Alignment
    $label.Height = $Height

    if ($Style) {
        $label.Style = $Style
    }

    return $label
}

<#
.SYNOPSIS
Creates a new Button component.

.DESCRIPTION
Creates an interactive button with click event support.

.PARAMETER Label
Button text.

.PARAMETER OnClick
ScriptBlock to execute when clicked (Enter or Space).

.PARAMETER IsDefault
Whether this is the default button. Default: false.

.PARAMETER Width
Button width. Default: auto-sized to label + padding.

.PARAMETER Height
Button height. Default: 3 (with border).

.EXAMPLE
$button = New-Button -Label "OK" -OnClick { Write-Host "Clicked!" } -IsDefault $true

.EXAMPLE
$cancelBtn = New-Button -Label "Cancel" -OnClick { Pop-Screen }
#>
function New-Button {
    [CmdletBinding()]
    [OutputType([SuperTUI.Button])]
    param(
        [Parameter(Mandatory)]
        [string]$Label,

        [Parameter()]
        [scriptblock]$OnClick = $null,

        [Parameter()]
        [switch]$IsDefault,

        [Parameter()]
        [int]$Width = 0,

        [Parameter()]
        [int]$Height = 3
    )

    $button = [SuperTUI.Button]::new()
    $button.Label = $Label
    $button.IsDefault = $IsDefault.IsPresent
    $button.Height = $Height

    if ($Width -gt 0) {
        $button.Width = $Width
    } else {
        # Auto-size: label + 4 chars padding + 2 for borders
        $button.Width = $Label.Length + 6
    }

    if ($OnClick) {
        $handler = [EventHandler]{
            param($sender, $args)
            & $OnClick
        }
        $button.add_Click($handler)
    }

    return $button
}

<#
.SYNOPSIS
Creates a new TextBox component for single-line input.

.DESCRIPTION
Creates a text input field with validation, placeholder, and constraints.

.PARAMETER Label
Optional label displayed before the textbox.

.PARAMETER Value
Initial value.

.PARAMETER Placeholder
Placeholder text shown when empty.

.PARAMETER MaxLength
Maximum allowed length. Default: 0 (unlimited).

.PARAMETER IsReadOnly
Whether the textbox is read-only. Default: false.

.PARAMETER Width
Textbox width. Default: 40.

.EXAMPLE
$nameBox = New-TextBox -Label "Name:" -Value "" -Placeholder "Enter name" -MaxLength 50

.EXAMPLE
$pathBox = New-TextBox -Value "C:\Temp" -IsReadOnly -Width 60
#>
function New-TextBox {
    [CmdletBinding()]
    [OutputType([SuperTUI.TextBox])]
    param(
        [Parameter()]
        [string]$Label = "",

        [Parameter()]
        [string]$Value = "",

        [Parameter()]
        [string]$Placeholder = "",

        [Parameter()]
        [int]$MaxLength = 0,

        [Parameter()]
        [switch]$IsReadOnly,

        [Parameter()]
        [int]$Width = 40
    )

    $textBox = [SuperTUI.TextBox]::new()
    $textBox.Value = $Value
    $textBox.Placeholder = $Placeholder
    $textBox.MaxLength = $MaxLength
    $textBox.IsReadOnly = $IsReadOnly.IsPresent
    $textBox.Width = $Width
    $textBox.Height = 1

    # Label is rendered separately in PowerShell screens
    # Store it as a note property for reference
    if ($Label) {
        Add-Member -InputObject $textBox -NotePropertyName "_Label" -NotePropertyValue $Label
    }

    return $textBox
}

<#
.SYNOPSIS
Creates a new DataGrid component with auto-binding support.

.DESCRIPTION
Creates a table view that auto-updates when ItemsSource changes.
Supports ObservableCollection for automatic UI refresh.

.PARAMETER ItemsSource
Data source (IEnumerable). Use ObservableCollection for auto-updates.

.PARAMETER Columns
Array of column definitions. Each should have Header, Property, and optionally Width.

.PARAMETER OnItemSelected
ScriptBlock executed when an item is selected.

.PARAMETER Width
Grid width. Default: 80.

.PARAMETER Height
Grid height. Default: 20.

.EXAMPLE
$grid = New-DataGrid -ItemsSource $tasks -Columns @(
    @{ Header = "ID"; Property = "Id"; Width = "5" }
    @{ Header = "Title"; Property = "Title"; Width = "*" }
    @{ Header = "Status"; Property = "Status"; Width = "10" }
) -OnItemSelected { param($item) Write-Host "Selected: $($item.Title)" }
#>
function New-DataGrid {
    [CmdletBinding()]
    [OutputType([SuperTUI.DataGrid])]
    param(
        [Parameter()]
        [object]$ItemsSource = $null,

        [Parameter(Mandatory)]
        [hashtable[]]$Columns,

        [Parameter()]
        [scriptblock]$OnItemSelected = $null,

        [Parameter()]
        [int]$Width = 80,

        [Parameter()]
        [int]$Height = 20
    )

    $grid = [SuperTUI.DataGrid]::new()
    $grid.Width = $Width
    $grid.Height = $Height

    # Add columns
    foreach ($colDef in $Columns) {
        $col = [SuperTUI.GridColumn]::new()
        $col.Header = $colDef.Header
        $col.Property = $colDef.Property
        $col.Width = if ($colDef.Width) { $colDef.Width } else { "*" }
        $grid.Columns.Add($col)
    }

    # Set ItemsSource (supports ObservableCollection auto-binding)
    if ($ItemsSource) {
        $grid.ItemsSource = $ItemsSource
    }

    # Add ItemSelected event handler
    if ($OnItemSelected) {
        $handler = [EventHandler[SuperTUI.ItemSelectedEventArgs]]{
            param($sender, $args)
            & $OnItemSelected $args.SelectedItem
        }
        $grid.add_ItemSelected($handler)
    }

    return $grid
}

<#
.SYNOPSIS
Creates a new ListView component.

.DESCRIPTION
Creates a simple list view for displaying items.

.PARAMETER ItemsSource
Data source (IEnumerable). Use ObservableCollection for auto-updates.

.PARAMETER DisplayProperty
Property name to display for each item. If null, ToString() is used.

.PARAMETER OnItemSelected
ScriptBlock executed when an item is selected.

.PARAMETER Width
List width. Default: 40.

.PARAMETER Height
List height. Default: 15.

.EXAMPLE
$list = New-ListView -ItemsSource $projects -DisplayProperty "Name" -Height 20
#>
function New-ListView {
    [CmdletBinding()]
    [OutputType([SuperTUI.ListView])]
    param(
        [Parameter()]
        [object]$ItemsSource = $null,

        [Parameter()]
        [string]$DisplayProperty = $null,

        [Parameter()]
        [scriptblock]$OnItemSelected = $null,

        [Parameter()]
        [int]$Width = 40,

        [Parameter()]
        [int]$Height = 15
    )

    $list = [SuperTUI.ListView]::new()
    $list.Width = $Width
    $list.Height = $Height

    if ($DisplayProperty) {
        $list.DisplayProperty = $DisplayProperty
    }

    if ($ItemsSource) {
        $list.ItemsSource = $ItemsSource
    }

    if ($OnItemSelected) {
        $handler = [EventHandler[SuperTUI.ItemSelectedEventArgs]]{
            param($sender, $args)
            & $OnItemSelected $args.SelectedItem
        }
        $list.add_ItemSelected($handler)
    }

    return $list
}

#endregion

#region Navigation Helpers

<#
.SYNOPSIS
Pushes a screen onto the navigation stack.

.DESCRIPTION
Navigates to a new screen, deactivating the current one.

.PARAMETER Screen
The screen instance to navigate to.

.EXAMPLE
Push-Screen $taskListScreen

.EXAMPLE
Push-Screen (New-TaskFormScreen -TaskId 42)
#>
function Push-Screen {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [SuperTUI.Screen]$Screen
    )

    process {
        $mgr = [SuperTUI.ScreenManager]::Instance
        $mgr.Push($Screen)
    }
}

<#
.SYNOPSIS
Pops the current screen from the navigation stack.

.DESCRIPTION
Returns to the previous screen, reactivating it.

.EXAMPLE
Pop-Screen
#>
function Pop-Screen {
    [CmdletBinding()]
    param()

    $mgr = [SuperTUI.ScreenManager]::Instance
    $mgr.Pop()
}

<#
.SYNOPSIS
Replaces the current screen with a new one.

.DESCRIPTION
Replaces the top of the stack without growing it.

.PARAMETER Screen
The screen instance to replace with.

.EXAMPLE
Replace-Screen $loginScreen
#>
function Replace-Screen {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [SuperTUI.Screen]$Screen
    )

    process {
        $mgr = [SuperTUI.ScreenManager]::Instance
        $mgr.Replace($Screen)
    }
}

<#
.SYNOPSIS
Starts the TUI application with an initial screen.

.DESCRIPTION
Initializes the terminal, pushes the initial screen, and starts the main loop.

.PARAMETER InitialScreen
The first screen to display.

.EXAMPLE
Start-TUIApp -InitialScreen $mainMenuScreen
#>
function Start-TUIApp {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [SuperTUI.Screen]$InitialScreen
    )

    $mgr = [SuperTUI.ScreenManager]::Instance
    $mgr.Push($InitialScreen)
    $mgr.Run()
}

#endregion

#region Service Helpers

<#
.SYNOPSIS
Registers a service in the service container.

.DESCRIPTION
Registers a service factory or singleton instance.

.PARAMETER Name
Service name for lookup.

.PARAMETER Factory
ScriptBlock that creates the service instance (transient).

.PARAMETER Instance
Pre-created singleton instance.

.EXAMPLE
Register-Service "TaskService" { [TaskService]::new() }

.EXAMPLE
Register-Service "Config" -Instance $configObject
#>
function Register-Service {
    [CmdletBinding(DefaultParameterSetName = 'Factory')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Name,

        [Parameter(Mandatory, ParameterSetName = 'Factory', Position = 1)]
        [scriptblock]$Factory,

        [Parameter(Mandatory, ParameterSetName = 'Instance')]
        [object]$Instance
    )

    $container = [SuperTUI.ServiceContainer]::Instance

    if ($PSCmdlet.ParameterSetName -eq 'Instance') {
        $container.RegisterSingleton($Name, $Instance)
    } else {
        $factoryFunc = [Func[object]]{
            & $Factory
        }.GetNewClosure()
        $container.Register($Name, $factoryFunc)
    }
}

<#
.SYNOPSIS
Gets a service from the service container.

.DESCRIPTION
Retrieves a registered service by name.

.PARAMETER Name
Service name.

.EXAMPLE
$taskService = Get-Service "TaskService"
#>
function Get-Service {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Name
    )

    $container = [SuperTUI.ServiceContainer]::Instance
    return $container.Get($Name)
}

#endregion

#region Theme Helpers

<#
.SYNOPSIS
Gets the current theme or creates a default one.

.DESCRIPTION
Returns the default theme or a custom theme instance.

.EXAMPLE
$theme = Get-Theme
#>
function Get-Theme {
    [CmdletBinding()]
    [OutputType([SuperTUI.Theme])]
    param()

    return [SuperTUI.Theme]::Default
}

<#
.SYNOPSIS
Sets a custom theme color.

.DESCRIPTION
Modifies a theme's color properties.

.PARAMETER Theme
The theme to modify.

.PARAMETER Property
Color property name (Primary, Success, Error, etc.).

.PARAMETER R
Red component (0-255).

.PARAMETER G
Green component (0-255).

.PARAMETER B
Blue component (0-255).

.EXAMPLE
$theme = Get-Theme
Set-ThemeColor -Theme $theme -Property "Primary" -R 100 -G 150 -B 255
#>
function Set-ThemeColor {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [SuperTUI.Theme]$Theme,

        [Parameter(Mandatory)]
        [ValidateSet("Primary", "Secondary", "Success", "Warning", "Error", "Background", "Foreground", "Border", "Focus", "Selection")]
        [string]$Property,

        [Parameter(Mandatory)]
        [ValidateRange(0, 255)]
        [byte]$R,

        [Parameter(Mandatory)]
        [ValidateRange(0, 255)]
        [byte]$G,

        [Parameter(Mandatory)]
        [ValidateRange(0, 255)]
        [byte]$B
    )

    $color = [SuperTUI.Color]::FromRgb($R, $G, $B)
    $Theme.$Property = $color
}

#endregion

#region Module Initialization

# Auto-load core on module import
Initialize-SuperTUICore -Verbose:$false

#endregion

#region Exports

# Export all public functions
Export-ModuleMember -Function @(
    # Layout builders
    'New-GridLayout',
    'New-StackLayout',
    'New-DockLayout',

    # Component builders
    'New-Label',
    'New-Button',
    'New-TextBox',
    'New-DataGrid',
    'New-ListView',

    # Navigation
    'Push-Screen',
    'Pop-Screen',
    'Replace-Screen',
    'Start-TUIApp',

    # Services
    'Register-Service',
    'Get-Service',

    # Theme
    'Get-Theme',
    'Set-ThemeColor',

    # Core
    'Initialize-SuperTUICore'
)

#endregion
