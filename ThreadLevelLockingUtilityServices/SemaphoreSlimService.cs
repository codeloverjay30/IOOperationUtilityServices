using CustomDataAnnotations.Maintenance;
using IOOperation.BaseUtilityServices;
using LoggerFactoryUtilityServices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;
using Tasks.Model;
using TaskUtilityServices;
using ThreadLevelLockingUtilityServices.Consts;
using ThreadLevelLockingUtilityServices.Models;
using static TaskUtilityServices.TaskExtensions;
namespace ThreadLevelLockingUtilityServices
{
    /// <summary>
    /// Utility service about <see cref="SemaphoreSlim"/> class.
    /// </summary>
    /// <typeparam name="TModel"><see="IOOperation.BaseUtilityServices.OperationModel"/></typeparam>
    /// <typeparam name="TException"><see="global::System.IO.Exception"></typeparam>
    /// <remarks>
    /// This class has been tested a few times. 
    /// However, it isn't integrated with `Polly` v.8 which is more stable and has more functionality.'
    /// Use <see cref="ThreadLevelLockingUtilityServices.PollySemaphoreSlimService{TModel, TResultStatus, TException}"/> class instead. 
    /// </remarks>
    [Obsolete("It can't retry to execute the task many times, and isn't integrated with Polly v8. Use <see cref=\"global::ThreadLevelLockingUtilityServices.PollySemaphoreSlimService\"/> class instead.")]
    [TechnicalDebt(CategoryType.NeedsChanged | CategoryType.InstableBehaviorInMultipleThreadsIssue,"global::ThreadLevelLockingUtilityServices.PollySemaphoreSlimService")]
    public partial class SemaphoreSlimService<TModel,TException>:
        ThreadLevelLockingBaseUtilityService, ISemaphoreSlimService<TModel,TException>, IDisposable
        where TModel : OperationModel, new()
        where TException : Exception, new()
    {
        [LoggerMessage(Level = LogLevel.Warning , Message = "Please wait {WaitTime} ms The next available token")]
        static partial void LogAvailableToken(ILogger logger,double waitTime);

        [LoggerMessage(Level = LogLevel.Warning , Message = "rate limit updates：{oldRateLimitUpdates} -> {newRateLimeUpdates}")]
        static partial void LogRateLimitUpdates(ILogger logger,int oldRateLimitUpdates,int newRateLimeUpdates);
        /// <summary>
        /// Utility service to create the <see cref="Microsoft.Extensions.Logging.ILogger">
        /// </summary>
        public ILoggerFactoryBaseUtilityService LoggerFactoryService { get; set; }

        /// <summary>
        /// Utility service to convert <see cref="ValueTask"/> to <see cref="Task"/> 
        /// </summary>
        public ITaskUtilityService TaskService { get; set; }

        /// <summary>
        /// The configuration for <see cref="GlobalConcurrencySemaphoreManager"/> and <see cref="GlobalConcurrencySemaphore"/> used in <see cref="SemaphoreSlimService"/> class.
        /// </summary>
        public SemaphoreSlimModel GlobalSemaphoreSlimModel { get; set; }

        /// <summary>
        /// The builder class to build a <see cref="SemaphoreSlim"/> used by <see cref="GlobalConcurrencySemaphore"/>
        /// </summary>
        public ISemaphoreSlimManager GlobalConcurrencySemaphoreManager { get; set; }

        /// <summary>
        /// The global semaphore (normal + emergency) (<see cref="SemaphoreSlim"/> type)
        /// </summary>
        public SemaphoreSlim GlobalConcurrencySemaphore => GlobalConcurrencySemaphoreManager.SemaphoreSlimInstance;

        /// <summary>
        /// The configuration for <see cref="NormalTaskSemaphoreManager"/> and <see cref="NormalTaskSemaphore"/> used in <see cref="SemaphoreSlimService"/> class.
        /// </summary>
        public SemaphoreSlimModel NormalSemaphoreSlimModel { get; set; }

        /// <summary>
        /// The builder class to build a <see cref="SemaphoreSlim"/> used by <see cref="NormalTaskSemaphore"/>
        /// </summary>
        public ISemaphoreSlimManager NormalTaskSemaphoreManager { get; set; }


        //// <summary>
        /// The normal semaphore (see cref="SemaphoreSlim"/> type)
        /// </summary>
        public SemaphoreSlim NormalTaskSemaphore => NormalTaskSemaphoreManager.SemaphoreSlimInstance;

        /// <summary>
        /// The configuration of watchdog.
        /// </summary>
        public WatchdogExecutionSettings Watchdog { get; set; }

        /// <summary>
        /// The configuration used in <see cref="ServiceHealthMetrics"/> class.
        /// </summary>
        public ServiceHealthMetrics ServHealthMetrics { get; set; }

        /// <summary>
        /// The lock to read the history (see <see cref="RateLimitBuffer"/>)
        /// </summary>
        private readonly object _historyLock = new object();

        /// <summary>
        /// The internal cancellation token source.
        /// </summary>
        private CancellationTokenSource _globalInternalStop = new CancellationTokenSource();

        /// <summary>
        /// The configuration of circuit breaker that will be used in <see cref="SemaphoreSlimService"/> class.
        /// </summary>
        public CircuitBreakerExecutionSettings CircuitBreaker { get; set; }

        /// <summary>
        /// The maximum allowed requests per window at same time
        /// </summary>
        public int MaxRequestsPerWindow { get; set; }

        /// <summary>
        /// The maximum allowed requests during the period.
        /// </summary>
        public TimeSpan MaxLimitRate { get; set; }

        /// <summary>
        /// The current adjusted maximum allowed requests per window at same time
        /// </summary>
        public int CurrentMaxRequestsPerWindow { get; set; }

        /// <summary>
        /// The history time of requests.
        /// </summary>
        public DateTime [ ] RateLimitBuffer { get; set; }

        /// <summary>
        /// The head pointer to the buffer (<see cref="RateLimitBuffer"/>)
        /// </summary>
        private int _bufferHead = 0;

        /// <summary>
        /// The tail pointer to the buffer (<see cref="RateLimitBuffer"/>)
        /// </summary>
        private int _bufferTail = 0;

        /// <summary>
        /// The total count on the buffer (<see cref="RateLimitBuffer"/>) used
        /// </summary>
        private int _bufferCount = 0;
        /// <summary>
        /// The lock used for <see cref="CurrentMaxRequestsPerWindow"/>.
        /// </summary>

        private readonly object _rateLimitAdjustmentLock = new object();

        /// <summary>
        /// The semaphore (<see cref="SemaphoreSlim"/> type) used for tasks with priority.
        /// </summary>
        private readonly SemaphoreSlim PrioritySemaphore = new SemaphoreSlim(1 , 1);

        /// <summary>
        /// use waiter that supports the handle the tasks by priority. 
        /// </summary>
        private readonly PriorityQueue<PriorityWaiter<TModel> , int> _waitingQueue = new();

        private bool _needToStartWatchDog = false;
        /// <summary>
        /// The lock used by <see cref="_waitingQueue"/>
        /// </summary>
        private readonly object _queueLock = new object();

        /// <summary>
        /// A flag that stores the status of Watch dog is running or not.
        /// </summary>
        private bool _isWatchdogRunning = false;

        /// <summary>
        /// A lock used for <seealso cref="StartWatchdog"/>
        /// </summary>
        private readonly object _watchdogLock = new object();

        /// <summary>
        /// A flag that stores the status of the semaphore has been closed or being closed.
        /// </summary>
        /// <remarks>
        /// DON'T use volatile modifier. Use Volatile.Write and Volatile.Read so that
        /// it tells the compiler and the CPU:
        /// "Do not cache this value in a register, and do not reorder instructions around it."
        /// It prevents the CPU from optimizing the read/write by using a local cache.
        /// </remarks>
        private bool _isShuttingDown = false;

        private ILogger _logger => LoggerFactoryService.Logger;
        public ILogger Logger => _logger;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="loggerFactoryService"></param>
        /// <param name="globalSemaphoreSlimModel"></param>
        /// <param name="maxRequestsPerWindow"></param>
        /// <param name="maxLimitRate"></param>
        /// <param name="watchdogModel"></param>
        /// <param name="circuitBreakerModel"></param>
        public SemaphoreSlimService(
            ILoggerFactoryBaseUtilityService loggerFactoryService ,
            SemaphoreSlimModel globalSemaphoreSlimModel ,
            int maxRequestsPerWindow ,
            TimeSpan maxLimitRate ,
            WatchdogExecutionSettings watchdogModel ,
            CircuitBreakerExecutionSettings circuitBreakerModel ,
            bool needToStartWatchDog = false
        ) :this(loggerFactoryService,null,globalSemaphoreSlimModel,maxRequestsPerWindow,maxLimitRate,watchdogModel,circuitBreakerModel,needToStartWatchDog)
        {
            
        }

        public SemaphoreSlimService(
            ILoggerFactoryBaseUtilityService loggerFactoryService ,
            ITaskUtilityService? taskUtilityService,
            SemaphoreSlimModel globalSemaphoreSlimModel ,
            int maxRequestsPerWindow ,
            TimeSpan maxLimitRate ,
            WatchdogExecutionSettings watchdogModel ,
            CircuitBreakerExecutionSettings circuitBreakerModel ,
            bool needToStartWatchDog = false
        ) : base(loggerFactoryService)
        {
            var normalMax = Math.Max(1 , globalSemaphoreSlimModel.InitialCount - 1);

            LoggerFactoryService = loggerFactoryService;
            TaskService = taskUtilityService ?? new TaskUtilityService(); // 預設使用TaskUtilityService這個Service
            GlobalSemaphoreSlimModel = globalSemaphoreSlimModel;
            NormalSemaphoreSlimModel = new SemaphoreSlimModel { InitialCount = normalMax , MaxCount = normalMax };

            GlobalConcurrencySemaphoreManager = new SemaphoreSlimManager(GlobalSemaphoreSlimModel);
            NormalTaskSemaphoreManager = new SemaphoreSlimManager(NormalSemaphoreSlimModel);
            MaxRequestsPerWindow = maxRequestsPerWindow;
            CurrentMaxRequestsPerWindow = maxRequestsPerWindow;
            MaxLimitRate = maxLimitRate;

            Watchdog = watchdogModel;
            CircuitBreaker = circuitBreakerModel;
            RateLimitBuffer = new DateTime [ MaxRequestsPerWindow ];

            ServHealthMetrics = new ServiceHealthMetrics();
            _needToStartWatchDog = needToStartWatchDog;
            if(_needToStartWatchDog)
            {
                StartWatchdog();
            }
        }

        /// <summary>
        /// Watch for the system is hanging or not.
        /// </summary>

        private void StartWatchdog()
        {
            // 1. 使用 Double-Check Locking 確保全生命週期只有一個背景 Task
            if(_isWatchdogRunning)
            {
                return;
            }

            lock(_watchdogLock)
            {
                if(_isWatchdogRunning)
                {
                    return;
                }
                _isWatchdogRunning = true;

                // 2. 使用 Task.Run 抽離，並務必傳入 Token
                Task.Run(async () =>
                {
                    var token = Watchdog.cancellationTokenSource.Token;
                    try
                    {
                        _logger.LogInformation("Watchdog started.");

                        while(!token.IsCancellationRequested)
                        {
                            // 3. 傳入 token 到 Delay，確保 Dispose 時能立刻喚醒並結束 Task
                            await Task.Delay(Watchdog.PollingTime , token);

                            var idleTime = DateTime.UtcNow - Watchdog.LastActivityTime;
                            if(idleTime > Watchdog.Timeout)
                            {
                                _logger.LogWarning("Watchdog detected timeout, cleaning up resources...");
                                // 執行清理邏輯，例如重置 Semaphore 或觸發警告
                            }
                        }
                    }
                    catch(OperationCanceledException)
                    {
                        // 正常的關閉流程，不視為錯誤
                        _logger.LogInformation("Watchdog stopped via cancellation.");
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex , "Watchdog encountered an error.");
                    }
                    finally
                    {
                        _isWatchdogRunning = false;
                    }
                } , Watchdog.cancellationTokenSource.Token);
            }
        }

