# SuperTUI - Production Entry Point

# Platform check
if ($PSVersionTable.Platform -eq 'Unix') {
    Write-Error "SuperTUI requires Windows (WPF is Windows-only)"
    exit 1
}

# Load WPF assemblies (required for PowerShell Core / pwsh)
# Windows PowerShell 5.1 loads these automatically, but PowerShell 7+ does not
Write-Host "Loading WPF assemblies..." -ForegroundColor Cyan
try {
    Add-Type -AssemblyName PresentationFramework
    Add-Type -AssemblyName PresentationCore
    Add-Type -AssemblyName WindowsBase
    Add-Type -AssemblyName System.Xaml
    Write-Host "WPF assemblies loaded" -ForegroundColor Green
} catch {
    Write-Error "Failed to load WPF assemblies. Ensure you're running on Windows with .NET Desktop Runtime installed."
    Write-Error "Error: $_"
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

    <Grid x:Name="RootContainer">
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
            Background="{StaticResource TerminalBackground}">
            <Grid x:Name="WorkspacePanel"/>
        </ContentControl>

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
                    Text="Tab: Next | Ctrl+Up/Down: Focus | Ctrl+Shift+Arrows: Move | ?: Help | G: Quick Jump"
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
$rootContainer = $window.FindName("RootContainer")
$workspaceContainer = $window.FindName("WorkspaceContainer")
$workspacePanel = $window.FindName("WorkspacePanel")
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
# INITIALIZE INFRASTRUCTURE WITH DEPENDENCY INJECTION
# ============================================================================

Write-Host "Initializing SuperTUI infrastructure with DI..." -ForegroundColor Cyan

# Initialize ServiceContainer and register all services
Write-Host "Setting up ServiceContainer..." -ForegroundColor Cyan
$supertuiDataDir = Join-Path $PSScriptRoot ".supertui"
if (-not (Test-Path $supertuiDataDir)) {
    New-Item -Path $supertuiDataDir -ItemType Directory -Force | Out-Null
}
$configPath = Join-Path $supertuiDataDir "config.json"
$serviceContainer = [SuperTUI.DI.ServiceRegistration]::RegisterAllServices($configPath, $null)
Write-Host "ServiceContainer initialized and services registered" -ForegroundColor Green
Write-Host "  Config location: $configPath" -ForegroundColor Gray

# Get services from container (already initialized by RegisterAllServices)
$logger = $serviceContainer.GetService([SuperTUI.Infrastructure.ILogger])
$configManager = $serviceContainer.GetService([SuperTUI.Infrastructure.IConfigurationManager])
$themeManager = $serviceContainer.GetService([SuperTUI.Infrastructure.IThemeManager])
$securityManager = $serviceContainer.GetService([SuperTUI.Infrastructure.ISecurityManager])
$errorHandler = $serviceContainer.GetService([SuperTUI.Infrastructure.IErrorHandler])
$eventBus = $serviceContainer.GetService([SuperTUI.Core.IEventBus])
$stateManager = $serviceContainer.GetService([SuperTUI.Infrastructure.IStatePersistenceManager])
Write-Host "Core services resolved from container" -ForegroundColor Green

# Enable DEBUG level logging
$logger.SetMinLevel([SuperTUI.Infrastructure.LogLevel]::Debug)
Write-Host "Logger set to DEBUG level" -ForegroundColor Yellow

# Add file log sink to supertui folder
$logPath = Join-Path $supertuiDataDir "logs"
if (-not (Test-Path $logPath)) {
    New-Item -Path $logPath -ItemType Directory -Force | Out-Null
}
$fileLogSink = New-Object SuperTUI.Infrastructure.FileLogSink($logPath, "SuperTUI")
$logger.AddSink($fileLogSink)
Write-Host "File log sink added: $logPath\SuperTUI_*.log" -ForegroundColor Green

# Apply saved theme (or default to "Cyberpunk" to showcase new effects)
$savedThemeName = $configManager.Get("UI.Theme", "Cyberpunk")
$themeManager.ApplyTheme($savedThemeName)
Write-Host "Applied theme: $savedThemeName" -ForegroundColor Green

# Get ApplicationContext (singleton, not in DI container)
$appContext = [SuperTUI.Infrastructure.ApplicationContext]::Instance
Write-Host "ApplicationContext initialized" -ForegroundColor Green

# Create WidgetFactory for dependency injection
$widgetFactory = New-Object SuperTUI.DI.WidgetFactory($serviceContainer)
Write-Host "WidgetFactory created" -ForegroundColor Green

# Register all available widgets in WidgetFactory
# Using the non-generic RegisterWidget(Type, string) method for PowerShell compatibility
Write-Host "Registering widgets..." -ForegroundColor Cyan
try {
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.ClockWidget], "SuperTUI.Widgets.ClockWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.CounterWidget], "SuperTUI.Widgets.CounterWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.TaskSummaryWidget], "SuperTUI.Widgets.TaskSummaryWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.TaskManagementWidget], "SuperTUI.Widgets.TaskManagementWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.TaskManagementWidget_TUI], "SuperTUI.Widgets.TaskManagementWidget_TUI")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.KanbanBoardWidget], "SuperTUI.Widgets.KanbanBoardWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.AgendaWidget], "SuperTUI.Widgets.AgendaWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.ProjectStatsWidget], "SuperTUI.Widgets.ProjectStatsWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.NotesWidget], "SuperTUI.Widgets.NotesWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.FileExplorerWidget], "SuperTUI.Widgets.FileExplorerWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.ExcelImportWidget], "SuperTUI.Widgets.ExcelImportWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.ExcelExportWidget], "SuperTUI.Widgets.ExcelExportWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.ExcelMappingEditorWidget], "SuperTUI.Widgets.ExcelMappingEditorWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.CommandPaletteWidget], "SuperTUI.Widgets.CommandPaletteWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.SettingsWidget], "SuperTUI.Widgets.SettingsWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.ShortcutHelpWidget], "SuperTUI.Widgets.ShortcutHelpWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.TimeTrackingWidget], "SuperTUI.Widgets.TimeTrackingWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.RetroTaskManagementWidget], "SuperTUI.Widgets.RetroTaskManagementWidget")
    $widgetFactory.RegisterWidget([SuperTUI.Widgets.TUIDemoWidget], "SuperTUI.Widgets.TUIDemoWidget")
    Write-Host "Widgets registered in factory (19 widgets)" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to register widgets: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    throw
}

