# DemoScreen - Simple demonstration screen for Phase 2 integration
# Shows all widgets working with SpeedTUI rendering

. "$PSScriptRoot/PmcApplication.ps1"
. "$PSScriptRoot/PmcScreen.ps1"

<#
.SYNOPSIS
Demo screen showing Phase 2 integration

.DESCRIPTION
Demonstrates:
- PmcScreen base class
- All widgets rendering via SpeedTUI
- MenuBar with dropdowns
- Input handling
- Layout management
#>
class DemoScreen : PmcScreen {
    # Custom properties
    [PmcPanel]$InfoPanel

    # Constructor
    DemoScreen() : base("Demo", "PMC Widget Demo") {
        # Configure header
        $this.Header.SetIcon("⚡")
        $this.Header.SetBreadcrumb(@("Home", "Demo", "Phase 2"))
        $this.Header.SetContext("SpeedTUI Integration")

        # Configure footer
        $this.Footer.ClearShortcuts()
        $this.Footer.AddShortcut("F10", "Menu")
        $this.Footer.AddShortcut("↑↓", "Navigate")
        $this.Footer.AddShortcut("Q", "Quit")
        $this.Footer.AddShortcut("Esc", "Exit")

        # Create menu bar
        $this._CreateMenu()

        # Create info panel
        $this.InfoPanel = [PmcPanel]::new("Phase 2 Implementation Status", 60, 15)
        $this.InfoPanel.SetBorderStyle('rounded')
        $this.InfoPanel.SetPadding(2)

        # Add to content widgets
        $this.AddContentWidget($this.InfoPanel)
    }

    hidden [void] _CreateMenu() {
        $this.MenuBar = [PmcMenuBar]::new()

        # File menu
        $fileItems = @(
            [PmcMenuItem]::new('New', 'N', { $this.ShowStatus("New selected") })
            [PmcMenuItem]::new('Open', 'O', { $this.ShowStatus("Open selected") })
            [PmcMenuItem]::Separator()
            [PmcMenuItem]::new('Exit', 'X', { $this.ShowStatus("Exiting..."); Start-Sleep -Milliseconds 500; exit })
        )
        $this.MenuBar.AddMenu('File', 'F', $fileItems)

        # View menu
        $viewItems = @(
            [PmcMenuItem]::new('Tasks', 'T', { $this.ShowStatus("Tasks view selected") })
            [PmcMenuItem]::new('Projects', 'P', { $this.ShowStatus("Projects view selected") })
            [PmcMenuItem]::new('Calendar', 'C', { $this.ShowStatus("Calendar view selected") })
        )
        $this.MenuBar.AddMenu('View', 'V', $viewItems)

        # Theme menu
        $themeItems = @(
            [PmcMenuItem]::new('Ocean', 'O', { $this._SetTheme('#33aaff') })
            [PmcMenuItem]::new('Matrix', 'M', { $this._SetTheme('#00ff66') })
            [PmcMenuItem]::new('Amber', 'A', { $this._SetTheme('#ffbf00') })
            [PmcMenuItem]::new('Synthwave', 'S', { $this._SetTheme('#ff2bd6') })
        )
        $this.MenuBar.AddMenu('Theme', 'T', $themeItems)

        # Help menu
        $helpItems = @(
            [PmcMenuItem]::new('About', 'A', { $this._ShowAbout() })
            [PmcMenuItem]::new('Controls', 'C', { $this._ShowControls() })
        )
        $this.MenuBar.AddMenu('Help', 'H', $helpItems)
    }

    [void] LoadData() {
        $this.ShowStatus("Loading demo data...")

        # Update panel content with implementation status
        $content = @"
✅ SpeedTUI Integration Complete

Core Systems:
  ✓ SpeedTUILoader.ps1 - Framework loading
  ✓ PmcApplication.ps1 - App wrapper
  ✓ PmcScreen.ps1 - Base screen class
  ✓ OptimizedRenderEngine - Wired

Widgets:
  ✓ PmcWidget - Base class
  ✓ PmcMenuBar - With dropdowns
  ✓ PmcHeader - Title + breadcrumb
  ✓ PmcFooter - Keyboard shortcuts
  ✓ PmcStatusBar - 3-section status
  ✓ PmcPanel - Bordered container

Phase 2: COMPLETE
"@
        $this.InfoPanel.SetContent($content, 'left')

        $this.ShowStatus("Ready")
    }

    [void] ApplyContentLayout([PmcLayoutManager]$layoutManager, [int]$termWidth, [int]$termHeight) {
        # Position info panel in content area
        $contentRect = $layoutManager.GetRegion('Content', $termWidth, $termHeight)

        # Center panel in content area
        $panelX = $contentRect.X + [Math]::Floor(($contentRect.Width - $this.InfoPanel.Width) / 2)
        $panelY = $contentRect.Y + 2

        $this.InfoPanel.SetPosition($panelX, $panelY)
    }

    [bool] HandleInput([ConsoleKeyInfo]$keyInfo) {
        switch ($keyInfo.Key) {
            'Q' {
                $this.ShowStatus("Quitting...")
                Start-Sleep -Milliseconds 500
                exit
            }
            'R' {
                $this.ShowStatus("Reloading...")
                $this.LoadData()
                return $true
            }
            default {
                return $false
            }
        }
    }

    hidden [void] _SetTheme([string]$hex) {
        try {
            $theme = [PmcThemeManager]::GetInstance()
            $theme.SetTheme($hex)
            $this.ShowSuccess("Theme changed to $hex")
        } catch {
            $this.ShowError("Failed to change theme: $_")
        }
    }

    hidden [void] _ShowAbout() {
        $this.InfoPanel.SetContent(@"
PMC Widget Library
Phase 2: SpeedTUI Integration

Built: 2025-11-05
Architecture: Hybrid PMC + SpeedTUI
Rendering: OptimizedRenderEngine
Performance: <20ms frame times

Foundation complete.
Ready for screen migration.
"@, 'center')
        $this.ShowStatus("About PMC Widget Library")
    }

    hidden [void] _ShowControls() {
        $this.InfoPanel.SetContent(@"
Keyboard Controls

F10          Activate menu bar
↑↓←→         Navigate menus/items
Enter        Select menu item
Esc          Close menu / Exit
Q            Quit application
R            Reload data

Theme Menu:
  Change color scheme on the fly
"@, 'left')
        $this.ShowStatus("Keyboard controls")
    }
}

<#
.SYNOPSIS
Run the demo screen

.DESCRIPTION
Creates application, pushes demo screen, and runs event loop
#>
function Start-PmcDemo {
    Write-Host "Starting PMC Demo..." -ForegroundColor Cyan

    try {
        # Create application
        $app = [PmcApplication]::new()

        # Create and push demo screen
        $screen = [DemoScreen]::new()
        $app.PushScreen($screen)

        # Run event loop
        $app.Run()

    } catch {
        Write-Host "`e[?25h"  # Show cursor on error
        Write-Host "`nDemo error: $_" -ForegroundColor Red
        Write-Host $_.ScriptStackTrace -ForegroundColor Red
        throw
    }
}

# Allow running directly
if ($MyInvocation.InvocationName -eq './DemoScreen.ps1' -or $MyInvocation.InvocationName -eq 'DemoScreen.ps1') {
    Start-PmcDemo
}
