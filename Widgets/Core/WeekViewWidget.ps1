# Week View Widget
# Shows tasks for the current week with bar charts

. (Join-Path (Split-Path (Split-Path $PSCommandPath)) "BaseWidget.ps1")

class WeekViewWidget : ScrollableWidget {
    [object]$TaskService
    [DateTime]$StartOfWeek

    static [hashtable] GetMetadata() {
        return @{
            Name = "Week View"
            Description = "Shows task distribution across the week"
            Author = "SuperTUI"
            Version = "1.0.0"
        }
    }

    WeekViewWidget([hashtable]$config) {
        $this.Id = $config.id ?? "week-view"
        $this.Title = "THIS WEEK"
        $this.Number = $config.number ?? 2
        $this.Width = $config.width ?? 30
        $this.Height = $config.height ?? 10
        $this.ViewportHeight = $config.settings.viewportHeight ?? 7
        $this.Settings = $config.settings ?? @{}

        # Get TaskService
        if ($config.taskService) {
            $this.TaskService = $config.taskService
        }

        # Calculate start of week (Monday)
        $today = (Get-Date).Date
        $dayOfWeek = [int]$today.DayOfWeek
        if ($dayOfWeek -eq 0) { $dayOfWeek = 7 }  # Sunday = 7
        $this.StartOfWeek = $today.AddDays(1 - $dayOfWeek)

        $this.RefreshItems()
    }

    [void] RefreshItems() {
        if (-not $this.TaskService) { return }

        # Build items for each day of the week
        $this.Items = @()

        for ($i = 0; $i -lt 7; $i++) {
            $date = $this.StartOfWeek.AddDays($i)
            $dayName = $date.ToString("ddd")
            $isToday = $date -eq (Get-Date).Date

            # Count tasks for this day
            $tasks = $this.TaskService.Tasks | Where-Object {
                $_.DueDate.Date -eq $date -and $_.Status -ne "Completed"
            }
            if ($tasks) {
                $taskCount = $tasks.Count
            } else {
                $taskCount = 0
            }

            $this.Items += @{
                Date = $date
                DayName = $dayName
                TaskCount = $taskCount
                IsToday = $isToday
            }
        }
    }

    [object] GetControl() {
        # Build multi-line label with week view
        $lines = @()

        # Header
        $header = "[$($this.Number)] $($this.Title)$($this.GetFocusIndicator())"
        $lines += $header
        $lines += ("─" * ($this.Width - 2))

        # Visible items (days)
        $visibleItems = $this.GetVisibleItems()
        $visibleStartIndex = $this.ScrollOffset

        # Find max task count for scaling bars
        $maxTasks = ($this.Items | Measure-Object -Property TaskCount -Maximum).Maximum
        if ($maxTasks -eq 0) { $maxTasks = 1 }

        for ($i = 0; $i -lt $this.ViewportHeight; $i++) {
            if ($i -lt $visibleItems.Count) {
                $item = $visibleItems[$i]
                $itemIndex = $visibleStartIndex + $i
                $isSelected = ($itemIndex -eq $this.SelectedIndex) -and $this.IsFocused

                # Build line
                if ($isSelected) {
                    $prefix = "►"
                } else {
                    $prefix = " "
                }
                if ($item.IsToday) {
                    $dayMarker = "*"
                } else {
                    $dayMarker = " "
                }

                # Calculate bar length (proportional to task count)
                $barWidth = 10
                if ($item.TaskCount -gt 0) {
                    $filledWidth = [Math]::Max(1, [int](($item.TaskCount / $maxTasks) * $barWidth))
                } else {
                    $filledWidth = 0
                }

                if ($filledWidth -gt 0) {
                    $bar = ("#" * $filledWidth).PadRight($barWidth)
                } else {
                    $bar = (" " * $barWidth)
                }

                $line = "$prefix$dayMarker$($item.DayName) [$bar] $($item.TaskCount)"

                # Pad and add scroll indicator
                $line = $line.PadRight($this.Width - 3)
                if ($this.Items.Count -gt $this.ViewportHeight) {
                    $indicator = $this.GetScrollIndicator($i)
                    $line = $line.Substring(0, $this.Width - 3) + $indicator
                }

                $lines += $line
            } else {
                # Empty line
                $lines += (" " * ($this.Width - 2))
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
        $selected = $this.GetSelectedItem()

        Write-Host "`nTasks for $($selected.DayName), $($selected.Date.ToString('MMM dd')):" -ForegroundColor Yellow

        if ($selected.TaskCount -eq 0) {
            Write-Host "  No tasks scheduled" -ForegroundColor Gray
        } else {
            $tasks = $this.TaskService.Tasks | Where-Object {
                $_.DueDate.Date -eq $selected.Date -and $_.Status -ne "Completed"
            }

            foreach ($task in $tasks) {
                Write-Host "  - [$($task.Priority.PadRight(6))] $($task.Title)" -ForegroundColor Cyan
            }
        }

        Write-Host "`n(Press any key to continue)" -ForegroundColor Gray
        Read-Host "Press Enter" | Out-Null
    }

    [void] Refresh() {
        $this.RefreshItems()
    }
}
