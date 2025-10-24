# Recent Activity Widget
# Shows recent actions and events

. (Join-Path (Split-Path (Split-Path $PSCommandPath)) "BaseWidget.ps1")

class RecentActivityWidget : ScrollableWidget {
    [object]$TaskService

    static [hashtable] GetMetadata() {
        return @{
            Name = "Recent Activity"
            Description = "Shows recent task actions and events"
            Author = "SuperTUI"
            Version = "1.0.0"
        }
    }

    RecentActivityWidget([hashtable]$config) {
        $this.Id = $config.id ?? "recent-activity"
        $this.Title = "RECENT ACTIVITY"
        $this.Number = $config.number ?? 5
        $this.Width = $config.width ?? 70
        $this.Height = $config.height ?? 6
        $this.ViewportHeight = $config.settings.viewportHeight ?? 3
        $this.Settings = $config.settings ?? @{}

        # Get TaskService
        if ($config.taskService) {
            $this.TaskService = $config.taskService
        }

        $this.RefreshItems()
    }

    [void] RefreshItems() {
        # Build activity feed
        # In real implementation, this would come from EventBus or activity log
        # For now, generate from tasks

        $this.Items = @()

        if ($this.TaskService) {
            # Recent completed tasks
            $completed = $this.TaskService.Tasks |
                Where-Object { $_.Status -eq "Completed" } |
                Sort-Object CreatedDate -Descending |
                Select-Object -First 5

            foreach ($task in $completed) {
                $timeAgo = $this.GetTimeAgo($task.CreatedDate)
                $this.Items += @{
                    Time = $timeAgo
                    Action = "DONE"
                    Description = "#$($task.Id) $($task.Title)"
                    Task = $task
                }
            }

            # Recent in-progress tasks
            $inProgress = $this.TaskService.Tasks |
                Where-Object { $_.Status -eq "InProgress" } |
                Sort-Object CreatedDate -Descending |
                Select-Object -First 3

            foreach ($task in $inProgress) {
                $timeAgo = $this.GetTimeAgo($task.CreatedDate)
                $this.Items += @{
                    Time = $timeAgo
                    Action = "START"
                    Description = "#$($task.Id) $($task.Title)"
                    Task = $task
                }
            }

            # Recent created tasks
            $created = $this.TaskService.Tasks |
                Where-Object { $_.Status -eq "Pending" } |
                Sort-Object CreatedDate -Descending |
                Select-Object -First 3

            foreach ($task in $created) {
                $timeAgo = $this.GetTimeAgo($task.CreatedDate)
                $this.Items += @{
                    Time = $timeAgo
                    Action = "NEW"
                    Description = "#$($task.Id) $($task.Title)"
                    Task = $task
                }
            }

            # Sort all by time (most recent first)
            # For demo, just use the order we added them
        }

        # Limit to reasonable number
        if ($this.Items.Count -gt 15) {
            $this.Items = $this.Items[0..14]
        }
    }

    [string] GetTimeAgo([DateTime]$date) {
        $span = (Get-Date) - $date

        if ($span.TotalMinutes -lt 1) {
            return "just now"
        } elseif ($span.TotalMinutes -lt 60) {
            $mins = [int]$span.TotalMinutes
            return "${mins}m ago"
        } elseif ($span.TotalHours -lt 24) {
            $hours = [int]$span.TotalHours
            return "${hours}h ago"
        } else {
            $days = [int]$span.TotalDays
            return "${days}d ago"
        }
    }

    [object] GetControl() {
        # Build multi-line label with activity feed
        $lines = @()

        # Header
        $header = "[$($this.Number)] $($this.Title)$($this.GetFocusIndicator())"
        if ($this.Items.Count -gt $this.ViewportHeight) {
            $counter = $this.GetCounter()
            $header = $header.PadRight($this.Width - $counter.Length - 1) + $counter
        }
        $lines += $header
        $lines += ("─" * ($this.Width - 2))

        if ($this.Items.Count -eq 0) {
            # No activity
            $lines += ""
            $lines += "  No recent activity".PadRight($this.Width - 2)
            for ($i = 2; $i -lt $this.ViewportHeight; $i++) {
                $lines += (" " * ($this.Width - 2))
            }
        } else {
            # Visible items
            $visibleItems = $this.GetVisibleItems()
            $visibleStartIndex = $this.ScrollOffset

            for ($i = 0; $i -lt $this.ViewportHeight; $i++) {
                if ($i -lt $visibleItems.Count) {
                    $activity = $visibleItems[$i]
                    $itemIndex = $visibleStartIndex + $i
                    $isSelected = ($itemIndex -eq $this.SelectedIndex) -and $this.IsFocused

                    # Build line
                    if ($isSelected) {
                        $prefix = "►"
                    } else {
                        $prefix = " "
                    }

                    # Format: time    [ACTION] description
                    $time = $activity.Time.PadRight(10)
                    $action = "[$($activity.Action.PadRight(5))]"
                    $desc = $activity.Description

                    # Truncate description if too long
                    $maxDescLength = $this.Width - 25
                    if ($desc.Length -gt $maxDescLength) {
                        $desc = $desc.Substring(0, $maxDescLength - 3) + "..."
                    }

                    $line = "$prefix $time $action $desc"

                    # Add scroll indicator
                    if ($this.Items.Count -gt $this.ViewportHeight) {
                        $indicator = $this.GetScrollIndicator($i)
                        $line = $line.PadRight($this.Width - 3) + $indicator
                    } else {
                        $line = $line.PadRight($this.Width - 2)
                    }

                    $lines += $line
                } else {
                    # Empty line
                    $lines += (" " * ($this.Width - 2))
                }
            }
        }

        $text = $lines -join "`n"

        # Create label
        $label = New-Label -Text $text -Alignment Left
        $label.Width = $this.Width
        $label.Height = $this.Height

        return $label
    }

    [void] Activate() {
        $activity = $this.GetSelectedItem()

        if (-not $activity) { return }

        # Jump to the task
        if ($activity.Task) {
            Write-Host "`nJumping to task: #$($activity.Task.Id) $($activity.Task.Title)" -ForegroundColor Yellow
            Write-Host "(Task detail screen would open here)" -ForegroundColor Gray
            Write-Host ""
            Write-Host "  Status:   $($activity.Task.Status)" -ForegroundColor Cyan
            Write-Host "  Priority: $($activity.Task.Priority)" -ForegroundColor Cyan
            Write-Host "  Due:      $($activity.Task.DueDate.ToString('yyyy-MM-dd'))" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "(Press any key to continue)" -ForegroundColor Gray
            Read-Host "Press Enter" | Out-Null
        }
    }

    [void] Refresh() {
        $this.RefreshItems()
    }
}
