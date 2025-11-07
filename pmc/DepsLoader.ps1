# ConsoleUI DepsLoader - loads local dependency copies only

param()

$deps = Join-Path $PSScriptRoot 'deps'

# Neutralize Export-ModuleMember calls in copied files
function Export-ModuleMember { param([Parameter(ValueFromRemainingArguments=$true)]$args) }

# Load core primitives and rendering helpers first (ensure type order)
. (Join-Path $deps 'PraxisVT.ps1')
. (Join-Path $deps 'PraxisStringBuilder.ps1')
. (Join-Path $deps 'TerminalDimensions.ps1')
. (Join-Path $deps 'PraxisFrameRenderer.ps1')

# Core types/config/state/security/ui/storage/time
. (Join-Path $deps 'Types.ps1')
. (Join-Path $deps 'Config.ps1')
. (Join-Path $deps 'Debug.ps1')
. (Join-Path $deps 'Security.ps1')
. (Join-Path $deps 'State.ps1')
. (Join-Path $deps 'UI.ps1')
. (Join-Path $deps 'Storage.ps1')
. (Join-Path $deps 'Time.ps1')

# Schema support (for grid/columns)
. (Join-Path $deps 'FieldSchemas.ps1')

# Local PmcTemplate class before template system
. (Join-Path $PSScriptRoot 'deps/PmcTemplate.ps1')
. (Join-Path $deps 'TemplateDisplay.ps1')

# Display systems
. (Join-Path $deps 'DataDisplay.ps1')
. (Join-Path $deps 'UniversalDisplay.ps1')

# Help content and UI (curated content for standalone)
. (Join-Path $PSScriptRoot 'deps/HelpContent.ps1')
. (Join-Path $deps 'HelpUI.ps1')

# Analytics + Theme
. (Join-Path $deps 'Analytics.ps1')
. (Join-Path $deps 'Theme.ps1')

# Excel integration (optional, will load if Excel is available)
try {
    . (Join-Path $deps 'Excel.ps1')
    # Initialize field mappings from disk or defaults
    Initialize-ExcelT2020Mappings
} catch {
    Write-Host "Excel integration not available (Excel COM not installed)" -ForegroundColor Yellow
}

Write-Host "ConsoleUI deps loaded (standalone)" -ForegroundColor Green
