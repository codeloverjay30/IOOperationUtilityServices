using Castle.DynamicProxy;
using IOOperation.BaseUtilityServices;
using LoggerFactoryUtilityServices;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tasks.Model;
using TaskUtilityServices;
using ThreadLevelLockingUtilityServices;
using ThreadLevelLockingUtilityServices.Models;
using Xunit;

namespace ThreadLevelLockingUtilityServices.Tests
{
    public class ConcurrentTaskDispatcherTests
    {
        private readonly Mock<ISemaphoreSlimService<OperationModel, LockTimeoutException>> _mockLockService;
        private readonly Mock<ILoggerFactoryBaseUtilityService> _mockLoggerFactory;
        private readonly Mock<ITaskUtilityService> _mockTaskUtilityService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly ConcurrentTaskDispatcher<OperationModel, LockTimeoutException> _dispatcher;

        public ConcurrentTaskDispatcherTests()
        {
            _mockLoggerFactory = new Mock<ILoggerFactoryBaseUtilityService>();
            _mockTaskUtilityService = new Mock<ITaskUtilityService>();
            _mockLockService = new Mock<ISemaphoreSlimService<OperationModel, LockTimeoutException>>();
            _mockLogger = new Mock<ILogger>();

            // 設定 Logger Factory 回傳模擬的 Logger
            _mockLoggerFactory.Setup(f => f.Logger).Returns(_mockLogger.Object);

            _dispatcher = new ConcurrentTaskDispatcher<OperationModel, LockTimeoutException>(
                _mockLoggerFactory.Object,
                _mockTaskUtilityService.Object,
                _mockLockService.Object
            );
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnResult_WhenTaskSucceeds()
        {
            // Arrange
            var expectedResult = "Success";
            var priority = TaskPriority.High;
            var ct = CancellationToken.None;

            _mockTaskUtilityService
                .Setup(s => s.ToTaskQuickly(It.IsAny<ValueTask<string>>()))
                .Returns<ValueTask<string>>(vt => vt.AsTask());

            // 模擬 LockWithPriorityAsync 回傳一個可釋放的 IDisposable (使用 Mock<IDisposable>)
            var mockDisposable = new Mock<IDisposable>();
            _mockLockService.Setup(s => s.LockWithPriorityAsync(priority , ct))
                            .ReturnsAsync(mockDisposable.Object);

            // Act
            var result = await _dispatcher.ExecuteAsync(
                async (token) => { await Task.Yield(); return expectedResult; } ,
                priority ,
                ct
            );

            // Assert
            Assert.Equal(expectedResult , result);
            _mockLockService.Verify(s => s.LockWithPriorityAsync(priority , ct) , Times.Once);
            mockDisposable.Verify(d => d.Dispose() , Times.Once); // 確保 lock 有被釋放
        }

        [Fact]
        public async Task ExecuteAsync_ShouldCallReportFailure_WhenTaskFails()
        {
            // Arrange
            var priority = TaskPriority.Normal;
            var exception = new Exception("Test Exception");

            _mockTaskUtilityService
                .Setup(s => s.ToTaskQuickly(It.IsAny<ValueTask<string>>()))
                .Returns<ValueTask<string>>(vt => vt.AsTask());

            _mockLockService.Setup(s => s.LockWithPriorityAsync(priority , It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new Mock<IDisposable>().Object);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                _dispatcher.ExecuteAsync<string>(
                    (token) => throw exception ,
                    priority
                )
            );

            // 驗證是否呼叫了斷路器的回報機制
            _mockLockService.Verify(s => s.ReportFailure() , Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldPassCancellationTokenToTaskFunc()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            _mockLockService.Setup(s => s.LockWithPriorityAsync(It.IsAny<TaskPriority>() , cts.Token))
                            .ReturnsAsync(new Mock<IDisposable>().Object);

            bool tokenWasPassed = false;

            // Act
            await _dispatcher.ExecuteAsync(async (token) =>
            {
                if(token == cts.Token) tokenWasPassed = true;
                return await Task.FromResult(true);
            } , TaskPriority.Normal , cts.Token);

            // Assert
            Assert.True(tokenWasPassed);
        }
    }
}
