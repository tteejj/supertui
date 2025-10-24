using System;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for security management - enables testing and mocking
    /// </summary>
    public interface ISecurityManager
    {
        void Initialize();
        void AddAllowedDirectory(string directory);
        bool ValidateFileAccess(string path, bool checkWrite = false);
        bool ValidateScriptExecution();
    }
}
