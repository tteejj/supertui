# SuperTUI - Production Entry Point

# Platform check
if ($PSVersionTable.Platform -eq 'Unix') {
    Write-Error "SuperTUI requires Windows (WPF is Windows-only)"
    exit 1
}

# Load SuperTUI.dll
$dllPath = Join-Path $PSScriptRoot "bin/Release/net8.0-windows/SuperTUI.dll"

if (-not (Test-Path $dllPath)) {
    Write-Host "SuperTUI.dll not found. Building..." -ForegroundColor Yellow
    & "$PSScriptRoot/build.ps1"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
}

Write-Host "Loading SuperTUI..." -ForegroundColor Cyan

try {
    Add-Type -Path $dllPath
    Write-Host "Loaded successfully" -ForegroundColor Green
} catch {
    Write-Error "Failed to load SuperTUI.dll: $_"
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
                    Text="Ctrl+1-9: Workspace | Ctrl+Left/Right: Prev/Next WS | Tab: Focus | Ctrl+Up/Down: Cycle | Ctrl+C: Close | Ctrl+N: Add | Ctrl+Shift+Arrows: Move | Ctrl+Q: Quit"
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

# ============================================================================
# INITIALIZE INFRASTRUCTURE
# ============================================================================

Write-Host "Initializing SuperTUI infrastructure..." -ForegroundColor Cyan

# Initialize Logger
$logger = [SuperTUI.Infrastructure.Logger]::Instance
$fileLogSink = New-Object SuperTUI.Infrastructure.FileLogSink("$env:TEMP", "SuperTUI")
$logger.AddSink($fileLogSink)
$logger.SetMinLevel([SuperTUI.Infrastructure.LogLevel]::Debug)
Write-Host "Logger initialized with Debug level" -ForegroundColor Green

# Initialize ThemeManager
$themeManager = [SuperTUI.Infrastructure.ThemeManager]::Instance
$themeManager.Initialize($null)  # Uses default built-in themes

# Initialize ConfigurationManager
$configManager = [SuperTUI.Infrastructure.ConfigurationManager]::Instance
$configManager.Initialize("$env:TEMP\SuperTUI-config.json")

# Initialize other infrastructure
$errorHandler = [SuperTUI.Infrastructure.ErrorHandler]::Instance
$securityManager = [SuperTUI.Infrastructure.SecurityManager]::Instance
$securityManager.Initialize()

# Initialize ExcelMappingService (will create default profiles if none exist)
$excelMappingService = [SuperTUI.Core.Services.ExcelMappingService]::Instance
$excelMappingService.Initialize()

Write-Host "Infrastructure initialized" -ForegroundColor Green

# ============================================================================
# GLOBAL EXCEPTION HANDLER
# ============================================================================

# Catch unhandled exceptions as last resort
[System.AppDomain]::CurrentDomain.add_UnhandledException({
    param($sender, $e)
    $exception = $e.ExceptionObject

    # Log critical error with full details
    $logger.Critical("Application",
        "UNHANDLED EXCEPTION CAUGHT`n" +
        "  Message: $($exception.Message)`n" +
        "  Type: $($exception.GetType().FullName)`n" +
        "  Stack Trace:`n$($exception.StackTrace)", $exception)

    # Show user-friendly error dialog
    $logPath = "$env:TEMP\SuperTUI\Logs"
    [System.Windows.MessageBox]::Show(
        "A critical error occurred. The application will attempt to continue but may be unstable.`n`n" +
        "Error: $($exception.Message)`n`n" +
        "Details have been logged to:`n$logPath`n`n" +
        "Please report this issue with the log files.",
        "SuperTUI - Critical Error",
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Error)

    # Check if CLR is terminating
    if ($e.IsTerminating)
    {
        $logger.Critical("Application", "CLR is terminating. Application will exit.")

        # Save state before exit (best effort)
        try
        {
            Write-Host "Attempting emergency state save..." -ForegroundColor Yellow
            # StatePersistenceManager would go here if initialized
        }
        catch
        {
            $logger.Error("Application", "Emergency state save failed: $_")
        }
    }
})

Write-Host "Global exception handler registered" -ForegroundColor Green

# Create WorkspaceManager
$workspaceManager = New-Object SuperTUI.Core.WorkspaceManager($workspaceContainer)

# Create ShortcutManager
$shortcutManager = New-Object SuperTUI.Core.ShortcutManager

# ============================================================================
# DEFINE WORKSPACES
# ============================================================================

Write-Host "Setting up workspaces..." -ForegroundColor Cyan

# Workspace 1: Dashboard (Flexible i3-like layout)
$workspace1Layout = New-Object SuperTUI.Core.DashboardLayoutEngine
$workspace1 = New-Object SuperTUI.Core.Workspace("Dashboard", 1, $workspace1Layout)

# Add Clock widget to slot 0 (top-left)
$clockWidget = New-Object SuperTUI.Widgets.ClockWidget
$clockWidget.WidgetName = "Clock"
$clockWidget.Initialize()
$workspace1Layout.SetWidget(0, $clockWidget)
$workspace1.Widgets.Add($clockWidget)

# Add TaskSummary widget to slot 1 (top-right)
$taskSummary = New-Object SuperTUI.Widgets.TaskSummaryWidget
$taskSummary.WidgetName = "TaskSummary"
$taskSummary.Initialize()
$workspace1Layout.SetWidget(1, $taskSummary)
$workspace1.Widgets.Add($taskSummary)

# Slots 2 and 3 are empty (will show "Empty Slot" placeholders)

$workspaceManager.AddWorkspace($workspace1)

# Workspace 2: Projects (Full Project Management)
$workspace2Layout = New-Object SuperTUI.Core.StackLayoutEngine([System.Windows.Controls.Orientation]::Vertical)
$workspace2 = New-Object SuperTUI.Core.Workspace("Projects", 2, $workspace2Layout)

# Add ProjectManagementWidget (3-pane layout: list, context, details)
$projectManagementWidget = New-Object SuperTUI.Widgets.ProjectManagementWidget
$projectManagementWidget.WidgetName = "ProjectManagement"
$projectManagementWidget.Initialize()
$ws2Params = New-Object SuperTUI.Core.LayoutParams
$workspace2Layout.AddChild($projectManagementWidget, $ws2Params)
$workspace2.Widgets.Add($projectManagementWidget)

$workspaceManager.AddWorkspace($workspace2)

# Workspace 3: Kanban Board (3-column task board)
$workspace3Layout = New-Object SuperTUI.Core.StackLayoutEngine([System.Windows.Controls.Orientation]::Vertical)
$workspace3 = New-Object SuperTUI.Core.Workspace("Kanban", 3, $workspace3Layout)

# Add KanbanBoardWidget (Todo, In Progress, Done columns)
$kanbanWidget = New-Object SuperTUI.Widgets.KanbanBoardWidget
$kanbanWidget.WidgetName = "KanbanBoard"
$kanbanWidget.Initialize()
$ws3Params = New-Object SuperTUI.Core.LayoutParams
$workspace3Layout.AddChild($kanbanWidget, $ws3Params)
$workspace3.Widgets.Add($kanbanWidget)

$workspaceManager.AddWorkspace($workspace3)

# Workspace 4: Agenda (Time-grouped task view)
$workspace4Layout = New-Object SuperTUI.Core.StackLayoutEngine([System.Windows.Controls.Orientation]::Vertical)
$workspace4 = New-Object SuperTUI.Core.Workspace("Agenda", 4, $workspace4Layout)

# Add AgendaWidget (Overdue, Today, Tomorrow, This Week, Later, No Due Date)
$agendaWidget = New-Object SuperTUI.Widgets.AgendaWidget
$agendaWidget.WidgetName = "Agenda"
$agendaWidget.Initialize()
$ws4Params = New-Object SuperTUI.Core.LayoutParams
$workspace4Layout.AddChild($agendaWidget, $ws4Params)
$workspace4.Widgets.Add($agendaWidget)

$workspaceManager.AddWorkspace($workspace4)

# Workspace 5: Project Analytics (Stats and metrics)
$workspace5Layout = New-Object SuperTUI.Core.StackLayoutEngine([System.Windows.Controls.Orientation]::Vertical)
$workspace5 = New-Object SuperTUI.Core.Workspace("Analytics", 5, $workspace5Layout)

# Add ProjectStatsWidget (Metrics, charts, recent activity)
$statsWidget = New-Object SuperTUI.Widgets.ProjectStatsWidget
$statsWidget.WidgetName = "ProjectStats"
$statsWidget.Initialize()
$ws5Params = New-Object SuperTUI.Core.LayoutParams
$workspace5Layout.AddChild($statsWidget, $ws5Params)
$workspace5.Widgets.Add($statsWidget)

$workspaceManager.AddWorkspace($workspace5)

# Workspace 6: Excel Integration (Import/Export)
$workspace6Layout = New-Object SuperTUI.Core.GridLayoutEngine(2, 1)
$workspace6 = New-Object SuperTUI.Core.Workspace("Excel", 6, $workspace6Layout)

# Top: Import widget (left) and Export widget (right)
$importWidget = New-Object SuperTUI.Widgets.ExcelImportWidget
$importWidget.WidgetName = "ExcelImport"
$importWidget.Initialize()
$ws6Params1 = New-Object SuperTUI.Core.LayoutParams
$ws6Params1.Row = 0
$ws6Params1.Column = 0
$workspace6Layout.AddChild($importWidget, $ws6Params1)
$workspace6.Widgets.Add($importWidget)

$exportWidget = New-Object SuperTUI.Widgets.ExcelExportWidget
$exportWidget.WidgetName = "ExcelExport"
$exportWidget.Initialize()
$ws6Params2 = New-Object SuperTUI.Core.LayoutParams
$ws6Params2.Row = 0
$ws6Params2.Column = 1
$workspace6Layout.AddChild($exportWidget, $ws6Params2)
$workspace6.Widgets.Add($exportWidget)

$workspaceManager.AddWorkspace($workspace6)

Write-Host "Workspaces created!" -ForegroundColor Green

# ============================================================================
# KEYBOARD SHORTCUTS
# ============================================================================

# Workspace switching (Ctrl+1 through Ctrl+9)
# NOTE: Must use GetNewClosure() to capture current loop value
for ($i = 1; $i -le 9; $i++) {
    $key = [System.Windows.Input.Key]::("D$i")

    # GetNewClosure() captures ALL variables in current scope at this moment
    # This creates a new closure for each iteration with the current value of $i
    $action = {
        $workspaceManager.SwitchToWorkspace($i)
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $workspaceTitle.Text = "SuperTUI - $($current.Name)"
            $statusText.Text = "Switched to workspace: $($current.Name)"
        }
    }.GetNewClosure()

    $shortcutManager.RegisterGlobal($key, [System.Windows.Input.ModifierKeys]::Control, $action, "Switch to workspace $i")
}

