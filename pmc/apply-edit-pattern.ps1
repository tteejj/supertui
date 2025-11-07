# Apply inline edit pattern to all view screens
# This adds the same inline edit functionality to all task view screens

$screensToUpdate = @(
    @{File='MonthViewScreen.ps1'; TaskArray='SelectableTasks'},
    @{File='AgendaViewScreen.ps1'; TaskArray='SelectableTasks'}
)

$baseDir = "/home/teej/pmc/module/Pmc.Strict/consoleui/screens"

# Template for new methods to add
function Get-EditMethodsTemplate {
    param([string]$TaskArrayName)

    return @"

    hidden [bool] _HandleInputMode([ConsoleKeyInfo]`$keyInfo) {
        switch (`$keyInfo.Key) {
            'Enter' {
                `$this._SubmitInput()
                return `$true
            }
            'Escape' {
                `$this._CancelInput()
                return `$true
            }
            'Tab' {
                if (`$this.InputMode -eq 'edit-field') {
                    `$this._CycleField()
                    return `$true
                }
            }
            'Backspace' {
                if (`$this.InputBuffer.Length -gt 0) {
                    `$this.InputBuffer = `$this.InputBuffer.Substring(0, `$this.InputBuffer.Length - 1)
                    return `$true
                }
            }
            default {
                # Add character to buffer
                if (`$keyInfo.KeyChar -and -not [char]::IsControl(`$keyInfo.KeyChar)) {
                    `$this.InputBuffer += `$keyInfo.KeyChar
                    return `$true
                }
            }
        }
        return `$false
    }

    hidden [void] _StartEditField() {
        if (`$this.SelectedIndex -lt 0 -or `$this.SelectedIndex -ge `$this.$TaskArrayName.Count) {
            return
        }

        `$task = `$this.$TaskArrayName[`$this.SelectedIndex]
        `$this.InputMode = "edit-field"
        `$this.EditFieldIndex = 0
        `$this.EditField = `$this.EditableFields[`$this.EditFieldIndex]

        # Pre-fill with current value
        `$currentValue = `$task.(`$this.EditField)
        if (`$currentValue) {
            `$this.InputBuffer = [string]`$currentValue
        } else {
            `$this.InputBuffer = ""
        }

        `$this.ShowStatus("Editing task #`$(`$task.id) - Press Tab to cycle fields")
    }

    hidden [void] _SubmitInput() {
        try {
            if (`$this.InputMode -eq 'edit-field') {
                # Update current field value
                `$this._UpdateField(`$this.EditField, `$this.InputBuffer)
                # Exit edit mode
                `$this.InputMode = ""
                `$this.InputBuffer = ""
                `$this.EditField = ""
                `$this.EditFieldIndex = 0
            }
        } catch {
            `$this.ShowError("Operation failed: `$_")
            `$this.InputMode = ""
            `$this.InputBuffer = ""
            `$this.EditField = ""
            `$this.EditFieldIndex = 0
        }
    }

    hidden [void] _CancelInput() {
        `$this.InputMode = ""
        `$this.InputBuffer = ""
        `$this.EditField = ""
        `$this.EditFieldIndex = 0
        `$this.ShowStatus("Cancelled")
    }

    hidden [void] _CycleField() {
        if (`$this.InputMode -eq 'edit-field') {
            if (`$this.SelectedIndex -lt 0 -or `$this.SelectedIndex -ge `$this.$TaskArrayName.Count) {
                return
            }

            `$task = `$this.$TaskArrayName[`$this.SelectedIndex]

            # Save current field value before switching
            try {
                if (`$this.InputBuffer) {
                    `$this._UpdateField(`$this.EditField, `$this.InputBuffer)
                }
            } catch {
                # If update fails, show error but continue to next field
                `$this.ShowError("Invalid value for `$(`$this.EditField): `$_")
            }

            # Move to next field
            `$this.EditFieldIndex = (`$this.EditFieldIndex + 1) % `$this.EditableFields.Count
            `$this.EditField = `$this.EditableFields[`$this.EditFieldIndex]

            # Load new field value
            `$currentValue = `$task.(`$this.EditField)
            if (`$currentValue) {
                `$this.InputBuffer = [string]`$currentValue
            } else {
                `$this.InputBuffer = ""
            }

            `$this.ShowStatus("Now editing: `$(`$this.EditField) - Press Tab for next field, Enter to finish")
        }
    }

    hidden [void] _UpdateField([string]`$field, [string]`$value) {
        if (`$this.SelectedIndex -lt 0 -or `$this.SelectedIndex -ge `$this.$TaskArrayName.Count) {
            return
        }

        `$task = `$this.$TaskArrayName[`$this.SelectedIndex]

        try {
            # Use existing FieldSchema to normalize and validate
            `$schema = Get-PmcFieldSchema -Domain 'task' -Field `$field

            if (-not `$schema) {
                `$this.ShowError("Unknown field: `$field")
                return
            }

            # Normalize the value using existing schema logic
            `$normalizedValue = `$value
            if (`$schema.Normalize) {
                `$normalizedValue = & `$schema.Normalize `$value
            }

            # Validate using existing schema logic
            if (`$schema.Validate) {
                `$isValid = & `$schema.Validate `$normalizedValue
                if (-not `$isValid) {
                    `$this.ShowError("Invalid value for `$field")
                    return
                }
            }

            # Update in-memory task
            `$task.`$field = `$normalizedValue

            # Update storage
            `$allData = Get-PmcAllData
            `$taskToUpdate = `$allData.tasks | Where-Object { `$_.id -eq `$task.id }

            if (`$taskToUpdate) {
                `$taskToUpdate.`$field = `$normalizedValue
                Set-PmcAllData `$allData
            }

            # Show formatted value in success message
            `$displayValue = `$normalizedValue
            if (`$schema.DisplayFormat) {
                `$displayValue = & `$schema.DisplayFormat `$normalizedValue
            }

            `$this.ShowSuccess("Task #`$(`$task.id) `$field = `$displayValue")

        } catch {
            `$this.ShowError("Error updating `$field``: `$_")
        }
    }
"@
}

foreach ($screen in $screensToUpdate) {
    $filePath = Join-Path $baseDir $screen.File
    $taskArrayName = $screen.TaskArray

    Write-Host "Processing $($screen.File)..."

    $content = Get-Content $filePath -Raw

    # 1. Add properties after SelectedIndex
    $propertyAddition = @"
    [string]`$InputBuffer = ""
    [string]`$InputMode = ""  # "", "edit-field"
    [string]`$EditField = ""  # Which field being edited (due, priority, text)
    [array]`$EditableFields = @('priority', 'due', 'text')
    [int]`$EditFieldIndex = 0
"@

    $content = $content -replace "(\[int\]\`$SelectedIndex = 0)", "`$1`n    $propertyAddition"

    # 2. Update HandleInput to check for InputMode first
    $content = $content -replace "(\[bool\] HandleInput\(\[ConsoleKeyInfo\]\`$keyInfo\) \{[\r\n\s]+if \(\`$this\.$taskArrayName\.Count -eq 0\) \{)", @"
[bool] HandleInput([ConsoleKeyInfo]`$keyInfo) {
        # Input mode handling
        if (`$this.InputMode) {
            return `$this._HandleInputMode(`$keyInfo)
        }

        # Normal mode handling
        if (`$this.$taskArrayName.Count -eq 0) {
"@

    # 3. Change 'E' key to call _StartEditField instead of _EditTask
    $content = $content -replace "'E' \{[\r\n\s]+\`$this\._EditTask\(\)", @"
'E' {
                `$this._StartEditField()
"@

    # 4. Remove stub _EditTask and _ShowTaskDetail methods and add new implementation
    # Find and remove these stubs
    $content = $content -replace "hidden \[void\] _ShowTaskDetail\(\) \{[^\}]+\}[\r\n\s]+", ""
    $content = $content -replace "hidden \[void\] _EditTask\(\) \{[^\}]+\}[\r\n\s]+", ""

    # Add new methods before _CompleteTask
    $newMethods = Get-EditMethodsTemplate $taskArrayName
    $content = $content -replace "([\r\n\s]+hidden \[void\] _CompleteTask\(\))", "$newMethods`n`$1"

    # Write back
    Set-Content -Path $filePath -Value $content -NoNewline

    Write-Host "  Updated $($screen.File)"
}

Write-Host "`nDone! Updated all view screens with inline edit functionality."
