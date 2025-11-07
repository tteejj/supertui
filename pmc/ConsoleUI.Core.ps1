# PMC ConsoleUI - All-in-one loader to handle PowerShell class dependencies
# Load all classes and functions in proper order for module compatibility

using namespace System.Collections.Generic
using namespace System.Collections.Concurrent
using namespace System.Text

# === LOAD REQUIRED FUNCTIONS (standalone) ===
# Load only local copies; no external references allowed
. (Join-Path $PSScriptRoot 'DepsLoader.ps1')

# Load handlers
. (Join-Path $PSScriptRoot 'Handlers/TaskHandlers.ps1')
. (Join-Path $PSScriptRoot 'Handlers/ProjectHandlers.ps1')
. (Join-Path $PSScriptRoot 'deps/Project.ps1')
try {
    . (Join-Path $PSScriptRoot 'Handlers/ExcelHandlers.ps1')
    . (Join-Path $PSScriptRoot 'Handlers/ExcelImportHandlers.ps1')
    . (Join-Path $PSScriptRoot 'deps/Excel.ps1')
} catch {
    Write-ConsoleUIDebug "Excel handlers not loaded: $_" "WARN"
}

# Initialize core systems
Initialize-PmcSecuritySystem
Initialize-PmcThemeSystem

# Compute a safe, static default root for file pickers (avoid per-method OS checks)
try {
    $Script:DefaultPickerRoot = '/'
    $isWin = $false
    try { if ($env:OS -like '*Windows*') { $isWin = $true } } catch {}
    if (-not $isWin) {
        try { if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) { $isWin = $true } } catch {}
    }
    if ($isWin) { $Script:DefaultPickerRoot = 'C:\' }
    if (-not (Test-Path $Script:DefaultPickerRoot)) { $Script:DefaultPickerRoot = (Get-Location).Path }
} catch { $Script:DefaultPickerRoot = (Get-Location).Path }

# Error handling preferences (scoped to this script/session to avoid global side effects)
$Script:_PrevErrorActionPreference = $ErrorActionPreference
try { $ErrorActionPreference = 'Continue' } catch {}
# Trap disabled for standalone debugging - errors will show actual message
# trap {
#     try {
#         Write-ConsoleUIDebug ("TRAP: {0} | STACK: {1}" -f $_.Exception.Message, $_.ScriptStackTrace) 'TRAP'
#     } catch {}
#     throw
# }

# === HELPERS ===
function Get-ConsoleUIDateOrNull {
    param([object]$Value)
    try {
        if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
        $out = [datetime]::MinValue
        if ([DateTime]::TryParse([string]$Value, [ref]$out)) { return $out }
    } catch {}
    return $null
}

# Normalize flexible date input to ISO yyyy-MM-dd or '' if empty; returns $null if invalid
function Normalize-ConsoleUIDate {
    param([string]$Input)
    if ([string]::IsNullOrWhiteSpace($Input)) { return '' }
    $s = $Input.Trim()
    $lower = $s.ToLower()
    $d = $null
    try {
        if ($lower -eq 'today') { $d = (Get-Date).Date }
        elseif ($lower -eq 'tomorrow') { $d = (Get-Date).Date.AddDays(1) }
        elseif ($lower -eq 'yesterday') { $d = (Get-Date).Date.AddDays(-1) }
        elseif ($s -match '^[+-]\d+$') { $d = (Get-Date).Date.AddDays([int]$s) }
        elseif ($s -match '^\d{8}$') {
            # yyyymmdd
            $d = [datetime]::ParseExact($s,'yyyyMMdd',$null)
        }
        elseif ($s -match '^\d{4}$') {
            # mmdd, assume current year
            $year = (Get-Date).Year
            $mm = [int]$s.Substring(0,2)
            $dd = [int]$s.Substring(2,2)
            $d = Get-Date -Year $year -Month $mm -Day $dd
        }
        elseif ($s -match '^\d{4}-\d{2}-\d{2}$') {
            $d = [datetime]::ParseExact($s,'yyyy-MM-dd',$null)
        }
        elseif ($s -match '^\d{4}/\d{2}/\d{2}$') {
            $d = [datetime]::ParseExact($s,'yyyy/MM/dd',$null)
        } else {
            # last resort attempt
            $tmp = [datetime]::MinValue
            if ([DateTime]::TryParse($s, [ref]$tmp)) { $d = $tmp.Date }
        }
    } catch { $d = $null }
    if ($d) { return $d.ToString('yyyy-MM-dd') } else { return $null }
}

function Truncate-ConsoleUIText {
    param([string]$text, [int]$maxWidth)
    if ($text.Length -gt $maxWidth) {
        return $text.Substring(0, $maxWidth - 1) + '…'
    }
    return $text.PadRight($maxWidth)
}

function Test-ConsoleInteractive {
    try {
        if ([Console]::IsInputRedirected) { return $false }
        if ([Console]::IsOutputRedirected) { return $false }
    } catch { return $false }
    return $true
}

# === PERFORMANCE CORE ===
class PmcStringCache {
    static [hashtable]$_spaces = @{}
    static [hashtable]$_ansiSequences = @{}
    static [hashtable]$_boxDrawing = @{}
    static [int]$_maxCacheSize = 200
    static [bool]$_initialized = $false

    static [void] Initialize() {
        if ([PmcStringCache]::_initialized) { return }

        for ($i = 1; $i -le [PmcStringCache]::_maxCacheSize; $i++) {
            [PmcStringCache]::_spaces[$i] = " " * $i
        }

        [PmcStringCache]::_ansiSequences["reset"] = "`e[0m"
        [PmcStringCache]::_ansiSequences["clear"] = "`e[2J"
        [PmcStringCache]::_ansiSequences["clearline"] = "`e[2K"
        [PmcStringCache]::_ansiSequences["home"] = "`e[H"
        [PmcStringCache]::_ansiSequences["hidecursor"] = "`e[?25l"
        [PmcStringCache]::_ansiSequences["showcursor"] = "`e[?25h"

        [PmcStringCache]::_boxDrawing["horizontal"] = "─"
        [PmcStringCache]::_boxDrawing["vertical"] = "│"
        [PmcStringCache]::_boxDrawing["topleft"] = "┌"
        [PmcStringCache]::_boxDrawing["topright"] = "┐"
        [PmcStringCache]::_boxDrawing["bottomleft"] = "└"
        [PmcStringCache]::_boxDrawing["bottomright"] = "┘"

        [PmcStringCache]::_initialized = $true
    }

    static [string] GetSpaces([int]$count) {
        if ($count -le 0) { return "" }
        if ($count -le [PmcStringCache]::_maxCacheSize) {
            return [PmcStringCache]::_spaces[$count]
        }
        return " " * $count
    }

    static [string] GetAnsiSequence([string]$sequenceName) {
        if ([PmcStringCache]::_ansiSequences.ContainsKey($sequenceName)) {
            return [PmcStringCache]::_ansiSequences[$sequenceName]
        }
        return ""
    }

    static [string] GetBoxDrawing([string]$characterName) {
        if ([PmcStringCache]::_boxDrawing.ContainsKey($characterName)) {
            return [PmcStringCache]::_boxDrawing[$characterName]
        }
        return ""
    }
}

class PmcStringBuilderPool {
    static [ConcurrentQueue[StringBuilder]]$_pool = [ConcurrentQueue[StringBuilder]]::new()
    static [int]$_maxPoolSize = 20
    static [int]$_maxCapacity = 8192

    static [StringBuilder] Get() {
        $sb = $null
        if ([PmcStringBuilderPool]::_pool.TryDequeue([ref]$sb)) {
            $sb.Clear()
        } else {
            $sb = [StringBuilder]::new()
        }
        return $sb
    }

    static [StringBuilder] Get([int]$initialCapacity) {
        $sb = [PmcStringBuilderPool]::Get()
        if ($sb.Capacity -lt $initialCapacity) {
            $sb.Capacity = $initialCapacity
        }
        return $sb
    }

    static [void] Return([StringBuilder]$sb) {
        if (-not $sb) { return }
        if ($sb.Capacity -gt [PmcStringBuilderPool]::_maxCapacity) { return }
        if ([PmcStringBuilderPool]::_pool.Count -ge [PmcStringBuilderPool]::_maxPoolSize) { return }
        $sb.Clear()
        [PmcStringBuilderPool]::_pool.Enqueue($sb)
    }
}

# === THEME SYSTEM (Unified) ===
# Adapter to the centralized theme in deps/UI.ps1 + deps/Theme.ps1
class PmcVT100 {
    static [hashtable]$_colorCache = @{}
    static [int]$_maxColorCache = 200

    static hidden [string] _AnsiFromHex([string]$hex, [bool]$bg=$false) {
        if (-not $hex) { return '' }
        try {
            if (-not $hex.StartsWith('#')) { $hex = '#'+$hex }
            $rgb = ConvertFrom-PmcHex $hex
            $key = "{0}_{1}_{2}_{3}" -f ($bg ? 'bg' : 'fg'), $rgb.R, $rgb.G, $rgb.B
            if ([PmcVT100]::_colorCache.ContainsKey($key)) { return [PmcVT100]::_colorCache[$key] }
            $seq = if ($bg) { "`e[48;2;$($rgb.R);$($rgb.G);$($rgb.B)m" } else { "`e[38;2;$($rgb.R);$($rgb.G);$($rgb.B)m" }
            if ([PmcVT100]::_colorCache.Count -lt [PmcVT100]::_maxColorCache) { [PmcVT100]::_colorCache[$key] = $seq }
            return $seq
        } catch { return '' }
    }

    static hidden [string] _MapColor([string]$name, [bool]$bg=$false) {
        # Map legacy ConsoleUI color names to centralized style tokens
        $styles = Get-PmcState -Section 'Display' -Key 'Styles'
        $token = switch ($name) {
            'Red'      { 'Error' }
            'Green'    { 'Success' }
            'Yellow'   { 'Warning' }
            'Blue'     { 'Header' }
            'Cyan'     { 'Info' }
            'White'    { 'Body' }
            'Gray'     { 'Muted' }
            'Black'    { $null }
            'BgRed'    { 'Error' }
            'BgGreen'  { 'Success' }
            'BgYellow' { 'Warning' }
            'BgBlue'   { 'Header' }
            'BgCyan'   { 'Info' }
            'BgWhite'  { 'Body' }
            default    { 'Body' }
        }
        if ($null -eq $token) { return ($bg ? "`e[48;2;0;0;0m" : "`e[38;2;0;0;0m") }
        if ($styles -and $styles.ContainsKey($token)) {
            $fg = $styles[$token].Fg
            return [PmcVT100]::_AnsiFromHex($fg, $bg)
        }
        # Fallback to theme palette primary color
        $palette = Get-PmcColorPalette
        $hex = '#33aaff'
        try { $hex = ("#{0:X2}{1:X2}{2:X2}" -f $palette.Primary.R,$palette.Primary.G,$palette.Primary.B) } catch {}
        return [PmcVT100]::_AnsiFromHex($hex, $bg)
    }

    static [string] MoveTo([int]$x, [int]$y) { return "`e[$($y + 1);$($x + 1)H" }
    static [string] Reset() { return "`e[0m" }
    static [string] Bold() { return "`e[1m" }
    static [string] Red() { return [PmcVT100]::_MapColor('Red', $false) }
    static [string] Green() { return [PmcVT100]::_MapColor('Green', $false) }
    static [string] Yellow() { return [PmcVT100]::_MapColor('Yellow', $false) }
    static [string] Blue() { return [PmcVT100]::_MapColor('Blue', $false) }
    static [string] Cyan() { return [PmcVT100]::_MapColor('Cyan', $false) }
    static [string] White() { return [PmcVT100]::_MapColor('White', $false) }
    static [string] Gray() { return [PmcVT100]::_MapColor('Gray', $false) }
    static [string] Black() { return [PmcVT100]::_MapColor('Black', $false) }
    static [string] BgRed() { return [PmcVT100]::_MapColor('BgRed', $true) }
    static [string] BgGreen() { return [PmcVT100]::_MapColor('BgGreen', $true) }
    static [string] BgYellow() { return [PmcVT100]::_MapColor('BgYellow', $true) }
    static [string] BgBlue() { return [PmcVT100]::_MapColor('BgBlue', $true) }
    static [string] BgCyan() { return [PmcVT100]::_MapColor('BgCyan', $true) }
    static [string] BgWhite() { return [PmcVT100]::_MapColor('BgWhite', $true) }
}

# === UI WIDGET FUNCTIONS ===
# Simple, reusable UI components for blocking forms

function Show-InfoMessage {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,
        [string]$Title = "Information",
        [string]$Color = "Cyan"
    )

    $terminal = [PmcSimpleTerminal]::GetInstance()
    $terminal.Clear()

    # Draw box
    $boxWidth = [Math]::Min(60, $terminal.Width - 4)
    $boxX = ($terminal.Width - $boxWidth) / 2
    $terminal.DrawBox($boxX, 8, $boxWidth, 8)

    # Draw title
    $titleX = ($terminal.Width - $Title.Length) / 2
    $colorCode = switch ($Color) {
        "Red" { [PmcVT100]::Red() }
        "Green" { [PmcVT100]::Green() }
        "Yellow" { [PmcVT100]::Yellow() }
        default { [PmcVT100]::Cyan() }
    }
    $terminal.WriteAtColor([int]$titleX, 8, " $Title ", [PmcVT100]::BgBlue(), [PmcVT100]::White())

    # Draw message (word wrap)
    $y = 10
    $maxWidth = $boxWidth - 4
    $words = $Message -split '\s+'
    $line = ""
    foreach ($word in $words) {
        if (($line + " " + $word).Length -gt $maxWidth) {
            $terminal.WriteAtColor([int]($boxX + 2), $y++, $line, $colorCode, "")
            $line = $word
        } else {
            $line = if ($line) { "$line $word" } else { $word }
        }
    }
    if ($line) {
        $terminal.WriteAtColor([int]($boxX + 2), $y++, $line, $colorCode, "")
    }

    # Brief display; non-blocking
}

function Show-ConfirmDialog {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,
        [string]$Title = "Confirm"
    )

    $terminal = [PmcSimpleTerminal]::GetInstance()
    $terminal.Clear()

    # Draw box
    $boxWidth = [Math]::Min(60, $terminal.Width - 4)
    $boxX = ($terminal.Width - $boxWidth) / 2
    $terminal.DrawBox($boxX, 8, $boxWidth, 8)

    # Draw title
    $titleX = ($terminal.Width - $Title.Length) / 2
    $terminal.WriteAtColor([int]$titleX, 8, " $Title ", [PmcVT100]::BgBlue(), [PmcVT100]::White())

    # Draw message
    $terminal.WriteAtColor([int]($boxX + 2), 10, $Message, [PmcVT100]::Yellow(), "")

    # Prompt
    $terminal.WriteAt([int]($boxX + 2), 13, "Y/N: ")

    while ($true) {
        try {
            $key = [Console]::ReadKey($true)
        } catch {
            # Non-interactive environment: exit the special view loop gracefully
            $this.running = $false
            return
        }
        if ($key.KeyChar -eq 'y' -or $key.KeyChar -eq 'Y') {
            return $true
        } elseif ($key.KeyChar -eq 'n' -or $key.KeyChar -eq 'N' -or $key.Key -eq 'Escape') {
            return $false
        }
    }
}

function Show-SelectList {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Title,
        [Parameter(Mandatory=$true)]
        [string[]]$Options,
        [string]$DefaultValue = $null
    )

    $terminal = [PmcSimpleTerminal]::GetInstance()
    $terminal.Clear()

    # Find default index
    $selected = 0
    if ($DefaultValue) {
        $idx = [Array]::IndexOf(@($Options), $DefaultValue)
        if ($idx -ge 0) { $selected = $idx }
    }

    # Draw box
    $boxWidth = [Math]::Min(60, $terminal.Width - 4)
    $boxHeight = [Math]::Min(20, 8 + $Options.Count)
    $boxX = ($terminal.Width - $boxWidth) / 2
    $terminal.DrawBox($boxX, 5, $boxWidth, $boxHeight)

    # Draw title
    $titleX = ($terminal.Width - $Title.Length) / 2
    $terminal.WriteAtColor([int]$titleX, 5, " $Title ", [PmcVT100]::BgBlue(), [PmcVT100]::White())

    $running = $true
    while ($running) {
        # Draw options
        $y = 7
        $maxDisplay = $boxHeight - 5
        $startIdx = [Math]::Max(0, $selected - $maxDisplay + 1)

        for ($i = 0; $i -lt [Math]::Min($Options.Count, $maxDisplay); $i++) {
            $idx = $startIdx + $i
            if ($idx -ge $Options.Count) { break }

            $opt = $Options[$idx]
            if ($idx -eq $selected) {
                $terminal.WriteAtColor([int]($boxX + 2), $y, "> $opt", [PmcVT100]::BgCyan(), [PmcVT100]::Black())
            } else {
                $terminal.WriteAt([int]($boxX + 2), $y, "  $opt")
            }
            $y++
        }

        # Draw footer
        $terminal.WriteAt([int]($boxX + 2), [int](5 + $boxHeight - 2), "↑/↓: Navigate  Enter: Select  Esc: Cancel")

        # Handle input
        try {
            $key = [Console]::ReadKey($true)
        } catch {
            $this.running = $false
            return
        }
        switch ($key.Key) {
            'UpArrow' {
                $selected = [Math]::Max(0, $selected - 1)
            }
            'DownArrow' {
                $selected = [Math]::Min($Options.Count - 1, $selected + 1)
            }
            'Enter' {
                return $Options[$selected]
            }
            'Escape' {
                return $null
            }
        }
    }
}

function Show-InputForm {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Title,
        [Parameter(Mandatory=$true)]
        [hashtable[]]$Fields  # Array of @{Name='fieldname'; Label='Display label'; Required=$true; Type='text'|'select'; Options=@()}
    )

    $terminal = [PmcSimpleTerminal]::GetInstance()
    $terminal.Clear()

    # Normalize fields into a list of simple objects with mutable Value
    $norm = @()
    foreach ($f in $Fields) {
        $label = $null; $name = $null; $required = $false; $type = 'text'; $options = $null; $value = ''
        if ($f -is [hashtable]) {
            $label = [string]$f['Label']; $name = [string]$f['Name']
            $required = try { [bool]$f['Required'] } catch { $false }
            $type = if ($f.ContainsKey('Type') -and $f['Type']) { [string]$f['Type'] } else { 'text' }
            $options = if ($f.ContainsKey('Options')) { $f['Options'] } else { $null }
        } else {
            $label = [string]$f.Label; $name = [string]$f.Name
            $required = if ($f.PSObject.Properties['Required'] -and $f.Required) { [bool]$f.Required } else { $false }
            $type = if ($f.PSObject.Properties['Type'] -and $f.Type) { [string]$f.Type } else { 'text' }
            $options = if ($f.PSObject.Properties['Options']) { $f.Options } else { $null }
        }
        $norm += [pscustomobject]@{ Label=$label; Name=$name; Required=$required; Type=$type; Options=$options; Value=$value }
    }

    $active = 0
    $done = $false
    while (-not $done) {
        # Layout
        $boxWidth = [Math]::Min(78, $terminal.Width - 4)
        $boxHeight = [Math]::Min($terminal.Height - 6, 10 + $norm.Count * 2)
        $boxX = ($terminal.Width - $boxWidth) / 2
        $terminal.Clear()
        $terminal.DrawBox($boxX, 5, $boxWidth, $boxHeight)
        $titleX = ($terminal.Width - $Title.Length) / 2
        $terminal.WriteAtColor([int]$titleX, 5, " $Title ", [PmcVT100]::BgBlue(), [PmcVT100]::White())

        # Render fields
        $y = 7
        for ($i=0; $i -lt $norm.Count; $i++) {
            $f = $norm[$i]
            $isActive = ($i -eq $active)
            $labelColor = if ($isActive) { [PmcVT100]::Yellow() } else { [PmcVT100]::Cyan() }
            $star = ''
            if ($f.Required) { $star = ' *' }
            $labelText = ("{0}:{1}" -f $f.Label, $star)
            $terminal.WriteAtColor([int]($boxX + 2), $y, $labelText, $labelColor, "")
            $val = [string]$f.Value
            if ($f.Type -eq 'select' -and $val -eq '') { $val = '(choose)' }
            if ($isActive) {
                $terminal.FillArea([int]($boxX + 2), $y+1, $boxWidth-4, 1, ' ')
                $terminal.WriteAtColor([int]($boxX + 2), $y+1, $val, [PmcVT100]::White(), "")
            } else {
                $terminal.WriteAt([int]($boxX + 2), $y+1, $val)
            }
            $y += 2
        }

        $terminal.WriteAt([int]($boxX + 2), [int](5 + $boxHeight - 2), "Tab/Shift+Tab navigate | Enter saves | Esc cancels")

        # Position cursor at end of active value (value row is at 8 + 2*i)
        $curY = 8 + ($active * 2)
        $curX = [int]($boxX + 2 + ([string]$norm[$active].Value).Length)
        try { [Console]::SetCursorPosition($curX, $curY) } catch {}

        # Read input
        $k = [Console]::ReadKey($true)
        if ($k.Key -eq 'Escape') { return $null }
        elseif ($k.Key -eq 'Tab') {
            $isShift = ("" + $k.Modifiers) -match 'Shift'
            if ($isShift) { $active = ($active - 1); if ($active -lt 0) { $active = $norm.Count - 1 } }
            else { $active = ($active + 1) % $norm.Count }
            continue
        }
        elseif ($k.Key -eq 'Enter') {
            $field = $norm[$active]
            if ($field.Type -eq 'select' -and $field.Options) {
                $sel = Show-SelectList -Title $field.Label -Options $field.Options
                if ($null -ne $sel) { $field.Value = [string]$sel }
                continue
            }
            # If not on a select field, Enter attempts to submit
            $allOk = $true
            foreach ($f in $norm) { if ($f.Required -and [string]::IsNullOrWhiteSpace([string]$f.Value)) { $allOk = $false; break } }
            if ($allOk) {
                $out = @{}
                foreach ($f in $norm) { $out[$f.Name] = [string]$f.Value }
                return $out
            } else {
                # Focus first missing required
                for ($i=0; $i -lt $norm.Count; $i++) { if ($norm[$i].Required -and [string]::IsNullOrWhiteSpace([string]$norm[$i].Value)) { $active = $i; break } }
                continue
            }
        }
        elseif ($k.Key -eq 'Backspace') {
            $v = [string]$norm[$active].Value
            if ($v.Length -gt 0) { $norm[$active].Value = $v.Substring(0, $v.Length - 1) }
            continue
        } else {
            $ch = $k.KeyChar
            if ($ch -and $ch -ne "`0") { $norm[$active].Value = ([string]$norm[$active].Value) + $ch }
            continue
        }
    }
}

# === SIMPLE TERMINAL ===
class PmcSimpleTerminal {
    static [PmcSimpleTerminal]$Instance = $null
    [int]$Width
    [int]$Height
    [bool]$CursorVisible = $true
    [System.Text.StringBuilder]$buffer = $null
    [bool]$buffering = $false
    [hashtable]$dirtyRegions = @{}

    hidden PmcSimpleTerminal() {
        $this.UpdateDimensions()
        $this.buffer = [System.Text.StringBuilder]::new(8192)
    }

    static [PmcSimpleTerminal] GetInstance() {
        if ($null -eq [PmcSimpleTerminal]::Instance) {
            [PmcSimpleTerminal]::Instance = [PmcSimpleTerminal]::new()
        }
        return [PmcSimpleTerminal]::Instance
    }

    [void] Initialize() {
        [Console]::Clear()
        try {
            # Keep cursor visible during interactive forms so users can see focus
            [Console]::CursorVisible = $true
            $this.CursorVisible = $true
        } catch { }
        $this.UpdateDimensions()
        [Console]::SetCursorPosition(0, 0)
    }

    [void] Cleanup() {
        try {
            [Console]::CursorVisible = $true
            $this.CursorVisible = $true
        } catch { }
        [Console]::Clear()
    }

    [void] UpdateDimensions() {
        try {
            $this.Width = [Console]::WindowWidth
            $this.Height = [Console]::WindowHeight
        } catch {
            $this.Width = 120
            $this.Height = 30
        }
    }

    [void] Clear() {
        if ($this.buffering) {
            # In buffering mode, queue a clear sequence
            $this.buffer.Append([PraxisVT]::Clear()) | Out-Null
            $this.dirtyRegions.Clear()
        } else {
            [Console]::Clear()
            [Console]::SetCursorPosition(0, 0)
        }
    }

    [void] BeginFrame() {
        $this.buffering = $true
        $this.buffer.Clear() | Out-Null
        $this.dirtyRegions.Clear()
    }

    [void] EndFrame() {
        if ($this.buffering -and $this.buffer.Length -gt 0) {
            # Write entire buffered frame at once (double buffering)
            [Console]::SetCursorPosition(0, 0)
            [Console]::Write($this.buffer.ToString())
        }
        $this.buffering = $false
    }

    [void] WriteAt([int]$x, [int]$y, [string]$text) {
        if ([string]::IsNullOrEmpty($text) -or $x -lt 0 -or $y -lt 0 -or $x -ge $this.Width -or $y -ge $this.Height) { return }
        $maxLength = $this.Width - $x
        if ($text.Length -gt $maxLength) { $text = $text.Substring(0, $maxLength) }

        if ($this.buffering) {
            # Append to buffer with VT100 positioning
            $this.buffer.Append([PraxisVT]::MoveTo($y + 1, $x + 1)).Append($text) | Out-Null
            $regionKey = "$y,$x"
            $this.dirtyRegions[$regionKey] = $true
        } else {
            [Console]::SetCursorPosition($x, $y)
            [Console]::Write($text)
        }
    }

    [void] WriteAtColor([int]$x, [int]$y, [string]$text, [string]$foreground, [string]$background = "") {
        if ([string]::IsNullOrEmpty($text) -or $x -lt 0 -or $y -lt 0 -or $x -ge $this.Width -or $y -ge $this.Height) { return }
        $maxLength = $this.Width - $x
        if ($text.Length -gt $maxLength) { $text = $text.Substring(0, $maxLength) }
        $colored = $foreground
        if (-not [string]::IsNullOrEmpty($background)) { $colored += $background }
        $colored += $text + [PmcVT100]::Reset()

        if ($this.buffering) {
            # Append to buffer with VT100 positioning and color
            $this.buffer.Append([PraxisVT]::MoveTo($y + 1, $x + 1)).Append($colored) | Out-Null
            $regionKey = "$y,$x"
            $this.dirtyRegions[$regionKey] = $true
        } else {
            [Console]::SetCursorPosition($x, $y)
            [Console]::Write($colored)
        }
    }

    [void] FillArea([int]$x, [int]$y, [int]$width, [int]$height, [char]$ch = ' ') {
        if ($width -le 0 -or $height -le 0) { return }
        $line = if ($ch -eq ' ') { [PmcStringCache]::GetSpaces($width) } else { [string]::new($ch, $width) }
        for ($row = 0; $row -lt $height; $row++) {
            $currentY = $y + $row
            if ($currentY -ge $this.Height) { break }
            $this.WriteAt($x, $currentY, $line)
        }
    }

    [void] DrawBox([int]$x, [int]$y, [int]$width, [int]$height) {
        if ($width -lt 2 -or $height -lt 2) { return }
        if ($x + $width -gt $this.Width -or $y + $height -gt $this.Height) { return }

        $tl = [PmcStringCache]::GetBoxDrawing("topleft")
        $tr = [PmcStringCache]::GetBoxDrawing("topright")
        $bl = [PmcStringCache]::GetBoxDrawing("bottomleft")
        $br = [PmcStringCache]::GetBoxDrawing("bottomright")
        $h = [PmcStringCache]::GetBoxDrawing("horizontal")
        $v = [PmcStringCache]::GetBoxDrawing("vertical")

        $topLine = $tl + ([PmcStringCache]::GetSpaces($width - 2).Replace(' ', $h)) + $tr
        $bottomLine = $bl + ([PmcStringCache]::GetSpaces($width - 2).Replace(' ', $h)) + $br

        $this.WriteAtColor($x, $y, $topLine, [PmcVT100]::Cyan(), "")
        for ($row = 1; $row -lt $height - 1; $row++) {
            $this.WriteAtColor($x, $y + $row, $v, [PmcVT100]::Cyan(), "")
            $this.WriteAtColor($x + $width - 1, $y + $row, $v, [PmcVT100]::Cyan(), "")
        }
        $this.WriteAtColor($x, $y + $height - 1, $bottomLine, [PmcVT100]::Cyan(), "")
    }

    [void] DrawFilledBox([int]$x, [int]$y, [int]$width, [int]$height, [bool]$border = $true) {
        $this.FillArea($x, $y, $width, $height, ' ')
        if ($border) { $this.DrawBox($x, $y, $width, $height) }
    }

    [void] DrawHorizontalLine([int]$x, [int]$y, [int]$length) {
        if ($length -le 0) { return }
        $h = [PmcStringCache]::GetBoxDrawing("horizontal")
        $line = [PmcStringCache]::GetSpaces($length).Replace(' ', $h)
        $this.WriteAtColor($x, $y, $line, [PmcVT100]::Cyan(), "")
    }

    [void] DrawFooter([string]$content) {
        $this.FillArea(0, $this.Height - 1, $this.Width, 1, ' ')
        $this.WriteAtColor(2, $this.Height - 1, $content, [PmcVT100]::Cyan(), "")
    }
}

# === MENU SYSTEM ===
class PmcMenuItem {
    [string]$Label
    [string]$Action
    [char]$Hotkey
    [bool]$Enabled = $true
    [bool]$Separator = $false

    PmcMenuItem([string]$label, [string]$action, [char]$hotkey) {
        $this.Label = $label
        $this.Action = $action
        $this.Hotkey = $hotkey
    }

    static [PmcMenuItem] Separator() {
        $item = [PmcMenuItem]::new("", "", ' ')
        $item.Separator = $true
        return $item
    }
}

class PmcMenuSystem {
    [PmcSimpleTerminal]$terminal
    [hashtable]$menus = @{}
    [string[]]$menuOrder = @()
    [int]$selectedMenu = -1
    [bool]$inMenuMode = $false
    [bool]$showingDropdown = $false

    PmcMenuSystem() {
        $this.terminal = [PmcSimpleTerminal]::GetInstance()
        $this.InitializeDefaultMenus()
    }

    [void] InitializeDefaultMenus() {
        $this.AddMenu('File', 'F', @(
            [PmcMenuItem]::new('Backup Data', 'file:backup', 'B'),
            [PmcMenuItem]::new('Restore Data', 'file:restore', 'R'),
            [PmcMenuItem]::new('Clear Backups', 'file:clearbackups', 'C'),
            [PmcMenuItem]::new('Exit', 'app:exit', 'X')
        ))
        $this.AddMenu('Tasks', 'T', @(
            [PmcMenuItem]::new('Task List', 'task:list', 'L')
        ))
        $this.AddMenu('Projects', 'P', @(
            [PmcMenuItem]::new('Project List', 'project:list', 'L')
        ))
        $this.AddMenu('Time', 'I', @(
            [PmcMenuItem]::new('Time Log', 'time:list', 'L'),
            [PmcMenuItem]::new('Weekly Report', 'tools:weeklyreport', 'W')
        ))
        $this.AddMenu('View', 'V', @(
            [PmcMenuItem]::new('Agenda', 'view:agenda', 'G'),
            [PmcMenuItem]::new('Next Actions', 'view:nextactions', 'N'),
            [PmcMenuItem]::new('Today', 'view:today', 'T'),
            [PmcMenuItem]::new('Week', 'view:week', 'W'),
            [PmcMenuItem]::new('Month', 'view:month', 'M'),
            [PmcMenuItem]::new('Kanban Board', 'view:kanban', 'K'),
            [PmcMenuItem]::new('Burndown Chart', 'view:burndown', 'C'),
            [PmcMenuItem]::new('Help', 'view:help', 'H')
        ))
        $this.AddMenu('Tools', 'O', @(
            [PmcMenuItem]::new('Wizard', 'tools:wizard', 'W'),
            [PmcMenuItem]::new('Theme', 'tools:theme', 'T'),
            [PmcMenuItem]::new('Theme Editor', 'tools:themeedit', 'E'),
            [PmcMenuItem]::new('Preferences', 'tools:preferences', 'P'),
            [PmcMenuItem]::Separator(),
            [PmcMenuItem]::new('Excel T2020 Workflow', 'excel:t2020', 'X'),
            [PmcMenuItem]::new('Excel Preview', 'excel:preview', 'V'),
            [PmcMenuItem]::new('Excel Import', 'excel:import', 'I')
        ))
        $this.AddMenu('Help', 'H', @(
            [PmcMenuItem]::new('Help Browser', 'help:browser', 'B'),
            [PmcMenuItem]::new('About PMC', 'help:about', 'A')
        ))
    }

    [void] AddMenu([string]$name, [char]$hotkey, [PmcMenuItem[]]$items) {
        $this.menus[$name] = @{ Name = $name; Hotkey = $hotkey; Items = $items }
        $this.menuOrder += $name
    }

    [void] DrawMenuBar() {
        $this.terminal.UpdateDimensions()
        $this.terminal.FillArea(0, 0, $this.terminal.Width, 1, ' ')

        $x = 2
        for ($i = 0; $i -lt $this.menuOrder.Count; $i++) {
            $menuName = $this.menuOrder[$i]
            $menu = $this.menus[$menuName]
            $hotkey = $menu.Hotkey

            if ($this.inMenuMode -and $i -eq $this.selectedMenu) {
                $this.terminal.WriteAtColor($x, 0, $menuName, [PmcVT100]::BgWhite(), [PmcVT100]::Blue())
                $this.terminal.WriteAtColor($x + $menuName.Length, 0, "($hotkey)", [PmcVT100]::Yellow(), "")
            } else {
                $this.terminal.WriteAtColor($x, 0, $menuName, [PmcVT100]::White(), "")
                $this.terminal.WriteAtColor($x + $menuName.Length, 0, "($hotkey)", [PmcVT100]::Gray(), "")
            }
            $x += $menuName.Length + 6
        }

        $this.terminal.DrawHorizontalLine(0, 1, $this.terminal.Width)
    }

    [string] HandleInput() {
        while ($true) {
            $this.DrawMenuBar()
            $key = [Console]::ReadKey($true)

            if (-not $this.inMenuMode) {
                # Check Alt+letter menu activations FIRST (before generic Alt check)
                if ($key.Modifiers -band [ConsoleModifiers]::Alt) {
                    # Check for Alt+menu hotkey (F, E, T, P, M, V, C, D, O, H)
                    $menuActivated = $false
                    for ($i = 0; $i -lt $this.menuOrder.Count; $i++) {
                        $menuName = $this.menuOrder[$i]
                        $menu = $this.menus[$menuName]
                        if ($menu.Hotkey.ToString().ToUpper() -eq $key.Key.ToString().ToUpper()) {
                            $this.inMenuMode = $true
                            $this.selectedMenu = $i
                            $menuActivated = $true
                            break
                        }
                    }
                    if ($menuActivated) {
        Write-ConsoleUIDebug "Alt+$($key.Key) activated menu $($this.menuOrder[$this.selectedMenu])" "MENU"
                        # Show dropdown immediately instead of waiting for Enter
                        return $this.ShowDropdown($this.menuOrder[$this.selectedMenu])
                    }
                    # Alt+X to exit
                    if ($key.Key -eq 'X') {
                        return "app:exit"
                    }
                }
                # F10 activates menu bar at position 0
                if ($key.Key -eq 'F10') {
                    $this.inMenuMode = $true
                    $this.selectedMenu = 0
                    continue
                }
                # Escape to exit
                if ($key.Key -eq 'Escape') {
                    return "app:exit"
                }
                return ""
            } else {
                # While in menu mode, allow Alt+Hotkey to jump directly to another menu
                if ($key.Modifiers -band [ConsoleModifiers]::Alt) {
                    for ($i = 0; $i -lt $this.menuOrder.Count; $i++) {
                        $menuName = $this.menuOrder[$i]
                        $menu = $this.menus[$menuName]
                        if ($menu.Hotkey.ToString().ToUpper() -eq $key.Key.ToString().ToUpper()) {
                            $this.selectedMenu = $i
                            return $this.ShowDropdown($this.menuOrder[$this.selectedMenu])
                        }
                    }
                }
                switch ($key.Key) {
                    'LeftArrow' { if ($this.selectedMenu -gt 0) { $this.selectedMenu-- } else { $this.selectedMenu = $this.menuOrder.Count - 1 } }
                    'RightArrow' { if ($this.selectedMenu -lt $this.menuOrder.Count - 1) { $this.selectedMenu++ } else { $this.selectedMenu = 0 } }
                    'Enter' {
                        if ($this.selectedMenu -ge 0 -and $this.selectedMenu -lt $this.menuOrder.Count) {
                            return $this.ShowDropdown($this.menuOrder[$this.selectedMenu])
                        }
                    }
                    'Escape' { $this.inMenuMode = $false; $this.selectedMenu = -1 }
                }
            }
        }
        return ""
    }

    [string] ShowDropdown([string]$menuName) {
        $menu = $this.menus[$menuName]
        if (-not $menu) { return "" }
        $items = $menu.Items
        $dropdownX = 2; $dropdownY = 2; $maxWidth = 20
        $this.terminal.DrawFilledBox($dropdownX, $dropdownY, $maxWidth, $items.Count + 2, $true)

        $selectedItem = 0
        while ($true) {
            for ($i = 0; $i -lt $items.Count; $i++) {
                $item = $items[$i]
                $itemY = $dropdownY + 1 + $i
                $itemText = " {0}({1}) " -f $item.Label, $item.Hotkey
                if ($i -eq $selectedItem) {
                    $this.terminal.WriteAtColor($dropdownX + 1, $itemY, $itemText.PadRight($maxWidth - 2), [PmcVT100]::BgBlue(), [PmcVT100]::White())
                } else {
                    $this.terminal.WriteAtColor($dropdownX + 1, $itemY, $itemText.PadRight($maxWidth - 2), [PmcVT100]::White(), "")
                }
            }
            $key = [Console]::ReadKey($true)

            # Check for letter hotkeys
            $hotkeyPressed = $false
            for ($i = 0; $i -lt $items.Count; $i++) {
                if ($items[$i].Hotkey.ToString().ToUpper() -eq $key.Key.ToString().ToUpper()) {
                    $this.inMenuMode = $false
                    $this.selectedMenu = -1
                    $this.terminal.FillArea($dropdownX, $dropdownY, $maxWidth, $items.Count + 2, ' ')
                    return $items[$i].Action
                }
            }

            switch ($key.Key) {
                'UpArrow' { if ($selectedItem -gt 0) { $selectedItem-- } }
                'DownArrow' { if ($selectedItem -lt $items.Count - 1) { $selectedItem++ } }
                'Enter' {
                    $this.inMenuMode = $false
                    $this.selectedMenu = -1
                    $this.terminal.FillArea($dropdownX, $dropdownY, $maxWidth, $items.Count + 2, ' ')
                    return $items[$selectedItem].Action
                }
                'Escape' {
                    # Close dropdown, keep menu mode active so user can navigate to other menus
                    $this.terminal.FillArea($dropdownX, $dropdownY, $maxWidth, $items.Count + 2, ' ')
                    return ""
                }
            }
        }
        return ""
    }

    [string] GetActionDescription([string]$action) { return $action }
}

## CLI adapter removed in ConsoleUI copy

# === MAIN APP ===
function Show-ConsoleUIFooter {
    param($app,[string]$msg)
    # Avoid flushing input to prevent dropping Alt/Shift key sequences
    $y = $app.terminal.Height - 1
    $app.terminal.FillArea(0, $y, $app.terminal.Width, 1, ' ')
    $app.terminal.WriteAt(2, $y, $msg)
}

function Browse-ConsoleUIPath {
    param($app,[string]$StartPath,[bool]$DirectoriesOnly=$false)
    $cwd = if ($StartPath -and (Test-Path $StartPath)) {
        if (Test-Path $StartPath -PathType Leaf) { Split-Path -Parent $StartPath } else { $StartPath }
    } else { (Get-Location).Path }
    $selected = 0; $topIndex = 0
    while ($true) {
        $items = @()
        try { $dirs = @(Get-ChildItem -Force -Directory -LiteralPath $cwd | Sort-Object Name) } catch { $dirs=@() }
        try { $files = if ($DirectoriesOnly) { @() } else { @(Get-ChildItem -Force -File -LiteralPath $cwd | Sort-Object Name) } } catch { $files=@() }
        $items += ([pscustomobject]@{ Kind='Up'; Name='..' })
        foreach ($d in $dirs) { $items += [pscustomobject]@{ Kind='Dir'; Name=$d.Name } }
        foreach ($f in $files) { $items += [pscustomobject]@{ Kind='File'; Name=$f.Name } }
        if ($selected -ge $items.Count) { $selected = [Math]::Max(0, $items.Count-1) }
        if ($selected -lt 0) { $selected = 0 }

        $app.terminal.Clear(); $app.menuSystem.DrawMenuBar()
        $kind = 'File'; if ($DirectoriesOnly) { $kind = 'Folder' }
        $title = " Select $kind "
        $titleX = ($app.terminal.Width - $title.Length) / 2
        $app.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $app.terminal.WriteAtColor(4, 5, "Current: $cwd", [PmcVT100]::Cyan(), "")

        $listTop = 7
        $maxVisible = [Math]::Max(5, [Math]::Min(25, $app.terminal.Height - $listTop - 3))
        if ($selected -lt $topIndex) { $topIndex = $selected }
        if ($selected -ge ($topIndex + $maxVisible)) { $topIndex = $selected - $maxVisible + 1 }
        for ($row=0; $row -lt $maxVisible; $row++) {
            $idx = $topIndex + $row
            $line = ''
            if ($idx -lt $items.Count) {
                $item = $items[$idx]
                $tag = if ($item.Kind -eq 'Dir') { '[D]' } elseif ($item.Kind -eq 'File') { '[F]' } else { '  ' }
                $line = "$tag $($item.Name)"
            }
            $prefix = if (($topIndex + $row) -eq $selected) { '> ' } else { '  ' }
            $color = if (($topIndex + $row) -eq $selected) { [PmcVT100]::Yellow() } else { [PmcVT100]::White() }
            $app.terminal.WriteAtColor(4, $listTop + $row, ($prefix + $line).PadRight($app.terminal.Width - 8), $color, "")
        }
        Show-ConsoleUIFooter $app "↑/↓ scroll  |  Enter: select  |  → open folder  |  ←/Backspace up  |  Esc cancel"
        $key = [Console]::ReadKey($true)
        switch ($key.Key) {
            'UpArrow' { if ($selected -gt 0) { $selected--; if ($selected -lt $topIndex) { $topIndex = $selected } } }
            'DownArrow' { if ($selected -lt $items.Count-1) { $selected++; if ($selected -ge $topIndex+$maxVisible) { $topIndex = $selected - $maxVisible + 1 } } }
            'PageUp' { $selected = [Math]::Max(0, $selected - $maxVisible); $topIndex = [Math]::Max(0, $topIndex - $maxVisible) }
            'PageDown' { $selected = [Math]::Min($items.Count-1, $selected + $maxVisible); if ($selected -ge $topIndex+$maxVisible) { $topIndex = $selected - $maxVisible + 1 } }
            'Home' { $selected = 0; $topIndex = 0 }
            'End' { $selected = [Math]::Max(0, $items.Count-1); $topIndex = [Math]::Max(0, $items.Count - $maxVisible) }
            'LeftArrow' {
                try {
                    if ([string]::IsNullOrWhiteSpace($cwd)) { $cwd = ($StartPath ?? (Get-Location).Path) }
                    else {
                        $parent = ''
                        try { $parent = Split-Path -Parent $cwd } catch { $parent = '' }
                        if (-not [string]::IsNullOrWhiteSpace($parent) -and $parent -ne $cwd) { $cwd = $parent }
                    }
                } catch {}
            }
            'Backspace' {
                try {
                    if ([string]::IsNullOrWhiteSpace($cwd)) { $cwd = ($StartPath ?? (Get-Location).Path) }
                    else {
                        $parent = ''
                        try { $parent = Split-Path -Parent $cwd } catch { $parent = '' }
                        if (-not [string]::IsNullOrWhiteSpace($parent) -and $parent -ne $cwd) { $cwd = $parent }
                    }
                } catch {}
            }
            'RightArrow' { if ($items.Count -gt 0) { $it=$items[$selected]; if ($it.Kind -eq 'Dir') { $cwd = Join-Path $cwd $it.Name } } }
            'Escape' { return $null }
            'Enter' {
                if ($items.Count -eq 0) { continue }
                $it = $items[$selected]
                if ($it.Kind -eq 'Up') {
                    try {
                        $parent = ''
                        try { $parent = Split-Path -Parent $cwd } catch { $parent = '' }
                        if (-not [string]::IsNullOrWhiteSpace($parent) -and $parent -ne $cwd) { $cwd = $parent }
                    } catch {}
                }
                elseif ($it.Kind -eq 'Dir') { return (Join-Path $cwd $it.Name) }
                else { return (Join-Path $cwd $it.Name) }
            }
        }
    }
}

function Select-ConsoleUIPathAt {
    param($app,[string]$Hint,[int]$Col,[int]$Row,[string]$StartPath,[bool]$DirectoriesOnly=$false)
    Show-ConsoleUIFooter $app ("$Hint  |  Enter: Pick  |  Tab: Skip  |  Esc: Cancel")
    [Console]::SetCursorPosition($Col, $Row)
    $key = [Console]::ReadKey($true)
    if ($key.Key -eq 'Escape') { Show-ConsoleUIFooter $app "Enter values; Enter = next, Esc = cancel"; return '' }
    if ($key.Key -eq 'Tab') { Show-ConsoleUIFooter $app "Enter values; Enter = next, Esc = cancel"; return '' }
    $sel = Browse-ConsoleUIPath -app $app -StartPath $StartPath -DirectoriesOnly:$DirectoriesOnly
    Show-ConsoleUIFooter $app "Enter values; Enter = next, Esc = cancel"
    return ($sel ?? '')
}

function Get-ConsoleUISelectedProjectName {
    param($app)
    try {
        if ($app.currentView -eq 'projectlist') {
            if ($app.selectedProjectIndex -lt $app.projects.Count) {
                $p = $app.projects[$app.selectedProjectIndex]
                $pname = $null
                if ($p -is [string]) { $pname = $p } else { $pname = $p.name }
                return $pname
            }
        }
        if ($app.filterProject) { return $app.filterProject }
    } catch {}
    return $null
}

function Open-SystemPath {
    param([string]$Path,[bool]$IsDir=$false)
    try {
        if (-not $Path -or -not (Test-Path $Path)) { return $false }
        $isWin = $false
        try { if ($env:OS -like '*Windows*') { $isWin = $true } } catch {}
        if (-not $isWin) { try { if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) { $isWin = $true } } catch {} }
        if ($isWin) {
            if ($IsDir) { Start-Process -FilePath explorer.exe -ArgumentList @("$Path") | Out-Null }
            else { Start-Process -FilePath "$Path" | Out-Null }
            return $true
        } else {
            $cmd = 'xdg-open'
            if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) { $cmd = 'gio'; $args = @('open', "$Path") } else { $args = @("$Path") }
            Start-Process -FilePath $cmd -ArgumentList $args | Out-Null
            return $true
        }
    } catch { return $false }
}

function Open-ConsoleUIProjectPath {
    param($app,[string]$Field)
    $projName = Get-ConsoleUISelectedProjectName -app $app
    if (-not $projName) { Show-InfoMessage -Message "Select a project first (Projects → Project List)" -Title "Info" -Color "Yellow"; return }
    try {
        $data = Get-PmcAllData
        $proj = $data.projects | ForEach-Object {
            if ($_ -is [string]) { if ($_ -eq $projName) { $_ } } else { if ($_.name -eq $projName) { $_ } }
        } | Select-Object -First 1
        if (-not $proj) { Show-InfoMessage -Message "Project not found: $projName" -Title "Error" -Color "Red"; return }
        $path = $null
        if ($proj.PSObject.Properties[$Field]) { $path = $proj.$Field }
        if (-not $path -or [string]::IsNullOrWhiteSpace($path)) { Show-InfoMessage -Message "$Field not set for project" -Title "Error" -Color "Red"; return }
        $isDir = ($Field -eq 'ProjFolder')
        if (Open-SystemPath -Path $path -IsDir:$isDir) {
            Show-InfoMessage -Message "Opened: $path" -Title "Success" -Color "Green"
        } else {
            Show-InfoMessage -Message "Failed to open: $path" -Title "Error" -Color "Red"
        }
    } catch {
        Show-InfoMessage -Message "Failed to open: $_" -Title "Error" -Color "Red"
    }
}

function Draw-ConsoleUIProjectFormValues {
    param($app,[int]$RowStart,[hashtable]$Inputs)
    try {
        $app.terminal.WriteAt(28, $RowStart + 0, [string]($Inputs.Name ?? ''))
        $app.terminal.WriteAt(16, $RowStart + 1, [string]($Inputs.Description ?? ''))
        $app.terminal.WriteAt(9,  $RowStart + 2, [string]($Inputs.ID1 ?? ''))
        $app.terminal.WriteAt(9,  $RowStart + 3, [string]($Inputs.ID2 ?? ''))
        $app.terminal.WriteAt(20, $RowStart + 4, [string]($Inputs.ProjFolder ?? ''))
        $app.terminal.WriteAt(14, $RowStart + 5, [string]($Inputs.CAAName ?? ''))
        $app.terminal.WriteAt(17, $RowStart + 6, [string]($Inputs.RequestName ?? ''))
        $app.terminal.WriteAt(11, $RowStart + 7, [string]($Inputs.T2020 ?? ''))
        $app.terminal.WriteAt(32, $RowStart + 8, [string]($Inputs.AssignedDate ?? ''))
        $app.terminal.WriteAt(27, $RowStart + 9, [string]($Inputs.DueDate ?? ''))
        $app.terminal.WriteAt(26, $RowStart + 10, [string]($Inputs.BFDate ?? ''))
    } catch {}
}

class PmcConsoleUIApp {
    [PmcSimpleTerminal]$terminal
    [PmcMenuSystem]$menuSystem
    # CLI adapter removed in ConsoleUI build
    [bool]$running = $true
    [string]$statusMessage = ""
    [string]$currentView = 'main'  # main, tasklist, taskdetail
    [string]$previousView = ''  # Track where we came from
    [array]$tasks = @()
    [int]$selectedTaskIndex = 0
    [int]$scrollOffset = 0
    [object]$selectedTask = $null
    [string]$filterProject = ''  # Empty means show all
    [string]$searchText = ''  # Empty means no search
    [string]$sortBy = 'id'  # id, priority, status, created, due
    [hashtable]$stats = @{} # Performance stats
    [hashtable]$multiSelect = @{} # Task ID -> selected boolean
    [array]$projects = @()  # Project list
    [int]$selectedProjectIndex = 0  # Selected project in list
    [string]$selectedProjectName = ''  # Selected project name for edit/info flows
    [array]$timelogs = @()  # Time log entries
    [int]$selectedTimeIndex = 0  # Selected time entry in list

    [void] RefreshCurrentView() {
        try {
            switch ($this.currentView) {
                'tasklist' { $this.DrawTaskList(); break }
                'timelist' { $this.DrawTimeList(); break }
                'projectlist' { $this.DrawProjectList(); break }
                'todayview' { $this.DrawTodayView(); break }
                'tomorrowview' { $this.DrawTomorrowView(); break }
                'weekview' { $this.DrawWeekView(); break }
                'monthview' { $this.DrawMonthView(); break }
                'overdueview' { $this.DrawOverdueView(); break }
                'upcomingview' { $this.DrawUpcomingView(); break }
                'blockedview' { $this.DrawBlockedView(); break }
                'noduedateview' { $this.DrawNoDueDateView(); break }
                'nextactionsview' { $this.DrawNextActionsView(); break }
                'agendaview' { $this.DrawAgendaView(); break }
                'kanbanview' { $this.DrawKanbanView(); break }
                'help' { $this.DrawHelpView(); break }
                default { $this.DrawLayout(); break }
            }
        } catch { }
    }

    [void] GoBackOr([string]$fallback) {
        if ($this.previousView) { $this.currentView = $this.previousView; $this.previousView = '' }
        else { $this.currentView = $fallback }
        # Immediately redraw the target view so changes are visible right away
        $this.RefreshCurrentView()
    }
    [array]$specialItems = @()
    [int]$specialSelectedIndex = 0

    PmcConsoleUIApp() {
        # Theme is managed by centralized system via deps/Theme.ps1

        $this.terminal = [PmcSimpleTerminal]::GetInstance()
        $this.menuSystem = [PmcMenuSystem]::new()
        # No CLI adapter
        $this.LoadTasks()
    }

    # Convenience overload to avoid passing $null explicitly from call sites
    [void] LoadTasks() { $this.LoadTasks($null) }

    [void] LoadTasks([object]$dataInput = $null) {
        try {
            $data = if ($null -ne $dataInput) { $dataInput } else { Get-PmcAllData }
            $allTasks = @($data.tasks | Where-Object { $_ -ne $null })

            # Calculate stats
            $this.stats = @{
                total = $allTasks.Count
                active = @($allTasks | Where-Object { $_.status -ne 'completed' }).Count
                completed = @($allTasks | Where-Object { $_.status -eq 'completed' }).Count
                overdue = @($allTasks | Where-Object {
                    if (-not $_.due -or $_.status -eq 'completed') { return $false }
                    $d = Get-ConsoleUIDateOrNull $_.due
                    if ($d) { return ($d.Date -lt (Get-Date).Date) } else { return $false }
                }).Count
            }

            if ($this.filterProject) {
                $allTasks = @($allTasks | Where-Object { $_.project -eq $this.filterProject })
            }

            if ($this.searchText) {
                $search = $this.searchText.ToLower()
                $allTasks = @($allTasks | Where-Object {
                    ($_.text -and $_.text.ToLower().Contains($search)) -or
                    ($_.project -and $_.project.ToLower().Contains($search)) -or
                    ($_.id -and $_.id.ToString().Contains($search))
                })
            }

            # Apply sorting
            switch ($this.sortBy) {
                'priority' {
                    $priorityOrder = @{ 'high' = 1; 'medium' = 2; 'low' = 3; 'none' = 4; $null = 5 }
                    $this.tasks = @($allTasks | Sort-Object { $priorityOrder[$_.priority] })
                }
                'status' {
                    $this.tasks = @($allTasks | Sort-Object status)
                }
                'created' {
                    $this.tasks = @($allTasks | Sort-Object created -Descending)
                }
                'due' {
                    # Sort by due date - invalid/missing at bottom
                    $this.tasks = @($allTasks | Sort-Object {
                        $d = Get-ConsoleUIDateOrNull $_.due
                        if ($d) { return $d } else { return [DateTime]::MaxValue }
                    })
                }
                default {
                    $this.tasks = @($allTasks | Sort-Object { [int]$_.id })
                }
            }
        } catch {
            $this.tasks = @()
        }
    }

    [void] LoadProjects() {
        try {
            $data = Get-PmcAllData
            $this.projects = if ($data.PSObject.Properties['projects']) {
                @($data.projects | Where-Object { $_ -ne $null } | ForEach-Object {
                    if ($_ -is [string]) { [pscustomobject]@{ name = $_ } } else { $_ }
                })
            } else { @() }
        } catch {
            $this.projects = @()
        }
    }

    [void] LoadTimeLogs() {
        try {
            $data = Get-PmcAllData
            $this.timelogs = if ($data.PSObject.Properties['timelogs']) {
                @($data.timelogs | Where-Object { $_ -ne $null } | Sort-Object { $_.date } -Descending)
            } else {
                @()
            }
        } catch {
            $this.timelogs = @()
        }
    }

    [void] Initialize() {
        Write-ConsoleUIDebug "Initialize() called" "APP"
        $this.terminal.Initialize()
        # Determine default landing view from config; default to todayview
        try {
            $cfg = Get-PmcConfig
            $dv = try { [string]$cfg.Behavior.DefaultView } catch { 'todayview' }
            switch ($dv) {
                'todayview' { $this.currentView = 'todayview' }
                'agendaview' { $this.currentView = 'agendaview' }
                'tasklist' { $this.currentView = 'tasklist' }
                default { $this.currentView = 'todayview' }
            }
        } catch { $this.currentView = 'todayview' }
        $this.statusMessage = "PMC Ready - F10 for menus, Esc to exit"
    }

    [void] DrawLayout() {
        $this.terminal.BeginFrame()
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $this.terminal.DrawBox(1, 3, $this.terminal.Width - 2, $this.terminal.Height - 6)
        $title = " PMC - Project Management Console "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        # Load and display quick stats
        try {
            $data = Get-PmcAllData
            $taskCount = $data.tasks.Count
            $activeCount = @($data.tasks | Where-Object { $_.status -ne 'completed' }).Count
            $projectCount = $data.projects.Count

            $this.terminal.WriteAtColor(4, 6, "Tasks: $taskCount total | Active: $activeCount | Completed: $($this.stats.completed)", [PmcVT100]::White(), "")
            if ($this.stats.overdue -gt 0) {
                $this.terminal.WriteAtColor(4, 7, "Overdue: ", [PmcVT100]::White(), "")
                $this.terminal.WriteAtColor(13, 7, "$($this.stats.overdue)", [PmcVT100]::Red(), "")
                $this.terminal.WriteAtColor(16, 7, " | Projects: $projectCount", [PmcVT100]::White(), "")
            } else {
                $this.terminal.WriteAtColor(4, 7, "Projects: $projectCount", [PmcVT100]::White(), "")
            }

            # Display recent tasks
            $this.terminal.WriteAtColor(4, 9, "Recent Tasks:", [PmcVT100]::Yellow(), "")
            $recentTasks = @($data.tasks | Sort-Object created -Descending | Select-Object -First 5)
            $y = 10
            foreach ($task in $recentTasks) {
                $statusIcon = if ($task.status -eq 'completed') { 'X' } else { 'o' }
                $statusColor = if ($task.status -eq 'completed') { [PmcVT100]::Green() } else { [PmcVT100]::Cyan() }
                $text = $task.text
                if ($text.Length -gt 40) { $text = $text.Substring(0, 37) + "..." }
                $this.terminal.WriteAtColor(4, $y, $statusIcon, $statusColor, "")
                $this.terminal.WriteAtColor(6, $y, "$($task.id): $text", [PmcVT100]::White(), "")
                $y++
            }

            $y++
            $this.terminal.WriteAtColor(4, $y++, "Quick Keys:", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAtColor(6, $y++, "Alt+T - Task list", [PmcVT100]::Cyan(), "")
            $this.terminal.WriteAtColor(6, $y++, "Alt+A - Add task", [PmcVT100]::Cyan(), "")
            $this.terminal.WriteAtColor(6, $y++, "Alt+P - Projects", [PmcVT100]::Cyan(), "")
            $this.terminal.WriteAtColor(6, $y++, "F10   - Menu bar", [PmcVT100]::Cyan(), "")
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading PMC data: $_", [PmcVT100]::Red(), "")
            Write-Host "DrawLayout error: $_" -ForegroundColor Red
            Write-Host "Stack: $($_.ScriptStackTrace)" -ForegroundColor Red
        }
        $this.UpdateStatus()
        $this.terminal.EndFrame()
    }

    [void] UpdateStatus() {
        $statusY = $this.terminal.Height - 1
        $this.terminal.FillArea(0, $statusY, $this.terminal.Width, 1, ' ')
        if ($this.statusMessage) { $this.terminal.WriteAtColor(2, $statusY, $this.statusMessage, [PmcVT100]::Cyan(), "") }
    }

    [void] ShowSuccessMessage([string]$message) {
        $statusY = $this.terminal.Height - 1
        $this.terminal.FillArea(0, $statusY, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAtColor(2, $statusY, "OK: $message", [PmcVT100]::Green(), "")
    }

    # Check for global menu/hotkeys - returns action string or empty if not a global key
    [string] CheckGlobalKeys([System.ConsoleKeyInfo]$key) {
        # F10 activates menu
        if ($key.Key -eq 'F10') {
            Write-ConsoleUIDebug "F10 pressed, showing menu" "GLOBAL"
            $action = $this.menuSystem.HandleInput()
            return $action
        }

        # Ctrl+N for Quick Add (from anywhere)
        if ( ($key.Modifiers -band [ConsoleModifiers]::Control) -and $key.Key -eq 'N') {
            Write-ConsoleUIDebug "Ctrl+N pressed, opening Quick Add" "GLOBAL"
            return "task:add"
        }

        # Alt+letter activates specific menu
        if ($key.Modifiers -band [ConsoleModifiers]::Alt) {
            # Check for Alt+menu hotkey
            for ($i = 0; $i -lt $this.menuSystem.menuOrder.Count; $i++) {
                $menuName = $this.menuSystem.menuOrder[$i]
                $menu = $this.menuSystem.menus[$menuName]
                if ($menu.Hotkey.ToString().ToUpper() -eq $key.Key.ToString().ToUpper()) {
                    Write-ConsoleUIDebug "Alt+$($key.Key) pressed, showing dropdown for $menuName" "GLOBAL"
                    # Show dropdown directly instead of using HandleInput
                    $action = $this.menuSystem.ShowDropdown($menuName)
                    return $action
                }
            }
            # Alt+X to exit
            if ($key.Key -eq 'X') {
                return "app:exit"
            }
        }

        return ""
    }

    # Process menu action from any screen
    [void] ProcessMenuAction([string]$action) {
        Write-ConsoleUIDebug "ProcessMenuAction: $action" "ACTION"

        # Try extended handlers first
        $handled = $false
        if ($this.PSObject.Methods['ProcessExtendedActions']) {
            $handled = $this.ProcessExtendedActions($action)
        }

        # Built-in handlers - ONLY set currentView, do NOT call Draw methods!
        if (-not $handled) {
            if ($action -ne 'app:exit') { $this.previousView = $this.currentView }
            switch ($action) {
                # File menu
                'file:backup' { $this.currentView = 'filebackup' }
                'file:restore' { $this.currentView = 'filerestore' }
                'file:clearbackups' { $this.currentView = 'fileclearbackups' }
                'app:exit' { $this.running = $false }

                # Edit menu
                'edit:undo' { $this.currentView = 'editundo' }
                'edit:redo' { $this.currentView = 'editredo' }

                # Task menu
                'task:add' { $this.currentView = 'taskadd' }
        'task:list' { $this.currentView = 'tasklist' }
                'task:edit' { $this.currentView = 'taskedit' }
                'task:complete' { $this.currentView = 'taskcomplete' }
                'task:delete' { $this.currentView = 'taskdelete' }
                'task:copy' { $this.currentView = 'taskcopy' }
                'task:move' { $this.currentView = 'taskmove' }
                'task:find' { $this.currentView = 'search' }
                'task:priority' { $this.currentView = 'taskpriority' }
                'task:postpone' { $this.currentView = 'taskpostpone' }
                'task:note' { $this.currentView = 'tasknote' }
                'task:import' { $this.currentView = 'taskimport' }
                'task:export' { $this.currentView = 'taskexport' }

                # Project menu
        'project:list' { $this.currentView = 'projectlist' }
                'project:create' { $this.currentView = 'projectcreate' }
                'project:edit' { $this.currentView = 'projectedit' }
                'project:rename' { $this.currentView = 'projectedit' }
                'project:archive' { $this.currentView = 'projectarchive' }
                'project:delete' { $this.currentView = 'projectdelete' }
                'project:stats' { $this.currentView = 'projectstats' }
                'project:info' { $this.currentView = 'projectinfo' }
                'project:recent' { $this.currentView = 'projectrecent' }
                # project open actions moved to Project List hotkeys

                # Time menu
                'time:add' { $this.currentView = 'timeadd' }
        'time:list' { $this.currentView = 'timelist' }
                'time:edit' { $this.currentView = 'timeedit' }
                'time:delete' { $this.currentView = 'timedelete' }
                'time:report' { $this.currentView = 'timereport' }
                'timer:start' { $this.currentView = 'timerstart' }
                'timer:stop' { $this.currentView = 'timerstop' }
                'timer:status' { $this.currentView = 'timerstatus' }

                # View menu
                'view:agenda' { $this.currentView = 'agendaview' }
                'view:all' { $this.currentView = 'tasklist' }
                'view:today' {
                    $this.filterProject = ''
                    $this.searchText = ''
                    $this.currentView = 'todayview'
                }
                'view:tomorrow' { $this.currentView = 'tomorrowview' }
                'view:week' { $this.currentView = 'weekview' }
                'view:month' { $this.currentView = 'monthview' }
                'view:overdue' { $this.currentView = 'overdueview' }
                'view:upcoming' { $this.currentView = 'upcomingview' }
                'view:blocked' { $this.currentView = 'blockedview' }
                'view:noduedate' { $this.currentView = 'noduedateview' }
                'view:nextactions' { $this.currentView = 'nextactionsview' }
                'view:kanban' { $this.currentView = 'kanbanview' }
                'view:burndown' { $this.currentView = 'burndownview' }
                'view:help' { $this.currentView = 'help' }

                # Focus menu
                'focus:set' { $this.currentView = 'focusset' }
                'focus:clear' { $this.currentView = 'focusclear' }
                'focus:status' { $this.currentView = 'focusstatus' }

                # Dependencies menu
                'dep:add' { $this.currentView = 'depadd' }
                'dep:remove' { $this.currentView = 'depremove' }
                'dep:show' { $this.currentView = 'depshow' }
                'dep:graph' { $this.currentView = 'depgraph' }

                # Tools menu
                'tools:review' { $this.currentView = 'toolsreview' }
                'tools:wizard' { $this.currentView = 'toolswizard' }
                'tools:templates' { $this.currentView = 'toolstemplates' }
                'tools:statistics' { $this.currentView = 'toolsstatistics' }
                'tools:velocity' { $this.currentView = 'toolsvelocity' }
                'tools:preferences' { $this.currentView = 'toolspreferences' }
                'tools:theme' { $this.currentView = 'toolstheme' }
                'tools:themeedit' { $this.currentView = 'toolstheme' }
                'tools:applytheme' { $this.currentView = 'toolstheme' }
                'tools:aliases' { $this.currentView = 'toolsaliases' }
                'tools:weeklyreport' { $this.currentView = 'toolsweeklyreport' }

                # Excel menu
                'excel:t2020' { $this.currentView = 'excelt2020' }
                'excel:preview' { $this.currentView = 'excelpreview' }
                'excel:import' { $this.currentView = 'excelimport' }

                # Help menu
                'help:browser' { $this.currentView = 'helpbrowser' }
                'help:categories' { $this.currentView = 'helpcategories' }
                'help:search' { $this.currentView = 'helpsearch' }
                'help:about' { $this.currentView = 'helpabout' }
            }
        }
    }

    # (removed duplicate GoBackOr definition)

    [void] DisplayResult([object]$result) {
        $contentY = 5
        $this.terminal.FillArea(2, $contentY, $this.terminal.Width - 4, $this.terminal.Height - 8, ' ')

        # Normalize result to ensure Type/Message/Data are available
        $type = 'info'
        $message = '(no result)'
        $dataOut = $null

        if ($null -ne $result) {
            if ($result -is [hashtable]) {
                if ($result.ContainsKey('Type') -and $result['Type']) { $type = [string]$result['Type'] }
                if ($result.ContainsKey('Message') -and $null -ne $result['Message']) { $message = [string]$result['Message'] } else { $message = [string]$result }
                if ($result.ContainsKey('Data')) { $dataOut = $result['Data'] }
            } else {
                # Try to read as object with properties; fall back to string
                if ($result.PSObject -and $result.PSObject.Properties['Type'] -and $result.Type) { $type = [string]$result.Type }
                if ($result.PSObject -and $result.PSObject.Properties['Message']) { $message = [string]$result.Message } else { $message = [string]$result }
                if ($result.PSObject -and $result.PSObject.Properties['Data']) { $dataOut = $result.Data }
            }
        }

        switch ($type) {
            'success' { $this.terminal.WriteAtColor(4, $contentY, "SUCCESS: " + $message, [PmcVT100]::Green(), "") }
            'error' { $this.terminal.WriteAtColor(4, $contentY, "ERROR: " + $message, [PmcVT100]::Red(), "") }
            'info' { $this.terminal.WriteAtColor(4, $contentY, "ℹ INFO: " + $message, [PmcVT100]::Cyan(), "") }
            'exit' { $this.running = $false; return }
            default { $this.terminal.WriteAtColor(4, $contentY, "ℹ INFO: " + $message, [PmcVT100]::Cyan(), "") }
        }

        if ($dataOut) { $this.terminal.WriteAt(4, $contentY + 2, [string]$dataOut) }
        $this.statusMessage = "${type}: $message".ToUpper()
        $this.UpdateStatus()
    }

    [void] Run() {
        Write-ConsoleUIDebug "Run() entered" "APP"
        while ($this.running) {
            try {
                if ($this.currentView -eq 'tasklist') {
                    $this.HandleTaskListView()
                } elseif ($this.currentView -eq 'taskdetail') {
                    $this.HandleTaskDetailView()
                } elseif ($this.currentView -eq 'taskadd') {
                    $this.HandleTaskAddForm()
                } elseif ($this.currentView -eq 'subtaskadd') {
                    $this.HandleAddSubtaskForm()
                } elseif ($this.currentView -eq 'taskedit') {
                    $this.HandleTaskEditForm()
                } elseif ($this.currentView -eq 'projectfilter') {
                    $this.HandleProjectFilter()
                } elseif ($this.currentView -eq 'search') {
                    $this.HandleSearchForm()
                } elseif ($this.currentView -eq 'help') {
                    $this.HandleHelpView()
                } elseif ($this.currentView -eq 'projectselect') {
                    $this.HandleProjectSelect()
                } elseif ($this.currentView -eq 'duedateedit') {
                    $this.HandleDueDateEdit()
                } elseif ($this.currentView -eq 'multiselect') {
                    # Already handled in HandleTaskListView
                } elseif ($this.currentView -eq 'multipriority') {
                    # Already handled in HandleMultiSelectMode
                } elseif ($this.currentView -eq 'todayview' -or $this.currentView -eq 'overdueview' -or $this.currentView -eq 'upcomingview' -or $this.currentView -eq 'blockedview') {
                    HandleSpecialViewPersistent $this
                } elseif ($this.currentView -eq 'focusstatus') {
                    $this.HandleFocusStatusView()
                } elseif ($this.currentView -eq 'timeadd') {
                    $this.HandleTimeAddForm()
                } elseif ($this.currentView -eq 'timelist') {
                    $this.HandleTimeListView()
                } elseif ($this.currentView -eq 'timereport') {
                    HandleSpecialViewPersistent $this
                } elseif ($this.currentView -eq 'projectlist') {
                    $this.HandleProjectListView()
                } elseif ($this.currentView -eq 'projectcreate') {
                    $this.HandleProjectCreateForm()
                } elseif ($this.currentView -eq 'projectarchive') {
                    $this.HandleProjectArchiveForm()
                } elseif ($this.currentView -eq 'projectdelete') {
                    $this.HandleProjectDeleteForm()
                } elseif ($this.currentView -eq 'projectstats') {
                    $this.HandleProjectStatsView()
                } elseif ($this.currentView -eq 'timeedit') {
                    $this.HandleTimeEditForm()
                } elseif ($this.currentView -eq 'timedelete') {
                    $this.HandleTimeDeleteForm()
                } elseif ($this.currentView -eq 'taskimport') {
                    $this.HandleTaskImportForm()
                } elseif ($this.currentView -eq 'taskexport') {
                    $this.HandleTaskExportForm()
                } elseif ($this.currentView -eq 'timerstatus') {
                    HandleSpecialViewPersistent $this
                } elseif ($this.currentView -eq 'taskedit') {
                    $this.HandleTaskEditForm()
                } elseif ($this.currentView -eq 'taskcomplete') {
                    $this.HandleTaskCompleteForm()
                } elseif ($this.currentView -eq 'taskdelete') {
                    $this.HandleTaskDeleteForm()
                } elseif ($this.currentView -eq 'depadd') {
                    $this.HandleDepAddForm()
                } elseif ($this.currentView -eq 'depremove') {
                    $this.HandleDepRemoveForm()
                } elseif ($this.currentView -eq 'depshow') {
                    $this.HandleDepShowForm()
                } elseif ($this.currentView -eq 'depgraph') {
                    $this.HandleDependencyGraph()
                } elseif ($this.currentView -eq 'filerestore') {
                    $this.HandleFileRestoreForm()
                } elseif ($this.currentView -in @('editundo','editredo','tomorrowview','weekview','monthview','noduedateview','nextactionsview','kanbanview')) {
                    HandleSpecialViewPersistent $this
                } elseif ($this.currentView -eq 'burndownview') {
                    $this.HandleBurndownChart()
                } elseif ($this.currentView -eq 'toolsreview') {
                    $this.HandleStartReview()
                } elseif ($this.currentView -eq 'toolswizard') {
                    $this.HandleProjectWizard()
                } elseif ($this.currentView -eq 'toolsconfig') {
                    $this.HandleConfigEditor()
                } elseif ($this.currentView -eq 'toolstheme') {
                    HandleThemeTool $this
                } elseif ($this.currentView -eq 'toolsaliases') {
                    $this.HandleManageAliases()
                } elseif ($this.currentView -eq 'toolsweeklyreport') {
                    $this.HandleWeeklyReport()
                } elseif ($this.currentView -eq 'excelt2020') {
                    Invoke-ExcelT2020Handler -app $this
                } elseif ($this.currentView -eq 'excelpreview') {
                    Invoke-ExcelPreviewHandler -app $this
                } elseif ($this.currentView -eq 'excelimport') {
                    Invoke-ExcelImportWizard -app $this
                } elseif ($this.currentView -eq 'agendaview') {
                    HandleSpecialViewPersistent $this
                } elseif ($this.currentView -eq 'taskcopy') {
                    $this.HandleCopyTaskForm()
                } elseif ($this.currentView -eq 'taskmove') {
                    $this.HandleMoveTaskForm()
                } elseif ($this.currentView -eq 'taskpriority') {
                    $this.HandleSetPriorityForm()
                } elseif ($this.currentView -eq 'taskpostpone') {
                    $this.HandlePostponeTaskForm()
                } elseif ($this.currentView -eq 'tasknote') {
                    $this.HandleAddNoteForm()
                } elseif ($this.currentView -eq 'projectedit') {
                    $this.HandleEditProjectForm()
                } elseif ($this.currentView -eq 'projectinfo') {
                    $this.HandleProjectInfoView()
                } elseif ($this.currentView -eq 'projectdetail') {
                    $this.HandleProjectDetailView()
                } elseif ($this.currentView -eq 'projectrecent') {
                    $this.HandleRecentProjectsView()
                } elseif ($this.currentView -eq 'timerstart') {
                    HandleSpecialViewPersistent $this
                } elseif ($this.currentView -eq 'timerstop') {
                    HandleSpecialViewPersistent $this
                } elseif ($this.currentView -eq 'focusclear') {
                    HandleSpecialViewPersistent $this
                } elseif ($this.currentView -eq 'toolstemplates') {
                    $this.HandleTemplates()
                } elseif ($this.currentView -eq 'toolsstatistics') {
                    $this.HandleStatistics()
                } elseif ($this.currentView -eq 'toolsvelocity') {
                    $this.HandleVelocity()
                } elseif ($this.currentView -eq 'toolspreferences') {
                    $this.HandlePreferences()
                } elseif ($this.currentView -eq 'helpbrowser') {
                    $this.HandleHelpBrowser()
                } elseif ($this.currentView -eq 'helpcategories') {
                    $this.HandleHelpCategories()
                } elseif ($this.currentView -eq 'helpsearch') {
                    $this.HandleHelpSearch()
                } elseif ($this.currentView -eq 'helpabout') {
                    $this.HandleAboutPMC()
                } elseif ($this.currentView -eq 'filebackup') {
                    $this.HandleBackupView()
                } elseif ($this.currentView -eq 'fileclearbackups') {
                    $this.HandleClearBackupsView()
                } elseif ($this.currentView -eq 'focusset') {
                    $this.HandleFocusSetForm()
                } else {
                    # Fallback: show menu and process action
                    $action = $this.menuSystem.HandleInput()
                    if ($action) {
                        # Use centralized action routing
                        $this.ProcessMenuAction($action)
                    }
                }
            } catch {
                try { Write-ConsoleUIDebug ("RUN LOOP EXCEPTION: {0} | STACK: {1}" -f $_.Exception.Message, $_.ScriptStackTrace) "APP" } catch {}
                throw
            }
        }
    }

    [void] DrawTaskList() {
        $this.terminal.BeginFrame()
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Task List ($($this.tasks.Count) tasks) [Sort: $($this.sortBy)] "
        if ($this.searchText) {
            $title = " Search: '$($this.searchText)' ($($this.tasks.Count) tasks) [Sort: $($this.sortBy)] "
        } elseif ($this.filterProject) {
            $title = " Project: $($this.filterProject) ($($this.tasks.Count) tasks) [Sort: $($this.sortBy)] "
        }
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        # Calculate dynamic column widths based on terminal size
        $termWidth = $this.terminal.Width
        $idWidth = 5
        $statusWidth = 8
        $dueWidth = 11
        $overdueWidth = 3
        $spacing = 10  # Total spacing between all columns

        $remainingWidth = $termWidth - $idWidth - $statusWidth - $dueWidth - $overdueWidth - $spacing - 4
        # Allocate 25% to project, rest to task text
        $projectWidth = [Math]::Min(25, [Math]::Max(12, [int]($remainingWidth * 0.25)))
        $taskWidth = $remainingWidth - $projectWidth - 2

        # Column positions (priority-based: fixed for small cols, dynamic for task/project)
        $colID = 2
        $colStatus = $colID + $idWidth + 1
        $colTask = $colStatus + $statusWidth + 2
        $colDue = $colTask + $taskWidth + 2
        $colProject = $colDue + $dueWidth + 1
        $colOverdue = $colProject + $projectWidth + 1

        $headerY = 5
        $this.terminal.WriteAtColor($colID, $headerY, "ID", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAtColor($colStatus, $headerY, "Status", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAtColor($colTask, $headerY, "Task", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAtColor($colDue, $headerY, "Due", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAtColor($colProject, $headerY, "Project", [PmcVT100]::Cyan(), "")
        $this.terminal.DrawHorizontalLine(0, $headerY + 1, $this.terminal.Width)

        $startY = $headerY + 2
        $maxRows = $this.terminal.Height - $startY - 3

        # Show empty state if no tasks
        if ($this.tasks.Count -eq 0) {
            $emptyY = $startY + 3
            $this.terminal.WriteAtColor(4, $emptyY++, "No tasks to display", [PmcVT100]::Yellow(), "")
            $emptyY++
            $this.terminal.WriteAtColor(4, $emptyY++, "Press 'A' to add your first task", [PmcVT100]::Cyan(), "")
            $this.terminal.WriteAt(4, $emptyY++, "Press '/' to search for tasks")
            $this.terminal.WriteAt(4, $emptyY++, "Press 'C' to clear filters")
        }

        $displayedRows = 0
        $taskIdx = 0
        while ($displayedRows -lt $maxRows -and $taskIdx -lt $this.tasks.Count) {
            # Skip tasks before scroll offset
            if ($taskIdx -lt $this.scrollOffset) {
                $taskIdx++
                continue
            }

            $task = $this.tasks[$taskIdx]
            $y = $startY + $displayedRows
            $isSelected = ($taskIdx -eq $this.selectedTaskIndex)

            if ($isSelected) {
                $this.terminal.FillArea(0, $y, $this.terminal.Width, 1, ' ')
                $this.terminal.WriteAtColor(0, $y, ">", [PmcVT100]::Yellow(), "")
            }

            $statusIcon = if ($task.status -eq 'completed') { 'X' } else { 'o' }
            $statusColor = if ($task.status -eq 'completed') { [PmcVT100]::Green() } else { [PmcVT100]::Cyan() }

            $this.terminal.WriteAtColor($colID, $y, $task.id.ToString(), [PmcVT100]::Cyan(), "")
            $this.terminal.WriteAtColor($colStatus, $y, $statusIcon, $statusColor, "")

            # Truncate task text to fit dynamic width
            $text = if ($null -ne $task.text) { $task.text } else { "" }
            $truncated = $false
            if ($text.Length -gt $taskWidth) {
                $text = $text.Substring(0, $taskWidth - 3) + "..."
                $truncated = $true
            }
            $this.terminal.WriteAtColor($colTask, $y, $text, [PmcVT100]::Yellow(), "")

            # Show due date
            if ($task.due) {
                $dueStr = $task.due.ToString().Substring(0, [Math]::Min(10, $task.due.ToString().Length))
                $this.terminal.WriteAtColor($colDue, $y, $dueStr, [PmcVT100]::Cyan(), "")
            }

            # Show project with dynamic truncation
            $project = if ($null -ne $task.project -and $task.project -ne '') { $task.project } else { 'none' }
            if ($project.Length -gt $projectWidth) {
                $project = $project.Substring(0, $projectWidth - 3) + "..."
            }
            $this.terminal.WriteAtColor($colProject, $y, $project, [PmcVT100]::Gray(), "")

            # Show overdue indicator
            if ($task.due) {
                $dueDate = Get-ConsoleUIDateOrNull $task.due
                if ($dueDate) {
                    $today = Get-Date
                    if ($dueDate.Date -lt $today.Date -and $task.status -ne 'completed') {
                        $this.terminal.WriteAtColor($colOverdue, $y, "⚠", [PmcVT100]::Red(), "")
                    }
                }
            }

            # Show full title in status bar if this task is selected and truncated
            if ($isSelected -and $truncated -and $task.text) {
                $this.terminal.FillArea(0, $this.terminal.Height - 2, $this.terminal.Width, 1, ' ')
                $fullText = "Full: $($task.text)"
                if ($fullText.Length -gt $this.terminal.Width - 4) {
                    $fullText = $fullText.Substring(0, $this.terminal.Width - 7) + "..."
                }
                $this.terminal.WriteAtColor(2, $this.terminal.Height - 2, $fullText, [PmcVT100]::Cyan(), "")
            }

            $displayedRows++

            # Display subtasks as indented lines
            if ($task.PSObject.Properties['subtasks'] -and $task.subtasks -and $task.subtasks.Count -gt 0) {
                foreach ($subtask in $task.subtasks) {
                    if ($displayedRows -ge $maxRows) { break }
                    $y = $startY + $displayedRows
                    # Indent subtask with special character
                    $subtaskIndent = $colTask + 5
                    $this.terminal.WriteAtColor($subtaskIndent, $y, "└─ ", [PmcVT100]::Blue(), "")

                    # Handle both string and object subtasks
                    $subtaskText = if ($subtask -is [string]) {
                        $subtask
                    } elseif ($subtask.text) {
                        $subtask.text
                    } else {
                        $subtask.ToString()
                    }

                    $subtaskMaxWidth = $taskWidth - 8
                    if ($subtaskText.Length -gt $subtaskMaxWidth) {
                        $subtaskText = $subtaskText.Substring(0, $subtaskMaxWidth - 3) + "..."
                    }
                    $this.terminal.WriteAtColor($subtaskIndent + 3, $y, $subtaskText, [PmcVT100]::Blue(), "")
                    $displayedRows++
                }
            }

            $taskIdx++
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $statusBar = "↑↓:Nav | Enter:Detail | A:Add | U:Subtask | E:Edit | Del:Delete | M:Multi | D:Done | S:Sort | F:Filter | C:Clear"
        $this.terminal.WriteAtColor(2, $this.terminal.Height - 1, $statusBar, [PmcVT100]::Cyan(), "")
        $this.terminal.EndFrame()
    }

    [void] HandleTaskListView() {
        $this.DrawTaskList()
        $key = [Console]::ReadKey($true)

        # Check for global menu keys first
        $globalAction = $this.CheckGlobalKeys($key)
        if ($globalAction) {
            Write-ConsoleUIDebug "Global action from task list: $globalAction" "TASKLIST"
            # Process the action
            if ($globalAction -eq 'app:exit') {
                $this.running = $false
                return
            }
            # For other actions, set view and let Run() loop handle it
            $this.ProcessMenuAction($globalAction)
            return
        }

        switch ($key.Key) {
            'UpArrow' {
                if ($this.selectedTaskIndex -gt 0) {
                    $this.selectedTaskIndex--
                    if ($this.selectedTaskIndex -lt $this.scrollOffset) {
                        $this.scrollOffset = $this.selectedTaskIndex
                    }
                }

            }
            'DownArrow' {
                if ($this.selectedTaskIndex -lt $this.tasks.Count - 1) {
                    $this.selectedTaskIndex++
                    $maxRows = $this.terminal.Height - 10
                    if ($this.selectedTaskIndex -ge $this.scrollOffset + $maxRows) {
                        $this.scrollOffset = $this.selectedTaskIndex - $maxRows + 1
                    }
                }

            }
            'Enter' {
                if ($this.selectedTaskIndex -lt $this.tasks.Count) {
                    $this.selectedTask = $this.tasks[$this.selectedTaskIndex]
                    $this.currentView = 'taskdetail'

                }
            }
            'D' {
                if ($this.selectedTaskIndex -lt $this.tasks.Count) {
                    $task = $this.tasks[$this.selectedTaskIndex]
                    $task.status = 'completed'
                    try { $task.completed = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') } catch {}
                    try {
                        $data = Get-PmcAllData
                        Save-PmcData -Data $data -Action "Completed task $($task.id)"
                        $this.LoadTasks()
                        $this.DrawTaskList()
                        $this.ShowSuccessMessage("Task #$($task.id) completed")
                    } catch {}
                }
            }
            'A' {
                $this.currentView = 'taskadd'
            }
            'E' {
                # Edit selected task directly (open edit form, not detail)
                if ($this.selectedTaskIndex -lt $this.tasks.Count) {
                    $this.selectedTask = $this.tasks[$this.selectedTaskIndex]
                    $this.previousView = 'tasklist'
                    $this.currentView = 'taskedit'
                }
            }
            'U' {
                # Add a subtask to the selected task
                if ($this.selectedTaskIndex -lt $this.tasks.Count) {
                    $this.selectedTask = $this.tasks[$this.selectedTaskIndex]
                    $this.previousView = 'tasklist'
                    $this.currentView = 'subtaskadd'
                }
            }
            'Delete' {
                # Delete selected task with confirmation
                if ($this.selectedTaskIndex -lt $this.tasks.Count) {
                    $task = $this.tasks[$this.selectedTaskIndex]
                    $this.terminal.WriteAtColor(4, $this.terminal.Height - 2, "Delete task #$($task.id)? (y/N): ", [PmcVT100]::Yellow(), "")
                    $confirm = [Console]::ReadKey($true)
                    $yes = ($confirm.Key -eq [ConsoleKey]::Y) -or ($confirm.KeyChar -eq 'y') -or ($confirm.KeyChar -eq 'Y')
                    if ($yes) {
                        try {
                            $taskId = $task.id
                            $data = Get-PmcAllData
                            $data.tasks = @($data.tasks | Where-Object { $_.id -ne $taskId })
                            Save-PmcData -Data $data -Action "Deleted task $taskId"
                            $this.LoadTasks()
                            if ($this.selectedTaskIndex -ge $this.tasks.Count -and $this.selectedTaskIndex -gt 0) {
                                $this.selectedTaskIndex--
                            }
                            $this.DrawTaskList()
                            $this.ShowSuccessMessage("Task #$taskId deleted")
                        } catch {
                            Write-ConsoleUIDebug "Delete task error: $($_.Exception.Message)" "ERROR"
                        }
                    } else {
                        Show-InfoMessage -Message "Deletion cancelled" -Title "Cancelled" -Color "Yellow"
                    }

                }
            }
            'C' {
                # Clear filters
                $this.searchText = ''
                $this.filterProject = ''
                $this.LoadTasks()
                $this.selectedTaskIndex = 0
                $this.scrollOffset = 0

            }
            'S' {
                $sortOptions = @('id', 'priority', 'status', 'created', 'due')
                $currentIdx = [Array]::IndexOf(@($sortOptions), $this.sortBy)
                if ($currentIdx -lt 0) { $currentIdx = 0 }
                $newIdx = ($currentIdx + 1) % $sortOptions.Count
                $this.sortBy = $sortOptions[$newIdx]
                $this.LoadTasks()

            }
            'F' {
                $this.currentView = 'projectfilter'

            }
            'Divide' {  # Numpad '/'
                $this.currentView = 'search'
                $this.DrawSearchForm()
            }
            'Oem2' {    # Main keyboard '/?'
                $this.currentView = 'search'
                $this.DrawSearchForm()
            }
            'Spacebar' {
                if ($this.selectedTaskIndex -lt $this.tasks.Count) {
                    $task = $this.tasks[$this.selectedTaskIndex]
                    $isCompleting = -not ($task.status -eq 'completed')
                    $task.status = if ($isCompleting) { 'completed' } else { 'active' }
                    if ($isCompleting) {
                        try { $task.completed = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') } catch {}
                    } else {
                        try { $task.completed = $null } catch {}
                    }
                    try {
                        $data = Get-PmcAllData
                        Save-PmcData -Data $data -Action "Toggled task $($task.id)"
                        $this.LoadTasks()

                    } catch {}
                }
            }
            'Escape' {
                if ($this.previousView -eq 'projectlist') {
                    $this.filterProject = ''
                    $this.currentView = 'projectlist'
                    $this.previousView = ''
                } elseif ($this.previousView) {
                    $this.currentView = $this.previousView
                    $this.previousView = ''
                } else {
                    # Stay on task list if no previous view
                    $this.currentView = 'tasklist'
                }
            }
            'H' { $this.currentView = 'help' }
            'F1' { $this.currentView = 'help' }
            'M' {
                # Multi-select mode toggle
                $this.currentView = 'multiselect'
                $this.DrawMultiSelectMode()
                $this.HandleMultiSelectMode()
            }
            # F10 handled via global CheckGlobalKeys
        }
    }

    [void] DrawTaskDetail() {
        try {
            $this.terminal.Clear()
            $task = $this.selectedTask

            if (-not $task) {
                $this.terminal.WriteAtColor(4, 6, "Error: No task selected", [PmcVT100]::Red(), "")
                $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
                return
            }

            $title = " Task #$($task.id) "
            $titleX = ($this.terminal.Width - $title.Length) / 2
            $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 4
        $this.terminal.WriteAtColor(4, $y++, "Text: $($task.text)", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, $y++, "Status: $(if ($task.status) { $task.status } else { 'none' })", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, $y++, "Priority: $(if ($task.priority) { $task.priority } else { 'none' })", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, $y++, "Project: $(if ($task.project) { $task.project } else { 'none' })", [PmcVT100]::Yellow(), "")

        if ($task.PSObject.Properties['due'] -and $task.due) {
            $dueDisplay = $task.due
            $dueDate = Get-ConsoleUIDateOrNull $task.due
            if ($dueDate) {
                $today = Get-Date
                $daysUntil = ($dueDate.Date - $today.Date).Days

                if ($task.status -ne 'completed') {
                    if ($daysUntil -lt 0) {
                        $this.terminal.WriteAtColor(4, $y, "Due: ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(9, $y, "$dueDisplay (OVERDUE by $([Math]::Abs($daysUntil)) days)", [PmcVT100]::Red(), "")
                        $y++
                    } elseif ($daysUntil -eq 0) {
                        $this.terminal.WriteAtColor(4, $y, "Due: ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(9, $y, "$dueDisplay (TODAY)", [PmcVT100]::Yellow(), "")
                        $y++
                    } elseif ($daysUntil -eq 1) {
                        $this.terminal.WriteAtColor(4, $y, "Due: ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(9, $y, "$dueDisplay (tomorrow)", [PmcVT100]::Cyan(), "")
                        $y++
                    } else {
                        $this.terminal.WriteAtColor(4, $y++, "Due: $dueDisplay (in $daysUntil days)", [PmcVT100]::Yellow(), "")
                    }
                } else {
                    $this.terminal.WriteAtColor(4, $y++, "Due: $dueDisplay", [PmcVT100]::Yellow(), "")
                }
            } else {
                $this.terminal.WriteAtColor(4, $y++, "Due: $dueDisplay", [PmcVT100]::Yellow(), "")
            }
        }

        if ($task.PSObject.Properties['created'] -and $task.created) { $this.terminal.WriteAtColor(4, $y++, "Created: $($task.created)", [PmcVT100]::Yellow(), "") }
        if ($task.PSObject.Properties['modified'] -and $task.modified) { $this.terminal.WriteAtColor(4, $y++, "Modified: $($task.modified)", [PmcVT100]::Yellow(), "") }
        if ($task.PSObject.Properties['completed'] -and $task.completed -and ($task.status -eq 'completed' -or $task.status -eq 'done')) {
            $this.terminal.WriteAtColor(4, $y++, "Completed: $($task.completed)", [PmcVT100]::Green(), "")
        }

        # Display time logs if they exist
        try {
            $data = Get-PmcAllData
            if ($data.timelogs) {
                $taskLogs = @($data.timelogs | Where-Object { $_.taskId -eq $task.id -or $_.task -eq $task.id })
                if ($taskLogs.Count -gt 0) {
                    $totalMinutes = ($taskLogs | ForEach-Object { if ($_.minutes) { $_.minutes } else { 0 } } | Measure-Object -Sum).Sum
                    $hours = [Math]::Floor($totalMinutes / 60)
                    $mins = $totalMinutes % 60
                    $y++
                    $this.terminal.WriteAtColor(4, $y++, "Time Logged: ${hours}h ${mins}m ($($taskLogs.Count) entries)", [PmcVT100]::Yellow(), "")
                }
            }
        } catch {}

        # Display subtasks if they exist
        if ($task.PSObject.Properties['subtasks'] -and $task.subtasks -and $task.subtasks.Count -gt 0) {
            $y++
            $this.terminal.WriteAtColor(4, $y++, "Subtasks:", [PmcVT100]::Yellow(), "")
            foreach ($subtask in $task.subtasks) {
                $subtaskText = if ($subtask -is [string]) {
                    $subtask
                } elseif ($subtask.PSObject.Properties['text'] -and $subtask.text) {
                    $subtask.text
                } else {
                    $subtask.ToString()
                }
                $isCompleted = $subtask.PSObject.Properties['completed'] -and $subtask.completed
                $completed = if ($isCompleted) { "X" } else { "o" }
                $color = if ($isCompleted) { [PmcVT100]::Green() } else { [PmcVT100]::White() }
                $this.terminal.WriteAtColor(6, $y++, "$completed $subtaskText", $color, "")
            }
        }

        # Display notes if they exist
        if ($task.PSObject.Properties['notes'] -and $task.notes -and $task.notes.Count -gt 0) {
            $y++
            $this.terminal.WriteAtColor(4, $y++, "Notes:", [PmcVT100]::Yellow(), "")
            foreach ($note in $task.notes) {
                $noteText = if ($note.PSObject.Properties['text'] -and $note.text) { $note.text } elseif ($note -is [string]) { $note } else { $note.ToString() }
                $noteDate = if ($note.PSObject.Properties['date'] -and $note.date) { $note.date } else { "" }
                if ($noteDate) {
                    $this.terminal.WriteAtColor(6, $y++, "• [$noteDate] $noteText", [PmcVT100]::Cyan(), "")
                } else {
                    $this.terminal.WriteAtColor(6, $y++, "• $noteText", [PmcVT100]::Cyan(), "")
                }
            }
        }

            $this.terminal.DrawFooter("↑↓:Nav | E:Edit | A:Add Subtask | J:Project | T:Due | D:Done | P:Priority | Del:Delete | Esc:Back")
        } catch {
            $this.terminal.Clear()
            $this.terminal.WriteAtColor(4, 6, "Error displaying task detail: $_", [PmcVT100]::Red(), "")
            $this.terminal.WriteAtColor(4, 8, "Stack: $($_.ScriptStackTrace)", [PmcVT100]::Gray(), "")
            $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
        }
    }

    [void] HandleTaskDetailView() {
        $this.DrawTaskDetail()
        $key = [Console]::ReadKey($true)
        # Global menus
        $globalAction = $this.CheckGlobalKeys($key)
        if ($globalAction) {
            if ($globalAction -eq 'app:exit') { $this.running = $false; return }
            $this.ProcessMenuAction($globalAction)
            return
        }

        switch ($key.Key) {
            'E' {
                $this.previousView = 'taskdetail'
                $this.currentView = 'taskedit'
            }
            'A' {
                # Add subtask to the currently selected task
                $this.previousView = 'taskdetail'
                $this.currentView = 'subtaskadd'
            }
            'J' {
                $this.previousView = 'taskdetail'
                $this.currentView = 'projectselect'
            }
            'T' {
                $this.previousView = 'taskdetail'
                $this.currentView = 'duedateedit'
            }
            'D' {
                $this.selectedTask.status = 'completed'
                try { $this.selectedTask.completed = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') } catch {}
                try {
                    $data = Get-PmcAllData
                    Save-PmcData -Data $data -Action "Completed task $($this.selectedTask.id)"
                    $this.LoadTasks()
                    $this.GoBackOr('tasklist')
                } catch {}
            }
            'P' {
                $priorities = @('high', 'medium', 'low', 'none')
                $currentVal = if ($this.selectedTask.priority) { $this.selectedTask.priority } else { 'none' }
                $currentIdx = [Array]::IndexOf(@($priorities), $currentVal)
                if ($currentIdx -lt 0) { $currentIdx = 0 }
                $newIdx = ($currentIdx + 1) % $priorities.Count
                $this.selectedTask.priority = if ($priorities[$newIdx] -eq 'none') { $null } else { $priorities[$newIdx] }
                try {
                    $data = Get-PmcAllData
                    Save-PmcData -Data $data -Action "Changed priority for task $($this.selectedTask.id)"
                    $this.LoadTasks()

                } catch {}
            }
            'X' {
                try {
                    $taskId = $this.selectedTask.id
                    $data = Get-PmcAllData
                    $data.tasks = @($data.tasks | Where-Object { $_.id -ne $taskId })
                    Save-PmcData -Data $data -Action "Deleted task $taskId"
                    $this.LoadTasks()
                    $this.GoBackOr('tasklist')
                    if ($this.selectedTaskIndex -ge $this.tasks.Count) {
                        $this.selectedTaskIndex = [Math]::Max(0, $this.tasks.Count - 1)
                    }

                } catch {}
            }
            'Escape' { $this.GoBackOr('tasklist') }
        }
    }

    [void] DrawAddSubtaskForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Add Subtask "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Parent Task: #$($this.selectedTask.id) - $($this.selectedTask.text)", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "Subtask text:", [PmcVT100]::Cyan(), "")
        $this.terminal.DrawFooter("Tab:Navigate  Enter:Save  Esc:Cancel")
    }

    [void] HandleAddSubtaskForm() {
        if (-not $this.selectedTask) { $this.GoBackOr('tasklist'); return }
        $this.DrawAddSubtaskForm()

        # Simple one-field input using the widget form for consistency
        $fields = @(@{ Name='text'; Label='Subtask text'; Required=$true; Type='text' })
        $res = Show-InputForm -Title "Add Subtask" -Fields $fields
        if ($null -eq $res) { $this.GoBackOr('taskdetail'); return }
        $text = [string]$res['text']
        if ([string]::IsNullOrWhiteSpace($text)) { $this.GoBackOr('taskdetail'); return }

        try {
            $data = Get-PmcAllData
            $task = @($data.tasks | Where-Object { $_.id -eq $this.selectedTask.id })[0]
            if (-not $task) { Show-InfoMessage -Message "Task not found" -Title "Error" -Color "Red"; $this.GoBackOr('tasklist'); return }
            if (-not $task.PSObject.Properties['subtasks']) {
                $task | Add-Member -MemberType NoteProperty -Name 'subtasks' -Value @() -Force
            }
            $sub = [pscustomobject]@{ text = $text.Trim(); completed = $false; created = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') }
            $task.subtasks += $sub
            Save-PmcData -Data $data -Action ("Added subtask to task #{0}" -f $task.id)
            # Keep selection and show updated detail
            $this.LoadTasks($data)
            # Re-select the same task by id
            for ($i=0; $i -lt $this.tasks.Count; $i++) { if ($this.tasks[$i].id -eq $task.id) { $this.selectedTaskIndex = $i; break } }
            $this.selectedTask = @($this.tasks | Where-Object { $_.id -eq $task.id })[0]
            $this.previousView = 'tasklist'  # ensure Esc from detail goes back to list if needed
            $this.currentView = 'taskdetail'
            $this.RefreshCurrentView()
        } catch {
            Show-InfoMessage -Message "Failed to add subtask: $_" -Title "SAVE ERROR" -Color "Red"
            $this.GoBackOr('taskdetail')
        }
    }

    [void] DrawTaskAddForm() {
        $this.terminal.Clear()

        $title = " Add New Task "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 5, "Task text:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 6, "> ", [PmcVT100]::Yellow(), "")

        $this.terminal.WriteAtColor(4, 8, "Quick Add Syntax:", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAtColor(6, 9, "@project  - Set project (e.g., @work)", [PmcVT100]::Gray(), "")
        $this.terminal.WriteAtColor(6, 10, "#priority - Set priority: #high #medium #low or #h #m #l", [PmcVT100]::Gray(), "")
        $this.terminal.WriteAtColor(6, 11, "!due      - Set due: !today !tomorrow !+7 (days)", [PmcVT100]::Gray(), "")
        $this.terminal.WriteAtColor(4, 13, "Example: Fix bug @myapp #high !tomorrow", [PmcVT100]::Cyan(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAtColor(2, $this.terminal.Height - 1, "Type task with quick add syntax, Enter to save, Esc to cancel", [PmcVT100]::Yellow(), "")
    }

    [void] HandleTaskAddForm() {
        # Get available projects
        $data = Get-PmcAllData
        $projectList = @('none', 'inbox') + @($data.projects | ForEach-Object { $_.name } | Where-Object { $_ -and $_ -ne 'inbox' } | Sort-Object)

        # Use new widget-based approach with separate fields
        $input = Show-InputForm -Title "Add New Task" -Fields @(
            @{Name='text'; Label='Task description'; Required=$true; Type='text'}
            @{Name='project'; Label='Project'; Required=$false; Type='select'; Options=$projectList}
            @{Name='priority'; Label='Priority'; Required=$false; Type='select'; Options=@('high', 'medium', 'low')}
            @{Name='due'; Label='Due date (YYYY-MM-DD or today/tomorrow)'; Required=$false; Type='text'}
        )

        if ($null -eq $input) {
            $this.GoBackOr('tasklist')
            return
        }

        $taskText = $input['text']
        if ($taskText.Length -lt 3) {
            Show-InfoMessage -Message "Task description must be at least 3 characters" -Title "Error" -Color "Red"
            $this.GoBackOr('tasklist')
            return
        }

        try {
            $data = Get-PmcAllData
            $newId = if ($data.tasks.Count -gt 0) {
                ($data.tasks | ForEach-Object { [int]$_.id } | Measure-Object -Maximum).Maximum + 1
            } else { 1 }

            # Get values from form fields
            $project = if ([string]::IsNullOrWhiteSpace($input['project']) -or $input['project'] -eq 'none') { $null } elseif ($input['project'] -eq 'inbox') { 'inbox' } else { $input['project'].Trim() }

            $priority = 'medium'
            if (-not [string]::IsNullOrWhiteSpace($input['priority'])) {
                $priInput = $input['priority'].Trim().ToLower()
                $priority = switch -Regex ($priInput) {
                    '^h(igh)?$' { 'high' }
                    '^l(ow)?$' { 'low' }
                    '^m(edium)?$' { 'medium' }
                    default { 'medium' }
                }
            }

            $due = $null
            if (-not [string]::IsNullOrWhiteSpace($input['due'])) {
                $dueInput = $input['due'].Trim().ToLower()
                $due = switch ($dueInput) {
                    'today' { (Get-Date).ToString('yyyy-MM-dd') }
                    'tomorrow' { (Get-Date).AddDays(1).ToString('yyyy-MM-dd') }
                    default {
                        # Try to parse as date safely
                        $parsedDate = Get-ConsoleUIDateOrNull $dueInput
                        if ($parsedDate) { $parsedDate.ToString('yyyy-MM-dd') } else { $null }
                    }
                }
            }

            $newTask = [PSCustomObject]@{
                id = $newId
                text = $taskText.Trim()
                status = 'active'
                priority = $priority
                project = $project
                due = $due
                created = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
            }

            $data.tasks += $newTask

            # CRITICAL: Save with error handling that BLOCKS
            Save-PmcData -Data $data -Action "Added task $newId"

            # Only continue if save succeeded
            $this.LoadTasks()
            Show-InfoMessage -Message "Task #$newId added successfully: $($taskText.Trim())" -Title "Success" -Color "Green"

        } catch {
            # CRITICAL: Show error and BLOCK until user acknowledges
            Show-InfoMessage -Message "FAILED TO SAVE TASK: $_`n`nYour task was NOT saved. Please try again." -Title "SAVE ERROR" -Color "Red"
        }

        $this.GoBackOr('tasklist')
    }

    [void] DrawProjectFilter() {
        $this.terminal.Clear()

        $title = " Filter by Project "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $projectList = @(
                $data.projects |
                    ForEach-Object {
                        if ($_ -is [string]) { $_ }
                        elseif ($_.PSObject.Properties['name']) { $_.name }
                    } |
                    Where-Object { $_ }
            )
            $projectList = @('All') + ($projectList | Sort-Object -Unique)

            $y = 5
            for ($i = 0; $i -lt $projectList.Count; $i++) {
                $project = $projectList[$i]
                $isSelected = if ($project -eq 'All') {
                    -not $this.filterProject
                } else {
                    $this.filterProject -eq $project
                }

                if ($isSelected) {
                    $this.terminal.WriteAtColor(4, $y + $i, "> $project", [PmcVT100]::Yellow(), "")
                } else {
                    $this.terminal.WriteAt(4, $y + $i, "  $project")
                }
            }
        } catch {}

        $this.terminal.DrawFooter("↑↓:Nav | Enter:Select | Esc:Back")
    }

    [void] HandleProjectFilter() {
        $this.DrawProjectFilter()
        try {
            $data = Get-PmcAllData
            $projectList = @(
                $data.projects |
                    ForEach-Object {
                        if ($_ -is [string]) { $_ }
                        elseif ($_.PSObject.Properties['name']) { $_.name }
                    } |
                    Where-Object { $_ }
            )
            $projectList = @('All') + ($projectList | Sort-Object -Unique)
            $selectedIdx = 0

            if ($this.filterProject) {
                $selectedIdx = [Array]::IndexOf(@($projectList), $this.filterProject)
                if ($selectedIdx -lt 0) { $selectedIdx = 0 }
            }

            while ($true) {
                $this.terminal.Clear()
                $title = " Filter by Project "
                $titleX = ($this.terminal.Width - $title.Length) / 2
                $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

                $y = 5
                for ($i = 0; $i -lt $projectList.Count; $i++) {
                    $project = $projectList[$i]
                    if ($i -eq $selectedIdx) {
                        $this.terminal.WriteAtColor(4, $y + $i, "> $project", [PmcVT100]::Yellow(), "")
                    } else {
                        $this.terminal.WriteAt(4, $y + $i, "  $project")
                    }
                }

                $this.terminal.DrawFooter("↑↓:Nav | Enter:Select | Esc:Back")

                $key = [Console]::ReadKey($true)

                switch ($key.Key) {
                    'UpArrow' {
                        if ($selectedIdx -gt 0) { $selectedIdx-- }
                    }
                    'DownArrow' {
                        if ($selectedIdx -lt $projectList.Count - 1) { $selectedIdx++ }
                    }
                    'Enter' {
                        $selected = $projectList[$selectedIdx]
                        if ($selected -eq 'All') {
                            $this.filterProject = ''
                        } else {
                            $this.filterProject = $selected
                        }
                        $this.LoadTasks()
                        $this.GoBackOr('tasklist')
                        $this.selectedTaskIndex = 0
                        $this.scrollOffset = 0

                        break
                    }
                    'Escape' {
                        $this.GoBackOr('tasklist')
                        break
                    }
                }
            }
        } catch {
            $this.GoBackOr('tasklist')

        }
    }

    [void] DrawSearchForm() {
        $this.terminal.Clear()

        $title = " Search Tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 5, "Search for:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 6, "> ", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAtColor(2, $this.terminal.Height - 1, "Type search term, Enter to search, Esc to cancel", [PmcVT100]::Yellow(), "")
    }

    [void] HandleSearchForm() {
        $this.DrawSearchForm()
        $this.terminal.WriteAtColor(6, 6, "", [PmcVT100]::Yellow(), "")
        $searchInput = ""
        $cursorX = 6

        while ($true) {
            $key = [Console]::ReadKey($true)

            if ($key.Key -eq 'Enter') {
                $this.searchText = $searchInput.Trim()
                $this.LoadTasks()
                $this.GoBackOr('tasklist')
                $this.selectedTaskIndex = 0
                $this.scrollOffset = 0

                break
            } elseif ($key.Key -eq 'Escape') {
                $this.GoBackOr('tasklist')

                break
            } elseif ($key.Key -eq 'Backspace') {
                if ($searchInput.Length -gt 0) {
                    $searchInput = $searchInput.Substring(0, $searchInput.Length - 1)
                    $cursorX = 6 + $searchInput.Length
                    $this.terminal.FillArea(6, 6, $this.terminal.Width - 7, 1, ' ')
                    $this.terminal.WriteAtColor(6, 6, $searchInput, [PmcVT100]::Yellow(), "")
                }
            } else {
                $char = $key.KeyChar
                if ($char -and $char -ne "`0") {
                    $searchInput += $char
                    $this.terminal.WriteAtColor($cursorX, 6, $char.ToString(), [PmcVT100]::Yellow(), "")
                    $cursorX++
                }
            }
        }
    }

    [void] DrawHelpView() {
        $this.terminal.Clear()

        $title = " PMC ConsoleUI - Keybindings & Help "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 4
        $this.terminal.WriteAtColor(4, $y++, "Global Keys:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(6, $y++, "F10       - Open menu bar")
        $this.terminal.WriteAt(6, $y++, "Esc       - Back / Close menus / Exit")
        $this.terminal.WriteAt(6, $y++, "Alt+X     - Quick exit PMC")
        $this.terminal.WriteAt(6, $y++, "Alt+T     - Open task list")
        $this.terminal.WriteAt(6, $y++, "Alt+A     - Add new task")
        $this.terminal.WriteAt(6, $y++, "Alt+P     - Project list")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Task List Keys:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(6, $y++, "↑↓        - Navigate tasks")
        $this.terminal.WriteAt(6, $y++, "Enter     - View task details")
        $this.terminal.WriteAt(6, $y++, "A         - Add new task")
        $this.terminal.WriteAt(6, $y++, "M         - Multi-select mode (bulk operations)")
        $this.terminal.WriteAt(6, $y++, "D         - Mark task complete")
        $this.terminal.WriteAt(6, $y++, "S         - Cycle sort order (id/priority/status/created/due)")
        $this.terminal.WriteAt(6, $y++, "F         - Filter by project")
        $this.terminal.WriteAt(6, $y++, "C         - Clear all filters")
        $this.terminal.WriteAt(6, $y++, "/         - Search tasks")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Multi-Select Mode Keys:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(6, $y++, "Space     - Toggle task selection")
        $this.terminal.WriteAt(6, $y++, "A         - Select all visible tasks")
        $this.terminal.WriteAt(6, $y++, "N         - Clear all selections")
        $this.terminal.WriteAt(6, $y++, "D         - Complete selected tasks")
        $this.terminal.WriteAt(6, $y++, "X         - Delete selected tasks")
        $this.terminal.WriteAt(6, $y++, "P         - Set priority for selected tasks")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Task Detail Keys:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(6, $y++, "E         - Edit task text")
        $this.terminal.WriteAt(6, $y++, "J         - Change project")
        $this.terminal.WriteAt(6, $y++, "T         - Set due date")
        $this.terminal.WriteAt(6, $y++, "D         - Mark as complete")
        $this.terminal.WriteAt(6, $y++, "P         - Cycle priority (high/medium/low/none)")
        $this.terminal.WriteAt(6, $y++, "X         - Delete task")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Quick Add Syntax:", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAt(6, $y++, "@project  - Set project (e.g., 'Fix bug @work')")
        $this.terminal.WriteAt(6, $y++, "#priority - Set priority: #high #medium #low or #h #m #l")
        $this.terminal.WriteAt(6, $y++, "!due      - Set due: !today !tomorrow !+7 (days)")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Features:", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAt(6, $y++, "• Real-time PMC data integration with persistent storage")
        $this.terminal.WriteAt(6, $y++, "• Quick add syntax for fast task creation (@project #priority !due)")
        $this.terminal.WriteAt(6, $y++, "• Multi-select mode for bulk operations (complete/delete/priority)")
        $this.terminal.WriteAt(6, $y++, "• Color-coded priorities and overdue warnings")
        $this.terminal.WriteAt(6, $y++, "• Project filtering, task search, and 5-way sorting")
        $this.terminal.WriteAt(6, $y++, "• Due date management with relative dates and smart indicators")
        $this.terminal.WriteAt(6, $y++, "• Scrollable lists, inline editing, full CRUD operations")

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    [void] HandleHelpView() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'help') {
            $this.DrawHelpView()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'help') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('main')
    }

    [void] DrawProjectSelect() {
        $this.terminal.Clear()

        $title = " Change Task Project "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $projectList = @(
                $data.projects |
                    ForEach-Object {
                        if ($_ -is [string]) { $_ }
                        elseif ($_.PSObject.Properties['name']) { $_.name }
                    } |
                    Where-Object { $_ }
            )

            $y = 5
            for ($i = 0; $i -lt $projectList.Count; $i++) {
                $project = $projectList[$i]
                $isSelected = ($this.selectedTask.project -eq $project)

                if ($isSelected) {
                    $this.terminal.WriteAtColor(4, $y + $i, "> $project", [PmcVT100]::Yellow(), "")
                } else {
                    $this.terminal.WriteAt(4, $y + $i, "  $project")
                }
            }
        } catch {}

        $this.terminal.DrawFooter("↑↓:Nav | Enter:Select | Esc:Cancel")
    }

    [void] HandleProjectSelect() {
        try {
            $data = Get-PmcAllData
            $projectList = @(
                $data.projects |
                    ForEach-Object {
                        if ($_ -is [string]) { $_ }
                        elseif ($_.PSObject.Properties['name']) { $_.name }
                    } |
                    Where-Object { $_ }
            )
            $selectedIdx = [Array]::IndexOf(@($projectList), $this.selectedTask.project)
            if ($selectedIdx -lt 0) { $selectedIdx = 0 }

            while ($true) {
                $this.terminal.Clear()
                $title = " Change Task Project "
                $titleX = ($this.terminal.Width - $title.Length) / 2
                $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

                $y = 5
                for ($i = 0; $i -lt $projectList.Count; $i++) {
                    $project = $projectList[$i]
                    if ($i -eq $selectedIdx) {
                        $this.terminal.WriteAtColor(4, $y + $i, "> $project", [PmcVT100]::Yellow(), "")
                    } else {
                        $this.terminal.WriteAt(4, $y + $i, "  $project")
                    }
                }

                $this.terminal.DrawFooter("↑↓:Nav | Enter:Select | Esc:Cancel")

                $key = [Console]::ReadKey($true)
                switch ($key.Key) {
                    'UpArrow' {
                        if ($selectedIdx -gt 0) { $selectedIdx-- }
                    }
                    'DownArrow' {
                        if ($selectedIdx -lt $projectList.Count - 1) { $selectedIdx++ }
                    }
                    'Enter' {
                        $selected = $projectList[$selectedIdx]
                        try {
                            $data = Get-PmcAllData
                            $task = $data.tasks | Where-Object { $_.id -eq $this.selectedTask.id } | Select-Object -First 1
                            if ($task) {
                                $task.project = $selected
                                Save-PmcData -Data $data -Action "Changed project for task $($task.id) to $selected"
                                $this.LoadTasks()
                                $this.selectedTask = $task
                                $this.currentView = 'taskdetail'

                            }
                        } catch {}
                        break
                    }
                    'Escape' {
                        $this.currentView = 'taskdetail'

                        break
                    }
                }
            }
        } catch {
            $this.currentView = 'taskdetail'

        }
    }

    [void] DrawDueDateEdit() {
        $this.terminal.Clear()

        $title = " Set Due Date "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAt(4, 5, "Current due date: $(if ($this.selectedTask.due) { $this.selectedTask.due } else { 'none' })")
        $this.terminal.WriteAt(4, 7, "Enter new due date (YYYY-MM-DD):")
        $this.terminal.WriteAt(4, 8, "> ")
        $this.terminal.WriteAt(4, 10, "Or press:")
        $this.terminal.WriteAt(6, 11, "1 - Today")
        $this.terminal.WriteAt(6, 12, "2 - Tomorrow")
        $this.terminal.WriteAt(6, 13, "3 - Next week (+7 days)")
        $this.terminal.WriteAt(6, 14, "C - Clear due date")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Type date or shortcut, Enter to save, Esc to cancel")
    }

    [void] HandleDueDateEdit() {
        $this.terminal.WriteAt(6, 8, "")
        $dateInput = ""
        $cursorX = 6

        while ($true) {
            $key = [Console]::ReadKey($true)

            if ($key.Key -eq 'Enter') {
                try {
                    $newDate = $null
                    if ($dateInput.Trim()) {
                        # Try to parse as date (supports multiple formats)
                        $newDate = ConvertTo-PmcDate -DateString $dateInput.Trim()
                        if ($null -eq $newDate) {
                            # Invalid format
                            $this.terminal.WriteAtColor(4, 16, "Invalid date! Try: yyyymmdd, mmdd, +3, today, etc.", [PmcVT100]::Red(), "")
                            $this.DrawDueDateEdit()
                            $dateInput = ""
                            $cursorX = 6
                            continue
                        }
                    }

                    $data = Get-PmcAllData
                    $task = $data.tasks | Where-Object { $_.id -eq $this.selectedTask.id } | Select-Object -First 1
                    if ($task) {
                        $task.due = $newDate
                        Save-PmcData -Data $data -Action "Set due date for task $($task.id)"
                        $this.LoadTasks()
                        $this.selectedTask = $task
                        $this.currentView = 'taskdetail'

                    }
                } catch {
                    $this.terminal.WriteAtColor(4, 16, "Error: $_", [PmcVT100]::Red(), "")
                }
                break
            } elseif ($key.Key -eq 'Escape') {
                $this.currentView = 'taskdetail'

                break
            } elseif ($key.KeyChar -eq '1') {
                $dateInput = (Get-Date).ToString('yyyy-MM-dd')
                $this.terminal.FillArea(6, 8, $this.terminal.Width - 7, 1, ' ')
                $this.terminal.WriteAt(6, 8, $dateInput)
                $cursorX = 6 + $dateInput.Length
            } elseif ($key.KeyChar -eq '2') {
                $dateInput = (Get-Date).AddDays(1).ToString('yyyy-MM-dd')
                $this.terminal.FillArea(6, 8, $this.terminal.Width - 7, 1, ' ')
                $this.terminal.WriteAt(6, 8, $dateInput)
                $cursorX = 6 + $dateInput.Length
            } elseif ($key.KeyChar -eq '3') {
                $dateInput = (Get-Date).AddDays(7).ToString('yyyy-MM-dd')
                $this.terminal.FillArea(6, 8, $this.terminal.Width - 7, 1, ' ')
                $this.terminal.WriteAt(6, 8, $dateInput)
                $cursorX = 6 + $dateInput.Length
            } elseif ($key.KeyChar -eq 'c' -or $key.KeyChar -eq 'C') {
                $dateInput = ""
                $this.terminal.FillArea(6, 8, $this.terminal.Width - 7, 1, ' ')
                $cursorX = 6
            } elseif ($key.Key -eq 'Backspace') {
                if ($dateInput.Length -gt 0) {
                    $dateInput = $dateInput.Substring(0, $dateInput.Length - 1)
                    $cursorX = 6 + $dateInput.Length
                    $this.terminal.FillArea(6, 8, $this.terminal.Width - 7, 1, ' ')
                    $this.terminal.WriteAt(6, 8, $dateInput)
                }
            } else {
                $char = $key.KeyChar
                if ($char -and $char -ne "`0" -and ($char -match '[0-9\-]')) {
                    $dateInput += $char
                    $this.terminal.WriteAt($cursorX, 8, $char.ToString())
                    $cursorX++
                }
            }
        }
    }

    [void] DrawMultiSelectMode() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $selectedCount = ($this.multiSelect.Values | Where-Object { $_ -eq $true }).Count
        $title = " Multi-Select Mode ($selectedCount selected) "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgYellow(), [PmcVT100]::Black())

        $headerY = 5
        $this.terminal.WriteAt(2, $headerY, "Sel")
        $this.terminal.WriteAt(8, $headerY, "ID")
        $this.terminal.WriteAt(14, $headerY, "Status")
        $this.terminal.WriteAt(24, $headerY, "Pri")
        $this.terminal.WriteAt(30, $headerY, "Task")
        $this.terminal.DrawHorizontalLine(0, $headerY + 1, $this.terminal.Width)

        $startY = $headerY + 2
        $maxRows = $this.terminal.Height - $startY - 3

        for ($i = 0; $i -lt $maxRows -and ($i + $this.scrollOffset) -lt $this.tasks.Count; $i++) {
            $taskIdx = $i + $this.scrollOffset
            $task = $this.tasks[$taskIdx]
            $y = $startY + $i
            $isSelected = ($taskIdx -eq $this.selectedTaskIndex)
            $isMarked = $this.multiSelect[$task.id]

            if ($isSelected) {
                $this.terminal.FillArea(0, $y, $this.terminal.Width, 1, ' ')
                $this.terminal.WriteAtColor(0, $y, ">", [PmcVT100]::Yellow(), "")
            }

            $marker = if ($isMarked) { '[X]' } else { '[ ]' }
            $markerColor = if ($isMarked) { [PmcVT100]::Green() } else { "" }
            if ($markerColor) {
                $this.terminal.WriteAtColor(2, $y, $marker, $markerColor, "")
            } else {
                $this.terminal.WriteAt(2, $y, $marker)
            }

            $statusIcon = if ($task.status -eq 'completed') { 'X' } else { 'o' }
            $this.terminal.WriteAt(8, $y, $task.id.ToString())
            $this.terminal.WriteAt(14, $y, $statusIcon)

            $priVal = if ($task.priority) { $task.priority } else { 'none' }
            $priChar = $priVal.Substring(0,1).ToUpper()
            $this.terminal.WriteAt(24, $y, $priChar)

            $text = if ($task.text) { $task.text } else { "" }
            if ($text.Length -gt 45) { $text = $text.Substring(0, 42) + "..." }
            $this.terminal.WriteAt(30, $y, $text)
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Space:Toggle | A:All | N:None | C:Complete | X:Delete | P:Priority | M:Move | Esc:Exit")
    }

    [void] HandleMultiSelectMode() {
        while ($true) {
            $key = [Console]::ReadKey($true)

            switch ($key.Key) {
                'UpArrow' {
                    if ($this.selectedTaskIndex -gt 0) {
                        $this.selectedTaskIndex--
                        if ($this.selectedTaskIndex -lt $this.scrollOffset) {
                            $this.scrollOffset = $this.selectedTaskIndex
                        }
                    }
                    $this.DrawMultiSelectMode()
                }
                'DownArrow' {
                    if ($this.selectedTaskIndex -lt $this.tasks.Count - 1) {
                        $this.selectedTaskIndex++
                        $maxRows = $this.terminal.Height - 10
                        if ($this.selectedTaskIndex -ge $this.scrollOffset + $maxRows) {
                            $this.scrollOffset = $this.selectedTaskIndex - $maxRows + 1
                        }
                    }
                    $this.DrawMultiSelectMode()
                }
                'Spacebar' {
                    if ($this.selectedTaskIndex -lt $this.tasks.Count) {
                        $task = $this.tasks[$this.selectedTaskIndex]
                        $this.multiSelect[$task.id] = -not $this.multiSelect[$task.id]
                        $this.DrawMultiSelectMode()
                    }
                }
                'A' {
                    foreach ($task in $this.tasks) {
                        $this.multiSelect[$task.id] = $true
                    }
                    $this.DrawMultiSelectMode()
                }
                'N' {
                    $this.multiSelect.Clear()
                    $this.DrawMultiSelectMode()
                }
                'C' {
                    # Complete selected tasks
                    $selectedIds = @($this.multiSelect.Keys | Where-Object { $this.multiSelect[$_] })
                    if ($selectedIds.Count -gt 0) {
                        try {
                            $count = $selectedIds.Count
                            $data = Get-PmcAllData
                            foreach ($id in $selectedIds) {
                                $task = $data.tasks | Where-Object { $_.id -eq $id } | Select-Object -First 1
                                if ($task) {
                                    $task.status = 'completed'
                                    $task.completed = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
                                }
                            }
                            Save-PmcData -Data $data -Action "Completed $count tasks"
                            $this.multiSelect.Clear()
                            $this.LoadTasks()
                            $this.GoBackOr('tasklist')
                            $this.DrawTaskList()
                            $this.ShowSuccessMessage("Completed $count tasks")
                        } catch {}
                    }
                    break
                }
                'X' {
                    # Delete selected tasks
                    $selectedIds = @($this.multiSelect.Keys | Where-Object { $this.multiSelect[$_] })
                    if ($selectedIds.Count -gt 0) {
                        try {
                            $count = $selectedIds.Count
                            $data = Get-PmcAllData
                            $data.tasks = @($data.tasks | Where-Object { $selectedIds -notcontains $_.id })
                            Save-PmcData -Data $data -Action "Deleted $count tasks"
                            $this.multiSelect.Clear()
                            $this.LoadTasks()
                            $this.GoBackOr('tasklist')
                            $this.DrawTaskList()
                            $this.ShowSuccessMessage("Deleted $count tasks")
                        } catch {}
                    }
                    break
                }
                'P' {
                    # Set priority for selected tasks
                    $selectedIds = @($this.multiSelect.Keys | Where-Object { $this.multiSelect[$_] })
                    if ($selectedIds.Count -gt 0) {
                        $this.currentView = 'multipriority'
                        $this.DrawMultiPrioritySelect($selectedIds)
                        $this.HandleMultiPrioritySelect($selectedIds)
                    }
                    break
                }
                'M' {
                    # Move selected tasks to project
                    $selectedIds = @($this.multiSelect.Keys | Where-Object { $this.multiSelect[$_] })
                    if ($selectedIds.Count -gt 0) {
                        $this.currentView = 'multiproject'
                        $this.DrawMultiProjectSelect($selectedIds)
                        $this.HandleMultiProjectSelect($selectedIds)
                    }
                    break
                }
                'Escape' {
                    $this.multiSelect.Clear()
                    $this.GoBackOr('tasklist')
                    $this.DrawTaskList()
                    break
                }
            }
        }
    }

    [void] DrawMultiPrioritySelect([array]$taskIds) {
        $this.terminal.Clear()

        $title = " Set Priority for $($taskIds.Count) tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $priorities = @('high', 'medium', 'low', 'none')
        $y = 5
        for ($i = 0; $i -lt $priorities.Count; $i++) {
            $pri = $priorities[$i]
            $color = switch ($pri) {
                'high' { [PmcVT100]::Red() }
                'medium' { [PmcVT100]::Yellow() }
                'low' { [PmcVT100]::Green() }
                default { "" }
            }
            if ($color) {
                $this.terminal.WriteAtColor(4, $y + $i, "  $pri", $color, "")
            } else {
                $this.terminal.WriteAt(4, $y + $i, "  $pri")
            }
        }

        $this.terminal.DrawFooter("↑↓:Nav | Enter:Select | Esc:Cancel")
    }

    [void] HandleMultiPrioritySelect([array]$taskIds) {
        $priorities = @('high', 'medium', 'low', 'none')
        $selectedIdx = 1  # Default to medium

        while ($true) {
            $this.terminal.Clear()
            $title = " Set Priority for $($taskIds.Count) tasks "
            $titleX = ($this.terminal.Width - $title.Length) / 2
            $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

            $y = 5
            for ($i = 0; $i -lt $priorities.Count; $i++) {
                $pri = $priorities[$i]
                $prefix = if ($i -eq $selectedIdx) { "> " } else { "  " }
                $color = switch ($pri) {
                    'high' { [PmcVT100]::Red() }
                    'medium' { [PmcVT100]::Yellow() }
                    'low' { [PmcVT100]::Green() }
                    default { "" }
                }
                if ($color) {
                    $this.terminal.WriteAtColor(4, $y + $i, "$prefix$pri", $color, "")
                } else {
                    $this.terminal.WriteAt(4, $y + $i, "$prefix$pri")
                }
            }

            $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
            $this.terminal.WriteAt(2, $this.terminal.Height - 1, "↑↓:Navigate | Enter:Select | Esc:Cancel")

            $key = [Console]::ReadKey($true)
            switch ($key.Key) {
                'UpArrow' {
                    if ($selectedIdx -gt 0) { $selectedIdx-- }
                }
                'DownArrow' {
                    if ($selectedIdx -lt $priorities.Count - 1) { $selectedIdx++ }
                }
                'Enter' {
                    $selectedPri = $priorities[$selectedIdx]
                    try {
                        $count = $taskIds.Count
                        $data = Get-PmcAllData
                        foreach ($id in $taskIds) {
                            $task = $data.tasks | Where-Object { $_.id -eq $id } | Select-Object -First 1
                            if ($task) {
                                $task.priority = if ($selectedPri -eq 'none') { $null } else { $selectedPri }
                            }
                        }
                        Save-PmcData -Data $data -Action "Set priority to $selectedPri for $count tasks"
                        $this.multiSelect.Clear()
                        $this.LoadTasks()
                        $this.GoBackOr('tasklist')
                        $this.DrawTaskList()
                        $this.ShowSuccessMessage("Set priority to $selectedPri for $count tasks")
                    } catch {}
                    break
                }
                'Escape' {
                    $this.currentView = 'multiselect'
                    $this.DrawMultiSelectMode()
                    $this.HandleMultiSelectMode()
                    break
                }
            }
        }
    }

    [void] DrawMultiProjectSelect([array]$taskIds) {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Move $($taskIds.Count) tasks to Project "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $projectList = @($data.projects | ForEach-Object { if ($_ -is [string]) { $_ } else { $_.name } } | Where-Object { $_ })

            if ($projectList.Count -eq 0) {
                $this.terminal.WriteAtColor(4, 6, "No projects available", [PmcVT100]::Yellow(), "")
            } else {
                $y = 6
                $this.terminal.WriteAtColor(4, $y++, "Select Project:", [PmcVT100]::Cyan(), "")
                $y++
                for ($i = 0; $i -lt $projectList.Count; $i++) {
                    $this.terminal.WriteAt(4, $y++, "  $($projectList[$i])")
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading projects: $($_.Exception.Message)", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑↓:Nav | Enter:Select | Esc:Cancel")
    }

    [void] HandleMultiProjectSelect([array]$taskIds) {
        try {
            $data = Get-PmcAllData
            $projectList = @($data.projects | ForEach-Object { if ($_ -is [string]) { $_ } else { $_.name } } | Where-Object { $_ })

            if ($projectList.Count -eq 0) {
                [Console]::ReadKey($true) | Out-Null
                $this.currentView = 'multiselect'
                $this.DrawMultiSelectMode()
                $this.HandleMultiSelectMode()
                return
            }

            $selectedIdx = 0

            while ($true) {
                $this.terminal.Clear()
                $this.menuSystem.DrawMenuBar()

                $title = " Move $($taskIds.Count) tasks to Project "
                $titleX = ($this.terminal.Width - $title.Length) / 2
                $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

                $y = 6
                $this.terminal.WriteAtColor(4, $y++, "Select Project:", [PmcVT100]::Cyan(), "")
                $y++
                for ($i = 0; $i -lt $projectList.Count; $i++) {
                    $prefix = if ($i -eq $selectedIdx) { "> " } else { "  " }
                    $color = if ($i -eq $selectedIdx) { [PmcVT100]::Yellow() } else { "" }
                    if ($color) {
                        $this.terminal.WriteAtColor(4, $y++, "$prefix$($projectList[$i])", $color, "")
                    } else {
                        $this.terminal.WriteAt(4, $y++, "$prefix$($projectList[$i])")
                    }
                }

                $this.terminal.DrawFooter("↑↓:Nav | Enter:Select | Esc:Cancel")

                $key = [Console]::ReadKey($true)
                switch ($key.Key) {
                    'UpArrow' {
                        if ($selectedIdx -gt 0) { $selectedIdx-- }
                    }
                    'DownArrow' {
                        if ($selectedIdx -lt $projectList.Count - 1) { $selectedIdx++ }
                    }
                    'Enter' {
                        $targetProject = $projectList[$selectedIdx]
                        try {
                            $count = $taskIds.Count
                            $data = Get-PmcAllData
                            foreach ($id in $taskIds) {
                                $task = $data.tasks | Where-Object { $_.id -eq $id } | Select-Object -First 1
                                if ($task) {
                                    $task.project = $targetProject
                                }
                            }
                            Save-PmcData -Data $data -Action "Moved $count tasks to project $targetProject"
                            $this.multiSelect.Clear()
                            $this.LoadTasks()
                            $this.GoBackOr('tasklist')
                            $this.DrawTaskList()
                            $this.ShowSuccessMessage("Moved $count tasks to $targetProject")
                        } catch {}
                        break
                    }
                    'Escape' {
                        $this.currentView = 'multiselect'
                        $this.DrawMultiSelectMode()
                        $this.HandleMultiSelectMode()
                        break
                    }
                }
            }
        } catch {
            [Console]::ReadKey($true) | Out-Null
            $this.currentView = 'multiselect'
            $this.DrawMultiSelectMode()
            $this.HandleMultiSelectMode()
        }
    }

    [void] DrawTomorrowView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Tomorrow's Tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgCyan(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $tomorrow = (Get-Date).AddDays(1).Date
            $taskList = @($data.tasks | Where-Object {
                if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                $d = Get-ConsoleUIDateOrNull $_.due
                if ($d) { return ($d.Date -eq $tomorrow) } else { return $false }
            })

            if ($taskList.Count -gt 0) {
                $this.terminal.WriteAtColor(4, 6, "$($taskList.Count) task(s) due tomorrow:", [PmcVT100]::Cyan(), "")
                $y = 8
                foreach ($task in $taskList) {
                    if ($y -ge $this.terminal.Height - 3) { break }
                    $pri = if ($task.priority) { "[$($task.priority)]" } else { "" }
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $pri $($task.text)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAt(4, $y++, "[$($task.id)] $pri $($task.text)")
                    }
                }
            } else {
                $this.terminal.WriteAtColor(4, 6, "No tasks due tomorrow", [PmcVT100]::Green(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawWeekView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " This Week's Tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgGreen(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $today = (Get-Date).Date
            $weekEnd = $today.AddDays(7)

            # Get overdue tasks
            $overdue = @($data.tasks | Where-Object {
                if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                $d = Get-ConsoleUIDateOrNull $_.due
                if ($d) { return ($d.Date -lt $today) } else { return $false }
            } | Sort-Object { ($tmp = Get-ConsoleUIDateOrNull $_.due); if ($tmp) { $tmp } else { [DateTime]::MaxValue } })

            # Get tasks due this week
            $thisWeek = @($data.tasks | Where-Object {
                if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                $d = Get-ConsoleUIDateOrNull $_.due
                if ($d) { return ($d.Date -ge $today -and $d.Date -le $weekEnd) } else { return $false }
            } | Sort-Object { ($tmp = Get-ConsoleUIDateOrNull $_.due); if ($tmp) { $tmp } else { [DateTime]::MaxValue } })

            $y = 6

            # Show overdue tasks first
            if ($overdue.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "=== OVERDUE ($($overdue.Count)) ===", [PmcVT100]::BgRed(), [PmcVT100]::White())
                $y++
                $idx = 0
                foreach ($task in $overdue) {
                    if ($y -ge $this.terminal.Height - 3) { break }
                    $dueDate = Get-ConsoleUIDateOrNull $task.due
                    if (-not $dueDate) { continue }
                    $daysOverdue = ($today - $dueDate.Date).Days
                    if ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $($dueDate.ToString('MMM dd')) ($daysOverdue days ago) - $($task.text)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $($dueDate.ToString('MMM dd')) ($daysOverdue days ago) - $($task.text)", [PmcVT100]::Red(), "")
                    }
                    $idx++
                }
                $y++
            }

            # Show this week's tasks
            if ($thisWeek.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "=== DUE THIS WEEK ($($thisWeek.Count)) ===", [PmcVT100]::Green(), "")
                $y++
                foreach ($task in $thisWeek) {
                    if ($y -ge $this.terminal.Height - 3) { break }
                    $dueDate = Get-ConsoleUIDateOrNull $task.due
                    if (-not $dueDate) { continue }
                    $dayName = $dueDate.ToString('ddd MMM dd')
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $dayName - $($task.text)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAt(4, $y++, "[$($task.id)] $dayName - $($task.text)")
                    }
                }
            }

            if ($overdue.Count -eq 0 -and $thisWeek.Count -eq 0) {
                $this.terminal.WriteAtColor(4, $y, "No tasks due this week", [PmcVT100]::Green(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawMonthView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " This Month's Tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $today = (Get-Date).Date
            $monthEnd = $today.AddDays(30)

            # Get overdue tasks
            $overdue = @($data.tasks | Where-Object {
                if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                $d = Get-ConsoleUIDateOrNull $_.due
                if ($d) { return ($d.Date -lt $today) } else { return $false }
            } | Sort-Object { ($tmp = Get-ConsoleUIDateOrNull $_.due); if ($tmp) { $tmp } else { [DateTime]::MaxValue } })

            # Get tasks due this month
            $thisMonth = @($data.tasks | Where-Object {
                if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                $d = Get-ConsoleUIDateOrNull $_.due
                if ($d) { return ($d.Date -ge $today -and $d.Date -le $monthEnd) } else { return $false }
            } | Sort-Object { ($tmp = Get-ConsoleUIDateOrNull $_.due); if ($tmp) { $tmp } else { [DateTime]::MaxValue } })

            # Get undated tasks
            $undated = @($data.tasks | Where-Object { $_.status -ne 'completed' -and -not $_.due })

            $y = 6

            # Show overdue tasks first
            if ($overdue.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "=== OVERDUE ($($overdue.Count)) ===", [PmcVT100]::BgRed(), [PmcVT100]::White())
                $y++
                foreach ($task in $overdue) {
                    if ($y -ge $this.terminal.Height - 5) { break }
                    $dueDate = Get-ConsoleUIDateOrNull $task.due
                    if (-not $dueDate) { continue }
                    $daysOverdue = ($today - $dueDate.Date).Days
                    $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $($dueDate.ToString('MMM dd')) ($daysOverdue days ago) - $($task.text)", [PmcVT100]::Red(), "")
                }
                $y++
            }

            # Show this month's tasks
            if ($thisMonth.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "=== DUE THIS MONTH ($($thisMonth.Count)) ===", [PmcVT100]::Cyan(), "")
                $y++
                foreach ($task in $thisMonth) {
                    if ($y -ge $this.terminal.Height - 5) { break }
                    $dueDate = Get-ConsoleUIDateOrNull $task.due
                    if (-not $dueDate) { continue }
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $($dueDate.ToString('MMM dd')) - $($task.text)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAt(4, $y++, "[$($task.id)] $($dueDate.ToString('MMM dd')) - $($task.text)")
                    }
                }
                $y++
            }

            # Show undated tasks
            if ($undated.Count -gt 0 -and $y -lt $this.terminal.Height - 5) {
                $this.terminal.WriteAtColor(4, $y++, "=== NO DUE DATE ($($undated.Count)) ===", [PmcVT100]::Yellow(), "")
                $y++
                foreach ($task in $undated) {
                    if ($y -ge $this.terminal.Height - 3) { break }
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $($task.text)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $($task.text)", [PmcVT100]::Yellow(), "")
                    }
                }
            }

            if ($overdue.Count -eq 0 -and $thisMonth.Count -eq 0 -and $undated.Count -eq 0) {
                $this.terminal.WriteAtColor(4, $y, "No active tasks", [PmcVT100]::Green(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawNoDueDateView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Tasks Without Due Date "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgYellow(), [PmcVT100]::Black())

        try {
            $data = Get-PmcAllData
            $taskList = @($data.tasks | Where-Object { $_.status -ne 'completed' -and -not $_.due })

            if ($taskList.Count -gt 0) {
                $this.terminal.WriteAtColor(4, 6, "$($taskList.Count) task(s) without due date:", [PmcVT100]::Yellow(), "")
                $y = 8
                foreach ($task in $taskList) {
                    if ($y -ge $this.terminal.Height - 3) { break }
                    $proj = if ($task.project) { "@$($task.project)" } else { "" }
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $($task.text) $proj", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAt(4, $y++, "[$($task.id)] $($task.text) $proj")
                    }
                }
            } else {
                $this.terminal.WriteAtColor(4, 6, "All tasks have due dates!", [PmcVT100]::Green(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawNextActionsView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Next Actions "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgGreen(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            # Next actions: high priority, not blocked, active status
            $taskList = @($data.tasks | Where-Object {
                $_.status -ne 'completed' -and
                $_.status -ne 'blocked' -and
                $_.status -ne 'waiting' -and
                ($_.priority -eq 'high' -or -not $_.due -or ((($d = Get-ConsoleUIDateOrNull $_.due)) -and $d.Date -le (Get-Date).AddDays(7)))
            } | Sort-Object {
                if ($_.priority -eq 'high') { 0 }
                elseif ($_.priority -eq 'medium') { 1 }
                else { 2 }
            } | Select-Object -First 20)

            if ($taskList.Count -gt 0) {
                $this.terminal.WriteAtColor(4, 6, "$($taskList.Count) next action(s):", [PmcVT100]::Green(), "")
                $y = 8
                foreach ($task in $taskList) {
                    if ($y -ge $this.terminal.Height - 3) { break }
                    $pri = if ($task.priority -eq 'high') { "[!]" } elseif ($task.priority -eq 'medium') { "[*]" } else { "[ ]" }
                    $dueDt = if ($task.due) { Get-ConsoleUIDateOrNull $task.due } else { $null }
                    $due = if ($dueDt) { " ($($dueDt.ToString('MMM dd')))" } else { "" }
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y++, "$pri [$($task.id)] $($task.text)$due", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAt(4, $y++, "$pri [$($task.id)] $($task.text)$due")
                    }
                }
            } else {
                $this.terminal.WriteAtColor(4, 6, "No next actions found", [PmcVT100]::Yellow(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawTodayView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Today's Tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $today = (Get-Date).Date

            # Get overdue tasks
            $overdue = @($data.tasks | Where-Object {
                if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                $d = Get-ConsoleUIDateOrNull $_.due
                if ($d) { return ($d.Date -lt $today) } else { return $false }
            } | Sort-Object { ($tmp = Get-ConsoleUIDateOrNull $_.due); if ($tmp) { $tmp } else { [DateTime]::MaxValue } })

            # Get today's tasks
            $todayTasks = @($data.tasks | Where-Object {
                if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                $d = Get-ConsoleUIDateOrNull $_.due
                if ($d) { return ($d.Date -eq $today) } else { return $false }
            })

            $y = 6

            # Show overdue tasks first
            if ($overdue.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "=== OVERDUE ($($overdue.Count)) ===", [PmcVT100]::BgRed(), [PmcVT100]::White())
                $y++
                foreach ($task in $overdue) {
                    if ($y -ge $this.terminal.Height - 3) { break }
                    $dueDate = Get-ConsoleUIDateOrNull $task.due
                    if (-not $dueDate) { continue }
                    $daysOverdue = ($today - $dueDate.Date).Days
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $($dueDate.ToString('MMM dd')) ($daysOverdue days ago) - $($task.text)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAtColor(4, $y++, "[$($task.id)] $($dueDate.ToString('MMM dd')) ($daysOverdue days ago) - $($task.text)", [PmcVT100]::Red(), "")
                    }
                }
                $y++
            }

            # Show today's tasks
            if ($todayTasks.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "=== DUE TODAY ($($todayTasks.Count)) ===", [PmcVT100]::Cyan(), "")
                $y++
                foreach ($task in $todayTasks) {
                    if ($y -ge $this.terminal.Height - 3) { break }
                    $priColor = switch ($task.priority) {
                        'high' { [PmcVT100]::Red() }
                        'medium' { [PmcVT100]::Yellow() }
                        'low' { [PmcVT100]::Green() }
                        default { "" }
                    }
                    $pri = if ($task.priority) { "[$($task.priority.Substring(0,1).ToUpper())] " } else { "" }
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) { $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "") }
                    if ($priColor -and -not $isSel) { $this.terminal.WriteAtColor(4, $y++, "$pri[$($task.id)] $($task.text)", $priColor, "") }
                    elseif ($isSel) { $this.terminal.WriteAtColor(4, $y++, "$pri[$($task.id)] $($task.text)", [PmcVT100]::BgBlue(), [PmcVT100]::White()) }
                    else { $this.terminal.WriteAt(4, $y++, "[$($task.id)] $($task.text)") }
                }
            }

            if ($overdue.Count -eq 0 -and $todayTasks.Count -eq 0) {
                $this.terminal.WriteAtColor(4, $y, "No tasks due today", [PmcVT100]::Green(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading today's tasks: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawOverdueView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Overdue Tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgRed(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $today = (Get-Date).Date
            $overdueTasks = @($data.tasks | Where-Object {
                if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                $d = Get-ConsoleUIDateOrNull $_.due
                if ($d) { return ($d.Date -lt $today) } else { return $false }
            })

            $y = 6
            if ($overdueTasks.Count -eq 0) {
                $this.terminal.WriteAtColor(4, $y, "No overdue tasks! 🎉", [PmcVT100]::Green(), "")
            } else {
                foreach ($task in $overdueTasks) {
                    $dueDate = Get-ConsoleUIDateOrNull $task.due
                    if (-not $dueDate) { continue }
                    $daysOverdue = ($today - $dueDate.Date).Days
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y, "[$($task.id)] ", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                        $this.terminal.WriteAtColor(10, $y, "$($task.text) ", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                        $this.terminal.WriteAtColor(70, $y, "($daysOverdue days overdue)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAtColor(4, $y, "[$($task.id)] ", [PmcVT100]::Red(), "")
                        $this.terminal.WriteAt(10, $y, "$($task.text) ")
                        $this.terminal.WriteAtColor(70, $y, "($daysOverdue days overdue)", [PmcVT100]::Red(), "")
                    }
                    $y++
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading overdue tasks: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawUpcomingView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Upcoming Tasks (Next 7 Days) "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $today = (Get-Date).Date
            $nextWeek = $today.AddDays(7)
            $upcomingTasks = @($data.tasks | Where-Object {
                if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                $d = Get-ConsoleUIDateOrNull $_.due
                if ($d) { return ($d.Date -gt $today -and $d.Date -le $nextWeek) } else { return $false }
            } | Sort-Object { ($tmp = Get-ConsoleUIDateOrNull $_.due); if ($tmp) { $tmp } else { [DateTime]::MaxValue } })

            $y = 6
            if ($upcomingTasks.Count -eq 0) {
                $this.terminal.WriteAtColor(4, $y, "No upcoming tasks in the next 7 days", [PmcVT100]::Cyan(), "")
            } else {
                foreach ($task in $upcomingTasks) {
                    $dueDate = Get-ConsoleUIDateOrNull $task.due
                    if (-not $dueDate) { continue }
                    $daysUntil = ($dueDate.Date - $today).Days
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y, "[$($task.id)] $($task.text) ", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                        $this.terminal.WriteAtColor(70, $y, "(in $daysUntil days)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAt(4, $y, "[$($task.id)] $($task.text) ")
                        $this.terminal.WriteAtColor(70, $y, "(in $daysUntil days)", [PmcVT100]::Cyan(), "")
                    }
                    $y++
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading upcoming tasks: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawKanbanView() {
        # Use New-Object to avoid property interpretation
        $kbColSc = New-Object int[] 3
        $kbColSc[0] = 0
        $kbColSc[1] = 0
        $kbColSc[2] = 0
        $selectedCol = 0
        $selectedRow = 0
        $kanbanActive = $true
        $focusTaskId = -1
        $focusCol = -1

        # Initialize variables outside the loop to avoid scope issues
        $data = $null
        [array]$columns = @()
        [int]$startY = 5
        [int]$headerHeight = 3
        [int]$gap = 3
        [int]$colWidth = 30
        [int]$columnHeight = 20

        while ($kanbanActive) {
            $this.terminal.Clear()
            $this.menuSystem.DrawMenuBar()

            $title = " Kanban Board "
            $titleX = ($this.terminal.Width - $title.Length) / 2
            $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

            try {
                $data = Get-PmcAllData

                # 3 column layout: TODO, IN PROGRESS, DONE
                $columns = @(
                    @{Name='TODO'; Status=@('active', 'todo', '', 'pending'); Tasks=@()}
                    @{Name='IN PROGRESS'; Status=@('in-progress', 'started', 'working'); Tasks=@()}
                    @{Name='DONE'; Status=@('completed', 'done'); Tasks=@()}
                )

                # Populate columns
                foreach ($task in $data.tasks) {
                    $taskStatus = if ($task.status) { $task.status.ToLower() } else { '' }
                    for ($i = 0; $i -lt $columns.Count; $i++) {
                        if ($columns[$i].Status -contains $taskStatus) {
                            $columns[$i].Tasks += $task
                            break
                        }
                    }
                }

                # If a focus task was requested (after move/done), locate it and set selection
                if ($focusTaskId -gt 0 -and $focusCol -ge 0 -and $focusCol -lt $columns.Count) {
                    $targetTasks = $columns[$focusCol].Tasks
                    $foundIndex = -1
                    for ($i = 0; $i -lt $targetTasks.Count; $i++) {
                        $t = $targetTasks[$i]
                        try { if ([int]$t.id -eq $focusTaskId) { $foundIndex = $i; break } } catch {}
                    }
                    if ($foundIndex -ge 0) {
                        $selectedCol = $focusCol
                        $selectedRow = $foundIndex
                        # Ensure scroll shows the selected row
                        $visibleRows = $this.terminal.Height - $startY - $headerHeight - 2
                        if ($selectedRow -lt $kbColSc[$selectedCol]) { $kbColSc[$selectedCol] = $selectedRow }
                        if ($selectedRow -ge $kbColSc[$selectedCol] + $visibleRows) { $kbColSc[$selectedCol] = [Math]::Max(0, $selectedRow - $visibleRows + 1) }
                    }
                    # Clear focus request
                    $focusTaskId = -1
                    $focusCol = -1
                }

                # Calculate column dimensions with gaps
                $gap = 3
                $availableWidth = $this.terminal.Width - 8  # Margins on sides
                $colWidth = [math]::Floor(($availableWidth - ($gap * 2)) / 3)
                $startY = 5
                $headerHeight = 3
                $columnHeight = $this.terminal.Height - $startY - $headerHeight - 2

                # Draw columns with rounded bordered boxes
                for ($i = 0; $i -lt $columns.Count; $i++) {
                    $col = $columns[$i]
                    $x = 4 + ($colWidth + $gap) * $i

                    # Draw rounded box border
                    # Top border with rounded corners
                    $this.terminal.WriteAtColor($x, $startY, "╭" + ("─" * ($colWidth - 2)) + "╮", [PmcVT100]::Cyan(), "")

                    # Column header
                    $headerText = " $($col.Name) ($($col.Tasks.Count)) "
                    $headerPadding = [math]::Floor(($colWidth - $headerText.Length) / 2)
                    $headerLine = (" " * $headerPadding) + $headerText
                    $headerLine = $headerLine.PadRight($colWidth - 2)
                    $this.terminal.WriteAtColor($x, $startY + 1, "│", [PmcVT100]::Gray(), "")
                    $this.terminal.WriteAtColor($x + 1, $startY + 1, $headerLine, [PmcVT100]::White(), "")
                    $this.terminal.WriteAtColor($x + $colWidth - 1, $startY + 1, "│", [PmcVT100]::Gray(), "")

                    # Separator under header
                    $this.terminal.WriteAtColor($x, $startY + 2, "├" + ("─" * ($colWidth - 2)) + "┤", [PmcVT100]::Gray(), "")

                    # Side borders for content area
                    for ($row = 0; $row -lt $columnHeight; $row++) {
                        $this.terminal.WriteAtColor($x, $startY + 3 + $row, "│", [PmcVT100]::Gray(), "")
                        $this.terminal.WriteAtColor($x + $colWidth - 1, $startY + 3 + $row, "│", [PmcVT100]::Gray(), "")
                    }

                    # Bottom border with rounded corners
                    $this.terminal.WriteAtColor($x, $startY + 3 + $columnHeight, "╰" + ("─" * ($colWidth - 2)) + "╯", [PmcVT100]::Gray(), "")
                }

                # Draw tasks in columns (scrollable)
                for ($i = 0; $i -lt $columns.Count; $i++) {
                    $col = $columns[$i]
                    $x = 4 + ($colWidth + $gap) * $i
                    $contentWidth = $colWidth - 4  # Account for borders and padding

                    $visibleStart = $kbColSc[$i]
                    $visibleEnd = [math]::Min($visibleStart + $columnHeight, $col.Tasks.Count)

                    for ($taskIdx = $visibleStart; $taskIdx -lt $visibleEnd; $taskIdx++) {
                        $task = $col.Tasks[$taskIdx]
                        $displayRow = $taskIdx - $visibleStart
                        $row = $startY + 3 + $displayRow

                        # Build task display text
                        $pri = if ($task.priority -eq 'high') { "!" } elseif ($task.priority -eq 'medium') { "*" } else { " " }
                        $due = if ($task.due) { " " + (Get-Date -Date $task.due).ToString('MM/dd') } else { "" }
                        $text = "$pri #$($task.id) $($task.text)$due"

                        if ($text.Length -gt $contentWidth) {
                            $text = $text.Substring(0, $contentWidth - 3) + "..."
                        }
                        $text = " " + $text.PadRight($contentWidth)

                        # Highlight if selected
                        if ($i -eq $selectedCol -and $taskIdx -eq ($selectedRow + $kbColSc[$i])) {
                            $this.terminal.WriteAtColor($x + 1, $row, $text, [PmcVT100]::BgCyan(), [PmcVT100]::White())
                        } else {
                            # Color by priority
                            $taskColor = switch ($task.priority) {
                                'high' { [PmcVT100]::Red() }
                                'medium' { [PmcVT100]::Yellow() }
                                default { "" }
                            }
                            if ($taskColor) {
                                $this.terminal.WriteAtColor($x + 1, $row, $text, $taskColor, "")
                            } else {
                                $this.terminal.WriteAt($x + 1, $row, $text)
                            }
                        }
                    }

                    # Show scroll indicator if needed
                    if ($col.Tasks.Count -gt $columnHeight) {
                        $scrollInfo = "[$($visibleStart + 1)-$visibleEnd/$($col.Tasks.Count)]"
                        $scrollX = $x + $colWidth - $scrollInfo.Length - 2
                        $this.terminal.WriteAtColor($scrollX, $startY + 3 + $columnHeight, $scrollInfo, [PmcVT100]::Gray(), "")
                    }
                }

                $this.terminal.DrawFooter("←→:Column | ↑↓:Scroll | 1-3:Move | Enter:Edit | D:Done | Esc:Exit")

            } catch {
                $this.terminal.WriteAtColor(4, 6, "Error loading kanban: $_", [PmcVT100]::Red(), "")
            }

            # Handle input
            $key = [Console]::ReadKey($true)

            # Check for global keys first
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                $this.ProcessMenuAction($globalAction)
                return
            }

            switch ($key.Key) {
                'LeftArrow' {
                    if ($selectedCol -gt 0) {
                        $selectedCol--
                        $selectedRow = 0
                    }
                }
                'RightArrow' {
                    if ($selectedCol -lt 2) {  # 3 columns: 0, 1, 2
                        $selectedCol++
                        $selectedRow = 0
                    }
                }
                'UpArrow' {
                    if ($selectedRow -gt 0) {
                        $selectedRow--
                        # Adjust scroll if needed
                        if ($selectedRow -lt $kbColSc[$selectedCol]) {
                            $kbColSc[$selectedCol] = $selectedRow
                        }
                    }
                }
                'DownArrow' {
                    $col = $columns[$selectedCol]
                    $maxRow = $col.Tasks.Count - 1
                    if ($selectedRow -lt $maxRow) {
                        $selectedRow++
                        # Adjust scroll if needed
                        $visibleRows = $this.terminal.Height - $startY - $headerHeight - 2
                        if ($selectedRow -ge $kbColSc[$selectedCol] + $visibleRows) {
                            $kbColSc[$selectedCol] = $selectedRow - $visibleRows + 1
                        }
                    }
                }
                'D' {
                    # Mark task as done
                    $col = $columns[$selectedCol]
                    if ($col.Tasks.Count -gt $selectedRow) {
                        $task = $col.Tasks[$selectedRow]
                        try {
                            $tid = try { [int]$task.id } catch { 0 }
                            $task.status = 'done'
                            Save-PmcData -Data $data -Action "Marked task $($task.id) as done"
                            Show-InfoMessage -Message "Task marked as done!" -Title "Success" -Color "Green"
                            # Focus this task in DONE column on next refresh
                            $focusTaskId = $tid
                            $focusCol = 2
                        } catch {
                            Show-InfoMessage -Message "Failed to update task: $_" -Title "Error" -Color "Red"
                        }
                    }
                }
                'Escape' {
                    $kanbanActive = $false
                    $this.GoBackOr('tasklist')
                }
            }

            # Number keys 1-3 to move task to column
            if ($key.KeyChar -ge '1' -and $key.KeyChar -le '3') {
                $targetCol = [int]$key.KeyChar.ToString() - 1
                $col = $columns[$selectedCol]
                if ($col.Tasks.Count -gt $selectedRow) {
                    $task = $col.Tasks[$selectedRow]
                    $newStatus = $columns[$targetCol].Status[0]

                    try {
                        $tid = try { [int]$task.id } catch { 0 }
                        $task.status = $newStatus
                        Save-PmcData -Data $data -Action "Moved task $($task.id) to $($columns[$targetCol].Name)"
                        # Focus this task in target column on next refresh
                        $focusTaskId = $tid
                        $focusCol = $targetCol
                    } catch {
                        Show-InfoMessage -Message "Failed to move task: $_" -Title "Error" -Color "Red"
                    }
                }
            }
        }
    }

    [void] DrawAgendaView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Agenda View "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $activeTasks = @($data.tasks | Where-Object { $_.status -ne 'completed' })

            # Group tasks by date
            $overdue = @()
            $today = @()
            $tomorrow = @()
            $thisWeek = @()
            $later = @()
            $noDue = @()

            $nowDate = Get-Date
            $todayDate = $nowDate.Date
            $tomorrowDate = $todayDate.AddDays(1)
            $weekEndDate = $todayDate.AddDays(7)

            foreach ($task in $activeTasks) {
                if ($task.due) {
                    try {
                        $tmp = Get-ConsoleUIDateOrNull $task.due
                        if (-not $tmp) { throw 'invalid date' }
                        $dueDate = $tmp.Date
                        if ($dueDate -lt $todayDate) {
                            $overdue += $task
                        } elseif ($dueDate -eq $todayDate) {
                            $today += $task
                        } elseif ($dueDate -eq $tomorrowDate) {
                            $tomorrow += $task
                        } elseif ($dueDate -le $weekEndDate) {
                            $thisWeek += $task
                        } else {
                            $later += $task
                        }
                    } catch {
                        $noDue += $task
                    }
                } else {
                    $noDue += $task
                }
            }

            $y = 6

            if ($overdue.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "OVERDUE ($($overdue.Count)):", [PmcVT100]::Red(), "")
                foreach ($task in ($overdue | Select-Object -First 5)) {
                    $dueDate = Get-ConsoleUIDateOrNull $task.due
                    if (-not $dueDate) { continue }
                    $daysOverdue = ($todayDate - $dueDate.Date).Days
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(4, $y, ">", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(6, $y++, "[$($task.id)] $($task.text) (-$daysOverdue days)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAtColor(6, $y++, "[$($task.id)] $($task.text) (-$daysOverdue days)", [PmcVT100]::Red(), "")
                    }
                }
                if ($overdue.Count -gt 5) {
                    $this.terminal.WriteAtColor(6, $y++, "... and $($overdue.Count - 5) more", [PmcVT100]::Gray(), "")
                }
                $y++
            }

            if ($today.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "TODAY ($($today.Count)):", [PmcVT100]::Yellow(), "")
                foreach ($task in ($today | Select-Object -First 5)) {
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(4, $y, ">", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(6, $y++, "[$($task.id)] $($task.text)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAtColor(6, $y++, "[$($task.id)] $($task.text)", [PmcVT100]::Yellow(), "")
                    }
                }
                if ($today.Count -gt 5) {
                    $this.terminal.WriteAtColor(6, $y++, "... and $($today.Count - 5) more", [PmcVT100]::Gray(), "")
                }
                $y++
            }

            if ($tomorrow.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "TOMORROW ($($tomorrow.Count)):", [PmcVT100]::Cyan(), "")
                foreach ($task in ($tomorrow | Select-Object -First 3)) {
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(4, $y, ">", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(6, $y++, "[$($task.id)] $($task.text)", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAtColor(6, $y++, "[$($task.id)] $($task.text)", [PmcVT100]::Cyan(), "")
                    }
                }
                if ($tomorrow.Count -gt 3) {
                    $this.terminal.WriteAtColor(6, $y++, "... and $($tomorrow.Count - 3) more", [PmcVT100]::Gray(), "")
                }
                $y++
            }

            if ($thisWeek.Count -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "THIS WEEK ($($thisWeek.Count)):", [PmcVT100]::Green(), "")
                foreach ($task in ($thisWeek | Select-Object -First 3)) {
                    $dueDate = Get-ConsoleUIDateOrNull $task.due
                    if (-not $dueDate) { continue }
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    if ($isSel) {
                        $this.terminal.WriteAtColor(4, $y, ">", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(6, $y++, "[$($task.id)] $($task.text) ($($dueDate.ToString('ddd MMM dd')))", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAt(6, $y++, "[$($task.id)] $($task.text) ($($dueDate.ToString('ddd MMM dd')))")
                    }
                }
                if ($thisWeek.Count -gt 3) {
                    $this.terminal.WriteAt(6, $y++, "... and $($thisWeek.Count - 3) more")
                }
                $y++
            }

            if ($later.Count -gt 0) {
                $this.terminal.WriteAt(4, $y++, "LATER ($($later.Count))")
                $y++
            }

            if ($noDue.Count -gt 0) {
                $this.terminal.WriteAt(4, $y++, "NO DUE DATE ($($noDue.Count))")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading agenda: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawBlockedView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Blocked/Waiting Tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgYellow(), [PmcVT100]::Black())

        try {
            $data = Get-PmcAllData
            $blockedTasks = @($data.tasks | Where-Object {
                $_.status -eq 'blocked' -or $_.status -eq 'waiting'
            })

            $y = 6
            if ($blockedTasks.Count -eq 0) {
                $this.terminal.WriteAtColor(4, $y, "No blocked tasks", [PmcVT100]::Green(), "")
            } else {
                foreach ($task in $blockedTasks) {
                    $isSel = ($this.specialSelectedIndex -lt $this.specialItems.Count -and $this.specialItems[$this.specialSelectedIndex].id -eq $task.id)
                    $statusColor = if ($task.status -eq 'blocked') { [PmcVT100]::Red() } else { [PmcVT100]::Yellow() }
                    if ($isSel) {
                        $this.terminal.WriteAtColor(2, $y, "> ", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(4, $y, "[$($task.id)] $($task.text) ($($task.status))", [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    } else {
                        $this.terminal.WriteAt(4, $y, "[$($task.id)] $($task.text) ")
                        $this.terminal.WriteAtColor(70, $y, "($($task.status))", $statusColor, "")
                    }
                    $y++
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading blocked tasks: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑/↓:Select  Enter:Detail  E:Edit  D:Toggle  F10/Alt:Menus  Esc:Back")
    }

    # Legacy single-shot special view handler removed. Persistent handler is used for all these views.

    [void] DrawBackupView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Backup Data "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $file = Get-PmcTaskFilePath
            $backups = @()
            for ($i = 1; $i -le 9; $i++) {
                $bakFile = "$file.bak$i"
                if (Test-Path $bakFile) {
                    $info = Get-Item $bakFile
                    $backups += [PSCustomObject]@{
                        Number = $i
                        File = $bakFile
                        Size = $info.Length
                        Modified = $info.LastWriteTime
                    }
                }
            }

            if ($backups.Count -gt 0) {
                $this.terminal.WriteAtColor(4, 6, "Existing Backups:", [PmcVT100]::Cyan(), "")
                $y = 8
                foreach ($backup in $backups) {
                    $sizeKB = [math]::Round($backup.Size / 1KB, 2)
                    $line = "  .bak$($backup.Number)  -  $($backup.Modified.ToString('yyyy-MM-dd HH:mm:ss'))  -  $sizeKB KB"
                    $this.terminal.WriteAt(4, $y++, $line)
                }

                $y++
                $this.terminal.WriteAtColor(4, $y, "Main data file:", [PmcVT100]::Cyan(), "")
                $y++
                if (Test-Path $file) {
                    $mainInfo = Get-Item $file
                    $sizeKB = [math]::Round($mainInfo.Length / 1KB, 2)
                    $this.terminal.WriteAt(4, $y++, "  $file")
                    $this.terminal.WriteAt(4, $y++, "  Modified: $($mainInfo.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))  -  $sizeKB KB")
                }

                $y += 2
                $this.terminal.WriteAtColor(4, $y++, "Press 'C' to create manual backup now", [PmcVT100]::Green(), "")
                $this.terminal.WriteAt(4, $y, "Backups are automatically created on every save (up to 9 retained)")
            } else {
                $this.terminal.WriteAtColor(4, 8, "No backups found.", [PmcVT100]::Yellow(), "")
                $y = 10
                $this.terminal.WriteAt(4, $y++, "Backups are automatically created when data is saved.")
                $this.terminal.WriteAt(4, $y++, "Up to 9 backups are retained (.bak1 through .bak9)")
                $y++
                $this.terminal.WriteAtColor(4, $y, "Press 'C' to create manual backup now", [PmcVT100]::Green(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 8, "Error loading backup info: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "C:Create Backup | Esc:Back")
    }

    [void] HandleBackupView() {
        $this.DrawBackupView()
        $key = [Console]::ReadKey($true)

        if ($key.Key -eq 'C') {
            try {
                $file = Get-PmcTaskFilePath
                if (Test-Path $file) {
                    # Rotate backups
                    for ($i = 8; $i -ge 1; $i--) {
                        $src = "$file.bak$i"
                        $dst = "$file.bak$($i+1)"
                        if (Test-Path $src) {
                            Move-Item -Force $src $dst
                        }
                    }
                    Copy-Item $file "$file.bak1" -Force

                    $this.terminal.WriteAtColor(4, $this.terminal.Height - 3, "Backup created successfully", [PmcVT100]::Green(), "")
                    # Redraw to show updated backup list
                    $this.HandleBackupView()
                    return
                }
            } catch {
                $this.terminal.WriteAtColor(4, $this.terminal.Height - 3, "Error creating backup: $_", [PmcVT100]::Red(), "")
            }
        }

        $this.GoBackOr('tasklist')
    }

    [void] DrawTimerStatus() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Timer Status "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $status = Get-PmcTimerStatus
            if ($status.Running) {
                $this.terminal.WriteAtColor(4, 6, "Timer is RUNNING", [PmcVT100]::Green(), "")
                $y = 8
                $this.terminal.WriteAt(4, $y++, "Started: $($status.StartTime)")
                $this.terminal.WriteAt(4, $y++, "Elapsed: $($status.Elapsed)h")
                if ($status.Task) {
                    $y++
                    $this.terminal.WriteAt(4, $y++, "Task: $($status.Task)")
                }
                if ($status.Project) {
                    $this.terminal.WriteAt(4, $y++, "Project: $($status.Project)")
                }
            } else {
                $this.terminal.WriteAtColor(4, 6, "Timer is not running", [PmcVT100]::Yellow(), "")
                if ($status.LastElapsed) {
                    $this.terminal.WriteAt(4, 8, "Last session: $($status.LastElapsed)h")
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    [void] DrawTimerStart() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Start Timer "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgGreen(), [PmcVT100]::White())

        try {
            $status = Get-PmcTimerStatus
            if ($status.Running) {
                $this.terminal.WriteAtColor(4, 6, "Timer is already running!", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAt(4, 8, "Started: $($status.StartTime)")
                $this.terminal.WriteAt(4, 9, "Elapsed: $($status.Elapsed)h")
            } else {
                $this.terminal.WriteAtColor(4, 6, "Press 'S' to start the timer", [PmcVT100]::Green(), "")
                $this.terminal.WriteAt(4, 8, "This will track your work time for logging.")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "S:Start | Esc:Cancel")
    }

    [void] DrawTimerStop() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Stop Timer "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgRed(), [PmcVT100]::White())

        try {
            $status = Get-PmcTimerStatus
            if ($status.Running) {
                $this.terminal.WriteAtColor(4, 6, "Timer is running", [PmcVT100]::Green(), "")
                $this.terminal.WriteAt(4, 8, "Started: $($status.StartTime)")
                $this.terminal.WriteAt(4, 9, "Elapsed: $($status.Elapsed)h")
                $y = 11
                $this.terminal.WriteAtColor(4, $y, "Press 'S' to stop and log this time", [PmcVT100]::Yellow(), "")
            } else {
                $this.terminal.WriteAtColor(4, 6, "Timer is not running", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAt(4, 8, "There is nothing to stop.")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "S:Stop | Esc:Cancel")
    }

    [void] DrawUndoView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Undo Last Change "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $status = Get-PmcUndoStatus
            if ($status.UndoAvailable) {
                $this.terminal.WriteAtColor(4, 6, "Undo stack has $($status.UndoCount) change(s) available", [PmcVT100]::Green(), "")
                $y = 8
                $this.terminal.WriteAt(4, $y++, "Last action: $($status.LastAction)")
                $y++
                $this.terminal.WriteAtColor(4, $y, "Press 'U' to undo the last change", [PmcVT100]::Yellow(), "")
            } else {
                $this.terminal.WriteAtColor(4, 6, "No changes available to undo", [PmcVT100]::Yellow(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "U:Undo | Esc:Cancel")
    }

    [void] DrawRedoView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Redo Last Undone Change "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $status = Get-PmcUndoStatus
            if ($status.RedoAvailable) {
                $this.terminal.WriteAtColor(4, 6, "Redo stack has $($status.RedoCount) change(s) available", [PmcVT100]::Green(), "")
                $y = 8
                $this.terminal.WriteAtColor(4, $y, "Press 'R' to redo the last undone change", [PmcVT100]::Yellow(), "")
            } else {
                $this.terminal.WriteAtColor(4, 6, "No changes available to redo", [PmcVT100]::Yellow(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "R:Redo | Esc:Cancel")
    }

    [void] DrawFocusClearView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Clear Focus "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        $this.terminal.WriteAtColor(4, $y++, "Clear the current focus selection.", [PmcVT100]::Cyan(), "")
        $y++
        $this.terminal.WriteAtColor(4, $y, "Press 'C' to clear focus.", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "C:Clear  F10/Alt:Menus  Esc:Back")
    }

    [void] DrawClearBackupsView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Clear Backup Files "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $file = Get-PmcTaskFilePath
            $backupCount = 0
            $totalSize = 0

            for ($i = 1; $i -le 9; $i++) {
                $bakFile = "$file.bak$i"
                if (Test-Path $bakFile) {
                    $backupCount++
                    $totalSize += (Get-Item $bakFile).Length
                }
            }

            $this.terminal.WriteAtColor(4, 6, "Automatic backups (.bak1 - .bak9):", [PmcVT100]::Cyan(), "")
            $this.terminal.WriteAt(4, 8, "  Count: $backupCount files")
            $sizeMB = [math]::Round($totalSize / 1MB, 2)
            $this.terminal.WriteAt(4, 9, "  Total size: $sizeMB MB")

            $y = 11
            $backupDir = Join-Path (Get-PmcRootPath) "backups"
            if (Test-Path $backupDir) {
                $manualBackups = @(Get-ChildItem $backupDir -Filter "*.json")
                $manualCount = $manualBackups.Count
                $manualSize = ($manualBackups | Measure-Object -Property Length -Sum).Sum
                $manualSizeMB = [math]::Round($manualSize / 1MB, 2)

                $this.terminal.WriteAtColor(4, $y++, "Manual backups (backups directory):", [PmcVT100]::Cyan(), "")
                $y++
                $this.terminal.WriteAt(4, $y++, "  Count: $manualCount files")
                $this.terminal.WriteAt(4, $y++, "  Total size: $manualSizeMB MB")
            }

            $y += 2
            if ($backupCount -gt 0) {
                $this.terminal.WriteAtColor(4, $y++, "Press 'A' to clear automatic backups (.bak files)", [PmcVT100]::Yellow(), "")
            }
            if (Test-Path $backupDir) {
                $this.terminal.WriteAtColor(4, $y++, "Press 'M' to clear manual backups (backups directory)", [PmcVT100]::Yellow(), "")
                $y++
                $this.terminal.WriteAtColor(4, $y, "Press 'B' to clear BOTH", [PmcVT100]::Red(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 8, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "A:Auto | M:Manual | B:Both | Esc:Cancel")
    }

    [void] HandleClearBackupsView() {
        $this.DrawClearBackupsView()
        $key = [Console]::ReadKey($true)

        if ($key.Key -eq 'A') {
            $confirmed = Show-ConfirmDialog -Message "Clear automatic backups (.bak files)?" -Title "Confirm"
            if ($confirmed) {
                try {
                    $file = Get-PmcTaskFilePath
                    $count = 0
                    for ($i = 1; $i -le 9; $i++) {
                        $bakFile = "$file.bak$i"
                        if (Test-Path $bakFile) {
                            Remove-Item $bakFile -Force
                            $count++
                        }
                    }
                    $this.terminal.WriteAtColor(4, $this.terminal.Height - 2, "Cleared $count automatic backup files", [PmcVT100]::Green(), "")
                } catch {
                    $this.terminal.WriteAtColor(4, $this.terminal.Height - 2, "Error: $_", [PmcVT100]::Red(), "")
                }
            }
            $this.GoBackOr('tasklist')
        } elseif ($key.Key -eq 'M') {
            $confirmed = Show-ConfirmDialog -Message "Clear manual backups (backups/*.json)?" -Title "Confirm"
            if ($confirmed) {
                try {
                    $backupDir = Join-Path (Get-PmcRootPath) "backups"
                    if (Test-Path $backupDir) {
                        $files = Get-ChildItem $backupDir -Filter "*.json"
                        $count = $files.Count
                        Remove-Item "$backupDir/*.json" -Force
                        $this.terminal.WriteAtColor(4, $this.terminal.Height - 2, "Cleared $count manual backup files", [PmcVT100]::Green(), "")
                    }
                } catch {
                    $this.terminal.WriteAtColor(4, $this.terminal.Height - 2, "Error: $_", [PmcVT100]::Red(), "")
                }
            }
            $this.GoBackOr('tasklist')
        } elseif ($key.Key -eq 'B') {
            $confirmed = Show-ConfirmDialog -Message "Clear ALL backups? (auto + manual)" -Title "Confirm"
            if ($confirmed) {
                try {
                    $file = Get-PmcTaskFilePath
                    $autoCount = 0
                    for ($i = 1; $i -le 9; $i++) {
                        $bakFile = "$file.bak$i"
                        if (Test-Path $bakFile) {
                            Remove-Item $bakFile -Force
                            $autoCount++
                        }
                    }
                    $manualCount = 0
                    $backupDir = Join-Path (Get-PmcRootPath) "backups"
                    if (Test-Path $backupDir) {
                        $files = Get-ChildItem $backupDir -Filter "*.json"
                        $manualCount = $files.Count
                        Remove-Item "$backupDir/*.json" -Force
                    }
                    $this.terminal.WriteAtColor(4, $this.terminal.Height - 2, "Cleared $autoCount auto + $manualCount manual backups", [PmcVT100]::Green(), "")
                } catch {
                    $this.terminal.WriteAtColor(4, $this.terminal.Height - 2, "Error: $_", [PmcVT100]::Red(), "")
                }
            }
            $this.GoBackOr('tasklist')
        } elseif ($key.Key -eq 'Escape') {
            $this.GoBackOr('tasklist')
        } else {
            $this.currentView = 'fileclearbackups'  # Refresh
        }
    }

    [void] DrawPlaceholder([string]$featureName) {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " $featureName "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $message = "This feature is not yet implemented in the TUI."
        $messageX = ($this.terminal.Width - $message.Length) / 2
        $this.terminal.WriteAtColor([int]$messageX, 8, $message, [PmcVT100]::Yellow(), "")

        $hint = "Use the PowerShell commands instead (Get-Command *Pmc*)"
        $hintX = ($this.terminal.Width - $hint.Length) / 2
        $this.terminal.WriteAt([int]$hintX, 10, $hint)

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    [void] DrawFocusSetForm() {
        $this.terminal.Clear()

        $title = " Set Focus Context "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 2, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAt(4, 5, "Project name:")
        $this.terminal.WriteAt(4, 6, "> ")

        try {
            $data = Get-PmcAllData
            $this.terminal.WriteAtColor(4, 8, "Available projects:", [PmcVT100]::Cyan(), "")
            $y = 9
            foreach ($proj in $data.projects) {
                $this.terminal.WriteAt(6, $y++, "• $($proj.name)")
            }
        } catch {}

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Type project name, Enter to set, Esc to cancel")
    }

    [void] HandleFocusSetForm() {
        # Get available projects
        $data = Get-PmcAllData
        $projectList = @('inbox') + @($data.projects | ForEach-Object { $_.name } | Where-Object { $_ -and $_ -ne 'inbox' } | Sort-Object)

        # Get current context as default
        $currentContext = if ($data.PSObject.Properties['currentContext']) { $data.currentContext } else { 'inbox' }

        # Show selection list
        $selected = Show-SelectList -Title "Select Focus Context" -Options $projectList -DefaultValue $currentContext

        if ($selected) {
            try {
                if (-not $data.PSObject.Properties['currentContext']) {
                    $data | Add-Member -NotePropertyName currentContext -NotePropertyValue $selected -Force
                } else {
                    $data.currentContext = $selected
                }
                Save-PmcData -Data $data -Action "Set focus to $selected"
                Show-InfoMessage -Message "Focus set to: $selected" -Title "Success" -Color "Green"
            } catch {
                Show-InfoMessage -Message "Failed to set focus: $_" -Title "Error" -Color "Red"
            }
        }

        $this.GoBackOr('tasklist')
    }

    [void] DrawFocusStatus() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Focus Status "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $currentContext = if ($data.PSObject.Properties['currentContext']) { $data.currentContext } else { 'inbox' }

            $this.terminal.WriteAtColor(4, 6, "Current Focus:", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAt(20, 6, $currentContext)

            if ($currentContext -and $currentContext -ne 'inbox') {
                $contextTasks = @($data.tasks | Where-Object {
                    $_.project -eq $currentContext -and $_.status -ne 'completed'
                })

                $this.terminal.WriteAtColor(4, 8, "Active Tasks:", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAt(18, 8, "$($contextTasks.Count)")

                $overdue = @($contextTasks | Where-Object {
                    if (-not $_.due) { return $false }
                    $d = Get-ConsoleUIDateOrNull $_.due
                    if ($d) { return ($d.Date -lt (Get-Date).Date) } else { return $false }
                })

                if ($overdue.Count -gt 0) {
                    $this.terminal.WriteAtColor(4, 9, "Overdue:", [PmcVT100]::Red(), "")
                    $this.terminal.WriteAt(18, 9, "$($overdue.Count)")
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading focus status: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    [void] HandleFocusStatusView() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'focusstatus') {
            $this.DrawFocusStatus()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'focusstatus') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('main')
    }

    [void] DrawTimeAddForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Add Time Entry "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Project:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(4, 8, "Task ID (optional):")
        $this.terminal.WriteAtColor(4, 10, "Minutes:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 12, "Description:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(4, 14, "Date (YYYY-MM-DD, default today):")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter fields | Esc=Cancel")
    }

    [void] HandleTimeAddForm() {
        # Unified input form for adding time
        $allData = Get-PmcAllData
        $projectNames = @($allData.projects | ForEach-Object { $_.name } | Where-Object { $_ } | Sort-Object)
        $options = @('(generic time code)') + $projectNames
        $fields = @(
            @{Name='hours'; Label='Hours (e.g., 1, 1.5, 2.25)'; Required=$true; Type='text'}
            @{Name='project'; Label='Project or Code Mode'; Required=$true; Type='select'; Options=$options}
            @{Name='timeCode'; Label='Time Code (if generic)'; Required=$false; Type='text'}
            @{Name='date'; Label='Date (today/tomorrow/+N/-N/YYYYMMDD or empty for today)'; Required=$false; Type='text'}
            @{Name='description'; Label='Description'; Required=$false; Type='text'}
        )
        $res = Show-InputForm -Title "Add Time Entry" -Fields $fields
        if ($null -eq $res) { $this.previousView=''; $this.currentView='timelist'; $this.RefreshCurrentView(); return }

        # Validate hours
        $hoursStr = [string]$res['hours']
        $hours = 0.0; if (-not [double]::TryParse($hoursStr, [ref]$hours) -or $hours -le 0) {
            Show-InfoMessage -Message "Invalid hours. Use numbers like 1, 1.5, 2.25." -Title "Validation" -Color "Red"
            $this.previousView=''; $this.currentView='timelist'; $this.RefreshCurrentView(); return
        }

        $selProject = [string]$res['project']
        $timeCode = $null; $isNumeric = $false
        if ($selProject -eq '(generic time code)') {
            $codeStr = [string]$res['timeCode']
            $codeVal = 0; if (-not [int]::TryParse(($codeStr+''), [ref]$codeVal) -or $codeVal -le 0) {
                Show-InfoMessage -Message "Invalid time code (must be a positive number)." -Title "Validation" -Color "Red"
                $this.previousView=''; $this.currentView='timelist'; $this.RefreshCurrentView(); return
            }
            $timeCode = $codeVal; $isNumeric = $true; $selProject = $null
        }

        # Parse date
        $dateInput = [string]$res['date']
        $dateOut = (Get-Date).Date
        if (-not [string]::IsNullOrWhiteSpace($dateInput)) {
            $trim = $dateInput.Trim().ToLower()
            $dt = $null
            if ($trim -eq 'today') { $dt = (Get-Date).Date }
            elseif ($trim -eq 'tomorrow') { $dt = (Get-Date).Date.AddDays(1) }
            elseif ($trim -match '^[+-]\d+$') { $dt = (Get-Date).Date.AddDays([int]$trim) }
            elseif ($trim -match '^\d{8}$') { try { $dt = [DateTime]::ParseExact($trim, 'yyyyMMdd', $null) } catch {} }
            if (-not $dt) { $dt = Get-ConsoleUIDateOrNull $trim }
            if ($dt) { $dateOut = $dt.Date }
        }

        # Build and save entry
        try {
            $entry = [pscustomobject]@{
                id = Get-PmcNextTimeId $allData
                project = $selProject
                id1 = if ($isNumeric) { $timeCode.ToString() } else { $null }
                id2 = $null
                date = $dateOut.ToString('yyyy-MM-dd')
                minutes = [int]([math]::Round($hours * 60))
                description = ([string]$res['description']).Trim()
                created = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
            }
            $data = Get-PmcAllData
            if (-not $data.PSObject.Properties['timelogs']) { $data | Add-Member -NotePropertyName timelogs -NotePropertyValue @() }
            $data.timelogs += $entry
            Save-PmcData -Data $data -Action ("Added time entry #{0} ({1} min)" -f $entry.id, $entry.minutes)
            $this.LoadTimeLogs()
            Show-InfoMessage -Message "Time entry added successfully!" -Title "Success" -Color "Green"
        } catch {
            Show-InfoMessage -Message "Failed to add time entry: $_" -Title "SAVE ERROR" -Color "Red"
        }
        $this.previousView=''; $this.currentView='timelist'; $this.RefreshCurrentView()
    }

    [void] DrawTimeList() {
        $this.LoadTimeLogs()
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Time Log "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            if (-not $this.timelogs -or $this.timelogs.Count -eq 0) {
                $emptyY = 8
                $this.terminal.WriteAtColor(4, $emptyY++, "No time entries yet", [PmcVT100]::Yellow(), "")
                $emptyY++
                $this.terminal.WriteAtColor(4, $emptyY++, "Press 'A' to add your first time entry", [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(4, $emptyY++, "Or press Alt+I then L to view this screen", [PmcVT100]::Gray(), "")
            } else {
                $headerY = 5
                $this.terminal.WriteAtColor(2, $headerY, "ID", [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(8, $headerY, "Date", [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(22, $headerY, "Project", [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(41, $headerY, "Hrs", [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(50, $headerY, "Description", [PmcVT100]::Cyan(), "")
                $this.terminal.DrawHorizontalLine(0, $headerY + 1, $this.terminal.Width)

                $startY = $headerY + 2
                $y = $startY

                for ($i = 0; $i -lt $this.timelogs.Count; $i++) {
                    if ($y -ge $this.terminal.Height - 3) { break }

                    $log = $this.timelogs[$i]

                    # Highlight selected entry
                    $prefix = if ($i -eq $this.selectedTimeIndex) { ">" } else { " " }
                    $bg = if ($i -eq $this.selectedTimeIndex) { [PmcVT100]::BgBlue() } else { "" }
                    $fg = if ($i -eq $this.selectedTimeIndex) { [PmcVT100]::White() } else { "" }

                    # Safe string handling with null checks and date normalization
                    $rawDate = if ($log.date) { $log.date.ToString() } else { "" }
                    # Normalize "today" to actual date
                    $dateStr = if ($rawDate -eq 'today') {
                        (Get-Date).ToString('yyyy-MM-dd')
                    } elseif ($rawDate -eq 'tomorrow') {
                        (Get-Date).AddDays(1).ToString('yyyy-MM-dd')
                    } else {
                        $rawDate
                    }
                    $projectStr = if ($log.project) { $log.project.ToString() } else { if ($log.id1) { "#$($log.id1)" } else { "" } }
                    $hours = if ($log.minutes) { [math]::Round($log.minutes / 60.0, 2) } else { 0 }
                    $hoursStr = $hours.ToString("0.00")
                    $descStr = if ($log.PSObject.Properties['description'] -and $log.description) { $log.description.ToString() } else { "" }

                    # Format columns with proper padding
                    $idCol = ($prefix + $log.id.ToString()).PadRight(5)
                    $dateCol = $dateStr.Substring(0, [Math]::Min(10, $dateStr.Length)).PadRight(13)
                    $projectCol = $projectStr.Substring(0, [Math]::Min(16, $projectStr.Length)).PadRight(18)
                    $hoursCol = $hoursStr.PadRight(8)

                    if ($bg) {
                        $this.terminal.WriteAtColor(2, $y, $idCol, $bg, $fg)
                        $this.terminal.WriteAtColor(8, $y, $dateCol, $bg, $fg)
                        $this.terminal.WriteAtColor(22, $y, $projectCol, $bg, $fg)
                        $this.terminal.WriteAtColor(41, $y, $hoursCol, $bg, [PmcVT100]::Cyan())
                        if ($descStr) {
                            $desc = $descStr.Substring(0, [Math]::Min(30, $descStr.Length))
                            $this.terminal.WriteAtColor(50, $y, $desc, $bg, $fg)
                        }
                    } else {
                        $this.terminal.WriteAtColor(2, $y, $idCol, [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(8, $y, $dateCol, [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(22, $y, $projectCol, [PmcVT100]::Gray(), "")
                        $this.terminal.WriteAtColor(41, $y, $hoursCol, [PmcVT100]::Cyan(), "")
                        if ($descStr) {
                            $desc = $descStr.Substring(0, [Math]::Min(30, $descStr.Length))
                            $this.terminal.WriteAtColor(50, $y, $desc, [PmcVT100]::Yellow(), "")
                        }
                    }
                    $y++
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading time log: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑↓:Nav | A:Add | E:Edit | Del:Delete | R:Report | Esc:Back")
    }

    [void] HandleTimeListView() {
        $this.DrawTimeList()
        $key = [Console]::ReadKey($true)

        # Check for global menu keys first
        $globalAction = $this.CheckGlobalKeys($key)
        if ($globalAction) {
            Write-ConsoleUIDebug "Global action from time list: $globalAction" "TIMELIST"
            if ($globalAction -eq 'app:exit') {
                $this.running = $false
                return
            }
            $this.ProcessMenuAction($globalAction)
            return
        }

        switch ($key.Key) {
            'UpArrow' {
                if ($this.selectedTimeIndex -gt 0) {
                    $this.selectedTimeIndex--
                }
                $this.DrawTimeList()
            }
            'DownArrow' {
                if ($this.selectedTimeIndex -lt $this.timelogs.Count - 1) {
                    $this.selectedTimeIndex++
                }
                $this.DrawTimeList()
            }
            'A' {
                $this.currentView = 'timeadd'
            }
            'E' {
                $this.currentView = 'timeedit'
            }
            'Delete' {
                # Delete time entry with confirmation
                if ($this.selectedTimeIndex -lt $this.timelogs.Count) {
                    $log = $this.timelogs[$this.selectedTimeIndex]

                    $confirmed = Show-ConfirmDialog -Message "Delete time entry #$($log.id) ($($log.minutes) min on $($log.project))?" -Title "Confirm Delete"
                    if ($confirmed) {
                        try {
                            $data = Get-PmcAllData
                            $data.timelogs = @($data.timelogs | Where-Object { $_.id -ne $log.id })
                            Save-PmcData -Data $data -Action "Deleted time entry #$($log.id)"
                            $this.LoadTimeLogs()
                            if ($this.selectedTimeIndex -ge $this.timelogs.Count -and $this.selectedTimeIndex -gt 0) {
                                $this.selectedTimeIndex--
                            }
                            Show-InfoMessage -Message "Time entry #$($log.id) deleted successfully" -Title "Success" -Color "Green"
                        } catch {
                            Show-InfoMessage -Message "Failed to delete time entry: $_" -Title "Error" -Color "Red"
                        }
                    }
                    $this.DrawTimeList()
                }
            }
            'R' {
                $this.currentView = 'timereport'
            }
            'Escape' {
                $this.GoBackOr('main')
            }
        }
    }

    [void] DrawTimeReport() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Time Report "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $timelogList = if ($data.PSObject.Properties['timelogs']) { $data.timelogs } else { @() }

            if ($timelogList.Count -eq 0) {
                $this.terminal.WriteAt(4, 6, "No time entries to report")
            } else {
                # Group by project
                $byProject = $timelogList | Group-Object -Property project | Sort-Object Name

                $this.terminal.WriteAtColor(4, 5, "Time Summary by Project:", [PmcVT100]::Yellow(), "")

                $headerY = 7
                $this.terminal.WriteAt(4, $headerY, "Project")
                $this.terminal.WriteAt(30, $headerY, "Entries")
                $this.terminal.WriteAt(42, $headerY, "Total Minutes")
                $this.terminal.WriteAt(60, $headerY, "Hours")
                $this.terminal.DrawHorizontalLine(2, $headerY + 1, $this.terminal.Width - 4)

                $y = $headerY + 2
                $totalMinutes = 0
                foreach ($group in $byProject) {
                    $minutes = ($group.Group | Measure-Object -Property minutes -Sum).Sum
                    $hours = [Math]::Round($minutes / 60, 1)
                    $totalMinutes += $minutes

                    $this.terminal.WriteAt(4, $y, $group.Name.Substring(0, [Math]::Min(24, $group.Name.Length)))
                    $this.terminal.WriteAt(30, $y, $group.Count.ToString())
                    $this.terminal.WriteAtColor(42, $y, $minutes.ToString(), [PmcVT100]::Cyan(), "")
                    $this.terminal.WriteAtColor(60, $y, $hours.ToString(), [PmcVT100]::Green(), "")
                    $y++

                    if ($y -ge $this.terminal.Height - 5) { break }
                }

                $totalHours = [Math]::Round($totalMinutes / 60, 1)
                $this.terminal.DrawHorizontalLine(2, $y, $this.terminal.Width - 4)
                $y++
                $this.terminal.WriteAtColor(4, $y, "TOTAL:", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAtColor(42, $y, $totalMinutes.ToString(), [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(60, $y, $totalHours.ToString(), [PmcVT100]::Green(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error generating report: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    [void] DrawProjectList() {
        $this.LoadProjects()
        $this.terminal.BeginFrame()
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Project List "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData

            if ($this.projects.Count -eq 0) {
                $emptyY = 8
                $this.terminal.WriteAtColor(4, $emptyY++, "No projects yet", [PmcVT100]::Yellow(), "")
                $emptyY++
                $this.terminal.WriteAtColor(4, $emptyY++, "Press 'A' to create your first project", [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(4, $emptyY++, "Or press Alt+P then L to view this screen", [PmcVT100]::Gray(), "")
            } else {
                $headerY = 5
                $this.terminal.WriteAtColor(2, $headerY, "Project", [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(30, $headerY, "Active", [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(42, $headerY, "Done", [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(52, $headerY, "Total", [PmcVT100]::Cyan(), "")
                $this.terminal.DrawHorizontalLine(0, $headerY + 1, $this.terminal.Width)

                $startY = $headerY + 2
                $y = $startY

                for ($i = 0; $i -lt $this.projects.Count; $i++) {
                    if ($y -ge $this.terminal.Height - 3) { break }

                    $proj = $this.projects[$i]
                    $projName = if ($proj -is [string]) { $proj } else { $proj.name }

                    $projTasks = @($data.tasks | Where-Object { $_.project -eq $projName })
                    $active = @($projTasks | Where-Object { $_.status -ne 'completed' }).Count
                    $completed = @($projTasks | Where-Object { $_.status -eq 'completed' }).Count
                    $total = $projTasks.Count

                    # Highlight selected project
                    $prefix = if ($i -eq $this.selectedProjectIndex) { "> " } else { "  " }
                    $bg = if ($i -eq $this.selectedProjectIndex) { [PmcVT100]::BgBlue() } else { "" }
                    $fg = if ($i -eq $this.selectedProjectIndex) { [PmcVT100]::White() } else { "" }

                    $projDisplay = $projName.Substring(0, [Math]::Min(24, $projName.Length))
                    if ($bg) {
                        $this.terminal.WriteAtColor(2, $y, ($prefix + $projDisplay).PadRight(28), $bg, $fg)
                        $this.terminal.WriteAtColor(30, $y, $active.ToString().PadRight(10), $bg, [PmcVT100]::Cyan())
                        $this.terminal.WriteAtColor(42, $y, $completed.ToString().PadRight(8), $bg, [PmcVT100]::Yellow())
                        $this.terminal.WriteAtColor(52, $y, $total.ToString(), $bg, $fg)
                    } else {
                        $this.terminal.WriteAtColor(2, $y, $prefix + $projDisplay, [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(30, $y, $active.ToString(), [PmcVT100]::Cyan(), "")
                        $this.terminal.WriteAtColor(42, $y, $completed.ToString(), [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAtColor(52, $y, $total.ToString(), [PmcVT100]::Yellow(), "")
                    }
                    $y++
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading projects: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("↑↓:Nav | Enter:Details | V:View Tasks | A:Add | E:Edit | Del:Delete | I:Info | F:Open Folder | C:Open CAA | T:Open T2020 | R:Open Request | Esc:Back")
        $this.terminal.EndFrame()
    }

    [void] HandleProjectListView() {
        $this.DrawProjectList()
        $key = [Console]::ReadKey($true)

        # Check for global menu keys first
        $globalAction = $this.CheckGlobalKeys($key)
        if ($globalAction) {
            Write-ConsoleUIDebug "Global action from project list: $globalAction" "PROJECTLIST"
            if ($globalAction -eq 'app:exit') {
                $this.running = $false
                return
            }
            $this.ProcessMenuAction($globalAction)
            return
        }

        switch ($key.Key) {
            'UpArrow' {
                if ($this.selectedProjectIndex -gt 0) {
                    $this.selectedProjectIndex--
                }
                $this.DrawProjectList()
            }
            'DownArrow' {
                if ($this.selectedProjectIndex -lt $this.projects.Count - 1) {
                    $this.selectedProjectIndex++
                }
                $this.DrawProjectList()
            }
            'Enter' {
                # Show project details screen
                if ($this.selectedProjectIndex -lt $this.projects.Count) {
                    $proj = $this.projects[$this.selectedProjectIndex]
                    $projName = if ($proj -is [string]) { $proj } else { $proj.name }
                    $this.selectedProjectName = $projName
                    $this.previousView = 'projectlist'
                    $this.currentView = 'projectdetail'
                }
            }
            'V' {
                # View tasks for selected project
                if ($this.selectedProjectIndex -lt $this.projects.Count) {
                    $proj = $this.projects[$this.selectedProjectIndex]
                    $projName = if ($proj -is [string]) { $proj } else { $proj.name }
                    $this.filterProject = $projName
                    $this.previousView = 'projectlist'
                    $this.currentView = 'tasklist'
                    $this.LoadTasks()
                }
            }
            'A' {
                $this.previousView = 'projectlist'
                $this.currentView = 'projectcreate'
            }
            'E' {
                # Edit project - use existing form
                if ($this.selectedProjectIndex -lt $this.projects.Count) {
                    $proj = $this.projects[$this.selectedProjectIndex]
                    $projName = if ($proj -is [string]) { $proj } else { $proj.name }
                    # Store selected project for edit form to use
                    $this.selectedProjectName = $projName
                    $this.previousView = 'projectlist'
                    $this.currentView = 'projectedit'
                }
            }
            # Removed dedicated Rename: use Edit (E) to change name
            'Delete' {
                # Delete project with confirmation
                if ($this.selectedProjectIndex -lt $this.projects.Count) {
                    $proj = $this.projects[$this.selectedProjectIndex]
                    $projName = if ($proj -is [string]) { $proj } else { $proj.name }

                    $confirmed = Show-ConfirmDialog -Message "Delete project '$projName'? This will NOT delete tasks in this project." -Title "Confirm Delete"
                    if ($confirmed) {
                        try {
                            $data = Get-PmcAllData
                            $data.projects = @($data.projects | Where-Object {
                                $pName = if ($_ -is [string]) { $_ } else { $_.name }
                                $pName -ne $projName
                            })
                            Save-PmcData -Data $data -Action "Deleted project '$projName'"
                            $this.LoadProjects()
                            if ($this.selectedProjectIndex -ge $this.projects.Count -and $this.selectedProjectIndex -gt 0) {
                                $this.selectedProjectIndex--
                            }
                            Show-InfoMessage -Message "Project '$projName' deleted successfully" -Title "Success" -Color "Green"
                        } catch {
                            Show-InfoMessage -Message "Failed to delete project: $_" -Title "Error" -Color "Red"
                        }
                    }
                    $this.DrawProjectList()
                }
            }
            'I' {
                # Show project detail (info)
                if ($this.selectedProjectIndex -lt $this.projects.Count) {
                    $proj = $this.projects[$this.selectedProjectIndex]
                    $projName = if ($proj -is [string]) { $proj } else { $proj.name }
                    $this.selectedProjectName = $projName
                    $this.previousView = 'projectlist'
                    $this.currentView = 'projectdetail'
                }
            }
            'F' {
                Open-ConsoleUIProjectPath -app $this -Field 'ProjFolder'
                $this.DrawProjectList()
            }
            'C' {
                Open-ConsoleUIProjectPath -app $this -Field 'CAAName'
                $this.DrawProjectList()
            }
            'T' {
                Open-ConsoleUIProjectPath -app $this -Field 'T2020'
                $this.DrawProjectList()
            }
            'R' {
                Open-ConsoleUIProjectPath -app $this -Field 'RequestName'
                $this.DrawProjectList()
            }
            'Escape' {
                $this.GoBackOr('main')
            }
        }
    }

    [void] DrawProjectCreateForm([int]$ActiveField = -1) {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Create New Project "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        $labels = @(
            'Project Name (required):',
            'Description:',
            'ID1:',
            'ID2:',
            'Project Folder:',
            'CAA Name:',
            'Request Name:',
            'T2020:',
            'Assigned Date (yyyy-MM-dd):',
            'Due Date (yyyy-MM-dd):',
            'BF Date (yyyy-MM-dd):'
        )
        for ($i=0; $i -lt $labels.Count; $i++) {
            $label = $labels[$i]
            if ($ActiveField -eq $i) {
                $this.terminal.WriteAtColor(2, $y, '> ', [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAtColor(4, $y, $label, [PmcVT100]::BgBlue(), [PmcVT100]::White())
            } else {
                $this.terminal.WriteAtColor(4, $y, $label, [PmcVT100]::Yellow(), "")
            }
            $y++
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter values; Enter = next, Esc = cancel")
    }

    [void] HandleProjectCreateForm() {
        # Draw header/labels
        $this.DrawProjectCreateForm(-1)

        $rowStart = 6
        $defaultRoot = $Script:DefaultPickerRoot
        $fields = @(
            @{ Name='Name';          Label='Project Name (required):';           X=28; Y=($rowStart + 0); Value='' }
            @{ Name='Description';   Label='Description:';                        X=16; Y=($rowStart + 1); Value='' }
            @{ Name='ID1';           Label='ID1:';                                X=9;  Y=($rowStart + 2); Value='' }
            @{ Name='ID2';           Label='ID2:';                                X=9;  Y=($rowStart + 3); Value='' }
            @{ Name='ProjFolder';    Label='Project Folder:';                     X=20; Y=($rowStart + 4); Value='' }
            @{ Name='CAAName';       Label='CAA Name:';                           X=14; Y=($rowStart + 5); Value='' }
            @{ Name='RequestName';   Label='Request Name:';                       X=17; Y=($rowStart + 6); Value='' }
            @{ Name='T2020';         Label='T2020:';                              X=11; Y=($rowStart + 7); Value='' }
            @{ Name='AssignedDate';  Label='Assigned Date (yyyy-MM-dd):';         X=32; Y=($rowStart + 8); Value='' }
            @{ Name='DueDate';       Label='Due Date (yyyy-MM-dd):';              X=27; Y=($rowStart + 9); Value='' }
            @{ Name='BFDate';        Label='BF Date (yyyy-MM-dd):';               X=26; Y=($rowStart + 10); Value='' }
        )

        # Draw initial values (blank for create)
        foreach ($f in $fields) { $this.terminal.WriteAt($f['X'], $f['Y'], [string]$f['Value']) }
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Tab/Shift+Tab navigate  |  F2: Pick path  |  Enter: Create  |  Esc: Cancel")

        # In-form editor with active label highlight
        $active = 0
        $prevActive = -1
        while ($true) {
            $f = $fields[$active]
            if ($prevActive -ne $active) {
                if ($prevActive -ge 0) { $pf = $fields[$prevActive]; $this.terminal.WriteAtColor(4, $pf['Y'], $pf['Label'], [PmcVT100]::Yellow(), "") }
                $this.terminal.WriteAtColor(4, $f['Y'], $f['Label'], [PmcVT100]::BgBlue(), [PmcVT100]::White())
                $prevActive = $active
            }
            $buf = [string]($f['Value'] ?? '')
            $col = [int]$f['X']; $row = [int]$f['Y']
            [Console]::SetCursorPosition($col + $buf.Length, $row)
            $k = [Console]::ReadKey($true)
            if ($k.Key -eq 'Enter') { break }
            elseif ($k.Key -eq 'Escape') { $this.GoBackOr('projectlist'); return }
            elseif ($k.Key -eq 'F2') {
                $fname = [string]$f['Name']
                if ($fname -in @('ProjFolder','CAAName','RequestName','T2020')) {
                    $dirsOnly = ($fname -eq 'ProjFolder')
                    $hint = "Pick $fname (Enter to pick)"
                    $picked = Select-ConsoleUIPathAt -app $this -Hint $hint -Col $col -Row $row -StartPath $defaultRoot -DirectoriesOnly:$dirsOnly
                    if ($null -ne $picked) {
                        $fields[$active]['Value'] = $picked
                        $this.terminal.FillArea($col, $row, $this.terminal.Width - $col - 2, 1, ' ')
                        $this.terminal.WriteAt($col, $row, [string]$picked)
                    }
                }
                continue
            }
            elseif ($k.Key -eq 'Tab') {
                $isShift = ("" + $k.Modifiers) -match 'Shift'
                if ($isShift) { $active = ($active - 1); if ($active -lt 0) { $active = $fields.Count - 1 } } else { $active = ($active + 1) % $fields.Count }
                continue
            } elseif ($k.Key -eq 'Backspace') {
                if ($buf.Length -gt 0) {
                    $buf = $buf.Substring(0, $buf.Length - 1)
                    $fields[$active]['Value'] = $buf
                    $this.terminal.FillArea($col, $row, $this.terminal.Width - $col - 2, 1, ' ')
                    $this.terminal.WriteAt($col, $row, $buf)
                }
                continue
            } else {
                $ch = $k.KeyChar
                if ($ch -and $ch -ne "`0") {
                    $buf += $ch
                    $fields[$active]['Value'] = $buf
                    $this.terminal.WriteAt($col + $buf.Length - 1, $row, $ch.ToString())
                }
            }
        }

        # Collect values
        $inputs = @{}
        foreach ($f in $fields) { $inputs[$f['Name']] = [string]$f['Value'] }

        # Validate
        if ([string]::IsNullOrWhiteSpace($inputs.Name)) { Show-InfoMessage -Message "Project name is required" -Title "Validation" -Color "Red"; $this.GoBackOr('projectlist'); return }

        foreach ($pair in @('AssignedDate','DueDate','BFDate')) {
            $raw = [string]$inputs[$pair]
            $norm = Normalize-ConsoleUIDate $raw
            if ($null -eq $norm -and -not [string]::IsNullOrWhiteSpace($raw)) {
                Show-InfoMessage -Message ("Invalid {0}. Use yyyymmdd, mmdd, +/-N, today/tomorrow/yesterday, or yyyy-MM-dd." -f $pair) -Title "Validation" -Color "Red"
                $this.GoBackOr('projectlist'); return
            }
            $inputs[$pair] = $norm
        }

        try {
            $data = Get-PmcAllData
            if (-not $data.projects) { $data.projects = @() }

            # Normalize any legacy entries
            try {
                $normalized = @()
                foreach ($p in @($data.projects)) { if ($p -is [string]) { $normalized += [pscustomobject]@{ id=[guid]::NewGuid().ToString(); name=$p; description=''; created=(Get-Date).ToString('yyyy-MM-dd HH:mm:ss'); status='active'; tags=@() } } else { $normalized += $p } }
                $data.projects = $normalized
            } catch {}

            # Duplicate name check
            $exists = @($data.projects | Where-Object { $_.PSObject.Properties['name'] -and $_.name -eq $inputs.Name })
            if ($exists.Count -gt 0) { Show-InfoMessage -Message ("Project '{0}' already exists" -f $inputs.Name) -Title "Error" -Color "Red"; $this.GoBackOr('projectlist'); return }

            # Create project
            $newProject = [pscustomobject]@{
                id = [guid]::NewGuid().ToString()
                name = $inputs.Name
                description = $inputs.Description
                ID1 = $inputs.ID1
                ID2 = $inputs.ID2
                ProjFolder = $inputs.ProjFolder
                AssignedDate = $inputs.AssignedDate
                DueDate = $inputs.DueDate
                BFDate = $inputs.BFDate
                CAAName = $inputs.CAAName
                RequestName = $inputs.RequestName
                T2020 = $inputs.T2020
                icon = ''
                color = 'Gray'
                sortOrder = 0
                aliases = @()
                isArchived = $false
                created = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
                status = 'active'
                tags = @()
            }

            $data.projects += $newProject
            Set-PmcAllData $data
            Show-InfoMessage -Message ("Project '{0}' created" -f $inputs.Name) -Title "Success" -Color "Green"
        } catch {
            Show-InfoMessage -Message ("Failed to create project: {0}" -f $_) -Title "Error" -Color "Red"
        }

        $this.GoBackOr('projectlist')
    }

    [void] HandleTaskEditForm() {
        # Determine task ID
        $taskId = 0
        if ($this.selectedTask) { $taskId = try { [int]$this.selectedTask.id } catch { 0 } }
        if ($taskId -le 0) {
            $fields = @(@{Name='taskId'; Label='Task ID'; Required=$true})
            $result = Show-InputForm -Title "Edit Task" -Fields $fields
            if ($null -eq $result) { $this.GoBackOr('tasklist'); return }
            $taskId = try { [int]$result['taskId'] } catch { 0 }
            if ($taskId -le 0) { Show-InfoMessage -Message "Invalid task ID" -Title "Error" -Color "Red"; $this.GoBackOr('tasklist'); return }
        }

        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq $taskId } | Select-Object -First 1
            if (-not $task) { Show-InfoMessage -Message "Task $taskId not found!" -Title "Error" -Color "Red"; $this.GoBackOr('tasklist'); return }

            # Get current task values for prepopulation
            $currentText = if ($task.text) { [string]$task.text } else { '' }
            $currentProject = if ($task.project) { [string]$task.project } else { '' }
            $currentPriority = if ($task.PSObject.Properties['priority'] -and $task.priority) { [string]$task.priority } else { 'medium' }
            $currentDue = ''
            if ($task.PSObject.Properties['due'] -and $task.due) { $currentDue = [string]$task.due }
            elseif ($task.PSObject.Properties['dueDate'] -and $task.dueDate) { $currentDue = [string]$task.dueDate }

            # Get available projects for dropdown
            $projectList = @('none', 'inbox') + @($data.projects | ForEach-Object { $_.name } | Where-Object { $_ -and $_ -ne 'inbox' } | Sort-Object)

            # Use the same input form as add task, but with prepopulated values
            $input = Show-InputForm -Title "Edit Task #$taskId" -Fields @(
                @{Name='text'; Label='Task description'; Required=$true; Type='text'; Value=$currentText}
                @{Name='project'; Label='Project'; Required=$false; Type='select'; Options=$projectList; Value=$currentProject}
                @{Name='priority'; Label='Priority'; Required=$false; Type='select'; Options=@('high', 'medium', 'low'); Value=$currentPriority}
                @{Name='due'; Label='Due date (YYYY-MM-DD or today/tomorrow)'; Required=$false; Type='text'; Value=$currentDue}
            )

            if ($null -eq $input) {
                $this.GoBackOr('tasklist')
                return
            }

            # Update task with new values
            $changed = $false

            if ($input['text'] -ne $currentText) {
                $task.text = $input['text'].Trim()
                $changed = $true
            }

            $newProject = if ([string]::IsNullOrWhiteSpace($input['project']) -or $input['project'] -eq 'none') { $null } else { $input['project'].Trim() }
            if ($newProject -ne $task.project) {
                $task.project = $newProject
                $changed = $true
            }

            if (-not [string]::IsNullOrWhiteSpace($input['priority'])) {
                $newPriority = $input['priority'].Trim().ToLower()
                if ($newPriority -ne $task.priority) {
                    $task.priority = $newPriority
                    $changed = $true
                }
            }

            # Handle due date
            if (-not [string]::IsNullOrWhiteSpace($input['due'])) {
                $parsedDate = ConvertTo-PmcDate -DateString $input['due']
                if ($null -eq $parsedDate) {
                    Show-InfoMessage -Message "Invalid due date. Try: today, yyyymmdd, mmdd, +3, etc." -Title "Invalid Date" -Color "Red"
                    $this.GoBackOr('tasklist')
                    return
                }
                if ($parsedDate -ne $currentDue) {
                    $task | Add-Member -MemberType NoteProperty -Name 'due' -Value $parsedDate -Force
                    $changed = $true
                }
            } elseif (-not [string]::IsNullOrWhiteSpace($currentDue)) {
                # Clear due date if user deleted it
                $task | Add-Member -MemberType NoteProperty -Name 'due' -Value $null -Force
                $changed = $true
            }

            if ($changed) {
                $task | Add-Member -MemberType NoteProperty -Name 'modified' -Value (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') -Force
                Save-PmcData -Data $data -Action "Edited task $taskId"
                $this.LoadTasks()
                Show-InfoMessage -Message "Task #$taskId updated successfully" -Title "Success" -Color "Green"
            } else {
                Show-InfoMessage -Message "No changes made to task #$taskId" -Title "Info" -Color "Cyan"
            }

            # Return to previous view and maintain selection
            if ($this.previousView -and $this.previousView -ne 'taskdetail') {
                $this.currentView = $this.previousView
                $this.previousView = ''
            } else {
                $this.currentView = 'tasklist'
            }

            # Try to keep selection on the edited task
            for ($i=0; $i -lt $this.tasks.Count; $i++) {
                if ($this.tasks[$i].id -eq $taskId) {
                    $this.selectedTaskIndex = $i
                    break
                }
            }

            $this.DrawLayout()
        } catch {
            Show-InfoMessage -Message "Failed to edit task: $_" -Title "Error" -Color "Red"
            $this.GoBackOr('tasklist')
        }
    }

    [void] DrawTaskCompleteForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Complete Task "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID:", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter task ID | Esc=Cancel")
    }

    [void] HandleTaskCompleteForm() {
        # Use Show-InputForm widget
        $fields = @(
            @{Name='taskId'; Label='Task ID to complete'; Required=$true}
        )

        $result = Show-InputForm -Title "Complete Task" -Fields $fields

        if ($null -eq $result) {
            $this.GoBackOr('tasklist')
            return
        }

        $taskId = try { [int]$result['taskId'] } catch { 0 }

        if ($taskId -le 0) {
            Show-InfoMessage -Message "Invalid task ID" -Title "Error" -Color "Red"
            $this.GoBackOr('tasklist')
            return
        }

        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq $taskId }

            if (-not $task) {
                Show-InfoMessage -Message "Task $taskId not found!" -Title "Error" -Color "Red"
            } else {
                $task.status = 'completed'
                $task.completed = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
                Save-PmcData -Data $data -Action "Completed task $taskId"
                Show-InfoMessage -Message "Task $taskId completed successfully!" -Title "Success" -Color "Green"
            }
        } catch {
            Show-InfoMessage -Message "Failed to complete task: $_" -Title "SAVE ERROR" -Color "Red"
        }

        $this.GoBackOr('tasklist')
    }

    [void] DrawTaskDeleteForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Delete Task "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "WARNING: This will permanently delete the task!", [PmcVT100]::Red(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter task ID | Esc=Cancel")
    }

    [void] HandleTaskDeleteForm() {
        # Use Show-InputForm widget
        $fields = @(
            @{Name='taskId'; Label='Task ID to delete'; Required=$true}
        )

        $result = Show-InputForm -Title "Delete Task" -Fields $fields

        if ($null -eq $result) {
            $this.GoBackOr('tasklist')
            return
        }

        $taskId = try { [int]$result['taskId'] } catch { 0 }

        if ($taskId -le 0) {
            Show-InfoMessage -Message "Invalid task ID" -Title "Error" -Color "Red"
            $this.GoBackOr('tasklist')
            return
        }

        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq $taskId }

            if (-not $task) {
                Show-InfoMessage -Message "Task $taskId not found!" -Title "Error" -Color "Red"
            } else {
                # Use Show-ConfirmDialog for deletion confirmation
                $confirmed = Show-ConfirmDialog -Message "Delete task '$($task.text)'? This cannot be undone." -Title "Confirm Deletion"

                if ($confirmed) {
                    $data.tasks = @($data.tasks | Where-Object { $_.id -ne $taskId })
                    Save-PmcData -Data $data -Action "Deleted task $taskId"
                    Show-InfoMessage -Message "Task $taskId deleted successfully!" -Title "Success" -Color "Green"
                } else {
                    Show-InfoMessage -Message "Deletion cancelled" -Title "Cancelled" -Color "Yellow"
                }
            }
        } catch {
            Show-InfoMessage -Message "Failed to delete task: $_" -Title "SAVE ERROR" -Color "Red"
        }

        $this.GoBackOr('tasklist')
    }

    [void] DrawDepAddForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Add Dependency "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "Depends on Task ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(4, 10, "(Task will be blocked until dependency is completed)")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter IDs | Esc=Cancel")
    }

    [void] HandleDepAddForm() {
        # Use Show-InputForm widget
        $fields = @(
            @{Name='taskId'; Label='Task ID'; Required=$true}
            @{Name='dependsId'; Label='Depends on Task ID'; Required=$true}
        )

        $result = Show-InputForm -Title "Add Dependency" -Fields $fields

        if ($null -eq $result) {
            $this.GoBackOr('projectlist')
            return
        }

        $taskId = try { [int]$result['taskId'] } catch { 0 }
        $dependsId = try { [int]$result['dependsId'] } catch { 0 }

        if ($taskId -le 0 -or $dependsId -le 0) {
            Show-InfoMessage -Message "Invalid task IDs" -Title "Error" -Color "Red"
            $this.GoBackOr('tasklist')
            return
        }

        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq $taskId }
            $dependsTask = $data.tasks | Where-Object { $_.id -eq $dependsId }

            if (-not $task) {
                Show-InfoMessage -Message "Task $taskId not found!" -Title "Error" -Color "Red"
            } elseif (-not $dependsTask) {
                Show-InfoMessage -Message "Task $dependsId not found!" -Title "Error" -Color "Red"
            } else {
                # Initialize depends array if needed
                if (-not $task.PSObject.Properties['depends']) {
                    $task | Add-Member -NotePropertyName depends -NotePropertyValue @()
                }

                # Check if dependency already exists
                if ($task.depends -contains $dependsId) {
                    Show-InfoMessage -Message "Dependency already exists!" -Title "Warning" -Color "Yellow"
                } else {
                    $task.depends = @($task.depends + $dependsId)

                    # Update blocked status
                    Update-PmcBlockedStatus -data $data

                    Save-PmcData -Data $data -Action "Added dependency: $taskId depends on $dependsId"
                    Show-InfoMessage -Message "Dependency added successfully! Task $taskId now depends on task $dependsId." -Title "Success" -Color "Green"
                }
            }
        } catch {
            Show-InfoMessage -Message "Failed to add dependency: $_" -Title "SAVE ERROR" -Color "Red"
        }

        $this.GoBackOr('tasklist')
    }

    [void] DrawDepRemoveForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Remove Dependency "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "Remove dependency on Task ID:", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter IDs | Esc=Cancel")
    }

    [void] HandleDepRemoveForm() {
        # Use Show-InputForm widget
        $fields = @(
            @{Name='taskId'; Label='Task ID'; Required=$true}
            @{Name='dependsId'; Label='Remove dependency on Task ID'; Required=$true}
        )

        $result = Show-InputForm -Title "Remove Dependency" -Fields $fields

        if ($null -eq $result) {
            $this.GoBackOr('timelist')
            return
        }

        $taskId = try { [int]$result['taskId'] } catch { 0 }
        $dependsId = try { [int]$result['dependsId'] } catch { 0 }

        if ($taskId -le 0 -or $dependsId -le 0) {
            Show-InfoMessage -Message "Invalid task IDs" -Title "Error" -Color "Red"
            $this.GoBackOr('tasklist')
            return
        }

        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq $taskId }

            if (-not $task) {
                Show-InfoMessage -Message "Task $taskId not found!" -Title "Error" -Color "Red"
            } elseif (-not $task.PSObject.Properties['depends'] -or -not $task.depends) {
                Show-InfoMessage -Message "Task has no dependencies!" -Title "Warning" -Color "Yellow"
            } else {
                $task.depends = @($task.depends | Where-Object { $_ -ne $dependsId })

                # Clean up empty depends array
                if ($task.depends.Count -eq 0) {
                    $task.PSObject.Properties.Remove('depends')
                }

                # Update blocked status
                Update-PmcBlockedStatus -data $data

                Save-PmcData -Data $data -Action "Removed dependency: $taskId no longer depends on $dependsId"
                Show-InfoMessage -Message "Dependency removed successfully!" -Title "Success" -Color "Green"
            }
        } catch {
            Show-InfoMessage -Message "Failed to remove dependency: $_" -Title "SAVE ERROR" -Color "Red"
        }

        $this.GoBackOr('tasklist')
    }

    [void] DrawDepShowForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Show Task Dependencies "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID:", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter task ID | Esc=Cancel")
    }

    [void] HandleDepShowForm() {
        # Unified input for task id
        $fields = @(
            @{Name='taskId'; Label='Task ID to show dependencies'; Required=$true}
        )
        $form = Show-InputForm -Title "Show Task Dependencies" -Fields $fields
        if ($null -eq $form) { $this.GoBackOr('tasklist'); return }
        $taskId = try { [int]$form['taskId'] } catch { 0 }
        if ($taskId -le 0) { Show-InfoMessage -Message "Invalid task ID" -Title "Validation" -Color "Red"; $this.GoBackOr('tasklist'); return }

        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq $taskId }

            if (-not $task) {
                Show-InfoMessage -Message ("Task {0} not found" -f $taskId) -Title "Error" -Color "Red"
                $this.GoBackOr('tasklist'); return
            }

            $this.terminal.Clear()
            $this.menuSystem.DrawMenuBar()
            $title = " Show Task Dependencies "
            $titleX = ($this.terminal.Width - $title.Length) / 2
            $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
            $this.terminal.WriteAtColor(4, 9, "Task:", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAt(10, 9, $task.text.Substring(0, [Math]::Min(60, $task.text.Length)))

            $depends = if ($task.PSObject.Properties['depends'] -and $task.depends) { $task.depends } else { @() }
            if ($depends.Count -eq 0) {
                $this.terminal.WriteAt(4, 11, "No dependencies")
            } else {
                $this.terminal.WriteAtColor(4, 11, "Dependencies:", [PmcVT100]::Yellow(), "")
                $y = 13
                foreach ($depId in $depends) {
                    $depTask = $data.tasks | Where-Object { $_.id -eq $depId }
                    if ($depTask) {
                        $statusIcon = if ($depTask.status -eq 'completed') { 'X' } else { 'o' }
                        $statusColor = if ($depTask.status -eq 'completed') { [PmcVT100]::Green() } else { [PmcVT100]::Red() }
                        $this.terminal.WriteAtColor(6, $y, $statusIcon, $statusColor, "")
                        $this.terminal.WriteAt(8, $y, "#$depId")
                        $this.terminal.WriteAt(15, $y, $depTask.text.Substring(0, [Math]::Min(50, $depTask.text.Length)))
                        $y++
                    }
                    if ($y -ge $this.terminal.Height - 5) { break }
                }
                if ($task.PSObject.Properties['blocked'] -and $task.blocked) {
                    $this.terminal.WriteAtColor(4, $y + 1, "WARNING: Task is BLOCKED", [PmcVT100]::Red(), "")
                } else {
                    $this.terminal.WriteAtColor(4, $y + 1, "Task is ready", [PmcVT100]::Green(), "")
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, 9, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
        [Console]::ReadKey($true) | Out-Null
        $this.GoBackOr('tasklist')
    }

    [void] DrawDepGraph() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Dependency Graph "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $tasksWithDeps = @($data.tasks | Where-Object {
                $_.PSObject.Properties['depends'] -and $_.depends -and $_.depends.Count -gt 0
            })

            if ($tasksWithDeps.Count -eq 0) {
                $this.terminal.WriteAt(4, 6, "No task dependencies found")
            } else {
                $headerY = 5
                $this.terminal.WriteAt(2, $headerY, "Task")
                $this.terminal.WriteAt(10, $headerY, "Depends On")
                $this.terminal.WriteAt(26, $headerY, "Status")
                $this.terminal.WriteAt(40, $headerY, "Description")
                $this.terminal.DrawHorizontalLine(0, $headerY + 1, $this.terminal.Width)

                $y = $headerY + 2
                foreach ($task in $tasksWithDeps) {
                    if ($y -ge $this.terminal.Height - 5) { break }

                    $dependsText = ($task.depends -join ', ')
                    $statusText = if ($task.PSObject.Properties['blocked'] -and $task.blocked) { "BLOCKED" } else { "Ready" }
                    $statusColor = if ($task.PSObject.Properties['blocked'] -and $task.blocked) { [PmcVT100]::Red() } else { [PmcVT100]::Green() }

                    $this.terminal.WriteAt(2, $y, "#$($task.id)")
                    $this.terminal.WriteAt(10, $y, $dependsText.Substring(0, [Math]::Min(14, $dependsText.Length)))
                    $this.terminal.WriteAtColor(26, $y, $statusText, $statusColor, "")
                    $this.terminal.WriteAt(40, $y, $task.text.Substring(0, [Math]::Min(38, $task.text.Length)))
                    $y++
                }

                # Summary
                $blockedCount = @($data.tasks | Where-Object { $_.PSObject.Properties['blocked'] -and $_.blocked }).Count
                $y += 2
                $this.terminal.WriteAtColor(4, $y, "Tasks with dependencies:", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAt(30, $y, $tasksWithDeps.Count.ToString())
                $y++
                $this.terminal.WriteAtColor(4, $y, "Currently blocked:", [PmcVT100]::Yellow(), "")
                $blockedColor = if ($blockedCount -gt 0) { [PmcVT100]::Red() } else { [PmcVT100]::Green() }
                $this.terminal.WriteAtColor(30, $y, $blockedCount.ToString(), $blockedColor, "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error loading dependency graph: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    # File Management Methods
    [void] DrawFileRestoreForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Restore Data from Backup "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $file = Get-PmcTaskFilePath
            $allBackups = @()

            # Collect .bak1 through .bak9 files
            for ($i = 1; $i -le 9; $i++) {
                $bakFile = "$file.bak$i"
                if (Test-Path $bakFile) {
                    $info = Get-Item $bakFile
                    $allBackups += [PSCustomObject]@{
                        Number = $allBackups.Count + 1
                        Name = ".bak$i"
                        Path = $bakFile
                        Size = $info.Length
                        Modified = $info.LastWriteTime
                        Type = "auto"
                    }
                }
            }

            # Collect manual backups from backups directory
            $backupDir = Join-Path (Get-PmcRootPath) "backups"
            if (Test-Path $backupDir) {
                $manualBackups = @(Get-ChildItem $backupDir -Filter "*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 10)
                foreach ($backup in $manualBackups) {
                    $allBackups += [PSCustomObject]@{
                        Number = $allBackups.Count + 1
                        Name = $backup.Name
                        Path = $backup.FullName
                        Size = $backup.Length
                        Modified = $backup.LastWriteTime
                        Type = "manual"
                    }
                }
            }

            if ($allBackups.Count -gt 0) {
                $this.terminal.WriteAtColor(4, 6, "Available backups:", [PmcVT100]::Yellow(), "")
                $y = 8
                foreach ($backup in $allBackups) {
                    $sizeKB = [math]::Round($backup.Size / 1KB, 2)
                    $typeLabel = if ($backup.Type -eq "auto") { "[Auto]" } else { "[Manual]" }
                    $line = "$($backup.Number). $typeLabel $($backup.Name) - $($backup.Modified.ToString('yyyy-MM-dd HH:mm:ss')) ($sizeKB KB)"
                    $this.terminal.WriteAt(4, $y++, $line)
                }
                $this.terminal.WriteAtColor(4, $y + 1, "Enter backup number to restore:", [PmcVT100]::Yellow(), "")
            } else {
                $this.terminal.WriteAtColor(4, 6, "No backups found", [PmcVT100]::Red(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error listing backups: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter number or Esc=Cancel")
    }

    [void] HandleFileRestoreForm() {
        try {
            $file = Get-PmcTaskFilePath
            $allBackups = @()

            # Collect .bak1 through .bak9 files
            for ($i = 1; $i -le 9; $i++) {
                $bakFile = "$file.bak$i"
                if (Test-Path $bakFile) {
                    $info = Get-Item $bakFile
                    $allBackups += [PSCustomObject]@{
                        Number = $allBackups.Count + 1
                        Name = ".bak$i"
                        Path = $bakFile
                        Size = $info.Length
                        Modified = $info.LastWriteTime
                        Type = "auto"
                    }
                }
            }

            # Collect manual backups from backups directory
            $backupDir = Join-Path (Get-PmcRootPath) "backups"
            if (Test-Path $backupDir) {
                $manualBackups = @(Get-ChildItem $backupDir -Filter "*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 10)
                foreach ($backup in $manualBackups) {
                    $allBackups += [PSCustomObject]@{
                        Number = $allBackups.Count + 1
                        Name = $backup.Name
                        Path = $backup.FullName
                        Size = $backup.Length
                        Modified = $backup.LastWriteTime
                        Type = "manual"
                    }
                }
            }

            if ($allBackups.Count -eq 0) { Show-InfoMessage -Message "No backups found" -Title "Restore" -Color "Yellow"; $this.GoBackOr('tasklist'); return }

            # Build options for select list
            $options = @()
            foreach ($b in $allBackups) {
                $sizeKB = [math]::Round($b.Size / 1KB, 2)
                $typeLabel = if ($b.Type -eq 'auto') { '[Auto]' } else { '[Manual]' }
                $options += ("{0} {1}  {2}  ({3} KB)" -f $typeLabel, $b.Name, $b.Modified.ToString('yyyy-MM-dd HH:mm'), $sizeKB)
            }

            $selected = Show-SelectList -Title "Select Backup to Restore" -Options $options
            if (-not $selected) { $this.GoBackOr('tasklist'); return }
            $idx = [Array]::IndexOf(@($options), $selected)
            if ($idx -lt 0) { $this.GoBackOr('tasklist'); return }
            $backup = $allBackups[$idx]

            $confirmed = Show-ConfirmDialog -Message ("Restore from {0}? This overwrites current data." -f $backup.Name) -Title "Confirm Restore"
            if ($confirmed) {
                try {
                    $data = Get-Content $backup.Path -Raw | ConvertFrom-Json
                    Save-PmcData -Data $data -Action ("Restored from backup: {0}" -f $backup.Name)
                    $this.LoadTasks()
                    Show-InfoMessage -Message "Data restored successfully" -Title "Success" -Color "Green"
                } catch {
                    Show-InfoMessage -Message ("Error restoring backup: {0}" -f $_) -Title "Error" -Color "Red"
                }
            } else {
                Show-InfoMessage -Message "Restore cancelled" -Title "Cancelled" -Color "Yellow"
            }
        } catch {
            Show-InfoMessage -Message ("Error restoring backup: {0}" -f $_) -Title "Error" -Color "Red"
        }

        $this.GoBackOr('tasklist')
    }

    

    [void] DrawProjectArchiveForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Archive Project "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $this.terminal.WriteAtColor(4, 6, "Project Name:", [PmcVT100]::Yellow(), "")
        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter project name | Esc=Cancel")
    }

    [void] HandleProjectArchiveForm() {
        # Use Show-InputForm widget
        $fields = @(
            @{Name='projectName'; Label='Project Name to archive'; Required=$true}
        )

        $result = Show-InputForm -Title "Archive Project" -Fields $fields

        if ($null -eq $result) { $this.GoBackOr('projectlist'); return }

        $projectName = $result['projectName']

        try {
            $data = Get-PmcAllData
            $exists = @($data.projects | Where-Object { ($_ -is [string] -and $_ -eq $projectName) -or ($_.PSObject.Properties['name'] -and $_.name -eq $projectName) }).Count -gt 0
            if (-not $exists) {
                Show-InfoMessage -Message "Project '$projectName' not found!" -Title "Error" -Color "Red"
            } else {
                if (-not $data.PSObject.Properties['archivedProjects']) { $data | Add-Member -NotePropertyName 'archivedProjects' -NotePropertyValue @() }
                $data.archivedProjects += $projectName
                $data.projects = @(
                    $data.projects | Where-Object {
                        $pName = if ($_ -is [string]) { $_ } else { $_.name }
                        $pName -ne $projectName
                    }
                )
                Save-PmcData -Data $data -Action "Archived project: $projectName"
                Show-InfoMessage -Message "Project '$projectName' archived successfully!" -Title "Success" -Color "Green"
            }
        } catch {
            Show-InfoMessage -Message "Failed to archive project: $_" -Title "SAVE ERROR" -Color "Red"
        }
        $this.GoBackOr('projectlist')
    }

    [void] DrawProjectDeleteForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Delete Project "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $this.terminal.WriteAtColor(4, 6, "Project Name:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "WARNING: This will NOT delete tasks!", [PmcVT100]::Red(), "")
        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter project name | Esc=Cancel")
    }

    [void] HandleProjectDeleteForm() {
        # Use Show-InputForm widget
        $fields = @(
            @{Name='projectName'; Label='Project Name to delete'; Required=$true}
        )

        $result = Show-InputForm -Title "Delete Project" -Fields $fields

        if ($null -eq $result) { $this.GoBackOr('projectlist'); return }

        $projectName = $result['projectName']

        try {
            $data = Get-PmcAllData
            $exists = @($data.projects | Where-Object { ($_ -is [string] -and $_ -eq $projectName) -or ($_.PSObject.Properties['name'] -and $_.name -eq $projectName) }).Count -gt 0
            if (-not $exists) {
                Show-InfoMessage -Message "Project '$projectName' not found!" -Title "Error" -Color "Red"
            } else {
                $confirmed = Show-ConfirmDialog -Message "Delete project '$projectName'? (Tasks will remain in inbox)" -Title "Confirm Deletion"
                if ($confirmed) {
                    $data.projects = @(
                        $data.projects | Where-Object {
                            $pName = if ($_ -is [string]) { $_ } else { $_.name }
                            $pName -ne $projectName
                        }
                    )
                    Save-PmcData -Data $data -Action "Deleted project: $projectName"
                    Show-InfoMessage -Message "Project '$projectName' deleted successfully!" -Title "Success" -Color "Green"
                } else {
                    Show-InfoMessage -Message "Deletion cancelled" -Title "Cancelled" -Color "Yellow"
                }
            }
        } catch {
            Show-InfoMessage -Message "Failed to delete project: $_" -Title "SAVE ERROR" -Color "Red"
        }
        $this.GoBackOr('projectlist')
    }

    [void] DrawProjectStatsForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Project Statistics "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $this.terminal.WriteAtColor(4, 6, "Project Name:", [PmcVT100]::Yellow(), "")
        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter project name | Esc=Cancel")
    }

    [void] HandleProjectStatsView() {
        try {
            $data = Get-PmcAllData
            $projectList = @($data.projects | ForEach-Object { if ($_ -is [string]) { $_ } else { $_.name } } | Where-Object { $_ })
            $selected = Show-SelectList -Title "Project for Stats" -Options $projectList
            if (-not $selected) { $this.GoBackOr('projectlist'); return }
            $projectName = $selected
            $this.terminal.Clear()
            $this.menuSystem.DrawMenuBar()
            $title = " Project Statistics "
            $titleX = ($this.terminal.Width - $title.Length) / 2
            $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
            $this.terminal.WriteAtColor(4, 6, "Project Name:", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAt(19, 6, $projectName)
        } catch {
            $this.GoBackOr('projectlist')
            return
        }
        try {
            $data = Get-PmcAllData
            $exists = @($data.projects | Where-Object { ($_ -is [string] -and $_ -eq $projectName) -or ($_.PSObject.Properties['name'] -and $_.name -eq $projectName) }).Count -gt 0
            if (-not $exists) {
                $this.terminal.WriteAtColor(4, 9, "Project '$projectName' not found!", [PmcVT100]::Red(), "")
            } else {
                $projTasks = @($data.tasks | Where-Object { $_.project -eq $projectName })
                $completed = @($projTasks | Where-Object { $_.status -eq 'completed' }).Count
                $active = @($projTasks | Where-Object { $_.status -ne 'completed' }).Count
                $overdue = @($projTasks | Where-Object {
                    if ($_.status -eq 'completed' -or -not $_.due) { return $false }
                    $d = Get-ConsoleUIDateOrNull $_.due
                    if ($d) { return ($d.Date -lt (Get-Date).Date) } else { return $false }
                }).Count

                $projTimelogs = if ($data.PSObject.Properties['timelogs']) { @($data.timelogs | Where-Object { $_.project -eq $projectName }) } else { @() }
                $totalMinutes = if ($projTimelogs.Count -gt 0) { ($projTimelogs | Measure-Object -Property minutes -Sum).Sum } else { 0 }
                $totalHours = [Math]::Round($totalMinutes / 60, 1)

                $this.terminal.WriteAtColor(4, 9, "Project:", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAt(13, 9, $projectName)

                $this.terminal.WriteAtColor(4, 11, "Total Tasks:", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAt(18, 11, $projTasks.Count.ToString())

                $this.terminal.WriteAtColor(4, 12, "Active:", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAtColor(18, 12, $active.ToString(), [PmcVT100]::Cyan(), "")

                $this.terminal.WriteAtColor(4, 13, "Completed:", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAtColor(18, 13, $completed.ToString(), [PmcVT100]::Green(), "")

                if ($overdue -gt 0) {
                    $this.terminal.WriteAtColor(4, 14, "Overdue:", [PmcVT100]::Yellow(), "")
                    $this.terminal.WriteAtColor(18, 14, $overdue.ToString(), [PmcVT100]::Red(), "")
                }

                $this.terminal.WriteAtColor(4, 16, "Time Logged:", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAt(18, 16, "$totalHours hours ($totalMinutes minutes)")

                $this.terminal.WriteAtColor(4, 17, "Time Entries:", [PmcVT100]::Yellow(), "")
                $this.terminal.WriteAt(18, 17, $projTimelogs.Count.ToString())
            }
        } catch {
            $this.terminal.WriteAtColor(4, 9, "Error: $_", [PmcVT100]::Red(), "")
        }
        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
        [Console]::ReadKey($true) | Out-Null
        $this.GoBackOr('projectlist')
    }

    # Time Management Methods
    [void] DrawTimeEditForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Edit Time Entry "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $this.terminal.WriteAtColor(4, 6, "Time Entry ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "New Minutes:", [PmcVT100]::Yellow(), "")
        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter ID and minutes | Esc=Cancel")
    }

    [void] HandleTimeEditForm() {
        # Determine target entry
        $id = 0
        if ($this.timelogs -and $this.selectedTimeIndex -ge 0 -and $this.selectedTimeIndex -lt $this.timelogs.Count) {
            $id = try { [int]$this.timelogs[$this.selectedTimeIndex].id } catch { 0 }
        }
        if ($id -le 0) {
            $fields = @(@{Name='id'; Label='Time Entry ID'; Required=$true})
            $result = Show-InputForm -Title "Edit Time Entry" -Fields $fields
            if ($null -eq $result) { $this.GoBackOr('timelist'); return }
            $id = try { [int]$result['id'] } catch { 0 }
            if ($id -le 0) { Show-InfoMessage -Message "Invalid time entry ID" -Title "Error" -Color "Red"; $this.GoBackOr('timelist'); return }
        }

        try {
            $data = Get-PmcAllData
            if (-not $data.PSObject.Properties['timelogs']) { Show-InfoMessage -Message "No time logs found!" -Title "Error" -Color "Red"; $this.GoBackOr('timelist'); return }
            $entry = $data.timelogs | Where-Object { $_.id -eq $id } | Select-Object -First 1
            if (-not $entry) { Show-InfoMessage -Message "Time entry $id not found!" -Title "Error" -Color "Red"; $this.GoBackOr('timelist'); return }

            # Draw form
            $this.terminal.Clear(); $this.menuSystem.DrawMenuBar()
            $title = " Edit Time Entry #$id "; $titleX = ($this.terminal.Width - $title.Length) / 2
            $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
            $yStart = 6
            $minVal = try { [string]$entry.minutes } catch { '' }
            $descVal = if ($entry.PSObject.Properties['description']) { [string]$entry.description } else { '' }
            $fields = @(
                @{ Name='minutes'; Label='Minutes:'; Value=$minVal; X=14; Y=$yStart }
                @{ Name='description'; Label='Description:'; Value=$descVal; X=16; Y=($yStart+3) }
            )
            foreach ($f in $fields) { $this.terminal.WriteAtColor(4, $f['Y'], $f['Label'], [PmcVT100]::Yellow(), ""); $this.terminal.WriteAt($f['X'], $f['Y'], [string]$f['Value']) }
            $this.terminal.DrawFooter("Tab/Shift+Tab navigate | Enter saves | Esc cancels")

            $active = 0
            $prevActive = -1
            while ($true) {
                $f = $fields[$active]
                if ($prevActive -ne $active) {
                    if ($prevActive -ge 0) { $pf = $fields[$prevActive]; $this.terminal.WriteAtColor(4, $pf['Y'], $pf['Label'], [PmcVT100]::Yellow(), "") }
                    $this.terminal.WriteAtColor(4, $f['Y'], $f['Label'], [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    $prevActive = $active
                }
                $buf = [string]($f['Value'] ?? '')
                $col = [int]$f['X']; $row = [int]$f['Y']
                [Console]::SetCursorPosition($col + $buf.Length, $row)
                $k = [Console]::ReadKey($true)
                if ($k.Key -eq 'Enter') { break }
                elseif ($k.Key -eq 'Escape') { $this.GoBackOr('timelist'); return }
                elseif ($k.Key -eq 'Tab') {
                    $isShift = ("" + $k.Modifiers) -match 'Shift'
                    if ($isShift) { $active = ($active - 1); if ($active -lt 0) { $active = $fields.Count - 1 } } else { $active = ($active + 1) % $fields.Count }
                } elseif ($k.Key -eq 'Backspace') {
                    if ($buf.Length -gt 0) { $buf = $buf.Substring(0, $buf.Length - 1); $fields[$active]['Value'] = $buf; $this.terminal.FillArea($col, $row, $this.terminal.Width - $col - 1, 1, ' '); $this.terminal.WriteAt($col, $row, $buf) }
                } else {
                    $ch = $k.KeyChar; if ($ch -and $ch -ne "`0") { $buf += $ch; $fields[$active]['Value'] = $buf; $this.terminal.WriteAt($col + $buf.Length - 1, $row, $ch.ToString()) }
                }
            }

            $newMinutes = [string]$fields | Where-Object { $_.Name -eq 'minutes' } | ForEach-Object { $_.Value }
            $newDesc = [string]$fields | Where-Object { $_.Name -eq 'description' } | ForEach-Object { $_.Value }
            $mInt = 0; try { $mInt = [int]$newMinutes } catch { $mInt = 0 }
            if ($mInt -le 0) { Show-InfoMessage -Message "Minutes must be a positive number" -Title "Error" -Color "Red"; $this.GoBackOr('timelist'); return }
            $entry.minutes = $mInt
            if ($entry.PSObject.Properties['description']) { $entry.description = $newDesc }
            Save-PmcData -Data $data -Action "Updated time entry $id"
            $this.GoBackOr('timelist')
        } catch {
            Show-InfoMessage -Message "Failed to update time entry: $_" -Title "SAVE ERROR" -Color "Red"; $this.GoBackOr('timelist')
        }
    }

    [void] DrawTimeDeleteForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Delete Time Entry "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $this.terminal.WriteAtColor(4, 6, "Time Entry ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter ID | Esc=Cancel")
    }

    [void] HandleTimeDeleteForm() {
        # Use Show-InputForm widget
        $fields = @(
            @{Name='id'; Label='Time Entry ID to delete'; Required=$true}
        )

        $result = Show-InputForm -Title "Delete Time Entry" -Fields $fields

        if ($null -eq $result) {
            $this.GoBackOr('timelist')
            return
        }

        $id = try { [int]$result['id'] } catch { 0 }

        if ($id -le 0) {
            Show-InfoMessage -Message "Invalid time entry ID" -Title "Error" -Color "Red"
            $this.GoBackOr('timelist')
            return
        }

        try {
            $data = Get-PmcAllData
            if (-not $data.PSObject.Properties['timelogs']) {
                Show-InfoMessage -Message "No time logs found!" -Title "Error" -Color "Red"
            } else {
                $entry = $data.timelogs | Where-Object { $_.id -eq $id }
                if (-not $entry) {
                    Show-InfoMessage -Message "Time entry $id not found!" -Title "Error" -Color "Red"
                } else {
                    # Use Show-ConfirmDialog for confirmation
                    $confirmed = Show-ConfirmDialog -Message "Delete time entry #$($id)? This cannot be undone." -Title "Confirm Deletion"

                    if ($confirmed) {
                        $data.timelogs = @($data.timelogs | Where-Object { $_.id -ne $id })
                        Save-PmcData -Data $data -Action "Deleted time entry $id"
                        Show-InfoMessage -Message "Time entry deleted successfully!" -Title "Success" -Color "Green"
                    } else {
                        Show-InfoMessage -Message "Deletion cancelled" -Title "Cancelled" -Color "Yellow"
                    }
                }
            }
        } catch {
            Show-InfoMessage -Message "Failed to delete time entry: $_" -Title "SAVE ERROR" -Color "Red"
        }
        $this.GoBackOr('timelist')
    }

    # Task Import/Export Methods
    [void] DrawTaskImportForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Import Tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $this.terminal.WriteAtColor(4, 6, "Import File Path:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(4, 8, "(Must be JSON format compatible with PMC)")
        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter file path | Esc=Cancel")
    }

    [void] HandleTaskImportForm() {
        # Unified input
        $fields = @(
            @{Name='path'; Label='Import file path (JSON)'; Required=$true}
        )
        $result = Show-InputForm -Title "Import Tasks" -Fields $fields
        if ($null -eq $result) { $this.GoBackOr('main'); return }
        $filePath = [string]$result['path']
        if ([string]::IsNullOrWhiteSpace($filePath)) { $this.GoBackOr('main'); return }
        try {
            if (-not (Test-Path $filePath)) {
                $this.terminal.WriteAtColor(4, 11, "File not found: $filePath", [PmcVT100]::Red(), "")
            } else {
                $importData = Get-Content $filePath -Raw | ConvertFrom-Json
                $data = Get-PmcAllData
                $newTasks = 0
                foreach ($task in $importData.tasks) {
                    if (-not ($data.tasks | Where-Object { $_.id -eq $task.id })) {
                        $data.tasks += $task
                        $newTasks++
                    }
                }
                Save-PmcData -Data $data -Action "Imported $newTasks tasks from $filePath"
                $this.terminal.WriteAtColor(4, 11, "Imported $newTasks tasks!", [PmcVT100]::Green(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 11, "Error: $_", [PmcVT100]::Red(), "")
        }
        $this.GoBackOr('main')
    }

    [void] DrawTaskExportForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Export Tasks "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $this.terminal.WriteAtColor(4, 6, "Export File Path:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(4, 8, "(Will export all tasks as JSON)")
        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter file path | Esc=Cancel")
    }

    [void] HandleTaskExportForm() {
        # Unified input
        $fields = @(
            @{Name='path'; Label='Export file path (JSON)'; Required=$true}
        )
        $result = Show-InputForm -Title "Export Tasks" -Fields $fields
        if ($null -eq $result) { $this.GoBackOr('main'); return }
        $filePath = [string]$result['path']
        if ([string]::IsNullOrWhiteSpace($filePath)) { $this.GoBackOr('main'); return }
        try {
            $data = Get-PmcAllData
            $exportData = @{ tasks = $data.tasks; projects = $data.projects }
            $exportData | ConvertTo-Json -Depth 10 | Set-Content -Path $filePath -Encoding UTF8
            $this.terminal.WriteAtColor(4, 11, "Exported $($data.tasks.Count) tasks to $filePath", [PmcVT100]::Green(), "")
        } catch {
            $this.terminal.WriteAtColor(4, 11, "Error: $_", [PmcVT100]::Red(), "")
        }
        $this.GoBackOr('main')
    }

    [void] DrawThemeEditor() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Theme Editor "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        $this.terminal.WriteAtColor(4, $y++, "Current color scheme:", [PmcVT100]::Cyan(), "")
        $y++

        # Show current colors in use
        $this.terminal.WriteAt(4, $y++, "Primary colors:")
        $this.terminal.WriteAtColor(6, $y++, "• Success/Completed", [PmcVT100]::Green(), "")
        $this.terminal.WriteAtColor(6, $y++, "• Errors/Warnings", [PmcVT100]::Red(), "")
        $this.terminal.WriteAtColor(6, $y++, "• Information", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAtColor(6, $y++, "• Highlights", [PmcVT100]::Yellow(), "")
        $y++

        $this.terminal.WriteAt(4, $y++, "Available themes:")
        $y++
        $this.terminal.WriteAtColor(6, $y++, "1. Default - Standard color scheme", [PmcVT100]::White(), "")
        $this.terminal.WriteAtColor(6, $y++, "2. Dark - High contrast dark theme", [PmcVT100]::White(), "")
        $this.terminal.WriteAtColor(6, $y++, "3. Light - Light background theme", [PmcVT100]::White(), "")
        $this.terminal.WriteAtColor(6, $y++, "4. Solarized - Solarized color palette", [PmcVT100]::White(), "")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Press number key to preview theme", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, $y, "Press 'A' to apply selected theme", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "1-4:Select | A:Apply | Esc:Cancel")
    }

    [void] DrawApplyTheme() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Apply Theme "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        $this.terminal.WriteAtColor(4, $y++, "Select a theme to apply:", [PmcVT100]::Cyan(), "")
        $y++

        $this.terminal.WriteAtColor(6, $y++, "1. Default Theme", [PmcVT100]::White(), "")
        $this.terminal.WriteAt(8, $y++, "Standard colors optimized for dark terminals")
        $y++

        $this.terminal.WriteAtColor(6, $y++, "2. Dark Theme", [PmcVT100]::White(), "")
        $this.terminal.WriteAt(8, $y++, "High contrast with bright highlights")
        $y++

        $this.terminal.WriteAtColor(6, $y++, "3. Light Theme", [PmcVT100]::White(), "")
        $this.terminal.WriteAt(8, $y++, "Designed for light terminal backgrounds")
        $y++

        $this.terminal.WriteAtColor(6, $y++, "4. Solarized Theme", [PmcVT100]::White(), "")
        $this.terminal.WriteAt(8, $y++, "Popular Solarized color palette")
        $y++
        $y++

        $this.terminal.WriteAtColor(4, $y, "Press number to apply theme (changes take effect immediately)", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "1-4:Apply Theme | Esc:Cancel")
    }

    [void] DrawCopyTaskForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Copy/Duplicate Task "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID to copy:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(4, 8, "This will create an exact duplicate of the task")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter task ID | Esc:Cancel")
    }

    [void] HandleCopyTaskForm() {
        $fields = @(
            @{Name='taskId'; Label='Task ID to copy'; Required=$true}
        )
        $res = Show-InputForm -Title "Copy Task" -Fields $fields
        if ($null -eq $res) { $this.GoBackOr('tasklist'); return }
        $taskId = [string]$res['taskId']
        if ([string]::IsNullOrWhiteSpace($taskId)) { $this.GoBackOr('tasklist'); return }
        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq [int]$taskId } | Select-Object -First 1
            if ($task) {
                $clone = $task.PSObject.Copy()
                $clone.id = ($data.tasks | ForEach-Object { $_.id } | Measure-Object -Maximum).Maximum + 1
                $data.tasks += $clone
                Set-PmcAllData $data
                Show-InfoMessage -Message ("Task {0} duplicated as task {1}" -f $taskId, $clone.id) -Title "Success" -Color "Green"
                $this.LoadTasks()
            } else {
                Show-InfoMessage -Message ("Task {0} not found" -f $taskId) -Title "Error" -Color "Red"
            }
        } catch {
            Show-InfoMessage -Message ("Error: {0}" -f $_) -Title "Error" -Color "Red"
        }
        $this.GoBackOr('tasklist')
    }

    [void] DrawMoveTaskForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Move Task to Project "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "Project Name:", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter fields | Esc:Cancel")
    }

    [void] HandleMoveTaskForm() {
        $data = Get-PmcAllData
        $projectList = @('inbox') + @($data.projects | ForEach-Object { $_.name } | Where-Object { $_ -and $_ -ne 'inbox' } | Sort-Object)
        $fields = @(
            @{Name='taskId'; Label='Task ID to move'; Required=$true}
            @{Name='project'; Label='Target Project'; Required=$true; Type='select'; Options=$projectList}
        )
        $res = Show-InputForm -Title "Move Task to Project" -Fields $fields
        if ($null -eq $res) { $this.GoBackOr('tasklist'); return }
        $taskId = [string]$res['taskId']
        $project = [string]$res['project']
        if ([string]::IsNullOrWhiteSpace($taskId) -or [string]::IsNullOrWhiteSpace($project)) { $this.GoBackOr('tasklist'); return }
        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq [int]$taskId } | Select-Object -First 1
            if ($task) {
                $task.project = $project
                Set-PmcAllData $data
                Show-InfoMessage -Message ("Moved task {0} to @{1}" -f $taskId, $project) -Title "Success" -Color "Green"
                $this.LoadTasks()
            } else {
                Show-InfoMessage -Message ("Task {0} not found" -f $taskId) -Title "Error" -Color "Red"
            }
        } catch {
            Show-InfoMessage -Message ("Error: {0}" -f $_) -Title "Error" -Color "Red"
        }
        $this.GoBackOr('tasklist')
    }

    [void] DrawSetPriorityForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Set Task Priority "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "Priority (high/medium/low):", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter fields | Esc:Cancel")
    }

    [void] HandleSetPriorityForm() {
        $fields = @(
            @{Name='taskId'; Label='Task ID'; Required=$true; Type='text'}
            @{Name='priority'; Label='Priority'; Required=$true; Type='select'; Options=@('high','medium','low','none')}
        )
        $result = Show-InputForm -Title "Set Task Priority" -Fields $fields
        if ($null -eq $result) { $this.GoBackOr('tasklist'); return }
        $taskIdStr = [string]$result['taskId']
        $priority = [string]$result['priority']
        $tid = 0; try { $tid = [int]$taskIdStr } catch { $tid = 0 }
        if ($tid -le 0 -or [string]::IsNullOrWhiteSpace($priority)) { Show-InfoMessage -Message "Invalid inputs" -Title "Validation" -Color "Red"; $this.GoBackOr('tasklist'); return }
        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq $tid } | Select-Object -First 1
            if ($task) {
                $task.priority = if ($priority -eq 'none') { $null } else { $priority.ToLower() }
                Set-PmcAllData $data
                Show-InfoMessage -Message ("Set task {0} priority to {1}" -f $tid, ($priority)) -Title "Success" -Color "Green"
                $this.LoadTasks()
            } else {
                Show-InfoMessage -Message ("Task {0} not found" -f $tid) -Title "Error" -Color "Red"
            }
        } catch {
            Show-InfoMessage -Message ("Failed to set priority: {0}" -f $_) -Title "Error" -Color "Red"
        }
        $this.GoBackOr('tasklist')
    }

    [void] DrawPostponeTaskForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Postpone Task "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "Days to postpone (default: 1):", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter fields | Esc:Cancel")
    }

    [void] HandlePostponeTaskForm() {
        $fields = @(
            @{Name='taskId'; Label='Task ID'; Required=$true; Type='text'}
            @{Name='days'; Label='Days to postpone (default 1)'; Required=$false; Type='text'}
        )
        $result = Show-InputForm -Title "Postpone Task" -Fields $fields
        if ($null -eq $result) { $this.GoBackOr('tasklist'); return }
        $taskIdStr = [string]$result['taskId']
        $daysStr = [string]$result['days']
        $tid = 0; try { $tid = [int]$taskIdStr } catch { $tid = 0 }
        if ($tid -le 0) { Show-InfoMessage -Message "Invalid task ID" -Title "Validation" -Color "Red"; $this.GoBackOr('tasklist'); return }
        $days = 1; try { if (-not [string]::IsNullOrWhiteSpace($daysStr)) { $days = [int]$daysStr } } catch { $days = 1 }
        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq $tid } | Select-Object -First 1
            if ($task) {
                $currentDue = if ($task.due) { (Get-ConsoleUIDateOrNull $task.due) } else { Get-Date }
                if (-not $currentDue) { $currentDue = Get-Date }
                $task.due = $currentDue.AddDays($days).ToString('yyyy-MM-dd')
                Set-PmcAllData $data
                Show-InfoMessage -Message ("Postponed task {0} by {1} day(s) to {2}" -f $tid,$days,$task.due) -Title "Success" -Color "Green"
                $this.LoadTasks()
            } else {
                Show-InfoMessage -Message ("Task {0} not found" -f $tid) -Title "Error" -Color "Red"
            }
        } catch {
            Show-InfoMessage -Message ("Failed to postpone: {0}" -f $_) -Title "Error" -Color "Red"
        }
        $this.GoBackOr('tasklist')
    }

    [void] DrawAddNoteForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Add Note to Task "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Task ID:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAtColor(4, 8, "Note:", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter fields | Esc:Cancel")
    }

    [void] HandleAddNoteForm() {
        $fields = @(
            @{Name='taskId'; Label='Task ID'; Required=$true; Type='text'}
            @{Name='note'; Label='Note'; Required=$true; Type='text'}
        )
        $result = Show-InputForm -Title "Add Note to Task" -Fields $fields
        if ($null -eq $result) { $this.GoBackOr('tasklist'); return }
        $taskIdStr = [string]$result['taskId']
        $note = [string]$result['note']
        $tid = 0; try { $tid = [int]$taskIdStr } catch { $tid = 0 }
        if ($tid -le 0 -or [string]::IsNullOrWhiteSpace($note)) { Show-InfoMessage -Message "Invalid inputs" -Title "Validation" -Color "Red"; $this.GoBackOr('tasklist'); return }
        try {
            $data = Get-PmcAllData
            $task = $data.tasks | Where-Object { $_.id -eq $tid } | Select-Object -First 1
            if ($task) {
                if (-not $task.notes) { $task.notes = @() }
                $task.notes += $note
                Set-PmcAllData $data
                Show-InfoMessage -Message ("Added note to task {0}" -f $tid) -Title "Success" -Color "Green"
                $this.LoadTasks()
            } else {
                Show-InfoMessage -Message ("Task {0} not found" -f $tid) -Title "Error" -Color "Red"
            }
        } catch {
            Show-InfoMessage -Message ("Failed to add note: {0}" -f $_) -Title "Error" -Color "Red"
        }
        $this.GoBackOr('tasklist')
    }

    [void] DrawEditProjectForm() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Edit Project "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $this.terminal.WriteAtColor(4, 6, "Edit fields; Tab/Shift+Tab navigate; F2 picks path; Enter saves; Esc cancels.", [PmcVT100]::Yellow(), "")
        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Tab/Shift+Tab navigate  |  F2: Pick path  |  Enter: Save  |  Esc: Cancel")
    }

    [void] HandleEditProjectForm() {
        # Determine target project
        $projectName = ''
        if ($this.PSObject.Properties['selectedProjectName'] -and $this.selectedProjectName) { $projectName = [string]$this.selectedProjectName }
        if (-not $projectName) {
            try {
                $data = Get-PmcAllData
                $projectNames = @($data.projects | ForEach-Object { if ($_ -is [string]) { $_ } else { $_.name } } | Where-Object { $_ })
                $sel = Show-SelectList -Title "Select Project to Edit" -Options $projectNames
                if (-not $sel) { $this.GoBackOr('projectlist'); return }
                $projectName = $sel
            } catch { $this.GoBackOr('projectlist'); return }
        }

        try {
            $data = Get-PmcAllData
            $project = $data.projects | Where-Object { ($_.PSObject.Properties['name'] -and $_.name -eq $projectName) -or ($_ -is [string] -and $_ -eq $projectName) } | Select-Object -First 1
            if ($project -is [string]) { $project = [pscustomobject]@{ name = $project } }

            $this.DrawEditProjectForm()

            $rowStart = 6
            $defaultRoot = $Script:DefaultPickerRoot
            # Build fields with current values (avoid inline if in hashtable values)
            $nameVal       = [string]$project.name
            $descVal       = if ($project.PSObject.Properties['description']) { [string]$project.description } else { '' }
            $statusVal     = if ($project.PSObject.Properties['status']) { [string]$project.status } else { '' }
            $tagsVal       = if ($project.PSObject.Properties['tags']) { [string]::Join(', ', $project.tags) } else { '' }
            $id1Val        = if ($project.PSObject.Properties['ID1']) { [string]$project.ID1 } else { '' }
            $id2Val        = if ($project.PSObject.Properties['ID2']) { [string]$project.ID2 } else { '' }
            $projFolderVal = if ($project.PSObject.Properties['ProjFolder']) { [string]$project.ProjFolder } else { '' }
            $caaVal        = if ($project.PSObject.Properties['CAAName']) { [string]$project.CAAName } else { '' }
            $reqVal        = if ($project.PSObject.Properties['RequestName']) { [string]$project.RequestName } else { '' }
            $t2020Val      = if ($project.PSObject.Properties['T2020']) { [string]$project.T2020 } else { '' }
            $assignedVal   = if ($project.PSObject.Properties['AssignedDate']) { [string]$project.AssignedDate } else { '' }
            $dueVal        = if ($project.PSObject.Properties['DueDate']) { [string]$project.DueDate } else { '' }
            $bfVal         = if ($project.PSObject.Properties['BFDate']) { [string]$project.BFDate } else { '' }

            $fields = @(
                @{ Name='Name';        Label='Project Name:';                      X=16; Y=($rowStart + 1);  Value=$nameVal }
                @{ Name='Description'; Label='Description:';                        X=16; Y=($rowStart + 2);  Value=$descVal }
                @{ Name='Status';      Label='Status:';                             X=12; Y=($rowStart + 3);  Value=$statusVal }
                @{ Name='Tags';        Label='Tags (comma-separated):';             X=30; Y=($rowStart + 4);  Value=$tagsVal }
                @{ Name='ID1';         Label='ID1:';                                X=9;  Y=($rowStart + 5);  Value=$id1Val }
                @{ Name='ID2';         Label='ID2:';                                X=9;  Y=($rowStart + 6);  Value=$id2Val }
                @{ Name='ProjFolder';  Label='Project Folder:';                     X=20; Y=($rowStart + 7);  Value=$projFolderVal }
                @{ Name='CAAName';     Label='CAA Name:';                           X=14; Y=($rowStart + 8);  Value=$caaVal }
                @{ Name='RequestName'; Label='Request Name:';                       X=17; Y=($rowStart + 9);  Value=$reqVal }
                @{ Name='T2020';       Label='T2020:';                              X=11; Y=($rowStart + 10); Value=$t2020Val }
                @{ Name='AssignedDate';Label='Assigned Date (yyyy-MM-dd):';         X=32; Y=($rowStart + 11); Value=$assignedVal }
                @{ Name='DueDate';     Label='Due Date (yyyy-MM-dd):';              X=27; Y=($rowStart + 12); Value=$dueVal }
                @{ Name='BFDate';      Label='BF Date (yyyy-MM-dd):';               X=26; Y=($rowStart + 13); Value=$bfVal }
            )

            # Draw labels/values
            foreach ($f in $fields) { $this.terminal.WriteAtColor(4, $f['Y'], $f['Label'], [PmcVT100]::Yellow(), ""); $this.terminal.WriteAt($f['X'], $f['Y'], [string]$f['Value']) }
            $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Tab/Shift+Tab navigate  |  F2: Pick path  |  Enter: Save  |  Esc: Cancel")

            # In-form editor with F2 pickers and active label highlight
            $active = 0
            $prevActive = -1
            while ($true) {
                $f = $fields[$active]
                if ($prevActive -ne $active) {
                    if ($prevActive -ge 0) { $pf = $fields[$prevActive]; $this.terminal.WriteAtColor(4, $pf['Y'], $pf['Label'], [PmcVT100]::Yellow(), "") }
                    $this.terminal.WriteAtColor(4, $f['Y'], $f['Label'], [PmcVT100]::BgBlue(), [PmcVT100]::White())
                    $prevActive = $active
                }
                $buf = [string]($f['Value'] ?? '')
                $col = [int]$f['X']; $row = [int]$f['Y']
                [Console]::SetCursorPosition($col + $buf.Length, $row)
                $k = [Console]::ReadKey($true)
                if ($k.Key -eq 'Enter') { break }
                elseif ($k.Key -eq 'Escape') { $this.GoBackOr('projectlist'); return }
                elseif ($k.Key -eq 'F2') {
                    $fname = [string]$f['Name']
                    if ($fname -in @('ProjFolder','CAAName','RequestName','T2020')) {
                        $dirsOnly = ($fname -eq 'ProjFolder')
                        $hint = "Pick $fname"
                        $picked = Select-ConsoleUIPathAt -app $this -Hint $hint -Col $col -Row $row -StartPath $defaultRoot -DirectoriesOnly:$dirsOnly
                        if ($null -ne $picked) {
                            $fields[$active]['Value'] = $picked
                            $this.terminal.FillArea($col, $row, $this.terminal.Width - $col - 2, 1, ' ')
                            $this.terminal.WriteAt($col, $row, [string]$picked)
                        }
                    }
                    continue
                }
                elseif ($k.Key -eq 'Tab') {
                    $isShift = ("" + $k.Modifiers) -match 'Shift'
                    if ($isShift) { $active = ($active - 1); if ($active -lt 0) { $active = $fields.Count - 1 } } else { $active = ($active + 1) % $fields.Count }
                    continue
                } elseif ($k.Key -eq 'Backspace') {
                    if ($buf.Length -gt 0) {
                        $buf = $buf.Substring(0, $buf.Length - 1)
                        $fields[$active]['Value'] = $buf
                        $this.terminal.FillArea($col, $row, $this.terminal.Width - $col - 2, 1, ' ')
                        $this.terminal.WriteAt($col, $row, $buf)
                    }
                    continue
                } else {
                    $ch = $k.KeyChar
                    if ($ch -and $ch -ne "`0") {
                        $buf += $ch
                        $fields[$active]['Value'] = $buf
                        $this.terminal.WriteAt($col + $buf.Length - 1, $row, $ch.ToString())
                    }
                }
            }

            # Collect new values
            $vals = @{}
            foreach ($f in $fields) { $vals[$f['Name']] = [string]$f['Value'] }

            foreach ($d in @('AssignedDate','DueDate','BFDate')) {
                $norm = Normalize-ConsoleUIDate $vals[$d]
                if ($null -eq $norm -and -not [string]::IsNullOrWhiteSpace([string]$vals[$d])) {
                    Show-InfoMessage -Message ("Invalid {0}. Use yyyymmdd, mmdd, +/-N, today/tomorrow/yesterday, or yyyy-MM-dd." -f $d) -Title "Validation" -Color "Red"
                    $this.GoBackOr('projectlist'); return
                }
                $vals[$d] = $norm
            }

            # Track if any changes occur
            $changed = $false
            # Handle project name change first (rename)
            $newName = ([string]$vals['Name']).Trim()
            if ([string]::IsNullOrWhiteSpace($newName)) { $newName = $projectName }
            if ($newName -ne $projectName) {
                # Validate duplicate
                $hasNew = @($data.projects | Where-Object { ($_ -is [string] -and $_ -eq $newName) -or ($_.PSObject.Properties['name'] -and $_.name -eq $newName) }).Count -gt 0
                if ($hasNew) {
                    Show-InfoMessage -Message ("Project '{0}' already exists" -f $newName) -Title "Error" -Color "Red"
                    $this.GoBackOr('projectlist'); return
                }
                # Apply rename across projects, tasks, and timelogs
                $newProjects = @()
                foreach ($p in @($data.projects)) {
                    if ($p -is [string]) {
                        $newProjects += if ($p -eq $projectName) { $newName } else { $p }
                    } else {
                        if ($p.name -eq $projectName) { $p.name = $newName }
                        $newProjects += $p
                    }
                }
                $data.projects = $newProjects
                foreach ($t in $data.tasks) { if ($t.project -eq $projectName) { $t.project = $newName } }
                if ($data.PSObject.Properties['timelogs']) { foreach ($log in $data.timelogs) { if ($log.project -eq $projectName) { $log.project = $newName } } }
                $this.selectedProjectName = $newName
                $projectName = $newName
                $changed = $true
            }

            # Apply other field updates
            foreach ($key in @('Description','Status','ID1','ID2','ProjFolder','CAAName','RequestName','T2020','AssignedDate','DueDate','BFDate','Tags')) {
                $newVal = $vals[$key]
                if ($key -eq 'Tags') {
                    $oldTags = if ($project.PSObject.Properties['tags']) { [string]::Join(', ', $project.tags) } else { '' }
                    if ($newVal -ne $oldTags) { $project | Add-Member -MemberType NoteProperty -Name 'tags' -Value (@($newVal -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })) -Force; $changed = $true }
                } elseif ($key -eq 'Status') {
                    $old = if ($project.PSObject.Properties['status']) { [string]$project.status } else { '' }
                    if ($newVal -ne $old) { $project | Add-Member -MemberType NoteProperty -Name 'status' -Value $newVal -Force; $changed = $true }
                } else {
                    $old = if ($project.PSObject.Properties[$key]) { [string]$project.$key } else { '' }
                    if ($newVal -ne $old) { $project | Add-Member -MemberType NoteProperty -Name $key -Value $newVal -Force; $changed = $true }
                }
            }

            if ($changed) { Set-PmcAllData $data }
            $this.GoBackOr('projectlist')
        } catch {
        }
    }

    [void] DrawProjectInfoView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Project Info "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Project Name:", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter project name | Esc:Cancel")
    }

    [void] HandleProjectInfoView() {
        try {
            $data = Get-PmcAllData
            $projectNames = @($data.projects | ForEach-Object { if ($_ -is [string]) { $_ } else { $_.name } } | Where-Object { $_ })
            $projectName = Show-SelectList -Title "Select Project" -Options $projectNames
            if (-not $projectName) { $this.GoBackOr('projectlist'); return }
        } catch { $this.GoBackOr('projectlist'); return }

        try {
            $data = Get-PmcAllData
            $project = $data.projects | Where-Object { $_.name -eq $projectName } | Select-Object -First 1
            if ($project) {
                $y = 8
                $this.terminal.WriteAtColor(4, $y++, "Project: $($project.name)", [PmcVT100]::Cyan(), "")
                $y++
                $this.terminal.WriteAt(4, $y++, "ID: $($project.id)")
                $this.terminal.WriteAt(4, $y++, "Description: $($project.description)")
                $this.terminal.WriteAt(4, $y++, "Status: $($project.status)")
                $this.terminal.WriteAt(4, $y++, "Created: $($project.created)")
                if ($project.tags) {
                    $this.terminal.WriteAt(4, $y++, "Tags: $($project.tags -join ', ')")
                }
                $y++

                # Count tasks
                $taskCount = @($data.tasks | Where-Object { $_.project -eq $projectName }).Count
                $completedCount = @($data.tasks | Where-Object { $_.project -eq $projectName -and $_.status -eq 'completed' }).Count
                $this.terminal.WriteAtColor(4, $y++, "Tasks: $taskCount total, $completedCount completed", [PmcVT100]::Green(), "")
            } else {
                $this.terminal.WriteAtColor(4, 8, "Project '$projectName' not found", [PmcVT100]::Red(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 8, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
        [Console]::ReadKey($true) | Out-Null
        $this.GoBackOr('tasklist')
    }

    [void] DrawProjectDetailView([string]$ProjectName) {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Project Detail "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            $proj = $data.projects | Where-Object { ($_ -is [string] -and $_ -eq $ProjectName) -or ($_.PSObject.Properties['name'] -and $_.name -eq $ProjectName) } | Select-Object -First 1
            if (-not $proj) { $this.terminal.WriteAtColor(4, 6, "Project '$ProjectName' not found", [PmcVT100]::Red(), ""); return }

            $name = if ($proj -is [string]) { $proj } else { [string]$proj.name }
            $desc = if ($proj -is [string]) { '' } else { [string]$proj.description }
            $status = if ($proj -is [string]) { '' } else { [string]$proj.status }
            $created = if ($proj -is [string]) { '' } else { [string]$proj.created }

            $y = 6
            $this.terminal.WriteAtColor(4, $y++, "Name: $name", [PmcVT100]::Cyan(), "")
            if ($desc) { $this.terminal.WriteAt(4, $y++, "Description: $desc") }
            if ($status) { $this.terminal.WriteAt(4, $y++, "Status: $status") }
            if ($created) { $this.terminal.WriteAt(4, $y++, "Created: $created") }

            # Extended fields if present
            foreach ($pair in @('ID1','ID2','ProjFolder','CAAName','RequestName','T2020','AssignedDate','DueDate','BFDate')) {
                if ($proj.PSObject.Properties[$pair] -and $proj.$pair) {
                    $this.terminal.WriteAt(4, $y++, ("{0}: {1}" -f $pair, [string]$proj.$pair))
                }
            }

            # Excel/T2020 Data if present
            if ($proj.PSObject.Properties['excelData'] -and $proj.excelData) {
                $y++
                $this.terminal.WriteAtColor(4, $y++, "Excel/T2020 Data:", [PmcVT100]::Cyan(), "")
                $excelData = $proj.excelData
                if ($excelData.imported) { $this.terminal.WriteAt(6, $y++, "Imported: $($excelData.imported)") }
                if ($excelData.source) { $this.terminal.WriteAt(6, $y++, "Source: $($excelData.source)") }
                if ($excelData.sourceSheet) { $this.terminal.WriteAt(6, $y++, "Source Sheet: $($excelData.sourceSheet)") }
                if ($excelData.txtExport) { $this.terminal.WriteAt(6, $y++, "Txt Export: $($excelData.txtExport)") }

                # Show key fields
                if ($excelData.fields) {
                    $y++
                    $this.terminal.WriteAtColor(6, $y++, "Key Fields:", [PmcVT100]::Yellow(), "")
                    $keyFields = @('RequestDate','AuditType','TPName','TaxID','CASNumber','Status','DueDate')
                    foreach ($fieldName in $keyFields) {
                        if ($excelData.fields.PSObject.Properties[$fieldName] -and $excelData.fields.$fieldName) {
                            if ($y -ge $this.terminal.Height - 8) { break }
                            $value = [string]$excelData.fields.$fieldName
                            if ($value.Length -gt 40) { $value = $value.Substring(0, 37) + "..." }
                            $this.terminal.WriteAt(8, $y++, "$fieldName`: $value")
                        }
                    }
                }
            }

            # Show first 15 tasks for this project
            $y++
            $this.terminal.WriteAtColor(4, $y++, "Tasks (first 15):", [PmcVT100]::Yellow(), "")
            $projTasks = @($data.tasks | Where-Object { $_.project -eq $name })
            $shown = 0
            foreach ($t in $projTasks) {
                if ($y -ge $this.terminal.Height - 4) { break }
                $statusIcon = if ($t.status -eq 'completed') { 'X' } else { 'o' }
                $line = ("[{0}] #{1} {2}" -f $statusIcon, $t.id, $t.text)
                $this.terminal.WriteAt(4, $y++, $line.Substring(0, [Math]::Min($line.Length, $this.terminal.Width - 6)))
                $shown++
                if ($shown -ge 15) { break }
            }
            if ($projTasks.Count -gt $shown) { $this.terminal.WriteAt(4, $y++, ("... and {0} more" -f ($projTasks.Count - $shown))) }

        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("F10/Alt:Menus  V:View Tasks  E:Edit  Esc:Back")
    }

    [void] HandleProjectDetailView() {
        # Expect $this.selectedProjectName set by caller
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'projectdetail') {
            $projName = ''
            if ($this.PSObject.Properties['selectedProjectName'] -and $this.selectedProjectName) { $projName = [string]$this.selectedProjectName } else { $active = $false; break }
            $this.DrawProjectDetailView($projName)
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'projectdetail') { return }
                continue
            }
            switch ($key.Key) {
                'V' {
                    $this.filterProject = $projName
                    $this.previousView = 'projectdetail'
                    $this.currentView = 'tasklist'
                    $this.LoadTasks()
                    return
                }
                'E' {
                    $this.previousView = 'projectdetail'
                    $this.currentView = 'projectedit'
                    return
                }
                # 'Y' (legacy rename) removed; use E to edit name
                'Escape' { $active = $false }
                default {}
            }
        }
        $this.GoBackOr('projectlist')
    }

    [void] DrawRecentProjectsView() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Recent Projects "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        try {
            $data = Get-PmcAllData
            # Get recent tasks and extract unique projects
            $recentTasks = @($data.tasks | Where-Object { $_.project } | Sort-Object { if ($_.modified) { [DateTime]$_.modified } else { [DateTime]::MinValue } } -Descending | Select-Object -First 50)
            $recentProjects = @($recentTasks | Select-Object -ExpandProperty project -Unique | Select-Object -First 10)

            if ($recentProjects.Count -gt 0) {
                $y = 6
                $this.terminal.WriteAtColor(4, $y++, "Recently Used Projects:", [PmcVT100]::Cyan(), "")
                $y++

                foreach ($projectName in $recentProjects) {
                    $project = $data.projects | Where-Object { $_.name -eq $projectName } | Select-Object -First 1
                    $taskCount = @($data.tasks | Where-Object { $_.project -eq $projectName -and $_.status -ne 'completed' }).Count
                    if ($project) {
                        $this.terminal.WriteAtColor(4, $y++, "• $projectName", [PmcVT100]::Yellow(), "")
                        $this.terminal.WriteAt(6, $y++, "  $($project.description) ($taskCount active tasks)")
                    } else {
                        $this.terminal.WriteAt(4, $y++, "• $projectName ($taskCount active tasks)")
                    }
                }
            } else {
                $this.terminal.WriteAtColor(4, 6, "No recent projects found", [PmcVT100]::Yellow(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    [void] HandleRecentProjectsView() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'projectrecent') {
            $this.DrawRecentProjectsView()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'projectrecent') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    [void] DrawHelpBrowser() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Help Browser "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        $this.terminal.WriteAtColor(4, $y++, "PMC Task Management System - Help", [PmcVT100]::Cyan(), "")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Navigation:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(6, $y++, "F10 or Alt+Letter  - Open menus")
        $this.terminal.WriteAt(6, $y++, "Arrow Keys         - Navigate menus/lists")
        $this.terminal.WriteAt(6, $y++, "Enter              - Select item")
        $this.terminal.WriteAt(6, $y++, "Esc                - Cancel/Go back")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Quick Keys:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(6, $y++, "Alt+T  - Task menu")
        $this.terminal.WriteAt(6, $y++, "Alt+P  - Project menu")
        $this.terminal.WriteAt(6, $y++, "Alt+V  - View menu")
        $this.terminal.WriteAt(6, $y++, "Alt+M  - Time menu")
        $this.terminal.WriteAt(6, $y++, "Alt+O  - Tools menu")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "For more help:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(6, $y++, "Use Get-Command *Pmc* to see all available PowerShell commands")

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    [void] HandleHelpBrowser() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'helpbrowser') {
            $this.DrawHelpBrowser()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'helpbrowser') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    [void] DrawHelpCategories() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Help Categories "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        $this.terminal.WriteAtColor(4, $y++, "Available Help Topics:", [PmcVT100]::Cyan(), "")
        $y++

        $categories = @(
            @{Name="Tasks"; Desc="Creating, editing, and managing tasks"}
            @{Name="Projects"; Desc="Project organization and tracking"}
            @{Name="Time Tracking"; Desc="Time logging and timer functions"}
            @{Name="Views"; Desc="Different task views (Agenda, Kanban, etc.)"}
            @{Name="Focus"; Desc="Focus mode for concentrated work"}
            @{Name="Dependencies"; Desc="Task dependencies and relationships"}
            @{Name="Backup/Restore"; Desc="Data backup and recovery"}
        )

        foreach ($cat in $categories) {
            $this.terminal.WriteAtColor(4, $y++, "• $($cat.Name)", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAt(6, $y++, "  $($cat.Desc)")
        }

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    [void] HandleHelpCategories() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'helpcategories') {
            $this.DrawHelpCategories()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'helpcategories') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    [void] DrawHelpSearch() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Help Search "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $this.terminal.WriteAtColor(4, 6, "Search for:", [PmcVT100]::Yellow(), "")

        $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Enter search term | Esc:Cancel")
    }

    [void] HandleHelpSearch() {
        $fields = @(
            @{Name='q'; Label='Search term'; Required=$true}
        )
        $res = Show-InputForm -Title "Help Search" -Fields $fields
        if ($null -eq $res) { $this.GoBackOr('tasklist'); return }
        $searchTerm = [string]$res['q']
        if (-not [string]::IsNullOrWhiteSpace($searchTerm)) {
            $y = 8
            $this.terminal.WriteAtColor(4, $y++, "Search results for '$searchTerm':", [PmcVT100]::Cyan(), "")
            $y++

            # Enhanced keyword matching
            $helpTopics = @{
                'task|todo|add|create' = "Task Management - Add, edit, complete, delete tasks (Alt+T)"
                'project|organize' = "Project Organization - Create and manage projects (Alt+P)"
                'time|timer|track|log' = "Time Tracking - Track time on tasks, view reports (Alt+M)"
                'view|agenda|kanban|burndown' = "Views - Agenda, Kanban, Burndown charts (Alt+V)"
                'focus|concentrate' = "Focus Mode - Set focus for concentrated work (Alt+C)"
                'backup|restore|data' = "File Operations - Backup and restore data (Alt+F)"
                'undo|redo|revert' = "Edit Operations - Undo/redo changes (Alt+E)"
                'dependency|depends|block' = "Dependencies - Manage task dependencies (Alt+D)"
                'priority|urgent|high|low' = "Priority - Set task priority (P key in task list)"
                'due|date|deadline' = "Due Dates - Set task due dates (T key in task detail)"
                'search|find|filter' = "Search - Find tasks by text (/ key in task list)"
                'sort|order' = "Sorting - Sort tasks by various criteria (S key in task list)"
                'multi|bulk|batch' = "Multi-Select - Bulk operations on tasks (M key in task list)"
                'complete|done|finish' = "Complete Tasks - Mark tasks as done (D key or Space)"
                'delete|remove' = "Delete Tasks - Remove tasks (Delete key)"
                'help|keys|shortcuts' = "Keyboard Shortcuts - Press H for help browser"
            }

            $matches = @()
            $lowerSearch = $searchTerm.ToLower()
            foreach ($pattern in $helpTopics.Keys) {
                if ($lowerSearch -match $pattern) {
                    $matches += $helpTopics[$pattern]
                }
            }

            if ($matches.Count -gt 0) {
                foreach ($match in $matches) {
                    $this.terminal.WriteAt(4, $y++, "• $match")
                }
            } else {
                $this.terminal.WriteAtColor(4, $y, "No help topics found for '$searchTerm'", [PmcVT100]::Yellow(), "")
            }

            $this.terminal.FillArea(0, $this.terminal.Height - 1, $this.terminal.Width, 1, ' ')
            $this.terminal.WriteAt(2, $this.terminal.Height - 1, "F10/Alt:Menus  Esc:Back")
            $k = [Console]::ReadKey($true)
            $ga = $this.CheckGlobalKeys($k)
            if ($ga) {
                if ($ga -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($ga)
                if ($this.currentView -ne 'helpsearch') { return }
            }
        }

        $this.GoBackOr('tasklist')
    }

    [void] DrawAboutPMC() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " About PMC "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        $this.terminal.WriteAtColor(4, $y++, "PMC - PowerShell Project Management Console", [PmcVT100]::Cyan(), "")
        $y++
        $this.terminal.WriteAt(4, $y++, "A comprehensive task and project management system")
        $this.terminal.WriteAt(4, $y++, "built entirely in PowerShell")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Features:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(6, $y++, "• Task management with priorities and due dates")
        $this.terminal.WriteAt(6, $y++, "• Project organization")
        $this.terminal.WriteAt(6, $y++, "• Time tracking and reporting")
        $this.terminal.WriteAt(6, $y++, "• Multiple views (Agenda, Kanban, etc.)")
        $this.terminal.WriteAt(6, $y++, "• Focus mode")
        $this.terminal.WriteAt(6, $y++, "• Task dependencies")
        $this.terminal.WriteAt(6, $y++, "• Automatic backups")
        $this.terminal.WriteAt(6, $y++, "• Undo/Redo support")
        $y++

        $this.terminal.WriteAtColor(4, $y++, "Version:", [PmcVT100]::Yellow(), "")
        $this.terminal.WriteAt(6, $y++, "TUI Interface - October 2025")

        $this.terminal.DrawFooter("F10/Alt:Menus  Esc:Back")
    }

    [void] HandleAboutPMC() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'helpabout') {
            $this.DrawAboutPMC()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'helpabout') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    # Dependency Graph
    [void] DrawDependencyGraph() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Dependency Graph "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        try {
            $data = Get-PmcAllData
            $tasksWithDeps = $data.tasks | Where-Object { $_.dependencies -and $_.dependencies.Count -gt 0 }

            if ($tasksWithDeps.Count -eq 0) {
                $this.terminal.WriteAtColor(4, $y, "No task dependencies found", [PmcVT100]::Yellow(), "")
            } else {
                $this.terminal.WriteAtColor(4, $y++, "Task Dependencies:", [PmcVT100]::Cyan(), "")
                $y++

                foreach ($task in $tasksWithDeps) {
                    $textOrTitle = if ($task.text) { $task.text } else { $task.title }
                    $this.terminal.WriteAtColor(4, $y++, "Task #$($task.id): $textOrTitle", [PmcVT100]::White(), "")
                    $this.terminal.WriteAt(6, $y++, "└─> Depends on:")

                    $depCount = $task.dependencies.Count
                    for ($i = 0; $i -lt $depCount; $i++) {
                        $depId = $task.dependencies[$i]
                        $isLast = ($i -eq $depCount - 1)
                        $prefix = if ($isLast) { "    └─> " } else { "    ├─> " }

                        $depTask = $data.tasks | Where-Object { $_.id -eq $depId } | Select-Object -First 1
                        if ($depTask) {
                            $depStatus = $depTask.status
                            $statusIcon = switch ($depStatus) {
                                'done' { 'X' }
                                'completed' { 'X' }
                                'in-progress' { '...' }
                                'blocked' { 'BLOCKED' }
                                default { '-' }
                            }
                            $color = switch ($depStatus) {
                                'done' { [PmcVT100]::Green() }
                                'completed' { [PmcVT100]::Green() }
                                'in-progress' { [PmcVT100]::Yellow() }
                                'blocked' { [PmcVT100]::Red() }
                                default { [PmcVT100]::White() }
                            }
                            $depTextTitle = if ($depTask.text) { $depTask.text } else { $depTask.title }
                            $depText = "$prefix Task #${depId}: $depTextTitle $statusIcon"
                            $this.terminal.WriteAtColor(8, $y++, $depText, $color, "")
                        } else {
                            $this.terminal.WriteAtColor(8, $y++, "$prefix Task #${depId}: [Missing task] ERROR", [PmcVT100]::Red(), "")
                        }
                    }
                    $y++
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, $y, "Error loading dependencies: $($_.Exception.Message)", [PmcVT100]::Red(), "")
        }

        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "F10/Alt:Menus  Esc:Back")
    }

    [void] HandleDependencyGraph() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'depgraph') {
            $this.DrawDependencyGraph()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'depgraph') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    # Burndown Chart
    [void] DrawBurndownChart() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Burndown Chart "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        try {
            $data = Get-PmcAllData
            $currentProject = if ($this.filterProject) { $this.filterProject } else { $null }

            # Filter tasks by project if needed
            $projectTasks = if ($currentProject) {
                $data.tasks | Where-Object { $_.project -eq $currentProject }
            } else {
                $data.tasks
            }

            # Calculate burndown metrics
            $totalTasks = $projectTasks.Count
            $completedTasks = ($projectTasks | Where-Object { $_.status -eq 'done' -or $_.status -eq 'completed' }).Count
            $inProgressTasks = ($projectTasks | Where-Object { $_.status -eq 'in-progress' }).Count
            $blockedTasks = ($projectTasks | Where-Object { $_.status -eq 'blocked' }).Count
            $todoTasks = ($projectTasks | Where-Object { $_.status -eq 'todo' -or $_.status -eq 'active' -or -not $_.status }).Count

            $projectTitle = if ($currentProject) { "Project: $currentProject" } else { "All Projects" }
            $this.terminal.WriteAtColor(4, $y++, $projectTitle, [PmcVT100]::Cyan(), "")
            $y++

            $this.terminal.WriteAtColor(4, $y++, "Task Summary:", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAtColor(6, $y++, "Total Tasks:      $totalTasks", [PmcVT100]::White(), "")
            $this.terminal.WriteAtColor(6, $y++, "Completed:        $completedTasks", [PmcVT100]::Green(), "")
            $this.terminal.WriteAtColor(6, $y++, "In Progress:      $inProgressTasks", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAtColor(6, $y++, "Blocked:          $blockedTasks", [PmcVT100]::Red(), "")
            $this.terminal.WriteAtColor(6, $y++, "To Do:            $todoTasks", [PmcVT100]::White(), "")
            $y++

            # Calculate completion percentage
            $completionPct = if ($totalTasks -gt 0) { [math]::Round(($completedTasks / $totalTasks) * 100, 1) } else { 0 }
            $this.terminal.WriteAtColor(4, $y++, "Completion: $completionPct%", [PmcVT100]::Cyan(), "")
            $y++

            # Draw simple bar chart
            $barWidth = 50
            $completedWidth = if ($totalTasks -gt 0) { [math]::Floor(($completedTasks / $totalTasks) * $barWidth) } else { 0 }
            $inProgressWidth = if ($totalTasks -gt 0) { [math]::Floor(($inProgressTasks / $totalTasks) * $barWidth) } else { 0 }
            $remainingWidth = $barWidth - $completedWidth - $inProgressWidth

            $bar = ""
            if ($completedWidth -gt 0) { $bar += [string]::new('█', $completedWidth) }
            if ($inProgressWidth -gt 0) { $bar += [string]::new('▒', $inProgressWidth) }
            if ($remainingWidth -gt 0) { $bar += [string]::new('░', $remainingWidth) }

            $this.terminal.WriteAt(4, $y++, "Progress:")
            $this.terminal.WriteAt(4, $y++, "[$bar]")
            $y++

            # Legend
            $this.terminal.WriteAtColor(4, $y++, "Legend:", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAtColor(6, $y++, "█ Completed", [PmcVT100]::Green(), "")
            $this.terminal.WriteAtColor(6, $y++, "▒ In Progress", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAtColor(6, $y++, "░ To Do", [PmcVT100]::White(), "")

        } catch {
            $this.terminal.WriteAtColor(4, $y, "Error generating burndown chart: $($_.Exception.Message)", [PmcVT100]::Red(), "")
        }

        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "F10/Alt:Menus  Esc:Back")
    }

    [void] HandleBurndownChart() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'burndownview') {
            $this.DrawBurndownChart()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'burndownview') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    # Tools Menu - Start Review
    [void] DrawStartReview() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        $title = " Start Review "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())

        $y = 6
        try {
            $data = Get-PmcAllData
            $reviewableTasks = $data.tasks | Where-Object {
                $_.status -eq 'review' -or $_.status -eq 'done'
            } | Sort-Object -Property @{Expression={$_.priority}; Descending=$true}, due

            if ($reviewableTasks.Count -eq 0) {
                $this.terminal.WriteAtColor(4, $y, "No tasks available for review", [PmcVT100]::Yellow(), "")
            } else {
                $this.terminal.WriteAtColor(4, $y++, "Tasks for Review:", [PmcVT100]::Cyan(), "")
                $y++

                foreach ($task in $reviewableTasks) {
                    $status = $task.status
                    $color = switch ($status) {
                        'done' { [PmcVT100]::Green() }
                        'review' { [PmcVT100]::Yellow() }
                        default { [PmcVT100]::White() }
                    }

                    $dueStr = if ($task.due) { " (Due: $($task.due))" } else { "" }
                    $projectStr = if ($task.project) { " [$($task.project)]" } else { "" }

                    $this.terminal.WriteAtColor(4, $y++, "#$($task.id): $($task.title)$projectStr$dueStr", $color, "")

                    if ($y -gt $this.terminal.Height - 4) { break }
                }
            }
        } catch {
            $this.terminal.WriteAtColor(4, $y, "Error loading review tasks: $($_.Exception.Message)", [PmcVT100]::Red(), "")
        }

        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "F10/Alt:Menus  Esc:Back")
    }

    [void] HandleStartReview() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'toolsreview') {
            $this.DrawStartReview()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'toolsreview') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    # Tools Menu - Project Wizard (expanded to collect full project fields)
    [void] DrawProjectWizard() {
        # Reuse the project create form layout for consistency
        $this.DrawProjectCreateForm(0)
    }

    [void] HandleProjectWizard() {
        $this.DrawProjectWizard()

        $inputs = @{}
        $rowStart = 6
        $defaultRoot = $Script:DefaultPickerRoot
        $app = $this

        function Read-LineAt([int]$col, [int]$row, [bool]$required=$false) {
            [Console]::SetCursorPosition($col, $row)
            [Console]::Write([PmcVT100]::Yellow())
            $buf = ''
            while ($true) {
                $k = [Console]::ReadKey($true)
                switch ($k.Key) {
                    'Escape' { [Console]::Write([PmcVT100]::Reset()); return $null }
                    'Enter' {
                        [Console]::Write([PmcVT100]::Reset())
                        if ($required -and [string]::IsNullOrWhiteSpace($buf)) { return $null }
                        return $buf.Trim()
                    }
                    'Backspace' {
                        if ($buf.Length -gt 0) {
                            $buf = $buf.Substring(0, $buf.Length - 1)
                            [Console]::SetCursorPosition($col, $row)
                            [Console]::Write((' ' * ($buf.Length + 1)))
                            [Console]::SetCursorPosition($col, $row)
                            [Console]::Write($buf)
                        }
                    }
                    default {
                        $ch = $k.KeyChar
                        if ($ch -and $ch -ne "`0") { $buf += $ch; [Console]::Write($ch) }
                    }
                }
            }
        }

        # Step 1: Name (required)
        $inputs.Name = Read-LineAt 28 ($rowStart + 0) $true
        if ([string]::IsNullOrWhiteSpace($inputs.Name)) { $this.GoBackOr('tasklist'); return }

        # Step 2: Description
        $inputs.Description = Read-LineAt 16 ($rowStart + 1)
        if ($null -eq $inputs.Description) { $this.GoBackOr('tasklist'); return }
        $this.DrawProjectCreateForm(2); Draw-ConsoleUIProjectFormValues -app $app -RowStart $rowStart -Inputs $inputs

        # Step 3: IDs
        $inputs.ID1 = Read-LineAt 9 ($rowStart + 2)
        if ($null -eq $inputs.ID1) { $this.GoBackOr('tasklist'); return }
        $this.DrawProjectCreateForm(3); Draw-ConsoleUIProjectFormValues -app $app -RowStart $rowStart -Inputs $inputs
        $inputs.ID2 = Read-LineAt 9 ($rowStart + 3)
        if ($null -eq $inputs.ID2) { $this.GoBackOr('tasklist'); return }
        $this.DrawProjectCreateForm(4); Draw-ConsoleUIProjectFormValues -app $app -RowStart $rowStart -Inputs $inputs

        # Step 4: Paths (with pickers)
        $inputs.ProjFolder = Select-ConsoleUIPathAt -app $app -Hint "Project Folder (Enter to pick)" -Col 20 -Row ($rowStart + 4) -StartPath $defaultRoot -DirectoriesOnly:$true
        $this.DrawProjectCreateForm(5); Draw-ConsoleUIProjectFormValues -app $app -RowStart $rowStart -Inputs $inputs
        $inputs.CAAName = Select-ConsoleUIPathAt -app $app -Hint "CAA (Enter to pick)" -Col 14 -Row ($rowStart + 5) -StartPath $defaultRoot -DirectoriesOnly:$false
        $this.DrawProjectCreateForm(6); Draw-ConsoleUIProjectFormValues -app $app -RowStart $rowStart -Inputs $inputs
        $inputs.RequestName = Select-ConsoleUIPathAt -app $app -Hint "Request (Enter to pick)" -Col 17 -Row ($rowStart + 6) -StartPath $defaultRoot -DirectoriesOnly:$false
        $this.DrawProjectCreateForm(7); Draw-ConsoleUIProjectFormValues -app $app -RowStart $rowStart -Inputs $inputs
        $inputs.T2020 = Select-ConsoleUIPathAt -app $app -Hint "T2020 (Enter to pick)" -Col 11 -Row ($rowStart + 7) -StartPath $defaultRoot -DirectoriesOnly:$false
        $this.DrawProjectCreateForm(8); Draw-ConsoleUIProjectFormValues -app $app -RowStart $rowStart -Inputs $inputs

        # Step 5: Dates (validate yyyy-MM-dd)
        $inputs.AssignedDate = Read-LineAt 32 ($rowStart + 8); if ($null -eq $inputs.AssignedDate) { $this.GoBackOr('tasklist'); return }
        $inputs.DueDate      = Read-LineAt 27 ($rowStart + 9); if ($null -eq $inputs.DueDate)      { $this.GoBackOr('tasklist'); return }
        $inputs.BFDate       = Read-LineAt 26 ($rowStart + 10); if ($null -eq $inputs.BFDate)       { $this.GoBackOr('tasklist'); return }

        foreach ($pair in @(@{k='AssignedDate'; v=$inputs.AssignedDate}, @{k='DueDate'; v=$inputs.DueDate}, @{k='BFDate'; v=$inputs.BFDate})) {
            $norm = Normalize-ConsoleUIDate $pair.v
            if ($null -eq $norm -and -not [string]::IsNullOrWhiteSpace([string]$pair.v)) {
                Show-InfoMessage -Message ("Invalid {0}. Use yyyymmdd, mmdd, +/-N, today/tomorrow/yesterday, or yyyy-MM-dd." -f $pair.k) -Title "Validation" -Color "Red"
                $this.GoBackOr('tasklist')
                return
            }
            switch ($pair.k) {
                'AssignedDate' { $inputs.AssignedDate = $norm }
                'DueDate'      { $inputs.DueDate = $norm }
                'BFDate'       { $inputs.BFDate = $norm }
            }
        }

        try {
            $data = Get-PmcAllData
            if (-not $data.projects) { $data.projects = @() }

            # Normalize any legacy entries
            try {
                $normalized = @()
                foreach ($p in @($data.projects)) {
                    if ($p -is [string]) {
                        $normalized += [pscustomobject]@{
                            id = [guid]::NewGuid().ToString()
                            name = $p
                            description = ''
                            created = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
                            status = 'active'
                            tags = @()
                        }
                    } else { $normalized += $p }
                }
                $data.projects = $normalized
            } catch {}

            # Duplicate project name check
            $exists = @($data.projects | Where-Object { $_.PSObject.Properties['name'] -and $_.name -eq $inputs.Name })
            if ($exists.Count -gt 0) {
                Show-InfoMessage -Message ("Project '{0}' already exists" -f $inputs.Name) -Title "Error" -Color "Red"
                $this.GoBackOr('tasklist')
                return
            }

            # Build project object using extended fields schema
            $newProject = [pscustomobject]@{
                id = [guid]::NewGuid().ToString()
                name = $inputs.Name
                description = $inputs.Description
                ID1 = $inputs.ID1
                ID2 = $inputs.ID2
                ProjFolder = $inputs.ProjFolder
                AssignedDate = $inputs.AssignedDate
                DueDate = $inputs.DueDate
                BFDate = $inputs.BFDate
                CAAName = $inputs.CAAName
                RequestName = $inputs.RequestName
                T2020 = $inputs.T2020
                icon = ''
                color = 'Gray'
                sortOrder = 0
                aliases = @()
                isArchived = $false
                created = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
                status = 'active'
                tags = @()
            }

            $data.projects += $newProject
            Set-PmcAllData $data
            Show-InfoMessage -Message ("Project '{0}' created" -f $inputs.Name) -Title "Success" -Color "Green"
        } catch {
            Show-InfoMessage -Message ("Failed to create project: {0}" -f $_) -Title "Error" -Color "Red"
        }

        $this.GoBackOr('projectlist')
        $this.DrawLayout()
    }

    # (removed) Tools - Templates

    # Tools - Statistics
    [void] DrawStatistics() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Statistics "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $y = 6
        try {
            $data = Get-PmcAllData
            $total = $data.tasks.Count
            $completed = ($data.tasks | Where-Object { $_.status -eq 'done' -or $_.status -eq 'completed' }).Count
            $inProgress = ($data.tasks | Where-Object { $_.status -eq 'in-progress' }).Count
            $blocked = ($data.tasks | Where-Object { $_.status -eq 'blocked' }).Count
            $todo = ($data.tasks | Where-Object { $_.status -eq 'todo' -or $_.status -eq 'active' -or (-not $_.status) }).Count

            $this.terminal.WriteAtColor(4, $y++, "Task Statistics:", [PmcVT100]::Cyan(), "")
            $y++
            $this.terminal.WriteAt(4, $y++, "Total Tasks:      $total")
            $this.terminal.WriteAtColor(4, $y++, "Completed:        $completed", [PmcVT100]::Green(), "")
            $this.terminal.WriteAtColor(4, $y++, "In Progress:      $inProgress", [PmcVT100]::Yellow(), "")
            $this.terminal.WriteAtColor(4, $y++, "To Do:            $todo", [PmcVT100]::Cyan(), "")
            $this.terminal.WriteAtColor(4, $y++, "Blocked:          $blocked", [PmcVT100]::Red(), "")
            $y++
            $completionRate = if ($total -gt 0) { [math]::Round(($completed / $total) * 100, 1) } else { 0 }
            $this.terminal.WriteAtColor(4, $y++, "Completion Rate: $completionRate%", [PmcVT100]::Cyan(), "")

            # Validation check
            $sum = $completed + $inProgress + $todo + $blocked
            if ($sum -ne $total) {
                $other = $total - $sum
                $this.terminal.WriteAtColor(4, $y++, "Other:            $other", [PmcVT100]::Gray(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, $y, "Error: $($_.Exception.Message)", [PmcVT100]::Red(), "")
        }
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "F10/Alt:Menus  Esc:Back")
    }

    [void] HandleStatistics() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'toolsstatistics') {
            $this.DrawStatistics()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'toolsstatistics') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    # Tools - Velocity
    [void] DrawVelocity() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Team Velocity "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $y = 6
        try {
            $data = Get-PmcAllData
            $now = Get-Date
            $lastWeek = $now.AddDays(-7)
            $recentCompleted = ($data.tasks | Where-Object {
                ($_.status -eq 'done' -or $_.status -eq 'completed') -and $_.completed -and ([DateTime]$_.completed) -gt $lastWeek
            }).Count
            $this.terminal.WriteAtColor(4, $y++, "Velocity Metrics (Last 7 Days):", [PmcVT100]::Cyan(), "")
            $y++
            $this.terminal.WriteAtColor(4, $y++, "Tasks Completed:  $recentCompleted", [PmcVT100]::Green(), "")
            $avgPerDay = [math]::Round($recentCompleted / 7, 1)
            $this.terminal.WriteAt(4, $y++, "Avg Per Day:      $avgPerDay")
            $projectedWeek = [math]::Round($avgPerDay * 7, 0)
            $this.terminal.WriteAt(4, $y++, "Projected/Week:   $projectedWeek")
        } catch {
            $this.terminal.WriteAtColor(4, $y, "Error: $($_.Exception.Message)", [PmcVT100]::Red(), "")
        }
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "F10/Alt:Menus  Esc:Back")
    }

    [void] HandleVelocity() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'toolsvelocity') {
            $this.DrawVelocity()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'toolsvelocity') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    # Tools - Preferences
    [void] DrawPreferences() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Preferences "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $y = 6
        try {
            $cfg = Get-PmcConfig
            $defView = try { [string]$cfg.Behavior.DefaultView } catch { 'tasklist' }
            $autoSave = try { [bool]$cfg.Behavior.AutoSave } catch { $true }
            $showCompleted = try { [bool]$cfg.Behavior.ShowCompleted } catch { $true }
            $dateFmt = try { [string]$cfg.Behavior.DateFormat } catch { 'yyyy-MM-dd' }
            $this.terminal.WriteAtColor(4, $y++, "PMC Preferences:", [PmcVT100]::Cyan(), "")
            $y++
            $this.terminal.WriteAt(4, $y++, ("1. Default view:         {0}" -f $defView))
            $autoSaveStr = 'Disabled'; if ($autoSave) { $autoSaveStr = 'Enabled' }
            $showCompletedStr = 'No'; if ($showCompleted) { $showCompletedStr = 'Yes' }
            $this.terminal.WriteAt(4, $y++, ("2. Auto-save:            {0}" -f $autoSaveStr))
            $this.terminal.WriteAt(4, $y++, ("3. Show completed:       {0}" -f $showCompletedStr))
            $this.terminal.WriteAt(4, $y++, ("4. Date format:          {0}" -f $dateFmt))
            $this.terminal.WriteAt(2, $this.terminal.Height - 1, "Any key: Edit  F10/Alt:Menus  Esc:Back")
        } catch {
            $this.terminal.WriteAtColor(4, $y, "Error loading preferences: $($_.Exception.Message)", [PmcVT100]::Red(), "")
        }
    }

    [void] HandlePreferences() {
        # Show current
        $this.DrawPreferences()
        $key = [Console]::ReadKey($true)
        $globalAction = $this.CheckGlobalKeys($key)
        if ($globalAction) {
            if ($globalAction -eq 'app:exit') { $this.running = $false; return }
            $this.ProcessMenuAction($globalAction)
            return
        }
        if ($key.Key -eq 'Escape') { $this.GoBackOr('tasklist'); return }

        # Edit via form (no initial values supported; blanks keep current)
        $cfg = Get-PmcConfig
        $defView = try { [string]$cfg.Behavior.DefaultView } catch { 'tasklist' }
        $autoSave = try { [bool]$cfg.Behavior.AutoSave } catch { $true }
        $showCompleted = try { [bool]$cfg.Behavior.ShowCompleted } catch { $true }
        $dateFmt = try { [string]$cfg.Behavior.DateFormat } catch { 'yyyy-MM-dd' }

        $views = @('tasklist','todayview','agendaview')
        $fields = @(
            @{Name='defaultView'; Label=("Default view (current: {0})" -f $defView); Required=$false; Type='text'}
            @{Name='autoSave'; Label=("Auto-save (true/false) (current: {0})" -f ($autoSave.ToString().ToLower())); Required=$false; Type='text'}
            @{Name='showCompleted'; Label=("Show completed (true/false) (current: {0})" -f ($showCompleted.ToString().ToLower())); Required=$false; Type='text'}
            @{Name='dateFormat'; Label=("Date format (current: {0})" -f $dateFmt); Required=$false; Type='text'}
        )

        $result = Show-InputForm -Title "Edit Preferences" -Fields $fields
        if ($null -eq $result) { $this.GoBackOr('tasklist'); return }

        try {
            if (-not $cfg.ContainsKey('Behavior')) { $cfg['Behavior'] = @{} }

            # Validate defaultView
            $newView = $defView
            if ($result['defaultView']) {
                if ($views -contains $result['defaultView']) { $newView = $result['defaultView'] }
                else { Show-InfoMessage -Message "Invalid default view; keeping $defView" -Title "Validation" -Color "Yellow" }
            }
            $cfg.Behavior.DefaultView = $newView

            # Validate booleans
            function Parse-Bool([string]$s, [bool]$fallback) {
                if ([string]::IsNullOrWhiteSpace($s)) { return $fallback }
                $sl = $s.ToLower()
                if ($sl -eq 'true' -or $sl -eq 'false') { return ($sl -eq 'true') }
                Show-InfoMessage -Message ("Invalid boolean: '{0}'" -f $s) -Title "Validation" -Color "Yellow"
                return $fallback
            }
            $cfg.Behavior.AutoSave = Parse-Bool ($result['autoSave'] + '') $autoSave
            $cfg.Behavior.ShowCompleted = Parse-Bool ($result['showCompleted'] + '') $showCompleted

            # Date format: accept non-empty; else keep
            if ($result['dateFormat']) { $cfg.Behavior.DateFormat = ($result['dateFormat'] + '') }
            else { $cfg.Behavior.DateFormat = $dateFmt }

            Save-PmcConfig $cfg
            Show-InfoMessage -Message "Preferences updated" -Title "Success" -Color "Green"
        } catch {
            Show-InfoMessage -Message "Failed to save preferences: $_" -Title "Error" -Color "Red"
        }

        $this.GoBackOr('tasklist')
    }

    # Tools - Manage Aliases
    [void] DrawManageAliases() {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Manage Aliases "
        $titleX = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$titleX, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $y = 6
        $this.terminal.WriteAtColor(4, $y++, "Command Aliases:", [PmcVT100]::Cyan(), "")
        $y++
        $this.terminal.WriteAt(4, $y++, "ls     = List tasks")
        $this.terminal.WriteAt(4, $y++, "add    = Add task")
        $this.terminal.WriteAt(4, $y++, "done   = Complete task")
        $this.terminal.WriteAt(4, $y++, "rm     = Delete task")
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "F10/Alt:Menus  Esc:Back")
    }

    [void] HandleManageAliases() {
        $active = $true
        while ($active -and $this.running -and $this.currentView -eq 'toolsaliases') {
            $this.DrawManageAliases()
            $key = [Console]::ReadKey($true)
            $globalAction = $this.CheckGlobalKeys($key)
            if ($globalAction) {
                if ($globalAction -eq 'app:exit') { $this.running = $false; return }
                $this.ProcessMenuAction($globalAction)
                if ($this.currentView -ne 'toolsaliases') { return }
                continue
            }
            if ($key.Key -eq 'Escape') { $active = $false }
        }
        $this.GoBackOr('tasklist')
    }

    # (removed) Tools - Query Browser

    # Tools - Weekly Report
    [void] DrawWeeklyReport([int]$weekOffset = 0) {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()

        try {
            $data = Get-PmcAllData
            $logs = if ($data.PSObject.Properties['timelogs']) { $data.timelogs } else { @() }

            # Calculate week start (Monday)
            $today = Get-Date
            $daysFromMonday = ($today.DayOfWeek.value__ + 6) % 7
            $thisMonday = $today.AddDays(-$daysFromMonday).Date
            $weekStart = $thisMonday.AddDays($weekOffset * 7)
            $weekEnd = $weekStart.AddDays(4)

            $weekHeader = "Week of {0} - {1}" -f $weekStart.ToString('MMM dd'), $weekEnd.ToString('MMM dd, yyyy')

            # Add indicator for current/past/future week
            $weekIndicator = ''
            if ($weekOffset -eq 0) {
                $weekIndicator = ' (This Week)'
            } elseif ($weekOffset -lt 0) {
                $weeks = [Math]::Abs($weekOffset)
                $plural = if ($weeks -gt 1) { 's' } else { '' }
                $weekIndicator = " ($weeks week$plural ago)"
            } else {
                $plural = if ($weekOffset -gt 1) { 's' } else { '' }
                $weekIndicator = " ($weekOffset week$plural from now)"
            }

            $this.terminal.WriteAtColor(4, 4, "TIME REPORT", [PmcVT100]::Cyan(), "")
            $this.terminal.WriteAtColor(4, 5, "$weekHeader$weekIndicator", [PmcVT100]::Yellow(), "")

            # Filter logs for the week
            $weekLogs = @()
            for ($d = 0; $d -lt 5; $d++) {
                $dayDate = $weekStart.AddDays($d).ToString('yyyy-MM-dd')
                $dayLogs = $logs | Where-Object { $_.date -eq $dayDate }
                $weekLogs += $dayLogs
            }

            if ($weekLogs.Count -eq 0) {
                $this.terminal.WriteAtColor(4, 7, "No time entries for this week", [PmcVT100]::Yellow(), "")
            } else {
                # Group by project/indirect code
                $grouped = @{}
                foreach ($log in $weekLogs) {
                    $key = ''
                    if ($log.id1) {
                        $key = "#$($log.id1)"
                    } else {
                        $name = $log.project
                        if (-not $name) { $name = 'Unknown' }
                        $key = $name
                    }

                    if (-not $grouped.ContainsKey($key)) {
                        $name = ''
                        $id1 = ''
                        if ($log.id1) { $id1 = $log.id1; $name = '' } else { $name = ($log.project); if (-not $name) { $name = 'Unknown' } }
                        $grouped[$key] = @{
                            Name = $name
                            ID1 = $id1
                            Mon = 0; Tue = 0; Wed = 0; Thu = 0; Fri = 0; Total = 0
                        }
                    }

                    $logDate = [datetime]$log.date
                    $dayIndex = ($logDate.DayOfWeek.value__ + 6) % 7
                    $hours = [Math]::Round($log.minutes / 60.0, 1)

                    switch ($dayIndex) {
                        0 { $grouped[$key].Mon += $hours }
                        1 { $grouped[$key].Tue += $hours }
                        2 { $grouped[$key].Wed += $hours }
                        3 { $grouped[$key].Thu += $hours }
                        4 { $grouped[$key].Fri += $hours }
                    }
                    $grouped[$key].Total += $hours
                }

                # Draw table header
                $y = 7
                $header = "Name                 ID1   Mon    Tue    Wed    Thu    Fri    Total"
                $this.terminal.WriteAtColor(4, $y++, $header, [PmcVT100]::Cyan(), "")
                $this.terminal.WriteAtColor(4, $y++, "─" * 75, [PmcVT100]::Gray(), "")

                # Draw rows
                $grandTotal = 0
                foreach ($entry in ($grouped.GetEnumerator() | Sort-Object Key)) {
                    $d = $entry.Value
                    $row = "{0,-20} {1,-5} {2,6:F1} {3,6:F1} {4,6:F1} {5,6:F1} {6,6:F1} {7,8:F1}" -f `
                        $d.Name, $d.ID1, $d.Mon, $d.Tue, $d.Wed, $d.Thu, $d.Fri, $d.Total
                    $this.terminal.WriteAtColor(4, $y++, $row, [PmcVT100]::Yellow(), "")
                    $grandTotal += $d.Total
                }

                # Draw footer
                $this.terminal.WriteAtColor(4, $y++, "─" * 75, [PmcVT100]::Gray(), "")
                $totalRow = "                                                          Total: {0,8:F1}" -f $grandTotal
                $this.terminal.WriteAtColor(4, $y++, $totalRow, [PmcVT100]::Yellow(), "")
            }
        } catch {
            $this.terminal.WriteAtColor(4, 6, "Error generating weekly report: $_", [PmcVT100]::Red(), "")
        }

        $this.terminal.DrawFooter("=:Next Week | -:Previous Week | Any other key to return")
    }

    [void] HandleWeeklyReport() {
        [int]$weekOffset = 0
        $active = $true

        while ($active) {
            $this.DrawWeeklyReport($weekOffset)
            $key = [Console]::ReadKey($true)

            switch ($key.KeyChar) {
                '=' {
                    $weekOffset++
                }
                '-' {
                    $weekOffset--
                }
                default {
                    $active = $false
                }
            }

            if ($key.Key -eq 'Escape') {
                $active = $false
            }
        }

        $this.GoBackOr('timelist')
    }

    [void] Shutdown() {
        $this.terminal.Cleanup()
        # Restore prior error preference
        try { if ($PSBoundParameters -ne $null) { } } catch { }
        try { if ($Script:_PrevErrorActionPreference) { $ErrorActionPreference = $Script:_PrevErrorActionPreference } } catch {}
    }
}

# Theme tools (persistent)
function HandleThemeTool([PmcConsoleUIApp] $this) {
    $active = $true
    while ($active -and $this.running -and $this.currentView -eq 'toolstheme') {
        $this.terminal.Clear()
        $this.menuSystem.DrawMenuBar()
        $title = " Theme Tools "
        $tx = ($this.terminal.Width - $title.Length) / 2
        $this.terminal.WriteAtColor([int]$tx, 3, $title, [PmcVT100]::BgBlue(), [PmcVT100]::White())
        $y = 6
        $this.terminal.WriteAtColor(4, $y++, "A: Apply Theme (picker)", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAtColor(4, $y++, "E: Edit/Apply (picker)", [PmcVT100]::Cyan(), "")
        $this.terminal.WriteAt(2, $this.terminal.Height - 1, "A/E:Action  F10/Alt:Menus  Esc:Back")

        $key = [Console]::ReadKey($true)
        $ga = $this.CheckGlobalKeys($key)
        if ($ga) {
            if ($ga -eq 'app:exit') { $this.running = $false; return }
            $this.ProcessMenuAction($ga)
            if ($this.currentView -ne 'toolstheme') { return }
            continue
        }

        switch ($key.Key) {
            'Escape' { $active = $false; break }
            'E' {
                try {
                    $ctx = New-Object PmcCommandContext 'theme','edit'
                    Edit-PmcTheme -Context $ctx
                    Initialize-PmcThemeSystem
                    Show-InfoMessage -Message 'Theme updated' -Title 'Theme' -Color 'Green'
                } catch {
                    Show-InfoMessage -Message ("Theme editor error: {0}" -f $_) -Title 'Theme' -Color 'Red'
                }
            }
            'A' {
                $choices = @('Enter Hex...','default (#33aaff)','ocean','lime','purple','slate','matrix','amber','synthwave','high-contrast')
                $sel = Show-SelectList -Title "Select Theme" -Options $choices -DefaultValue 'default (#33aaff)'
                if (-not $sel) { continue }
                $arg = ''
                if ($sel -eq 'Enter Hex...') {
                    $res = Show-InputForm -Title "Enter Theme Color" -Fields @(@{Name='hex'; Label='#RRGGBB'; Required=$true})
                    if ($res -and $res['hex']) { $arg = [string]$res['hex'] } else { continue }
                } elseif ($sel -like 'default*') {
                    $arg = 'default'
                } else {
                    $arg = $sel
                }
                try {
                    $ctx = New-Object PmcCommandContext 'theme','apply'
                    $ctx.FreeText = @($arg)
                    Apply-PmcTheme -Context $ctx
                    Initialize-PmcThemeSystem
                    Show-InfoMessage -Message ("Theme applied: {0}" -f $arg) -Title 'Theme' -Color 'Green'
                } catch {
                    Show-InfoMessage -Message ("Apply failed: {0}" -f $_) -Title 'Theme' -Color 'Red'
                }
            }
            default {}
        }
    }
    $this.GoBackOr('tasklist')
}

# Persistent handler for summary views (today/overdue/etc.) with global menu support
function HandleSpecialViewPersistent([PmcConsoleUIApp]$this) {
    $active = $true
    while ($active -and $this.running) {
        # Build items list for selection depending on view
        try {
            $data = Get-PmcAllData
            $today = (Get-Date).Date
            switch ($this.currentView) {
                'todayview' {
                    $this.specialItems = @($data.tasks | Where-Object { $_.due -and $_.status -ne 'completed' -and (Get-ConsoleUIDateOrNull $_.due).Date -eq $today })
                }
                'tomorrowview' {
                    $this.specialItems = @($data.tasks | Where-Object {
                        $_.due -and $_.status -ne 'completed' -and (Get-ConsoleUIDateOrNull $_.due).Date -eq $today.AddDays(1)
                    })
                }
                'weekview' {
                    $weekEnd = $today.AddDays(7)
                    $this.specialItems = @($data.tasks | Where-Object {
                        $_.due -and $_.status -ne 'completed' -and ($d = Get-ConsoleUIDateOrNull $_.due) -and $d.Date -ge $today -and $d.Date -le $weekEnd
                    })
                }
                'monthview' {
                    $monthEnd = $today.AddDays(30)
                    $this.specialItems = @($data.tasks | Where-Object {
                        $_.due -and $_.status -ne 'completed' -and ($d = Get-ConsoleUIDateOrNull $_.due) -and $d.Date -ge $today -and $d.Date -le $monthEnd
                    })
                }
                'agendaview' {
                    $this.specialItems = @($data.tasks | Where-Object { $_.status -ne 'completed' } | Sort-Object {
                        $d = if ($_.due) { Get-ConsoleUIDateOrNull $_.due } else { $null }; if ($d) { $d } else { [DateTime]::MaxValue }
                    })
                }
                'overdueview' {
                    $this.specialItems = @($data.tasks | Where-Object { $_.due -and $_.status -ne 'completed' -and (Get-ConsoleUIDateOrNull $_.due).Date -lt $today })
                }
                'upcomingview' {
                    $this.specialItems = @($data.tasks | Where-Object { $_.due -and $_.status -ne 'completed' -and (Get-ConsoleUIDateOrNull $_.due).Date -gt $today })
                }
                'blockedview' {
                    $this.specialItems = @($data.tasks | Where-Object { $_.status -in @('blocked','waiting') })
                }
                'noduedateview' {
                    $this.specialItems = @($data.tasks | Where-Object { -not $_.due -and $_.status -ne 'completed' })
                }
                'nextactionsview' {
                    $this.specialItems = @($data.tasks | Where-Object { $_.priority -in @('high','medium') -and $_.status -ne 'completed' })
                }
                default { $this.specialItems = @($data.tasks | Where-Object { $_.status -ne 'completed' }) }
            }
            if ($this.specialSelectedIndex -ge $this.specialItems.Count) { $this.specialSelectedIndex = [Math]::Max(0, $this.specialItems.Count-1) }
        } catch { $this.specialItems = @() }
        switch ($this.currentView) {
            'todayview' { $this.DrawTodayView() }
            'tomorrowview' { $this.DrawTomorrowView() }
            'weekview' { $this.DrawWeekView() }
            'monthview' { $this.DrawMonthView() }
            'overdueview' { $this.DrawOverdueView() }
            'upcomingview' { $this.DrawUpcomingView() }
            'blockedview' { $this.DrawBlockedView() }
            'noduedateview' { $this.DrawNoDueDateView() }
            'nextactionsview' { $this.DrawNextActionsView() }
            'kanbanview' { $this.DrawKanbanView() }
            'agendaview' { $this.DrawAgendaView() }
            'timereport' { $this.DrawTimeReport() }
            'timerstatus' { $this.DrawTimerStatus() }
            'timerstart' { $this.DrawTimerStart() }
            'timerstop' { $this.DrawTimerStop() }
            'editundo' { $this.DrawUndoView() }
            'editredo' { $this.DrawRedoView() }
            'focusclear' { $this.DrawFocusClearView() }
            default { $this.DrawTodayView() }
        }

        $key = [Console]::ReadKey($true)
        $globalAction = $this.CheckGlobalKeys($key)
        if ($globalAction) {
            if ($globalAction -eq 'app:exit') { $this.running = $false; return }
            $this.ProcessMenuAction($globalAction)
            if ($this.currentView -notin @('todayview','tomorrowview','weekview','monthview','overdueview','upcomingview','blockedview','noduedateview','nextactionsview','kanbanview','agendaview','timereport','timerstatus','timerstart','timerstop','editundo','editredo','focusclear')) {
                return
            }
            continue
        }

        switch ($key.Key) {
            'Escape' { $active = $false }
            'UpArrow' { if ($this.specialSelectedIndex -gt 0) { $this.specialSelectedIndex-- } }
            'DownArrow' { if ($this.specialSelectedIndex -lt $this.specialItems.Count - 1) { $this.specialSelectedIndex++ } }
            'Enter' {
                if ($this.specialSelectedIndex -lt $this.specialItems.Count) {
                    $this.selectedTask = $this.specialItems[$this.specialSelectedIndex]
                    $this.previousView = $this.currentView
                    $this.currentView = 'taskdetail'
                    return
                }
            }
            'E' {
                if ($this.specialSelectedIndex -lt $this.specialItems.Count) {
                    $this.selectedTask = $this.specialItems[$this.specialSelectedIndex]
                    $this.previousView = $this.currentView
                    $this.currentView = 'taskedit'
                    return
                }
            }
            'D' {
                if ($this.specialSelectedIndex -lt $this.specialItems.Count) {
                    try {
                        $t = $this.specialItems[$this.specialSelectedIndex]
                        $t.status = if ($t.status -eq 'completed') { 'active' } else { 'completed' }
                        if ($t.status -eq 'completed') { $t.completed = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') } else { $t.completed = $null }
                        $d = Get-PmcAllData; Save-PmcData -Data $d -Action "Toggled task $($t.id)"
                    } catch {}
                }
            }
            'S' {
                # Timer actions (persistent)
                if ($this.currentView -eq 'timerstart') {
                    try {
                        Start-PmcTimer
                        $this.DisplayResult(@{ Type='success'; Message='Timer started' })
                    } catch { $this.DisplayResult(@{ Type='error'; Message=("Failed to start timer: {0}" -f $_) }) }
                } elseif ($this.currentView -eq 'timerstop') {
                    try {
                        $res = Stop-PmcTimer
                        $msg = if ($res -and $res.Elapsed) { "Timer stopped. Elapsed: $($res.Elapsed) hours" } else { 'Timer stopped' }
                        $this.DisplayResult(@{ Type='success'; Message=$msg })
                    } catch { $this.DisplayResult(@{ Type='error'; Message=("Failed to stop timer: {0}" -f $_) }) }
                }
            }
            'U' {
                if ($this.currentView -eq 'editundo') {
                    try {
                        $status = Get-PmcUndoStatus
                        $action = if ($status -and $status.LastAction) { $status.LastAction } else { 'last change' }
                        Invoke-PmcUndo
                        $this.LoadTasks()
                        $this.DisplayResult(@{ Type='success'; Message=("Undone: {0}" -f $action) })
                    } catch { $this.DisplayResult(@{ Type='error'; Message=("Failed to undo: {0}" -f $_) }) }
                }
            }
            'R' {
                if ($this.currentView -eq 'editredo') {
                    try {
                        Invoke-PmcRedo
                        $this.LoadTasks()
                        $this.DisplayResult(@{ Type='success'; Message='Redone last change' })
                    } catch { $this.DisplayResult(@{ Type='error'; Message=("Failed to redo: {0}" -f $_) }) }
                }
            }
            'C' {
                if ($this.currentView -eq 'focusclear') {
                    try { Clear-PmcFocus; $this.DisplayResult(@{ Type='success'; Message='Focus cleared' }) } catch { $this.DisplayResult(@{ Type='error'; Message=("Failed to clear focus: {0}" -f $_) }) }
                }
            }
            'F10' {
                $action = $this.menuSystem.HandleInput()
                if ($action) {
                    $this.ProcessMenuAction($action)
                    if ($this.currentView -notin @('todayview','tomorrowview','weekview','monthview','overdueview','upcomingview','blockedview','noduedateview','nextactionsview','kanbanview','agendaview','timereport','timerstatus','timerstart','timerstop','editundo','editredo','focusclear')) {
                        return
                    }
                }
            }
            default {}
        }
    }
    $this.currentView = 'tasklist'
}

# Initialize performance systems
[PmcStringCache]::Initialize()

# Helper functions
function Get-PmcTerminal { return [PmcSimpleTerminal]::GetInstance() }
function Get-PmcSpaces([int]$count) { return [PmcStringCache]::GetSpaces($count) }
function Get-PmcStringBuilder([int]$capacity = 256) { return [PmcStringBuilderPool]::Get($capacity) }
function Return-PmcStringBuilder([StringBuilder]$sb) { [PmcStringBuilderPool]::Return($sb) }

# Main entry point
function Start-PmcConsoleUI {
    try {
        Write-Host "Starting PMC ConsoleUI..." -ForegroundColor Green
        $app = [PmcConsoleUIApp]::new()
        $app.Initialize()
        # Exit gracefully in headless/non-interactive environments (CI/sandbox)
        if (-not (Test-ConsoleInteractive)) {
            try { $app.DrawLayout() } catch {}
            $app.Shutdown()
            Write-Host "PMC ConsoleUI exited (non-interactive terminal detected)." -ForegroundColor Yellow
            return
        }
        $app.Run()
        $app.Shutdown()
        Write-Host "PMC ConsoleUI exited." -ForegroundColor Green
    } catch {
        Write-Host "Failed to start PMC ConsoleUI: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

# Function will be exported by the main module
