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
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Mode Indicator (Left) -->
                <TextBlock
                    x:Name="ModeIndicator"
                    Grid.Column="0"
                    Text="-- NORMAL --"
                    FontFamily="Cascadia Mono, Consolas"
                    FontSize="11"
                    FontWeight="Bold"
                    Foreground="#569CD6"
                    Margin="0,0,15,0"
                    VerticalAlignment="Center"/>

                <!-- Keyboard Shortcuts (Center) -->
                <TextBlock
                    x:Name="StatusText"
                    Grid.Column="1"
                    Text="Tab: Next Widget | Alt+Arrows: Navigate | Alt+Shift+Arrows: Move Widget | ?: Help | G: Quick Jump"
                    FontFamily="Cascadia Mono, Consolas"
                    FontSize="11"
                    Foreground="#666666"
                    VerticalAlignment="Center"/>

                <!-- Clock (Right) -->
                <TextBlock
                    x:Name="ClockStatus"
                    Grid.Column="2"
                    FontFamily="Cascadia Mono, Consolas"
                    FontSize="11"
                    Foreground="{StaticResource TerminalAccent}"
                    VerticalAlignment="Center"/>
            </Grid>
        </Border>

        <!-- Shortcut Overlay (spans all rows, renders on top) -->
        <Border
            x:Name="ShortcutOverlayContainer"
            Grid.Row="0"
            Grid.RowSpan="3"
            Visibility="Collapsed"/>

        <!-- Quick Jump Overlay (spans all rows, renders on top) -->
        <Border
            x:Name="QuickJumpOverlayContainer"
            Grid.Row="0"
            Grid.RowSpan="3"
            Visibility="Collapsed"/>

        <!-- CRT Effects Overlay (spans all rows, renders on top of everything) -->
        <Canvas
            x:Name="CRTOverlay"
            Grid.Row="0"
            Grid.RowSpan="3"
            IsHitTestVisible="False"
            Background="Transparent"/>
    </Grid>
</Window>
"@

# Load XAML
$reader = [System.Xml.XmlNodeReader]::new($xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)

# Get controls
$workspaceContainer = $window.FindName("WorkspaceContainer")
$workspaceTitle = $window.FindName("WorkspaceTitle")
$modeIndicator = $window.FindName("ModeIndicator")
$statusText = $window.FindName("StatusText")
$clockStatus = $window.FindName("ClockStatus")
$closeButton = $window.FindName("CloseButton")
$minimizeButton = $window.FindName("MinimizeButton")
$maximizeButton = $window.FindName("MaximizeButton")
$shortcutOverlayContainer = $window.FindName("ShortcutOverlayContainer")
$quickJumpOverlayContainer = $window.FindName("QuickJumpOverlayContainer")
$crtOverlayCanvas = $window.FindName("CRTOverlay")

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

# Initialize ConfigurationManager
$configManager = [SuperTUI.Infrastructure.ConfigurationManager]::Instance
$configManager.Initialize("$env:TEMP\SuperTUI-config.json")

# Initialize ThemeManager
$themeManager = [SuperTUI.Infrastructure.ThemeManager]::Instance
$themeManager.Initialize($null)  # Uses default built-in themes

# Apply saved theme (or default to "Cyberpunk" to showcase new effects)
$savedThemeName = $configManager.Get("UI.Theme", "Cyberpunk")
$themeManager.ApplyTheme($savedThemeName)
Write-Host "Applied theme: $savedThemeName" -ForegroundColor Green

# Initialize other infrastructure
$errorHandler = [SuperTUI.Infrastructure.ErrorHandler]::Instance
$securityManager = [SuperTUI.Infrastructure.SecurityManager]::Instance
$securityManager.Initialize()

# Initialize ExcelMappingService (will create default profiles if none exist)
$excelMappingService = [SuperTUI.Core.Services.ExcelMappingService]::Instance
$excelMappingService.Initialize()

# Initialize EventBus
$eventBus = [SuperTUI.Core.EventBus]::Instance
Write-Host "EventBus initialized" -ForegroundColor Green

