@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'SuperTUI.psm1'

    # Version number of this module.
    ModuleVersion = '0.1.0'

    # ID used to uniquely identify this module
    GUID = 'a3f4d2e1-8c9b-4a7e-9d2f-1b8c4e6a9f3d'

    # Author of this module
    Author = 'SuperTUI Team'

    # Company or vendor of this module
    CompanyName = 'SuperTUI'

    # Copyright statement for this module
    Copyright = '(c) 2025 SuperTUI. All rights reserved. MIT License.'

    # Description of the functionality provided by this module
    Description = 'PowerShell module for creating beautiful terminal-style desktop UIs with workspaces, widgets, and layouts.'

    # Minimum version of PowerShell required
    PowerShellVersion = '5.1'

    # .NET Framework version required
    DotNetFrameworkVersion = '4.7.2'

    # Assemblies that must be loaded prior to importing this module
    RequiredAssemblies = @(
        'PresentationFramework',
        'PresentationCore',
        'WindowsBase',
        'System.Xaml'
    )

    # Functions to export from this module
    FunctionsToExport = @(
        # Core functions
        'Start-SuperTUI',
        'Initialize-SuperTUI',

        # Workspace functions
        'New-SuperTUIWorkspace',
        'Add-SuperTUIWorkspace',
        'Remove-SuperTUIWorkspace',
        'Switch-SuperTUIWorkspace',

        # Layout functions
        'Use-GridLayout',
        'Use-DockLayout',
        'Use-StackLayout',

        # Widget functions
        'Add-ClockWidget',
        'Add-CounterWidget',
        'Add-NotesWidget',
        'Add-TaskSummaryWidget',
        'Add-SystemMonitorWidget',
        'Add-GitStatusWidget',
        'Add-TodoWidget',
        'Add-FileExplorerWidget',
        'Add-TerminalWidget',
        'Add-CommandPaletteWidget',

        # Configuration
        'Get-SuperTUIConfig',
        'Set-SuperTUIConfig',

        # Theme
        'Get-SuperTUITheme',
        'Set-SuperTUITheme',
        'Import-SuperTUITheme',
        'Export-SuperTUITheme',

        # Workspace Templates
        'Get-SuperTUITemplate',
        'Save-SuperTUITemplate',
        'Remove-SuperTUITemplate',
        'Export-SuperTUITemplate',
        'Import-SuperTUITemplate',
        'Initialize-SuperTUIBuiltInTemplates',

        # Utility
        'Show-SuperTUIWindow',
        'Hide-SuperTUIWindow',
        'Get-SuperTUIStatistics',

        # Hot Reload
        'Enable-SuperTUIHotReload',
        'Disable-SuperTUIHotReload',
        'Get-SuperTUIHotReloadStats'
    )

    # Cmdlets to export from this module
    CmdletsToExport = @()

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module
    AliasesToExport = @(
        'stui',
        'New-Workspace',
        'Add-Widget'
    )

    # Private data to pass to the module specified in RootModule/ModuleToProcess
    PrivateData = @{
        PSData = @{
            # Tags applied to this module
            Tags = @('UI', 'Terminal', 'TUI', 'Workspace', 'Widget', 'Desktop', 'WPF', 'PowerShell')

            # A URL to the license for this module.
            LicenseUri = 'https://github.com/yourusername/supertui/blob/main/LICENSE'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/yourusername/supertui'

            # Release notes
            ReleaseNotes = 'Initial release of SuperTUI PowerShell module'
        }
    }
}
