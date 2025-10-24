# Dashboard Manager
# Orchestrates widget layout, focus management, and rendering

# NOTE: Dependencies must be loaded by caller:
# - Widgets/BaseWidget.ps1
# - Widgets/WidgetRegistry.ps1
# - Config/DashboardConfig.ps1
# - All widget types to be used

class DashboardManager {
    [object[]]$Widgets = @()
    [int]$FocusedWidgetIndex = 0
    [object]$Layout
    [object]$Config
    [bool]$Running = $true
    [object]$TaskService
    [int]$TerminalWidth = 80
    [int]$TerminalHeight = 24

    DashboardManager([string]$configName, [object]$taskService) {
        $this.TaskService = $taskService

        # Get terminal size - use defaults as PowerShell classes can't access $Host
        # These will be updated by the caller if needed
        $this.TerminalWidth = 120
        $this.TerminalHeight = 30

        # Load configuration
        $this.Config = [DashboardConfig]::Load($configName)

        # Build layout
        $this.BuildLayout()
    }

    [void] BuildLayout() {
        # Create GridLayout from config
        $rows = $this.Config.Layout.rows
        $columns = $this.Config.Layout.columns

        # SuperTUI module should already be loaded by caller
        # (importing with -Force would reset state and break widget types)

        $this.Layout = New-GridLayout -Rows $rows -Columns $columns
        $this.Layout.Width = $this.TerminalWidth
        $this.Layout.Height = $this.TerminalHeight

        # Create widgets from config
        $widgetNumber = 1
        foreach ($widgetConfig in $this.Config.Layout.widgets) {
            # Create widget instance
            $widgetSettings = @{
                id = $widgetConfig.id
                number = $widgetNumber++
                taskService = $this.TaskService
                settings = $widgetConfig.settings ?? @{}
            }

            try {
                # Create widget directly (registry has PowerShell scope issues)
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
                    $this.Layout.AddChild($control, $pos.row, $pos.col)

                    # Track widget
                    $this.Widgets += $widget

                    Write-Verbose "Added widget: $($widgetConfig.type) at ($($pos.row),$($pos.col))"
                }

            } catch {
                Write-Warning "Failed to create widget '$($widgetConfig.type)': $_"
            }
        }

        # Focus first widget
        if ($this.Widgets.Count -gt 0) {
            $this.Widgets[$this.FocusedWidgetIndex].Focus()
        }
    }

    [void] Run() {
        # Hide cursor
        [Console]::CursorVisible = $false

        try {
            while ($this.Running) {
                # Render
                $this.Render()

                # Handle input
                if ([Console]::KeyAvailable) {
                    $key = [Console]::ReadKey($true)
                    $this.HandleInput($key)
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

    [void] Render() {
        # Clear and position cursor
        [Console]::SetCursorPosition(0, 0)

        # Get theme
        $theme = Get-Theme

        # Create render context
        $context = [SuperTUI.RenderContext]::new($theme, $this.TerminalWidth, $this.TerminalHeight)

        # Render layout
        $output = $this.Layout.Render($context)

        # Write output
        Write-Host $output -NoNewline
    }

    [void] HandleInput([ConsoleKeyInfo]$key) {
        $handled = $false

        # Check if focused widget wants to handle it
        if ($this.Widgets.Count -gt 0) {
            $focusedWidget = $this.Widgets[$this.FocusedWidgetIndex]
            $handled = $focusedWidget.HandleKey($key)
        }

        # If not handled by widget, handle navigation
        if (-not $handled) {
            switch ($key.Key) {
                # Tab - next widget
                "Tab" {
                    if ($key.Modifiers -eq [ConsoleModifiers]::Shift) {
                        $this.FocusPreviousWidget()
                    } else {
                        $this.FocusNextWidget()
                    }
                    $handled = $true
                }

                # Number keys - jump to widget
                "D1" { $this.FocusWidget(0); $handled = $true }
                "D2" { $this.FocusWidget(1); $handled = $true }
                "D3" { $this.FocusWidget(2); $handled = $true }
                "D4" { $this.FocusWidget(3); $handled = $true }
                "D5" { $this.FocusWidget(4); $handled = $true }
                "D6" { $this.FocusWidget(5); $handled = $true }
                "D7" { $this.FocusWidget(6); $handled = $true }
                "D8" { $this.FocusWidget(7); $handled = $true }
                "D9" { $this.FocusWidget(8); $handled = $true }

                # Enter - activate focused widget
                "Enter" {
                    if ($this.Widgets.Count -gt 0) {
                        $this.Widgets[$this.FocusedWidgetIndex].Activate()
                        $this.RebuildLayout()  # Refresh after activation
                    }
                    $handled = $true
                }

                # F5 - Refresh all
                "F5" {
                    $this.RefreshAll()
                    $handled = $true
                }

                # Q or Escape - Exit
                "Q" {
                    $this.Running = $false
                    $handled = $true
                }
                "Escape" {
                    $this.Running = $false
                    $handled = $true
                }
            }
        }

        # Rebuild layout if something changed
        if ($handled) {
            $this.RebuildLayout()
        }
    }

    [void] FocusNextWidget() {
        if ($this.Widgets.Count -eq 0) { return }

        # Blur current
        $this.Widgets[$this.FocusedWidgetIndex].Blur()

        # Move to next
        $this.FocusedWidgetIndex = ($this.FocusedWidgetIndex + 1) % $this.Widgets.Count

        # Focus new
        $this.Widgets[$this.FocusedWidgetIndex].Focus()
    }

    [void] FocusPreviousWidget() {
        if ($this.Widgets.Count -eq 0) { return }

        # Blur current
        $this.Widgets[$this.FocusedWidgetIndex].Blur()

        # Move to previous
        $this.FocusedWidgetIndex--
        if ($this.FocusedWidgetIndex -lt 0) {
            $this.FocusedWidgetIndex = $this.Widgets.Count - 1
        }

        # Focus new
        $this.Widgets[$this.FocusedWidgetIndex].Focus()
    }

    [void] FocusWidget([int]$index) {
        if ($index -lt 0 -or $index -ge $this.Widgets.Count) { return }

        # Blur current
        $this.Widgets[$this.FocusedWidgetIndex].Blur()

        # Focus target
        $this.FocusedWidgetIndex = $index
        $this.Widgets[$this.FocusedWidgetIndex].Focus()
    }

    [void] RefreshAll() {
        foreach ($widget in $this.Widgets) {
            $widget.Refresh()
        }
    }

    [void] RebuildLayout() {
        # Clear existing layout children
        $this.Layout.Children.Clear()

        # Re-add all widgets with updated controls
        $widgetIndex = 0
        foreach ($widgetConfig in $this.Config.Layout.widgets) {
            if ($widgetIndex -lt $this.Widgets.Count) {
                $widget = $this.Widgets[$widgetIndex]
                $control = $widget.GetControl()

                $pos = $widgetConfig.position
                $this.Layout.AddChild($control, $pos.row, $pos.col)

                $widgetIndex++
            }
        }
    }
}

# Helper function to start dashboard
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

    # Create and run dashboard
    $dashboard = [DashboardManager]::new($ConfigName, $TaskService)
    $dashboard.Run()
}