# Initialize ApplicationContext
$appContext = [SuperTUI.Infrastructure.ApplicationContext]::Instance
Write-Host "ApplicationContext initialized" -ForegroundColor Green

# Initialize StatePersistenceManager
$stateManager = [SuperTUI.Infrastructure.StatePersistenceManager]::Instance
$stateManager.Initialize("$env:TEMP\SuperTUI-state.json")
Write-Host "StatePersistenceManager initialized" -ForegroundColor Green

Write-Host "Infrastructure initialized" -ForegroundColor Green

# ============================================================================
# SHORTCUT OVERLAY
# ============================================================================

# Create and initialize the shortcut overlay
$shortcutOverlay = New-Object SuperTUI.Core.Components.ShortcutOverlay(
    $themeManager,
    [SuperTUI.Infrastructure.ShortcutManager]::Instance,
    $logger
)
$shortcutOverlayContainer.Child = $shortcutOverlay
Write-Host "ShortcutOverlay initialized" -ForegroundColor Green

# ============================================================================
# QUICK JUMP OVERLAY
# ============================================================================

# Create and initialize the quick jump overlay
$quickJumpOverlay = New-Object SuperTUI.Core.Components.QuickJumpOverlay(
    $themeManager,
    $logger
)
$quickJumpOverlayContainer.Child = $quickJumpOverlay

# Wire up QuickJumpOverlay events
$quickJumpOverlay.add_JumpRequested({
    param($targetWidget, $context)

    # Find the target widget in the current workspace
    $current = $workspaceManager.CurrentWorkspace
    if ($current) {
        $widgets = $current.GetAllWidgets()
        foreach ($widget in $widgets) {
            if ($widget.WidgetName -eq $targetWidget -or $widget.WidgetType -eq $targetWidget) {
                $current.FocusWidget($widget)

                # If context is provided, apply it to the widget
                if ($context -ne $null) {
                    # Context could be a task ID, date, file path, etc.
                    # Widgets can handle context via a method or property
                    if ($widget | Get-Member -Name "SetContext" -MemberType Method) {
                        $widget.SetContext($context)
                    }
                }

                $logger.Info("QuickJump", "Jumped to $targetWidget")
                break
            }
        }
    }
})

$quickJumpOverlay.add_CloseRequested({
    # Nothing special needed - overlay already hidden itself
})

Write-Host "QuickJumpOverlay initialized" -ForegroundColor Green

# ============================================================================
# CRT EFFECTS OVERLAY
# ============================================================================

# Create and initialize the CRT effects overlay
$crtOverlay = New-Object SuperTUI.Core.Components.CRTEffectsOverlay
$crtOverlayCanvas.Children.Add($crtOverlay)

# Apply CRT effects from current theme
$currentTheme = $themeManager.CurrentTheme
if ($currentTheme.CRTEffects -ne $null) {
    $crtOverlay.UpdateFromTheme(
        $currentTheme.CRTEffects.EnableScanlines,
        $currentTheme.CRTEffects.ScanlineOpacity,
        $currentTheme.CRTEffects.ScanlineSpacing,
        $currentTheme.CRTEffects.ScanlineColor,
        $currentTheme.CRTEffects.EnableBloom,
        $currentTheme.CRTEffects.BloomIntensity
    )
}

# Apply window opacity from theme
if ($currentTheme.Opacity -ne $null) {
    $window.Opacity = $currentTheme.Opacity.WindowOpacity
}

# Subscribe to theme changes to update CRT overlay and opacity
$themeManager.ThemeChanged += {
    param($sender, $args)
    $newTheme = $args.NewTheme

    # Update CRT overlay
    if ($newTheme.CRTEffects -ne $null) {
        $crtOverlay.UpdateFromTheme(
            $newTheme.CRTEffects.EnableScanlines,
            $newTheme.CRTEffects.ScanlineOpacity,
            $newTheme.CRTEffects.ScanlineSpacing,
            $newTheme.CRTEffects.ScanlineColor,
            $newTheme.CRTEffects.EnableBloom,
            $newTheme.CRTEffects.BloomIntensity
        )
    }

    # Update window opacity
    if ($newTheme.Opacity -ne $null) {
        $window.Opacity = $newTheme.Opacity.WindowOpacity
    }
}