Write-Host "Infrastructure initialized with DI" -ForegroundColor Green

# ============================================================================
# SHORTCUT OVERLAY
# ============================================================================

# Create and initialize the shortcut overlay
$shortcutOverlay = New-Object SuperTUI.Core.Components.ShortcutOverlay(
    $themeManager,
    [SuperTUI.Core.ShortcutManager]::Instance,
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
$themeManager.add_ThemeChanged({
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
})

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

# Initialize OverlayManager with root container and workspace panel (Grid inside ContentControl)
Write-Host "Initializing OverlayManager..." -ForegroundColor Cyan
$overlayManager = [SuperTUI.Core.Services.OverlayManager]::Instance
$overlayManager.Initialize($rootContainer, $workspacePanel)
Write-Host "OverlayManager initialized" -ForegroundColor Green

# Create WorkspaceManager
$workspaceManager = New-Object SuperTUI.Core.WorkspaceManager($workspaceContainer)

# Create ShortcutManager
$shortcutManager = New-Object SuperTUI.Core.ShortcutManager

# ============================================================================
# DEFINE WORKSPACES
# ============================================================================

Write-Host "Setting up workspaces..." -ForegroundColor Cyan

# Workspace 1: Dashboard (Flexible i3-like layout)
$workspace1Layout = New-Object SuperTUI.Core.DashboardLayoutEngine($logger, $themeManager)
$workspace1 = New-Object SuperTUI.Core.Workspace("Dashboard", 1, $workspace1Layout, $logger, $themeManager)

# Add Clock widget to slot 0 (top-left)
$clockWidget = $widgetFactory.CreateWidget("SuperTUI.Widgets.ClockWidget")
$clockWidget.WidgetName = "Clock"
$clockWidget.Initialize()
$workspace1Layout.SetWidget(0, $clockWidget)
$workspace1.Widgets.Add($clockWidget)

# Add TaskSummary widget to slot 1 (top-right)
$taskSummary = $widgetFactory.CreateWidget("SuperTUI.Widgets.TaskSummaryWidget")
$taskSummary.WidgetName = "TaskSummary"
$taskSummary.Initialize()
$workspace1Layout.SetWidget(1, $taskSummary)
$workspace1.Widgets.Add($taskSummary)

# Slots 2 and 3 are empty (will show "Empty Slot" placeholders)

$workspaceManager.AddWorkspace($workspace1)

# Workspace 2: Projects (Full Project Management)
Write-Host "Creating Workspace 2: TaskManagement..." -ForegroundColor Cyan
$workspace2Layout = New-Object SuperTUI.Core.StackLayoutEngine([System.Windows.Controls.Orientation]::Vertical)
$workspace2 = New-Object SuperTUI.Core.Workspace("Projects", 2, $workspace2Layout, $logger, $themeManager)
Write-Host "  Layout engine created: StackLayoutEngine" -ForegroundColor Gray

# Add TaskManagementWidget_TUI (NEW clean TUI-styled version)
Write-Host "  Creating TaskManagementWidget_TUI..." -ForegroundColor Gray
try {
    $projectManagementWidget = $widgetFactory.CreateWidget("SuperTUI.Widgets.TaskManagementWidget_TUI")
    Write-Host "    Widget created successfully" -ForegroundColor Green
    Write-Host "    Widget type: $($projectManagementWidget.GetType().Name)" -ForegroundColor Gray

    $projectManagementWidget.WidgetName = "Tasks"
    Write-Host "  Initializing widget..." -ForegroundColor Gray
    $projectManagementWidget.Initialize()
    Write-Host "    Widget initialized (NEW TUI-styled version)" -ForegroundColor Green

    $ws2Params = New-Object SuperTUI.Core.LayoutParams
    Write-Host "  Adding widget to workspace..." -ForegroundColor Gray
    $workspace2.AddWidget($projectManagementWidget, $ws2Params)
    Write-Host "    Widget added to workspace with ErrorBoundary" -ForegroundColor Green
} catch {
    Write-Host "    ERROR creating TaskManagementWidget_TUI: $_" -ForegroundColor Red
    Write-Host "    Exception type: $($_.Exception.GetType().FullName)" -ForegroundColor Red
    Write-Host "    Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host "    Inner exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
}

$workspaceManager.AddWorkspace($workspace2)
Write-Host "Workspace 2 created and added to manager" -ForegroundColor Green

# Workspace 3: Kanban Board (3-column task board)
$workspace3Layout = New-Object SuperTUI.Core.StackLayoutEngine([System.Windows.Controls.Orientation]::Vertical)
$workspace3 = New-Object SuperTUI.Core.Workspace("Kanban", 3, $workspace3Layout, $logger, $themeManager)

# Add KanbanBoardWidget (Todo, In Progress, Done columns)
$kanbanWidget = $widgetFactory.CreateWidget("SuperTUI.Widgets.KanbanBoardWidget")
$kanbanWidget.WidgetName = "KanbanBoard"
$kanbanWidget.Initialize()
$ws3Params = New-Object SuperTUI.Core.LayoutParams
$workspace3Layout.AddChild($kanbanWidget, $ws3Params)
$workspace3.Widgets.Add($kanbanWidget)

$workspaceManager.AddWorkspace($workspace3)

# Workspace 4: Agenda (Time-grouped task view)
$workspace4Layout = New-Object SuperTUI.Core.StackLayoutEngine([System.Windows.Controls.Orientation]::Vertical)
$workspace4 = New-Object SuperTUI.Core.Workspace("Agenda", 4, $workspace4Layout, $logger, $themeManager)

# Add AgendaWidget (Overdue, Today, Tomorrow, This Week, Later, No Due Date)
$agendaWidget = $widgetFactory.CreateWidget("SuperTUI.Widgets.AgendaWidget")
$agendaWidget.WidgetName = "Agenda"
$agendaWidget.Initialize()
$ws4Params = New-Object SuperTUI.Core.LayoutParams
$workspace4Layout.AddChild($agendaWidget, $ws4Params)
$workspace4.Widgets.Add($agendaWidget)

$workspaceManager.AddWorkspace($workspace4)

# Workspace 5: Project Analytics (Stats and metrics)
$workspace5Layout = New-Object SuperTUI.Core.StackLayoutEngine([System.Windows.Controls.Orientation]::Vertical)
$workspace5 = New-Object SuperTUI.Core.Workspace("Analytics", 5, $workspace5Layout, $logger, $themeManager)

# Add ProjectStatsWidget (Metrics, charts, recent activity)
$statsWidget = $widgetFactory.CreateWidget("SuperTUI.Widgets.ProjectStatsWidget")
$statsWidget.WidgetName = "ProjectStats"
$statsWidget.Initialize()
$ws5Params = New-Object SuperTUI.Core.LayoutParams
$workspace5Layout.AddChild($statsWidget, $ws5Params)
$workspace5.Widgets.Add($statsWidget)

$workspaceManager.AddWorkspace($workspace5)

# ============================================================================
# Workspace 6: Excel Integration (Import, Export, Mapping Editor)
# ============================================================================

Write-Host "Setting up Workspace 6: Excel Integration..." -ForegroundColor Cyan

$workspace6Layout = New-Object SuperTUI.Core.GridLayoutEngine(2, 2)
$workspace6 = New-Object SuperTUI.Core.Workspace("Excel", 6, $workspace6Layout, $logger, $themeManager)

# Top-left: Excel Import Widget
$excelImportWidget = $widgetFactory.CreateWidget("SuperTUI.Widgets.ExcelImportWidget")
$excelImportWidget.WidgetName = "Excel Import"
$excelImportWidget.Initialize()
$ws6ImportParams = New-Object SuperTUI.Core.LayoutParams
$ws6ImportParams.Row = 0
$ws6ImportParams.Column = 0
$workspace6Layout.AddChild($excelImportWidget, $ws6ImportParams)
$workspace6.Widgets.Add($excelImportWidget)

# Top-right: Excel Export Widget
$excelExportWidget = $widgetFactory.CreateWidget("SuperTUI.Widgets.ExcelExportWidget")
$excelExportWidget.WidgetName = "Excel Export"
$excelExportWidget.Initialize()
$ws6ExportParams = New-Object SuperTUI.Core.LayoutParams
$ws6ExportParams.Row = 0
$ws6ExportParams.Column = 1
$workspace6Layout.AddChild($excelExportWidget, $ws6ExportParams)
$workspace6.Widgets.Add($excelExportWidget)

# Bottom (spans both columns): Excel Mapping Editor
$excelMappingWidget = $widgetFactory.CreateWidget("SuperTUI.Widgets.ExcelMappingEditorWidget")
$excelMappingWidget.WidgetName = "Mapping Editor"
$excelMappingWidget.Initialize()
$ws6MappingParams = New-Object SuperTUI.Core.LayoutParams
$ws6MappingParams.Row = 1
$ws6MappingParams.Column = 0
$ws6MappingParams.RowSpan = 1
$ws6MappingParams.ColumnSpan = 2
$workspace6Layout.AddChild($excelMappingWidget, $ws6MappingParams)
$workspace6.Widgets.Add($excelMappingWidget)

$workspaceManager.AddWorkspace($workspace6)

Write-Host "Workspaces created!" -ForegroundColor Green

# ============================================================================
# STATE RESTORATION
# ============================================================================

Write-Host "`nRestoring workspace states..." -ForegroundColor Cyan

foreach ($ws in $workspaceManager.Workspaces) {
    try {
        # Try to load saved state for this workspace
        $stateKey = "workspace_$($ws.Index)"
        $savedState = $null

        # StatePersistenceManager doesn't have a simple Get method, so check file directly
        $stateFile = Join-Path $env:LOCALAPPDATA "SuperTUI\state\$stateKey.json"

        if (Test-Path $stateFile) {
            $savedState = Get-Content $stateFile -Raw | ConvertFrom-Json
            $logger.Debug("StateManagement", "Found saved state for workspace: $($ws.Name)")

            # Level 1: Restore widget presence (recreate widgets)
            if ($savedState.Widgets -and $savedState.Widgets.Count -gt 0) {
                Write-Host "  Restoring $($savedState.Widgets.Count) widgets to workspace: $($ws.Name)" -ForegroundColor Gray

                foreach ($widgetState in $savedState.Widgets) {
                    try {
                        # Get widget type
                        $widgetTypeName = $widgetState.WidgetType
                        if (-not $widgetTypeName) {
                            $logger.Warn("StateManagement", "Widget state missing WidgetType, skipping")
                            continue
                        }

                        # Try to get the Type
                        $widgetType = [Type]::GetType($widgetTypeName)
                        if (-not $widgetType) {
                            $logger.Warn("StateManagement", "Widget type not found: $widgetTypeName (widget may have been removed)")
                            continue
                        }

                        # Create widget via WidgetFactory (DI)
                        $widget = $widgetFactory.CreateWidget($widgetType)

                        if ($widget) {
                            # Initialize widget
                            $widget.Initialize()

                            # Level 2: Restore widget position (if layout supports it)
                            # For now, just add to workspace - layout engines will position automatically
                            # TODO: Restore specific grid slots for DashboardLayoutEngine

                            # Add to workspace with layout params
                            $layoutParams = New-Object SuperTUI.Core.LayoutParams
                            $ws.AddWidget($widget, $layoutParams)

                            $logger.Debug("StateManagement", "Restored widget: $($widget.WidgetName)")
                        }
                    }
                    catch {
                        $logger.Error("StateManagement", "Failed to restore widget: $($widgetState.WidgetType) - $_")
                    }
                }

                Write-Host "    Restored widgets successfully" -ForegroundColor Green
            }
            else {
                $logger.Debug("StateManagement", "No widgets in saved state for workspace: $($ws.Name)")
            }
        }
        else {
            $logger.Debug("StateManagement", "No saved state found for workspace: $($ws.Name)")
        }
    }
    catch {
        $logger.Error("StateManagement", "Failed to restore workspace $($ws.Name): $_")
    }
}

Write-Host "State restoration complete`n" -ForegroundColor Green

# Switch to the first workspace (important: must be done before ShowDialog())
Write-Host "Switching to initial workspace..." -ForegroundColor Cyan
$workspaceManager.SwitchToWorkspace(1)
$current = $workspaceManager.CurrentWorkspace
if ($current) {
    $workspaceTitle.Text = "SuperTUI - $($current.Name)"
    Write-Host "Initialized on workspace: $($current.Name)" -ForegroundColor Green
} else {
    Write-Error "Failed to switch to initial workspace!"
    exit 1
}

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

    # REMOVED: i3-style Win+number shortcuts do not work on Windows
    # Windows OS intercepts Win+1-9 for taskbar app switching
    # $shortcutManager.RegisterGlobal($key, [System.Windows.Input.ModifierKeys]::Windows, $action, "Switch to workspace $i")

    # Use Ctrl+number instead (actually works)
    $shortcutManager.RegisterGlobal($key, [System.Windows.Input.ModifierKeys]::Control, $action, "Switch to workspace $i")
}

