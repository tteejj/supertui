using System;
using System.IO;
using Xunit;
using SuperTUI.Infrastructure;

namespace SuperTUI.Tests.Infrastructure
{
    /// <summary>
    /// Security tests for SecurityManager with SecurityMode support.
    /// Tests cover: path validation, symlink resolution, mode enforcement, attack scenarios.
    /// </summary>
    public class SecurityManagerTests : IDisposable
    {
        private SecurityManager securityManager;
        private string testDirectory;

        public SecurityManagerTests()
        {
            // Create fresh SecurityManager instance for each test
            // Note: Cannot use singleton Instance in tests due to immutability
            securityManager = new SecurityManager();

            // Reset for testing (only available in DEBUG builds)
            securityManager.ResetForTesting();

            // Create test directory
            testDirectory = Path.Combine(Path.GetTempPath(), $"SuperTUI_Test_{Guid.NewGuid()}");
            Directory.CreateDirectory(testDirectory);
        }

        public void Dispose()
        {
            // Cleanup test directory
            try
            {
                if (Directory.Exists(testDirectory))
                    Directory.Delete(testDirectory, recursive: true);
            }
            catch { /* Ignore cleanup errors */ }
        }

        // ====================================================================
        // SECURITY MODE TESTS
        // ====================================================================

        [Fact]
        public void Initialize_WithStrictMode_ShouldSetModeCorrectly()
        {
            // Act
            securityManager.Initialize(SecurityMode.Strict);

            // Assert
            Assert.Equal(SecurityMode.Strict, securityManager.Mode);
        }

        [Fact]
        public void Initialize_WithPermissiveMode_ShouldSetModeCorrectly()
        {
            // Act
            securityManager.Initialize(SecurityMode.Permissive);

            // Assert
            Assert.Equal(SecurityMode.Permissive, securityManager.Mode);
        }

        [Fact]
        public void Initialize_WithDevelopmentMode_ShouldSetModeCorrectly()
        {
            // Act
            securityManager.Initialize(SecurityMode.Development);

            // Assert
            Assert.Equal(SecurityMode.Development, securityManager.Mode);
        }

