# Dashboard Manager - Function-based with state management
# Works around PowerShell class visibility issues while maintaining full state

# NOTE: Dependencies must be loaded by caller:
# - Widgets/BaseWidget.ps1
# - Widgets/WidgetRegistry.ps1
# - Config/DashboardConfig.ps1
# - All widget types to be used

function New-DashboardManager {
    <#
    .SYNOPSIS
    Creates a new dashboard manager state object

    .PARAMETER ConfigName
    Name of the configuration to load

    .PARAMETER TaskService
    TaskService instance

    .EXAMPLE
    $dashboard = New-DashboardManager -ConfigName "default" -TaskService $taskSvc
    #>
    param(
        [string]$ConfigName = "default",
        [Parameter(Mandatory)]
        [object]$TaskService
    )

    # Create state hashtable (acts like object)
    $state = @{
        Widgets = @()
        FocusedWidgetIndex = 0
        Layout = $null
        Config = $null
        Running = $true
        TaskService = $TaskService
        TerminalWidth = 120
        TerminalHeight = 30
    }

    # Get terminal size if available
    try {
        if ($Host.UI.RawUI.WindowSize) {
            $state.TerminalWidth = $Host.UI.RawUI.WindowSize.Width
            $state.TerminalHeight = $Host.UI.RawUI.WindowSize.Height
        }
    } catch {
        # Use defaults
    }

    # Load configuration
    $state.Config = [DashboardConfig]::Load($ConfigName)

    # Build layout
    Build-DashboardLayout -State $state

    return $state
}

function Build-DashboardLayout {
    <#
    .SYNOPSIS
    Builds the dashboard layout from configuration

    .PARAMETER State
    Dashboard manager state object
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$State
    )

    # Create GridLayout from config
    $rows = $State.Config.Layout.rows
    $columns = $State.Config.Layout.columns

    # SuperTUI module should already be loaded by caller
    $State.Layout = New-GridLayout -Rows $rows -Columns $columns
    $State.Layout.Width = $State.TerminalWidth
    $State.Layout.Height = $State.TerminalHeight

    # Create widgets from config
    $widgetNumber = 1
    foreach ($widgetConfig in $State.Config.Layout.widgets) {
        # Create widget instance
        $widgetSettings = @{
            id = $widgetConfig.id
            number = $widgetNumber++
            taskService = $State.TaskService
            settings = $widgetConfig.settings ?? @{}
        }

        try {
            # Create widget directly (works at runtime in functions)
            $widget = $null
            switch ($widgetConfig.type) {
                "TaskStatsWidget" { $widget = [TaskStatsWidget]::new($widgetSettings) }
                "WeekViewWidget" { $widget = [WeekViewWidget]::new($widgetSettings) }
                "MenuWidget" { $widget = [MenuWidget]::new($widgetSettings) }
                "TodayTasksWidget" { $widget = [TodayTasksWidget]::new($widgetSettings) }
                "RecentActivityWidget" { $widget = [RecentActivityWidget]::new($widgetSettings) }
                default { throw "Unknown widget type: $($widgetConfig.type)" }
            }

            if ($widget) {
                # Get control from widget
                $control = $widget.GetControl()

                # Add to layout
                $pos = $widgetConfig.position
                $rowSpan = if ($pos.rowSpan) { $pos.rowSpan } else { 1 }
                $colSpan = if ($pos.colSpan) { $pos.colSpan } else { 1 }
                $State.Layout.AddChild($control, $pos.row, $pos.col, $rowSpan, $colSpan)

                # Track widget
                $State.Widgets += $widget

                Write-Verbose "Added widget: $($widgetConfig.type) at ($($pos.row),$($pos.col))"
            }

        } catch {
            Write-Warning "Failed to create widget '$($widgetConfig.type)': $_"
        }
    }

    # Focus first widget
    if ($State.Widgets.Count -gt 0) {
        $State.Widgets[$State.FocusedWidgetIndex].Focus()
    }
}

