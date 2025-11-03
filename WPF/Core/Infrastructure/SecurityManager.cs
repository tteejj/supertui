using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using SuperTUI.Core.Infrastructure;

namespace SuperTUI.Infrastructure
{
    // ============================================================================
    // INPUT VALIDATION & SECURITY
    // ============================================================================

    /// <summary>
    /// Security mode determines the strictness of security validation.
    /// Once set during initialization, the mode is immutable to prevent runtime bypasses.
    /// </summary>
    public enum SecurityMode
    {
        /// <summary>
        /// Strict mode (default): All validation enabled, no bypasses.
        /// Recommended for production deployments.
        /// </summary>
        Strict,

        /// <summary>
        /// Permissive mode: Relaxed validation for specific scenarios.
        /// Allows UNC paths, larger file sizes, additional extensions.
        /// Use for trusted environments only.
        /// </summary>
        Permissive,

        /// <summary>
        /// Development mode: Minimal validation with extensive logging.
        /// All access attempts logged as warnings. NOT for production.
        /// </summary>
        Development
    }

    /// <summary>
    /// Input validation utilities for security
    /// </summary>
    public static class ValidationHelper
    {
        // Common regex patterns
        // Note: RegexOptions.Compiled removed - in .NET Core+, compiled regex is often slower
        // for simple patterns due to JIT overhead. For high-performance needs, use source generators.
        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        private static readonly Regex AlphanumericRegex = new Regex(@"^[a-zA-Z0-9]+$");

