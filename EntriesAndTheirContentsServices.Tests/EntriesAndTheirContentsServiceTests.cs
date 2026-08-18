using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using FluentAssertions;
using Moq;
using Xunit;
using IOOperationUtilityServices;
using FileCategorizationUtilityServices;

namespace EntriesAndTheirContentsServices.Tests;

public class EntriesAndTheirContentsServiceTests
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly Mock<IFileUtilityService> _mockFileUtilityService;
    private readonly Mock<IDirectoryUtilityService> _mockDirectoryUtilityService;
    private readonly Mock<IFileExtensionChecker> _mockFileExtensionChecker;
    private readonly Mock<IExcludedEntriesUtilityService> _mockExcludedEntriesUtilityService;
    private EntriesAndTheirContentsService _sut;

    public EntriesAndTheirContentsServiceTests()
    {
        // 使用真實的 MockFileSystem 隔離真實 I/O，杜絕測試平行時空的副作用
        _mockFileSystem = new MockFileSystem();

        _mockFileUtilityService = new Mock<IFileUtilityService>(MockBehavior.Strict);
        _mockDirectoryUtilityService = new Mock<IDirectoryUtilityService>(MockBehavior.Strict);
        _mockFileExtensionChecker = new Mock<IFileExtensionChecker>(MockBehavior.Strict);
        _mockExcludedEntriesUtilityService = new Mock<IExcludedEntriesUtilityService>(MockBehavior.Strict);

        InitializeSut();

        // 防禦設定：確保底層對 FileSystem 的存取引導至 mock 實例
        _mockFileUtilityService.Setup(x => x.FileSystem).Returns(_mockFileSystem);
    }
    
    private void InitializeSut()
    {
        _sut = new EntriesAndTheirContentsService(
            _mockFileSystem, 
            _mockFileUtilityService.Object, 
            _mockDirectoryUtilityService.Object, 
            _mockFileExtensionChecker.Object, 
            _mockExcludedEntriesUtilityService.Object);
    }

    #region Constructor Guard Tests

    [Fact]
    public void Constructor_WhenAnyDependencyIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act1 = () => new EntriesAndTheirContentsService(null!, _mockFileUtilityService.Object, _mockDirectoryUtilityService.Object);
        act1.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");

        Action act2 = () => new EntriesAndTheirContentsService(_mockFileSystem, null!, _mockDirectoryUtilityService.Object);
        act2.Should().Throw<ArgumentNullException>().WithParameterName("fileUtilityService");
    }

    #endregion

    #region GetSafeEntriesAndContents Tests

    [Fact]
    public void GetSafeEntriesAndContents_WhenRootPathDoesNotExist_ShouldThrowDirectoryNotFoundException()
    {
        // Arrange
        var invalidPath = @"C:\NonExistentFolder";

        // Act
        Action act = () => _sut.GetSafeEntriesAndContents(invalidPath, 1024).ToList();

        // Assert
        act.Should().Throw<DirectoryNotFoundException>()
           .WithMessage($"*targeted directory context was not found*");
    }

    [Fact]
    public void GetSafeEntriesAndContents_WhenFilesAreValid_ShouldReturnCorrectMetadataAndContent()
    {
        // Arrange
        var rootPath = @"C:\Workspace";
        _mockFileSystem.Directory.CreateDirectory(rootPath);
        
        var fileRelativePath = @"Sub\Program.cs";
        var fileFullPath = _mockFileSystem.Path.Combine(rootPath, fileRelativePath);
        var expectedContent = "using System;\nConsole.WriteLine(\"Hello\");";
        
        _mockFileSystem.AddFile(fileFullPath, new MockFileData(expectedContent));

        // Act
        var results = _sut.GetSafeEntriesAndContents(rootPath, maxFileSizeInBytes: 1024).ToList();

        // Assert
        results.Should().HaveCount(1);
        var entry = results.First();
        entry.RelativePath.Should().Be(fileRelativePath);
        entry.Content.Should().Be(expectedContent);
        entry.SizeInBytes.Should().Be(Encoding.UTF8.GetByteCount(expectedContent));
    }

    [Fact]
    public void GetSafeEntriesAndContents_WhenFileExceedsMaxLimit_ShouldFilterItOutSilently()
    {
        // Arrange
        var rootPath = @"C:\Workspace";
        _mockFileSystem.Directory.CreateDirectory(rootPath);
        
        var hugeFilePath = _mockFileSystem.Path.Combine(rootPath, "HugeAsset.json");
        // 建立 5MB 的虛擬檔案
        var hugeData = new byte[5 * 1024 * 1024]; 
        _mockFileSystem.AddFile(hugeFilePath, new MockFileData(hugeData));

        // Act
        var results = _sut.GetSafeEntriesAndContents(rootPath, maxFileSizeInBytes: 1 * 1024 * 1024).ToList();

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region LogEntriesOfDirectoryAndTheirContentsToFile (IndentedTextWriter) Tests

    [Fact]
    public void LogEntriesOfDirectoryAndTheirContents_ShouldWriteBeautifullyIndentedLogs()
    {
        // Arrange
        var srcDir = @"C:\ProjectSrc";
        var logFilePath = @"C:\Logs\output.log";

        _mockFileSystem.Directory.CreateDirectory(srcDir);
        _mockFileSystem.Directory.CreateDirectory(@"C:\Logs");

        // 建立測試專案檔案
        _mockFileSystem.AddFile(_mockFileSystem.Path.Combine(srcDir, "Class1.cs"), new MockFileData("public class Class1 {}"));

        _mockFileUtilityService
             .Setup(s => s.CreateOrClearFile(It.IsAny<string>()))
             .Callback<string>(path => { /* 這裡可以留空，僅供 Mock 攔截 */ });
            
        _mockDirectoryUtilityService.Setup(s => s.IsDirectory(It.IsAny<string>())).Returns(false);
    
        // Act
        _sut.LogEntriesOfDirectoryAndTheirContentsToFile(srcDir, "*.*", logFilePath, LogEntriesOptions.All);

        // Assert
        _mockFileUtilityService.Verify(s => s.CreateOrClearFile(logFilePath), Times.Once);
        
        var logContent = _mockFileSystem.File.ReadAllText(logFilePath);
        logContent.Should().Contain("Repository Scan Log");
        logContent.Should().Contain("[File Entry] : Class1.cs");
        logContent.Should().Contain("    public class Class1 {}"); // 驗證 IndentedTextWriter 的縮排行為
    }

    [Fact]
    public void LogEntriesOfDirectoryAndTheirContents_WhenOptionIsNetSolution_ShouldRespectFilters()
    {
        // Arrange
        var srcDir = @"C:\DotNetSolution";
        var logFilePath = @"C:\Logs\output.log";

        _mockFileSystem.Directory.CreateDirectory(srcDir);
        _mockFileSystem.Directory.CreateDirectory(@"C:\Logs");

        var binDir = _mockFileSystem.Path.Combine(srcDir, "bin");
        var sourceFile = _mockFileSystem.Path.Combine(srcDir, "Index.cshtml");

        _mockFileSystem.AddFile(binDir, new MockFileData(new byte[0]) { AllowedFileShare = FileShare.ReadWrite }); // 模擬目錄/檔案
        _mockFileSystem.AddFile(sourceFile, new MockFileData("<h1>Hello</h1>"));

        _mockFileUtilityService.Setup(s => s.CreateOrClearFile(logFilePath)).Verifiable();
        
        // 設定過濾條件模擬
        _mockDirectoryUtilityService.Setup(s => s.IsDirectory(binDir)).Returns(true);
        _mockDirectoryUtilityService.Setup(s => s.IsDirectory(sourceFile)).Returns(false);
        _mockFileUtilityService.Setup(s => s.FileExists(sourceFile)).Returns(true);

        _mockExcludedEntriesUtilityService.Setup(s => s.IsExcludedFolderName(binDir)).Returns(true);
        _mockFileExtensionChecker.Setup(s => s.NeedsToBeReplaced(sourceFile)).Returns(true); // .NetSolution 允許通過

        _mockFileUtilityService.Setup(s => s.CreateOrClearFile(logFilePath))
            .Callback<string>(path => { });
        // Act
        _sut.LogEntriesOfDirectoryAndTheirContentsToFile(srcDir, "*.*", logFilePath, LogEntriesOptions.NetSolution);

        // Assert
        var logContent = _mockFileSystem.File.ReadAllText(logFilePath);
        logContent.Should().Contain("Index.cshtml");
        logContent.Should().NotContain("bin");
    }

    [Fact]
    public void LogEntriesOfDirectoryAndTheirContents_WhenLogFileIsInSourceDirectory_ShouldPreventSelfWritingDeadlock()
    {
        // Arrange
        var srcDir = @"C:\ProjectSrc";
        var logFilePath = @"C:\ProjectSrc\output.log"; // 日誌位於掃描目錄內

        _mockFileSystem.Directory.CreateDirectory(srcDir);
        _mockFileSystem.AddFile(_mockFileSystem.Path.Combine(srcDir, "App.cs"), new MockFileData("Console.WriteLine();"));
        
        _mockFileUtilityService.Setup(s => s.CreateOrClearFile(logFilePath)).Verifiable();
        _mockDirectoryUtilityService.Setup(s => s.IsDirectory(It.IsAny<string>())).Returns(false);

        _mockFileUtilityService.Setup(s => s.CreateOrClearFile(logFilePath))
            .Callback<string>(path => { });
        // Act
        Action act = () => _sut.LogEntriesOfDirectoryAndTheirContentsToFile(srcDir, "*.*", logFilePath, LogEntriesOptions.All);

        // Assert
        act.Should().NotThrow();
        var logContent = _mockFileSystem.File.ReadAllText(logFilePath);
        logContent.Should().Contain("App.cs");
        logContent.Should().NotContain("[File Entry] : output.log"); // 防禦成功：自己跳過自己
    }

    #endregion
}