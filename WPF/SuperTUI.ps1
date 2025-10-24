#Requires -Version 5.1
#Requires -PSEdition Desktop

# SuperTUI - Production Entry Point

# Platform check
if ($PSVersionTable.Platform -eq 'Unix') {
    Write-Error "SuperTUI requires Windows (WPF is Windows-only)"
    exit 1
}

# Load WPF
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Xaml

Write-Host "Compiling framework..." -ForegroundColor Cyan

# Load all sources in dependency order
$files = @(
    "Core/Interfaces/ILogger.cs"
    "Core/Interfaces/IThemeManager.cs"
    "Core/Interfaces/IConfigurationManager.cs"
    "Core/Interfaces/ISecurityManager.cs"
    "Core/Interfaces/IErrorHandler.cs"
    "Core/Interfaces/IServiceContainer.cs"
    "Core/Interfaces/IEventBus.cs"
    "Core/Interfaces/ILayoutEngine.cs"
    "Core/Interfaces/IWidget.cs"
    "Core/Interfaces/IThemeable.cs"
    "Core/Interfaces/IWorkspace.cs"
    "Core/Interfaces/IWorkspaceManager.cs"
    "Core/Infrastructure/Logger.cs"
    "Core/Infrastructure/ConfigurationManager.cs"
    "Core/Infrastructure/ThemeManager.cs"
    "Core/Infrastructure/SecurityManager.cs"
    "Core/Infrastructure/ErrorHandler.cs"
    "Core/Infrastructure/ServiceContainer.cs"
    "Core/Infrastructure/Events.cs"
    "Core/Infrastructure/EventBus.cs"
    "Core/Infrastructure/ShortcutManager.cs"
    "Core/Layout/LayoutEngine.cs"
    "Core/Layout/GridLayoutEngine.cs"
    "Core/Layout/DockLayoutEngine.cs"
    "Core/Layout/StackLayoutEngine.cs"
    "Core/Components/WidgetBase.cs"
    "Core/Components/ScreenBase.cs"
    "Core/Components/ErrorBoundary.cs"
    "Core/Infrastructure/Workspace.cs"
    "Core/Infrastructure/WorkspaceManager.cs"
    "Core/Infrastructure/WorkspaceTemplate.cs"
    "Core/Extensions.cs"
    "Widgets/ClockWidget.cs"
    "Widgets/CounterWidget.cs"
    "Widgets/NotesWidget.cs"
    "Widgets/TaskSummaryWidget.cs"
)

# Load all files and separate usings from code
$allUsings = [System.Collections.Generic.HashSet[string]]::new()
$allCode = [System.Collections.Generic.List[string]]::new()

foreach ($file in $files) {
    $path = Join-Path $PSScriptRoot $file
    if (Test-Path $path) {
        $content = Get-Content $path -Raw

        # Split at first namespace declaration
        if ($content -match '(?s)(^.*?)(namespace\s+.*)$') {
            $usingSection = $matches[1]
            $codeSection = $matches[2]

            # Extract using statements
            $usingMatches = [regex]::Matches($usingSection, '^\s*using\s+[^;]+;', [System.Text.RegularExpressions.RegexOptions]::Multiline)
            foreach ($match in $usingMatches) {
                [void]$allUsings.Add($match.Value.Trim())
            }

            # Add code section
            $allCode.Add($codeSection)
        } else {
            # No namespace, just add the whole file
            $allCode.Add($content)
        }
    } else {
        Write-Warning "Missing: $file"
    }
}

# Build combined source: ALL usings first, then ALL code
$combinedSource = ($allUsings -join "`n") + "`n`n" + ($allCode -join "`n`n")

# Get loaded assemblies for version matching
$pf = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'PresentationFramework' }
$pc = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'PresentationCore' }
$wb = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'WindowsBase' }
$sx = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'System.Xaml' }

try {
    Add-Type -TypeDefinition $combinedSource -ReferencedAssemblies @(
        $pf.Location
        $pc.Location
        $wb.Location
        $sx.Location
    ) -IgnoreWarnings -ErrorAction Stop
    Write-Host "Compiled successfully" -ForegroundColor Green
} catch {
    Write-Error "Compilation failed: $_"
    $combinedSource | Out-File "$PSScriptRoot/compile_error.cs" -Encoding UTF8
    Write-Host "Debug output saved to compile_error.cs" -ForegroundColor Yellow

    # Show line 4260-4265 to see the problem
    $lines = $combinedSource -split "`n"
    Write-Host "`nLines 4258-4265:" -ForegroundColor Yellow
    for ($i = 4257; $i -lt 4265 -and $i -lt $lines.Count; $i++) {
        Write-Host "$($i+1): $($lines[$i])" -ForegroundColor Gray
    }
    exit 1
}

