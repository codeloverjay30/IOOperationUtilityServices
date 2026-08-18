using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;

namespace IOOperationUtilityServices.Tests
{
    public class FileHandlerTests : IDisposable
    {
        private readonly string _testRoot;

        public FileHandlerTests()
        {
            // 為每次測試建立唯一的暫時路徑
            _testRoot = Path.Combine(Path.GetTempPath() , Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testRoot);
        }

        [Fact]
        public async Task CopyAsync_SourceExists_ShouldCopyFileToDestination()
        {
            // Arrange
            var sourceFile = Path.Combine(_testRoot , "source.txt");
            var destFile = Path.Combine(_testRoot , "destSubDir" , "dest.txt");
            await File.WriteAllTextAsync(sourceFile , "Hello World");

            // Act
            await FileHandler.CopyAsync(sourceFile , destFile);

            // Assert
            File.Exists(destFile).Should().BeTrue();
            (await File.ReadAllTextAsync(destFile)).Should().Be("Hello World");
        }

        [Fact]
        public async Task CopyAsync_SourceMissing_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var fakeSource = Path.Combine(_testRoot , "nonexistent.txt");
            var destFile = Path.Combine(_testRoot , "dest.txt");

            // Act & Assert
            await Func(() => FileHandler.CopyAsync(fakeSource , destFile))
                .Should().ThrowAsync<FileNotFoundException>();
        }

        public void Dispose()
        {
            // 清理測試產生的檔案
            if(Directory.Exists(_testRoot))
                Directory.Delete(_testRoot , true);
        }

        // 輔助語法糖
        private Func<Task> Func(Func<Task> action) => action;
    }
}
