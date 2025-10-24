# Quick Menu Widget
# Scrollable navigation menu

. (Join-Path (Split-Path (Split-Path $PSCommandPath)) "BaseWidget.ps1")

class MenuWidget : ScrollableWidget {

    static [hashtable] GetMetadata() {
        return @{
            Name = "Quick Menu"
            Description = "Scrollable navigation menu with keyboard shortcuts"
            Author = "SuperTUI"
            Version = "1.0.0"
        }
    }

    MenuWidget([hashtable]$config) {
        $this.Id = $config.id ?? "menu"
        $this.Title = "MENU"
        $this.Number = $config.number ?? 3
        $this.Width = $config.width ?? 25
        $this.Height = $config.height ?? 10
        $this.ViewportHeight = $config.settings.viewportHeight ?? 6
        $this.Settings = $config.settings ?? @{}

        # Initialize menu items
        $this.Items = @(
            @{ Key = "T"; Label = "Tasks"; Target = "TaskList" }
            @{ Key = "P"; Label = "Projects"; Target = "ProjectList" }
            @{ Key = "W"; Label = "Today"; Target = "Today" }
            @{ Key = "K"; Label = "Week"; Target = "Week" }
            @{ Key = "M"; Label = "Time Tracking"; Target = "TimeTracking" }
            @{ Key = "C"; Label = "Commands"; Target = "CommandLibrary" }
            @{ Key = "F"; Label = "Files"; Target = "FileBrowser" }
            @{ Key = "R"; Label = "Reports"; Target = "Reports" }
            @{ Key = "S"; Label = "Settings"; Target = "Settings" }
            @{ Key = "H"; Label = "Help"; Target = "Help" }
            @{ Key = "Q"; Label = "Exit"; Target = "EXIT" }
        )
    }

    [object] GetControl() {
        # Build multi-line label with menu items
        $lines = @()

        # Header
        $header = "[$($this.Number)] $($this.Title)$($this.GetFocusIndicator())"
        if ($this.Items.Count -gt $this.ViewportHeight) {
            $counter = $this.GetCounter()
            $header = $header.PadRight($this.Width - $counter.Length - 1) + $counter
        }
        $lines += $header
        $lines += ("─" * ($this.Width - 2))

        # Visible items
        $visibleItems = $this.GetVisibleItems()
        $visibleStartIndex = $this.ScrollOffset

        for ($i = 0; $i -lt $this.ViewportHeight; $i++) {
            if ($i -lt $visibleItems.Count) {
                $item = $visibleItems[$i]
                $itemIndex = $visibleStartIndex + $i
                $isSelected = ($itemIndex -eq $this.SelectedIndex) -and $this.IsFocused

                if ($isSelected) {
                    $prefix = "►"
                } else {
                    $prefix = " "
                }
                $line = " $prefix [$($item.Key)] $($item.Label)"

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

        $text = $lines -join "`n"

        # Create label
        $label = New-Label -Text $text -Alignment Left
        $label.Width = $this.Width
        $label.Height = $this.Height

        return $label
    }

    [bool] HandleKey([ConsoleKeyInfo]$key) {
        # Let base class handle arrow keys first
        if (([ScrollableWidget]$this).HandleKey($key)) {
            return $true
        }

        # Handle letter shortcuts
        $char = $key.KeyChar.ToString().ToUpper()
        $matchedItem = $this.Items | Where-Object { $_.Key -eq $char } | Select-Object -First 1

        if ($matchedItem) {
            $index = $this.Items.IndexOf($matchedItem)
            $this.SelectedIndex = $index

            # Adjust scroll if needed
            if ($this.SelectedIndex -lt $this.ScrollOffset) {
                $this.ScrollOffset = $this.SelectedIndex
            } elseif ($this.SelectedIndex -ge ($this.ScrollOffset + $this.ViewportHeight)) {
                $this.ScrollOffset = $this.SelectedIndex - $this.ViewportHeight + 1
            }

            return $true
        }

        return $false
    }

    [void] Activate() {
        $selected = $this.GetSelectedItem()

        if ($selected.Target -eq "EXIT") {
            Write-Host "`nExiting application..." -ForegroundColor Yellow
            # Signal to exit
        } else {
            Write-Host "`nNavigating to: $($selected.Target)" -ForegroundColor Yellow
            Write-Host "(Screen not implemented yet)" -ForegroundColor Gray
            Write-Host "(Press any key to continue)" -ForegroundColor Gray
            Read-Host "Press Enter" | Out-Null
        }
    }
}
