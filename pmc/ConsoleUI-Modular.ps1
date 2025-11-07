# ConsoleUI Modular Loader
# This file loads the main ConsoleUI and extends it with additional handlers from modular files

# Require PowerShell 7+ for null-coalescing operator (??) used across core/deps
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "ConsoleUI (modular) requires PowerShell 7+. Detected: $($PSVersionTable.PSVersion)" -ForegroundColor Red
    return
}

# Load debug system first
. "$PSScriptRoot/Debug.ps1"
Write-ConsoleUIDebug "Loading ConsoleUI modular system" "LOADER"

# Load main ConsoleUI only if not already loaded (avoid class redefinition errors)
if (-not ('PmcConsoleUIApp' -as [type])) {
    try {
        . "$PSScriptRoot/ConsoleUI.Core.ps1"
        Write-ConsoleUIDebug "ConsoleUI core loaded" "LOADER"
    } catch {
        Write-ConsoleUIDebug "ConsoleUI core load failed: $($_.Exception.Message)" "LOADER"
        throw
    }
} else {
    Write-ConsoleUIDebug "ConsoleUI already loaded in session; skipping reload" "LOADER"
}

# Load handler modules
. "$PSScriptRoot/Handlers/TaskHandlers.ps1"
Write-ConsoleUIDebug "TaskHandlers.ps1 loaded" "LOADER"
. "$PSScriptRoot/Handlers/ProjectHandlers.ps1"
Write-ConsoleUIDebug "ProjectHandlers.ps1 loaded" "LOADER"

# Extend PmcConsoleUIApp class with additional action handlers
# This uses PowerShell's ability to add methods to existing instances

# Add helper method to wire up all extended handlers
Update-TypeData -TypeName PmcConsoleUIApp -MemberType ScriptMethod -MemberName 'ProcessExtendedActions' -Value {
    param([string]$action)

    # Task handlers
    if ($action -eq 'task:copy') {
        Invoke-TaskCopyHandler -app $this
        return $true
    } elseif ($action -eq 'task:move') {
        Invoke-TaskMoveHandler -app $this
        return $true
    } elseif ($action -eq 'task:find') {
        Invoke-TaskFindHandler -app $this
        return $true
    } elseif ($action -eq 'task:priority') {
        Invoke-TaskPriorityHandler -app $this
        return $true
    } elseif ($action -eq 'task:postpone') {
        Invoke-TaskPostponeHandler -app $this
        return $true
    } elseif ($action -eq 'task:note') {
        Invoke-TaskNoteHandler -app $this
        return $true
    }

    # Project handlers
    elseif ($action -eq 'project:edit') {
        $this.previousView = $this.currentView
        Invoke-ProjectEditHandler -app $this
        return $true
    } elseif ($action -eq 'project:info') {
        $this.previousView = $this.currentView
        Invoke-ProjectInfoHandler -app $this
        return $true
    } elseif ($action -eq 'project:recent') {
        $this.previousView = $this.currentView
        Invoke-RecentProjectsHandler -app $this
        return $true
    }

    # View handlers (stubs for now - can be implemented in separate files)
    elseif ($action -eq 'view:agenda') {
        $this.previousView = $this.currentView
        Show-PmcAgendaInteractive
        [Console]::ReadKey($true) | Out-Null
        $this.GoBackOr('main')
        return $true
    } elseif ($action -eq 'view:all') {
        $this.previousView = $this.currentView
        Show-PmcAllTasksInteractive
        [Console]::ReadKey($true) | Out-Null
        $this.GoBackOr('main')
        return $true
    }

    # Tool handlers (stubs)
    elseif ($action -eq 'tools:preferences') {
        $this.previousView = $this.currentView
        try {
            $ctx = New-Object PmcCommandContext('tools','preferences')
            Show-PmcPreferences -Context $ctx
        } catch {
            Show-InfoMessage -Message "Preferences error: $_" -Title "Error" -Color "Red"
        }
        $this.GoBackOr('main')
        return $true
    } elseif ($action -eq 'tools:applytheme') {
        # Picker to apply theme quickly
        $choices = @('Enter Hex...','default (#33aaff)','ocean','lime','purple','slate','matrix','amber','synthwave','high-contrast')
        $sel = Show-SelectList -Title "Select Theme" -Options $choices -DefaultValue 'default (#33aaff)'
        if (-not $sel) { $this.GoBackOr('main'); return $true }
        $arg = ''
        if ($sel -eq 'Enter Hex...') {
            $res = Show-InputForm -Title "Enter Theme Color" -Fields @(@{Name='hex'; Label='#RRGGBB'; Required=$true})
            if ($res -and $res['hex']) { $arg = [string]$res['hex'] } else { $this.GoBackOr('main'); return $true }
        } elseif ($sel -like 'default*') { $arg = 'default' } else { $arg = $sel }
        try { $ctx = New-Object PmcCommandContext 'theme','apply'; $ctx.FreeText=@($arg); Apply-PmcTheme -Context $ctx; Initialize-PmcThemeSystem; Show-InfoMessage -Message ("Theme applied: {0}" -f $arg) -Title "Success" -Color "Green" } catch { Show-InfoMessage -Message ("Error applying theme: {0}" -f $_) -Title "Error" -Color "Red" }
        $this.GoBackOr('main'); return $true
    } elseif ($action -eq 'tools:themeedit') {
        # Slider editor
        try { $ctx = New-Object PmcCommandContext 'theme','edit'; Edit-PmcTheme -Context $ctx; Initialize-PmcThemeSystem; Show-InfoMessage -Message 'Theme updated' -Title 'Theme' -Color 'Green' } catch { Show-InfoMessage -Message ("Theme editor error: {0}" -f $_) -Title 'Theme' -Color 'Red' }
        $this.GoBackOr('main'); return $true
    }

    # Help handlers
    elseif ($action -eq 'help:browser') {
        $this.previousView = $this.currentView
        Show-PmcHelpCategories
        [Console]::ReadKey($true) | Out-Null
        $this.GoBackOr('main')
        return $true
    } elseif ($action -eq 'help:categories') {
        $this.previousView = $this.currentView
        Show-PmcHelpCategories
        [Console]::ReadKey($true) | Out-Null
        $this.GoBackOr('main')
        return $true
    } elseif ($action -eq 'help:search') {
        # Use unified input form
        $fields = @(@{Name='q'; Label='Search term'; Required=$true})
        $res = Show-InputForm -Title "Help Search" -Fields $fields
        if ($res -and $res['q']) { Show-PmcHelpSearch -SearchTerm ([string]$res['q']); [Console]::ReadKey($true) | Out-Null }
        $this.GoBackOr('main')
        return $true
    }

    # Not handled
    return $false
} -Force

Write-Host "ConsoleUI modular extensions loaded successfully" -ForegroundColor Green