Write-Host "CRT Effects Overlay initialized" -ForegroundColor Green

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

# Add TaskManagementWidget (3-pane layout: list, context, details)
$projectManagementWidget = New-Object SuperTUI.Widgets.TaskManagementWidget
$projectManagementWidget.WidgetName = "TaskManagement"
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

# Workspace 6: Excel Integration (Import/Export/Automation)
$workspace6Layout = New-Object SuperTUI.Core.GridLayoutEngine(3, 2)
$workspace6 = New-Object SuperTUI.Core.Workspace("Excel", 6, $workspace6Layout)

# Row 0: Import widget (left) and Export widget (right)
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

# Row 1-2: Automation widget (spans both columns and 2 rows for more space)
$automationWidget = New-Object SuperTUI.Widgets.ExcelAutomationWidget
$automationWidget.WidgetName = "ExcelAutomation"
$automationWidget.Initialize()
$ws6Params3 = New-Object SuperTUI.Core.LayoutParams
$ws6Params3.Row = 1
$ws6Params3.Column = 0
$ws6Params3.ColumnSpan = 2
$ws6Params3.RowSpan = 2
$workspace6Layout.AddChild($automationWidget, $ws6Params3)
$workspace6.Widgets.Add($automationWidget)

$workspaceManager.AddWorkspace($workspace6)

Write-Host "Workspaces created!" -ForegroundColor Green

# ============================================================================
# KEYBOARD SHORTCUTS (i3-style with Windows key as $mod)
# ============================================================================

# Workspace switching (Win+1 through Win+9) - i3 style
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

    # i3-style: $mod+number (Win+number)
    $shortcutManager.RegisterGlobal($key, [System.Windows.Input.ModifierKeys]::Windows, $action, "Switch to workspace $i")

    # Keep Ctrl+number for backward compatibility
    $shortcutManager.RegisterGlobal($key, [System.Windows.Input.ModifierKeys]::Control, $action, "Switch to workspace $i (legacy)")
}

# Quit - i3 style: $mod+Shift+E
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::E,
    ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
    { $window.Close() },
    "Exit SuperTUI (i3-style)"
)

# Quit - legacy: Ctrl+Q (keep for compatibility)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Q,
    [System.Windows.Input.ModifierKeys]::Control,
    { $window.Close() },
    "Quit SuperTUI (legacy)"
)

# Close focused widget - i3 style: $mod+Shift+Q
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Q,
    ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
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
    "Close focused widget (i3-style)"
)

# Close focused widget - legacy: Ctrl+C (keep for compatibility)
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
    "Close focused widget (legacy)"
)

# Focus navigation - i3 style: $mod+h/j/k/l (Left/Down/Up/Right)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::H,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            # Navigate left (cycle backward for now, can be enhanced with real directional logic)
            $current.CycleFocusBackward()
            $focused = $current.GetFocusedWidget()
            $statusText.Text = "Focus left: $($focused?.WidgetName ?? 'None')"
        }
    },
    "Focus left (i3-style)"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::J,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            # Navigate down
            $current.CycleFocusForward()
            $focused = $current.GetFocusedWidget()
            $statusText.Text = "Focus down: $($focused?.WidgetName ?? 'None')"
        }
    },
    "Focus down (i3-style)"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::K,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            # Navigate up
            $current.CycleFocusBackward()
            $focused = $current.GetFocusedWidget()
            $statusText.Text = "Focus up: $($focused?.WidgetName ?? 'None')"
        }
    },
    "Focus up (i3-style)"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::L,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            # Navigate right (cycle forward for now)
            $current.CycleFocusForward()
            $focused = $current.GetFocusedWidget()
            $statusText.Text = "Focus right: $($focused?.WidgetName ?? 'None')"
        }
    },
    "Focus right (i3-style)"
)

# Focus navigation - legacy: Ctrl+Up/Down for cycling focus
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
    "Focus next widget (legacy)"
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
    "Focus previous widget (legacy)"
)