# REMOVED: Quit - i3 style Win+Shift+E does not work on Windows
# Windows OS intercepts Win+E for File Explorer
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::E,
#     ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
#     { $window.Close() },
#     "Exit SuperTUI (i3-style)"
# )

# Quit - Use Ctrl+Q (actually works)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::Q,
    [System.Windows.Input.ModifierKeys]::Control,
    { $window.Close() },
    "Quit SuperTUI"
)

# REMOVED: Close focused widget - i3 style Win+Shift+Q does not work
# Windows OS may intercept Win+Q combinations
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::Q,
#     ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             $focused = $current.GetFocusedWidget()
#             if ($focused) {
#                 $widgetName = $focused.WidgetName
#                 $current.RemoveFocusedWidget()
#                 $statusText.Text = "Closed widget: $widgetName"
#                 $logger.Info("Workspace", "Closed widget: $widgetName")
#             } else {
#                 $statusText.Text = "No widget focused"
#             }
#         }
#     },
#     "Close focused widget (i3-style)"
# )

# Close focused widget - Use Ctrl+W (standard Windows close, actually works)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::W,
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

# REMOVED: Focus navigation - i3 style Win+h/j/k/l does not work on Windows
# Windows OS intercepts Win+H (dictation), Win+L (lock screen), etc.
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::H,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             # Navigate left (cycle backward for now, can be enhanced with real directional logic)
#             $current.CycleFocusBackward()
#             $focused = $current.GetFocusedWidget()
#             $statusText.Text = "Focus left: $($focused?.WidgetName ?? 'None')"
#         }
#     },
#     "Focus left (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::J,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             # Navigate down
#             $current.CycleFocusForward()
#             $focused = $current.GetFocusedWidget()
#             $statusText.Text = "Focus down: $($focused?.WidgetName ?? 'None')"
#         }
#     },
#     "Focus down (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::K,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             # Navigate up
#             $current.CycleFocusBackward()
#             $focused = $current.GetFocusedWidget()
#             $statusText.Text = "Focus up: $($focused?.WidgetName ?? 'None')"
#         }
#     },
#     "Focus up (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::L,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             # Navigate right (cycle forward for now)
#             $current.CycleFocusForward()
#             $focused = $current.GetFocusedWidget()
#             $statusText.Text = "Focus right: $($focused?.WidgetName ?? 'None')"
#         }
#     },
#     "Focus right (i3-style)"
# )

