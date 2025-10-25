using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for configuration management - enables testing and mocking
    /// </summary>
    public interface IConfigurationManager
    {
        void Initialize(string configFilePath);
        void Register(string key, object defaultValue, string description, string category = "General", Func<object, bool> validator = null);

        T Get<T>(string key, T defaultValue = default);
        T GetStrict<T>(string key);
        void Set<T>(string key, T value, bool saveImmediately = false);

        bool Validate();
        void Save();
        void Load();
        void ResetToDefaults();

        Dictionary<string, ConfigValue> GetCategory(string category);
        List<string> GetCategories();

        /// <summary>
        /// Reset initialization state for testing purposes ONLY.
        /// WARNING: This bypasses state guarantees and should NEVER be used in production.
        /// </summary>
        void ResetForTesting();
    }
}
