using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using DriveInfoUtilityServices;
using EnvironmentUtilityServices;
using FluentAssertions;
using IOOperationUtilityServices;
using Moq;
using NUnit.Framework;
using SymbolicLinkUtilityServices;

namespace IOOperationUtilityServices.Tests
{
    /// <summary>
    /// Contains unit tests for the <see cref="DirectoryUtilityService"/> class,
    /// ensuring robust, defensive behavior under various file system scenarios.
    /// </summary>
    [TestFixture]
    public class DirectoryUtilityServiceTests
    {
        private MockFileSystem _mockFileSystem;
        private DirectoryUtilityService _directoryUtilityService;

        private Mock<IFileUtilityService> _mockFileService;
        private Mock<IEnvironmentService> _mockEnvironmentService;
        private Mock<IOsUtilityService> _mockOsUtilityService;
        private Mock<IDriveInfoUtilityService> _mockDriveInfoService;
        private Mock<ISymbolicLinkUtilityService> _mockSymbolicLinkService;

        /// <summary>
        /// Sets up the test environment before each test execution, initializing mocks and the target service.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _mockFileSystem = new MockFileSystem();
            _mockFileService = new Mock<IFileUtilityService>();
            _mockEnvironmentService = new Mock<IEnvironmentService>();
            _mockOsUtilityService = new Mock<IOsUtilityService>();
            _mockDriveInfoService = new Mock<IDriveInfoUtilityService>();
            _mockSymbolicLinkService = new Mock<ISymbolicLinkUtilityService>();

            // Defensive Moq configurations to avoid unexpected null or parallel universe side effects
            _mockDriveInfoService.Setup(x => x.IsCrossDrive(It.IsAny<string>(), It.IsAny<string>()))
                                 .Returns(false);

            _directoryUtilityService = new DirectoryUtilityService(
                _mockFileSystem,
                new Lazy<IFileUtilityService>(() => _mockFileService.Object),
                new Lazy<IEnvironmentService>(() => _mockEnvironmentService.Object),
                new Lazy<IOsUtilityService>(() => _mockOsUtilityService.Object),
                new Lazy<IDriveInfoUtilityService>(() => _mockDriveInfoService.Object),
                new Lazy<ISymbolicLinkUtilityService>(() => _mockSymbolicLinkService.Object)
            );
        }

        #region Fast Enumerate Files Tests

        /// <summary>
        /// Verifies that FastEnumerateFiles returns only the files matching the specified search pattern.
        /// </summary>
        [Test]
        public void FastEnumerateFiles_ShouldReturnMatchingFiles()
        {
            // Arrange
            string searchPath = @"C:\SearchFolder";
            _mockFileSystem.AddDirectory(searchPath);
            _mockFileSystem.AddFile(@"C:\SearchFolder\a.xml", new MockFileData(string.Empty));
            _mockFileSystem.AddFile(@"C:\SearchFolder\b.txt", new MockFileData(string.Empty));

            // Act
            var result = _directoryUtilityService.FastEnumerateFiles(searchPath, "*.txt").ToList();

            // Assert - Pure FluentAssertions usage
            result.Should().ContainSingle()
                  .Which.Should().Contain("b.txt");
        }

        /// <summary>
        /// Verifies that FastEnumerateFilesAsync successfully asynchronously returns files matching the pattern.
        /// </summary>
        [Test]
        public async Task FastEnumerateFilesAsync_ShouldReturnMatchingFilesAsync()
        {
            // Arrange
            string searchPath = @"C:\SearchFolder";
            _mockFileSystem.AddDirectory(searchPath);
            _mockFileSystem.AddFile(@"C:\SearchFolder\a.xml", new MockFileData(string.Empty));
            _mockFileSystem.AddFile(@"C:\SearchFolder\b.txt", new MockFileData(string.Empty));

            // Act
            var result = new List<string>();
            await foreach (var file in _directoryUtilityService.FastEnumerateFilesAsync(searchPath, "*.txt"))
            {
                result.Add(file);
            }

            // Assert - Pure FluentAssertions usage
            result.Should().ContainSingle()
                  .Which.Should().Contain("b.txt");
        }

        #endregion

        #region Move Directory Tests

        /// <summary>
        /// Validates that when the target directory already exists, the service defensively deletes it before moving.
        /// </summary>
        [Test]
        public void TryToMoveDirectory_TargetExists_ShouldDeleteTargetAndMove()
        {
            // Arrange
            string sourceDir = @"C:\SourceDir";
            string targetDir = @"C:\TargetDir";
            string sourceFile = @"C:\SourceDir\data.txt";
            string targetFile = @"C:\TargetDir\data.txt";

            _mockFileSystem.AddDirectory(sourceDir);
            _mockFileSystem.AddFile(sourceFile, new MockFileData("content"));
            _mockFileSystem.AddDirectory(targetDir); // Simulate target directory already exists

            // Act
            Action act = () => _directoryUtilityService.TryToMoveDirectory(sourceDir, targetDir);

            // Assert
            // 1. Ensure no exceptions (like IOException) are thrown due to conflicting directory states
            act.Should().NotThrow();

            // 2. Structural state verifications using FluentAssertions
            _mockFileSystem.Directory.Exists(sourceDir).Should().BeFalse();
            _mockFileSystem.Directory.Exists(targetDir).Should().BeTrue();
            _mockFileSystem.File.Exists(targetFile).Should().BeTrue();
        }

        /// <summary>
        /// Verifies that TryToMoveDirectory throws an ArgumentNullException or appropriate exception 
        /// when the source path provided is null.
        /// </summary>
        [Test]
        public void TryToMoveDirectory_SourcePathNull_ShouldThrowArgumentException()
        {
            // Arrange
            string? invalidSource = null;
            string validTarget = @"C:\TargetDir";

            // Act
            Action act = () => _directoryUtilityService.TryToMoveDirectory(invalidSource!, validTarget);

            // Assert - Action interception for verifying critical defensive parameters
            act.Should().Throw<ArgumentException>()
               .WithMessage("*path*");
        }

        #endregion
    }
}