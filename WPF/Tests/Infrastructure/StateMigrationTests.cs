using System;
using System.Collections.Generic;
using Xunit;
using SuperTUI.Extensions;

namespace SuperTUI.Tests.Infrastructure
{
    public class StateMigrationTests
    {
        [Fact]
        public void StateVersion_Compare_ShouldCompareCorrectly()
        {
            // Act & Assert - Equal versions
            Assert.Equal(0, StateVersion.Compare("1.0", "1.0"));
            Assert.Equal(0, StateVersion.Compare("2.5", "2.5"));

            // Act & Assert - v1 < v2
            Assert.Equal(-1, StateVersion.Compare("1.0", "1.1"));
            Assert.Equal(-1, StateVersion.Compare("1.0", "2.0"));
            Assert.Equal(-1, StateVersion.Compare("1.9", "2.0"));

            // Act & Assert - v1 > v2
            Assert.Equal(1, StateVersion.Compare("1.1", "1.0"));
            Assert.Equal(1, StateVersion.Compare("2.0", "1.9"));
            Assert.Equal(1, StateVersion.Compare("3.0", "2.5"));
        }

        [Fact]
        public void StateVersion_Compare_ShouldHandleMissingMinorVersion()
        {
            // Act & Assert - Missing minor defaults to 0
            Assert.Equal(0, StateVersion.Compare("1", "1.0"));
            Assert.Equal(0, StateVersion.Compare("2.0", "2"));
            Assert.Equal(-1, StateVersion.Compare("1", "1.1"));
            Assert.Equal(1, StateVersion.Compare("2.1", "2"));
        }

        [Fact]
        public void StateVersion_IsCompatible_ShouldCheckMajorVersion()
        {
            // Arrange - Set current to 1.5
            // (This test assumes Current = "1.0", so we test against that)

            // Act & Assert - Compatible (same major)
            Assert.True(StateVersion.IsCompatible("1.0"));
            Assert.True(StateVersion.IsCompatible("1.1"));
            Assert.True(StateVersion.IsCompatible("1.9"));

            // Act & Assert - Incompatible (different major)
            Assert.False(StateVersion.IsCompatible("2.0"));
            Assert.False(StateVersion.IsCompatible("0.9"));
        }

        [Fact]
        public void StateSnapshot_DefaultVersion_ShouldBeCurrent()
        {
            // Act
            var snapshot = new StateSnapshot();

            // Assert
            Assert.Equal(StateVersion.Current, snapshot.Version);
        }

        [Fact]
        public void StateMigrationManager_WithNoMigrations_ShouldReturnOriginalSnapshot()
        {
            // Arrange
            var manager = new StateMigrationManager();
            var snapshot = new StateSnapshot { Version = "1.0" };

            // Act
            var result = manager.MigrateToCurrentVersion(snapshot);

            // Assert - No migrations registered, so returns as-is
            Assert.Equal(snapshot, result);
        }

        [Fact]
        public void StateMigrationManager_WithSameVersion_ShouldReturnImmediately()
        {
            // Arrange
            var manager = new StateMigrationManager();
            var snapshot = new StateSnapshot { Version = StateVersion.Current };

            // Act
            var result = manager.MigrateToCurrentVersion(snapshot);

            // Assert
            Assert.Equal(snapshot, result);
            Assert.Equal(StateVersion.Current, result.Version);
        }

        [Fact]
        public void StateMigrationManager_RegisterMigration_ShouldAddToList()
        {
            // Arrange
            var manager = new StateMigrationManager();
            var migration = new TestMigration_1_0_to_1_1();

            // Act
            manager.RegisterMigration(migration);
            var migrations = manager.GetMigrations();

            // Assert
            Assert.Contains(migration, migrations);
        }

        [Fact]
        public void StateMigrationManager_MigrateToCurrentVersion_ShouldExecuteMigration()
        {
            // Arrange
            var manager = new StateMigrationManager();
            manager.RegisterMigration(new TestMigration_1_0_to_1_1());

            var snapshot = new StateSnapshot
            {
                Version = "1.0",
                ApplicationState = new Dictionary<string, object>()
            };

            // Act
            var result = manager.MigrateToCurrentVersion(snapshot);

            // Assert - Migration should add "TestField"
            Assert.True(result.ApplicationState.ContainsKey("TestField"));
            Assert.Equal("MigratedValue", result.ApplicationState["TestField"]);
        }

        [Fact]
        public void StateMigrationManager_MigrateToCurrentVersion_ShouldExecuteChain()
        {
            // Arrange
            var manager = new StateMigrationManager();
            manager.RegisterMigration(new TestMigration_1_0_to_1_1());
            manager.RegisterMigration(new TestMigration_1_1_to_1_2());

            var snapshot = new StateSnapshot
            {
                Version = "1.0",
                ApplicationState = new Dictionary<string, object>()
            };

            // Act
            var result = manager.MigrateToCurrentVersion(snapshot);

            // Assert - Both migrations executed
            Assert.True(result.ApplicationState.ContainsKey("TestField")); // From 1.0->1.1
            Assert.True(result.ApplicationState.ContainsKey("SecondField")); // From 1.1->1.2
        }

