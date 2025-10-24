# Task Statistics Widget
# Displays task counts and statistics (non-scrollable)

. (Join-Path (Split-Path (Split-Path $PSCommandPath)) "BaseWidget.ps1")

class TaskStatsWidget : StaticWidget {
    [object]$TaskService

    static [hashtable] GetMetadata() {
        return @{
            Name = "Task Statistics"
            Description = "Displays task counts and completion statistics"
            Author = "SuperTUI"
            Version = "1.0.0"
        }
    }

    TaskStatsWidget([hashtable]$config) {
        $this.Id = $config.id ?? "task-stats"
        $this.Title = "TASK STATS"
        $this.Number = $config.number ?? 1
        $this.Width = $config.width ?? 25
        $this.Height = $config.height ?? 8
        $this.Settings = $config.settings ?? @{}

        # Get TaskService from service container (will be set after creation)
        if ($config.taskService) {
            $this.TaskService = $config.taskService
        }
    }

    [object] GetControl() {
        # Build multi-line label with stats
        $stats = $this.TaskService.Statistics

        $lines = @()
        $lines += "[$($this.Number)] $($this.Title)$($this.GetFocusIndicator())"
        $lines += ("─" * ($this.Width - 2))
        $lines += ""
        $lines += "  Total:        $($stats.TotalTasks)"
        $lines += "  Completed:    $($stats.CompletedTasks)"
        $lines += "  In Progress:  $($stats.InProgressTasks)"
        $lines += "  Pending:      $($stats.PendingTasks)"
        $lines += ""
        $lines += "  Due Today:    $($stats.TodayTasks)"
        $lines += "  This Week:    $($stats.WeekTasks)"
        $lines += "  Overdue:      $($stats.OverdueTasks)"

        $text = $lines -join "`n"

        # Create label
        $label = New-Label -Text $text -Alignment Left
        $label.Width = $this.Width
        $label.Height = $this.Height

        return $label
    }

    [void] Activate() {
        # Could show expanded stats view
        Write-Host "`nTask Statistics Detail View would appear here" -ForegroundColor Yellow
        Write-Host "(Press any key to continue)" -ForegroundColor Gray
        Read-Host "Press Enter" | Out-Null
    }

    [void] Refresh() {
        if ($this.TaskService) {
            $this.TaskService.RefreshStatistics()
        }
    }
}
