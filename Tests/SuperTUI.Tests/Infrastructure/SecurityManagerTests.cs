using System;
using System.IO;
using FluentAssertions;
using SuperTUI.Infrastructure;
using Xunit;

namespace SuperTUI.Tests.Infrastructure
{
    public class SecurityManagerTests : IDisposable
    {
        private readonly SecurityManager securityManager;
        private readonly string tempDir;

        public SecurityManagerTests()
        {
            // Create temp directory for testing
            tempDir = Path.Combine(Path.GetTempPath(), "SuperTUI_Tests_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            securityManager = new SecurityManager();

            // Initialize with test configuration
            ConfigurationManager.Instance.Initialize(Path.Combine(tempDir, "config.json"));
            securityManager.Initialize();
            securityManager.AddAllowedDirectory(tempDir);
        }

        public void Dispose()
        {
            // Cleanup temp directory
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ValidateFileAccess_WithAllowedPath_ReturnsTrue()
        {
            // Arrange
            var filePath = Path.Combine(tempDir, "test.txt");

            // Act
            var result = securityManager.ValidateFileAccess(filePath);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ValidateFileAccess_WithPathOutsideAllowedDirectory_ReturnsFalse()
        {
            // Arrange
            var outsidePath = Path.Combine(Path.GetTempPath(), "NotAllowed", "test.txt");

            // Act
            var result = securityManager.ValidateFileAccess(outsidePath);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ValidateFileAccess_WithPathTraversalAttempt_ReturnsFalse()
        {
            // Arrange
            var traversalPath = Path.Combine(tempDir, "..", "..", "evil.txt");

            // Act
            var result = securityManager.ValidateFileAccess(traversalPath);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ValidateFileAccess_WithDirectoryNameSimilarToAllowed_ReturnsFalse()
        {
            // This tests the fix for the "C:\Allowed" vs "C:\Allowed_Evil" vulnerability

            // Arrange - Create a directory with similar name
            var evilDir = tempDir + "_Evil";
            Directory.CreateDirectory(evilDir);
            var evilFile = Path.Combine(evilDir, "test.txt");

            try
            {
                // Act
                var result = securityManager.ValidateFileAccess(evilFile);

                // Assert
                result.Should().BeFalse("because the directory is not actually allowed");
            }
            finally
            {
                Directory.Delete(evilDir, recursive: true);
            }
        }

        [Fact]
        public void ValidateFileAccess_WithSubdirectory_ReturnsTrue()
        {
            // Arrange
            var subDir = Path.Combine(tempDir, "SubFolder");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "test.txt");

            // Act
            var result = securityManager.ValidateFileAccess(filePath);

            // Assert
            result.Should().BeTrue("because subdirectories of allowed directories should be allowed");
        }

        [Fact]
        public void ValidateFileAccess_WithAllowedDirectoryItself_ReturnsTrue()
        {
            // Arrange - The allowed directory itself
            var dirPath = tempDir;

            // Act
            var result = securityManager.ValidateFileAccess(dirPath);

            // Assert
            result.Should().BeTrue("because the allowed directory itself should be valid");
        }

        [Fact]
        public void ValidateFileAccess_WithInvalidPathCharacters_ReturnsFalse()
        {
            // Arrange
            var invalidPath = Path.Combine(tempDir, "file<>:|?.txt");

            // Act
            var result = securityManager.ValidateFileAccess(invalidPath);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ValidateFileAccess_WhenValidationDisabled_ReturnsTrue()
        {
            // Arrange
            ConfigurationManager.Instance.Set("Security.ValidateFileAccess", false);
            var evilPath = "C:\\Windows\\System32\\evil.exe";

            // Act
            var result = securityManager.ValidateFileAccess(evilPath);

            // Assert
            result.Should().BeTrue("because validation is disabled");

            // Cleanup
            ConfigurationManager.Instance.Set("Security.ValidateFileAccess", true);
        }

        [Fact]
        public void AddAllowedDirectory_NormalizesPath()
        {
            // Arrange
            var secManager = new SecurityManager();
            secManager.Initialize();

            var pathWithTrailingSeparator = tempDir + Path.DirectorySeparatorChar;
            var pathWithoutTrailingSeparator = tempDir.TrimEnd(Path.DirectorySeparatorChar);

            // Act
            secManager.AddAllowedDirectory(pathWithTrailingSeparator);

            // Both should work
            var result1 = secManager.ValidateFileAccess(Path.Combine(pathWithoutTrailingSeparator, "test.txt"));
            var result2 = secManager.ValidateFileAccess(Path.Combine(pathWithTrailingSeparator, "test.txt"));

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeTrue();
        }

        [Fact]
        public void ValidateScriptExecution_ReturnsConfiguredValue()
        {
            // Arrange
            ConfigurationManager.Instance.Set("Security.AllowScriptExecution", true);

            // Act
            var result = securityManager.ValidateScriptExecution();

            // Assert
            result.Should().BeTrue();

            // Cleanup
            ConfigurationManager.Instance.Set("Security.AllowScriptExecution", false);
        }
    }
}
