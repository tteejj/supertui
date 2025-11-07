# PmcScreen - Base class for all PMC screens
# Provides standard screen lifecycle, layout, and widget management

using namespace System.Collections.Generic
using namespace System.Text

# Load dependencies
. "$PSScriptRoot/widgets/PmcWidget.ps1"
. "$PSScriptRoot/widgets/PmcMenuBar.ps1"
. "$PSScriptRoot/widgets/PmcHeader.ps1"
. "$PSScriptRoot/widgets/PmcFooter.ps1"
. "$PSScriptRoot/widgets/PmcStatusBar.ps1"
. "$PSScriptRoot/widgets/PmcPanel.ps1"
. "$PSScriptRoot/layout/PmcLayoutManager.ps1"
. "$PSScriptRoot/theme/PmcThemeManager.ps1"

<#
.SYNOPSIS
Base class for all PMC screens

.DESCRIPTION
PmcScreen provides:
- Standard widget composition (MenuBar, Header, Footer, StatusBar, Content)
- Layout management integration
- Screen lifecycle (OnEnter, OnExit, LoadData)
- Input handling delegation
- Rendering orchestration

.EXAMPLE
class TaskListScreen : PmcScreen {
    TaskListScreen() : base("TaskList", "Tasks") {
        $this.Header.SetBreadcrumb(@("Home", "Tasks"))
    }

    [void] LoadData() {
        # Load tasks...
    }

    [string] RenderContent() {
        # Render task list...
    }
}
#>
class PmcScreen {
    # === Core Properties ===
    [string]$ScreenKey = ""
    [string]$ScreenTitle = ""

    # === Standard Widgets ===
    [object]$MenuBar = $null
    [object]$Header = $null
    [object]$Footer = $null
    [object]$StatusBar = $null
    [object]$ContentWidgets

    # === Layout ===
    [object]$LayoutManager = $null
    [int]$TermWidth = 80
    [int]$TermHeight = 24

    # === State ===
    [bool]$IsActive = $false
    [object]$RenderEngine = $null

    # === Event Handlers ===
    [scriptblock]$OnEnterHandler = $null
    [scriptblock]$OnExitHandler = $null

    # === Constructor ===
    PmcScreen([string]$key, [string]$title) {
        $this.ScreenKey = $key
        $this.ScreenTitle = $title
        $this.ContentWidgets = New-Object 'System.Collections.Generic.List[object]'

        # Create default widgets
        $this._CreateDefaultWidgets()
    }

    hidden [void] _CreateDefaultWidgets() {
        # Menu bar
        $this.MenuBar = New-Object PmcMenuBar
        $this.MenuBar.AddMenu("Tasks", 'T', @())
        $this.MenuBar.AddMenu("Projects", 'P', @())
        $this.MenuBar.AddMenu("Options", 'O', @())
        $this.MenuBar.AddMenu("Help", '?', @())

        # Header
        $this.Header = New-Object PmcHeader -ArgumentList $this.ScreenTitle

        # Footer with standard shortcuts
        $this.Footer = New-Object PmcFooter
        $this.Footer.AddShortcut("Esc", "Back")
        $this.Footer.AddShortcut("F10", "Menu")

        # Status bar
        $this.StatusBar = New-Object PmcStatusBar
        $this.StatusBar.SetLeftText("Ready")
    }

    # === Lifecycle Methods ===

    <#
    .SYNOPSIS
    Called when screen becomes active

    .DESCRIPTION
    Override to perform initialization when screen is displayed
    #>
    [void] OnEnter() {
        $this.IsActive = $true
        $this.LoadData()

        if ($this.OnEnterHandler) {
            & $this.OnEnterHandler $this
        }
    }

    <#
    .SYNOPSIS
    Called when screen becomes inactive

    .DESCRIPTION
    Override to perform cleanup when leaving screen
    #>
    [void] OnExit() {
        $this.IsActive = $false

        if ($this.OnExitHandler) {
            & $this.OnExitHandler $this
        }
    }

    <#
    .SYNOPSIS
    Load data for this screen

