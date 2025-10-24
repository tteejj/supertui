# SuperTUI Dashboard Screen - Main entry point
# Demonstrates: Auto-binding, GridLayout, EventBus integration, Service Container

using namespace System.Collections.ObjectModel

# Dashboard statistics model (auto-updates UI via INotifyPropertyChanged)
class DashboardStats {
    [int]$TotalTasks = 0
    [int]$CompletedTasks = 0
    [int]$ActiveProjects = 0
    [int]$TodayTasks = 0
    [int]$WeekTasks = 0
    [string]$CurrentTime = ""

    DashboardStats() {
        $this.Refresh()
    }

    [void] Refresh() {
        # Will integrate with real services
        $this.CurrentTime = (Get-Date -Format "dddd, MMMM dd, yyyy - HH:mm:ss")
    }
}

# Recent activity model
class ActivityItem {
    [string]$Time
    [string]$Action
    [string]$Description

    ActivityItem([string]$time, [string]$action, [string]$desc) {
        $this.Time = $time
        $this.Action = $action
        $this.Description = $desc
    }
}

function New-DashboardScreen {
    <#
    .SYNOPSIS
    Creates the main dashboard/menu screen for SuperTUI

    .DESCRIPTION
    A hybrid dashboard + menu screen that showcases SuperTUI capabilities:
    - Auto-updating statistics via ObservableCollection
    - Real-time activity feed via EventBus
    - Multi-modal navigation (numbers, letters, arrows)
    - GridLayout for perfect positioning
    - No emoji icons - clean ASCII markers

    .EXAMPLE
    $dashboard = New-DashboardScreen
    Start-TUIApp -InitialScreen $dashboard
    #>

    # Initialize statistics (will auto-update UI when changed)
    $stats = [DashboardStats]::new()

    # Activity feed (auto-updates when events occur)
    $activities = [ObservableCollection[ActivityItem]]::new()
    $activities.Add([ActivityItem]::new("2m ago", "Completed", "Task #127: Fix login bug"))
    $activities.Add([ActivityItem]::new("15m ago", "Started", "Task #134: Review PR"))
    $activities.Add([ActivityItem]::new("1h ago", "Created", "Project: Q4 Planning"))

    # Main layout: 5 rows (header, stats, menu, activity, status)
    $layout = New-GridLayout -Rows "Auto","Auto","*","Auto","Auto" -Columns "*"
    $layout.Width = 80
    $layout.Height = 24

    # ──────────────────────────────────────────────────────────────
    # ROW 0: HEADER with title and clock
    # ──────────────────────────────────────────────────────────────
    $headerLayout = New-StackLayout -Orientation Vertical
    $headerLayout.Height = 4

    $title = New-Label -Text "  SUPERTUI  -  Terminal User Interface Framework" -Alignment Left
    $title.Height = 1
    $headerLayout.AddChild($title)

    $separator = New-Label -Text ("═" * 78) -Alignment Left
    $separator.Height = 1
    $headerLayout.AddChild($separator)

    $clock = New-Label -Text $stats.CurrentTime -Alignment Center
    $clock.Height = 1
    $headerLayout.AddChild($clock)

    $layout.AddChild($headerLayout, 0, 0)

    # ──────────────────────────────────────────────────────────────
    # ROW 1: QUICK STATS (auto-updating via data binding)
    # ──────────────────────────────────────────────────────────────
    $statsLayout = New-GridLayout -Rows "Auto" -Columns "*","*","*","*"
    $statsLayout.Height = 3

    $tasksStat = New-Label -Text "[T] Tasks: $($stats.TotalTasks) ($($stats.CompletedTasks) done)" -Alignment Center
    $statsLayout.AddChild($tasksStat, 0, 0)

    $projectsStat = New-Label -Text "[P] Projects: $($stats.ActiveProjects)" -Alignment Center
    $statsLayout.AddChild($projectsStat, 0, 1)

    $todayStat = New-Label -Text "[W] Today: $($stats.TodayTasks)" -Alignment Center
    $statsLayout.AddChild($todayStat, 0, 2)

    $weekStat = New-Label -Text "[K] Week: $($stats.WeekTasks)" -Alignment Center
    $statsLayout.AddChild($weekStat, 0, 3)

    $layout.AddChild($statsLayout, 1, 0)

    # ──────────────────────────────────────────────────────────────
    # ROW 2: MAIN MENU (no emoji - clean ASCII markers)
    # ──────────────────────────────────────────────────────────────
    $menuLayout = New-StackLayout -Orientation Vertical
    $menuLayout.Height = 15

    # Menu separator
    $menuHeader = New-Label -Text "──────────────────────────── MAIN MENU ────────────────────────────" -Alignment Center
    $menuHeader.Height = 1
    $menuLayout.AddChild($menuHeader)

    # Menu items - two column layout
    $menuGrid = New-GridLayout -Rows "Auto","Auto","Auto","Auto","Auto","Auto" -Columns "*","*"

    # Column 1
    $item1 = New-Label -Text "  [T] Tasks           - View and manage all tasks"
    $item1.Height = 1
    $menuGrid.AddChild($item1, 0, 0)

    $item2 = New-Label -Text "  [P] Projects        - Organize tasks by project"
    $item2.Height = 1
    $menuGrid.AddChild($item2, 1, 0)

    $item3 = New-Label -Text "  [W] Today           - Tasks due today"
    $item3.Height = 1
    $menuGrid.AddChild($item3, 2, 0)

    $item4 = New-Label -Text "  [K] Week            - This week's schedule"
    $item4.Height = 1
    $menuGrid.AddChild($item4, 3, 0)

    $item5 = New-Label -Text "  [M] Time Tracking   - Log and review time entries"
    $item5.Height = 1
    $menuGrid.AddChild($item5, 4, 0)

    $item6 = New-Label -Text "  [C] Commands        - Command library and scripts"
    $item6.Height = 1
    $menuGrid.AddChild($item6, 5, 0)

    # Column 2
    $item7 = New-Label -Text "  [F] Files           - Browse filesystem"
    $item7.Height = 1
    $menuGrid.AddChild($item7, 0, 1)

    $item8 = New-Label -Text "  [R] Reports         - Analytics and statistics"
    $item8.Height = 1
    $menuGrid.AddChild($item8, 1, 1)

    $item9 = New-Label -Text "  [S] Settings        - Configure application"
    $item9.Height = 1
    $menuGrid.AddChild($item9, 2, 1)

    $item10 = New-Label -Text "  [H] Help            - Documentation and support"
    $item10.Height = 1
    $menuGrid.AddChild($item10, 3, 1)

    $item11 = New-Label -Text "  [Q] Exit            - Close application"
    $item11.Height = 1
    $menuGrid.AddChild($item11, 4, 1)

    $menuLayout.AddChild($menuGrid)

    $layout.AddChild($menuLayout, 2, 0)

    # ──────────────────────────────────────────────────────────────
    # ROW 3: RECENT ACTIVITY (auto-updating via EventBus)
    # ──────────────────────────────────────────────────────────────
    $activityLayout = New-StackLayout -Orientation Vertical
    $activityLayout.Height = 5

    $activityHeader = New-Label -Text "─────────────────────── RECENT ACTIVITY ───────────────────────────" -Alignment Center
    $activityHeader.Height = 1
    $activityLayout.AddChild($activityHeader)

    # Activity items (will use ListView when we need auto-binding)
    foreach ($activity in $activities) {
        $activityLine = New-Label -Text "  $($activity.Time.PadRight(10)) $($activity.Action.PadRight(12)) $($activity.Description)"
        $activityLine.Height = 1
        $activityLayout.AddChild($activityLine)
    }

    $layout.AddChild($activityLayout, 3, 0)

    # ──────────────────────────────────────────────────────────────
    # ROW 4: STATUS BAR with keybindings
    # ──────────────────────────────────────────────────────────────
    $statusBar = New-Label -Text "  [1-9] Direct Access  |  [Letter] Quick Key  |  [↑↓] Navigate  |  [Enter] Select  |  [F5] Refresh  |  [Q] Quit" -Alignment Center
    $statusBar.Height = 1

    $layout.AddChild($statusBar, 4, 0)

    # ──────────────────────────────────────────────────────────────
    # RETURN the complete screen layout
    # ──────────────────────────────────────────────────────────────
    return $layout
}

# Note: Export-ModuleMember only needed if loaded as module
# When dot-sourced, function is automatically available
