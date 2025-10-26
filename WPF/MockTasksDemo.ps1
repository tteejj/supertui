# SuperTUI Mock Tasks Screen Demo
# Demonstrates proper terminal aesthetic with CONNECTED borders
# - Single-line Unicode box characters (┌─┐│└┘├┤┬┴┼)
# - Keyboard-driven navigation
# - Fake task data for demonstration

# Platform check
if ($PSVersionTable.Platform -eq 'Unix') {
    Write-Error "SuperTUI requires Windows (WPF is Windows-only)"
    exit 1
}

# Load WPF assemblies
Write-Host "Loading WPF assemblies..." -ForegroundColor Cyan
try {
    Add-Type -AssemblyName PresentationFramework
    Add-Type -AssemblyName PresentationCore
    Add-Type -AssemblyName WindowsBase
    Add-Type -AssemblyName System.Xaml
} catch {
    Write-Error "Failed to load WPF assemblies: $_"
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

# Initialize infrastructure (required for widgets)
Write-Host "Initializing infrastructure..." -ForegroundColor Cyan
$logger = [SuperTUI.Infrastructure.Logger]::Instance
$logger.SetMinLevel([SuperTUI.Infrastructure.LogLevel]::Error)

$themeManager = [SuperTUI.Infrastructure.ThemeManager]::Instance
$themeManager.Initialize($null)
$themeManager.ApplyTheme("Dark")
Write-Host "Infrastructure ready" -ForegroundColor Green

# Create main window - NO WPF CHROME
[xml]$xaml = @"
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="SuperTUI - Mock Tasks"
    Width="1000"
    Height="600"
    WindowStyle="None"
    ResizeMode="CanResizeWithGrip"
    Background="#000000"
    AllowsTransparency="False">

    <Grid>
        <Border
            x:Name="MainContainer"
            BorderBrush="#00FF00"
            BorderThickness="2"
            Margin="0"
            Padding="0">

            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <!-- Content Area -->
                <Border Grid.Row="0" Background="#000000" Padding="10">
                    <ContentControl x:Name="ContentArea"/>
                </Border>

                <!-- Footer -->
                <Border Grid.Row="1" Background="#000000" BorderBrush="#00FF00" BorderThickness="0,1,0,0" Padding="10,5">
                    <Grid>
                        <TextBlock
                            FontFamily="Consolas,Courier New"
                            FontSize="10"
                            Foreground="#00AA00"
                            Text="SYSTEM READY │ CTRL+Q TO QUIT │ ALL NAVIGATION IS KEYBOARD-DRIVEN"/>

                        <TextBlock
                            x:Name="ClockText"
                            HorizontalAlignment="Right"
                            FontFamily="Consolas,Courier New"
                            FontSize="10"
                            Foreground="#00FF00"/>
                    </Grid>
                </Border>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

# Load XAML
$reader = [System.Xml.XmlNodeReader]::new($xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)

# Get controls
$contentArea = $window.FindName("ContentArea")
$clockText = $window.FindName("ClockText")

# Update clock
$clockTimer = New-Object System.Windows.Threading.DispatcherTimer
$clockTimer.Interval = [TimeSpan]::FromSeconds(1)
$clockTimer.Add_Tick({
    $clockText.Text = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
})
$clockTimer.Start()

# Create and add the mock tasks widget
$tasksWidget = New-Object SuperTUI.Widgets.MockTasksWidget
$tasksWidget.Initialize()
$contentArea.Content = $tasksWidget

# Global keyboard shortcuts
$window.Add_KeyDown({
    param($sender, $e)

    if ($e.Key -eq [System.Windows.Input.Key]::Q -and
        [System.Windows.Input.Keyboard]::Modifiers -eq [System.Windows.Input.ModifierKeys]::Control) {
        $window.Close()
        $e.Handled = $true
    }
})

# Make window draggable
$window.Add_MouseLeftButtonDown({
    if ($_.Source -is [System.Windows.Controls.Border] -or
        $_.Source -is [System.Windows.Controls.Grid]) {
        try { $window.DragMove() } catch {}
    }
})

Write-Host ""
Write-Host "┌─────────────────────────────────────────────────────────┐" -ForegroundColor Green
Write-Host "│         MOCK TASKS SCREEN - TERMINAL AESTHETIC         │" -ForegroundColor Green
Write-Host "├─────────────────────────────────────────────────────────┤" -ForegroundColor Green
Write-Host "│                                                         │" -ForegroundColor Green
Write-Host "│  ✓ PROPERLY CONNECTED BORDERS (single-line Unicode)    │" -ForegroundColor Gray
Write-Host "│  ✓ KEYBOARD-DRIVEN NAVIGATION (no mouse needed)        │" -ForegroundColor Gray
Write-Host "│  ✓ FAKE DATA (not tied to any services)                │" -ForegroundColor Gray
Write-Host "│  ✓ TERMINAL AESTHETIC (green on black)                 │" -ForegroundColor Gray
Write-Host "│                                                         │" -ForegroundColor Green
Write-Host "├─────────────────────────────────────────────────────────┤" -ForegroundColor Green
Write-Host "│ KEYBOARD CONTROLS:                                      │" -ForegroundColor Cyan
Write-Host "│   ↑↓      Navigate tasks                               │" -ForegroundColor Gray
Write-Host "│   1-4     Filter (ALL/ACTIVE/PENDING/BLOCKED)           │" -ForegroundColor Gray
Write-Host "│   ENTER   View task details                            │" -ForegroundColor Gray
Write-Host "│   N       New task                                      │" -ForegroundColor Gray
Write-Host "│   E       Edit task                                     │" -ForegroundColor Gray
Write-Host "│   D       Delete task                                   │" -ForegroundColor Gray
Write-Host "│   S       Sort options                                  │" -ForegroundColor Gray
Write-Host "│   CTRL+Q  Quit                                          │" -ForegroundColor Gray
Write-Host "└─────────────────────────────────────────────────────────┘" -ForegroundColor Green
Write-Host ""

# Show the window
$window.ShowDialog() | Out-Null