# Create main window XAML
[xml]$xaml = @"
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="SuperTUI"
    Width="1400"
    Height="900"
    WindowStyle="None"
    ResizeMode="CanResizeWithGrip"
    Background="#0C0C0C">

    <Window.Resources>
        <!-- Terminal Colors -->
        <SolidColorBrush x:Key="TerminalBackground">#0C0C0C</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalForeground">#CCCCCC</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalBorder">#3A3A3A</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalAccent">#4EC9B0</SolidColorBrush>

        <!-- Title Bar Style -->
        <Style x:Key="TitleBarStyle" TargetType="Border">
            <Setter Property="Background" Value="#1E1E1E"/>
            <Setter Property="BorderBrush" Value="{StaticResource TerminalBorder}"/>
            <Setter Property="BorderThickness" Value="0,0,0,1"/>
        </Style>

        <!-- Status Bar Style -->
        <Style x:Key="StatusBarStyle" TargetType="Border">
            <Setter Property="Background" Value="#1E1E1E"/>
            <Setter Property="BorderBrush" Value="{StaticResource TerminalBorder}"/>
            <Setter Property="BorderThickness" Value="0,1,0,0"/>
            <Setter Property="Padding" Value="10,5"/>
        </Style>

        <!-- Button Style -->
        <Style x:Key="TitleBarButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="{StaticResource TerminalForeground}"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Width" Value="35"/>
            <Setter Property="Height" Value="30"/>
            <Setter Property="FontFamily" Value="Cascadia Mono, Consolas"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#2D2D2D"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="35"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="30"/>
        </Grid.RowDefinitions>

        <!-- Title Bar -->
        <Border Grid.Row="0" Style="{StaticResource TitleBarStyle}">
            <Grid>
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="10,0">
                    <TextBlock
                        x:Name="WorkspaceTitle"
                        Text="SuperTUI - Workspace 1"
                        FontFamily="Cascadia Mono, Consolas"
                        FontSize="13"
                        FontWeight="Bold"
                        Foreground="{StaticResource TerminalAccent}"
                        VerticalAlignment="Center"/>
                </StackPanel>

                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <Button Content="─" Style="{StaticResource TitleBarButtonStyle}" x:Name="MinimizeButton"/>
                    <Button Content="□" Style="{StaticResource TitleBarButtonStyle}" x:Name="MaximizeButton"/>
                    <Button Content="✕" Style="{StaticResource TitleBarButtonStyle}" x:Name="CloseButton"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Workspace Content (container for workspace layouts) -->
        <ContentControl
            x:Name="WorkspaceContainer"
            Grid.Row="1"
            Background="{StaticResource TerminalBackground}"/>

        <!-- Status Bar -->
        <Border Grid.Row="2" Style="{StaticResource StatusBarStyle}">
            <Grid>
                <TextBlock
                    x:Name="StatusText"
                    Text="Ctrl+1-9: Switch Workspace | Ctrl+Q: Quit | Tab: Next Widget"
                    FontFamily="Cascadia Mono, Consolas"
                    FontSize="11"
                    Foreground="#666666"
                    VerticalAlignment="Center"/>

                <TextBlock
                    x:Name="ClockStatus"
                    HorizontalAlignment="Right"
                    FontFamily="Cascadia Mono, Consolas"
                    FontSize="11"
                    Foreground="{StaticResource TerminalAccent}"
                    VerticalAlignment="Center"/>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

# Load XAML
$reader = [System.Xml.XmlNodeReader]::new($xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)

# Get controls
$workspaceContainer = $window.FindName("WorkspaceContainer")
$workspaceTitle = $window.FindName("WorkspaceTitle")
$statusText = $window.FindName("StatusText")
$clockStatus = $window.FindName("ClockStatus")
$closeButton = $window.FindName("CloseButton")
$minimizeButton = $window.FindName("MinimizeButton")
$maximizeButton = $window.FindName("MaximizeButton")

