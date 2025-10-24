using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;

namespace SuperTUI.Infrastructure
{
    // ============================================================================
    // INPUT VALIDATION & SECURITY
    // ============================================================================

    /// <summary>
    /// Input validation utilities for security
    /// </summary>
    public static class ValidationHelper
    {
        // Common regex patterns
        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex AlphanumericRegex = new Regex(@"^[a-zA-Z0-9]+$", RegexOptions.Compiled);
        private static readonly Regex PathSeparatorRegex = new Regex(@"[<>:|?*]", RegexOptions.Compiled);

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
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Check for invalid characters (Windows path-specific: <>:|?*)
                if (PathSeparatorRegex.IsMatch(path))
                    return false;

                // Check for null bytes (path traversal technique)
                if (path.Contains('\0'))
                    return false;

                // Try to get full path - this will throw if path is malformed
                string fullPath = Path.GetFullPath(path);

                // Check for UNC paths if not allowed
                if (fullPath.StartsWith(@"\\") || fullPath.StartsWith("//"))
                {
                    // UNC paths like \\server\share are potentially dangerous
                    // Allow them only if explicitly enabled in config
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
    /// Security manager for file access validation and sandboxing
    /// </summary>
    public class SecurityManager : ISecurityManager
    {
        private static SecurityManager instance;
        public static SecurityManager Instance => instance ??= new SecurityManager();

        private readonly HashSet<string> allowedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private long maxFileSizeBytes;

        public void Initialize()
        {
            // Load from config
            var extensions = ConfigurationManager.Instance.Get<List<string>>("Security.AllowedExtensions");
            if (extensions != null)
            {
                foreach (var ext in extensions)
                {
                    allowedExtensions.Add(ext);
                }
            }

            maxFileSizeBytes = ConfigurationManager.Instance.Get<int>("Security.MaxFileSize", 10) * 1024 * 1024;

            // Add default allowed directories
            AddAllowedDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddAllowedDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

            Logger.Instance.Info("Security", "Security manager initialized");
        }

        public void AddAllowedDirectory(string directory)
        {
            try
            {
                string fullPath = Path.GetFullPath(directory);

                // Normalize by removing trailing separators for consistent comparison
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                allowedDirectories.Add(fullPath);
                Logger.Instance.Debug("Security", $"Added allowed directory: {fullPath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning("Security", $"Failed to add allowed directory {directory}: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates file access against security policies
        /// Checks: path format, allowed directories, file extensions, file size
        /// Logs all denied access attempts for security auditing
        /// </summary>
        public bool ValidateFileAccess(string path, bool checkWrite = false)
        {
            if (!ConfigurationManager.Instance.Get<bool>("Security.ValidateFileAccess", true))
            {
                Logger.Instance.Debug("Security", "File access validation is DISABLED - allowing all paths");
                return true; // Validation disabled
            }

            try
            {
                // Step 1: Validate path format and syntax
                if (!ValidationHelper.IsValidPath(path))
                {
                    // Security audit log
                    Logger.Instance.Warning("Security", $"SECURITY VIOLATION: Invalid path format attempted: '{path}'");
                    return false;
                }

                // Get normalized absolute path
                string fullPath = Path.GetFullPath(path);
                string normalizedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Step 2: Check if within allowed directories
                bool inAllowedDirectory = allowedDirectories.Any(dir =>
                    ValidationHelper.IsWithinDirectory(normalizedPath, dir));

                if (!inAllowedDirectory)
                {
                    // Security audit log - include original and normalized paths
                    Logger.Instance.Warning("Security",
                        $"SECURITY VIOLATION: Path outside allowed directories\n" +
                        $"  Original path: '{path}'\n" +
                        $"  Normalized path: '{normalizedPath}'\n" +
                        $"  Allowed directories: {string.Join(", ", allowedDirectories)}");
                    return false;
                }

                // Step 3: Check file extension allowlist
                string extension = Path.GetExtension(path);
                if (!string.IsNullOrEmpty(extension))
                {
                    // Extensions should be checked case-insensitively
                    if (allowedExtensions.Count > 0 && !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        // Security audit log
                        Logger.Instance.Warning("Security",
                            $"SECURITY VIOLATION: Disallowed file extension\n" +
                            $"  Path: '{path}'\n" +
                            $"  Extension: '{extension}'\n" +
                            $"  Allowed: {string.Join(", ", allowedExtensions)}");
                        return false;
                    }
                }

                // Step 4: Check file size limits (if file exists)
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    if (fileInfo.Length > maxFileSizeBytes)
                    {
                        // Security audit log
                        Logger.Instance.Warning("Security",
                            $"SECURITY VIOLATION: File exceeds size limit\n" +
                            $"  Path: '{path}'\n" +
                            $"  Size: {fileInfo.Length:N0} bytes\n" +
                            $"  Limit: {maxFileSizeBytes:N0} bytes ({maxFileSizeBytes / 1024 / 1024} MB)");
                        return false;
                    }
                }

                // Step 5: Additional write-specific checks
                if (checkWrite)
                {
                    // Check if directory is writable (for new files)
                    if (!File.Exists(fullPath))
                    {
                        string directory = Path.GetDirectoryName(fullPath);
                        if (!Directory.Exists(directory))
                        {
                            Logger.Instance.Warning("Security",
                                $"SECURITY VIOLATION: Attempt to write to non-existent directory\n" +
                                $"  Path: '{path}'\n" +
                                $"  Directory: '{directory}'");
                            return false;
                        }
                    }
                }

                // All checks passed
                Logger.Instance.Debug("Security", $"File access validated: '{path}' (normalized: '{normalizedPath}')");
                return true;
            }
            catch (Exception ex)
            {
                // Security audit log for unexpected errors
                Logger.Instance.Error("Security",
                    $"SECURITY ERROR: File access validation failed with exception\n" +
                    $"  Path: '{path}'\n" +
                    $"  Error: {ex.Message}", ex);
                return false;
            }
        }

        public bool ValidateScriptExecution()
        {
            return ConfigurationManager.Instance.Get<bool>("Security.AllowScriptExecution", false);
        }
    }
}