# Quit
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Q,
    [System.Windows.Input.ModifierKeys]::Control,
    { $window.Close() },
    "Quit SuperTUI"
)

# Close focused widget (Ctrl+C)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::C,
    [System.Windows.Input.ModifierKeys]::Control,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $focused = $current.GetFocusedWidget()
            if ($focused) {
                $widgetName = $focused.WidgetName
                $current.RemoveFocusedWidget()
                $statusText.Text = "Closed widget: $widgetName"
                $logger.Info("Workspace", "Closed widget: $widgetName")
            } else {
                $statusText.Text = "No widget focused"
            }
        }
    },
    "Close focused widget"
)

# Focus navigation (Ctrl+Up/Down for cycling focus)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Down,
    [System.Windows.Input.ModifierKeys]::Control,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.CycleFocusForward()
            $focused = $current.GetFocusedWidget()
            $statusText.Text = "Focus: $($focused?.WidgetName ?? 'None')"
        }
    },
    "Focus next widget"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Up,
    [System.Windows.Input.ModifierKeys]::Control,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.CycleFocusBackward()
            $focused = $current.GetFocusedWidget()
            $statusText.Text = "Focus: $($focused?.WidgetName ?? 'None')"
        }
    },
    "Focus previous widget"
)

# Add new widget (Ctrl+N)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::N,
    [System.Windows.Input.ModifierKeys]::Control,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current -and $current.Layout -is [SuperTUI.Core.DashboardLayoutEngine]) {
            # Show widget picker
            $picker = New-Object SuperTUI.Core.Components.WidgetPicker
            $picker.Owner = $window
            $result = $picker.ShowDialog()

            if ($result -eq $true -and $picker.SelectedWidget) {
                try {
                    # Create widget instance by searching loaded assemblies
                    $widgetType = [AppDomain]::CurrentDomain.GetAssemblies() |
                        ForEach-Object { $_.GetType($picker.SelectedWidget.TypeName) } |
                        Where-Object { $_ -ne $null } |
                        Select-Object -First 1

                    if ($null -eq $widgetType) {
                        throw "Widget type '$($picker.SelectedWidget.TypeName)' not found"
                    }

                    $widget = [Activator]::CreateInstance($widgetType)
                    $widget.WidgetName = $picker.SelectedWidget.Name

                    # Find first empty slot
                    $layout = [SuperTUI.Core.DashboardLayoutEngine]$current.Layout
                    $slotIndex = -1
                    for ($i = 0; $i -lt 4; $i++) {
                        if ($null -eq $layout.GetWidget($i)) {
                            $slotIndex = $i
                            break
                        }
                    }

                    if ($slotIndex -ge 0) {
                        # Use workspace AddWidget method to properly wrap in ErrorBoundary
                        $layoutParams = New-Object SuperTUI.Core.LayoutParams
                        $layoutParams.Row = $slotIndex
                        $current.AddWidget($widget, $layoutParams)

                        $statusText.Text = "Added $($picker.SelectedWidget.Name) to slot $($slotIndex + 1)"
                        $logger.Info("Workspace", "Added widget: $($picker.SelectedWidget.Name) to slot $($slotIndex + 1)")
                    } else {
                        $statusText.Text = "All slots full - close a widget first (Ctrl+W)"
                    }
                } catch {
                    $statusText.Text = "Error adding widget: $_"
                    Write-Host "Error: $_" -ForegroundColor Red
                }
            }
        } else {
            $statusText.Text = "Add widget only works in Dashboard workspace"
        }
    },
    "Add new widget"
)