        /// <summary>
        /// Handle the system hanging.
        /// </summary>
        private void HandleSystemHang()
        {
            _logger.LogCritical("DETECTED SYSTEM HANG!!! Ready to perform GC");

            // Level 1: 清空優先權隊列，讓卡住的 Task 收到取消異常
            lock(_queueLock)
            {
                while(_waitingQueue.Count > 0)
                {
                    if(_waitingQueue.TryDequeue(out var waiter , out _))
                    {
                        waiter.Tcs.TrySetCanceled();
                    }
                }
            }

            // Level 2: 強制中斷 RateLimit 的 Delay
            ForceEmergencyStop();

            // Level 3: 給予短暫緩衝，觀察 Semaphore 是否釋放
            Task.Delay(2000).ContinueWith(_ =>
            {
                if(GlobalConcurrencySemaphore.CurrentCount == 0)
                {
                    _logger.LogCritical("Level 1 Recovery Failed. Level 2: Process Restart.");
                    ProcessRecovery();
                }
                else
                {
                    _logger.LogInformation("Level 1 Recovery Successful. System resumed.");
                }
            });
        }

        /// <summary>
        /// Force to terminate the tasks.
        /// </summary>
        public void ForceEmergencyStop()
        {
            // 這會強制所有正在 ApplyRateLimitAsync 裡 Task.Delay 的任務立即中斷
            // 釋放 OS 級別的等待狀態
            _globalInternalStop.Cancel();
            _globalInternalStop = new CancellationTokenSource();
        }

