# Dashboard Configuration System
# Loads/saves dashboard layouts and widget configurations

class DashboardConfig {
    [string]$Name
    [hashtable]$Layout
    [hashtable]$WidgetSettings = @{}

    static [string]$ConfigDir = (Join-Path ([Environment]::GetFolderPath('UserProfile')) ".supertui" "layouts")

    # Ensure config directory exists
    static [void] EnsureConfigDir() {
        if (-not (Test-Path ([DashboardConfig]::ConfigDir))) {
            New-Item -ItemType Directory -Path ([DashboardConfig]::ConfigDir) -Force | Out-Null
            Write-Verbose "Created config directory: $([DashboardConfig]::ConfigDir)"
        }
    }

    # Load configuration from file
    static [DashboardConfig] Load([string]$name) {
        [DashboardConfig]::EnsureConfigDir()

        $configPath = Join-Path ([DashboardConfig]::ConfigDir) "$name.json"

        if (-not (Test-Path $configPath)) {
            throw "Configuration '$name' not found at: $configPath"
        }

        try {
            $json = Get-Content $configPath -Raw | ConvertFrom-Json

            $config = [DashboardConfig]::new()
            $config.Name = $json.name
            $config.Layout = @{
                rows = $json.layout.rows
                columns = $json.layout.columns
                widgets = @($json.layout.widgets | ForEach-Object {
                    @{
                        id = $_.id
                        type = $_.type
                        position = @{
                            row = $_.position.row
                            col = $_.position.col
                            rowSpan = $_.position.rowSpan ?? 1
                            colSpan = $_.position.colSpan ?? 1
                        }
                        settings = if ($_.settings) {
                            # Convert PSCustomObject to hashtable
                            $ht = @{}
                            $_.settings.PSObject.Properties | ForEach-Object {
                                $ht[$_.Name] = $_.Value
                            }
                            $ht
                        } else {
                            @{}
                        }
                    }
                })
            }

            # Widget-specific settings
            if ($json.widgetSettings) {
                $json.widgetSettings.PSObject.Properties | ForEach-Object {
                    $config.WidgetSettings[$_.Name] = $_.Value
                }
            }

            Write-Verbose "Loaded configuration: $name"
            return $config

        } catch {
            throw "Failed to load configuration '$name': $_"
        }
    }

    # Save configuration to file
    [void] Save() {
        [DashboardConfig]::EnsureConfigDir()

        $configPath = Join-Path ([DashboardConfig]::ConfigDir) "$($this.Name).json"

        try {
            $json = @{
                name = $this.Name
                layout = $this.Layout
                widgetSettings = $this.WidgetSettings
            } | ConvertTo-Json -Depth 10

            Set-Content -Path $configPath -Value $json

            Write-Host "Configuration saved: $configPath" -ForegroundColor Green

        } catch {
            throw "Failed to save configuration '$($this.Name)': $_"
        }
    }

    # Get list of available configurations
    static [string[]] GetAvailableConfigs() {
        [DashboardConfig]::EnsureConfigDir()

        $configs = Get-ChildItem -Path ([DashboardConfig]::ConfigDir) -Filter "*.json" -File |
            ForEach-Object { $_.BaseName }

        return $configs
    }

    # Create default configuration if none exist
    static [void] CreateDefaultConfig() {
        [DashboardConfig]::EnsureConfigDir()

        $defaultPath = Join-Path ([DashboardConfig]::ConfigDir) "default.json"

        if (Test-Path $defaultPath) {
            Write-Verbose "Default configuration already exists"
            return
        }

        $defaultConfig = @{
            name = "default"
            layout = @{
                rows = @("10", "*", "*", "*")
                columns = @("*")
                widgets = @(
                    @{
                        id = "stats"
                        type = "TaskStatsWidget"
                        position = @{ row = 0; col = 0 }
                        settings = @{}
                    }
                    @{
                        id = "week"
                        type = "WeekViewWidget"
                        position = @{ row = 1; col = 0 }
                        settings = @{ viewportHeight = 7 }
                    }
                    @{
                        id = "tasks"
                        type = "TodayTasksWidget"
                        position = @{ row = 2; col = 0 }
                        settings = @{ viewportHeight = 5 }
                    }
                    @{
                        id = "menu"
                        type = "MenuWidget"
                        position = @{ row = 3; col = 0 }
                        settings = @{ viewportHeight = 5 }
                    }
                )
            }
            widgetSettings = @{
                tasks = @{
                    showCompleted = $false
                }
                menu = @{
                    compactMode = $false
                }
            }
        }

        $json = $defaultConfig | ConvertTo-Json -Depth 10
        Set-Content -Path $defaultPath -Value $json

        Write-Host "Created default configuration: $defaultPath" -ForegroundColor Green
    }
}

# Helper functions

function Get-DashboardConfig {
    <#
    .SYNOPSIS
    Loads a dashboard configuration

    .PARAMETER Name
    Configuration name (without .json extension)

    .EXAMPLE
    $config = Get-DashboardConfig -Name "default"
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    return [DashboardConfig]::Load($Name)
}

function Get-AvailableDashboardConfigs {
    <#
    .SYNOPSIS
    Gets list of available dashboard configurations

    .EXAMPLE
    Get-AvailableDashboardConfigs
    #>
    return [DashboardConfig]::GetAvailableConfigs()
}

function New-DefaultDashboardConfig {
    <#
    .SYNOPSIS
    Creates the default dashboard configuration if it doesn't exist

    .EXAMPLE
    New-DefaultDashboardConfig
    #>
    [DashboardConfig]::CreateDefaultConfig()
}