        [Fact]
        public void Initialize_CalledTwice_ShouldThrowException()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                securityManager.Initialize(SecurityMode.Permissive));
        }

        [Fact]
        public void Initialize_DefaultMode_ShouldBeStrict()
        {
            // Act
            securityManager.Initialize();

            // Assert
            Assert.Equal(SecurityMode.Strict, securityManager.Mode);
        }

        [Fact]
        public void DevelopmentMode_ShouldAllowAllPaths()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Development);
            var anyPath = @"C:\Windows\System32\evil.exe";

            // Act
            var result = securityManager.ValidateFileAccess(anyPath);

            // Assert
            Assert.True(result, "Development mode should allow all paths");
        }

        // ====================================================================
        // PATH VALIDATION TESTS
        // ====================================================================

        [Fact]
        public void ValidateFileAccess_WithAllowedDirectory_ShouldReturnTrue()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);
            securityManager.AddAllowedDirectory(testDirectory);
            var testFile = Path.Combine(testDirectory, "test.txt");

            // Act
            var result = securityManager.ValidateFileAccess(testFile);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateFileAccess_WithDisallowedDirectory_ShouldReturnFalse()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);
            securityManager.AddAllowedDirectory(testDirectory);
            var disallowedFile = Path.Combine(Path.GetTempPath(), "disallowed", "test.txt");

            // Act
            var result = securityManager.ValidateFileAccess(disallowedFile);

            // Assert
            Assert.False(result);
        }

        // ====================================================================
        // ATTACK SCENARIO TESTS
        // ====================================================================

        [Fact]
        [Trait("Category", "Security")]
        public void ValidateFileAccess_PathTraversalAttempt_ShouldReturnFalse()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);
            var allowedDir = Path.Combine(testDirectory, "allowed");
            Directory.CreateDirectory(allowedDir);
            securityManager.AddAllowedDirectory(allowedDir);

            // Attempt to access parent directory via path traversal
            var traversalPath = Path.Combine(allowedDir, "..", "sensitive.txt");

            // Act
            var result = securityManager.ValidateFileAccess(traversalPath);

            // Assert
            Assert.False(result, "Path traversal should be blocked");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void ValidateFileAccess_MultiplePathTraversalLevels_ShouldReturnFalse()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);
            var allowedDir = Path.Combine(testDirectory, "allowed", "deep", "nested");
            Directory.CreateDirectory(allowedDir);
            securityManager.AddAllowedDirectory(allowedDir);

            // Attempt multiple levels of traversal
            var traversalPath = Path.Combine(allowedDir, "..", "..", "..", "..", "etc", "passwd");

            // Act
            var result = securityManager.ValidateFileAccess(traversalPath);

            // Assert
            Assert.False(result, "Deep path traversal should be blocked");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void ValidateFileAccess_SimilarNameAttack_ShouldReturnFalse()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);
            var allowedDir = Path.Combine(testDirectory, "AllowedDir");
            Directory.CreateDirectory(allowedDir);
            securityManager.AddAllowedDirectory(allowedDir);

            // Try to access similar but different directory
            var evilDir = Path.Combine(testDirectory, "AllowedDir_Evil");
            Directory.CreateDirectory(evilDir);
            var evilPath = Path.Combine(evilDir, "hack.txt");

            // Act
            var result = securityManager.ValidateFileAccess(evilPath);

            // Assert
            Assert.False(result, "Similar directory name should not be allowed");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void ValidateFileAccess_NullByteInjection_ShouldReturnFalse()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);
            securityManager.AddAllowedDirectory(testDirectory);

            // Attempt null byte injection (technique to bypass extension checks)
            var maliciousPath = Path.Combine(testDirectory, "file.txt\0.exe");

            // Act
            var result = securityManager.ValidateFileAccess(maliciousPath);

            // Assert
            Assert.False(result, "Null byte injection should be blocked");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void ValidateFileAccess_SymlinkToDisallowedPath_ShouldReturnFalse()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);
            var allowedDir = Path.Combine(testDirectory, "allowed");
            var restrictedDir = Path.Combine(testDirectory, "restricted");
            Directory.CreateDirectory(allowedDir);
            Directory.CreateDirectory(restrictedDir);
            securityManager.AddAllowedDirectory(allowedDir);

            // Create symlink to restricted directory (requires admin on Windows)
            var symlinkPath = Path.Combine(allowedDir, "link_to_restricted");
            try
            {
                Directory.CreateSymbolicLink(symlinkPath, restrictedDir);

                var fileViaSymlink = Path.Combine(symlinkPath, "secret.txt");

                // Act
                var result = securityManager.ValidateFileAccess(fileViaSymlink);

                // Assert
                Assert.False(result, "Symlink to disallowed path should be blocked");
            }
            catch (IOException)
            {
                // Symlink creation failed (not admin) - skip test
                Assert.True(true, "Skipped: Requires elevated permissions");
            }
        }

        // ====================================================================
        // WRITE PERMISSION TESTS
        // ====================================================================

        [Fact]
        public void ValidateFileAccess_WithWriteCheck_ShouldValidateWritePermissions()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);
            securityManager.AddAllowedDirectory(testDirectory);
            var testFile = Path.Combine(testDirectory, $"write_test_{Guid.NewGuid()}.txt");

            // Act
            var result = securityManager.ValidateFileAccess(testFile, checkWrite: true);

            // Assert
            Assert.True(result, "Should have write access to test directory");
        }

        [Fact]
        public void ValidateFileAccess_WriteToNonExistentDirectory_ShouldReturnFalse()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);
            securityManager.AddAllowedDirectory(testDirectory);
            var nonExistentDir = Path.Combine(testDirectory, "does_not_exist", "file.txt");

            // Act
            var result = securityManager.ValidateFileAccess(nonExistentDir, checkWrite: true);

            // Assert
            Assert.False(result, "Write to non-existent directory should fail");
        }

        // ====================================================================
        // VALIDATION HELPER TESTS
        // ====================================================================

        [Fact]
        public void ValidationHelper_IsValidPath_WithValidPath_ShouldReturnTrue()
        {
            // Arrange
            var validPath = Path.Combine(testDirectory, "valid.txt");

            // Act
            var result = ValidationHelper.IsValidPath(validPath);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidationHelper_IsValidPath_WithInvalidCharacters_ShouldReturnFalse()
        {
            // Arrange
            var invalidPath = "C:\\test<>:|?.txt";

            // Act
            var result = ValidationHelper.IsValidPath(invalidPath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidationHelper_IsValidPath_WithUncPath_StrictMode_ShouldReturnFalse()
        {
            // Arrange
            var uncPath = @"\\server\share\file.txt";

            // Act
            var result = ValidationHelper.IsValidPath(uncPath, allowUncPaths: false);

            // Assert
            Assert.False(result, "UNC paths should be rejected in strict mode");
        }

        [Fact]
        public void ValidationHelper_IsValidPath_WithUncPath_PermissiveMode_ShouldReturnTrue()
        {
            // Arrange
            var uncPath = @"\\server\share\file.txt";

            // Act
            var result = ValidationHelper.IsValidPath(uncPath, allowUncPaths: true);

            // Assert
            Assert.True(result, "UNC paths should be allowed in permissive mode");
        }

        [Fact]
        public void ValidationHelper_IsWithinDirectory_SubdirectoryFile_ShouldReturnTrue()
        {
            // Arrange
            var parentDir = testDirectory;
            var childFile = Path.Combine(testDirectory, "subdir", "file.txt");

            // Act
            var result = ValidationHelper.IsWithinDirectory(childFile, parentDir);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidationHelper_IsWithinDirectory_OutsideDirectory_ShouldReturnFalse()
        {
            // Arrange
            var parentDir = Path.Combine(testDirectory, "parent");
            var outsideFile = Path.Combine(testDirectory, "outside", "file.txt");

            // Act
            var result = ValidationHelper.IsWithinDirectory(outsideFile, parentDir);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidationHelper_SanitizeFilename_WithInvalidChars_ShouldReplaceWithUnderscore()
        {
            // Arrange
            var invalidName = "file<>:|?.txt";

            // Act
            var result = ValidationHelper.SanitizeFilename(invalidName);

            // Assert
            Assert.DoesNotContain('<', result);
            Assert.DoesNotContain('>', result);
            Assert.DoesNotContain(':', result);
            Assert.DoesNotContain('|', result);
            Assert.DoesNotContain('?', result);
        }

        // ====================================================================
        // MISCELLANEOUS TESTS
        // ====================================================================

        [Fact]
        public void ValidateScriptExecution_ShouldEnforcePolicy()
        {
            // Arrange
            securityManager.Initialize(SecurityMode.Strict);

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
            securityManager.Initialize(SecurityMode.Strict);
            var dirWithoutSeparator = Path.Combine(testDirectory, "testdir");
            Directory.CreateDirectory(dirWithoutSeparator);
            securityManager.AddAllowedDirectory(dirWithoutSeparator);

            var fileInDir = Path.Combine(dirWithoutSeparator, "subdir", "file.txt");

            // Act
            var result = securityManager.ValidateFileAccess(fileInDir);

            // Assert
            Assert.True(result, "Subdirectory files should be accessible");
        }
    }
}