        /// <summary>
        /// Kill the tasks.
        /// </summary>
        private void ProcessRecovery()
        {
            _logger.LogCritical("SYSTEM ERROR!!! The app will restart to recovery the process...");

            // 記錄最後的狀態...
            RecordFinalState();

            Task.Delay(500).Wait();

            // 強制退出當前程序，OS 會回收所有 Socket、Thread 與 File Handles
            // ExitCode 非 0 通常會觸發外部的服務守護進程（如 Docker 或 Windows Service）重新啟動此 App
            Environment.Exit(1);
        }

        /// <summary>
        /// Log the final state.
        /// </summary>
        private void RecordFinalState()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== SEMAPHORE SLIM SERVICE FINAL STATE REPORT ===");
            sb.AppendLine($"Report Time (UTC): {DateTime.UtcNow}");
            sb.AppendLine($"Last Activity Time (UTC): {Watchdog.LastActivityTime}");
            sb.AppendLine($"Time Since Last Activity: {DateTime.UtcNow - Watchdog.LastActivityTime}");

            // 記錄 Semaphore 剩餘量
            sb.AppendLine($"Global Concurrency Count: {GlobalConcurrencySemaphore.CurrentCount}");
            sb.AppendLine($"Normal Task Semaphore Count: {NormalTaskSemaphore.CurrentCount}");

