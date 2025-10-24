# SuperTUI Test Fixes Demo
# This demo includes ALL widgets and logging to test the recent fixes
#
# Tests:
# - Fix #5: Settings Widget validation enforcement
# - Fix #6: Shortcut Help Widget dynamic shortcuts
# - Fix #7: ShortcutManager thread-safety (implicit - used by shortcuts)
# - Fix #8: State migration (run Test_StateMigration.ps1 separately)

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  SuperTUI - Test Fixes Demo" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Set up portable data directory
$dataDir = Join-Path $PSScriptRoot ".data"
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

# Set up logging in local directory
$logFile = Join-Path $dataDir "supertui_test_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
Write-Host "Data directory: $dataDir" -ForegroundColor Yellow
Write-Host "Log file: $logFile`n" -ForegroundColor Yellow

# Compile C# framework and widgets
Write-Host "[1/4] Compiling framework..." -ForegroundColor Yellow

# Load all core framework files
$coreFiles = @(
    "Core/Interfaces/IWidget.cs"
    "Core/Interfaces/ILogger.cs"
    "Core/Interfaces/IThemeManager.cs"
    "Core/Interfaces/IThemeable.cs"
    "Core/Interfaces/IConfigurationManager.cs"
    "Core/Interfaces/ISecurityManager.cs"
    "Core/Interfaces/IErrorHandler.cs"
    "Core/Interfaces/ILayoutEngine.cs"
    "Core/Interfaces/IServiceContainer.cs"
    "Core/Interfaces/IWorkspace.cs"
    "Core/Interfaces/IWorkspaceManager.cs"
    "Core/Interfaces/IEventBus.cs"
    "Core/Infrastructure/Logger.cs"
    "Core/Infrastructure/ConfigurationManager.cs"
    "Core/Infrastructure/ThemeManager.cs"
    "Core/Infrastructure/SecurityManager.cs"
    "Core/Infrastructure/ErrorHandler.cs"
    "Core/Infrastructure/Events.cs"
    "Core/Extensions.cs"
    "Core/Layout/LayoutEngine.cs"
    "Core/Layout/GridLayoutEngine.cs"
    "Core/Layout/DockLayoutEngine.cs"
    "Core/Layout/StackLayoutEngine.cs"
    "Core/Components/WidgetBase.cs"
    "Core/Components/ScreenBase.cs"
    "Core/Components/ErrorBoundary.cs"
    "Core/Components/EditableListControl.cs"
    "Core/Infrastructure/Workspace.cs"
    "Core/Infrastructure/WorkspaceManager.cs"
    "Core/Infrastructure/ShortcutManager.cs"
    "Core/Infrastructure/EventBus.cs"
    "Core/Infrastructure/ServiceContainer.cs"
)

# Load ALL widget files (including new ones)
$widgetFiles = @(
    "Widgets/ClockWidget.cs"
    "Widgets/TaskSummaryWidget.cs"
    "Widgets/CounterWidget.cs"
    "Widgets/NotesWidget.cs"
    "Widgets/SettingsWidget.cs"        # FIX #5
    "Widgets/ShortcutHelpWidget.cs"    # FIX #6
    "Widgets/SystemMonitorWidget.cs"
    "Widgets/GitStatusWidget.cs"
    "Widgets/FileExplorerWidget.cs"
    "Widgets/TerminalWidget.cs"
    "Widgets/TodoWidget.cs"
    "Widgets/CommandPaletteWidget.cs"
)

# Combine all source files
$allSources = @()
foreach ($file in ($coreFiles + $widgetFiles)) {
    $fullPath = Join-Path $PSScriptRoot $file
    if (Test-Path $fullPath) {
        $source = Get-Content $fullPath -Raw
        # Remove using statements except from first file to avoid duplicates
        if ($allSources.Count -gt 0) {
            $source = $source -replace '(?s)^using.*?(?=namespace)', ''
        }
        $allSources += $source
    } else {
        Write-Warning "File not found: $fullPath"
    }
}

$combinedSource = $allSources -join "`n`n"