# Widget Picker / Command Palette - i3 style: $mod+Enter
$widgetPickerAction = {
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
                    $statusText.Text = "All slots full - close a widget first (Win+Shift+Q)"
                }
            } catch {
                $statusText.Text = "Error adding widget: $_"
                Write-Host "Error: $_" -ForegroundColor Red
            }
        }
    } else {
        $statusText.Text = "Add widget only works in Dashboard workspace"
    }
}

# i3-style: $mod+Enter (launch widget picker)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Return,
    [System.Windows.Input.ModifierKeys]::Windows,
    $widgetPickerAction,
    "Launch widget picker (i3-style)"
)

# i3-style: $mod+d (dmenu-style command palette)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::D,
    [System.Windows.Input.ModifierKeys]::Windows,
    $widgetPickerAction,
    "Command palette (i3-style dmenu)"
)

# Legacy: Ctrl+N
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::N,
    [System.Windows.Input.ModifierKeys]::Control,
    $widgetPickerAction,
    "Add new widget (legacy)"
)

# Fullscreen focused widget - i3 style: $mod+f
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::F,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $focused = $current.GetFocusedWidget()
            if ($focused) {
                # Toggle fullscreen mode (hide all other widgets, expand focused to full workspace)
                $current.ToggleFullscreen()
                if ($current.IsFullscreen) {
                    $statusText.Text = "Fullscreen: $($focused.WidgetName) (Win+F to exit)"
                } else {
                    $statusText.Text = "Exited fullscreen mode"
                }
            } else {
                $statusText.Text = "No widget focused for fullscreen"
            }
        }
    },
    "Toggle fullscreen (i3-style)"
)

# Layout mode switching - i3 style: Win+e/s/w/t/g
# Win+e → Auto mode (split based on count)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::E,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.SetLayoutMode([SuperTUI.Core.TilingMode]::Auto)
            $statusText.Text = "Layout: Auto (split based on count)"
            $logger.Info("Shortcuts", "Layout mode: Auto")
        }
    },
    "Auto layout mode (i3-style)"
)

# Win+s → Stacking mode (Master + Stack)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::S,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.SetLayoutMode([SuperTUI.Core.TilingMode]::MasterStack)
            $statusText.Text = "Layout: Stacking (master + stack)"
            $logger.Info("Shortcuts", "Layout mode: MasterStack")
        }
    },
    "Stacking layout mode (i3-style)"
)

# Win+w → Wide mode (Horizontal splits)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::W,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.SetLayoutMode([SuperTUI.Core.TilingMode]::Wide)
            $statusText.Text = "Layout: Wide (horizontal splits)"
            $logger.Info("Shortcuts", "Layout mode: Wide")
        }
    },
    "Wide layout mode (i3-style)"
)

# Win+t → Tall mode (Vertical splits)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::T,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.SetLayoutMode([SuperTUI.Core.TilingMode]::Tall)
            $statusText.Text = "Layout: Tall (vertical splits)"
            $logger.Info("Shortcuts", "Layout mode: Tall")
        }
    },
    "Tall layout mode (i3-style)"
)

# Win+g → Grid mode (Force 2x2 grid)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::G,
    [System.Windows.Input.ModifierKeys]::Windows,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.SetLayoutMode([SuperTUI.Core.TilingMode]::Grid)
            $statusText.Text = "Layout: Grid (2x2 or NxN)"
            $logger.Info("Shortcuts", "Layout mode: Grid")
        }
    },
    "Grid layout mode (i3-style)"
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

# Move widget with i3-style: $mod+Shift+h/j/k/l
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

# i3-style: $mod+Shift+h (move left)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::H,
    ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Left" },
    "Move widget left (i3-style)"
)

# i3-style: $mod+Shift+j (move down)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::J,
    ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Down" },
    "Move widget down (i3-style)"
)

# i3-style: $mod+Shift+k (move up)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::K,
    ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Up" },
    "Move widget up (i3-style)"
)

# i3-style: $mod+Shift+l (move right)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::L,
    ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Right" },
    "Move widget right (i3-style)"
)

# Legacy: Ctrl+Shift+Arrow keys
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Left,
    ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Left" },
    "Move widget left (legacy)"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Right,
    ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Right" },
    "Move widget right (legacy)"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Up,
    ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Up" },
    "Move widget up (legacy)"
)

