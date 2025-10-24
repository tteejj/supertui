# Base Widget Classes for SuperTUI Dashboard
# All widgets inherit from these base classes

using namespace System.Collections.ObjectModel

# ═══════════════════════════════════════════════════════════════════════════
# BASE WIDGET CLASS
# ═══════════════════════════════════════════════════════════════════════════

class DashboardWidget {
    [string]$Id
    [string]$Title
    [int]$Number                    # Widget number for quick access (1-9)
    [bool]$IsFocused = $false
    [bool]$CanSelect = $false       # Whether widget has selectable items
    [hashtable]$Settings = @{}      # User-configurable settings
    [int]$Width = 20
    [int]$Height = 10

    # Metadata for registry
    static [hashtable] GetMetadata() {
        return @{
            Name = "Base Widget"
            Description = "Base class for all widgets"
            Author = "SuperTUI"
            Version = "1.0.0"
        }
    }

    # Required: Return SuperTUI control for rendering
    [object] GetControl() {
        throw "GetControl() must be implemented by derived class"
    }

    # Focus management
    [void] Focus() {
        $this.IsFocused = $true
        $this.OnFocus()
    }

    [void] Blur() {
        $this.IsFocused = $false
        $this.OnBlur()
    }

    # Key handling - return true if handled, false to bubble up
    [bool] HandleKey([ConsoleKeyInfo]$key) {
        return $false
    }

    # Activation - called when Enter pressed on widget
    [void] Activate() {
        # Override in derived class
    }

    # Refresh - called to update widget data
    [void] Refresh() {
        # Override in derived class
    }

    # Lifecycle hooks
    [void] OnFocus() { }
    [void] OnBlur() { }
    [void] OnAdded() { }
    [void] OnRemoved() { }

    # Helper to create focus indicator
    [string] GetFocusIndicator() {
        if ($this.IsFocused) {
            return " <FOCUS"
        } else {
            return ""
        }
    }
}

# ═══════════════════════════════════════════════════════════════════════════
# SCROLLABLE WIDGET CLASS
# ═══════════════════════════════════════════════════════════════════════════

class ScrollableWidget : DashboardWidget {
    [object[]]$Items = @()          # All items
    [int]$ViewportHeight = 5        # Visible items count
    [int]$ScrollOffset = 0          # Current scroll position
    [int]$SelectedIndex = 0         # Currently selected item (global index)

    ScrollableWidget() {
        $this.CanSelect = $true
    }

    # Navigation within widget
    [void] MoveUp() {
        if ($this.Items.Count -eq 0) { return }

        if ($this.SelectedIndex -gt 0) {
            $this.SelectedIndex--

            # Scroll up if selection moves above viewport
            if ($this.SelectedIndex -lt $this.ScrollOffset) {
                $this.ScrollOffset = $this.SelectedIndex
            }
        } else {
            # Wrap to bottom
            $this.SelectedIndex = $this.Items.Count - 1
            $this.ScrollOffset = [Math]::Max(0, $this.Items.Count - $this.ViewportHeight)
        }
    }

    [void] MoveDown() {
        if ($this.Items.Count -eq 0) { return }

        if ($this.SelectedIndex -lt ($this.Items.Count - 1)) {
            $this.SelectedIndex++

            # Scroll down if selection moves below viewport
            $visibleBottom = $this.ScrollOffset + $this.ViewportHeight - 1
            if ($this.SelectedIndex -gt $visibleBottom) {
                $this.ScrollOffset = $this.SelectedIndex - $this.ViewportHeight + 1
            }
        } else {
            # Wrap to top
            $this.SelectedIndex = 0
            $this.ScrollOffset = 0
        }
    }

    [void] PageUp() {
        $this.SelectedIndex = [Math]::Max(0, $this.SelectedIndex - $this.ViewportHeight)
        $this.ScrollOffset = [Math]::Max(0, $this.ScrollOffset - $this.ViewportHeight)
    }

    [void] PageDown() {
        $this.SelectedIndex = [Math]::Min($this.Items.Count - 1, $this.SelectedIndex + $this.ViewportHeight)
        $maxScroll = [Math]::Max(0, $this.Items.Count - $this.ViewportHeight)
        $this.ScrollOffset = [Math]::Min($maxScroll, $this.ScrollOffset + $this.ViewportHeight)
    }

    [void] Home() {
        $this.SelectedIndex = 0
        $this.ScrollOffset = 0
    }

    [void] End() {
        $this.SelectedIndex = $this.Items.Count - 1
        $this.ScrollOffset = [Math]::Max(0, $this.Items.Count - $this.ViewportHeight)
    }

    # Get currently visible items
    [object[]] GetVisibleItems() {
        if ($this.Items.Count -eq 0) { return @() }

        $endIndex = [Math]::Min($this.ScrollOffset + $this.ViewportHeight, $this.Items.Count)

        if ($this.ScrollOffset -ge $this.Items.Count) {
            return @()
        }

        return $this.Items[$this.ScrollOffset..($endIndex - 1)]
    }

    # Get currently selected item
    [object] GetSelectedItem() {
        if ($this.Items.Count -eq 0 -or $this.SelectedIndex -ge $this.Items.Count) {
            return $null
        }
        return $this.Items[$this.SelectedIndex]
    }

    # Scroll indicators
    [bool] HasItemsAbove() {
        return $this.ScrollOffset -gt 0
    }

    [bool] HasItemsBelow() {
        return ($this.ScrollOffset + $this.ViewportHeight) -lt $this.Items.Count
    }

    [string] GetScrollIndicator([int]$line) {
        if ($this.Items.Count -le $this.ViewportHeight) {
            return " "
        }

        $lastLine = $this.ViewportHeight - 1

        if ($line -eq 0) {
            if ($this.HasItemsAbove()) {
                return "▲"
            } else {
                return " "
            }
        } elseif ($line -eq $lastLine) {
            if ($this.HasItemsBelow()) {
                return "▼"
            } else {
                return " "
            }
        } else {
            return "║"
        }
    }

    # Counter display
    [string] GetCounter() {
        if ($this.Items.Count -eq 0) { return "(0)" }
        $current = $this.SelectedIndex + 1
        $total = $this.Items.Count
        return "($current/$total)"
    }

    # Override HandleKey to support scrolling
    [bool] HandleKey([ConsoleKeyInfo]$key) {
        if (-not $this.IsFocused) { return $false }

        switch ($key.Key) {
            "UpArrow" {
                $this.MoveUp()
                return $true
            }
            "DownArrow" {
                $this.MoveDown()
                return $true
            }
            "PageUp" {
                $this.PageUp()
                return $true
            }
            "PageDown" {
                $this.PageDown()
                return $true
            }
            "Home" {
                $this.Home()
                return $true
            }
            "End" {
                $this.End()
                return $true
            }
        }

        return $false
    }
}

# ═══════════════════════════════════════════════════════════════════════════
# STATIC WIDGET CLASS (Non-scrollable, non-selectable info display)
# ═══════════════════════════════════════════════════════════════════════════

class StaticWidget : DashboardWidget {
    StaticWidget() {
        $this.CanSelect = $false
    }

    # Static widgets just show info, activation might expand detail view
    [void] Activate() {
        # Override to show expanded view if desired
    }
}
