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
    }
}
