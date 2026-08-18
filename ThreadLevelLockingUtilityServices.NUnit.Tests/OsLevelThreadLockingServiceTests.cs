using Moq;
using TaskUtilityServices;

namespace ThreadLevelLockingUtilityServices.NUnit.Tests
{
    [TestFixture]
    public class OsLevelThreadLockingServiceTests
    {
        private Mock<ITaskUtilityService> _mockTaskUtility;
        private OsLevelThreadLockingService _service;

        [SetUp]
        public void Setup()
        {
            _mockTaskUtility = new Mock<ITaskUtilityService>();
            _service = new OsLevelThreadLockingService(_mockTaskUtility.Object);
        }

        [Test]
        public async Task LockSystemWideValueAsync_ShouldReturnExpectedResult_WhenNoConflict()
        {
            // Arrange
            string key = "TestKey_" + Guid.NewGuid().ToString();
            string expectedResult = "Success";
            int inputArg = 10;

            Func<int , Task<string>> mockFunc = (arg) => Task.FromResult(expectedResult);

            // Act
            var result = await _service.LockSystemWideValueAsync(key , mockFunc , inputArg);

            // Assert
            Assert.That(result , Is.EqualTo(expectedResult));
        }

        [Test]
        public void LockSystemWideValueAsync_ShouldThrowTimeoutException_WhenLockIsHeld()
        {
            // Arrange
            string key = "TimeoutKey_" + Guid.NewGuid().ToString();
            var timeout = TimeSpan.FromMilliseconds(500);

            // 建立一個外部 Mutex 模擬鎖定被另一個進程/執行緒佔用
            using var externalMutex = new Mutex(true , $@"Global\{key}");

            Func<int , Task<string>> mockFunc = (arg) => Task.FromResult("Should Not Reach Here");

            // Act & Assert
            Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await _service.LockSystemWideValueAsync(key , mockFunc , 1 , timeout);
            });
        }

        [Test]
        public async Task LockSystemWideValueAsync_ShouldTimeout()
        {
            // Arrange
            string key = "ConcurrencyKey_" + Guid.NewGuid().ToString();
            var shortTimeout = TimeSpan.FromMilliseconds(50);
            var tcs = new TaskCompletionSource<bool>();

            // 使用一個 Task 確保先佔住鎖定
            var task1 = Task.Run(async () =>
            {
                return await _service.LockSystemWideValueAsync(key , async (arg) =>
                {
                    tcs.SetResult(true); // 通知外部：我已經拿到鎖並開始執行了
                    await Task.Delay(200); // 佔用時間長一點，確保覆蓋 task2 的 timeout
                    return true;
                } , 1);
            });

            // 等待 task1 確定拿到鎖
            await tcs.Task;

            // Act
            // 現在呼叫 task2，它絕對拿不到鎖，應該會觸發 Timeout
            var task2 = _service.LockSystemWideValueAsync(key , async (arg) => true , 2 , timeout: shortTimeout).AsTask();

            // 等待 task2 完成（不論成功或失敗）
            try
            {
                await task2;
            }
            catch { /* 忽略異常，稍後 Assert 檢查 */ }

            // Assert
            Assert.That(task2.IsFaulted , Is.True);
            Assert.That(task2.Exception.InnerException , Is.InstanceOf<TimeoutException>());

            await task1; // 清理資源
        }

        [Test]
        public async Task LockSystemWideValueAsync_ShouldEnsureMutualExclusion()
        {
            // Arrange
            string key = "ConcurrencyKey_" + Guid.NewGuid().ToString();
            int runningTasks = 0;
            int maxConcurrentTasks = 0;
            object lockObj = new object();
            TimeSpan timeout = TimeSpan.FromSeconds(100); // 100s
            Func<int , Task<bool>> longRunningFunc = async (arg) =>
            {
                lock(lockObj)
                {
                    runningTasks++;
                    maxConcurrentTasks = Math.Max(maxConcurrentTasks , runningTasks);
                }

                await Task.Delay(100); // 模擬耗時工作

                lock(lockObj)
                {
                    runningTasks--;
                }
                return true;
            };

            // Act: 同時啟動三個競爭同一個 Key 的任務
            var task1 = _service.LockSystemWideValueAsync(key , longRunningFunc , 1,timeout: timeout);
            var task2 = _service.LockSystemWideValueAsync(key , longRunningFunc , 2 , timeout: timeout);
            var task3 = _service.LockSystemWideValueAsync(key , longRunningFunc , 3, timeout: timeout);

            Assert.DoesNotThrowAsync(async () =>
            {
                await Task.WhenAll(task1.AsTask() , task2.AsTask() , task3.AsTask());
            } , "任務執行逾時或發生錯誤");

            // Assert: 即使是並發呼叫，因為有 Mutex，同一時間執行的任務數應該永遠為 1
            Assert.That(maxConcurrentTasks , Is.EqualTo(1));
        }
    }
}
