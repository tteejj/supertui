# PmcApplication - Main application wrapper integrating PMC widgets with SpeedTUI
# Handles rendering engine, event loop, and screen management

using namespace System
using namespace System.Collections.Generic

# Load SpeedTUI framework
. "$PSScriptRoot/SpeedTUILoader.ps1"

# Load PMC widget system
. "$PSScriptRoot/widgets/PmcWidget.ps1"
. "$PSScriptRoot/layout/PmcLayoutManager.ps1"
. "$PSScriptRoot/theme/PmcThemeManager.ps1"

<#
.SYNOPSIS
Main application class for PMC TUI

.DESCRIPTION
PmcApplication manages:
- SpeedTUI rendering engine (OptimizedRenderEngine)
- Screen stack and navigation
- Event loop and input handling
- Layout management
- Theme management

.EXAMPLE
$app = [PmcApplication]::new()
$app.PushScreen($taskScreen)
$app.Run()
#>
class PmcApplication {
    # === Core Components ===
    [object]$RenderEngine
    [object]$LayoutManager
    [object]$ThemeManager

    # === Screen Management ===
    [object]$ScreenStack      # Stack of PmcScreen objects
    [object]$CurrentScreen = $null   # Currently active screen

    # === Terminal State ===
    [int]$TermWidth = 80
    [int]$TermHeight = 24
    [bool]$Running = $false

    # === Event Handlers ===
    [scriptblock]$OnTerminalResize = $null
    [scriptblock]$OnError = $null

    # === Constructor ===
    PmcApplication() {
        # Initialize render engine
        $this.RenderEngine = New-Object OptimizedRenderEngine
        $this.RenderEngine.Initialize()

        # Initialize layout manager
        $this.LayoutManager = New-Object PmcLayoutManager

        # Initialize theme manager
        $this.ThemeManager = New-Object PmcThemeManager

        # Initialize screen stack
        $this.ScreenStack = New-Object "System.Collections.Generic.Stack[object]"

        # Get terminal size
        $this._UpdateTerminalSize()
    }

    # === Screen Management ===

    <#
    .SYNOPSIS
    Push a screen onto the stack and make it active

    .PARAMETER screen
    Screen object to push (should have Render() and HandleInput() methods)
    #>
    [void] PushScreen([object]$screen) {
        # Deactivate current screen
        if ($this.CurrentScreen) {
            if ($this.CurrentScreen.PSObject.Methods['OnExit']) {
                $this.CurrentScreen.OnExit()
            }
        }

        # Clear screen to prevent old content from showing through
        [Console]::Write("`e[2J")

        # Push new screen
        $this.ScreenStack.Push($screen)
        $this.CurrentScreen = $screen

        # Initialize screen with render engine
        if ($screen.PSObject.Methods['Initialize']) {
            $screen.Initialize($this.RenderEngine)
        }

        # Apply layout if screen has widgets
        if ($screen.PSObject.Methods['ApplyLayout']) {
            $screen.ApplyLayout($this.LayoutManager, $this.TermWidth, $this.TermHeight)
        }

        # Activate screen
        if ($screen.PSObject.Methods['OnEnter']) {
            $screen.OnEnter()
        }

        # Force full render
        $this._RenderCurrentScreen()
    }

    <#
    .SYNOPSIS
    Pop current screen and return to previous

    .OUTPUTS
    The popped screen object
    #>
    [object] PopScreen() {
        if ($this.ScreenStack.Count -eq 0) {
            return $null
        }

        # Exit current screen
        $poppedScreen = $this.ScreenStack.Pop()
        if ($poppedScreen.PSObject.Methods['OnExit']) {
            $poppedScreen.OnExit()
        }

        # Clear screen to prevent old content from showing through
        [Console]::Write("`e[2J")

        # Restore previous screen
        if ($this.ScreenStack.Count -gt 0) {
            $this.CurrentScreen = $this.ScreenStack.Peek()

            # Re-enter previous screen
            if ($this.CurrentScreen.PSObject.Methods['OnEnter']) {
                $this.CurrentScreen.OnEnter()
            }

            # Force full render
            $this._RenderCurrentScreen()
        } else {
            $this.CurrentScreen = $null
        }

        return $poppedScreen
    }

    <#
    .SYNOPSIS
    Clear screen stack and set a new root screen

    .PARAMETER screen
    New root screen
    #>
    [void] SetRootScreen([object]$screen) {
        # Clear stack
        while ($this.ScreenStack.Count -gt 0) {
            $this.PopScreen()
        }

        # Push new root
        $this.PushScreen($screen)
    }

    # === Rendering ===

    hidden [void] _RenderCurrentScreen() {
        if (-not $this.CurrentScreen) {
            return
        }

        try {
            # USE SPEEDTUI PROPERLY - BeginFrame/WriteAt/EndFrame
            $this.RenderEngine.BeginFrame()

            # Get screen output (ANSI strings with position info)
            if ($this.CurrentScreen.PSObject.Methods['RenderToEngine']) {
                # New method: screen writes directly to engine
                $this.CurrentScreen.RenderToEngine($this.RenderEngine)
            } else {
                # Fallback: screen returns ANSI string, we parse and WriteAt
                $output = $this.CurrentScreen.Render()
                if ($output) {
                    # Parse ANSI positioning and write to engine
                    $this._WriteAnsiToEngine($output)
                }
            }

            # EndFrame does differential rendering
            $this.RenderEngine.EndFrame()

        } catch {
            # Render error - log if Write-PmcTuiLog available
            if (Get-Command Write-PmcTuiLog -ErrorAction SilentlyContinue) {
                Write-PmcTuiLog "Render error: $_" "ERROR"
            }
            if ($this.OnError) {
                & $this.OnError $_
            }
        }
    }

