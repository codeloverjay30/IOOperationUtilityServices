using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using LoggerFactoryUtilityServices;
using AsyncKeyedLock;
using TaskUtilityServices;

namespace ThreadLevelLockingUtilityServices.Tests
{
    public class InProcessThreadLockingServiceTests
    {
        private readonly Mock<ILoggerFactoryBaseUtilityService> _mockLoggerFactory;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<ITaskUtilityService> _mockTaskUtility;
        private readonly AsyncKeyedLocker<string> _keyedLocker;
        private readonly InProcessThreadLockingService<string> _service;

        public InProcessThreadLockingServiceTests()
        {
            _mockLoggerFactory = new Mock<ILoggerFactoryBaseUtilityService>();
            _mockLogger = new Mock<ILogger>();
            _mockTaskUtility = new Mock<ITaskUtilityService>();
            _keyedLocker = new AsyncKeyedLocker<string>();

            // 設定 Mock Logger 回傳
            _mockLoggerFactory.Setup(x => x.Logger).Returns(_mockLogger.Object);
            _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

            _service = new InProcessThreadLockingService<string>(
                _mockLoggerFactory.Object ,
                _mockTaskUtility.Object ,
                _keyedLocker
            );
        }

        [Fact]
        public async Task LockAndExecuteValueAsync_ShouldReturnExpectedResult()
        {
            // Arrange
            string key = "test-key";
            string expected = "Success";
            Func<int , Task<string>> func = (val) => Task.FromResult(expected);

            // Act
            var result = await _service.LockAndExecuteValueAsync(key , func , 123);

            // Assert
            Assert.Equal(expected , result);
            // 驗證 Log 是否被呼叫 (LogLevel.Information 至少會呼叫兩次: Acquired & Released)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information ,
                    It.IsAny<EventId>() ,
                    It.Is<It.IsAnyType>((v , t) => v.ToString()!.Contains("Lock acquired")) ,
                    null ,
                    It.IsAny<Func<It.IsAnyType , Exception? , string>>()) ,
                Times.Once);
        }

        [Fact]
        public async Task LockAndExecuteValueAsync_WhenExceptionOccurs_ShouldLogErrorAndRethrow()
        {
            // Arrange
            string key = "error-key";
            var exception = new InvalidOperationException("Test Error");
            Func<int , Task<string>> func = (val) => throw exception;

            // Act & Assert
            var caughtException = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.LockAndExecuteValueAsync(key , func , 123)
            );

            Assert.Equal("Test Error" , caughtException.Message);

            // 驗證 LogErrorDuringExecution 是否被調用
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error ,
                    It.IsAny<EventId>() ,
                    It.Is<It.IsAnyType>((v , t) => v.ToString()!.Contains("An error occurred")) ,
                    exception ,
                    It.IsAny<Func<It.IsAnyType , Exception? , string>>()) ,
                Times.Once);
        }

        [Fact]
        public async Task LockAndExecuteValueAsync_ShouldEnsureSequentialExecution()
        {
            // Arrange
            string key = "concurrent-key";
            int executionCount = 0;

            // 模擬一個耗時任務
            Func<int , Task<int>> func = async (val) => {
                await Task.Delay(100);
                Interlocked.Increment(ref executionCount);
                return executionCount;
            };

            // Act: 同時發出兩個請求
            var task1 = _service.LockAndExecuteValueAsync(key , func , 1).AsTask();
            var task2 = _service.LockAndExecuteValueAsync(key , func , 1).AsTask();

            await Task.WhenAll(task1 , task2);

            // Assert
            // 如果鎖定有效，task1 跟 task2 的結果應該分別是 1 跟 2 (順序執行)
            Assert.NotEqual(await task1 , await task2);
            Assert.Equal(2 , executionCount);
        }
    }
}
