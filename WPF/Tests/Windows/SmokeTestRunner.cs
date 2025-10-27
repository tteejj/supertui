using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using SuperTUI.Core;
using SuperTUI.Infrastructure;
using SuperTUI.Widgets;
using Xunit;
using Xunit.Abstractions;
using DI = SuperTUI.DI;

namespace SuperTUI.Tests.Windows
{
    /// <summary>
    /// Automated smoke tests for Windows - launches app, tests core functionality, collects diagnostics
    /// </summary>
    [Trait("Category", "Windows")]
    [Trait("Category", "Smoke")]
    public class SmokeTestRunner : IDisposable
    {
        private readonly ITestOutputHelper output;
        private readonly string diagnosticsDir;
        private readonly Stopwatch stopwatch;
        private readonly List<string> errors;

        public SmokeTestRunner(ITestOutputHelper output)
        {
            this.output = output;
            this.errors = new List<string>();
            this.stopwatch = Stopwatch.StartNew();

            // Create diagnostics directory
            diagnosticsDir = Path.Combine(
                Directory.GetCurrentDirectory(),
                "test-results",
                $"smoke-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
            );
            Directory.CreateDirectory(diagnosticsDir);

            output.WriteLine($"Diagnostics directory: {diagnosticsDir}");
        }

        public void Dispose()
        {
            stopwatch.Stop();
            WriteDiagnosticsSummary();
        }

        #region Application Launch Tests

        [Fact]
        public void SmokeTest_01_ApplicationShouldLaunch()
        {
            output.WriteLine("TEST: Application Launch");

            try
            {
                // Initialize infrastructure
                var container = new DI.ServiceContainer();
                DI.ServiceRegistration.RegisterServices(container);

                var logger = container.Resolve<ILogger>();
                var configManager = container.Resolve<IConfigurationManager>();
                var themeManager = container.Resolve<IThemeManager>();

                // Assert
                logger.Should().NotBeNull();
                configManager.Should().NotBeNull();
                themeManager.Should().NotBeNull();

                output.WriteLine("✓ Infrastructure initialized successfully");
            }
            catch (Exception ex)
            {
                LogError("Application Launch", ex);
                throw;
            }
        }

        #endregion

        #region Widget Instantiation Tests

        [Fact]
        public void SmokeTest_02_AllWidgetsShouldInstantiate()
        {
            output.WriteLine("TEST: Widget Instantiation");

            var container = new DI.ServiceContainer();
            DI.ServiceRegistration.RegisterServices(container);
            var factory = container.Resolve<DI.WidgetFactory>();

            var widgetTypes = new[]
            {
                "ClockWidget", "CounterWidget", "TodoWidget",
                "CommandPaletteWidget", "ShortcutHelpWidget", "SettingsWidget",
                "FileExplorerWidget", "GitStatusWidget",
                "TaskManagementWidget", "AgendaWidget", "ProjectStatsWidget",
                "KanbanBoardWidget", "TaskSummaryWidget", "NotesWidget",
                "TimeTrackingWidget"
            };

            var failedWidgets = new List<string>();

            foreach (var widgetType in widgetTypes)
            {
                try
                {
                    var widget = factory.CreateWidget(widgetType);
                    widget.Should().NotBeNull();
                    output.WriteLine($"✓ {widgetType} instantiated");

                    // Dispose to avoid leaks
                    widget.Dispose();
                }
                catch (Exception ex)
                {
                    failedWidgets.Add(widgetType);
                    LogError($"Widget: {widgetType}", ex);
                    output.WriteLine($"✗ {widgetType} FAILED: {ex.Message}");
                }
            }

            // Assert
            failedWidgets.Should().BeEmpty($"All widgets should instantiate, but these failed: {string.Join(", ", failedWidgets)}");
        }

        #endregion

        #region Workspace Tests

