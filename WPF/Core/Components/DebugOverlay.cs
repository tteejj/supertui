using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Debug overlay showing performance metrics, active state, and diagnostics
    /// Toggle with Ctrl+Shift+D
    /// </summary>
    public class DebugOverlay : Border
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IPerformanceMonitor performanceMonitor;
        private readonly PaneWorkspaceManager workspaceManager;

        private TextBlock debugText;
        private DispatcherTimer updateTimer;
        private Stopwatch frameTimer;
        private int frameCount;
        private double currentFps;

        public DebugOverlay(
            ILogger logger,
            IThemeManager themeManager,
            IPerformanceMonitor performanceMonitor,
            PaneWorkspaceManager workspaceManager)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            this.workspaceManager = workspaceManager ?? throw new ArgumentNullException(nameof(workspaceManager));

            frameTimer = Stopwatch.StartNew();
            BuildUI();
            StartUpdateTimer();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            // Semi-transparent background
            Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0));
            BorderBrush = new SolidColorBrush(theme.Warning);
            BorderThickness = new Thickness(2);
            CornerRadius = new CornerRadius(8);
            Padding = new Thickness(16, 12, 16, 12);
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = new Thickness(0, 60, 16, 0);

            // Debug info text
            debugText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Text = "Debug Mode\nInitializing..."
            };

            Child = debugText;
        }

        private void StartUpdateTimer()
        {
            // Update debug info every 500ms
            updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            updateTimer.Tick += UpdateDebugInfo;
            updateTimer.Start();
        }

        private void UpdateDebugInfo(object sender, EventArgs e)
        {
            // Calculate FPS
            frameCount++;
            if (frameTimer.ElapsedMilliseconds >= 1000)
            {
                currentFps = frameCount / (frameTimer.ElapsedMilliseconds / 1000.0);
                frameCount = 0;
                frameTimer.Restart();
            }

            // Get process memory
            var process = Process.GetCurrentProcess();
            var memoryMb = process.WorkingSet64 / (1024.0 * 1024.0);

            // Get current workspace info
            var currentWorkspace = workspaceManager.CurrentWorkspaceIndex + 1;

            // Build debug text
            debugText.Text = $@"🐛 DEBUG MODE (Ctrl+Shift+D to hide)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
WORKSPACE:
  Current: Workspace {currentWorkspace}

PERFORMANCE:
  FPS: {currentFps:F1}
  Memory: {memoryMb:F1} MB
  Uptime: {TimeSpan.FromMilliseconds(Environment.TickCount64):hh\\:mm\\:ss}

THEME:
  Name: {themeManager.CurrentTheme.Name}

SHORTCUTS:
  Ctrl+Shift+D - Hide debug overlay
  Ctrl+Shift+Q - Close pane
  : (colon) - Command palette
  ? - Help overlay
  F12 - Move pane mode
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
        }

        public void Stop()
        {
            updateTimer?.Stop();
            updateTimer = null;
        }
    }
}