# Focus navigation - Use Ctrl+Up/Down for cycling focus (actually works)
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

                $widget = $widgetFactory.CreateWidget($widgetType)
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

# REMOVED: i3-style Win+Enter does not work on Windows (typically does nothing or launches Windows action)
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::Return,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     $widgetPickerAction,
#     "Launch widget picker (i3-style)"
# )

# REMOVED: i3-style Win+D does not work on Windows
# Windows OS intercepts Win+D for "Show Desktop"
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::D,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     $widgetPickerAction,
#     "Command palette (i3-style dmenu)"
# )

# Use Ctrl+N for widget picker (actually works)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::N,
    [System.Windows.Input.ModifierKeys]::Control,
    $widgetPickerAction,
    "Add new widget"
)

# REMOVED: Fullscreen focused widget - i3 style Win+F does not work on Windows
# Windows OS intercepts Win+F for Feedback Hub
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::F,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             $focused = $current.GetFocusedWidget()
#             if ($focused) {
#                 # Toggle fullscreen mode (hide all other widgets, expand focused to full workspace)
#                 $current.ToggleFullscreen()
#                 if ($current.IsFullscreen) {
#                     $statusText.Text = "Fullscreen: $($focused.WidgetName) (Win+F to exit)"
#                 } else {
#                     $statusText.Text = "Exited fullscreen mode"
#                 }
#             } else {
#                 $statusText.Text = "No widget focused for fullscreen"
#             }
#         }
#     },
#     "Toggle fullscreen (i3-style)"
# )