        [Fact]
        public void SmokeTest_03_WorkspaceShouldCreateAndSwitch()
        {
            output.WriteLine("TEST: Workspace Creation");

            try
            {
                var layout = new DashboardLayoutEngine();
                var workspace = new Workspace("Test Workspace", 1, layout);

                workspace.Should().NotBeNull();
                workspace.Name.Should().Be("Test Workspace");

                output.WriteLine("✓ Workspace created successfully");

                // Add widgets
                var container = new DI.ServiceContainer();
                DI.ServiceRegistration.RegisterServices(container);
                var factory = container.Resolve<DI.WidgetFactory>();

                var widget1 = factory.CreateWidget("ClockWidget");
                var widget2 = factory.CreateWidget("CounterWidget");

                workspace.AddWidget(widget1, new LayoutParams());
                workspace.AddWidget(widget2, new LayoutParams());

                workspace.Widgets.Count.Should().Be(2);
                output.WriteLine("✓ Widgets added to workspace");

                // Activate/Deactivate
                workspace.Activate();
                output.WriteLine("✓ Workspace activated");

                workspace.Deactivate();
                output.WriteLine("✓ Workspace deactivated");

                // Cleanup
                workspace.Dispose();
                output.WriteLine("✓ Workspace disposed");
            }
            catch (Exception ex)
            {
                LogError("Workspace", ex);
                throw;
            }
        }

        #endregion

        #region Configuration Tests

        [Fact]
        public void SmokeTest_04_ConfigurationShouldLoadAndSave()
        {
            output.WriteLine("TEST: Configuration Persistence");

            try
            {
                var tempConfig = Path.Combine(diagnosticsDir, "test_config.json");
                var configManager = new ConfigurationManager();
                configManager.Initialize(tempConfig);

                // Register and set a test value
                configManager.Register("Test.Key", "default", "Test key");
                configManager.Set("Test.Key", "modified");

                // Save
                configManager.Save();
                File.Exists(tempConfig).Should().BeTrue("Config file should exist");
                output.WriteLine("✓ Configuration saved");

                // Load in new instance
                var newConfigManager = new ConfigurationManager();
                newConfigManager.Initialize(tempConfig);
                newConfigManager.Register("Test.Key", "default", "Test key");
                newConfigManager.Load();

                var loaded = newConfigManager.Get<string>("Test.Key");
                loaded.Should().Be("modified", "Value should persist");
                output.WriteLine("✓ Configuration loaded and verified");
            }
            catch (Exception ex)
            {
                LogError("Configuration", ex);
                throw;
            }
        }

        #endregion

        #region State Persistence Tests

        [Fact]
        public async Task SmokeTest_05_StateShouldPersistAndRestore()
        {
            output.WriteLine("TEST: State Persistence");

            try
            {
                var tempState = Path.Combine(diagnosticsDir, "test_state.json");
                var stateManager = new StatePersistenceManager();
                stateManager.Initialize(tempState);

                // Create test state
                var snapshot = new StateSnapshot
                {
                    Version = StateVersion.Current,
                    Timestamp = DateTime.Now,
                    ApplicationState = new Dictionary<string, object>
                    {
                        ["TestKey"] = "TestValue"
                    }
                };

                // Save
                await stateManager.SaveStateAsync(snapshot);
                File.Exists(tempState).Should().BeTrue("State file should exist");
                output.WriteLine("✓ State saved");

                // Load
                var loaded = await stateManager.LoadStateAsync();
                loaded.Should().NotBeNull();
                loaded.ApplicationState["TestKey"].Should().Be("TestValue");
                output.WriteLine("✓ State loaded and verified");
            }
            catch (Exception ex)
            {
                LogError("State Persistence", ex);
                throw;
            }
        }

        #endregion

        #region Theme Tests

        [Fact]
        public void SmokeTest_06_ThemeShouldSwitchWithoutErrors()
        {
            output.WriteLine("TEST: Theme Switching");

            try
            {
                var themeManager = ThemeManager.Instance;
                themeManager.Initialize(null);

                themeManager.SetTheme("Dark");
                themeManager.CurrentTheme.Name.Should().Be("Dark");
                output.WriteLine("✓ Dark theme applied");

                themeManager.SetTheme("Light");
                themeManager.CurrentTheme.Name.Should().Be("Light");
                output.WriteLine("✓ Light theme applied");

                themeManager.SetTheme("Dark");
                output.WriteLine("✓ Theme switching verified");
            }
            catch (Exception ex)
            {
                LogError("Theme Switching", ex);
                throw;
            }
        }

        #endregion

        #region Security Tests