        public static bool IsValidEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email);
        }

        public static bool IsAlphanumeric(string input)
        {
            return !string.IsNullOrWhiteSpace(input) && AlphanumericRegex.IsMatch(input);
        }

        /// <summary>
        /// Validates that a path is safe and well-formed
        /// Does NOT validate against allowed directories - use ValidateFileAccess for that
        /// </summary>
        /// <param name="path">The path to validate</param>
        /// <param name="allowUncPaths">Whether to allow UNC paths (\\server\share)</param>
        public static bool IsValidPath(string path, bool allowUncPaths = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Check for invalid path characters using .NET's built-in validation
                // This correctly handles platform-specific rules (e.g., C:\ on Windows)
                char[] invalidChars = Path.GetInvalidPathChars();
                if (path.IndexOfAny(invalidChars) >= 0)
                    return false;

                // Check for null bytes (path traversal technique)
                if (path.Contains('\0'))
                    return false;

                // Try to get full path - this will throw if path is malformed
                string fullPath = Path.GetFullPath(path);

                // Check for UNC paths
                if (fullPath.StartsWith(@"\\") || fullPath.StartsWith("//"))
                {
                    // UNC paths like \\server\share can be legitimate in enterprise environments
                    // but are potentially dangerous and should be explicitly allowed
                    if (!allowUncPaths)
                        return false;
                }

                // Path is syntactically valid
                return true;
            }
            catch (ArgumentException)
            {
                // Path contains invalid characters or format
                return false;
            }
            catch (SecurityException)
            {
                // Caller doesn't have required permissions
                return false;
            }
            catch (NotSupportedException)
            {
                // Path format is not supported
                return false;
            }
            catch (PathTooLongException)
            {
                // Path exceeds system maximum length
                return false;
            }
            catch
            {
                // Any other exception means invalid path
                return false;
            }
        }

        /// <summary>
        /// Resolves symlinks and junction points to their target paths.
        /// On Windows 10+, this helps prevent symlink attacks.
        /// </summary>
        /// <param name="path">Path that may be a symlink</param>
        /// <returns>Resolved target path, or original path if not a symlink</returns>
        public static string ResolveSymlinks(string path)
        {
            try
            {
                // Check if file/directory exists
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);

                    // Resolve symlink (Windows 10+ / .NET 6+)
                    if (fileInfo.LinkTarget != null)
                    {
                        return Path.GetFullPath(fileInfo.LinkTarget);
                    }
                }
                else if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);

                    // Resolve symlink (Windows 10+ / .NET 6+)
                    if (dirInfo.LinkTarget != null)
                    {
                        return Path.GetFullPath(dirInfo.LinkTarget);
                    }
                }

                // Not a symlink or platform doesn't support LinkTarget
                return Path.GetFullPath(path);
            }
            catch
            {
                // If resolution fails, return original path
                // Validation will catch issues later
                return path;
            }
        }

        /// <summary>
        /// Validates that a path is within an allowed directory
        /// Properly handles path traversal attacks (../, ..\, etc.)
        /// </summary>
        public static bool IsWithinDirectory(string path, string allowedDirectory)
        {
            try
            {
                // Get absolute paths to prevent traversal attacks
                string fullPath = Path.GetFullPath(path);
                string fullAllowedPath = Path.GetFullPath(allowedDirectory);

                // Normalize by removing trailing separators for consistent comparison
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                fullAllowedPath = fullAllowedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Check if paths are equal (file/dir IS the allowed directory)
                if (fullPath.Equals(fullAllowedPath, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Check if file/dir is a child of the allowed directory
                // Must start with "allowedDir\" or "allowedDir/" to prevent:
                // - "/allowed" matching "/allowedButDifferent"
                // - "C:\allowed" matching "C:\allowed-different"
                return fullPath.StartsWith(fullAllowedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                       fullPath.StartsWith(fullAllowedPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Any error means we can't verify the path is safe
                return false;
            }
        }

        public static string SanitizeFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return "unnamed";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", filename.Split(invalidChars));
        }

        public static string SanitizeInput(string input, int maxLength = 1000)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Truncate if too long
            if (input.Length > maxLength)
                input = input.Substring(0, maxLength);

            // Remove control characters
            return new string(input.Where(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').ToArray());
        }
    }

    /// <summary>
    /// Security manager for file access validation and sandboxing.
    ///
    /// SECURITY FEATURES:
    /// - Immutable security mode (cannot be changed after initialization)
    /// - Path traversal attack prevention (../, symlinks)
    /// - Directory allowlisting
    /// - File extension allowlisting
    /// - File size limits
    /// - Comprehensive audit logging
    ///
    /// SECURITY MODES:
    /// - Strict: Full validation (production default)
    /// - Permissive: Relaxed for trusted environments (UNC paths, larger files)
    /// - Development: Minimal validation with extensive logging (NOT for production)
    /// </summary>
    public class SecurityManager : ISecurityManager
    {
        private static SecurityManager instance;
        private static readonly object initializationLock = new object();

        /// <summary>
        /// Singleton instance for infrastructure use.
        /// Widgets should use injected ISecurityManager. Infrastructure may use Instance when DI is unavailable.
        /// </summary>
        public static SecurityManager Instance => instance ??= new SecurityManager();

        private HashSet<string> allowedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private long maxFileSizeBytes;
        private SecurityMode mode = SecurityMode.Strict;  // Default to strictest mode
        private bool isInitialized = false;

        /// <summary>
        /// Current security mode (read-only after initialization)
        /// </summary>
        public SecurityMode Mode => mode;

        /// <summary>
        /// Initialize security manager with specified mode.
        /// Can only be called once - subsequent calls will throw an exception.
        /// Thread-safe for concurrent test execution.
        /// </summary>
        /// <param name="securityMode">Security mode to use</param>
        /// <exception cref="InvalidOperationException">If already initialized</exception>
        /// <exception cref="InvalidOperationException">If Development mode is used in Release builds</exception>
        public void Initialize(SecurityMode securityMode = SecurityMode.Strict)
        {
            lock (initializationLock)
            {
                if (isInitialized)
                {
                    throw new InvalidOperationException(
                        "SecurityManager is already initialized. Security mode cannot be changed after initialization.");
                }

                // SECURITY: Prevent Development mode in Release builds
                #if !DEBUG
                if (securityMode == SecurityMode.Development)
                {
                    throw new InvalidOperationException(
                        "SECURITY VIOLATION: Development security mode is not allowed in Release builds. " +
                        "Development mode bypasses ALL security validation and should NEVER be used in production. " +
                        "Use SecurityMode.Strict or SecurityMode.Permissive instead.");
                }
                #endif

                mode = securityMode;
                isInitialized = true;

                // Log security mode with severity appropriate to risk level
                Logger.Instance.Info("Security", $"SecurityManager initializing in {mode} mode");

                if (mode == SecurityMode.Development)
                {
                    // Log with CRITICAL severity for development mode
                    Logger.Instance.Critical("Security",
                        "╔════════════════════════════════════════════════════════════════╗\n" +
                        "║  ⚠️  DEVELOPMENT MODE ACTIVE - SECURITY DISABLED  ⚠️           ║\n" +
                        "╠════════════════════════════════════════════════════════════════╣\n" +
                        "║  ALL file access validation is BYPASSED                        ║\n" +
                        "║  Path traversal attacks are NOT prevented                      ║\n" +
                        "║  File size limits are NOT enforced                             ║\n" +
                        "║  Extension filtering is NOT applied                            ║\n" +
                        "║                                                                ║\n" +
                        "║  This mode is for DEBUGGING ONLY                               ║\n" +
                        "║  DO NOT USE IN PRODUCTION                                      ║\n" +
                        "║  DO NOT EXPOSE TO UNTRUSTED INPUT                              ║\n" +
                        "╚════════════════════════════════════════════════════════════════╝");

                    // Defer modal warning dialog to when Application is initialized
                    // This prevents crashes when Initialize() is called before WPF Application exists
                    if (System.Windows.Application.Current != null)
                    {
                        ShowDevelopmentModeWarning();
                    }
                    else
                    {
                        // Schedule warning for when application starts
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                            new Action(ShowDevelopmentModeWarning),
                            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                }
                else if (mode == SecurityMode.Permissive)
                {
                    Logger.Instance.Warning("Security",
                        "SecurityManager running in PERMISSIVE mode. " +
                        "UNC paths allowed, larger file sizes permitted. " +
                        "Use only in trusted environments.");
                }

                // Load from config
                var extensions = ConfigurationManager.Instance.Get<List<string>>("Security.AllowedExtensions");
                if (extensions != null)
                {
                    foreach (var ext in extensions)
                    {
                        allowedExtensions.Add(ext);
                    }
                }

                // File size limits depend on mode
                int defaultMaxSize = mode == SecurityMode.Permissive ? 100 : 10;  // 100MB permissive, 10MB strict
                maxFileSizeBytes = ConfigurationManager.Instance.Get<int>("Security.MaxFileSize", defaultMaxSize) * 1024 * 1024;

                // Add default allowed directories
                AddAllowedDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                AddAllowedDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

                Logger.Instance.Info("Security",
                    $"Security manager initialized successfully. Mode: {mode}, MaxFileSize: {maxFileSizeBytes / 1024 / 1024}MB, AllowedExtensions: {allowedExtensions.Count}");
            }
        }

        /// <summary>
        /// Legacy Initialize() for backward compatibility.
        /// Uses Strict mode by default.
        /// </summary>
        public void Initialize()
        {
            Initialize(SecurityMode.Strict);
        }

        public void AddAllowedDirectory(string directory)
        {
            ErrorHandlingPolicy.SafeExecute(
                ErrorCategory.Security,
                () =>
                {
                    string fullPath = Path.GetFullPath(directory);

                    // Normalize by removing trailing separators for consistent comparison
                    fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    allowedDirectories.Add(fullPath);
                    Logger.Instance.Debug("Security", $"Added allowed directory: {fullPath}");
                },
                context: $"Adding allowed directory: {directory}");
        }

        /// <summary>
        /// Validates file access against security policies.
        ///
        /// Security checks performed:
        /// 1. Path format validation (no invalid chars, null bytes)
        /// 2. Symlink resolution (prevents symlink attacks)
        /// 3. Directory allowlisting (path must be within allowed dirs)
        /// 4. File extension allowlisting (optional)
        /// 5. File size limits
        /// 6. Write-specific checks (directory existence)
        ///
        /// All denied access attempts are logged for security auditing.
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <param name="checkWrite">Whether to perform write-specific validation</param>
        /// <returns>True if access should be allowed, false otherwise</returns>
        public bool ValidateFileAccess(string path, bool checkWrite = false)
        {
            // Development mode: Log but allow (for debugging)
            if (mode == SecurityMode.Development)
            {
                Logger.Instance.Warning("Security",
                    $"DEV MODE: Allowing file access (validation bypassed): '{path}'");
                return true;
            }

            try
            {
                // Step 1: Validate path format and syntax
                bool allowUncPaths = (mode == SecurityMode.Permissive);
                if (!ValidationHelper.IsValidPath(path, allowUncPaths))
                {
                    // Security audit log
                    Logger.Instance.Warning("Security",
                        $"SECURITY VIOLATION: Invalid path format attempted\n" +
                        $"  Path: '{path}'\n" +
                        $"  Mode: {mode}");
                    return false;
                }

                // Step 2: Resolve symlinks to prevent symlink attacks
                // This ensures we're validating the ACTUAL target path, not a symlink
                string resolvedPath = ValidationHelper.ResolveSymlinks(path);
                if (resolvedPath != path)
                {
                    Logger.Instance.Debug("Security",
                        $"Symlink resolved: '{path}' -> '{resolvedPath}'");
                }

                // Step 3: Get normalized absolute path (canonicalize)
                string fullPath = Path.GetFullPath(resolvedPath);
                string normalizedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Step 4: Check if within allowed directories
                bool inAllowedDirectory = allowedDirectories.Any(dir =>
                    ValidationHelper.IsWithinDirectory(normalizedPath, dir));

                if (!inAllowedDirectory)
                {
                    // Security audit log - include original, resolved, and normalized paths
                    Logger.Instance.Warning("Security",
                        $"SECURITY VIOLATION: Path outside allowed directories\n" +
                        $"  Original path: '{path}'\n" +
                        $"  Resolved path: '{resolvedPath}'\n" +
                        $"  Normalized path: '{normalizedPath}'\n" +
                        $"  Mode: {mode}\n" +
                        $"  Allowed directories: {string.Join(", ", allowedDirectories)}");
                    return false;
                }

                // Step 5: Check file extension allowlist (if configured)
                string extension = Path.GetExtension(normalizedPath);
                if (!string.IsNullOrEmpty(extension) && allowedExtensions.Count > 0)
                {
                    // Extensions should be checked case-insensitively
                    if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        // Security audit log
                        Logger.Instance.Warning("Security",
                            $"SECURITY VIOLATION: Disallowed file extension\n" +
                            $"  Path: '{normalizedPath}'\n" +
                            $"  Extension: '{extension}'\n" +
                            $"  Mode: {mode}\n" +
                            $"  Allowed extensions: {string.Join(", ", allowedExtensions)}");
                        return false;
                    }
                }

                // Step 6: Check file size limits (if file exists)
                if (File.Exists(normalizedPath))
                {
                    var fileInfo = new FileInfo(normalizedPath);
                    if (fileInfo.Length > maxFileSizeBytes)
                    {
                        // Security audit log
                        Logger.Instance.Warning("Security",
                            $"SECURITY VIOLATION: File exceeds size limit\n" +
                            $"  Path: '{normalizedPath}'\n" +
                            $"  Size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)\n" +
                            $"  Limit: {maxFileSizeBytes:N0} bytes ({maxFileSizeBytes / 1024 / 1024} MB)\n" +
                            $"  Mode: {mode}");
                        return false;
                    }
                }

                // Step 7: Additional write-specific checks
                if (checkWrite)
                {
                    // Check if directory is writable (for new files)
                    if (!File.Exists(normalizedPath))
                    {
                        string directory = Path.GetDirectoryName(normalizedPath);
                        if (!Directory.Exists(directory))
                        {
                            Logger.Instance.Warning("Security",
                                $"SECURITY VIOLATION: Attempt to write to non-existent directory\n" +
                                $"  Path: '{normalizedPath}'\n" +
                                $"  Directory: '{directory}'\n" +
                                $"  Mode: {mode}");
                            return false;
                        }
                    }
                }

                // All checks passed
                Logger.Instance.Debug("Security",
                    $"File access validated: '{normalizedPath}' (mode: {mode})");
                return true;
            }
            catch (Exception ex)
            {
                // Security audit log for unexpected errors
                // DENY by default on any exception - use ErrorPolicy for consistent handling
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Security,
                    ex,
                    $"File access validation failed for path '{path}' in mode {mode}");
                return false;
            }
        }

        public bool ValidateScriptExecution()
        {
            return ConfigurationManager.Instance.Get<bool>("Security.AllowScriptExecution", false);
        }

        /// <summary>
        /// Shows a modal warning dialog when Development mode is active.
        /// This ensures the user is explicitly aware that security is disabled.
        /// </summary>
        private void ShowDevelopmentModeWarning()
        {
            ErrorHandlingPolicy.SafeExecute(
                ErrorCategory.Security,
                () =>
                {
                    // Use Dispatcher to ensure we're on UI thread
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var result = System.Windows.MessageBox.Show(
                            "╔════════════════════════════════════════════════════════╗\n" +
                            "║  ⚠️  DEVELOPMENT MODE - SECURITY DISABLED  ⚠️          ║\n" +
                            "╚════════════════════════════════════════════════════════╝\n\n" +
                            "Security validation is DISABLED:\n\n" +
                            "  • Path traversal attacks are NOT prevented\n" +
                            "  • File size limits are NOT enforced\n" +
                            "  • Extension filtering is NOT applied\n" +
                            "  • ALL file access is allowed\n\n" +
                            "This mode is for DEBUGGING ONLY.\n" +
                            "DO NOT USE IN PRODUCTION.\n" +
                            "DO NOT EXPOSE TO UNTRUSTED INPUT.\n\n" +
                            "Continue anyway?",
                            "Security Warning - Development Mode Active",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning,
                            System.Windows.MessageBoxResult.No);  // Default to NO

                        if (result == System.Windows.MessageBoxResult.No)
                        {
                            Logger.Instance.Critical("Security", "User declined to continue with Development mode. Exiting application.");
                            System.Environment.Exit(1);
                        }
                        else
                        {
                            Logger.Instance.Warning("Security", "User accepted Development mode warning and chose to continue.");
                        }
                    });
                },
                context: "Showing Development mode security warning dialog");
        }

        /// <summary>
        /// Reset initialization state for testing purposes ONLY.
        /// WARNING: This bypasses immutability guarantees and should NEVER be used in production.
        /// Thread-safe for concurrent test execution.
        /// </summary>
        public void ResetForTesting()
        {
            #if DEBUG
            lock (initializationLock)
            {
                isInitialized = false;
                mode = SecurityMode.Strict;
                allowedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                maxFileSizeBytes = 10 * 1024 * 1024;
                Logger.Instance.Warning("Security", "SecurityManager reset for testing - NOT for production use");
            }
            #else
            throw new InvalidOperationException(
                "ResetForTesting() is only available in DEBUG builds and should NEVER be called in production.");
            #endif
        }
    }
}
