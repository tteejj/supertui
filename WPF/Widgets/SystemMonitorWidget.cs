using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Events;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Widget that displays real-time system resource usage (CPU, RAM, Network).
    /// Updates every second, publishes SystemResourcesChangedEvent.
    /// </summary>
    public class SystemMonitorWidget : WidgetBase, IThemeable
    {
        private DispatcherTimer updateTimer;
        private System.Diagnostics.PerformanceCounter cpuCounter;
        private System.Diagnostics.PerformanceCounter ramCounter;
        private System.Diagnostics.PerformanceCounter networkSentCounter;
        private System.Diagnostics.PerformanceCounter networkReceivedCounter;

        private TextBlock cpuLabel;
        private TextBlock cpuValue;
        private ProgressBar cpuBar;

        private TextBlock ramLabel;
        private TextBlock ramValue;
        private ProgressBar ramBar;

        private TextBlock networkLabel;
        private TextBlock networkValue;

        private float lastCpu = 0f;
        private float lastRam = 0f;
        private long lastNetworkSent = 0;
        private long lastNetworkReceived = 0;

        private const int UPDATE_INTERVAL_MS = 1000;

        public SystemMonitorWidget()
        {
            WidgetName = "System Monitor";
        }

        public override void Initialize()
        {
            try
            {
                // Initialize performance counters
                cpuCounter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");

                // Get available RAM counter
                ramCounter = new System.Diagnostics.PerformanceCounter("Memory", "Available MBytes");

                // Network counters (first network interface)
                var category = new System.Diagnostics.PerformanceCounterCategory("Network Interface");
                var instanceNames = category.GetInstanceNames();
                if (instanceNames.Length > 0)
                {
                    string instanceName = instanceNames[0];
                    networkSentCounter = new System.Diagnostics.PerformanceCounter("Network Interface", "Bytes Sent/sec", instanceName);
                    networkReceivedCounter = new System.Diagnostics.PerformanceCounter("Network Interface", "Bytes Received/sec", instanceName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("SystemMonitor", $"Failed to initialize performance counters: {ex.Message}");
            }

            BuildUI();

            // Initial read (first call to NextValue returns 0)
            try
            {
                cpuCounter?.NextValue();
                ramCounter?.NextValue();
                networkSentCounter?.NextValue();
                networkReceivedCounter?.NextValue();
            }
            catch { }

            // Start update timer
            updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(UPDATE_INTERVAL_MS)
            };
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        private void BuildUI()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(10),
                Background = new SolidColorBrush(theme.Background)
            };

            // Title
            var title = new TextBlock
            {
                Text = "SYSTEM RESOURCES",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(title);

            // CPU Section
            AddResourceSection(stackPanel, "CPU", out cpuLabel, out cpuValue, out cpuBar, theme);

            // RAM Section
            AddResourceSection(stackPanel, "RAM", out ramLabel, out ramValue, out ramBar, theme);

            // Network Section
            var networkPanel = new StackPanel { Margin = new Thickness(0, 15, 0, 0) };

            networkLabel = new TextBlock
            {
                Text = "NETWORK",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Info),
                Margin = new Thickness(0, 0, 0, 5)
            };
            networkPanel.Children.Add(networkLabel);

            networkValue = new TextBlock
            {
                Text = "↓ 0.0 KB/s  ↑ 0.0 KB/s",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 0)
            };
            networkPanel.Children.Add(networkValue);

            stackPanel.Children.Add(networkPanel);

            // Legend
            var legend = new TextBlock
            {
                Text = "Updates every second",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 15, 0, 0),
                Opacity = 0.7
            };
            stackPanel.Children.Add(legend);

            Content = stackPanel;
        }

        private void AddResourceSection(StackPanel parent, string label,
            out TextBlock labelBlock, out TextBlock valueBlock, out ProgressBar bar, Theme theme)
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

            labelBlock = new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Info),
                Width = 50
            };
            headerPanel.Children.Add(labelBlock);

            valueBlock = new TextBlock
            {
                Text = "0.0%",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            headerPanel.Children.Add(valueBlock);

            section.Children.Add(headerPanel);

            bar = new ProgressBar
            {
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Margin = new Thickness(0, 5, 0, 0),
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Success),
                BorderThickness = new Thickness(0)
            };
            section.Children.Add(bar);

            parent.Children.Add(section);
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateStats();
        }

        private void UpdateStats()
        {
            try
            {
                var theme = ThemeManager.Instance.CurrentTheme;

                // Update CPU
                if (cpuCounter != null)
                {
                    lastCpu = cpuCounter.NextValue();
                    cpuValue.Text = $"{lastCpu:F1}%";
                    cpuBar.Value = lastCpu;

                    // Color based on usage
                    cpuBar.Foreground = new SolidColorBrush(GetUsageColor(lastCpu, theme));
                }

                // Update RAM
                if (ramCounter != null)
                {
                    float availableMB = ramCounter.NextValue();

                    // Get total physical memory (Windows-specific, approximate)
                    var totalMemory = GetTotalPhysicalMemory();
                    float usedMB = totalMemory - availableMB;
                    float usedPercent = (usedMB / totalMemory) * 100f;

                    lastRam = usedPercent;
                    ramValue.Text = $"{usedPercent:F1}% ({usedMB:F0}/{totalMemory:F0} MB)";
                    ramBar.Value = usedPercent;

                    // Color based on usage
                    ramBar.Foreground = new SolidColorBrush(GetUsageColor(usedPercent, theme));
                }

                // Update Network
                if (networkSentCounter != null && networkReceivedCounter != null)
                {
                    float sent = networkSentCounter.NextValue();
                    float received = networkReceivedCounter.NextValue();

                    lastNetworkSent = (long)sent;
                    lastNetworkReceived = (long)received;

                    networkValue.Text = $"↓ {FormatBytes(received)}/s  ↑ {FormatBytes(sent)}/s";
                }

                // Publish system resources event
                EventBus.Instance.Publish(new SystemResourcesChangedEvent
                {
                    CpuUsagePercent = lastCpu,
                    MemoryUsedBytes = (long)(lastRam * GetTotalPhysicalMemory() * 1024 * 1024 / 100),
                    MemoryTotalBytes = (long)(GetTotalPhysicalMemory() * 1024 * 1024),
                    Timestamp = DateTime.Now
                });

                // Publish network activity event
                EventBus.Instance.Publish(new NetworkActivityEvent
                {
                    BytesSent = lastNetworkSent,
                    BytesReceived = lastNetworkReceived,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("SystemMonitor", $"Failed to update stats: {ex.Message}");
            }
        }

        private Color GetUsageColor(float percent, Theme theme)
        {
            if (percent >= 90f) return theme.Error;
            if (percent >= 75f) return theme.Warning;
            return theme.Success;
        }

        private float GetTotalPhysicalMemory()
        {
            try
            {
                // Use WMI to get total physical memory (in MB)
                var searcher = new System.Management.ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var totalKB = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                    return totalKB / 1024f; // Convert to MB
                }
            }
            catch
            {
                // Fallback: assume 16GB if WMI fails
                return 16384f;
            }
            return 16384f;
        }

        private string FormatBytes(float bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes = bytes / 1024;
            }
            return $"{bytes:F1} {sizes[order]}";
        }

        protected override void OnDispose()
        {
            // Stop timer
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Tick -= UpdateTimer_Tick;
                updateTimer = null;
            }

            // Dispose performance counters
            cpuCounter?.Dispose();
            ramCounter?.Dispose();
            networkSentCounter?.Dispose();
            networkReceivedCounter?.Dispose();

            base.OnDispose();
        }

        public override void OnActivated()
        {
            base.OnActivated();
            updateTimer?.Start();
        }

        public override void OnDeactivated()
        {
            base.OnDeactivated();
            updateTimer?.Stop();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            // Rebuild UI with new theme
            if (this.Content != null)
            {
                // Store current values
                var cpu = lastCpu;
                var ram = lastRam;

                // Rebuild UI
                BuildUI();

                // Restore values
                lastCpu = cpu;
                lastRam = ram;
                UpdateStats();
            }
        }
    }
}
