# SuperTUI Dashboard Screen - Interactive Version
# Demonstrates: Keyboard input, selection highlighting, live stats, navigation

using namespace System.Collections.ObjectModel

# Menu item model
class MenuItem {
    [string]$Key
    [string]$Title
    [string]$Description
    [string]$TargetScreen
    [int]$Index

    MenuItem([int]$idx, [string]$key, [string]$title, [string]$desc, [string]$target) {
        $this.Index = $idx
        $this.Key = $key
        $this.Title = $title
        $this.Description = $desc
        $this.TargetScreen = $target
    }

    [string] GetDisplayText([bool]$selected) {
        $prefix = if ($selected) { "► " } else { "  " }
        return "$prefix[$($this.Key)] $($this.Title.PadRight(18)) - $($this.Description)"
    }
}

# Dashboard screen state
class DashboardScreenState {
    [int]$SelectedIndex = 0
    [MenuItem[]]$MenuItems
    [object]$TaskService
    [bool]$Running = $true
    [string]$LastAction = ""

    DashboardScreenState() {
        $this.InitializeMenuItems()
    }

    [void] InitializeMenuItems() {
        $this.MenuItems = @(
            [MenuItem]::new(0, "T", "Tasks", "View and manage all tasks", "TaskList")
            [MenuItem]::new(1, "P", "Projects", "Organize tasks by project", "ProjectList")
            [MenuItem]::new(2, "W", "Today", "Tasks due today", "Today")
            [MenuItem]::new(3, "K", "Week", "This week's schedule", "Week")
            [MenuItem]::new(4, "M", "Time Tracking", "Log and review time entries", "TimeTracking")
            [MenuItem]::new(5, "C", "Commands", "Command library and scripts", "CommandLibrary")
            [MenuItem]::new(6, "F", "Files", "Browse filesystem", "FileBrowser")
            [MenuItem]::new(7, "R", "Reports", "Analytics and statistics", "Reports")
            [MenuItem]::new(8, "S", "Settings", "Configure application", "Settings")
            [MenuItem]::new(9, "H", "Help", "Documentation and support", "Help")
            [MenuItem]::new(10, "Q", "Exit", "Close application", "EXIT")
        )
    }

    [void] MoveUp() {
        if ($this.SelectedIndex -gt 0) {
            $this.SelectedIndex--
        }
    }

    [void] MoveDown() {
        if ($this.SelectedIndex -lt ($this.MenuItems.Count - 1)) {
            $this.SelectedIndex++
        }
    }

    [MenuItem] GetSelectedItem() {
        return $this.MenuItems[$this.SelectedIndex]
    }

    [MenuItem] GetItemByKey([string]$key) {
        return $this.MenuItems | Where-Object { $_.Key -eq $key.ToUpper() } | Select-Object -First 1
    }

    [MenuItem] GetItemByNumber([int]$num) {
        if ($num -ge 1 -and $num -le $this.MenuItems.Count) {
            return $this.MenuItems[$num - 1]
        }
        elseif ($num -eq 0) {
            # 0 maps to last item (Exit)
            return $this.MenuItems[$this.MenuItems.Count - 1]
        }
        return $null
    }
}

function Show-DashboardScreen {
    <#
    .SYNOPSIS
    Interactive dashboard screen with keyboard navigation

    .DESCRIPTION
    Main dashboard that shows:
    - Live statistics from TaskService
    - Interactive menu with arrow key navigation
    - Letter key shortcuts (T, P, W, etc.)
    - Number key shortcuts (1-9, 0)
    - Selection highlighting

    .PARAMETER TaskService
    TaskService instance for live statistics

    .EXAMPLE
    $taskSvc = New-TaskService
    Show-DashboardScreen -TaskService $taskSvc
    #>

    param(
        [Parameter(Mandatory)]
        [object]$TaskService
    )

    $state = [DashboardScreenState]::new()
    $state.TaskService = $TaskService

    # Hide cursor for cleaner UI
    [Console]::CursorVisible = $false

    try {
        while ($state.Running) {
            # Render the screen
            Render-Dashboard -State $state

            # Handle input
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                Handle-DashboardInput -State $state -Key $key
            }

            Start-Sleep -Milliseconds 50
        }
    }
    finally {
        [Console]::CursorVisible = $true
        [Console]::Clear()
    }
}

