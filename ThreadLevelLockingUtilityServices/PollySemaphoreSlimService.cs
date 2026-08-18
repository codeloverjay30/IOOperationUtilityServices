using CommonModels;
using ExceptionFactories;
using IOOperation.BaseUtilityServices;
using LoggerFactoryUtilityServices;
using Microsoft.Azure.Pipelines.WebApi;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.RateLimiting;
using Polly.Retry;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.RateLimiting;
using Tasks.Model;
using TaskUtilityServices;
using ThreadLevelLockingUtilityServices.Consts;
using ThreadLevelLockingUtilityServices.Models;
using static TaskUtilityServices.TaskExtensions;
using TaskPropriority = Tasks.Model.TaskPriority;

namespace ThreadLevelLockingUtilityServices
{ 
    /// <summary>
    /// Utility service about <see cref="SemaphoreSlim"/> class that integrates with <see cref="Polly"/> library.
    /// </summary>
    /// <typeparam name="TModel"><see="IOOperation.BaseUtilityServices.OperationModel"/></typeparam>
    /// <typeparam name="TResultStatus"><see="CommonModels.StatusJsonModel"/></typeparam>
    /// <typeparam name="TException"><see="global::System.IO.Exception"></typeparam>
    public partial class PollySemaphoreSlimService<TModel, TResultStatus, TException>:
        ThreadLevelLockingBaseUtilityService,
        IPollySemaphoreSlimService<TModel,TResultStatus, TException>,
        IDisposable
        where TModel: OperationModel, new()
        where TResultStatus: StatusJsonModel,new()
        where TException: Exception, new()
    {
        [LoggerMessage(Level = LogLevel.Warning , Message = "Please wait {WaitTime} ms The next available token")]
        static partial void LogWarningForAvailableToken(ILogger logger,double waitTime);

        [LoggerMessage(Level = LogLevel.Warning , Message = "Polly detects timeout occurred for key: {Key}")]
        static partial void LogWarningDueToPollyTimeout(ILogger logger,string key);

        [LoggerMessage(Level = LogLevel.Warning , Message = "Cancellation occurred for key: {Key}")]
        static partial void LogWarningDueToCancellation(ILogger logger,string key);

        [LoggerMessage(Level = LogLevel.Warning , Message = "Timeout occurred (but not detected by Polly) for key: {Key}")]
        static partial void LogWarningDueToTimeout(ILogger logger,string key);

        [LoggerMessage(Level = LogLevel.Warning , Message = "Circuit breaker is opened for key: {Key}")]
        static partial void LogWarningDueToCircuitBreakerIsOpened(ILogger logger,string key);
        [LoggerMessage(Level = LogLevel.Warning , Message = "Unknown exception occurred for key: {Key}")]
        static partial void LogWarningDueToUnknownException(ILogger logger,string key);

        [LoggerMessage(Level = LogLevel.Warning , Message = "The task: {taskName} is not executed")]
        static partial void LogWarningToNotifyUserThatTaskIsNotExecuted(ILogger logger,string taskName);

        [LoggerMessage(Level = LogLevel.Warning , Message = "rate limit updates：{oldRateLimitUpdates} -> {newRateLimeUpdates}")]
        static partial void LogRateLimitUpdates(ILogger logger,int oldRateLimitUpdates,int newRateLimeUpdates);

        private static readonly ResiliencePropertyKey<OperationModel> OperationKey = new("OperationKey");

        private readonly ConcurrentDictionary<string , ISemaphoreSlimManager> _keyBasedManagers = new();
        public ConcurrentDictionary<string , ISemaphoreSlimManager> KeyBasedManagers => _keyBasedManagers;

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
        public WatchdogExecutionSettings WatchdogExecutionSettings { get; set; }

        /// <summary>
        /// The configuration used in <see cref="ServiceHealthMetrics"/> class.
        /// </summary>
        private PollyServiceHealthMetrics _servHealthMetrics { get; set; }
        public PollyServiceHealthMetrics ServHealthMetrics => _servHealthMetrics;

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
        private PollyCircuitBreakerExecutionSettings _circuitBreakerSettings { get; init; }
        public PollyCircuitBreakerExecutionSettings CircuitBreakerSettings => _circuitBreakerSettings;

        /// <summary>
        /// The maximum allowed requests per window at same time
        /// </summary>
        public int MaxRequestsPerWindow { get; set; }

        /// <summary>
        /// use waiter that supports the handle the tasks by priority. 
        /// </summary>
        private readonly PriorityQueue<PriorityWaiter<TModel>, int> _waitingQueue = new();

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
        /// use volatile modifier so that
        /// it tells the compiler and the CPU:
        /// "Do not cache this value in a register, and do not reorder instructions around it."
        /// It prevents the CPU from optimizing the read/write by using a local cache.
        /// </remarks>
        private volatile bool _isShuttingDown = false;

        private ResiliencePipeline<IDisposable> _resiliencePipeline;

        private CircuitBreakerStateProvider _circuitBreakerStateProvider;

        private double _currentUsagePercentage = 0.0;
        private int _currentRequestCount = 0;

        private readonly IPollyKeyBasedManagerUtilityService _pollyUtilityService;
        private ILogger _logger => LoggerFactoryService.Logger;
        public ILogger Logger => _logger;
 
        private CircuitBreakerManualControl _manualControl;

        private readonly PollyStrategyConfig<IDisposable> _strategyConfig;
        public PollySemaphoreSlimService(
            ILoggerFactoryBaseUtilityService loggerFactoryService,
            ITaskUtilityService? taskUtilityService,
            IPollyKeyBasedManagerUtilityService pollyUtilityService,
            SemaphoreSlimModel globalSemaphoreSlimModel,
            int maxRequestsPerWindow,
            TimeSpan maxLimitRate,
            WatchdogExecutionSettings watchdogExecutingSettings,
            PollyCircuitBreakerExecutionSettings circuitBreakerExecutionSettings,
            PollyStrategyConfig<IDisposable> strategyConfig
        ) : base(loggerFactoryService)
        {
            var normalMax = Math.Max(1, globalSemaphoreSlimModel.InitialCount - 1);

            LoggerFactoryService = loggerFactoryService;
            TaskService = taskUtilityService ?? new TaskUtilityService(); // 預設使用TaskUtilityService這個Service
            _pollyUtilityService = pollyUtilityService ?? new PollyKeyBasedManagerUtilityService();
            GlobalSemaphoreSlimModel = globalSemaphoreSlimModel;
            NormalSemaphoreSlimModel = new SemaphoreSlimModel { InitialCount = normalMax, MaxCount = normalMax };

            GlobalConcurrencySemaphoreManager = new SemaphoreSlimManager(GlobalSemaphoreSlimModel);
            NormalTaskSemaphoreManager = new SemaphoreSlimManager(NormalSemaphoreSlimModel);
            MaxRequestsPerWindow = maxRequestsPerWindow;
            WatchdogExecutionSettings = watchdogExecutingSettings;
            _circuitBreakerSettings = circuitBreakerExecutionSettings;
            _circuitBreakerStateProvider = new CircuitBreakerStateProvider();
            _servHealthMetrics = new PollyServiceHealthMetrics();
            _strategyConfig = strategyConfig;
            _manualControl = new CircuitBreakerManualControl();

            InternalConfigureAndBuild();

            if (WatchdogExecutionSettings.IsEnabled)
            {
                StartWatchdog();
            }
        }

        /// <summary>
        /// Build the <see cref="global::Polly.ResiliencePipeline{IDisposable}"/>
        /// </summary>
        /// <remarks>
        /// Potential bug fix: 
        /// Ensure that the _resiliencePipeline is not null after this method is called (through <seealso cref="MemberNotNullAttribute"/> Attribute).
        /// </remarks>
        [MemberNotNull(nameof(_resiliencePipeline))]
        private void InternalConfigureAndBuild()
        {
            _resiliencePipeline = new ResiliencePipelineBuilder<IDisposable>()
                .AddStandardRetryStrategy<IDisposable>(
                    _circuitBreakerSettings ,
                    _strategyConfig ,
                    (waitTime) =>
                    {
                        LogWarningForAvailableToken(_logger , waitTime);
                    }
                )
                .AddStandardFallbackStrategy<IDisposable>(
                    _circuitBreakerSettings ,
                    _strategyConfig ,
                    (taskName , ex , ct) =>
                    {
                        switch(ex)
                        {
                            case Polly.Timeout.TimeoutRejectedException:
                                LogWarningDueToPollyTimeout(_logger , taskName);
                                break;
                            case OperationCanceledException:
                                if(ct?.IsCancellationRequested ?? false)
                                {
                                    LogWarningDueToCancellation(_logger , taskName);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                )
                .AddStandardCircuitBreakerStrategy<IDisposable>(
                    _circuitBreakerSettings ,
                    _strategyConfig
                )
                .AddStandardRateLimiterStrategy<IDisposable>(
                    _circuitBreakerSettings,
                    _strategyConfig
                )
            .Build();
        }

        /// <summary>
        /// Open the circuit breaker.
        /// </summary>
        /// <returns></returns>
        public async Task OpenCircuitBreaker()
        {
            await _manualControl.IsolateAsync();
        }

        /// <summary>
        /// Close the circuit breaker.
        /// </summary>
        /// <returns></returns>
        public async Task CloseCircuitBreaker()
        {
            await _manualControl.CloseAsync();
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
                    var token = WatchdogExecutionSettings.cancellationTokenSource.Token;
                    try
                    {
                        _logger.LogInformation("Watchdog started.");

                        while(!token.IsCancellationRequested)
                        {
                            // 3. 傳入 token 到 Delay，確保 Dispose 時能立刻喚醒並結束 Task
                            await Task.Delay(WatchdogExecutionSettings.PollingTime , token);

                            var idleTime = DateTime.UtcNow - WatchdogExecutionSettings.LastActivityTime;
                            if(idleTime > WatchdogExecutionSettings.Timeout)
                            {
                                _logger.LogWarning("Watchdog detected timeout, cleaning up resources...");
                                // 執行清理邏輯，例如重置 Semaphore 或觸發警告
                                await HandleSystemHang(); // 嘗試清空 _waitingQueue
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
                } , WatchdogExecutionSettings.cancellationTokenSource.Token);
            }
        }

        /// <summary>
        /// Handle the system hanging.
        /// </summary>
        private async Task HandleSystemHang()
        {
            _logger.LogCritical("DETECTED SYSTEM HANG!!! Ready to perform GC");

            if(_circuitBreakerSettings.IsEnabled)
            {
                try
                {
                    await OpenCircuitBreaker();
                    _logger.LogWarning("Circuit Breaker set to ISOLATED to block new traffic.");
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex , "Failed to isolate the circuit breaker.");
                }
            }

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
            await Task.Delay(2000).ContinueWith(async _ =>
            {
                if(GlobalConcurrencySemaphore.CurrentCount == 0)
                {
                    _logger.LogCritical("Level 1 Recovery Failed. Level 2: Process Restart.");
                    await ProcessRecovery();
                }
                else
                {
                    _logger.LogInformation("Level 1 Recovery Successful. System resumed.");
                }
            }).ConfigureAwait(false);
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
        private async Task ProcessRecovery()
        {
            _logger.LogCritical("SYSTEM ERROR!!! The app will restart to recovery the process...");

            // 記錄最後的狀態...
            RecordFinalState();

            await Task.Delay(500);

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
            sb.AppendLine($"Last Activity Time (UTC): {WatchdogExecutionSettings.LastActivityTime}");
            sb.AppendLine($"Time Since Last Activity: {DateTime.UtcNow - WatchdogExecutionSettings.LastActivityTime}");

            // 記錄 Semaphore 剩餘量
            sb.AppendLine($"Global Concurrency Count: {GlobalConcurrencySemaphore.CurrentCount}");
            sb.AppendLine($"Normal Task Semaphore Count: {NormalTaskSemaphore.CurrentCount}");

            // 記錄 Rate Limit 隊列狀況
            lock(_historyLock)
            {
                // potential bug fix : use the initialized MaxRequestsPerWindow property instead of the uninitialized one.
                sb.AppendLine($"Request History Count / Permit Limit: {_currentRequestCount} / {MaxRequestsPerWindow}"); 
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
        /// lock the <see cref="SemaphoreSlimService.GlobalConcurrencySemaphore"/> instance
        /// (<see cref="SemaphoreSlim"/>  type) until the task on the <see cref="SemaphoreSlimService.GlobalConcurrencySemaphore"/> has been completed or cancelled.
        /// </summary>
        /// <param name="ct">cancellation token</param>
        /// <param name="isEmergency">Is the task emergency so that it can skip the rate limit check</param>
        /// <returns></returns>
        public Task<(IDisposable,TResultStatus)> ExecuteWithLockAsync(
            TModel operation ,
            CancellationToken ct = default ,
            bool isEmergency = false
        )
        {
            var valueTask = ExecuteWithLockUsingPollyPollValueAsync(
                operation,
                ct ,
                isEmergency
            );
            return TaskService.ToTaskQuickly(valueTask);
        }

        public async ValueTask<(IDisposable,TResultStatus)> ExecuteWithLockUsingPollyPollValueAsync(
            TModel operation ,
            CancellationToken ct ,
            bool isEmergency
        )
        {
            TResultStatus statusJsonModel = new TResultStatus();
            // 1. 從池中抓一個 Context (效能較好)
            ResilienceContext context = ResilienceContextPool.Shared.Get(ct);

            // 2. 把你的 operation 放進信封
            context.Properties.Set(new ResiliencePropertyKey<TModel>(operation.Key), operation);

            try
            {
                var result = await _resiliencePipeline.ExecuteAsync<IDisposable>(async (ResilienceContext tempContext) =>
                {
                        // 這裡依然執行原本的邏輯
                        var result = await InternalLockUsingPollyPollAsync(
                            operation ,
                            operation.Priority ,
                            tempContext.CancellationToken
                        );
                        return result;
                } , context);

                statusJsonModel.IsSuccess = true;
                statusJsonModel.Result = "Executed successfully";
                statusJsonModel.ErrorMessage = string.Empty;
                return (result, statusJsonModel);
            }
            catch(OperationCanceledException ex) when(ct.IsCancellationRequested)
            {
                LogWarningDueToCancellation(_logger , operation.Key);
                statusJsonModel.IsSuccess = false;
                statusJsonModel.Result = "error";
                statusJsonModel.OverallErrorMessage = $"Cancellation occurred for key: {operation.Key}";
                statusJsonModel.ErrorMessage = ex.Message;
                statusJsonModel.DetailedErrorMessage = new ExceptionFactory(ex).Create();
                return (null, statusJsonModel);
            }
            catch(OperationCanceledException ex)
            {
                LogWarningDueToTimeout(_logger , operation.Key);
                statusJsonModel.IsSuccess = false;
                statusJsonModel.Result = "error";
                statusJsonModel.OverallErrorMessage = $"Circuit breaker is opened for key: {operation.Key}";
                statusJsonModel.ErrorMessage = ex.Message;
                statusJsonModel.DetailedErrorMessage = new ExceptionFactory(ex).Create();
                return (null, statusJsonModel);
            }
            catch(Polly.Timeout.TimeoutRejectedException ex)
            {
                LogWarningDueToPollyTimeout(_logger , operation.Key);
                statusJsonModel.IsSuccess = false;
                statusJsonModel.Result = "error";
                statusJsonModel.OverallErrorMessage = $"Polly detects timeout occurred for key: {operation.Key}";
                statusJsonModel.ErrorMessage = ex.Message;
                statusJsonModel.DetailedErrorMessage = new ExceptionFactory(ex).Create();
                return (null, statusJsonModel);        
            }
            catch(Exception ex)
            {
                LogWarningDueToUnknownException(_logger , operation.Key);
                statusJsonModel.IsSuccess = false;
                statusJsonModel.Result = "error";
                statusJsonModel.OverallErrorMessage = $"Polly detects timeout occurred for key: {operation.Key}";
                statusJsonModel.ErrorMessage = ex.Message;
                statusJsonModel.DetailedErrorMessage = new ExceptionFactory(ex).Create();
                return (null, statusJsonModel);
            }
            finally
            {
                // 3. 用完後歸還 Context 到池中
                ResilienceContextPool.Shared.Return(context);
            }
        }

        private async ValueTask<IDisposable> InternalLockUsingPollyPollAsync(
            TModel operation ,
            TaskPriority priority ,
            CancellationToken ct
        )
        {
            StatusJsonModel statusJsonModel = new StatusJsonModel();
            var manager = _keyBasedManagers.GetOrAdd(operation.Key , _ => new SemaphoreSlimManager(NormalSemaphoreSlimModel));

            // 優先嘗試 Fast Path：如果現在就有位子，直接拿走
            if(manager.SemaphoreSlimInstance.Wait(0))
            {            
               var releaser = new Releaser(
                    GlobalConcurrencySemaphoreManager ,
                    manager,
                    operation.Key,
                    _keyBasedManagers,
                    () =>
                    {
                        WatchdogExecutionSettings.LastActivityTime = DateTime.UtcNow;
                        if(manager != null)
                        {
                            manager.DecrementWaiter();
                            _keyBasedManagers.TryRemove(operation.Key , out manager);
                        }
                    });
                return releaser;
            }

            // 如果沒位置，才進入非同步等待 (這裡會產生 Task)
            manager.IncrementWaiter();
            try
            {
                // 這裡雖然是 Task，但因為外層是 ValueTask，
                // 只有在真正需要「等」的時候才會支付 Task 的分配成本
                await manager.SemaphoreSlimInstance.WaitAsync(ct).ConfigureAwait(false);

                var releaser = new Releaser(
                    GlobalConcurrencySemaphoreManager ,
                    manager ,
                    operation.Key,
                    _keyBasedManagers,
                    () =>
                    {
                        WatchdogExecutionSettings.LastActivityTime = DateTime.UtcNow;
                        if(manager != null)
                        {
                            manager.DecrementWaiter(); // 先減少 Waiter，確保在釋放 Semaphore 前不會被 Watchdog 誤判為閒置
                            // _keyBasedManagers.TryRemove(operation.Key , out manager);
                        }
                    });
                return releaser;
            }
            catch(Exception ex)
            {
                throw;
            }
            finally
            {
                manager.DecrementWaiter();
            }
        }

        private async ValueTask<TModel> InternalExecuteWithLockValueAsync(
            TModel operation ,
            CancellationToken ct ,
            bool isEmergency
        )
        {
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

                return operation;
            }
            catch(Polly.Timeout.TimeoutRejectedException)
            {
                // 這是 Polly 的 Timeout 策略觸發的超時
                LogWarningDueToPollyTimeout(_logger , operation.Key);
                // 發生異常 (如取消或超時) 也要記得把計數扣回來
                GlobalConcurrencySemaphoreManager.DecrementWaiter();
                if(!isEmergency) NormalTaskSemaphoreManager.DecrementWaiter();
                throw;
            }
            catch(OperationCanceledException ex) when(ct.IsCancellationRequested)
            {
                LogWarningDueToCancellation(_logger , operation.Key);
                // 發生異常 (如取消或超時) 也要記得把計數扣回來
                GlobalConcurrencySemaphoreManager.DecrementWaiter();
                if(!isEmergency) NormalTaskSemaphoreManager.DecrementWaiter();
                throw;
            }
            catch(OperationCanceledException ex)
            {
                LogWarningDueToTimeout(_logger , operation.Key);
                // 發生異常 (如取消或超時) 也要記得把計數扣回來
                GlobalConcurrencySemaphoreManager.DecrementWaiter();
                if(!isEmergency) NormalTaskSemaphoreManager.DecrementWaiter();
                throw;
            }
        }

        /// <summary>
        /// Lock the <see cref="PrioritySemaphore"> (<see cref="Semaphore"/> instance) by priority (<see cref="ThreadLevelLockingUtilityServices.Models.TaskPriority"/>
        /// </summary>
        /// <param name="priority">priority</param>
        /// <param name="ct"><inheritdoc cref="ExecuteWithLockAsync(CancellationToken,bool)" path="/param[@name='ct']"/></param>
        /// <param name="isEmergency"><inheritdoc cref="ExecuteWithLockAsync(CancellationToken,bool)" path="/param[@name='isEmergency']"/></param>
        /// <returns></returns>

        public Task<(IDisposable, TResultStatus)> LockWithPriorityAsync(
            TModel operation,
            TaskPriority priority ,
            CancellationToken ct = default ,
            bool isEmergency = false
        )
        {
            var valueTask = LockWithPriorityValueAsync(
                operation ,
                priority ,
                ct ,
                isEmergency
            );
            return TaskService.ToTaskQuickly(valueTask);
        }
        public async ValueTask<(IDisposable,TResultStatus)> LockWithPriorityValueAsync(
            TModel operation,
            TaskPriority priority ,
            CancellationToken ct = default ,
            bool isEmergency = false
        )
        {
            isEmergency = (priority is TaskPriority.High);
            return await ExecuteWithLockUsingPollyPollValueAsync(
                operation,
                CancellationToken.None ,
                isEmergency
            );
        }

        /// <summary>
        /// Get the healthy status of current executed task.
        /// </summary>
        /// <returns></returns>
        public PollyServiceHealthMetrics GetHealthMetrics(string key)
        {
            var currentState = _circuitBreakerStateProvider.CircuitState;
            bool isOpened = currentState == CircuitState.Open || currentState == CircuitState.Isolated;
            lock(_historyLock)
            {
                _pollyUtilityService.KeyBasedManagers = KeyBasedManagers;
                int activeGlobalTasksCount = _pollyUtilityService.GetActiveCount(key);
                return new PollyServiceHealthMetrics
                {
                    CurrentKeyName = key,
                    ActiveGlobalTasksCount = activeGlobalTasksCount,
                    ActiveNormalTasksCount = Math.Max(1 , GlobalSemaphoreSlimModel.InitialCount - 1) - NormalTaskSemaphore.CurrentCount ,
                    RequestQueueCount = MaxRequestsPerWindow,
                    RateLimitUsagePercentage = _currentUsagePercentage ,
                    IsCircuitBreakerOpen = isOpened,
                    CurrentStateName = currentState.ToString(),
                    LastActivityTime = WatchdogExecutionSettings.LastActivityTime,
                    WaitingGlobalTasksCount = GlobalConcurrencySemaphoreManager.RealTimeWaitingCount,
                    WaitingNormalTasksCount = NormalTaskSemaphoreManager.RealTimeWaitingCount,
                    IsShuttingDown = _isShuttingDown
                };
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
                var result = await ExecuteWithLockAsync(
                    nextWaiter.Operation,
                    nextWaiter.CancellationToken ,
                    false
                );

                var releaser = new Releaser(
                    GlobalConcurrencySemaphoreManager ,
                    NormalTaskSemaphoreManager ,
                    nextWaiter.Operation.Key ,
                    _keyBasedManagers ,
                    () => { WatchdogExecutionSettings.LastActivityTime = DateTime.UtcNow; }
                );

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
            var valueTask = ShutdownValueAsync(timeout);
            return TaskService.ToTaskQuickly(valueTask);
        }
        public async ValueTask<bool> ShutdownValueAsync(TimeSpan timeout)
        {
            _logger.LogInformation("Initiating graceful shutdown...");
            _isShuttingDown = true; // 設為 True 後，LockAsync 應拒絕新請求

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

        /// <summary>
        /// inner class to release the <see cref="SemaphoreSlim"/> instance
        /// </summary>
        public class Releaser : IDisposable
        {
            private readonly ISemaphoreSlimManager _globalManager;
            private readonly ISemaphoreSlimManager _currentManager;
            private readonly string _key;
            private readonly ConcurrentDictionary<string , ISemaphoreSlimManager> _dictionary;
            private int _isReleased = 0; // 防止重複 Release
            private readonly Action _onDispose;

            public Releaser(
                ISemaphoreSlimManager globalManager ,
                ISemaphoreSlimManager currentManager ,
                string key ,
                ConcurrentDictionary<string , ISemaphoreSlimManager> dictionary ,
                Action onDispose
            )
            {
                _globalManager = globalManager;
                _currentManager = currentManager;
                _key = key;
                _dictionary = dictionary;
                _onDispose = onDispose;
            }

            public void TryToRelease()
            {
                if(Interlocked.Exchange(ref _isReleased , 1) == 0)
                {
                    _globalManager?.TryToRelease();

                    // 在這裡執行清理邏輯
                    if(_currentManager != null && _currentManager.IsAvailable)
                    {
                        // 透過傳入的字典引用進行移除
                        _dictionary?.TryRemove(_key , out _);
                        _currentManager.TryToDispose();
                    }

                    _onDispose?.Invoke();
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
            _isShuttingDown = true;

            // 安全地取消並釋放 CancellationTokenSource，防止多執行緒下的 ObjectDisposedException。
            WatchdogExecutionSettings.cancellationTokenSource.SafeCancelAndDispose();

            var releaser = new Releaser(
                GlobalConcurrencySemaphoreManager ,
                NormalTaskSemaphoreManager ,
                OperationKey.Key ,
                _keyBasedManagers ,
                () => { }
            );

            releaser.Dispose();
        }

        //private RetryStrategyOptions<TModel> CreateRetryOptions()
        //{
        //    return new RetryStrategyOptions<TModel>
        //    {
        //        ShouldHandle = new PredicateBuilder<TModel>().Handle<Exception>() , // 簡化邏輯
        //        OnRetry = async args =>
        //        {
        //            // 1. 執行工具包內建的 [LoggerMessage] (這部分是內建的，不需要外部寫)
        //            LogWarningDueToPollyTimeout(args.RetryDelay.TotalMilliseconds);

        //            // 2. 執行使用者從 PollyStrategyConfig 注入的自定義邏輯 (如果有)
        //            if(_strategyConfig.OnRetry != null)
        //            {
        //                await _strategyConfig.OnRetry(args);
        //            }
        //        }
        //    };
        //}
    }
}