            // 記錄 Rate Limit 隊列狀況
            lock(_historyLock)
            {
                sb.AppendLine($"Request History Count: {RateLimitBuffer.Length} / {MaxRequestsPerWindow}");
                if(RateLimitBuffer.Length > 0)
                {
                    sb.AppendLine($"Oldest Request in Window: {RateLimitBuffer[0]}");
                }
            }

            _logger.LogCritical(sb.ToString());

            // 如果擔心 Log 系統本身也卡住，可以直接寫入一個緊急本地文件
            try
            {
                string dumpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory , $"Dump_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(dumpPath , sb.ToString());
            }
            catch { /* 避免在最後一刻因為 IO 錯誤崩潰 */ }
        }

        /// <summary>
        /// Report the failure of tasks to log.
        /// </summary>
        public void ReportFailure()
        {
            IncreaseFailrueCount();
            CheckToOpenCircuitBreaker();
        }
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken , Task<T>> action ,
            TimeSpan timeout = default ,
            CancellationToken ct = default
        )
        {
            // 自動處理 Lock 的獲取與釋放
            using var releaser = await LockWithTimeoutAsync(ct , timeout);
            try
            {
                return await action(ct);
            }
            catch(Exception)
            {
                ReportFailure(); // 自動整合熔斷機制
                throw;
            }
        }

        /// <summary>
        /// Lock the <see cref="SemaphoreSlimService.GlobalConcurrencySemaphore"/> instance using circuit breaker technique.
        /// </summary>
        /// <param name="ct"><inheritdoc cref="LockWithTimeoutAsync(CancellationToken,TimeSpan, bool)" path="/param[@name='ct']"/></param>
        /// <param name="timeout"><inheritdoc cref="LockWithTimeoutAsync(CancellationToken,TimeSpan, bool)" path="/param[@name='timeout']"/></param>
        /// <param name="isEmergency"<inheritdoc cref="LockWithTimeoutAsync(CancellationToken,TimeSpan, bool)" path="/param[@name='isEmergency']"/>></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<IDisposable> LockWithCircuitBreakerAsync(
            CancellationToken ct = default ,
            TimeSpan timeout = default ,
            bool isEmergency = false
        )
        {
            return LockWithCircuitBreakerValueAsync(ct ,timeout, isEmergency).AsTask();
        }
        public async ValueTask<IDisposable> LockWithCircuitBreakerValueAsync(
            CancellationToken ct = default ,
            TimeSpan timeout = default ,
            bool isEmergency = false
        )
        {
            if(DateTime.UtcNow < CircuitBreaker.OpenUntil && !isEmergency)
            {
                throw new InvalidOperationException("Circuit Breaker is open. API is currently unstable.");
            }

            var releaser = await LockWithTimeoutAsync(
                ct:ct,
                timeout:timeout,
                isEmergency:isEmergency
            );
            // 在業務邏輯中，如果失敗則調用 ReportFailure()
            return releaser;
        }

        /// <summary>
        /// lock the <see cref="SemaphoreSlimService.GlobalConcurrencySemaphore"/> instance
        /// (<see cref="SemaphoreSlim"/>  type) until the task on the <see cref="SemaphoreSlimService.GlobalConcurrencySemaphore"/> has been completed or cancelled.
        /// </summary>
        /// <param name="ct">cancellation token</param>
        /// <param name="isEmergency">Is the task emergency so that it can skip the rate limit check</param>
        /// <returns></returns>
        public async Task<IDisposable> ExecuteWithLockAsync(
            CancellationToken ct = default ,
            bool isEmergency = false
        )
        {
            var valueTask = LockValueAsync(ct , isEmergency);
            return TaskService.ToTaskQuickly(valueTask);
        }
        public ValueTask<IDisposable> LockValueAsync(
            CancellationToken ct = default ,
            bool isEmergency = false
        )
        {
            return InternalLockValueAsync(ct , isEmergency);
        }

        /// <summary>
        /// Lock the <see cref="PrioritySemaphore"> (<see cref="Semaphore"/> instance) by priority (<see cref="ThreadLevelLockingUtilityServices.Models.TaskPriority"/>
        /// </summary>
        /// <param name="priority">priority</param>
        /// <param name="ct"><inheritdoc cref="ExecuteWithLockAsync(CancellationToken,bool)" path="/param[@name='ct']"/></param>
        /// <param name="isEmergency"><inheritdoc cref="ExecuteWithLockAsync(CancellationToken,bool)" path="/param[@name='isEmergency']"/></param>
        /// <returns></returns>

        public Task<IDisposable> LockWithPriorityAsync(
            TaskPriority priority ,
            CancellationToken ct = default ,
            bool isEmergency = false
        )
        {
            return LockWithPriorityValueAsync(priority , ct , isEmergency).AsTask();
        }
        public async ValueTask<IDisposable> LockWithPriorityValueAsync(
            TaskPriority priority ,
            CancellationToken ct = default ,
            bool isEmergency = false
        )
        {
            isEmergency = (priority == TaskPriority.High);
            return await LockValueAsync(CancellationToken.None , isEmergency);
        }

        /// <summary>
        /// Enhanced version of <see cref="LockWithTimeoutAsync(CancellationToken, TimeSpan, bool)"/>.
        /// Add monitor to monitor it is closing, has been closed, timeout occur, or cancellation token is triggered or not.
        /// If it is closing or has been closed, then throw <see cref="ObjectDisposedException"/>,
        /// If timeout occured or detects cancellation token is triggered, then log the message into logger and then throw <see cref="TimeoutException"/>.
        /// </summary>
        /// <param name="ct"><inheritdoc cref="ExecuteWithLockAsync(CancellationToken, bool)" path="/param[@name='ct']"/></param>
        /// <param name="timeout">timeout</param>
        /// <param name="isEmergency"><inheritdoc cref="ExecuteWithLockAsync(CancellationToken, bool)" path="/param[@name='isEmergency']"/></param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException">When it is closing or has been closed</exception>
        /// <exception cref="TimeoutException">When timeout occured or detects cancellation token is triggered</exception>
        public Task<IDisposable> LockWithTimeoutByMonitorAsync(
            CancellationToken ct = default ,
            TimeSpan timeout = default ,
            bool isEmergency = false
        )
        {
            var valueTask = LockWithTimeoutByMonitorValueAsync(ct , timeout , isEmergency);
            return TaskService.ToTaskQuickly(valueTask);
        }
        public async ValueTask<IDisposable> LockWithTimeoutByMonitorValueAsync(
            CancellationToken ct = default ,
            TimeSpan timeout = default ,
            bool isEmergency = false
        )
        {
            if(Volatile.Read(ref _isShuttingDown))
            {
                throw new ObjectDisposedException(nameof(SemaphoreSlimService<TModel,TException>));
            }

            bool hasTimeout = timeout != Timeout.InfiniteTimeSpan && timeout != default;

            // CTS 連結優化路徑
            if(!hasTimeout && !ct.CanBeCanceled)
            {
                return await InternalLockValueAsync(ct , isEmergency);
            }

            using var timeoutCts = hasTimeout ? new CancellationTokenSource(timeout) : null;
            using var linkedCts = timeoutCts != null ? CancellationTokenSource.CreateLinkedTokenSource(ct , timeoutCts.Token) : null;
            CancellationToken effectiveToken = linkedCts?.Token ?? ct;

            try
            {
                return await InternalLockValueAsync(effectiveToken , isEmergency);
            }
            catch(OperationCanceledException) when(timeoutCts?.IsCancellationRequested ?? false)
            {
                ReportFailure(); // 觸發熔斷計數
                throw new TimeoutException($"鎖獲取超時。全域排隊中：{GlobalConcurrencySemaphoreManager.RealTimeWaitingCount}");
            }
        }

        /// <summary>
        /// lock the <see cref="SemaphoreSlimService.GlobalConcurrencySemaphore"/> instance
        /// (<see cref="SemaphoreSlim"/>  type) until the task on the <see cref="SemaphoreSlimService.GlobalConcurrencySemaphore"/> has been completed or been cancelled, or timeout occurs.
        /// </summary>
        /// <param name="ct"><inheritdoc cref="ExecuteWithLockAsync(CancellationToken, bool)" path="/param[@name='ct']"/></param>
        /// <param name="timeout">timeout</param>
        /// <param name="isEmergency"><inheritdoc cref="ExecuteWithLockAsync(CancellationToken, bool)" path="/param[@name='isEmergency']"/></param>
        /// <returns></returns>
        /// <exception cref="TimeoutException"></exception>
        public Task<IDisposable> LockWithTimeoutAsync(
            CancellationToken ct = default,
            TimeSpan timeout = default,
            bool isEmergency = false
        )
        {
            return LockWithTimeoutValueAsync(ct,timeout,isEmergency).AsTask();
        }
        public async ValueTask<IDisposable> LockWithTimeoutValueAsync(
            CancellationToken ct = default,
            TimeSpan timeout = default,
            bool isEmergency = false
        )
        {
            if(timeout.Equals(default))
            {
                // 預設Timeout為5分鐘
                timeout = TimeSpan.FromMinutes(5);
            }

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct , timeoutCts.Token);

            try
            {
                return await LockValueAsync(isEmergency: isEmergency, ct:linkedCts.Token);
            }
            catch(OperationCanceledException)
            {
                // 判斷是因為超時還是因為使用者手動取消
                if(timeoutCts.IsCancellationRequested)
                {
                    throw new TimeoutException($"Timeout occurred!!! Can't finish the operation in {timeout.Seconds} seconds.");
                }
                throw;
            }
        }
    

        public Task ApplyRateLimitAsync(CancellationToken ct)
        {
            return ApplyRateLimitValueAsync(ct).AsTask();
        }
        public async ValueTask ApplyRateLimitValueAsync(CancellationToken ct)
        {
            while(!ct.IsCancellationRequested)
            {
                DateTime now = DateTime.UtcNow;
                TimeSpan sleepTime = TimeSpan.Zero;

                lock(_historyLock)
                {
                    while(_bufferCount > 0 && now - RateLimitBuffer [ _bufferHead ] > MaxLimitRate)
                    {
                        _bufferHead = (_bufferHead + 1) % MaxRequestsPerWindow;
                        _bufferCount--;
                    }

                    if(_bufferCount < CurrentMaxRequestsPerWindow)
                    {
                        RateLimitBuffer [ _bufferTail ] = now;
                        _bufferTail = (_bufferTail + 1) % MaxRequestsPerWindow;
                        _bufferCount++;
                        return;
                    }

                    // 計算等待時間：直接取 Head 的時間點
                    sleepTime = MaxLimitRate - (now - RateLimitBuffer [ _bufferHead ]);

                    LogAvailableToken(_logger , sleepTime.TotalMilliseconds);
                }

                // 在 ApplyRateLimitAsync 中
                if(sleepTime > TimeSpan.FromMilliseconds(5))
                {
                    await Task.Delay(sleepTime , ct);
                }
                else if(sleepTime > TimeSpan.Zero)
                {
                    // 極短時間不使用 Delay，改用 Yield 讓出 CPU 即可，避免 15ms 的精確度陷阱
                    await Task.Yield();
                }
            }
        }

        /// <summary>
        /// Get the healthy status of current executed task.
        /// </summary>
        /// <returns></returns>
        public ServiceHealthMetrics GetHealthMetrics()
        {
            lock(_historyLock)
            {
                return new ServiceHealthMetrics
                {
                    ActiveGlobalTasks = GlobalConcurrencySemaphoreManager.GetCurrentActiveCount() ,
                    ActiveNormalTasks = Math.Max(1 , GlobalSemaphoreSlimModel.InitialCount - 1) - NormalTaskSemaphore.CurrentCount ,
                    RequestQueueCount = RateLimitBuffer.Length ,
                    RateLimitUsagePercentage = (RateLimitBuffer.Length / (double)CurrentMaxRequestsPerWindow) * 100 ,
                    IsCircuitBreakerOpen = DateTime.UtcNow < CircuitBreaker.OpenUntil ,
                    LastActivityTime = Watchdog.LastActivityTime,
                    GlobalWaitingTasks = GlobalConcurrencySemaphoreManager.RealTimeWaitingCount,
                    NormalWaitingTasks = NormalTaskSemaphoreManager.RealTimeWaitingCount,
                    IsShuttingDown = Volatile.Read(ref _isShuttingDown) 
                };
            }
        }

        private Task TryToExecuteAsync(
            SemaphoreSlim semaphore,
            CancellationToken ct,
            string semaphoreName
        )
        {
            var valueTask = TryToExecuteValueAsync(semaphore , ct , semaphoreName);
            return valueTask.AsTask();
        }
        private async ValueTask TryToExecuteValueAsync(
            SemaphoreSlim semaphore,
            CancellationToken ct,
            string semaphoreName
        )
        {
            if(semaphore.CurrentCount > 0)
            {
                await semaphore.WaitAsync(ct);
                return;
            }

            throw new OperationCanceledException($"The semaphore name `{semaphoreName}` isn't available now.");
        }

        private async ValueTask<IDisposable> InternalLockValueAsync(CancellationToken ct , bool isEmergency)
        {
            // 1. 基礎檢查：優雅關閉與熔斷器
            if(Volatile.Read(ref _isShuttingDown))
            {
                throw new ObjectDisposedException(nameof(SemaphoreSlimService<TModel,TException>));
            }
            long resumeTime = Volatile.Read(ref CircuitBreaker.OpenUntilTicksField);
            if(resumeTime > 0 && DateTime.UtcNow.Ticks < resumeTime)
            {
                throw new InvalidOperationException($"{Constants.Requests.RequestsDenied}{Constants.CircuitBreaker.IsOpened}");
            }

            // 2. 依照緊急程度進行計數 (關鍵優化：手動管理 Waiter)
            GlobalConcurrencySemaphoreManager.IncrementWaiter(); // 全域計數 (緊急 + 非緊急)

            if(!isEmergency)
            {
                NormalTaskSemaphoreManager.IncrementWaiter(); // 僅非緊急計數
            }

            try
            {
                // 3. 獲取鎖：非緊急任務必須先過 Normal 鎖，再過 Global 鎖
                if(!isEmergency)
                {
                    await NormalTaskSemaphore.WaitAsync(ct);
                }

                await GlobalConcurrencySemaphore.WaitAsync(ct);

                // 試圖重設(連續)失敗次數
                TryToResetFailureCount();

                // 4. 回傳 Releaser
                return new Releaser(
                    GlobalConcurrencySemaphoreManager ,
                    isEmergency ? null : NormalTaskSemaphoreManager ,
                    () => { Watchdog.LastActivityTime = DateTime.UtcNow; }
                );
            }
            catch
            {
                // 發生異常 (如取消或超時) 也要記得把計數扣回來
                GlobalConcurrencySemaphoreManager.DecrementWaiter();
                if(!isEmergency) NormalTaskSemaphoreManager.DecrementWaiter();
                throw;
            }
        }

        private void TryToResetFailureCount()
        {
            if(CircuitBreaker.ContinuousFailureCount > 0)
            {
                Interlocked.Exchange(ref CircuitBreaker.ContinuousFailureCount , 0);
            }
        }
        /// <summary>
        /// Update the rate limit.
        /// </summary>
        /// <param name="newMaxRequests"></param>
        public void UpdateRateLimit(int newMaxRequests)
        {
            lock(_rateLimitAdjustmentLock)
            {
                LogRateLimitUpdates(_logger , CurrentMaxRequestsPerWindow , newMaxRequests);
                CurrentMaxRequestsPerWindow = newMaxRequests;
            }
        }

        /// <summary>
        /// Attempt to categorize the lock by priority
        /// </summary>
        /// <returns></returns>
        private async Task ProcessQueueAsync()
        {
            PriorityWaiter<TModel>? nextWaiter = null;

            lock(_queueLock)
            {
                if(_waitingQueue.Count > 0)
                {
                    // 嘗試取得下一個最高優先權的任務
                    nextWaiter = _waitingQueue.Peek();
                }
            }

            if(nextWaiter == null)
            {
                return;
            }

            try
            {
                // 這裡才真正去競爭 Semaphore
                // 這樣可以確保只有當 Semaphore 有位置時，才從隊列中真正 Dequeue
                var releaser = await ExecuteWithLockAsync(nextWaiter.CancellationToken , false);

                lock(_queueLock)
                {
                    if(_waitingQueue.TryDequeue(out var dequeuedWaiter , out _))
                    {
                        // 將鎖交給等待中的 Task
                        dequeuedWaiter.Tcs.SetResult(releaser);
                    }
                }
            }
            catch(OperationCanceledException)
            {
                lock(_queueLock) { _waitingQueue.TryDequeue(out _ , out _); }
                nextWaiter.Tcs.SetCanceled();
            }
            catch(Exception ex)
            {
                nextWaiter.Tcs.SetException(ex);
            }
        }

        /// <summary>
        /// Wait to close until current tasks are completed.
        /// </summary>
        /// <returns>
        /// + returns true when it closed after (and due to) all tasked are complete.
        ///
        /// + return false when it closed after (and due to) timeout or the internal cancellation token is invoked.
        /// </returns>
        public Task<bool> ShutdownAsync(TimeSpan timeout)
        {
            return ShutdownValueAsync(timeout).AsTask();
        }
        public async ValueTask<bool> ShutdownValueAsync(TimeSpan timeout)
        {
            _logger.LogInformation("Initiating graceful shutdown...");
            Volatile.Write(ref _isShuttingDown, true); // 設為 True 後，LockAsync 應拒絕新請求

            var startTime = DateTime.UtcNow;

            // 循環檢查：當 CurrentCount 回到 InitialCount 代表所有任務已 Release
            while(DateTime.UtcNow - startTime < timeout)
            {
                bool isAvailable = GlobalConcurrencySemaphoreManager.IsAvailable;

                if(isAvailable)
                {
                    _logger.LogInformation("All tasks completed. Shutting down now.");
                    Dispose();
                    return true;
                }

                await Task.Delay(100 , _globalInternalStop.Token);
            }

            _logger.LogWarning("Shutdown timeout reached. Forcing disposal.");
            Dispose();
            return false;
        }

        private void IncreaseFailrueCount()
        {
            Interlocked.Increment(ref CircuitBreaker.ContinuousFailureCount);
        }
        private void CheckToOpenCircuitBreaker()
        {
            if(CircuitBreaker.ContinuousFailureCount >= CircuitBreaker.MaxAllowedFailureCount)
            {
                CircuitBreaker.OpenUntil = DateTime.UtcNow.Add(CircuitBreaker.CoolDown);
                _logger.LogCritical("Circuit Breaker OPENED due to multiple failures.");
            }
        }
        /// <summary>
        /// inner class to release the <see cref="SemaphoreSlim"/> instance
        /// </summary>
        private class Releaser : IDisposable
        {
            private readonly ISemaphoreSlimManager _globalSemaphoreSlimManager;
            private readonly ISemaphoreSlimManager? _normalSemaphoreSlimManager; // allow to be null for emergency tasks that skip normal semaphore
            private int _isReleased = 0; // 防止重複 Release
            private readonly Action _onDispose;

            public Releaser(
                ISemaphoreSlimManager globalSemaphoreSlimBuilder ,
                ISemaphoreSlimManager? normalSemaphoreSlimBuilder , 
                Action onDispose
            )
            {
                // defensive programming
                ArgumentNullException.ThrowIfNull(globalSemaphoreSlimBuilder);
                ArgumentNullException.ThrowIfNull(onDispose);
    
                _globalSemaphoreSlimManager = globalSemaphoreSlimBuilder;
                _normalSemaphoreSlimManager = normalSemaphoreSlimBuilder;
                _onDispose = onDispose;
            }

            public void TryToRelease()
            {
                if(Interlocked.Exchange(ref _isReleased , 1) == 0)
                {
                    // (根據_isReleased flag來判斷)沒被正在Release
                    _globalSemaphoreSlimManager.TryToRelease(); // 釋放全域SemaphoreSlim // nullable safety check not needed since it's required in constructor
                    _normalSemaphoreSlimManager?.TryToRelease(); // 釋放一般SemaphoreSlim (如果有的話)

                    _onDispose?.Invoke(); // 更新最後活動時間
                }
            }

            /// <summary>
            /// 實作IDisposeable的Dispose方法
            /// </summary>
            public void Dispose()
            {
                TryToRelease();
            }
        }

        /// <summary>
        /// Dispose the <see cref="SemaphoreSlimService.GlobalConcurrencySemaphore"/> instance (<see cref="SemaphoreSlim"/>  type).
        /// </summary>
        public void Dispose()
        {
            Volatile.Write(ref _isShuttingDown, true);

            // 安全地取消並釋放 CancellationTokenSource，防止多執行緒下的 ObjectDisposedException。
            Watchdog.cancellationTokenSource.SafeCancelAndDispose();

            GlobalConcurrencySemaphore.Dispose();
            NormalTaskSemaphore.Dispose();
        }
    }
}