function Start-DashboardLoop {
    <#
    .SYNOPSIS
    Runs the main dashboard event loop

    .PARAMETER State
    Dashboard manager state object
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$State
    )

    # Hide cursor
    [Console]::CursorVisible = $false

    try {
        while ($State.Running) {
            # Render
            Invoke-DashboardRender -State $State

            # Handle input
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                Invoke-DashboardInput -State $State -Key $key
            }

            # Small delay
            Start-Sleep -Milliseconds 50
        }
    } finally {
        # Restore cursor
        [Console]::CursorVisible = $true
        [Console]::Clear()
    }
}

function Invoke-DashboardRender {
    <#
    .SYNOPSIS
    Renders the dashboard

    .PARAMETER State
    Dashboard manager state object
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$State
    )

    # Clear and position cursor
    [Console]::SetCursorPosition(0, 0)

    # Get theme
    $theme = Get-Theme

    # Create render context
    $context = [SuperTUI.RenderContext]::new($theme, $State.TerminalWidth, $State.TerminalHeight)

    # Render layout
    $output = $State.Layout.Render($context)

    # Write output
    Write-Host $output -NoNewline
}

function Invoke-DashboardInput {
    <#
    .SYNOPSIS
    Handles keyboard input

    .PARAMETER State
    Dashboard manager state object

    .PARAMETER Key
    ConsoleKeyInfo from ReadKey
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$State,

        [Parameter(Mandatory)]
        [ConsoleKeyInfo]$Key
    )

    $handled = $false

    # Check if focused widget wants to handle it
    if ($State.Widgets.Count -gt 0) {
        $focusedWidget = $State.Widgets[$State.FocusedWidgetIndex]
        $handled = $focusedWidget.HandleKey($Key)
    }

    # If not handled by widget, handle navigation
    if (-not $handled) {
        switch ($Key.Key) {
            # Tab - next widget
            "Tab" {
                if ($Key.Modifiers -eq [ConsoleModifiers]::Shift) {
                    Set-PreviousWidgetFocus -State $State
                } else {
                    Set-NextWidgetFocus -State $State
                }
                $handled = $true
            }

            # Number keys - jump to widget
            "D1" { Set-WidgetFocus -State $State -Index 0; $handled = $true }
            "D2" { Set-WidgetFocus -State $State -Index 1; $handled = $true }
            "D3" { Set-WidgetFocus -State $State -Index 2; $handled = $true }
            "D4" { Set-WidgetFocus -State $State -Index 3; $handled = $true }
            "D5" { Set-WidgetFocus -State $State -Index 4; $handled = $true }
            "D6" { Set-WidgetFocus -State $State -Index 5; $handled = $true }
            "D7" { Set-WidgetFocus -State $State -Index 6; $handled = $true }
            "D8" { Set-WidgetFocus -State $State -Index 7; $handled = $true }
            "D9" { Set-WidgetFocus -State $State -Index 8; $handled = $true }

            # Enter - activate focused widget
            "Enter" {
                if ($State.Widgets.Count -gt 0) {
                    $State.Widgets[$State.FocusedWidgetIndex].Activate()
                    Invoke-DashboardRebuild -State $State  # Refresh after activation
                }
                $handled = $true
            }

            # F5 - Refresh all
            "F5" {
                Invoke-DashboardRefresh -State $State
                $handled = $true
            }

            # Q or Escape - Exit
            "Q" {
                $State.Running = $false
                $handled = $true
            }
            "Escape" {
                $State.Running = $false
                $handled = $true
            }
        }
    }

    # Rebuild layout if something changed
    if ($handled) {
        Invoke-DashboardRebuild -State $State
    }
}