# Window chrome handlers
$closeButton.Add_Click({ $window.Close() })
$minimizeButton.Add_Click({ $window.WindowState = 'Minimized' })
$maximizeButton.Add_Click({
    if ($window.WindowState -eq 'Maximized') {
        $window.WindowState = 'Normal'
    } else {
        $window.WindowState = 'Maximized'
    }
})

# Make title bar draggable
$window.Add_MouseLeftButtonDown({
    $window.DragMove()
})

# Update status bar clock
$statusClockTimer = New-Object System.Windows.Threading.DispatcherTimer
$statusClockTimer.Interval = [TimeSpan]::FromSeconds(1)
$statusClockTimer.Add_Tick({
    $clockStatus.Text = (Get-Date).ToString("HH:mm:ss")
})
$statusClockTimer.Start()

# Create WorkspaceManager
$workspaceManager = New-Object SuperTUI.Core.WorkspaceManager($workspaceContainer)

# Create ShortcutManager
$shortcutManager = New-Object SuperTUI.Core.ShortcutManager

# ============================================================================
# DEFINE WORKSPACES
# ============================================================================

Write-Host "Setting up workspaces..." -ForegroundColor Cyan

# Workspace 1: Dashboard (Grid layout with widgets)
$workspace1Layout = New-Object SuperTUI.Core.GridLayoutEngine(2, 2)
$workspace1 = New-Object SuperTUI.Core.Workspace("Dashboard", 1, $workspace1Layout)

# Add Clock widget (top-left)
$clockWidget = New-Object SuperTUI.Widgets.ClockWidget
$clockWidget.WidgetName = "Clock"
$clockWidget.Initialize()
$clockParams = New-Object SuperTUI.Core.LayoutParams
$clockParams.Row = 0
$clockParams.Column = 0
$workspace1.AddWidget($clockWidget, $clockParams)

# Add TaskSummary widget (top-right)
$taskSummary = New-Object SuperTUI.Widgets.TaskSummaryWidget
$taskSummary.WidgetName = "TaskSummary"
$taskSummary.Initialize()
$taskParams = New-Object SuperTUI.Core.LayoutParams
$taskParams.Row = 0
$taskParams.Column = 1
$workspace1.AddWidget($taskSummary, $taskParams)

# Add placeholder widgets for demo
$placeholder1 = New-Object System.Windows.Controls.Border
$placeholder1.Background = [System.Windows.Media.Brushes]::Transparent
$placeholder1.BorderBrush = [System.Windows.Media.Brushes]::Gray
$placeholder1.BorderThickness = 1
$placeholder1.Child = (New-Object System.Windows.Controls.TextBlock -Property @{
    Text = "Widget Slot 3`n(Calendar, Tasks, etc.)"
    FontFamily = "Cascadia Mono, Consolas"
    Foreground = [System.Windows.Media.Brushes]::Gray
    HorizontalAlignment = "Center"
    VerticalAlignment = "Center"
    TextAlignment = "Center"
})
$placeholder1Params = New-Object SuperTUI.Core.LayoutParams
$placeholder1Params.Row = 1
$placeholder1Params.Column = 0
$workspace1Layout.AddChild($placeholder1, $placeholder1Params)

$placeholder2 = New-Object System.Windows.Controls.Border
$placeholder2.Background = [System.Windows.Media.Brushes]::Transparent
$placeholder2.BorderBrush = [System.Windows.Media.Brushes]::Gray
$placeholder2.BorderThickness = 1
$placeholder2.Child = (New-Object System.Windows.Controls.TextBlock -Property @{
    Text = "Widget Slot 4`n(Projects, Notes, etc.)"
    FontFamily = "Cascadia Mono, Consolas"
    Foreground = [System.Windows.Media.Brushes]::Gray
    HorizontalAlignment = "Center"
    VerticalAlignment = "Center"
    TextAlignment = "Center"
})
$placeholder2Params = New-Object SuperTUI.Core.LayoutParams
$placeholder2Params.Row = 1
$placeholder2Params.Column = 1
$workspace1Layout.AddChild($placeholder2, $placeholder2Params)

$workspaceManager.AddWorkspace($workspace1)

# Workspace 2: Projects (Dock layout - list on left, detail on right)
$workspace2Layout = New-Object SuperTUI.Core.DockLayoutEngine
$workspace2 = New-Object SuperTUI.Core.Workspace("Projects", 2, $workspace2Layout)

