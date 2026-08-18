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
using Moq;
using SymbolicLinkUtilityServices;
using Xunit;

namespace IOOperationUtilityServices.Tests
{
    public class DirectoryUtilityServiceTests
    {
        private readonly MockFileSystem _mockFileSystem;
        private readonly Mock<IFileUtilityService> _mockFileUtilityService;
        private readonly Mock<IEnvironmentService> _mockEnvironmentService;
        private readonly Mock<IOsUtilityService> _mockOsUtilityService;
        private readonly Mock<IDriveInfoUtilityService> _mockDriveInfoUtilityService;
        private readonly Mock<ISymbolicLinkUtilityService> _mockSymbolicLinkUtilityService;
        private readonly DirectoryUtilityService _service;

        public DirectoryUtilityServiceTests()
        {
            _mockFileSystem = new MockFileSystem();
            _mockFileUtilityService = new Mock<IFileUtilityService>();
            _mockEnvironmentService = new Mock<IEnvironmentService>();
            _mockOsUtilityService = new Mock<IOsUtilityService>();
            _mockDriveInfoUtilityService = new Mock<IDriveInfoUtilityService>();
            _mockSymbolicLinkUtilityService = new Mock<ISymbolicLinkUtilityService>();

            _mockEnvironmentService.Setup(x => x.IsWindows()).Returns(true);
            _mockDriveInfoUtilityService.Setup(x => x.IsCrossDrive(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            _service = new DirectoryUtilityService(
                _mockFileSystem,
                new Lazy<IFileUtilityService>(() => _mockFileUtilityService.Object),
                new Lazy<IEnvironmentService>(() => _mockEnvironmentService.Object),
                new Lazy<IOsUtilityService>(() => _mockOsUtilityService.Object),
                new Lazy<IDriveInfoUtilityService>(() => _mockDriveInfoUtilityService.Object),
                new Lazy<ISymbolicLinkUtilityService>(() => _mockSymbolicLinkUtilityService.Object)
            );
        }

        [Fact]
        public void Ctor_ShouldThrow_WhenFileSystemIsNull()
        {
            Action action = () => new DirectoryUtilityService(
                null!,
                new Lazy<IFileUtilityService>(() => _mockFileUtilityService.Object),
                new Lazy<IEnvironmentService>(() => _mockEnvironmentService.Object),
                new Lazy<IOsUtilityService>(() => _mockOsUtilityService.Object),
                new Lazy<IDriveInfoUtilityService>(() => _mockDriveInfoUtilityService.Object),
                new Lazy<ISymbolicLinkUtilityService>(() => _mockSymbolicLinkUtilityService.Object)
            );

            action.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
        }

        [Fact]
        public void Exists_ShouldReturnTrue_ForFileAndDirectory()
        {
            var filePath = @"C:\Root\file.txt";
            var dirPath = @"C:\Root\Folder";
            _mockFileSystem.AddDirectory(dirPath);
            _mockFileSystem.AddFile(filePath, new MockFileData("data"));

            _service.Exists(filePath).Should().BeTrue();
            _service.AnyExists(filePath).Should().BeTrue();
            _service.Exists(dirPath).Should().BeTrue();
            _service.AnyExists(dirPath).Should().BeTrue();
        }

        [Fact]
        public void CopyFileToDirectory_ShouldCreateDestinationDirectory_AndCallCopy()
        {
            var sourceFile = @"C:\Root\source.txt";
            var destinationDirectory = @"C:\Root\Destination";
            var childName = "copy.txt";
            _mockFileSystem.AddFile(sourceFile, new MockFileData("hello"));
            _mockFileUtilityService
                .Setup(x => x.Copy(sourceFile, Path.Combine(destinationDirectory, childName)))
                .Verifiable();

            _service.CopyFileToDirectory(sourceFile, destinationDirectory, childName);

            _mockFileSystem.Directory.Exists(destinationDirectory).Should().BeTrue();
            _mockFileUtilityService.Verify();
        }

        [Fact]
        public async Task CopyToDirectoryAsync_ShouldCreateDestinationDirectory_AndCallCopyAsync()
        {
            var sourceFile = @"C:\Root\source.txt";
            var destinationDirectory = @"C:\Root\Destination";
            var childName = "copy.txt";
            _mockFileSystem.AddFile(sourceFile, new MockFileData("hello"));
            _mockFileUtilityService
                .Setup(x => x.CopyAsync(sourceFile, Path.Combine(destinationDirectory, childName)))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _service.CopyToDirectoryAsync(sourceFile, destinationDirectory, childName);

            _mockFileSystem.Directory.Exists(destinationDirectory).Should().BeTrue();
            _mockFileUtilityService.Verify();
        }

        [Fact]
        public void DeleteChild_ShouldRemoveFileInsideDirectory()
        {
            var directoryPath = @"C:\Root";
            var childName = "child.txt";
            _mockFileSystem.AddDirectory(directoryPath);
            var childPath = Path.Combine(directoryPath, childName);
            _mockFileSystem.AddFile(childPath, new MockFileData("content"));

            _service.DeleteChild(directoryPath, childName);

            _mockFileSystem.File.Exists(childPath).Should().BeFalse();
        }

        [Fact]
        public async Task DeleteChildAsync_ShouldRemoveFileInsideDirectory()
        {
            var directoryPath = @"C:\Root";
            var childName = "child.txt";
            _mockFileSystem.AddDirectory(directoryPath);
            var childPath = Path.Combine(directoryPath, childName);
            _mockFileSystem.AddFile(childPath, new MockFileData("content"));

            await _service.DeleteChildAsync(directoryPath, childName);

            _mockFileSystem.File.Exists(childPath).Should().BeFalse();
        }

        [Fact]
        public void DeleteFileSystemItem_ShouldDeleteFileAndDirectory()
        {
            var folder = @"C:\Root\Folder";
            var filePath = @"C:\Root\file.txt";
            _mockFileSystem.AddDirectory(folder);
            _mockFileSystem.AddFile(filePath, new MockFileData("content"));

            _service.DeleteFileSystemItem(filePath);
            _service.DeleteFileSystemItem(folder);

            _mockFileSystem.File.Exists(filePath).Should().BeFalse();
            _mockFileSystem.Directory.Exists(folder).Should().BeFalse();
        }

        [Fact]
        public async Task DeleteFileSystemItemAsync_ShouldDeleteFile()
        {
            var filePath = @"C:\Root\file.txt";
            _mockFileSystem.AddFile(filePath, new MockFileData("content"));

            await _service.DeleteFileSystemItemAsync(filePath);

            _mockFileSystem.File.Exists(filePath).Should().BeFalse();
        }

        [Fact]
        public void EnumerateChildren_ShouldReturnDirectoryChildren()
        {
            var root = @"C:\Root";
            _mockFileSystem.AddDirectory(root);
            _mockFileSystem.AddFile(Path.Combine(root, "a.txt"), new MockFileData("a"));
            _mockFileSystem.AddFile(Path.Combine(root, "b.txt"), new MockFileData("b"));

            var children = _service.EnumerateChildren(root);

            children.Should().Contain(new[] { Path.Combine(root, "a.txt"), Path.Combine(root, "b.txt") });
        }

        [Fact]
        public async Task FindChildrenAsync_ShouldReturnDirectoryChildren()
        {
            var root = @"C:\Root";
            _mockFileSystem.AddDirectory(root);
            _mockFileSystem.AddFile(Path.Combine(root, "a.txt"), new MockFileData("a"));
            _mockFileSystem.AddFile(Path.Combine(root, "b.txt"), new MockFileData("b"));

            var children = await _service.FindChildrenAsync(root);

            children.Should().Contain(new[] { Path.Combine(root, "a.txt"), Path.Combine(root, "b.txt") });
        }

        [Fact]
        public void GetChildPath_ShouldReturnChildPath_WhenChildExists()
        {
            var root = @"C:\Root";
            _mockFileSystem.AddDirectory(root);
            _mockFileSystem.AddFile(Path.Combine(root, "item.txt"), new MockFileData("content"));

            var path = _service.GetChildPath(root, "item.txt");

            path.Should().Be(Path.Combine(root, "item.txt"));
        }

        [Fact]
        public void HasChild_ShouldReturnTrue_WhenChildExists()
        {
            var root = @"C:\Root";
            _mockFileSystem.AddDirectory(root);
            _mockFileSystem.AddFile(Path.Combine(root, "item.txt"), new MockFileData("content"));

            _service.HasChild(root, "item.txt").Should().BeTrue();
            _service.HasChildAsync(root, "item.txt").Result.Should().BeTrue();
        }

        [Fact]
        public async Task FastEnumerateFilesAsync_ReturnsMatchingFiles()
        {
            var root = @"C:\Root";
            _mockFileSystem.AddDirectory(root);
            _mockFileSystem.AddFile(Path.Combine(root, "match.log"), new MockFileData("data"));
            _mockFileSystem.AddFile(Path.Combine(root, "skip.txt"), new MockFileData("data"));

            var results = new List<string>();
            await foreach (var file in _service.FastEnumerateFilesAsync(root, "*.log"))
            {
                results.Add(file);
            }

            results.Should().ContainSingle().Which.Should().EndWith("match.log");
        }

        [Fact]
        public void TryToDeleteDirectory_ShouldRemoveExistingDirectory_AndNotThrow_WhenMissing()
        {
            var root = @"C:\Root";
            _mockFileSystem.AddDirectory(root);

            _service.TryToDeleteDirectory(root);
            _mockFileSystem.Directory.Exists(root).Should().BeFalse();

            Action action = () => _service.TryToDeleteDirectory(root);
            action.Should().NotThrow();
        }

        [Fact]
        public async Task TryToDeleteDirectoryAsync_ShouldRemoveExistingDirectory_AndNotThrow_WhenMissing()
        {
            var root = @"C:\Root";
            _mockFileSystem.AddDirectory(root);

            await _service.TryToDeleteDirectoryAsync(root);
            _mockFileSystem.Directory.Exists(root).Should().BeFalse();

            Func<Task> action = () => _service.TryToDeleteDirectoryAsync(root);
            await action.Should().NotThrowAsync();
        }
    }
}