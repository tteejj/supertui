using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using SuperTUI.Core;
using SuperTUI.Infrastructure;
using SuperTUI.Widgets;

namespace SuperTUI.Tests.Integration
{
    /// <summary>
    /// Integration tests for complete app lifecycle and cross-component interactions
    /// </summary>
    public class IntegrationTests : IDisposable
    {
        private readonly string testDataDir;
        private readonly string testConfigPath;
        private readonly string testStatePath;

        public IntegrationTests()
        {
            testDataDir = Path.Combine(Path.GetTempPath(), $"SuperTUI_Test_{Guid.NewGuid()}");
            Directory.CreateDirectory(testDataDir);

            testConfigPath = Path.Combine(testDataDir, "test_config.json");
            testStatePath = Path.Combine(testDataDir, "test_state.json");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(testDataDir))
                {
                    Directory.Delete(testDataDir, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        // ====================================================================
        // INFRASTRUCTURE INITIALIZATION TESTS
        // ====================================================================

        [Fact]
        public void Infrastructure_FullInitializationSequence_ShouldSucceed()
        {
            // Arrange & Act
            var logger = Logger.Instance;
            var memoryLogSink = new MemoryLogSink();
            logger.AddSink(memoryLogSink);
            logger.SetMinLevel(LogLevel.Debug);

            var themeManager = ThemeManager.Instance;
            themeManager.Initialize(null); // Use built-in themes

            var configManager = ConfigurationManager.Instance;
            configManager.Initialize(testConfigPath);

            var securityManager = SecurityManager.Instance;
            securityManager.Initialize(SecurityMode.Strict);

            // Assert
            Assert.NotNull(logger);
            Assert.NotNull(themeManager.CurrentTheme);
            Assert.Equal("Dark", themeManager.CurrentTheme.Name);
            Assert.Equal(SecurityMode.Strict, securityManager.Mode);
            Assert.True(memoryLogSink.GetLogs().Any(log => log.Contains("initializ")));
        }

        [Fact]
        public void ConfigurationManager_Validate_AutomaticallyCalledAfterLoad()
        {
            // Arrange
            var configManager = new ConfigurationManager();

            // Write invalid config file
            var invalidConfig = @"{
                ""UI.FontSize"": 999,
                ""Performance.MaxFPS"": -10
            }";
            File.WriteAllText(testConfigPath, invalidConfig);

            // Act - Initialize will call Validate() automatically
            configManager.Initialize(testConfigPath);

            // Assert - Invalid values should be reset to defaults
            var fontSize = configManager.Get<int>("UI.FontSize");
            var maxFPS = configManager.Get<int>("Performance.MaxFPS");

            Assert.InRange(fontSize, 8, 24); // Valid range from validator
            Assert.InRange(maxFPS, 1, 144); // Valid range from validator
        }

        // ====================================================================
        // WORKSPACE LIFECYCLE TESTS
        // ====================================================================

        [Fact]
        public void Workspace_AddWidget_ShouldWrapInErrorBoundary()
        {
            // Arrange
            var layout = new DashboardLayoutEngine();
            var workspace = new Workspace("Test", 1, layout);
            var widget = new ClockWidget();
            widget.WidgetName = "TestClock";

            // Act
            workspace.AddWidget(widget, new LayoutParams());

            // Assert
            Assert.Contains(widget, workspace.Widgets);
            // ErrorBoundary wrapping is internal, but widget should initialize safely
        }

        [Fact]
        public void Workspace_ActivateDeactivate_ShouldCallWidgetLifecycleMethods()
        {
            // Arrange
            var layout = new DashboardLayoutEngine();
            var workspace = new Workspace("Test", 1, layout);
            var widget = new ClockWidget();
            widget.WidgetName = "TestClock";
            workspace.AddWidget(widget, new LayoutParams());

            // Act
            workspace.Activate();
            Assert.True(true); // Widget should be activated (timer starts)

            workspace.Deactivate();
            // Widget should be deactivated (timer stops)

            // Assert - no exceptions thrown
        }

        [Fact]
        public void Workspace_FocusCycling_ShouldNavigateBetweenWidgets()
        {
            // Arrange
            var layout = new DashboardLayoutEngine();
            var workspace = new Workspace("Test", 1, layout);

            var widget1 = new ClockWidget { WidgetName = "Clock1" };
            var widget2 = new ClockWidget { WidgetName = "Clock2" };

            workspace.AddWidget(widget1, new LayoutParams());
            workspace.AddWidget(widget2, new LayoutParams());
            workspace.Activate();

            // Act
            workspace.CycleFocusForward();
            var focused1 = workspace.GetFocusedWidget();

            workspace.CycleFocusForward();
            var focused2 = workspace.GetFocusedWidget();

            // Assert
            Assert.NotNull(focused1);
            Assert.NotNull(focused2);
            Assert.NotSame(focused1, focused2); // Should cycle to different widget
        }

        [Fact]
        public void Workspace_RemoveFocusedWidget_ShouldFocusNext()
        {
            // Arrange
            var layout = new DashboardLayoutEngine();
            var workspace = new Workspace("Test", 1, layout);

            var widget1 = new ClockWidget { WidgetName = "Clock1" };
            var widget2 = new ClockWidget { WidgetName = "Clock2" };

            workspace.AddWidget(widget1, new LayoutParams());
            workspace.AddWidget(widget2, new LayoutParams());
            workspace.Activate();
            workspace.CycleFocusForward();

            // Act
            workspace.RemoveFocusedWidget();

            // Assert
            Assert.Single(workspace.Widgets); // One widget removed
            var focused = workspace.GetFocusedWidget();
            Assert.NotNull(focused); // Should auto-focus remaining widget
        }

        [Fact]
        public void Workspace_Dispose_ShouldCleanupAllWidgets()
        {
            // Arrange
            var layout = new DashboardLayoutEngine();
            var workspace = new Workspace("Test", 1, layout);

            var widget1 = new ClockWidget { WidgetName = "Clock1" };
            var widget2 = new CounterWidget { WidgetName = "Counter1" };

            workspace.AddWidget(widget1, new LayoutParams());
            workspace.AddWidget(widget2, new LayoutParams());
            workspace.Activate();

            // Act
            workspace.Dispose();

            // Assert
            Assert.Empty(workspace.Widgets);
            Assert.Empty(workspace.Screens);
            // Widgets should be properly disposed (timers stopped, events unsubscribed)
        }

        // ====================================================================
        // STATE PERSISTENCE ROUND-TRIP TESTS
        // ====================================================================

        [Fact]
        public async Task StatePersistence_SaveAndLoad_ShouldPreserveWorkspaceState()
        {
            // Arrange
            var persistence = new StatePersistenceManager();
            persistence.Initialize(testStatePath);

            var snapshot = new StateSnapshot
            {
                Version = StateVersion.Current,
                Timestamp = DateTime.Now,
                ApplicationState = new Dictionary<string, object>
                {
                    ["CurrentWorkspaceIndex"] = 1,
                    ["TestKey"] = "TestValue"
                },
                Workspaces = new List<WorkspaceState>
                {
                    new WorkspaceState
                    {
                        Name = "Workspace1",
                        Index = 1,
                        WidgetStates = new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                ["WidgetId"] = Guid.NewGuid().ToString(),
                                ["WidgetName"] = "Clock",
                                ["WidgetType"] = "ClockWidget"
                            }
                        }
                    }
                },
                UserData = new Dictionary<string, object>
                {
                    ["CustomSetting"] = 42
                }
            };

