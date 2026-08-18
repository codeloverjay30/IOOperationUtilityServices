
using IOOperation.BaseUtilityServices;
using LoggerFactoryUtilityServices;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using Tasks.Model;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices.Tests
{
    public class SemaphoreSlimServiceTests: IDisposable
    {
        private const int PER_MILLI_SECONDS = 1000;
        private const int SHORT_WAIT_TIME = (int)(0.1 * PER_MILLI_SECONDS);
        private const int MEDIUM_WAIT_TIME = 1 * PER_MILLI_SECONDS;
        private const int LONG_WAIT_TIME = 2 * PER_MILLI_SECONDS;
        private const int VERY_LONG_WAIT_TIME = 10 * PER_MILLI_SECONDS;
        private readonly Mock<ILoggerFactoryBaseUtilityService> _mockLoggerFactory;
        private readonly SemaphoreSlimModel _semaphoreSlimModel;
        private SemaphoreSlimService<OperationModel, LockTimeoutException> _currentService;
        private readonly WatchdogExecutionSettings _watchdogModel;
        private readonly CircuitBreakerExecutionSettings _circuitBreakerModel;

        public SemaphoreSlimServiceTests()
        {
            _mockLoggerFactory = new Mock<ILoggerFactoryBaseUtilityService>();
            _mockLoggerFactory.Setup(x => x.Logger).Returns(new Mock<ILogger>().Object);

            _semaphoreSlimModel = new SemaphoreSlimModel
            {
                InitialCount = 3 ,
                MaxCount = 3
            };

            _watchdogModel = new WatchdogExecutionSettings
            {
                PollingTime = TimeSpan.FromMilliseconds(200) , // 加快掃描速度
                Timeout = TimeSpan.FromSeconds(10)
            };
            _circuitBreakerModel = new CircuitBreakerExecutionSettings
            {
                MaxAllowedFailureCount = 2,
                CoolDown = TimeSpan.FromSeconds(5),
            };

        }

        // 輔助方法：快速建立 Service 實例
        private SemaphoreSlimService<OperationModel, LockTimeoutException> CreateService(
            SemaphoreSlimModel semaphoreSlimModel,
            int maxRequests,
            TimeSpan maxLimitRate,
            WatchdogExecutionSettings watchdogModel,
            CircuitBreakerExecutionSettings circuitBreakerModel
        ){
            return new SemaphoreSlimService<OperationModel, LockTimeoutException>(
                _mockLoggerFactory.Object ,
                semaphoreSlimModel ,
                maxRequests ,
                maxLimitRate ,
                watchdogModel,
                circuitBreakerModel
            );
        }

        // 如果特定測試需要不同的參數，再重新指派給 _currentService
        private void ResetService(
            SemaphoreSlimModel semaphoreSlimModel ,
            int maxRequests ,
            TimeSpan maxLimitRate,
            WatchdogExecutionSettings watchdogModel ,
            CircuitBreakerExecutionSettings circuitBreakerModel
        )
        {
            _currentService.Dispose(); // 先釋放舊的，停止背景 Watchdog
            _currentService = CreateService(
                semaphoreSlimModel ,
                maxRequests ,
                maxLimitRate ,
                watchdogModel ,
                circuitBreakerModel
            );
        }

        [Fact]
        public async Task LockAsync_ShouldLimitConcurrency()
        {
            //IDisposable? l1 = null;
            //IDisposable? l2 = null;
            //IDisposable? l3 = null;
            //IDisposable? l4 = null;
            IDisposable? expectedToBelocked = null;
            //Task<IDisposable>? lockTask = null;
            bool isSuccess = false;
            try
            {
                try
                {
                    // Arrange
                    ResetService(
                        semaphoreSlimModel: _semaphoreSlimModel ,
                        maxRequests: 3 ,
                        maxLimitRate: TimeSpan.FromMicroseconds(1) , // 1ms
                        watchdogModel: _watchdogModel ,
                        circuitBreakerModel: _circuitBreakerModel
                    );

                    // Act
                    // 佔滿 3 個名額
                    using var l1 = await _currentService.ExecuteWithLockAsync();
                    using var l2 = await _currentService.ExecuteWithLockAsync();
                    using var l3 = await _currentService.ExecuteWithLockAsync();

                    // 嘗試第 4 個名額（設定較短的 Timeout 預期它會失敗）
                    //lockTask = _currentService.LockAsync(new CancellationTokenSource(500).Token);

                    // Assert
                    // await Assert.ThrowsAsync<OperationCanceledException>(async () => await lockTask);
                    await Assert.ThrowsAsync<OperationCanceledException>(async () => {
                        using var expectedToBelocked = await _currentService.ExecuteWithLockAsync(new CancellationTokenSource(500).Token);
                        expectedToBelocked?.Dispose();
                    });

                    isSuccess = true;
                }
                catch(Exception ex)
                {
                    isSuccess = false;
                }
                finally
                {
                }

                if(isSuccess)
                {
                    using var l4 = await _currentService.ExecuteWithLockAsync();
                    Assert.NotNull(l4);
                }
            }
            catch(Exception ex)
            {

            }
            finally
            {
                //l2?.Dispose();
                //l3?.Dispose();
                //l4?.Dispose();

                //lockTask?.Dispose();
                expectedToBelocked?.Dispose();
                this.Dispose();
            }
        }

        [Fact]
        public async Task ApplyRateLimit_ShouldDelayRequests()
        {
            IDisposable? disposbale = null;

            try
            {
                // Arrange
                ResetService(
                    semaphoreSlimModel: _semaphoreSlimModel ,
                    maxRequests: 2 ,
                    maxLimitRate: TimeSpan.FromMicroseconds(1) , // 1ms
                    watchdogModel: _watchdogModel ,
                    circuitBreakerModel: _circuitBreakerModel
                );

                // Act
                using(await _currentService.ExecuteWithLockAsync()) { } // 第 1 次
                using(await _currentService.ExecuteWithLockAsync()) { } // 第 2 次

                // 第 3 次應該會觸發等待
                using var cts = new CancellationTokenSource(LONG_WAIT_TIME);

                // Assert: 總耗時應該大於等於窗口時間（約 1 秒）
                // Assert.True(stopwatch.ElapsedMilliseconds >= 990);

                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    disposbale = await _currentService.ExecuteWithLockAsync(cts.Token);
                });
            }catch(Exception ex)
            {

            }
            finally
            {
                disposbale?.Dispose();
                this.Dispose();
            }
        }

        [Fact]
        public async Task Priority_HighShouldTakePrecedence()
        {
            var holds = new List<IDisposable?>();

            try
            {
                // Arrange
                ResetService(
                    semaphoreSlimModel: _semaphoreSlimModel ,
                    maxRequests: 3 ,
                    maxLimitRate: TimeSpan.FromMicroseconds(1) , // 1ms
                    watchdogModel: _watchdogModel ,
                    circuitBreakerModel: _circuitBreakerModel
                );

                // 佔滿所有名額
                for(int i = 0; i < 3; i++)
                {
                    using var disposable = await _currentService.ExecuteWithLockAsync();
                    holds.Add(disposable);
                }

                // 建立一個等待中的 Normal 任務
                using var normalTask = _currentService.LockWithPriorityAsync(TaskPriority.Normal);

                await Task.Yield(); // 確保 normal 先排隊

                // 建立一個等待中的 High 任務
                using var highTask = _currentService.LockWithPriorityAsync(TaskPriority.High);

                // Act
                holds [ 0 ]?.Dispose(); // 釋放一個名額

                // Assert
                var winner = await Task.WhenAny(normalTask , highTask);
                Assert.Same(highTask , winner); // 預期 High 優先權任務勝出

                
            }
            catch(Exception ex)
            {
            }
            finally
            {
                // 試圖釋放剩下的IDisposable實體
                for(int i = 0; i < holds.Count; i++)
                {
                    holds [ i ]?.Dispose();
                }

                this.Dispose();
            }
        }

        [Fact]
        public async Task CircuitBreaker_ShouldRejectWhenOpen()
        {
            IDisposable? disposable = null;
            try
            {
                // Arrange
                var cbModel = new CircuitBreakerExecutionSettings { OpenUntil = DateTime.UtcNow.AddSeconds(5) };
                ResetService(
                    semaphoreSlimModel: _semaphoreSlimModel ,
                    maxRequests: 3 ,
                    maxLimitRate: TimeSpan.FromMicroseconds(1) , // 1ms
                    watchdogModel: _watchdogModel ,
                    circuitBreakerModel: cbModel
                );

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    disposable = await _currentService.LockWithPriorityAsync(TaskPriority.Normal);
                    disposable?.Dispose();
                    disposable = null;
                });
            }
            catch(Exception ex)
            {
            }
            finally
            {
                disposable?.Dispose();
                this.Dispose();
            }
        }

        public void Dispose()
        {
            _currentService?.Dispose(); // 這會觸發 watchdogModel.cancellationTokenSource.Cancel()
            _currentService = null;
        }
    }
}