try {
    Add-Type -TypeDefinition $combinedSource -ReferencedAssemblies @(
        'PresentationFramework',
        'PresentationCore',
        'WindowsBase',
        'System.Xaml'
    )
    Write-Host "✓ Compilation successful" -ForegroundColor Green
} catch {
    Write-Host "✗ Compilation failed:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "[2/4] Initializing infrastructure..." -ForegroundColor Yellow

# Set portable data directory for C# code
[SuperTUI.Extensions.PortableDataDirectory]::DataDirectory = $dataDir

# Initialize Logger
$logger = [SuperTUI.Infrastructure.Logger]::Instance
$logger.Initialize($logFile)
$logger.Info("Demo", "=== SuperTUI Test Fixes Demo Started ===")
$logger.Info("Demo", "Data directory: $dataDir")
$logger.Info("Demo", "Log file: $logFile")

Write-Host "✓ Logger initialized" -ForegroundColor Green

# Initialize Configuration in local directory
$configFile = Join-Path $dataDir "config.json"
$config = [SuperTUI.Infrastructure.ConfigurationManager]::Instance
$config.Initialize($configFile)

# Add some test configuration values with validators
$config.Set("Performance", "MaxFPS", 60, [int], { param($v) $v -ge 1 -and $v -le 144 })
$config.Set("Performance", "MaxThreads", 4, [int], { param($v) $v -ge 1 -and $v -le 32 })
$config.Set("UI", "Theme", "Dark", [string], { param($v) $v -in @("Dark", "Light", "Custom") })
$config.Set("UI", "FontSize", 12, [int], { param($v) $v -ge 8 -and $v -le 32 })
$config.Save()

Write-Host "✓ Configuration initialized" -ForegroundColor Green

# Initialize Theme Manager
$themeManager = [SuperTUI.Infrastructure.ThemeManager]::Instance
$theme = $themeManager.CurrentTheme

Write-Host "✓ Theme manager initialized" -ForegroundColor Green

Write-Host "[3/4] Creating main window..." -ForegroundColor Yellow

# Create main window XAML (same as original demo)
[xml]$xaml = @"
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="SuperTUI - Test Fixes"
    Width="1600"
    Height="1000"
    WindowStyle="None"
    ResizeMode="CanResizeWithGrip"
    Background="#0C0C0C"
    WindowStartupLocation="CenterScreen">

    <Window.Resources>
        <SolidColorBrush x:Key="TerminalBackground">#0C0C0C</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalForeground">#CCCCCC</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalBorder">#3A3A3A</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalAccent">#4EC9B0</SolidColorBrush>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="40"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="30"/>
        </Grid.RowDefinitions>

        <!-- Title Bar -->
        <Border Grid.Row="0" Background="#1E1E1E" BorderBrush="#3A3A3A" BorderThickness="0,0,0,1">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <TextBlock x:Name="WorkspaceTitle" Grid.Column="0"
                           Text="SuperTUI - Test Fixes"
                           FontFamily="Consolas" FontSize="16" FontWeight="Bold"
                           Foreground="#4EC9B0" VerticalAlignment="Center"
                           Margin="15,0,0,0"/>

                <StackPanel Grid.Column="1" Orientation="Horizontal" Margin="0,0,5,0">
                    <Button x:Name="MinimizeButton" Content="_" Width="40" Height="30"
                            Background="Transparent" Foreground="#CCCCCC" BorderThickness="0"
                            FontFamily="Consolas" FontSize="20" FontWeight="Bold"/>
                    <Button x:Name="MaximizeButton" Content="□" Width="40" Height="30"
                            Background="Transparent" Foreground="#CCCCCC" BorderThickness="0"
                            FontFamily="Consolas" FontSize="16"/>
                    <Button x:Name="CloseButton" Content="✕" Width="40" Height="30"
                            Background="Transparent" Foreground="#CCCCCC" BorderThickness="0"
                            FontFamily="Consolas" FontSize="16"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Content Area -->
        <Border x:Name="ContentBorder" Grid.Row="1" Background="#0C0C0C"/>

        <!-- Status Bar -->
        <Border Grid.Row="2" Background="#1E1E1E" BorderBrush="#3A3A3A" BorderThickness="0,1,0,0">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <TextBlock x:Name="StatusText" Grid.Column="0"
                           Text="Ready"
                           FontFamily="Consolas" FontSize="10"
                           Foreground="#808080" VerticalAlignment="Center"
                           Margin="10,0,0,0"/>

                <TextBlock x:Name="InfoText" Grid.Column="1"
                           FontFamily="Consolas" FontSize="10"
                           Foreground="#4EC9B0" VerticalAlignment="Center"
                           Margin="0,0,10,0">
                    <Run Text="Tab: Focus"/>
                    <Run Text=" | "/>
                    <Run Text="Ctrl+1-3: Workspace"/>
                    <Run Text=" | "/>
                    <Run Text="Ctrl+Q: Quit"/>
                    <Run Text=" | "/>
                    <Run Text="F1: Help"/>
                </TextBlock>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

$reader = New-Object System.Xml.XmlNodeReader($xaml)
$window = [System.Windows.Markup.XamlReader]::Load($reader)

# Get UI elements
$contentBorder = $window.FindName("ContentBorder")
$workspaceTitle = $window.FindName("WorkspaceTitle")
$statusText = $window.FindName("StatusText")
$minimizeButton = $window.FindName("MinimizeButton")
$maximizeButton = $window.FindName("MaximizeButton")
$closeButton = $window.FindName("CloseButton")

# Window controls
$minimizeButton.Add_Click({ $window.WindowState = [System.Windows.WindowState]::Minimized })
$maximizeButton.Add_Click({
    if ($window.WindowState -eq [System.Windows.WindowState]::Maximized) {
        $window.WindowState = [System.Windows.WindowState]::Normal
    } else {
        $window.WindowState = [System.Windows.WindowState]::Maximized
    }
})
$closeButton.Add_Click({ $window.Close() })

# Drag window
$workspaceTitle.Add_MouseLeftButtonDown({ $window.DragMove() })

Write-Host "[4/4] Setting up workspaces..." -ForegroundColor Yellow

# Create workspace manager
$workspaceManager = New-Object SuperTUI.Core.WorkspaceManager($contentBorder)

# Create Workspace 1: Test Fixes
$workspace1 = New-Object SuperTUI.Core.Workspace("Test Fixes", "Grid")
$workspace1.Rows = 2
$workspace1.Columns = 2

# Add ShortcutHelpWidget (FIX #6 - Dynamic shortcuts)
$shortcutHelp = New-Object SuperTUI.Widgets.ShortcutHelpWidget
$shortcutHelpParams = @{
    Row = 0
    Column = 0
    RowSpan = 2
}
$workspace1.AddWidget($shortcutHelp, $shortcutHelpParams)

# Add SettingsWidget (FIX #5 - Validation enforcement)
$settings = New-Object SuperTUI.Widgets.SettingsWidget
$settingsParams = @{
    Row = 0
    Column = 1
}
$workspace1.AddWidget($settings, $settingsParams)

# Add Clock widget for comparison
$clock = New-Object SuperTUI.Widgets.ClockWidget
$clockParams = @{
    Row = 1
    Column = 1
}
$workspace1.AddWidget($clock, $clockParams)

# Create Workspace 2: More Widgets
$workspace2 = New-Object SuperTUI.Core.Workspace("More Widgets", "Grid")
$workspace2.Rows = 2
$workspace2.Columns = 2

# Add Counter
$counter = New-Object SuperTUI.Widgets.CounterWidget
$counterParams = @{ Row = 0; Column = 0 }
$workspace2.AddWidget($counter, $counterParams)

# Add Notes
$notes = New-Object SuperTUI.Widgets.NotesWidget
$notesParams = @{ Row = 0; Column = 1; RowSpan = 2 }
$workspace2.AddWidget($notes, $notesParams)

# Add Task Summary
$taskSummary = New-Object SuperTUI.Widgets.TaskSummaryWidget
$taskSummaryParams = @{ Row = 1; Column = 0 }
$workspace2.AddWidget($taskSummary, $taskSummaryParams)

# Add workspaces
$workspaceManager.AddWorkspace($workspace1)
$workspaceManager.AddWorkspace($workspace2)

# Activate first workspace
$workspaceManager.SwitchToWorkspace(0)
$workspaceTitle.Text = "SuperTUI - $($workspace1.Name)"
$statusText.Text = "Workspace: $($workspace1.Name) | $($workspace1.Widgets.Count) widgets"

Write-Host "✓ Workspaces configured" -ForegroundColor Green

# Register keyboard shortcuts (including Tab for FIX #6 testing)
$shortcutManager = [SuperTUI.Core.ShortcutManager]::Instance

# Register built-in Tab navigation shortcuts (FIX #6 - These should now appear in ShortcutHelpWidget)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Tab,
    [System.Windows.Input.ModifierKeys]::None,
    {}, # Handled by Workspace, no-op action
    "Focus next widget"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Tab,
    [System.Windows.Input.ModifierKeys]::Shift,
    {}, # Handled by Workspace, no-op action
    "Focus previous widget"
)

