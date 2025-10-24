# SuperTUI WPF Framework - Enhanced Demo
# Shows multiple widgets per workspace, state preservation, focus management, and resizable panels

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  SuperTUI WPF Framework - Enhanced Demo" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Compile C# framework and widgets
Write-Host "[1/3] Compiling framework..." -ForegroundColor Yellow

$frameworkSource = Get-Content "$PSScriptRoot/Core/Framework.cs" -Raw
$clockWidgetSource = Get-Content "$PSScriptRoot/Widgets/ClockWidget.cs" -Raw
$taskSummarySource = Get-Content "$PSScriptRoot/Widgets/TaskSummaryWidget.cs" -Raw
$counterWidgetSource = Get-Content "$PSScriptRoot/Widgets/CounterWidget.cs" -Raw
$notesWidgetSource = Get-Content "$PSScriptRoot/Widgets/NotesWidget.cs" -Raw

# Extract just the namespace content without the using statements for each widget
# This prevents duplicate using statements and CS1529 errors
$clockWidgetSource = $clockWidgetSource -replace '(?s)^using.*?(?=namespace)', ''
$taskSummarySource = $taskSummarySource -replace '(?s)^using.*?(?=namespace)', ''
$counterWidgetSource = $counterWidgetSource -replace '(?s)^using.*?(?=namespace)', ''
$notesWidgetSource = $notesWidgetSource -replace '(?s)^using.*?(?=namespace)', ''

$combinedSource = @"
$frameworkSource

$clockWidgetSource

$taskSummarySource

$counterWidgetSource

$notesWidgetSource
"@

# Use the assembly references that were already loaded at the top of the script
Add-Type -TypeDefinition $combinedSource -ReferencedAssemblies @(
    'PresentationFramework',
    'PresentationCore',
    'WindowsBase',
    'System.Xaml'
)

Write-Host "[2/3] Creating main window..." -ForegroundColor Yellow

# Create main window XAML
[xml]$xaml = @"
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="SuperTUI"
    Width="1600"
    Height="1000"
    WindowStyle="None"
    ResizeMode="CanResizeWithGrip"
    Background="#0C0C0C"
    WindowStartupLocation="CenterScreen">

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
            <Setter Property="Width" Value="40"/>
            <Setter Property="Height" Value="32"/>
            <Setter Property="FontFamily" Value="Cascadia Mono, Consolas"/>
            <Setter Property="FontSize" Value="14"/>
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
                <Trigger Property="IsPressed" Value="True">
                    <Setter Property="Background" Value="#3E3E3E"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="40"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="28"/>
        </Grid.RowDefinitions>

        <!-- Title Bar -->
        <Border Grid.Row="0" Style="{StaticResource TitleBarStyle}">
            <Grid>
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="12,0">
                    <TextBlock
                        Text="●"
                        FontSize="18"
                        Foreground="{StaticResource TerminalAccent}"
                        VerticalAlignment="Center"
                        Margin="0,0,10,0"/>
                    <TextBlock
                        x:Name="WorkspaceTitle"
                        Text="SuperTUI - Dashboard"
                        FontFamily="Cascadia Mono, Consolas"
                        FontSize="14"
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

        <!-- Workspace Content -->
        <ContentControl
            x:Name="WorkspaceContainer"
            Grid.Row="1"
            Background="{StaticResource TerminalBackground}"/>

        <!-- Status Bar -->
        <Border Grid.Row="2" Style="{StaticResource StatusBarStyle}">
            <Grid>
                <TextBlock
                    x:Name="StatusText"
                    FontFamily="Cascadia Mono, Consolas"
                    FontSize="11"
                    Foreground="#888888"
                    VerticalAlignment="Center"/>

                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <TextBlock
                        x:Name="FocusInfo"
                        FontFamily="Cascadia Mono, Consolas"
                        FontSize="11"
                        Foreground="#666666"
                        VerticalAlignment="Center"
                        Margin="0,0,20,0"/>
                    <TextBlock
                        x:Name="ClockStatus"
                        FontFamily="Cascadia Mono, Consolas"
                        FontSize="11"
                        Foreground="{StaticResource TerminalAccent}"
                        VerticalAlignment="Center"/>
                </StackPanel>
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
$focusInfo = $window.FindName("FocusInfo")
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
        $maximizeButton.Content = "□"
    } else {
        $window.WindowState = 'Maximized'
        $maximizeButton.Content = "❐"
    }
})

