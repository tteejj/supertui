using System;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for security management - enables testing and mocking
    /// </summary>
    public interface ISecurityManager
    {
        /// <summary>
        /// Current security mode (read-only after initialization)
        /// </summary>
        SecurityMode Mode { get; }

        /// <summary>
        /// Initialize security manager with specified mode.
        /// Can only be called once.
        /// </summary>
        void Initialize(SecurityMode securityMode = SecurityMode.Strict);

        /// <summary>
        /// Legacy Initialize() for backward compatibility (uses Strict mode)
        /// </summary>
        void Initialize();

        /// <summary>
        /// Add a directory to the allowlist
        /// </summary>
        void AddAllowedDirectory(string directory);

        /// <summary>
        /// Validate file access against security policies
        /// </summary>
        bool ValidateFileAccess(string path, bool checkWrite = false);

        /// <summary>
        /// Check if script execution is allowed
        /// </summary>
        bool ValidateScriptExecution();

        /// <summary>
        /// Reset initialization state (for testing only)
        /// </summary>
        void ResetForTesting();
    }
}