# Workspace switching (Ctrl+1 through Ctrl+9)
for ($i = 1; $i -le 9; $i++) {
    $index = $i - 1
    $key = [System.Windows.Input.Key]::("D$i")
    $shortcutManager.RegisterGlobal($key, [System.Windows.Input.ModifierKeys]::Control, {
        param($idx)
        if ($idx -lt $workspaceManager.Workspaces.Count) {
            $workspaceManager.SwitchToWorkspace($idx)
            $current = $workspaceManager.CurrentWorkspace
            $workspaceTitle.Text = "SuperTUI - $($current.Name)"
            $statusText.Text = "Workspace: $($current.Name) | $($current.Widgets.Count) widgets"
            $logger.Info("Demo", "Switched to workspace: $($current.Name)")
        }
    }.GetNewClosure(), "Switch to workspace $i")
}

# Quit
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Q,
    [System.Windows.Input.ModifierKeys]::Control,
    { $window.Close() },
    "Quit SuperTUI"
)

# Next/Previous workspace
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Right,
    [System.Windows.Input.ModifierKeys]::Control,
    {
        $workspaceManager.SwitchToNext()
        $current = $workspaceManager.CurrentWorkspace
        $workspaceTitle.Text = "SuperTUI - $($current.Name)"
        $statusText.Text = "Workspace: $($current.Name) | $($current.Widgets.Count) widgets"
        $logger.Info("Demo", "Switched to next workspace")
    },
    "Next workspace"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Left,
    [System.Windows.Input.ModifierKeys]::Control,
    {
        $workspaceManager.SwitchToPrevious()
        $current = $workspaceManager.CurrentWorkspace
        $workspaceTitle.Text = "SuperTUI - $($current.Name)"
        $statusText.Text = "Workspace: $($current.Name) | $($current.Widgets.Count) widgets"
        $logger.Info("Demo", "Switched to previous workspace")
    },
    "Previous workspace"
)

