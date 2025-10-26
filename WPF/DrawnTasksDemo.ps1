# SuperTUI Drawn Tasks Demo
# Uses WPF drawing primitives (Border, Line, Rectangle) instead of Unicode
# Result: PIXEL-PERFECT borders that look like terminal

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

# Initialize infrastructure
Write-Host "Initializing infrastructure..." -ForegroundColor Cyan
$logger = [SuperTUI.Infrastructure.Logger]::Instance
$logger.SetMinLevel([SuperTUI.Infrastructure.LogLevel]::Error)

$themeManager = [SuperTUI.Infrastructure.ThemeManager]::Instance
$themeManager.Initialize($null)
$themeManager.ApplyTheme("Dark")
Write-Host "Infrastructure ready" -ForegroundColor Green

# Create main window
[xml]$xaml = @"
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="SuperTUI - Drawn Tasks (Pixel Perfect)"
    Width="1000"
    Height="700"
    WindowStyle="None"
    ResizeMode="CanResizeWithGrip"
    Background="#000000"
    AllowsTransparency="False">

    <Grid>
        <Border
            BorderBrush="#00FF00"
            BorderThickness="2"
            Margin="0">

            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <!-- Content Area -->
                <Border Grid.Row="0" Background="#000000" Padding="0">
                    <ContentControl x:Name="ContentArea"/>
                </Border>

                <!-- Footer -->
                <Border Grid.Row="1" Background="#000000" BorderBrush="#00FF00" BorderThickness="0,1,0,0" Padding="10,5">
                    <Grid>
                        <TextBlock
                            FontFamily="Consolas,Courier New"
                            FontSize="10"
                            Foreground="#00AA00"
                            Text="DRAWN WITH WPF PRIMITIVES - PIXEL PERFECT │ CTRL+Q TO QUIT"/>

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

# Create the drawn tasks widget
$tasksWidget = New-Object SuperTUI.Widgets.DrawnTasksWidget
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
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  WPF-DRAWN TERMINAL INTERFACE (PIXEL PERFECT)" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "Using WPF Border, Line, Rectangle primitives instead of" -ForegroundColor Yellow
Write-Host "Unicode box-drawing characters." -ForegroundColor Yellow
Write-Host ""
Write-Host "Result: PERFECTLY ALIGNED borders that look like terminal!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Keyboard Controls:" -ForegroundColor Cyan
Write-Host "  ↑↓      Navigate tasks" -ForegroundColor Gray
Write-Host "  1-4     Filter (ALL/ACTIVE/PENDING/BLOCKED)" -ForegroundColor Gray
Write-Host "  ENTER   View task details" -ForegroundColor Gray
Write-Host "  CTRL+Q  Quit" -ForegroundColor Gray
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""

# Show the window
$window.ShowDialog() | Out-Null
