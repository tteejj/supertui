# Today's Tasks Widget
# Shows tasks due today in scrollable list

. (Join-Path (Split-Path (Split-Path $PSCommandPath)) "BaseWidget.ps1")

class TodayTasksWidget : ScrollableWidget {
    [object]$TaskService

    static [hashtable] GetMetadata() {
        return @{
            Name = "Today's Tasks"
            Description = "Shows all tasks due today in scrollable list"
            Author = "SuperTUI"
            Version = "1.0.0"
        }
    }

    TodayTasksWidget([hashtable]$config) {
        $this.Id = $config.id ?? "today-tasks"
        $this.Title = "TODAY'S TASKS"
        $this.Number = $config.number ?? 4
        $this.Width = $config.width ?? 50
        $this.Height = $config.height ?? 12
        $this.ViewportHeight = $config.settings.viewportHeight ?? 8
        $this.Settings = $config.settings ?? @{}

        # Get TaskService
        if ($config.taskService) {
            $this.TaskService = $config.taskService
        }

        $this.RefreshItems()
    }

    [void] RefreshItems() {
        if (-not $this.TaskService) {
            $this.Items = @()
            return
        }

        # Get tasks due today
        $this.Items = @($this.TaskService.GetTasksDueToday())
    }

    [object] GetControl() {
        # Build multi-line label with task list
        $lines = @()

        # Header with counter
        $header = "[$($this.Number)] $($this.Title)$($this.GetFocusIndicator())"
        if ($this.Items.Count -gt $this.ViewportHeight) {
            $counter = $this.GetCounter()
            $header = $header.PadRight($this.Width - $counter.Length - 1) + $counter
        }
        $lines += $header
        $lines += ("─" * ($this.Width - 2))

        if ($this.Items.Count -eq 0) {
            # No tasks
            $lines += ""
            $lines += "  No tasks due today!".PadRight($this.Width - 2)
            $lines += ""
            for ($i = 3; $i -lt $this.ViewportHeight; $i++) {
                $lines += (" " * ($this.Width - 2))
            }
        } else {
            # Visible items
            $visibleItems = $this.GetVisibleItems()
            $visibleStartIndex = $this.ScrollOffset

            for ($i = 0; $i -lt $this.ViewportHeight; $i++) {
                if ($i -lt $visibleItems.Count) {
                    $task = $visibleItems[$i]
                    $itemIndex = $visibleStartIndex + $i
                    $isSelected = ($itemIndex -eq $this.SelectedIndex) -and $this.IsFocused

                    # Build line
                    if ($isSelected) {
                        $prefix = "►"
                    } else {
                        $prefix = " "
                    }

                    # Status indicator
                    $statusIcon = switch ($task.Status) {
                        "Completed" { "[✓]" }
                        "InProgress" { "[~]" }
                        "Pending" { "[ ]" }
                        default { "[ ]" }
                    }

                    # Priority
                    $priorityMark = switch ($task.Priority) {
                        "High" { "!" }
                        "Medium" { "·" }
                        "Low" { " " }
                        default { " " }
                    }

                    # Task title (truncate if too long)
                    $maxTitleLength = $this.Width - 15
                    if ($task.Title.Length -gt $maxTitleLength) {
                        $title = $task.Title.Substring(0, $maxTitleLength - 3) + "..."
                    } else {
                        $title = $task.Title.PadRight($maxTitleLength)
                    }

                    $line = "$prefix$statusIcon$priorityMark $title"

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

    [bool] HandleKey([ConsoleKeyInfo]$key) {
        # Let base class handle arrow keys
        if (([ScrollableWidget]$this).HandleKey($key)) {
            return $true
        }

        # Space to toggle complete
        if ($key.Key -eq [ConsoleKey]::Spacebar -and $this.IsFocused) {
            $task = $this.GetSelectedItem()
            if ($task) {
                if ($task.Status -eq "Completed") {
                    $task.Status = "Pending"
                    Write-Host "`nTask marked as pending" -ForegroundColor Yellow
                } else {
                    $this.TaskService.CompleteTask($task.Id)
                    Write-Host "`nTask completed!" -ForegroundColor Green
                }
                $this.RefreshItems()
                return $true
            }
        }

        return $false
    }

    [void] Activate() {
        $task = $this.GetSelectedItem()

        if (-not $task) { return }

        # Show task details
        Write-Host "`n═══════════════════════════════════════════════" -ForegroundColor Cyan
        Write-Host "  TASK DETAIL" -ForegroundColor Cyan
        Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "  ID:          #$($task.Id)" -ForegroundColor Gray
        Write-Host "  Title:       $($task.Title)" -ForegroundColor White
        Write-Host "  Status:      $($task.Status)" -ForegroundColor $(
            switch ($task.Status) {
                "Completed" { "Green" }
                "InProgress" { "Yellow" }
                default { "Gray" }
            }
        )
        Write-Host "  Priority:    $($task.Priority)" -ForegroundColor $(
            switch ($task.Priority) {
                "High" { "Red" }
                "Medium" { "Yellow" }
                default { "Gray" }
            }
        )
        Write-Host "  Due Date:    $($task.DueDate.ToString('yyyy-MM-dd'))" -ForegroundColor Gray
        Write-Host "  Created:     $($task.CreatedDate.ToString('yyyy-MM-dd'))" -ForegroundColor Gray

        if ($task.Description) {
            Write-Host "  Description: $($task.Description)" -ForegroundColor Gray
        }

        if ($task.ProjectName) {
            Write-Host "  Project:     $($task.ProjectName)" -ForegroundColor Cyan
        }

        Write-Host ""
        Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Actions: [Space] Toggle Complete  [E] Edit  [D] Delete" -ForegroundColor Yellow
        Write-Host "(Press any key to close)" -ForegroundColor Gray
        Read-Host "Press Enter" | Out-Null
    }

    [void] Refresh() {
        $this.RefreshItems()
    }
}
