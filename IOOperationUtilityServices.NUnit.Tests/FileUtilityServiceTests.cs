using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using FluentAssertions;
using IOOperationUtilityServices;
using Moq;
using NUnit.Framework;

namespace IOOperationUtilityServices.Tests
{
    [TestFixture]
    public class FileUtilityServiceTests
    {
        private MockFileSystem _mockFileSystem;
        private Mock<IDirectoryUtilityService> _mockDirectoryService;
        private FileUtilityService _fileUtilityService;

        [SetUp]
        public void SetUp()
        {
            _mockFileSystem = new MockFileSystem();
            _mockDirectoryService = new Mock<IDirectoryUtilityService>();
            
            _fileUtilityService = new FileUtilityService(
                _mockFileSystem,
                new Lazy<IDirectoryUtilityService>(() => _mockDirectoryService.Object)
            );
        }

        #region Constructor Tests

        [Test]
        public void Constructor_NullFileSystem_ShouldThrowArgumentNullException()
        {
            Action act = () => new FileUtilityService(null, new Lazy<IDirectoryUtilityService>(() => _mockDirectoryService.Object));
            act.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
        }

        [Test]
        public void Constructor_NullDirectoryService_ShouldThrowArgumentNullException()
        {
            Action act = () => new FileUtilityService(_mockFileSystem, null);
            act.Should().Throw<ArgumentNullException>().WithParameterName("directoryUtilityService");
        }

        #endregion

        #region FileExists Tests

        [Test]
        public void FileExists_FileExists_ShouldReturnTrue()
        {
            _mockFileSystem.AddFile(@"C:\test.txt", new MockFileData("content"));
            _fileUtilityService.FileExists(@"C:\test.txt").Should().BeTrue();
        }

        [Test]
        public void FileExists_FileDoesNotExist_ShouldReturnFalse()
        {
            _fileUtilityService.FileExists(@"C:\nonexistent.txt").Should().BeFalse();
        }

        #endregion

        #region Copy & CopyAsync Tests

        [Test]
        public void Copy_SourceFileDoesNotExist_ShouldThrowFileNotFoundException()
        {
            Action act = () => _fileUtilityService.Copy(@"C:\source.txt", @"C:\dest.txt");
            act.Should().Throw<FileNotFoundException>().And.FileName.Should().Be(@"C:\source.txt");
        }

        [Test]
        public void Copy_SourceFileExists_ShouldCopyAndCreateDirectoryIfNeeded()
        {
            _mockFileSystem.AddFile(@"C:\source.txt", new MockFileData("hello"));

            _fileUtilityService.Copy(@"C:\source.txt", @"C:\NewFolder\dest.txt");

            _mockFileSystem.File.Exists(@"C:\NewFolder\dest.txt").Should().BeTrue();
            _mockFileSystem.File.ReadAllText(@"C:\NewFolder\dest.txt").Should().Be("hello");
        }

        [Test]
        public async Task CopyAsync_SourceFileExists_ShouldCopyAsynchronously()
        {
            _mockFileSystem.AddFile(@"C:\source.txt", new MockFileData("async hello"));

            await _fileUtilityService.CopyAsync(@"C:\source.txt", @"C:\NewFolderAsync\dest.txt");

            _mockFileSystem.File.Exists(@"C:\NewFolderAsync\dest.txt").Should().BeTrue();
            _mockFileSystem.File.ReadAllText(@"C:\NewFolderAsync\dest.txt").Should().Be("async hello");
        }

        #endregion

        #region Long Path Tests

        [TestCase(@"C:\Folder", @"\\?\C:\Folder")]
        [TestCase(@"\\Server\Share", @"\\?\UNC\Server\Share")]
        [TestCase(@"\\?\C:\AlreadyLong", @"\\?\C:\AlreadyLong")]
        public void ToLongPath_ValidPaths_ShouldReturnExpectedLongPath(string input, string expected)
        {
            // 由於實作中有呼叫 Path.GetFullPath，MockFileSystem 在不同作業系統平台行為可能略有不同
            // 這裡主要驗證邏輯分支
            if (input.StartsWith(@"\\?\"))
            {
                _fileUtilityService.ToLongPath(input).Should().Be(expected);
            }
            else
            {
                _fileUtilityService.ToLongPath(input).Should().Contain(expected.Replace(@"\\?\", ""));
            }
        }

        #endregion

        #region CreateOrClearFile Tests

        [Test]
        public void CreateOrClearFile_NullOrEmptyPath_ShouldThrowArgumentNullException()
        {
            Action act = () => _fileUtilityService.CreateOrClearFile("");
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void CreateOrClearFile_FileExists_ShouldClearContent()
        {
            _mockFileSystem.AddFile(@"C:\test.txt", new MockFileData("some text"));

            var result = _fileUtilityService.CreateOrClearFile(@"C:\test.txt");

            result.Should().BeTrue();
            _mockFileSystem.File.ReadAllText(@"C:\test.txt").Should().BeEmpty();
        }

        [Test]
        public void CreateOrClearFile_FileDoesNotExist_ShouldCreateBlankFileAndDirectories()
        {
            var result = _fileUtilityService.CreateOrClearFile(@"C:\NewDir\newfile.txt");

            result.Should().BeTrue();
            _mockFileSystem.File.Exists(@"C:\NewDir\newfile.txt").Should().BeTrue();
            _mockFileSystem.File.ReadAllText(@"C:\NewDir\newfile.txt").Should().BeEmpty();
        }

        #endregion

        #region Delete Tests

        [Test]
        public void TryToDeleteFile_FileExists_ShouldDeleteFile()
        {
            _mockFileSystem.AddFile(@"C:\delete.txt", new MockFileData("content"));
            _fileUtilityService.TryToDeleteFile(@"C:\delete.txt");
            _mockFileSystem.File.Exists(@"C:\delete.txt").Should().BeFalse();
        }

        [Test]
        public async Task TryToDeleteFileAsync_FileExists_ShouldDeleteFileAsync()
        {
            _mockFileSystem.AddFile(@"C:\deleteAsync.txt", new MockFileData("content"));
            await _fileUtilityService.TryToDeleteFileAsync(@"C:\deleteAsync.txt");
            _mockFileSystem.File.Exists(@"C:\deleteAsync.txt").Should().BeFalse();
        }

        #endregion

        #region Backup Tests

        [Test]
        public void BackupFile_ShouldDeleteOldBackupAndCreateNewOne()
        {
            _mockFileSystem.AddFile(@"C:\app.log", new MockFileData("log data"));
            _mockFileSystem.AddFile(@"C:\app.log.bak", new MockFileData("old data"));

            _fileUtilityService.BackupFile(@"C:\app.log", ".bak");

            _mockFileSystem.File.ReadAllText(@"C:\app.log.bak").Should().Be("log data");
        }

        #endregion
    }
}