    .DESCRIPTION
    Override to load screen-specific data
    #>
    [void] LoadData() {
        # Override in subclass
    }

    # === Layout Management ===

    <#
    .SYNOPSIS
    Apply layout to all widgets

    .PARAMETER layoutManager
    Layout manager instance

    .PARAMETER termWidth
    Terminal width

    .PARAMETER termHeight
    Terminal height
    #>
    [void] ApplyLayout([PmcLayoutManager]$layoutManager, [int]$termWidth, [int]$termHeight) {
        $this.LayoutManager = $layoutManager
        $this.TermWidth = $termWidth
        $this.TermHeight = $termHeight

        # Apply layout to standard widgets
        if ($this.MenuBar) {
            $rect = $layoutManager.GetRegion('MenuBar', $termWidth, $termHeight)
            $this.MenuBar.SetPosition($rect.X, $rect.Y)
            $this.MenuBar.SetSize($rect.Width, $rect.Height)
        }

        if ($this.Header) {
            $rect = $layoutManager.GetRegion('Header', $termWidth, $termHeight)
            $this.Header.SetPosition($rect.X, $rect.Y)
            $this.Header.SetSize($rect.Width, $rect.Height)
        }

        if ($this.Footer) {
            $rect = $layoutManager.GetRegion('Footer', $termWidth, $termHeight)
            $this.Footer.SetPosition($rect.X, $rect.Y)
            $this.Footer.SetSize($rect.Width, $rect.Height)
        }

        if ($this.StatusBar) {
            $rect = $layoutManager.GetRegion('StatusBar', $termWidth, $termHeight)
            $this.StatusBar.SetPosition($rect.X, $rect.Y)
            $this.StatusBar.SetSize($rect.Width, $rect.Height)
        }

        # Apply layout to content widgets
        $this.ApplyContentLayout($layoutManager, $termWidth, $termHeight)
    }

    <#
    .SYNOPSIS
    Apply layout to content area widgets

    .DESCRIPTION
    Override to position custom content widgets
    #>
    [void] ApplyContentLayout([PmcLayoutManager]$layoutManager, [int]$termWidth, [int]$termHeight) {
        # Override in subclass to position content widgets
    }

    <#
    .SYNOPSIS
    Handle terminal resize

    .PARAMETER newWidth
    New terminal width

    .PARAMETER newHeight
    New terminal height
    #>
    [void] OnTerminalResize([int]$newWidth, [int]$newHeight) {
        if ($this.LayoutManager) {
            $this.ApplyLayout($this.LayoutManager, $newWidth, $newHeight)
        }
    }

    # === Rendering ===

    <#
    .SYNOPSIS
    Initialize widgets with render engine

    .PARAMETER renderEngine
    SpeedTUI render engine instance
    #>
    [void] Initialize([object]$renderEngine) {
        $this.RenderEngine = $renderEngine

        # Initialize standard widgets
        if ($this.MenuBar) {
            $this.MenuBar.Initialize($renderEngine)
        }
        if ($this.Header) {
            $this.Header.Initialize($renderEngine)
        }
        if ($this.Footer) {
            $this.Footer.Initialize($renderEngine)
        }
        if ($this.StatusBar) {
            $this.StatusBar.Initialize($renderEngine)
        }

        # Initialize content widgets
        foreach ($widget in $this.ContentWidgets) {
            $widget.Initialize($renderEngine)
        }
    }

    <#
    .SYNOPSIS
    Render the entire screen

    .OUTPUTS
    String containing ANSI-formatted screen output
    #>
    [string] Render() {
        $sb = [System.Text.StringBuilder]::new(4096)

        # Render MenuBar (if present)
        if ($this.MenuBar) {
            $output = $this.MenuBar.Render()
            if ($output) {
                $sb.Append($output)
            }
        }

        # Render Header
        if ($this.Header) {
            $output = $this.Header.Render()
            if ($output) {
                $sb.Append($output)
            }
        }

        # Render content (override in subclass)
        $contentOutput = $this.RenderContent()
        if ($contentOutput) {
            $sb.Append($contentOutput)
        }

        # Render content widgets
        foreach ($widget in $this.ContentWidgets) {
            $output = $widget.Render()
            if ($output) {
                $sb.Append($output)
            }
        }

        # Render Footer
        if ($this.Footer) {
            $output = $this.Footer.Render()
            if ($output) {
                $sb.Append($output)
            }
        }

        # Render StatusBar
        if ($this.StatusBar) {
            $output = $this.StatusBar.Render()
            if ($output) {
                $sb.Append($output)
            }
        }

        $result = $sb.ToString()
        return $result
    }