function Set-NextWidgetFocus {
    <#
    .SYNOPSIS
    Moves focus to the next widget

    .PARAMETER State
    Dashboard manager state object
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$State
    )

    if ($State.Widgets.Count -eq 0) { return }

    # Blur current
    $State.Widgets[$State.FocusedWidgetIndex].Blur()

    # Move to next
    $State.FocusedWidgetIndex = ($State.FocusedWidgetIndex + 1) % $State.Widgets.Count

    # Focus new
    $State.Widgets[$State.FocusedWidgetIndex].Focus()
}

function Set-PreviousWidgetFocus {
    <#
    .SYNOPSIS
    Moves focus to the previous widget

    .PARAMETER State
    Dashboard manager state object
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$State
    )

    if ($State.Widgets.Count -eq 0) { return }

    # Blur current
    $State.Widgets[$State.FocusedWidgetIndex].Blur()

    # Move to previous
    $State.FocusedWidgetIndex--
    if ($State.FocusedWidgetIndex -lt 0) {
        $State.FocusedWidgetIndex = $State.Widgets.Count - 1
    }

    # Focus new
    $State.Widgets[$State.FocusedWidgetIndex].Focus()
}

function Set-WidgetFocus {
    <#
    .SYNOPSIS
    Sets focus to a specific widget by index

    .PARAMETER State
    Dashboard manager state object

    .PARAMETER Index
    Widget index (0-based)
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$State,

        [Parameter(Mandatory)]
        [int]$Index
    )

    if ($Index -lt 0 -or $Index -ge $State.Widgets.Count) { return }

    # Blur current
    $State.Widgets[$State.FocusedWidgetIndex].Blur()

    # Focus target
    $State.FocusedWidgetIndex = $Index
    $State.Widgets[$State.FocusedWidgetIndex].Focus()
}

function Invoke-DashboardRefresh {
    <#
    .SYNOPSIS
    Refreshes all widgets

    .PARAMETER State
    Dashboard manager state object
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$State
    )

    foreach ($widget in $State.Widgets) {
        $widget.Refresh()
    }
}

function Invoke-DashboardRebuild {
    <#
    .SYNOPSIS
    Rebuilds the layout (after widget updates)

    .PARAMETER State
    Dashboard manager state object
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$State
    )

    # GridLayout doesn't support clearing children, so we rebuild from scratch
    $rows = $State.Config.Layout.rows
    $columns = $State.Config.Layout.columns

    $State.Layout = New-GridLayout -Rows $rows -Columns $columns
    $State.Layout.Width = $State.TerminalWidth
    $State.Layout.Height = $State.TerminalHeight

    # Re-add all widgets with updated controls
    $widgetIndex = 0
    foreach ($widgetConfig in $State.Config.Layout.widgets) {
        if ($widgetIndex -lt $State.Widgets.Count) {
            $widget = $State.Widgets[$widgetIndex]
            $control = $widget.GetControl()

            $pos = $widgetConfig.position
            $rowSpan = if ($pos.rowSpan) { $pos.rowSpan } else { 1 }
            $colSpan = if ($pos.colSpan) { $pos.colSpan } else { 1 }
            $State.Layout.AddChild($control, $pos.row, $pos.col, $rowSpan, $colSpan)

            $widgetIndex++
        }
    }
}

# Main entry point function
function Start-Dashboard {
    <#
    .SYNOPSIS
    Starts the modular dashboard

    .PARAMETER ConfigName
    Configuration name to load (default: "default")

    .PARAMETER TaskService
    TaskService instance

    .EXAMPLE
    $taskSvc = New-TaskService
    Start-Dashboard -TaskService $taskSvc
    #>
    param(
        [string]$ConfigName = "default",
        [Parameter(Mandatory)]
        [object]$TaskService
    )

    # Ensure default config exists
    [DashboardConfig]::CreateDefaultConfig()

    # Create dashboard state
    $dashboard = New-DashboardManager -ConfigName $ConfigName -TaskService $TaskService

    # Run dashboard loop
    Start-DashboardLoop -State $dashboard
}
