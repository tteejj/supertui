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
        void Set<T>(string key, T value, bool saveImmediately = false);

        void Save();
        void Load();
        void ResetToDefaults();

        Dictionary<string, ConfigValue> GetCategory(string category);
        List<string> GetCategories();
    }
}