        [Fact]
        public void StateMigrationManager_MigrateToCurrentVersion_ShouldUpdateVersion()
        {
            // Arrange
            var manager = new StateMigrationManager();
            manager.RegisterMigration(new TestMigration_1_0_to_1_1());

            var snapshot = new StateSnapshot { Version = "1.0" };

            // Act
            var result = manager.MigrateToCurrentVersion(snapshot);

            // Assert
            Assert.Equal("1.1", result.Version);
        }

        [Fact]
        public void StateMigrationManager_MigrateToCurrentVersion_WithFailure_ShouldThrow()
        {
            // Arrange
            var manager = new StateMigrationManager();
            manager.RegisterMigration(new FailingMigration());

            var snapshot = new StateSnapshot { Version = "1.0" };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                manager.MigrateToCurrentVersion(snapshot));
        }

        [Fact]
        public void StateMigrationManager_MigrateToCurrentVersion_WithCircularDependency_ShouldThrow()
        {
            // Arrange
            var manager = new StateMigrationManager();
            // Create circular dependency: 1.0->1.1->1.0->1.1...
            manager.RegisterMigration(new CircularMigration_1_0_to_1_1());
            manager.RegisterMigration(new CircularMigration_1_1_to_1_0());

            var snapshot = new StateSnapshot { Version = "1.0" };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                manager.MigrateToCurrentVersion(snapshot));
        }

        // Test migration implementations
        private class TestMigration_1_0_to_1_1 : IStateMigration
        {
            public string FromVersion => "1.0";
            public string ToVersion => "1.1";

            public StateSnapshot Migrate(StateSnapshot snapshot)
            {
                snapshot.ApplicationState["TestField"] = "MigratedValue";
                return snapshot;
            }
        }

        private class TestMigration_1_1_to_1_2 : IStateMigration
        {
            public string FromVersion => "1.1";
            public string ToVersion => "1.2";

            public StateSnapshot Migrate(StateSnapshot snapshot)
            {
                snapshot.ApplicationState["SecondField"] = "SecondValue";
                return snapshot;
            }
        }

        private class FailingMigration : IStateMigration
        {
            public string FromVersion => "1.0";
            public string ToVersion => "1.1";

            public StateSnapshot Migrate(StateSnapshot snapshot)
            {
                throw new InvalidOperationException("Migration intentionally failed");
            }
        }

        private class CircularMigration_1_0_to_1_1 : IStateMigration
        {
            public string FromVersion => "1.0";
            public string ToVersion => "1.1";

            public StateSnapshot Migrate(StateSnapshot snapshot) => snapshot;
        }

        private class CircularMigration_1_1_to_1_0 : IStateMigration
        {
            public string FromVersion => "1.1";
            public string ToVersion => "1.0";

            public StateSnapshot Migrate(StateSnapshot snapshot) => snapshot;
        }
    }

    public class StatePersistenceManagerMigrationTests : IDisposable
    {
        private readonly string testStateDir;
        private readonly StatePersistenceManager manager;

        public StatePersistenceManagerMigrationTests()
        {
            testStateDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"supertui_test_{Guid.NewGuid()}");

            manager = new StatePersistenceManager();
            manager.Initialize(testStateDir);
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(testStateDir))
            {
                System.IO.Directory.Delete(testStateDir, recursive: true);
            }
        }

        [Fact]
        public void LoadState_WithOldVersion_ShouldMigrate()
        {
            // Arrange
            manager.MigrationManager.RegisterMigration(new TestMigration_0_9_to_1_0());

            var oldSnapshot = new StateSnapshot
            {
                Version = "0.9",
                ApplicationState = new Dictionary<string, object>
                {
                    ["OldKey"] = "OldValue"
                }
            };

            manager.SaveState(oldSnapshot);

            // Force version back to 0.9 in file
            var stateFile = System.IO.Path.Combine(testStateDir, "current_state.json");
            var json = System.IO.File.ReadAllText(stateFile);
            json = json.Replace($"\"Version\":\"{StateVersion.Current}\"", "\"Version\":\"0.9\"");
            System.IO.File.WriteAllText(stateFile, json);

            // Act
            var loaded = manager.LoadState();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(StateVersion.Current, loaded.Version); // Should be migrated
            Assert.True(loaded.ApplicationState.ContainsKey("MigratedKey")); // From migration
        }

        // Test migration for LoadState integration
        private class TestMigration_0_9_to_1_0 : IStateMigration
        {
            public string FromVersion => "0.9";
            public string ToVersion => StateVersion.Current;

            public StateSnapshot Migrate(StateSnapshot snapshot)
            {
                snapshot.ApplicationState["MigratedKey"] = "NewValue";
                return snapshot;
            }
        }
    }
}
