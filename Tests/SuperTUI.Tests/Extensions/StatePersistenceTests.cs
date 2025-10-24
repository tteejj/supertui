using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using SuperTUI.Extensions;
using SuperTUI.Infrastructure;
using Xunit;

namespace SuperTUI.Tests.Extensions
{
    public class StatePersistenceTests : IDisposable
    {
        private readonly string tempDir;
        private readonly StatePersistenceManager persistenceManager;

        public StatePersistenceTests()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "SuperTUI_StateTests_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            // Initialize required singletons
            ConfigurationManager.Instance.Initialize(Path.Combine(tempDir, "config.json"));
            Logger.Instance.SetMinLevel(LogLevel.Error); // Reduce noise in tests

            persistenceManager = StatePersistenceManager.Instance;
            persistenceManager.Initialize(tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void SaveState_CreatesFile()
        {
            // Arrange
            var snapshot = new StateSnapshot
            {
                Timestamp = DateTime.Now,
                ApplicationState = new Dictionary<string, object>
                {
                    ["TestKey"] = "TestValue"
                }
            };

            // Act
            persistenceManager.SaveState(snapshot);

            // Assert
            var stateFile = Path.Combine(tempDir, "current_state.json");
            File.Exists(stateFile).Should().BeTrue();
        }

        [Fact]
        public void LoadState_ReturnsNullWhenNoFileExists()
        {
            // Act
            var loaded = persistenceManager.LoadState();

            // Assert
            loaded.Should().BeNull();
        }

        [Fact]
        public void SaveAndLoad_PreservesState()
        {
            // Arrange
            var snapshot = new StateSnapshot
            {
                Version = StateVersion.Current,
                Timestamp = DateTime.Now,
                ApplicationState = new Dictionary<string, object>
                {
                    ["CurrentWorkspaceIndex"] = 2,
                    ["LastSaved"] = "2025-10-24"
                },
                UserData = new Dictionary<string, object>
                {
                    ["Username"] = "TestUser"
                }
            };

            // Act
            persistenceManager.SaveState(snapshot);
            var loaded = persistenceManager.LoadState();

            // Assert
            loaded.Should().NotBeNull();
            loaded.Version.Should().Be(StateVersion.Current);
            loaded.ApplicationState["CurrentWorkspaceIndex"].ToString().Should().Be("2");
            loaded.ApplicationState["LastSaved"].ToString().Should().Be("2025-10-24");
            loaded.UserData["Username"].ToString().Should().Be("TestUser");
        }

        [Fact]
        public void PushUndo_AddsToHistory()
        {
            // Arrange
            var snapshot1 = new StateSnapshot { Timestamp = DateTime.Now };
            var snapshot2 = new StateSnapshot { Timestamp = DateTime.Now.AddSeconds(1) };

            // Act
            persistenceManager.PushUndo(snapshot1);
            persistenceManager.PushUndo(snapshot2);

            // Assert - Should be able to undo
            var undone = persistenceManager.Undo();
            undone.Should().NotBeNull();
        }

        [Fact]
        public void Undo_ReturnsNullWhenNoHistory()
        {
            // Act
            var result = persistenceManager.Undo();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Redo_ReturnsNullWhenNoRedoHistory()
        {
            // Act
            var result = persistenceManager.Redo();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void UndoRedo_WorksCorrectly()
        {
            // Arrange
            var snapshot1 = new StateSnapshot
            {
                ApplicationState = new Dictionary<string, object> { ["Value"] = 1 }
            };
            var snapshot2 = new StateSnapshot
            {
                ApplicationState = new Dictionary<string, object> { ["Value"] = 2 }
            };

            persistenceManager.PushUndo(snapshot1);
            persistenceManager.PushUndo(snapshot2);

            // Act - Undo to get snapshot2
            var undone = persistenceManager.Undo();

            // Then redo
            var redone = persistenceManager.Redo();

            // Assert
            undone.ApplicationState["Value"].ToString().Should().Be("2");
            redone.Should().NotBeNull();
        }

        [Fact]
        public void PushUndo_ClearsRedoHistory()
        {
            // Arrange
            var snapshot1 = new StateSnapshot();
            var snapshot2 = new StateSnapshot();
            var snapshot3 = new StateSnapshot();

            persistenceManager.PushUndo(snapshot1);
            persistenceManager.PushUndo(snapshot2);
            persistenceManager.Undo(); // Creates redo history

            // Act - Push new undo should clear redo
            persistenceManager.PushUndo(snapshot3);
            var redone = persistenceManager.Redo();

            // Assert - Redo should be empty
            redone.Should().BeNull();
        }

        [Fact]
        public void CreateBackup_WhenEnabled_CreatesBackupFile()
        {
            // Arrange
            ConfigurationManager.Instance.Set("Backup.Enabled", true);
            ConfigurationManager.Instance.Set("Backup.Directory", Path.Combine(tempDir, "Backups"));
            ConfigurationManager.Instance.Set("Backup.CompressBackups", false); // Easier to test

            var snapshot = new StateSnapshot
            {
                ApplicationState = new Dictionary<string, object> { ["Test"] = "Backup" }
            };

            // Act
            persistenceManager.SaveState(snapshot);
            persistenceManager.SaveState(snapshot, createBackup: true);

            // Assert
            var backupDir = Path.Combine(tempDir, "Backups");
            Directory.Exists(backupDir).Should().BeTrue();
            Directory.GetFiles(backupDir, "state_backup_*").Should().NotBeEmpty();
        }

        [Fact]
        public void GetAvailableBackups_ReturnsBackupFiles()
        {
            // Arrange
            var backupDir = Path.Combine(tempDir, "Backups");
            ConfigurationManager.Instance.Set("Backup.Directory", backupDir);
            ConfigurationManager.Instance.Set("Backup.Enabled", true);
            ConfigurationManager.Instance.Set("Backup.CompressBackups", false);

            var snapshot = new StateSnapshot();
            persistenceManager.SaveState(snapshot, createBackup: true);

            // Act
            var backups = persistenceManager.GetAvailableBackups();

            // Assert
            backups.Should().HaveCountGreaterThan(0);
        }
    }

    public class StateVersionTests
    {
        [Fact]
        public void Compare_SameVersions_ReturnsZero()
        {
            // Act
            var result = StateVersion.Compare("1.0", "1.0");

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public void Compare_DifferentMajorVersions_ReturnsCorrectValue()
        {
            // Act
            var result = StateVersion.Compare("2.0", "1.0");

            // Assert
            result.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Compare_DifferentMinorVersions_ReturnsCorrectValue()
        {
            // Act
            var result = StateVersion.Compare("1.5", "1.3");

            // Assert
            result.Should().BeGreaterThan(0);
        }

        [Fact]
        public void IsCompatible_SameMajorVersion_ReturnsTrue()
        {
            // Act
            var result = StateVersion.IsCompatible("1.5");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsCompatible_DifferentMajorVersion_ReturnsFalse()
        {
            // Act
            var result = StateVersion.IsCompatible("2.0");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsCompatible_NullOrEmpty_ReturnsTrue()
        {
            // Act & Assert
            StateVersion.IsCompatible(null).Should().BeTrue();
            StateVersion.IsCompatible("").Should().BeTrue();
        }
    }
}