    hidden [void] _WriteAnsiToEngine([string]$ansiOutput) {
        # Parse ANSI cursor positioning and write to engine
        # ANSI format: ESC[row;colH (1-based)
        # WriteAt format: WriteAt(x, y) where x=col-1, y=row-1 (0-based)
        $pattern = "`e\[(\d+);(\d+)H"
        $matches = [regex]::Matches($ansiOutput, $pattern)

        if ($matches.Count -eq 0) {
            # No positioning - write at 0,0
            if ($ansiOutput) {
                $this.RenderEngine.WriteAt(0, 0, $ansiOutput)
            }
            return
        }

        for ($i = 0; $i -lt $matches.Count; $i++) {
            $match = $matches[$i]
            $row = [int]$match.Groups[1].Value
            $col = [int]$match.Groups[2].Value

            # Convert to 0-based coordinates
            $x = $col - 1
            $y = $row - 1

            # Get content after this position marker until next position marker
            $startIndex = $match.Index + $match.Length

            if ($i + 1 -lt $matches.Count) {
                # There's another position marker - content goes until there
                $endIndex = $matches[$i + 1].Index
            } else {
                # Last marker - content goes to end
                $endIndex = $ansiOutput.Length
            }

            $content = $ansiOutput.Substring($startIndex, $endIndex - $startIndex)

            if ($content) {
                $this.RenderEngine.WriteAt($x, $y, $content)
            }
        }
    }

    # === Event Loop ===

    <#
    .SYNOPSIS
    Start the application event loop

    .DESCRIPTION
    Runs until Stop() is called or screen stack is empty
    #>
    [void] Run() {
        $this.Running = $true

        # Hide cursor
        [Console]::CursorVisible = $false

        try {
            # Event loop - render every frame
            while ($this.Running -and $this.ScreenStack.Count -gt 0) {
                # Check for terminal resize
                $currentWidth = [Console]::WindowWidth
                $currentHeight = [Console]::WindowHeight

                if ($currentWidth -ne $this.TermWidth -or $currentHeight -ne $this.TermHeight) {
                    $this._HandleTerminalResize($currentWidth, $currentHeight)
                }

                # Check for input
                if ([Console]::KeyAvailable) {
                    $key = [Console]::ReadKey($true)

                    # Global keys - Ctrl+Q to exit
                    if ($key.Modifiers -eq [ConsoleModifiers]::Control -and $key.Key -eq 'Q') {
                        $this.Stop()
                        continue
                    }

                    # Pass to current screen
                    if ($this.CurrentScreen -and $this.CurrentScreen.PSObject.Methods['HandleKeyPress']) {
                        $this.CurrentScreen.HandleKeyPress($key)
                    }
                }

                # Render every frame - SpeedTUI will diff and only update changes
                $this._RenderCurrentScreen()

                # Small sleep to prevent CPU spinning
                Start-Sleep -Milliseconds 16  # ~60 FPS
            }

        } finally {
            # Cleanup
            [Console]::CursorVisible = $true
            [Console]::Clear()
        }
    }

    <#
    .SYNOPSIS
    Stop the application event loop
    #>
    [void] Stop() {
        $this.Running = $false
    }

    # === Terminal Management ===

    hidden [void] _UpdateTerminalSize() {
        try {
            $this.TermWidth = [Console]::WindowWidth
            $this.TermHeight = [Console]::WindowHeight
        } catch {
            # Fallback to defaults
            $this.TermWidth = 80
            $this.TermHeight = 24
        }
    }

    hidden [void] _HandleTerminalResize([int]$newWidth, [int]$newHeight) {
        $this.TermWidth = $newWidth
        $this.TermHeight = $newHeight

        # Notify current screen
        if ($this.CurrentScreen) {
            if ($this.CurrentScreen.PSObject.Methods['OnTerminalResize']) {
                $this.CurrentScreen.OnTerminalResize($newWidth, $newHeight)
            }

            # Reapply layout
            if ($this.CurrentScreen.PSObject.Methods['ApplyLayout']) {
                $this.CurrentScreen.ApplyLayout($this.LayoutManager, $newWidth, $newHeight)
            }
        }

        # Fire event
        if ($this.OnTerminalResize) {
            & $this.OnTerminalResize $newWidth $newHeight
        }

        # Force full render
        $this._RenderCurrentScreen()
    }

    # === Utility Methods ===

    <#
    .SYNOPSIS
    Get current terminal size

    .OUTPUTS
    Hashtable with Width and Height properties
    #>
    [hashtable] GetTerminalSize() {
        return @{
            Width = $this.TermWidth
            Height = $this.TermHeight
        }
    }

    <#
    .SYNOPSIS
    Request a render on next frame

    .DESCRIPTION
    Schedules a re-render of the current screen
    #>
    [void] RequestRender() {
        if ($this.Running) {
            $this._RenderCurrentScreen()
        }
    }
}

# Classes exported automatically in PowerShell 5.1+