    <#
    .SYNOPSIS
    Render content area

    .DESCRIPTION
    Override in subclass to render screen-specific content

    .OUTPUTS
    String containing ANSI-formatted content
    #>
    [string] RenderContent() {
        # Override in subclass
        return ""
    }

    # === Input Handling ===

    <#
    .SYNOPSIS
    Handle keyboard input

    .PARAMETER keyInfo
    Console key info

    .OUTPUTS
    Boolean indicating if input was handled
    #>
    [bool] HandleKeyPress([ConsoleKeyInfo]$keyInfo) {
        # MenuBar gets priority (if active)
        if ($this.MenuBar -and $this.MenuBar.IsActive) {
            if ($this.MenuBar.HandleKeyPress($keyInfo)) {
                return $true
            }
        }

        # F10 activates menu bar
        if ($keyInfo.Key -eq 'F10' -and $this.MenuBar) {
            $this.MenuBar.Activate()
            return $true
        }

        # Alt+letter hotkeys activate menu bar (even when not active)
        if ($this.MenuBar -and ($keyInfo.Modifiers -band [ConsoleModifiers]::Alt)) {
            if ($this.MenuBar.HandleKeyPress($keyInfo)) {
                return $true
            }
        }

        # Pass to content widgets (in reverse order for z-index)
        for ($i = $this.ContentWidgets.Count - 1; $i -ge 0; $i--) {
            $widget = $this.ContentWidgets[$i]
            if ($widget.PSObject.Methods['HandleKeyPress']) {
                if ($widget.HandleKeyPress($keyInfo)) {
                    return $true
                }
            }
        }

        # Pass to subclass
        return $this.HandleInput($keyInfo)
    }

    <#
    .SYNOPSIS
    Handle screen-specific input

    .DESCRIPTION
    Override in subclass to handle custom input

    .PARAMETER keyInfo
    Console key info

    .OUTPUTS
    Boolean indicating if input was handled
    #>
    [bool] HandleInput([ConsoleKeyInfo]$keyInfo) {
        # Override in subclass
        return $false
    }

    # === Widget Management ===

    <#
    .SYNOPSIS
    Add a widget to the content area

    .PARAMETER widget
    Widget to add
    #>
    [void] AddContentWidget([PmcWidget]$widget) {
        $this.ContentWidgets.Add($widget)

        # Initialize if render engine available
        if ($this.RenderEngine) {
            $widget.Initialize($this.RenderEngine)
        }
    }

    <#
    .SYNOPSIS
    Remove a widget from the content area

    .PARAMETER widget
    Widget to remove
    #>
    [void] RemoveContentWidget([PmcWidget]$widget) {
        $this.ContentWidgets.Remove($widget)
    }

    # === Utility Methods ===

    <#
    .SYNOPSIS
    Show a message in the status bar

    .PARAMETER message
    Message to display
    #>
    [void] ShowStatus([string]$message) {
        if ($this.StatusBar) {
            $this.StatusBar.SetLeftText($message)
        }
    }

    <#
    .SYNOPSIS
    Show an error in the status bar

    .PARAMETER message
    Error message
    #>
    [void] ShowError([string]$message) {
        if ($this.StatusBar) {
            $this.StatusBar.ShowError($message)
        }
    }

    <#
    .SYNOPSIS
    Show a success message in the status bar

    .PARAMETER message
    Success message
    #>
    [void] ShowSuccess([string]$message) {
        if ($this.StatusBar) {
            $this.StatusBar.ShowSuccess($message)
        }
    }
}

# Classes exported automatically in PowerShell 5.1+