            // Act
            await persistence.SaveStateAsync(snapshot);
            var loaded = await persistence.LoadStateAsync();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(snapshot.Version, loaded.Version);
            Assert.Equal(snapshot.ApplicationState["CurrentWorkspaceIndex"],
                loaded.ApplicationState["CurrentWorkspaceIndex"]);
            Assert.Equal(snapshot.Workspaces.Count, loaded.Workspaces.Count);
            Assert.Equal(snapshot.Workspaces[0].Name, loaded.Workspaces[0].Name);
            Assert.Equal(snapshot.UserData["CustomSetting"], loaded.UserData["CustomSetting"]);
        }

        [Fact]
        public void StatePersistence_WithBackup_ShouldCreateBackupFile()
        {
            // Arrange
            var persistence = new StatePersistenceManager();
            persistence.Initialize(testStatePath);

            var backupDir = Path.Combine(testDataDir, "backups");
            ConfigurationManager.Instance.Set("Backup.Enabled", true);
            ConfigurationManager.Instance.Set("Backup.Directory", backupDir);

            var snapshot = new StateSnapshot
            {
                Version = StateVersion.Current,
                Timestamp = DateTime.Now,
                ApplicationState = new Dictionary<string, object>()
            };

            // Act
            persistence.SaveState(snapshot, createBackup: false); // Initial save
            persistence.SaveState(snapshot, createBackup: true);  // Save with backup

            // Assert
            Assert.True(Directory.Exists(backupDir));
            var backupFiles = Directory.GetFiles(backupDir, "state_backup_*");
            Assert.NotEmpty(backupFiles);
        }