$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Down,
    ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift),
    { & $moveWidgetScript "Down" },
    "Move widget down (legacy)"
)

# Keyboard handler
$window.Add_KeyDown({
    param($sender, $e)

    # Handle '?' key for shortcut overlay (intercept before other handlers)
    # '?' is Shift+/ which is Key.OemQuestion or Key.Oem2 with Shift modifier
    if (($e.Key -eq [System.Windows.Input.Key]::OemQuestion) -or
        ($e.Key -eq [System.Windows.Input.Key]::Oem2 -and
         ([System.Windows.Input.Keyboard]::Modifiers -band [System.Windows.Input.ModifierKeys]::Shift) -eq [System.Windows.Input.ModifierKeys]::Shift)) {

        # Toggle overlay visibility
        if ($shortcutOverlay.Visibility -eq [System.Windows.Visibility]::Visible) {
            $shortcutOverlay.Hide()
        } else {
            # Get current focused widget type if any
            $focusedWidget = $workspaceManager.CurrentWorkspace.FocusedWidget
            $widgetType = if ($focusedWidget) { $focusedWidget.WidgetType } else { $null }
            $shortcutOverlay.Show($widgetType)
        }
        $e.Handled = $true
        return
    }

    # Handle Esc key - GLOBAL reset to Normal mode
    if ($e.Key -eq [System.Windows.Input.Key]::Escape) {
        # First, close any open overlays
        if ($shortcutOverlay.Visibility -eq [System.Windows.Visibility]::Visible) {
            $shortcutOverlay.Hide()
            $e.Handled = $true
            return
        }
        if ($quickJumpOverlay.Visibility -eq [System.Windows.Visibility]::Visible) {
            $quickJumpOverlay.Hide()
            $e.Handled = $true
            return
        }

        # Reset all widgets to Normal mode (terminal-like behavior)
        if ($workspaceManager.CurrentWorkspace) {
            foreach ($widget in $workspaceManager.CurrentWorkspace.GetAllWidgets()) {
                # Set InputMode to Normal (enum value 0)
                $widget.InputMode = [SuperTUI.Core.WidgetInputMode]::Normal
            }
        }

        # Update status bar to show Normal mode
        $modeIndicator.Text = "-- NORMAL --"
        $modeIndicator.Foreground = [System.Windows.Media.Brushes]::DodgerBlue
        $statusText.Text = "Tab: Next Widget | Alt+Arrows: Navigate | Alt+Shift+Arrows: Move Widget | ?: Help | G: Quick Jump"

        $e.Handled = $true
        return
    }

    # Handle 'G' key for quick jump overlay (only if no modifiers)
    if ($e.Key -eq [System.Windows.Input.Key]::G -and
        [System.Windows.Input.Keyboard]::Modifiers -eq [System.Windows.Input.ModifierKeys]::None -and
        $quickJumpOverlay.Visibility -eq [System.Windows.Visibility]::Collapsed) {

        # Clear any existing jump targets
        $quickJumpOverlay.ClearJumps()

        # Get current focused widget and register its jump targets
        $focusedWidget = $workspaceManager.CurrentWorkspace.FocusedWidget
        if ($focusedWidget) {
            # Register context-specific jumps based on widget type
            switch ($focusedWidget.WidgetType) {
                "TaskManagement" {
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::K, "KanbanBoard", "Kanban Board (current status)")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::A, "Agenda", "Agenda (today)")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::P, "ProjectStats", "Project Stats")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::N, "Notes", "Notes (current task)")
                }
                "KanbanBoard" {
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::T, "TaskManagement", "Tasks (current item)")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::A, "Agenda", "Agenda (today)")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::P, "ProjectStats", "Project Stats")
                }
                "Agenda" {
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::T, "TaskManagement", "Tasks (current item)")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::K, "KanbanBoard", "Kanban Board")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::P, "ProjectStats", "Project Stats")
                }
                "Notes" {
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::T, "TaskManagement", "Tasks (related)")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::F, "FileExplorer", "File Explorer")
                }
                "FileExplorer" {
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::N, "Notes", "Notes (current file)")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::G, "GitStatus", "Git Status")
                }
                Default {
                    # Generic jumps available from any widget
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::T, "TaskManagement", "Tasks")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::K, "KanbanBoard", "Kanban Board")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::A, "Agenda", "Agenda")
                    $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::F, "FileExplorer", "File Explorer")
                }
            }
        } else {
            # No focused widget - show generic jumps
            $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::T, "TaskManagement", "Tasks")
            $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::K, "KanbanBoard", "Kanban Board")
            $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::A, "Agenda", "Agenda")
            $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::F, "FileExplorer", "File Explorer")
            $quickJumpOverlay.RegisterJump([System.Windows.Input.Key]::N, "Notes", "Notes")
        }

        # Show the overlay
        $quickJumpOverlay.Show()
        $e.Handled = $true
        return
    }

    # Handle other shortcuts
    $currentWorkspaceName = $workspaceManager.CurrentWorkspace.Name
    $handled = $shortcutManager.HandleKeyDown($e.Key, $e.KeyboardDevice.Modifiers, $currentWorkspaceName)

    if ($handled) {
        $e.Handled = $true
    }

    # If not handled by shortcuts, route to workspace for widget keyboard handling
    if (-not $handled -and $workspaceManager.CurrentWorkspace) {
        $workspaceManager.HandleKeyDown($e)
        if ($e.Handled) {
            $handled = $true
        }
    }
})