# Use F11 for fullscreen (standard Windows convention, actually works)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::F11,
    [System.Windows.Input.ModifierKeys]::None,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $focused = $current.GetFocusedWidget()
            if ($focused) {
                # Toggle fullscreen mode (hide all other widgets, expand focused to full workspace)
                $current.ToggleFullscreen()
                if ($current.IsFullscreen) {
                    $statusText.Text = "Fullscreen: $($focused.WidgetName) (F11 to exit)"
                } else {
                    $statusText.Text = "Exited fullscreen mode"
                }
            } else {
                $statusText.Text = "No widget focused for fullscreen"
            }
        }
    },
    "Toggle fullscreen"
)

# REMOVED: Layout mode switching - i3 style Win+e/s/w/t/g does not work on Windows
# Windows OS intercepts Win+E (File Explorer), Win+S (Search), Win+W (Widgets), Win+T (Taskbar), Win+G (Game Bar)
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::E,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             $current.SetLayoutMode([SuperTUI.Core.TilingMode]::Auto)
#             $statusText.Text = "Layout: Auto (split based on count)"
#             $logger.Info("Shortcuts", "Layout mode: Auto")
#         }
#     },
#     "Auto layout mode (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::S,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             $current.SetLayoutMode([SuperTUI.Core.TilingMode]::MasterStack)
#             $statusText.Text = "Layout: Stacking (master + stack)"
#             $logger.Info("Shortcuts", "Layout mode: MasterStack")
#         }
#     },
#     "Stacking layout mode (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::W,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             $current.SetLayoutMode([SuperTUI.Core.TilingMode]::Wide)
#             $statusText.Text = "Layout: Wide (horizontal splits)"
#             $logger.Info("Shortcuts", "Layout mode: Wide")
#         }
#     },
#     "Wide layout mode (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::T,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             $current.SetLayoutMode([SuperTUI.Core.TilingMode]::Tall)
#             $statusText.Text = "Layout: Tall (vertical splits)"
#             $logger.Info("Shortcuts", "Layout mode: Tall")
#         }
#     },
#     "Tall layout mode (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::G,
#     [System.Windows.Input.ModifierKeys]::Windows,
#     {
#         $current = $workspaceManager.CurrentWorkspace
#         if ($current) {
#             $current.SetLayoutMode([SuperTUI.Core.TilingMode]::Grid)
#             $statusText.Text = "Layout: Grid (2x2 or NxN)"
#             $logger.Info("Shortcuts", "Layout mode: Grid")
#         }
#     },
#     "Grid layout mode (i3-style)"
# )

