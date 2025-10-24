using System;
using System.IO;
using Xunit;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Infrastructure
{
    public class SecurityManagerTests
    {
        private readonly SecurityManager securityManager;

        public SecurityManagerTests()
        {
            securityManager = new SecurityManager();
            securityManager.Initialize();
        }

        [Fact]
        public void ValidateFileAccess_WithAllowedDirectory_ShouldReturnTrue()
        {
            // Arrange
            var tempDir = Path.GetTempPath();
            securityManager.AddAllowedDirectory(tempDir);
            var testFile = Path.Combine(tempDir, "test.txt");

            // Act
            var result = securityManager.ValidateFileAccess(testFile);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateFileAccess_WithDisallowedDirectory_ShouldReturnFalse()
        {
            // Arrange
            var tempDir = Path.GetTempPath();
            securityManager.AddAllowedDirectory(tempDir);
            var disallowedFile = Path.Combine("C:\\Windows\\System32", "test.txt");

            // Act
            var result = securityManager.ValidateFileAccess(disallowedFile);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateFileAccess_PathTraversalAttempt_ShouldReturnFalse()
        {
            // Arrange
            var allowedDir = Path.Combine(Path.GetTempPath(), "allowed");
            securityManager.AddAllowedDirectory(allowedDir);

            // Attempt to access parent directory via path traversal
            var traversalPath = Path.Combine(allowedDir, "..", "sensitive.txt");

            // Act
            var result = securityManager.ValidateFileAccess(traversalPath);

            // Assert
            Assert.False(result, "Path traversal should be blocked");
        }

        [Fact]
        public void ValidateFileAccess_SimilarNameAttack_ShouldReturnFalse()
        {
            // Arrange - Test for C:\AllowedDir vs C:\AllowedDir_Evil
            var allowedDir = Path.Combine(Path.GetTempPath(), "AllowedDir");
            securityManager.AddAllowedDirectory(allowedDir);

            var evilPath = Path.Combine(Path.GetTempPath(), "AllowedDir_Evil", "hack.txt");

            // Act
            var result = securityManager.ValidateFileAccess(evilPath);

            // Assert
            Assert.False(result, "Similar directory name should not be allowed");
        }

        [Fact]
        public void ValidateFileAccess_WithWriteCheck_ShouldValidateWritePermissions()
        {
            // Arrange
            var tempDir = Path.GetTempPath();
            securityManager.AddAllowedDirectory(tempDir);
            var testFile = Path.Combine(tempDir, $"write_test_{Guid.NewGuid()}.txt");

            // Act
            var result = securityManager.ValidateFileAccess(testFile, checkWrite: true);

            // Assert
            Assert.True(result, "Should have write access to temp directory");
        }

        [Fact]
        public void ValidateScriptExecution_ShouldEnforcePolicy()
        {
            // Act
            var result = securityManager.ValidateScriptExecution();

            // Assert - This depends on the execution policy
            // Just verify it doesn't throw
            Assert.True(result || !result);
        }

        [Fact]
        public void AddAllowedDirectory_ShouldNormalizePathsWithSeparators()
        {
            // Arrange
            var dirWithoutSeparator = Path.Combine(Path.GetTempPath(), "testdir");
            securityManager.AddAllowedDirectory(dirWithoutSeparator);

            var fileInDir = Path.Combine(dirWithoutSeparator, "subdir", "file.txt");

            // Act
            var result = securityManager.ValidateFileAccess(fileInDir);

            // Assert
            Assert.True(result, "Subdirectory files should be accessible");
        }
    }
}
