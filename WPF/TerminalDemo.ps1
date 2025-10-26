# SuperTUI Terminal Aesthetic Demo
# This demonstrates what a COMPLETE REDESIGN would look like
# - Pure keyboard navigation (arrows, tab, enter, esc)
# - Terminal aesthetic (green on black, box-drawing chars, monospace)
# - No mouse required
# - Matrix/Fallout/classic terminal look

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

# Create main window with NO CHROME - pure terminal
[xml]$xaml = @"
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="SuperTUI Terminal Demo"
    Width="1200"
    Height="800"
    WindowStyle="None"
    ResizeMode="CanResizeWithGrip"
    Background="#000000"
    AllowsTransparency="False">

    <Grid>
        <!-- Pure terminal interface - no WPF chrome -->
        <Border
            x:Name="MainContainer"
            BorderBrush="#00FF00"
            BorderThickness="3"
            Margin="0"
            Padding="0">

            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <!-- Terminal Header (replaces title bar) -->
                <Border Grid.Row="0" Background="#000000" Padding="15,10">
                    <Grid>
                        <TextBlock
                            x:Name="HeaderText"
                            FontFamily="Consolas,Courier New"
                            FontSize="16"
                            FontWeight="Bold"
                            Foreground="#00FF00"
                            TextAlignment="Center">
                            <Run Text="╔═══════════════════════════════════════════════════════════════╗"/>
                            <LineBreak/>
                            <Run Text="║     SUPERTUI TERMINAL INTERFACE v2.0 - KEYBOARD DRIVEN     ║"/>
                            <LineBreak/>
                            <Run Text="╚═══════════════════════════════════════════════════════════════╝"/>
                        </TextBlock>

                        <!-- Close button in corner -->
                        <TextBlock
                            x:Name="CloseButton"
                            HorizontalAlignment="Right"
                            VerticalAlignment="Top"
                            FontFamily="Consolas,Courier New"
                            FontSize="14"
                            Foreground="#00FF00"
                            Text="[X]"
                            Cursor="Hand"
                            Margin="0,-5,5,0"/>
                    </Grid>
                </Border>

                <!-- Content Area -->
                <Border Grid.Row="1" Background="#000000" Padding="20">
                    <ContentControl x:Name="ContentArea"/>
                </Border>

                <!-- Terminal Footer (status bar) -->
                <Border Grid.Row="2" Background="#000000" BorderBrush="#00FF00" BorderThickness="0,1,0,0" Padding="15,8">
                    <Grid>
                        <TextBlock
                            x:Name="StatusText"
                            FontFamily="Consolas,Courier New"
                            FontSize="11"
                            Foreground="#00AA00"
                            Text="READY │ [↑][↓][TAB] NAVIGATE │ [ENTER] SELECT │ [ESC] BACK │ [CTRL+Q] QUIT"/>

                        <TextBlock
                            x:Name="ClockText"
                            HorizontalAlignment="Right"
                            FontFamily="Consolas,Courier New"
                            FontSize="11"
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
$statusText = $window.FindName("StatusText")
$clockText = $window.FindName("ClockText")
$closeButton = $window.FindName("CloseButton")

# Close button handler
$closeButton.Add_MouseLeftButtonDown({ $window.Close() })

# Update clock
$clockTimer = New-Object System.Windows.Threading.DispatcherTimer
$clockTimer.Interval = [TimeSpan]::FromSeconds(1)
$clockTimer.Add_Tick({
    $clockText.Text = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
})
$clockTimer.Start()

# Create and add the terminal demo widget
$terminalWidget = New-Object SuperTUI.Widgets.TerminalDemoWidget
$terminalWidget.Initialize()
$contentArea.Content = $terminalWidget

# Global keyboard shortcuts
$window.Add_KeyDown({
    param($sender, $e)

    if ($e.Key -eq [System.Windows.Input.Key]::Q -and
        [System.Windows.Input.Keyboard]::Modifiers -eq [System.Windows.Input.ModifierKeys]::Control) {
        $window.Close()
        $e.Handled = $true
    }
})

# Make window draggable (for repositioning)
$window.Add_MouseLeftButtonDown({
    if ($_.Source -is [System.Windows.Controls.Border] -or
        $_.Source -is [System.Windows.Controls.Grid]) {
        try { $window.DragMove() } catch {}
    }
})

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  TERMINAL AESTHETIC DEMO" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "This demonstrates what a COMPLETE REDESIGN would look like:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  ✓ Pure keyboard navigation (no mouse needed)" -ForegroundColor Gray
Write-Host "  ✓ Terminal aesthetic (green on black)" -ForegroundColor Gray
Write-Host "  ✓ Box-drawing characters for all UI" -ForegroundColor Gray
Write-Host "  ✓ Monospace fonts throughout" -ForegroundColor Gray
Write-Host "  ✓ Matrix/Fallout/classic terminal look" -ForegroundColor Gray
Write-Host ""
Write-Host "Keyboard Controls:" -ForegroundColor Cyan
Write-Host "  ↑↓    - Navigate menu items" -ForegroundColor Gray
Write-Host "  Tab   - Cycle through items" -ForegroundColor Gray
Write-Host "  Enter - Select item" -ForegroundColor Gray
Write-Host "  Esc   - Go back" -ForegroundColor Gray
Write-Host "  Ctrl+Q- Quit application" -ForegroundColor Gray
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""

# Show the window
$window.ShowDialog() | Out-Null