$leftPanel = New-Object System.Windows.Controls.Border
$leftPanel.Background = [System.Windows.Media.Brushes]::Transparent
$leftPanel.BorderBrush = [System.Windows.Media.Brushes]::Gray
$leftPanel.BorderThickness = 1
$leftPanel.Child = (New-Object System.Windows.Controls.TextBlock -Property @{
    Text = "Project List`n(Screen)"
    FontFamily = "Cascadia Mono, Consolas"
    Foreground = [System.Windows.Media.Brushes]::Gray
    HorizontalAlignment = "Center"
    VerticalAlignment = "Center"
    TextAlignment = "Center"
})
$leftParams = New-Object SuperTUI.Core.LayoutParams
$leftParams.Dock = [System.Windows.Controls.Dock]::Left
$leftParams.Width = 400
$workspace2Layout.AddChild($leftPanel, $leftParams)

$rightPanel = New-Object System.Windows.Controls.Border
$rightPanel.Background = [System.Windows.Media.Brushes]::Transparent
$rightPanel.BorderBrush = [System.Windows.Media.Brushes]::Gray
$rightPanel.BorderThickness = 1
$rightPanel.Child = (New-Object System.Windows.Controls.TextBlock -Property @{
    Text = "Task Detail`n(Screen)"
    FontFamily = "Cascadia Mono, Consolas"
    Foreground = [System.Windows.Media.Brushes]::Gray
    HorizontalAlignment = "Center"
    VerticalAlignment = "Center"
    TextAlignment = "Center"
})
$rightParams = New-Object SuperTUI.Core.LayoutParams
$workspace2Layout.AddChild($rightPanel, $rightParams)

$workspaceManager.AddWorkspace($workspace2)

# Workspace 3: Placeholder
$workspace3Layout = New-Object SuperTUI.Core.StackLayoutEngine
$workspace3 = New-Object SuperTUI.Core.Workspace("Workspace 3", 3, $workspace3Layout)

$ws3Placeholder = New-Object System.Windows.Controls.Border
$ws3Placeholder.Background = [System.Windows.Media.Brushes]::Transparent
$ws3Placeholder.BorderBrush = [System.Windows.Media.Brushes]::Gray
$ws3Placeholder.BorderThickness = 1
$ws3Placeholder.Child = (New-Object System.Windows.Controls.TextBlock -Property @{
    Text = "Workspace 3`n(Define your layout)"
    FontFamily = "Cascadia Mono, Consolas"
    FontSize = 24
    Foreground = [System.Windows.Media.Brushes]::Gray
    HorizontalAlignment = "Center"
    VerticalAlignment = "Center"
    TextAlignment = "Center"
})
$ws3Params = New-Object SuperTUI.Core.LayoutParams
$workspace3Layout.AddChild($ws3Placeholder, $ws3Params)

$workspaceManager.AddWorkspace($workspace3)

Write-Host "Workspaces created!" -ForegroundColor Green

# ============================================================================
# KEYBOARD SHORTCUTS
# ============================================================================

# Workspace switching (Ctrl+1 through Ctrl+9)
for ($i = 1; $i -le 9; $i++) {
    $index = $i
    $key = [System.Windows.Input.Key]::("D$i")
    $shortcutManager.RegisterGlobal($key, [System.Windows.Input.ModifierKeys]::Control, {
        $workspaceManager.SwitchToWorkspace($index)
        $current = $workspaceManager.CurrentWorkspace
        $workspaceTitle.Text = "SuperTUI - $($current.Name)"
        $statusText.Text = "Switched to workspace: $($current.Name)"
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
    },
    "Previous workspace"
)

# Keyboard handler
$window.Add_KeyDown({
    param($sender, $e)

    $currentWorkspaceName = $workspaceManager.CurrentWorkspace.Name
    $handled = $shortcutManager.HandleKeyDown($e.Key, $e.KeyboardDevice.Modifiers, $currentWorkspaceName)

    if ($handled) {
        $e.Handled = $true
    }
})

# ============================================================================
# SHOW WINDOW
# ============================================================================

Write-Host "`nStarting SuperTUI..." -ForegroundColor Green
Write-Host "Keyboard shortcuts:" -ForegroundColor Yellow
Write-Host "  Ctrl+1-9      Switch workspaces" -ForegroundColor Gray
Write-Host "  Ctrl+Left/Right   Previous/Next workspace" -ForegroundColor Gray
Write-Host "  Ctrl+Q        Quit" -ForegroundColor Gray
Write-Host ""

$window.ShowDialog() | Out-Null