# Make title bar draggable
$window.Add_MouseLeftButtonDown({
    if ($_.ChangedButton -eq [System.Windows.Input.MouseButton]::Left) {
        $window.DragMove()
    }
})

# Status bar clock
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

Write-Host "[3/3] Creating workspaces and widgets..." -ForegroundColor Yellow

# ============================================================================
# WORKSPACE 1: DASHBOARD (3x2 grid with resizable splitters)
# ============================================================================

$ws1Layout = New-Object SuperTUI.Core.GridLayoutEngine(2, 3, $true)  # enableSplitters = true
$workspace1 = New-Object SuperTUI.Core.Workspace("Dashboard", 1, $ws1Layout)

# Row 0, Column 0 - Clock
$clock1 = New-Object SuperTUI.Widgets.ClockWidget
$clock1.WidgetName = "Clock"
$clock1.Initialize()
$clock1Params = New-Object SuperTUI.Core.LayoutParams
$clock1Params.Row = 0
$clock1Params.Column = 0
$workspace1.AddWidget($clock1, $clock1Params)

# Row 0, Column 1 - Task Summary
$taskSummary1 = New-Object SuperTUI.Widgets.TaskSummaryWidget
$taskSummary1.WidgetName = "Task Summary"
$taskSummary1.Initialize()
$taskSummary1Params = New-Object SuperTUI.Core.LayoutParams
$taskSummary1Params.Row = 0
$taskSummary1Params.Column = 1
$workspace1.AddWidget($taskSummary1, $taskSummary1Params)

# Row 0, Column 2 - Counter 1
$counter1 = New-Object SuperTUI.Widgets.CounterWidget
$counter1.WidgetName = "Counter 1"
$counter1.Initialize()
$counter1Params = New-Object SuperTUI.Core.LayoutParams
$counter1Params.Row = 0
$counter1Params.Column = 2
$workspace1.AddWidget($counter1, $counter1Params)

# Row 1, Column 0-1 (span 2 columns) - Notes
$notes1 = New-Object SuperTUI.Widgets.NotesWidget
$notes1.WidgetName = "Notes"
$notes1.Initialize()
$notes1Params = New-Object SuperTUI.Core.LayoutParams
$notes1Params.Row = 1
$notes1Params.Column = 0
$notes1Params.ColumnSpan = 2
$workspace1.AddWidget($notes1, $notes1Params)

# Row 1, Column 2 - Counter 2 (to demonstrate independent state)
$counter2 = New-Object SuperTUI.Widgets.CounterWidget
$counter2.WidgetName = "Counter 2"
$counter2.Initialize()
$counter2Params = New-Object SuperTUI.Core.LayoutParams
$counter2Params.Row = 1
$counter2Params.Column = 2
$workspace1.AddWidget($counter2, $counter2Params)

$workspaceManager.AddWorkspace($workspace1)

# ============================================================================
# WORKSPACE 2: FOCUS TEST (2x2 grid)
# ============================================================================

$ws2Layout = New-Object SuperTUI.Core.GridLayoutEngine(2, 2, $true)
$workspace2 = New-Object SuperTUI.Core.Workspace("Focus Test", 2, $ws2Layout)

# Create 4 counter widgets to test focus switching with Tab
for ($i = 0; $i -lt 4; $i++) {
    $counter = New-Object SuperTUI.Widgets.CounterWidget
    $counter.WidgetName = "Counter $($i + 1)"
    $counter.Initialize()

    $params = New-Object SuperTUI.Core.LayoutParams
    $params.Row = [Math]::Floor($i / 2)
    $params.Column = $i % 2

    $workspace2.AddWidget($counter, $params)
}

$workspaceManager.AddWorkspace($workspace2)

# ============================================================================
# WORKSPACE 3: MIXED LAYOUT (dock layout)
# ============================================================================

$ws3Layout = New-Object SuperTUI.Core.DockLayoutEngine
$workspace3 = New-Object SuperTUI.Core.Workspace("Mixed Layout", 3, $ws3Layout)