# Use Ctrl+L followed by letter for layout modes (actually works, vim-style leader key)
$shortcutManager.RegisterGlobal(
    [System.Windows.Input.Key]::A,
    [System.Windows.Input.ModifierKeys]::Control,
    {
        $current = $workspaceManager.CurrentWorkspace
        if ($current) {
            $current.SetLayoutMode([SuperTUI.Core.TilingMode]::Auto)
            $statusText.Text = "Layout: Auto (split based on count)"
            $logger.Info("Shortcuts", "Layout mode: Auto")
        }
    },
    "Auto layout mode"
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

# REMOVED: i3-style Win+Shift+h/j/k/l does not work on Windows
# Windows OS intercepts Win+H (dictation), Win+L (lock screen), etc.
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::H,
#     ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
#     { & $moveWidgetScript "Left" },
#     "Move widget left (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::J,
#     ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
#     { & $moveWidgetScript "Down" },
#     "Move widget down (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::K,
#     ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
#     { & $moveWidgetScript "Up" },
#     "Move widget up (i3-style)"
# )
#
# $shortcutManager.RegisterGlobal(
#     [System.Windows.Input.Key]::L,
#     ([System.Windows.Input.ModifierKeys]::Windows -bor [System.Windows.Input.ModifierKeys]::Shift),
#     { & $moveWidgetScript "Right" },
#     "Move widget right (i3-style)"
# )

# Use Ctrl+Shift+Arrow keys (actually works)
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
        $statusText.Text = "Tab: Next | Ctrl+Up/Down: Focus | Ctrl+Shift+Arrows: Move | ?: Help | G: Quick Jump"

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

    try {
        # Capture state from all workspaces
        $snapshot = $stateManager.CaptureState($workspaceManager)

        # Save the snapshot to disk
        $stateManager.SaveState($snapshot, $false)

        $logger.Debug("StateManagement", "Saved state for all workspaces")
        Write-Host "Workspace states saved" -ForegroundColor Green
    } catch {
        $logger.Error("StateManagement", "Failed to save workspace states: $_")
        Write-Host "Failed to save workspace states: $_" -ForegroundColor Red
    }
})