# Next/Previous workspace
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Right,
    [System.Windows.Input.ModifierKeys]::Control,
    {
        try {
            $workspaceManager.SwitchToNext()
            $current = $workspaceManager.CurrentWorkspace
            if ($current) {
                $workspaceTitle.Text = "SuperTUI - $($current.Name)"
            }
        } catch {
            Write-Host "Error switching workspace: $_" -ForegroundColor Red
        }
    },
    "Next workspace"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Left,
    [System.Windows.Input.ModifierKeys]::Control,
    {
        try {
            $workspaceManager.SwitchToPrevious()
            $current = $workspaceManager.CurrentWorkspace
            if ($current) {
                $workspaceTitle.Text = "SuperTUI - $($current.Name)"
            }
        } catch {
            Write-Host "Error switching workspace: $_" -ForegroundColor Red
        }
    },
    "Previous workspace"
)

# Tab: Cycle focus between widgets in current workspace
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Tab,
    [System.Windows.Input.ModifierKeys]::None,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.CycleFocusForward()
        }
    },
    "Next widget focus"
)

# Shift+Tab: Cycle focus backward
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Tab,
    [System.Windows.Input.ModifierKeys]::Shift,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.CycleFocusBackward()
        }
    },
    "Previous widget focus"
)

# Move widget with Ctrl+Shift+Arrow
$moveWidgetScript = {
    param($direction)
    $current = $workspaceManager.CurrentWorkspace
    if ($current -and $current.Layout -is [SuperTUI.Core.DashboardLayoutEngine]) {
        $focused = $current.GetFocusedWidget()
        if ($focused) {
            $layout = [SuperTUI.Core.DashboardLayoutEngine]$current.Layout
            $currentSlot = $layout.FindSlotIndex($focused)

            if ($currentSlot -ge 0) {
                $targetSlot = -1
                switch ($direction) {
                    "Left"  { if ($currentSlot % 2 -eq 1) { $targetSlot = $currentSlot - 1 } }  # Right col -> Left col
                    "Right" { if ($currentSlot % 2 -eq 0) { $targetSlot = $currentSlot + 1 } }  # Left col -> Right col
                    "Up"    { if ($currentSlot -ge 2) { $targetSlot = $currentSlot - 2 } }      # Bottom row -> Top row
                    "Down"  { if ($currentSlot -le 1) { $targetSlot = $currentSlot + 2 } }      # Top row -> Bottom row
                }

                if ($targetSlot -ge 0 -and $targetSlot -lt 4) {
                    $layout.SwapWidgets($currentSlot, $targetSlot)
                    $statusText.Text = "Moved $($focused.WidgetName) to slot $($targetSlot + 1)"
                } else {
                    $statusText.Text = "Cannot move widget in that direction"
                }
            }
        } else {
            $statusText.Text = "No widget focused"
        }
    } else {
        $statusText.Text = "Widget movement only works in Dashboard"
    }
}

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Left,
    ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Left" },
    "Move widget left"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Right,
    ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Right" },
    "Move widget right"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Up,
    ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Up" },
    "Move widget up"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Down,
    ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Down" },
    "Move widget down"
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