# ============================================================================
# STATE PERSISTENCE HOOKS
# ============================================================================

# Save state on window closing
$window.Add_Closing({
    Write-Host "Saving workspace states..." -ForegroundColor Yellow

    foreach ($ws in $workspaceManager.Workspaces) {
        try {
            $state = $ws.SaveState()
            $stateManager.SaveState("workspace_$($ws.Index)", $state)
            $logger.Debug("StateManagement", "Saved state for workspace: $($ws.Name)")
        } catch {
            $logger.Error("StateManagement", "Failed to save workspace $($ws.Name): $_")
        }
    }

    # Flush to disk
    $stateManager.Flush()
    Write-Host "Workspace states saved" -ForegroundColor Green
})

# Auto-save on workspace switch
$workspaceManager.WorkspaceChanged += {
    param($workspace)

    # Update ApplicationContext
    $appContext.CurrentWorkspace = $workspace

    # Save previous workspace state (if any)
    foreach ($ws in $workspaceManager.Workspaces) {
        if ($ws -ne $workspace) {
            try {
                $state = $ws.SaveState()
                $stateManager.SaveState("workspace_$($ws.Index)", $state)
            } catch {
                $logger.Error("StateManagement", "Failed to auto-save workspace: $_")
            }
        }
    }
}

Write-Host "State persistence hooks registered" -ForegroundColor Green

# ============================================================================
# SHOW WINDOW
# ============================================================================

Write-Host "`nStarting SuperTUI..." -ForegroundColor Green
Write-Host "i3-style Keyboard Shortcuts:" -ForegroundColor Yellow
Write-Host "  Win+1-9           Switch workspaces" -ForegroundColor Gray
Write-Host "  Win+Enter / Win+d Launcher / Command palette" -ForegroundColor Gray
Write-Host "  Win+h/j/k/l       Focus left/down/up/right" -ForegroundColor Gray
Write-Host "  Win+Shift+h/j/k/l Move widget" -ForegroundColor Gray
Write-Host "  Win+Shift+Q       Close focused widget" -ForegroundColor Gray
Write-Host "  Win+f             Toggle fullscreen" -ForegroundColor Gray
Write-Host "  Win+e/s/w/t/g     Layout: Auto/Stack/Wide/Tall/Grid" -ForegroundColor Cyan
Write-Host "  Win+Shift+E       Exit application" -ForegroundColor Gray
Write-Host "  Tab / Shift+Tab   Cycle focus" -ForegroundColor Gray
Write-Host "  ?                 Help overlay" -ForegroundColor Gray
Write-Host ""
Write-Host "Legacy shortcuts (Ctrl-based) still work for compatibility" -ForegroundColor DarkGray
Write-Host ""

$window.ShowDialog() | Out-Null