# Auto-save on workspace switch
$workspaceManager.add_WorkspaceChanged({
    param($workspace)

    # Update ApplicationContext
    $appContext.CurrentWorkspace = $workspace

    # Auto-save all workspace states when switching
    try {
        $snapshot = $stateManager.CaptureState($workspaceManager)
        $stateManager.SaveState($snapshot, $false)
        $logger.Debug("StateManagement", "Auto-saved workspace states on switch to: $($workspace.Name)")
    } catch {
        $logger.Error("StateManagement", "Failed to auto-save workspace states: $_")
    }
})

Write-Host "State persistence hooks registered" -ForegroundColor Green

# ============================================================================
# SHOW WINDOW
# ============================================================================

Write-Host "`nStarting SuperTUI..." -ForegroundColor Green
Write-Host "Keyboard Shortcuts:" -ForegroundColor Yellow
Write-Host "  Ctrl+1-9              Switch workspaces" -ForegroundColor Gray
Write-Host "  Ctrl+N                Add widget / Command palette" -ForegroundColor Gray
Write-Host "  Ctrl+Up/Down          Focus previous/next widget" -ForegroundColor Gray
Write-Host "  Ctrl+Shift+Arrows     Move widget" -ForegroundColor Gray
Write-Host "  Ctrl+W                Close focused widget" -ForegroundColor Gray
Write-Host "  F11                   Toggle fullscreen" -ForegroundColor Gray
Write-Host "  Ctrl+A                Layout: Auto mode" -ForegroundColor Cyan
Write-Host "  Ctrl+Q                Exit application" -ForegroundColor Gray
Write-Host "  Tab / Shift+Tab       Cycle focus" -ForegroundColor Gray
Write-Host "  ?                     Help overlay" -ForegroundColor Gray
Write-Host ""
Write-Host "NOTE: Windows key shortcuts removed (conflict with Windows OS)" -ForegroundColor DarkGray
Write-Host ""

$window.ShowDialog() | Out-Null