# Cleanup on close
$window.Add_Closed({
    $logger.Info("Demo", "Application closing - cleaning up")
    $workspaceManager.Dispose()
    $logger.Info("Demo", "=== SuperTUI Test Fixes Demo Ended ===")

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  Session Complete" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Log file: $logFile" -ForegroundColor Yellow
    Write-Host "`nTo view log:" -ForegroundColor Yellow
    Write-Host "  notepad `"$logFile`"" -ForegroundColor Gray
    Write-Host ""
})

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  READY TO TEST" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "Testing Instructions:" -ForegroundColor Cyan
Write-Host ""
Write-Host "FIX #6 - Shortcut Help Widget (Dynamic Shortcuts):" -ForegroundColor Yellow
Write-Host "  • Left panel shows all shortcuts" -ForegroundColor Gray
Write-Host "  • Verify 'Tab' and 'Shift+Tab' appear under 'Navigation'" -ForegroundColor Gray
Write-Host "  • Verify 'Ctrl+1', 'Ctrl+2' appear under 'Workspace'" -ForegroundColor Gray
Write-Host "  • Search for shortcuts using the search box" -ForegroundColor Gray
Write-Host ""
Write-Host "FIX #5 - Settings Widget (Validation):" -ForegroundColor Yellow
Write-Host "  • Right panel shows configuration settings" -ForegroundColor Gray
Write-Host "  • Try setting 'MaxFPS' to -1000 → should show RED border" -ForegroundColor Gray
Write-Host "  • Try setting 'MaxFPS' to 60 → should show normal border" -ForegroundColor Gray
Write-Host "  • Try setting 'MaxThreads' to 100 → should show RED border (max 32)" -ForegroundColor Gray
Write-Host "  • Validation hints show acceptable ranges" -ForegroundColor Gray
Write-Host ""
Write-Host "Keyboard Shortcuts:" -ForegroundColor Yellow
Write-Host "  • Tab / Shift+Tab: Navigate between widgets" -ForegroundColor Gray
Write-Host "  • Ctrl+1 / Ctrl+2: Switch workspaces" -ForegroundColor Gray
Write-Host "  • Ctrl+Q: Quit" -ForegroundColor Gray
Write-Host ""
Write-Host "Log File:" -ForegroundColor Yellow
Write-Host "  $logFile" -ForegroundColor Gray
Write-Host ""

$logger.Info("Demo", "Showing main window")
$window.ShowDialog() | Out-Null
