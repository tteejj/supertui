# Script to add inline edit functionality to view screens
# Usage: ./add-inline-edit.ps1 <screen-file> <task-array-name>

param(
    [Parameter(Mandatory=$true)]
    [string]$ScreenFile,

    [Parameter(Mandatory=$true)]
    [string]$TaskArrayName
)

$content = Get-Content $ScreenFile -Raw

# 1. Add properties after task array declaration
$propertyPattern = "(\[array\]\`$$TaskArrayName = @\(\)\s+\[int\]\`$SelectedIndex = 0)"
$propertyReplacement = @"
`$1
    [string]`$InputBuffer = ""
    [string]`$InputMode = ""  # "", "edit-field"
    [string]`$EditField = ""  # Which field being edited (due, priority, text)
    [array]`$EditableFields = @('priority', 'due', 'text')
    [int]`$EditFieldIndex = 0
"@

$content = $content -replace $propertyPattern, $propertyReplacement

# 2. Update footer shortcuts
$footerPattern = '(\$this\.Footer\.AddShortcut\("Up/Down", "Select"\)\s+\$this\.Footer\.AddShortcut\("Enter", "Detail"\)\s+\$this\.Footer\.AddShortcut\("E", "Edit"\))'
$footerReplacement = @'
$this.Footer.AddShortcut("Up/Down", "Select")
        $this.Footer.AddShortcut("E", "Edit")
        $this.Footer.AddShortcut("Tab", "Next Field")
'@

$content = $content -replace $footerPattern, $footerReplacement

# 3. Add input mode handling to HandleInput
# Find the HandleInput method and modify it
$handleInputPattern = '(\[bool\] HandleInput\(\[ConsoleKeyInfo\]\`$keyInfo\) \{\s+if \(\$this\.' + $TaskArrayName + '\.Count -eq 0\) \{\s+return \`$false\s+\}\s+switch)'
$handleInputReplacement = @"
[bool] HandleInput([ConsoleKeyInfo]`$keyInfo) {
        # Input mode handling
        if (`$this.InputMode) {
            return `$this._HandleInputMode(`$keyInfo)
        }

        # Normal mode handling
        if (`$this.$TaskArrayName.Count -eq 0) {
            return `$false
        }

        switch
"@

$content = $content -replace $handleInputPattern, $handleInputReplacement

# 4. Change 'E' key handler and remove Enter/ShowTaskDetail
$content = $content -replace "'E' \{\s+\`$this\._EditTask\(\)", "'E' {`n                `$this._StartEditField()"

# 5. Remove _ShowTaskDetail and _EditTask stub methods and replace with full implementation
$stubMethodsPattern = 'hidden \[void\] _ShowTaskDetail\(\) \{[^}]+\}\s+hidden \[void\] _EditTask\(\) \{[^}]+\}'

$newMethods = @"
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
            `$this.ShowError("Error updating `$field`: `$_")
        }
    }
"@

$content = $content -replace $stubMethodsPattern, $newMethods

# Write back
Set-Content -Path $ScreenFile -Value $content

Write-Host "Updated $ScreenFile with inline edit functionality"