# Top - Clock
$clockTop = New-Object SuperTUI.Widgets.ClockWidget
$clockTop.WidgetName = "Top Clock"
$clockTop.Initialize()
$clockTopParams = New-Object SuperTUI.Core.LayoutParams
$clockTopParams.Dock = [System.Windows.Controls.Dock]::Top
$clockTopParams.Height = 150
$workspace3.AddWidget($clockTop, $clockTopParams)

# Left - Task Summary
$taskLeft = New-Object SuperTUI.Widgets.TaskSummaryWidget
$taskLeft.WidgetName = "Left Tasks"
$taskLeft.Initialize()
$taskLeftParams = New-Object SuperTUI.Core.LayoutParams
$taskLeftParams.Dock = [System.Windows.Controls.Dock]::Left
$taskLeftParams.Width = 300
$workspace3.AddWidget($taskLeft, $taskLeftParams)

# Fill - Notes (takes remaining space)
$notesFill = New-Object SuperTUI.Widgets.NotesWidget
$notesFill.WidgetName = "Main Notes"
$notesFill.Initialize()
$notesFillParams = New-Object SuperTUI.Core.LayoutParams
$workspace3.AddWidget($notesFill, $notesFillParams)

$workspaceManager.AddWorkspace($workspace3)

Write-Host "`n✓ Created 3 workspaces with 12 total widgets" -ForegroundColor Green

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
        $statusText.Text = "Switched to: $($current.Name) | $($current.Widgets.Count) widgets"
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
        $statusText.Text = "Switched to: $($current.Name) | $($current.Widgets.Count) widgets"
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
        $statusText.Text = "Switched to: $($current.Name) | $($current.Widgets.Count) widgets"
    },
    "Previous workspace"
)

# Keyboard handler - delegates to WorkspaceManager which handles Tab and widget key handling
$window.Add_KeyDown({
    param($sender, $e)

    $currentWorkspaceName = $workspaceManager.CurrentWorkspace.Name
    $handled = $shortcutManager.HandleKeyDown($e.Key, $e.KeyboardDevice.Modifiers, $currentWorkspaceName)

    if (!$handled) {
        # Let workspace handle key (for Tab focus switching and widget-specific keys)
        $workspaceManager.HandleKeyDown($e)
    }
})

# Update status bar with current workspace info
$workspaceManager.add_WorkspaceChanged({
    param($workspace)
    $statusText.Text = "$($workspace.Name) | $($workspace.Widgets.Count) widgets | Tab: Switch focus | Ctrl+1-3: Change workspace"
})

# Initial status
$statusText.Text = "Dashboard | 5 widgets | Tab: Switch focus | Ctrl+1-3: Change workspace"

# ============================================================================
# SHOW WINDOW
# ============================================================================

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  SuperTUI is ready!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "KEYBOARD SHORTCUTS:" -ForegroundColor Yellow
Write-Host "  Ctrl+1, Ctrl+2, Ctrl+3   Switch workspaces" -ForegroundColor Gray
Write-Host "  Ctrl+Left/Right          Previous/Next workspace" -ForegroundColor Gray
Write-Host "  Tab / Shift+Tab          Switch widget focus" -ForegroundColor Gray
Write-Host "  Ctrl+Q                   Quit" -ForegroundColor Gray
Write-Host "`nWIDGET CONTROLS:" -ForegroundColor Yellow
Write-Host "  Counter: Up/Down arrows  Increment/Decrement" -ForegroundColor Gray
Write-Host "  Counter: R               Reset to zero" -ForegroundColor Gray
Write-Host "  Notes: Type freely       Text persists across workspace switches!" -ForegroundColor Gray
Write-Host "`nFEATURES TO TEST:" -ForegroundColor Yellow
Write-Host "  • Focus indicator        Focused widget has cyan border" -ForegroundColor Gray
Write-Host "  • State preservation     Counter values persist when switching workspaces" -ForegroundColor Gray
Write-Host "  • Resizable panels       Drag the gray splitters (Workspace 1)" -ForegroundColor Gray
Write-Host "  • Multiple instances     Each counter widget has independent state" -ForegroundColor Gray
Write-Host ""

$window.ShowDialog() | Out-Null