function Render-Dashboard {
    param([DashboardScreenState]$State)

    [Console]::SetCursorPosition(0, 0)

    $stats = $State.TaskService.Statistics
    $currentTime = Get-Date -Format "dddd, MMMM dd, yyyy - HH:mm:ss"

    # Build output buffer (faster than multiple Write-Host calls)
    $output = ""

    # ─────────────────────────────────────────────────────────────
    # HEADER
    # ─────────────────────────────────────────────────────────────
    $output += "  SUPERTUI  -  Terminal User Interface Framework`n"
    $output += ("═" * 78) + "`n"
    $output += $currentTime.PadLeft(40 + $currentTime.Length / 2) + "`n"
    $output += "`n"

    # ─────────────────────────────────────────────────────────────
    # QUICK STATS
    # ─────────────────────────────────────────────────────────────
    $tasksStat = "[T] Tasks: $($stats.TotalTasks) ($($stats.CompletedTasks) done)".PadRight(20)
    $projectsStat = "[P] Projects: 3".PadRight(20)
    $todayStat = "[W] Today: $($stats.TodayTasks)".PadRight(20)
    $weekStat = "[K] Week: $($stats.WeekTasks)".PadRight(20)

    $output += "$tasksStat$projectsStat$todayStat$weekStat`n"
    $output += "`n"

    # ─────────────────────────────────────────────────────────────
    # MAIN MENU
    # ─────────────────────────────────────────────────────────────
    $output += ("─" * 30) + " MAIN MENU " + ("─" * 37) + "`n"

    # Split into two columns (6 items each, 5th column has 5)
    $leftItems = $State.MenuItems[0..5]
    $rightItems = $State.MenuItems[6..10]

    for ($i = 0; $i -lt [Math]::Max($leftItems.Count, $rightItems.Count); $i++) {
        $leftItem = if ($i -lt $leftItems.Count) {
            $leftItems[$i].GetDisplayText($State.SelectedIndex -eq $leftItems[$i].Index)
        } else {
            " " * 40
        }

        $rightItem = if ($i -lt $rightItems.Count) {
            $rightItems[$i].GetDisplayText($State.SelectedIndex -eq $rightItems[$i].Index)
        } else {
            ""
        }

        $output += $leftItem.PadRight(40) + $rightItem + "`n"
    }

    $output += "`n"

    # ─────────────────────────────────────────────────────────────
    # RECENT ACTIVITY
    # ─────────────────────────────────────────────────────────────
    $output += ("─" * 28) + " RECENT ACTIVITY " + ("─" * 33) + "`n"
    $output += "  2m ago     Completed    Task: Implement DashboardScreen`n"
    $output += "  15m ago    Started      Task: Create TaskService`n"
    $output += "  1h ago     Created      Task: Add keyboard navigation`n"
    $output += "`n"

    # ─────────────────────────────────────────────────────────────
    # STATUS BAR
    # ─────────────────────────────────────────────────────────────
    $statusLeft = "  [↑↓] Navigate  [Enter] Select  [1-9/0] Direct"
    $statusRight = "[Letter] Quick  [F5] Refresh  [Q] Quit  "
    $output += $statusLeft + $statusRight.PadLeft(78 - $statusLeft.Length) + "`n"

    # Last action feedback
    if ($State.LastAction) {
        $output += "`n  Last: $($State.LastAction)".PadRight(78) + "`n"
    }

    # Write entire buffer at once
    Write-Host $output -NoNewline
}

function Handle-DashboardInput {
    param(
        [DashboardScreenState]$State,
        [System.ConsoleKeyInfo]$Key
    )

    $State.LastAction = ""

    switch ($Key.Key) {
        # Arrow navigation
        "UpArrow" {
            $State.MoveUp()
            $State.LastAction = "Moved up to: $($State.GetSelectedItem().Title)"
        }
        "DownArrow" {
            $State.MoveDown()
            $State.LastAction = "Moved down to: $($State.GetSelectedItem().Title)"
        }

        # Enter - activate selected
        "Enter" {
            $item = $State.GetSelectedItem()
            if ($item.TargetScreen -eq "EXIT") {
                $State.Running = $false
                $State.LastAction = "Exiting..."
            } else {
                $State.LastAction = "Selected: $($item.Title) -> $($item.TargetScreen) (not implemented yet)"
            }
        }

        # F5 - Refresh
        "F5" {
            $State.TaskService.RefreshStatistics()
            $State.LastAction = "Statistics refreshed"
        }

        # Number keys (1-9, 0)
        "D1" { Activate-ItemByNumber -State $State -Number 1 }
        "D2" { Activate-ItemByNumber -State $State -Number 2 }
        "D3" { Activate-ItemByNumber -State $State -Number 3 }
        "D4" { Activate-ItemByNumber -State $State -Number 4 }
        "D5" { Activate-ItemByNumber -State $State -Number 5 }
        "D6" { Activate-ItemByNumber -State $State -Number 6 }
        "D7" { Activate-ItemByNumber -State $State -Number 7 }
        "D8" { Activate-ItemByNumber -State $State -Number 8 }
        "D9" { Activate-ItemByNumber -State $State -Number 9 }
        "D0" { Activate-ItemByNumber -State $State -Number 0 }

        # Letter keys (quick shortcuts)
        default {
            $char = $Key.KeyChar.ToString().ToUpper()
            $item = $State.GetItemByKey($char)
            if ($item) {
                if ($item.TargetScreen -eq "EXIT") {
                    $State.Running = $false
                    $State.LastAction = "Exiting..."
                } else {
                    $State.SelectedIndex = $item.Index
                    $State.LastAction = "Quick key '$char': $($item.Title) -> $($item.TargetScreen) (not implemented yet)"
                }
            }
        }
    }
}

function Activate-ItemByNumber {
    param(
        [DashboardScreenState]$State,
        [int]$Number
    )

    $item = $State.GetItemByNumber($Number)
    if ($item) {
        if ($item.TargetScreen -eq "EXIT") {
            $State.Running = $false
            $State.LastAction = "Exiting..."
        } else {
            $State.SelectedIndex = $item.Index
            $State.LastAction = "Number key ${Number}: $($item.Title) -> $($item.TargetScreen) (not implemented yet)"
        }
    }
}