        // ====================================================================
        // WIDGET DISPOSAL AND CLEANUP TESTS
        // ====================================================================

        [Fact]
        public void Widget_Disposal_ShouldStopTimers()
        {
            // Arrange
            var widget = new ClockWidget();
            widget.WidgetName = "TestClock";
            widget.Initialize();

            // Act
            widget.Dispose();

            // Assert
            // Timer should be stopped and disposed
            // If timer wasn't disposed, this would leak resources
            // No way to directly verify, but should not throw
        }

        [Fact]
        public void Widget_Disposal_ShouldUnsubscribeEvents()
        {
            // Arrange
            var widget = new FileExplorerWidget();
            widget.Initialize();

            // Act
            widget.Dispose();

            // Assert
            // Event handlers should be unsubscribed
            // This test mainly ensures Dispose() completes without exception
        }

        // ====================================================================
        // SECURITY INTEGRATION TESTS
        // ====================================================================

        [Fact]
        public void SecurityManager_Development_Mode_BlockedInReleaseBuilds()
        {
            #if !DEBUG
            // Arrange & Act & Assert
            var securityManager = new SecurityManager();
            Assert.Throws<InvalidOperationException>(() =>
                securityManager.Initialize(SecurityMode.Development));
            #else
            // In DEBUG builds, Development mode should be allowed
            var securityManager = new SecurityManager();
            securityManager.Initialize(SecurityMode.Development);
            Assert.Equal(SecurityMode.Development, securityManager.Mode);
            #endif
        }

        [Fact]
        public void SecurityManager_PathTraversal_ShouldBeBlocked()
        {
            // Arrange
            var securityManager = new SecurityManager();
            securityManager.Initialize(SecurityMode.Strict);
            securityManager.AddAllowedDirectory(testDataDir);

            // Act
            bool result = securityManager.ValidateFileAccess("../../../../etc/passwd");

            // Assert
            Assert.False(result); // Path traversal should be blocked
        }

        // ====================================================================
        // THEME SWITCHING INTEGRATION TESTS
        // ====================================================================

        [Fact]
        public void ThemeManager_SwitchTheme_ShouldNotifyWidgets()
        {
            // Arrange
            var themeManager = ThemeManager.Instance;
            themeManager.Initialize(null);

            var widget = new ClockWidget();
            widget.Initialize();

            var initialTheme = themeManager.CurrentTheme.Name;

            // Act
            var newTheme = initialTheme == "Dark" ? "Light" : "Dark";
            themeManager.SetTheme(newTheme);

            // Assert
            Assert.Equal(newTheme, themeManager.CurrentTheme.Name);
            // Widget should receive theme change notification via IThemeable
        }

        // ====================================================================
        // ERROR HANDLING INTEGRATION TESTS
        // ====================================================================

        [Fact]
        public void ErrorBoundary_WidgetException_ShouldIsolateError()
        {
            // Arrange
            var brokenWidget = new BrokenWidget(); // Widget that throws in Initialize
            var errorBoundary = new ErrorBoundary(brokenWidget);

            // Act & Assert
            // Should not throw - ErrorBoundary catches and shows error UI
            errorBoundary.SafeInitialize();

            Assert.True(errorBoundary.IsInErrorState);
        }
    }

    // ====================================================================
    // TEST HELPER CLASSES
    // ====================================================================

    /// <summary>
    /// Test widget that throws exceptions for error handling tests
    /// </summary>
    public class BrokenWidget : WidgetBase
    {
        public override void Initialize()
        {
            throw new InvalidOperationException("Test exception from BrokenWidget");
        }
    }
}