        [Fact]
        public void SmokeTest_07_SecurityManagerShouldEnforce()
        {
            output.WriteLine("TEST: Security Manager");

            try
            {
                var securityManager = new SecurityManager();
                securityManager.Initialize(SecurityMode.Strict);

                // Test path validation
                var result = securityManager.ValidateFileAccess("../../../../etc/passwd");
                result.Should().BeFalse("Path traversal should be blocked");
                output.WriteLine("✓ Path traversal blocked");

                // Test allowed directory
                var tempDir = Path.GetTempPath();
                securityManager.AddAllowedDirectory(tempDir);
                var tempFile = Path.Combine(tempDir, "test.txt");
                result = securityManager.ValidateFileAccess(tempFile);
                result.Should().BeTrue("Allowed path should validate");
                output.WriteLine("✓ Allowed path validated");
            }
            catch (Exception ex)
            {
                LogError("Security Manager", ex);
                throw;
            }
        }

        #endregion

        #region Memory Leak Tests

        [Fact]
        public void SmokeTest_08_WidgetsShouldNotLeakMemory()
        {
            output.WriteLine("TEST: Memory Leak Detection");

            try
            {
                var container = new DI.ServiceContainer();
                DI.ServiceRegistration.RegisterServices(container);
                var factory = container.Resolve<DI.WidgetFactory>();

                // Create and dispose many widgets
                var iterations = 100;
                var initialMemory = GC.GetTotalMemory(true);

                for (int i = 0; i < iterations; i++)
                {
                    var widget = factory.CreateWidget("ClockWidget");
                    widget.Dispose();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var finalMemory = GC.GetTotalMemory(true);
                var memoryGrowth = finalMemory - initialMemory;

                output.WriteLine($"Memory growth after {iterations} iterations: {memoryGrowth:N0} bytes");

                // Assert: Memory growth should be reasonable (< 10MB for 100 widgets)
                memoryGrowth.Should().BeLessThan(10 * 1024 * 1024,
                    "Memory growth should be minimal if widgets dispose properly");
            }
            catch (Exception ex)
            {
                LogError("Memory Leak Detection", ex);
                throw;
            }
        }

        #endregion

        #region Performance Tests

        [Fact]
        public void SmokeTest_09_WidgetCreationShouldBeFast()
        {
            output.WriteLine("TEST: Widget Creation Performance");

            try
            {
                var container = new DI.ServiceContainer();
                DI.ServiceRegistration.RegisterServices(container);
                var factory = container.Resolve<DI.WidgetFactory>();

                var sw = Stopwatch.StartNew();
                var iterations = 1000;

                for (int i = 0; i < iterations; i++)
                {
                    var widget = factory.CreateWidget("ClockWidget");
                    widget.Dispose();
                }

                sw.Stop();
                var avgTime = sw.ElapsedMilliseconds / (double)iterations;

                output.WriteLine($"Average widget creation time: {avgTime:F3} ms");

                // Assert: Widget creation should be fast (< 1ms average)
                avgTime.Should().BeLessThan(1.0,
                    "Widget creation should be fast via DI container");
            }
            catch (Exception ex)
            {
                LogError("Performance", ex);
                throw;
            }
        }

        #endregion

        #region Helper Methods

        private void LogError(string test, Exception ex)
        {
            var errorMessage = $"[{DateTime.Now:HH:mm:ss}] {test}: {ex.Message}\n{ex.StackTrace}";
            errors.Add(errorMessage);

            // Write to diagnostics file
            var errorFile = Path.Combine(diagnosticsDir, "errors.log");
            File.AppendAllText(errorFile, errorMessage + "\n\n");
        }

        private void WriteDiagnosticsSummary()
        {
            var summary = $@"SuperTUI Smoke Test Report
========================================
Date: {DateTime.Now}
Duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds
Errors: {errors.Count}

Errors:
{(errors.Any() ? string.Join("\n\n", errors) : "None")}

Environment:
- OS: {Environment.OSVersion}
- .NET: {Environment.Version}
- Working Directory: {Directory.GetCurrentDirectory()}
- Machine: {Environment.MachineName}
- User: {Environment.UserName}
";

            var summaryFile = Path.Combine(diagnosticsDir, "SUMMARY.txt");
            File.WriteAllText(summaryFile, summary);

            output.WriteLine("\n" + summary);
            output.WriteLine($"\nDiagnostics saved to: {diagnosticsDir}");
        }

        #endregion
    }
}
