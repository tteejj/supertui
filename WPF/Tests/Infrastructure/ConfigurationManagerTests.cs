using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Infrastructure
{
    public class ConfigurationManagerTests : IDisposable
    {
        private readonly string testConfigPath;
        private readonly ConfigurationManager configManager;

        public ConfigurationManagerTests()
        {
            testConfigPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
            configManager = new ConfigurationManager();

            // Reset for testing (only available in DEBUG builds)
            configManager.ResetForTesting();

            configManager.Initialize(testConfigPath);
        }

        public void Dispose()
        {
            if (File.Exists(testConfigPath))
            {
                File.Delete(testConfigPath);
            }
        }

        [Fact]
        public void Register_ShouldStoreConfigValue()
        {
            // Arrange & Act
            configManager.Register("test.key", "default_value", "Test key", "Testing");

            // Assert
            var result = configManager.Get<string>("test.key");
            Assert.Equal("default_value", result);
        }

        [Fact]
        public void Set_ShouldUpdateValue()
        {
            // Arrange
            configManager.Register("test.number", 42, "Test number");

            // Act
            configManager.Set("test.number", 100);

            // Assert
            Assert.Equal(100, configManager.Get<int>("test.number"));
        }

        [Fact]
        public void Get_WithNonExistentKey_ShouldReturnDefault()
        {
            // Act
            var result = configManager.Get<string>("nonexistent.key", "fallback");

            // Assert
            Assert.Equal("fallback", result);
        }

        [Fact]
        public void Validator_ShouldPreventInvalidValues()
        {
            // Arrange
            configManager.Register("test.positive", 10, "Must be positive",
                validator: val => (int)val > 0);

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                configManager.Set("test.positive", -5));
        }

        [Fact]
        public void SaveAndLoad_ShouldPersistConfiguration()
        {
            // Arrange
            configManager.Register("test.persist", "original", "Persistence test");
            configManager.Set("test.persist", "modified");
            configManager.Save();

            // Act
            var newManager = new ConfigurationManager();
            newManager.Initialize(testConfigPath);
            newManager.Register("test.persist", "original", "Persistence test");
            newManager.Load();

            // Assert
            Assert.Equal("modified", newManager.Get<string>("test.persist"));
        }

        [Fact]
        public void GetCategory_ShouldReturnOnlyCategoryValues()
        {
            // Arrange
            configManager.Register("cat1.key1", "value1", "Key 1", "Category1");
            configManager.Register("cat1.key2", "value2", "Key 2", "Category1");
            configManager.Register("cat2.key3", "value3", "Key 3", "Category2");

            // Act
            var cat1Values = configManager.GetCategory("Category1");

            // Assert
            Assert.Equal(2, cat1Values.Count);
            Assert.True(cat1Values.ContainsKey("cat1.key1"));
            Assert.True(cat1Values.ContainsKey("cat1.key2"));
            Assert.False(cat1Values.ContainsKey("cat2.key3"));
        }

        [Fact]
        public void ResetToDefaults_ShouldRestoreDefaultValues()
        {
            // Arrange
            configManager.Register("test.reset", "default", "Reset test");
            configManager.Set("test.reset", "modified");

            // Act
            configManager.ResetToDefaults();

            // Assert
            Assert.Equal("default", configManager.Get<string>("test.reset"));
        }

        [Fact]
        public void Get_WithComplexTypes_ShouldHandleCollections()
        {
            // Arrange
            var list = new List<string> { "item1", "item2", "item3" };
            configManager.Register("test.list", list, "List test");

            // Act
            var result = configManager.Get<List<string>>("test.list");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal("item1", result[0]);
        }

        // ====================================================================
        // PHASE 2 ADDITIONS: Configuration Validation Tests
        // ====================================================================

        [Fact]
        public void Validate_WithAllValidValues_ShouldReturnTrue()
        {
            // Arrange
            configManager.Register("test.valid", 50, "Valid value",
                validator: val => (int)val >= 0 && (int)val <= 100);
            configManager.Set("test.valid", 75);

            // Act
            bool result = configManager.Validate();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Validate_WithInvalidValue_ShouldResetToDefault()
        {
            // Arrange
            configManager.Register("test.invalid", 50, "Must be 0-100",
                validator: val => (int)val >= 0 && (int)val <= 100);

            // Manually set to invalid value (bypassing validator in Set)
            var configValue = configManager.GetType()
                .GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(configManager) as Dictionary<string, ConfigValue>;

            if (configValue != null && configValue.ContainsKey("test.invalid"))
            {
                configValue["test.invalid"].Value = 150; // Invalid!
            }

            // Act
            bool result = configManager.Validate();

            // Assert
            Assert.False(result); // Validation should fail
            Assert.Equal(50, configManager.Get<int>("test.invalid")); // Should reset to default
        }

        [Fact]
        public void Validate_WithNullValue_ShouldResetToDefault()
        {
            // Arrange
            configManager.Register("test.null", "default_value", "Should not be null");

            // Manually set to null (simulating corrupted config file)
            var configDict = configManager.GetType()
                .GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(configManager) as Dictionary<string, ConfigValue>;

            if (configDict != null && configDict.ContainsKey("test.null"))
            {
                configDict["test.null"].Value = null;
            }

            // Act
            bool result = configManager.Validate();

            // Assert
            Assert.False(result); // Validation should fail
            Assert.Equal("default_value", configManager.Get<string>("test.null")); // Should reset
        }

        [Fact]
        public void GetStrict_WithExistingKey_ShouldReturnValue()
        {
            // Arrange
            configManager.Register("test.exists", "test_value", "Exists test");

            // Act
            var result = configManager.GetStrict<string>("test.exists");

            // Assert
            Assert.Equal("test_value", result);
        }

        [Fact]
        public void GetStrict_WithMissingKey_ShouldThrowKeyNotFoundException()
        {
            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() =>
                configManager.GetStrict<string>("test.missing"));
        }

        [Fact]
        public void GetStrict_WithWrongType_ShouldThrowInvalidCastException()
        {
            // Arrange
            configManager.Register("test.string", "text_value", "String value");

            // Act & Assert
            Assert.Throws<InvalidCastException>(() =>
                configManager.GetStrict<int>("test.string"));
        }

        [Fact]
        public void GetStrict_WithNullForValueType_ShouldThrowInvalidOperationException()
        {
            // Arrange
            configManager.Register("test.nullable", 42, "Should not be null");

            // Manually set to null
            var configDict = configManager.GetType()
                .GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(configManager) as Dictionary<string, ConfigValue>;

            if (configDict != null && configDict.ContainsKey("test.nullable"))
            {
                configDict["test.nullable"].Value = null;
            }

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                configManager.GetStrict<int>("test.nullable"));
        }
    }
}
