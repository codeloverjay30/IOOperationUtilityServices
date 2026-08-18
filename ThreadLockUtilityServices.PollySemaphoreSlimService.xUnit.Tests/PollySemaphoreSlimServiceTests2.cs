using CommonModels;
using FluentAssertions;
using IOOperation.BaseUtilityServices;
using LoggerFactoryUtilityServices;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.RateLimiting;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.RateLimiting;
using TaskUtilityServices;
using ThreadLevelLockingUtilityServices;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLockUtilityServices.PollySemaphoreSlimService.xUnit.Tests
{
    public class PollySemaphoreSlimServiceTests2
    {
        // -------------------- Constants


        private static  ResiliencePropertyKey<OperationModel> _operationKey = new("MyOperationModel");
        private  ResiliencePropertyKey<RateLimiter> _rateLimiterKey = new ResiliencePropertyKey<RateLimiter>("MyRateLimiter");

        private double _currentUsagePercentage = 0.0;
        private int _currentRequestCount = 0;
        private  int _maxRequestsPerWindow = 10;
        private  TimeSpan _maxLimitRate = TimeSpan.FromSeconds(10);

        // -------------------- Mocks and Test Data
        private  ILoggerFactoryBaseUtilityService _mockLoggerFactory;
        private  ILogger _mockLogger;
        private  ITaskUtilityService _mockTaskService;

        // -------------------- Common Factory Setup
        private  IPollyStrategyConfigFactory _pollyStrategyConfigFactory;

        // -------------------- Common Config Setup
        private  PollyStrategyConfig<IDisposable> _pollyStrategyConfig;
        private  PollyStrategyConfig<IDisposable> _exceptionStrategyConfig = new PollyStrategyConfig<IDisposable>()
        {
            AdditionalExceptions = new List<Type> 
            { 
                typeof(OperationCanceledException),
                typeof(Polly.Timeout.TimeoutRejectedException) 
            }
        };


        // -------------------- Common POCO Setup
        private SemaphoreSlimModel _globalModel;
        private SemaphoreSlimModel _normalModel;
        private PollyServiceHealthMetrics _pollyServiceHealthMetrics;

        // -------------------- Common Execution Settings Setup
        private  WatchdogExecutionSettings _watchdogSettings;
        private  PollyCircuitBreakerExecutionSettings _cbSettings;

        // -------------------- Common Strategy Options or its related providers Setup
        private  FixedWindowRateLimiterOptions _fixedWindowRateLimiterOptions;
        private  FixedWindowRateLimiter _fixedWindowRateLimiter;
        private CircuitBreakerStateProvider _circuitBreakerStateProvider;

        // -------------------- Common Test Setup
        private IPollyServiceHealthMetricsUtilityService _pollyServiceHealthMetricsUtilityService;
        private  IPollyKeyBasedManagerUtilityService _pollyKeyBasedManagerUtilityService;
        private  IPollyRateLimiterUtilityService _pollyRateLimiterUtilityService;

        private  Func<RateLimiterArguments , ValueTask<RateLimitLease>> _rateLimiterDelegate;
        public PollySemaphoreSlimServiceTests2()
        {
            Initialize();
        }

        private void Initialize()
        {
            _mockLoggerFactory = Substitute.For<ILoggerFactoryBaseUtilityService>();
            _mockLogger = Substitute.For<ILogger>();
            _mockLoggerFactory.Logger.Returns(_mockLogger);

            _pollyStrategyConfigFactory = new PollyStrategyConfigFactory();

            _globalModel = new SemaphoreSlimModel { InitialCount = 2 , MaxCount = 2 };
            _normalModel = new SemaphoreSlimModel { InitialCount = 1 , MaxCount = 1 };

            _watchdogSettings = new WatchdogExecutionSettings
            {
                Timeout = TimeSpan.FromSeconds(5) ,
                PollingTime = TimeSpan.FromSeconds(1) ,
                cancellationTokenSource = new CancellationTokenSource() ,
                IsEnabled = false
            };
            _cbSettings = new PollyCircuitBreakerExecutionSettings
            {
                IsEnabled = true ,
            };

            _pollyKeyBasedManagerUtilityService = new PollyKeyBasedManagerUtilityService();

            _mockTaskService = Substitute.For<ITaskUtilityService>();
            _fixedWindowRateLimiterOptions = CreateFixedWindowRateLimiterOptions(
                _maxRequestsPerWindow ,
                _maxLimitRate
            );

            _fixedWindowRateLimiter = CreateFixedWindowRateLimiter(_fixedWindowRateLimiterOptions);

            _rateLimiterDelegate = args => _fixedWindowRateLimiter.AcquireAsync(1 , args.Context.CancellationToken);

            _pollyServiceHealthMetrics = CreateHealthMetrics();

            _pollyRateLimiterUtilityService = CreatePollyRateLimiterUtilityService(() => _fixedWindowRateLimiter);
    
            _pollyServiceHealthMetricsUtilityService = CreatePollyServiceHealthMetricsUtilityService(
                new SemaphoreSlimManager(_globalModel) ,
                new SemaphoreSlimManager(_normalModel) ,
                CreateCircuitBreakerStateProvider() ,
                _fixedWindowRateLimiterOptions ,
                _watchdogSettings ,
                _pollyServiceHealthMetrics ,
                _pollyKeyBasedManagerUtilityService ,
                _pollyRateLimiterUtilityService
            );

            _pollyStrategyConfig = _pollyStrategyConfigFactory.CreatePollyStrategyConfig<IDisposable>(
                _exceptionStrategyConfig,
                _cbSettings,
                _pollyServiceHealthMetricsUtilityService,
                _pollyRateLimiterUtilityService
            );
        }

        private void Configure()
        {
            _circuitBreakerStateProvider = new CircuitBreakerStateProvider();
            _pollyServiceHealthMetricsUtilityService = new PollyServiceHealthMetricsUtilityService(
                new SemaphoreSlimManager(_globalModel) ,
                new SemaphoreSlimManager(_normalModel) ,
                _circuitBreakerStateProvider ,
                _fixedWindowRateLimiterOptions ,
                _watchdogSettings ,
                CreateHealthMetrics() ,
                _pollyKeyBasedManagerUtilityService ,
                _pollyRateLimiterUtilityService
            );
        }

        private FixedWindowRateLimiterOptions CreateFixedWindowRateLimiterOptions(
            int maxRequestsPerWindow = 10 ,
            TimeSpan maxLimitRate = default
        )
        {
            maxLimitRate = maxLimitRate == default ? TimeSpan.FromSeconds(10) : maxLimitRate; // 預設10s

            return new FixedWindowRateLimiterOptions
            {
                PermitLimit = maxRequestsPerWindow ,
                Window = maxLimitRate ,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst ,
                QueueLimit = int.MaxValue
            };
        }

        private FixedWindowRateLimiter CreateFixedWindowRateLimiter(
            FixedWindowRateLimiterOptions fixedWindowRateLimiterOptions
        )
        {
            return new FixedWindowRateLimiter(fixedWindowRateLimiterOptions);
        }

        private CircuitBreakerStateProvider CreateCircuitBreakerStateProvider()
        {
            return new CircuitBreakerStateProvider();
        }

        private PollyRateLimiterUtilityService CreatePollyRateLimiterUtilityService(
            Func<RateLimiter?> rateLimiterProvider
        )
        {
            return new PollyRateLimiterUtilityService(rateLimiterProvider);
        }

        private SemaphoreSlimModel CreateSemaphoreSlimModel(
            int initialCount,
            int maxCount
        )
        {
            return new SemaphoreSlimModel { InitialCount = initialCount, MaxCount = maxCount };
        }

        private PollyServiceHealthMetrics CreateHealthMetrics(
            string keyName = "TestKey" ,
            bool isShuttingDown = false
        )
        {
            return new PollyServiceHealthMetrics
            {
                CurrentKeyName = keyName ,
                LastActivityTime = DateTime.UtcNow ,
                IsShuttingDown = isShuttingDown
            };
        }

        private IPollyServiceHealthMetricsUtilityService CreatePollyServiceHealthMetricsUtilityService(
             ISemaphoreSlimManager globalManager,
             ISemaphoreSlimManager normalManager ,
             CircuitBreakerStateProvider circuitBreakerStateProvider ,
             FixedWindowRateLimiterOptions fixedWindowRateLimiterOptions ,
             WatchdogExecutionSettings watchdogSettings ,
             PollyServiceHealthMetrics pollyServiceHealthMetrics ,
             IPollyKeyBasedManagerUtilityService pollyKeyBasedManagerUtilityService,
             IPollyRateLimiterUtilityService pollyRateLimiterUtilityService = null
        )
        {
            return new PollyServiceHealthMetricsUtilityService(
                globalManager ,
                normalManager ,
                circuitBreakerStateProvider ,
                fixedWindowRateLimiterOptions ,
                watchdogSettings ,
                pollyServiceHealthMetrics ,
                pollyKeyBasedManagerUtilityService ,
                pollyRateLimiterUtilityService
            );
        }

        private PollyStrategyConfig<T> CreatePollyStrategyConfig<T>(
            IPollyStrategyConfigFactory pollyStrategyConfigFactory,
            PollyStrategyConfig<T> originalStrategyConfig,
            PollyCircuitBreakerExecutionSettings circuitBreakerExecutionSettings,
            IPollyServiceHealthMetricsUtilityService pollyServiceHealthMetricsUtilityService,
            IPollyRateLimiterUtilityService rateLimiterUtility
        )
        {
            return pollyStrategyConfigFactory.CreatePollyStrategyConfig<T>(
                originalStrategyConfig,
                circuitBreakerExecutionSettings,
                pollyServiceHealthMetricsUtilityService,
                rateLimiterUtility
            );
        }

        private IPollySemaphoreSlimService<OperationModel , StatusJsonModel , LockTimeoutException> CreateService(
            int globalLimit = 10 ,
            int normalLimit = 5
        )
        {
            _globalModel = CreateSemaphoreSlimModel(globalLimit , globalLimit);
            _normalModel = CreateSemaphoreSlimModel(normalLimit , normalLimit);

            _mockTaskService.ToTaskQuickly(
                Arg.Any<ValueTask<(IDisposable,StatusJsonModel)>>()
            )
            .Returns(x => x.Arg<ValueTask<(IDisposable,StatusJsonModel)>>().AsTask());

            _mockTaskService.ToTaskQuickly(
                Arg.Any<ValueTask<bool>>()
            )
            .Returns(x => x.Arg<ValueTask<bool>>().AsTask());

            // 1. 建立有效的 RetryOptions (必須有 ShouldHandle)
            var retryOptions = new RetryStrategyOptions<IDisposable>
            {
                MaxRetryAttempts = 1 ,
                BackoffType = DelayBackoffType.Constant ,
                Delay = TimeSpan.Zero ,
                ShouldHandle = new PredicateBuilder<IDisposable>().Handle<OperationCanceledException>().Handle<Polly.Timeout.TimeoutRejectedException>()
            };

            // 2. 建立有效的 FallbackOptions
            var onFallbackCallback = new Func<OnFallbackArguments<IDisposable> , ValueTask>(args =>
            {
                ArgumentNullException.ThrowIfNull(args.Outcome.Result , nameof(args.Outcome.Result));

                if(!args.Context.Properties.TryGetValue(_operationKey , out var operation))
                {
                    // 如果取不到模型，則使用預設名稱或拋出異常
                    throw new InvalidOperationException("Operation model not found in Polly context.");
                }

                var exception = args.Outcome.Exception;
                var cts = args.Context.CancellationToken;
                var isCancelled = cts.IsCancellationRequested;
                string taskName = operation.TaskName;
                switch(exception)
                {
                    case OperationCanceledException ex:
                        if(isCancelled)
                        {
                            return ValueTask.FromException(ex);
                        }
                        return ValueTask.FromException(ex);
                    case Polly.Timeout.TimeoutRejectedException ex:
                        return ValueTask.FromException(ex);
                    default:
                        throw new Exception("Unknown exception occurred in fallback.");
                }
            });

            var fallbackActionCallback = new Func<FallbackActionArguments<IDisposable> , ValueTask<Outcome<IDisposable>>>(args =>
            {
                ArgumentNullException.ThrowIfNull(args.Outcome.Result , nameof(args.Outcome.Result));
                var result = args.Outcome.Result;
                if(!args.Context.Properties.TryGetValue(_operationKey , out var operation))
                {
                    // 如果取不到模型，則使用預設名稱或拋出異常
                    throw new InvalidOperationException("Operation model not found in Polly context.");
                }

                var exception = args.Outcome.Exception;
                var cts = args.Context.CancellationToken;
                var isCancelled = cts.IsCancellationRequested;
                string taskName = operation.TaskName;
                switch(exception)
                {
                    case OperationCanceledException ex:
                        if(isCancelled)
                        {
                            return Outcome.FromResultAsValueTask(result);
                        }
                        return Outcome.FromResultAsValueTask(result);
                    case Polly.Timeout.TimeoutRejectedException ex:
                        return Outcome.FromResultAsValueTask(result);
                    default:
                        throw new Exception("Unknown exception occurred in fallback.");
                }
            });

            var fallbackOptions = new FallbackStrategyOptions<IDisposable>
            {
                ShouldHandle = new PredicateBuilder<IDisposable>().Handle<OperationCanceledException>().Handle<Polly.Timeout.TimeoutRejectedException>() ,
                // 新增這一段：定義當發生錯誤時應該回傳什麼
                FallbackAction = args =>
                {
                    return Outcome.FromResultAsValueTask(default(IDisposable));
                },
                OnFallback = onFallbackCallback,
            };

            // 3. 建立有效的 CircuitBreakerOptions
            var cbOptions = new CircuitBreakerStrategyOptions<IDisposable>
            {
                ShouldHandle = new PredicateBuilder<IDisposable>().Handle<OperationCanceledException>().Handle<Polly.Timeout.TimeoutRejectedException>() ,
                BreakDuration = TimeSpan.FromSeconds(5)
            };

            _pollyKeyBasedManagerUtilityService = new PollyKeyBasedManagerUtilityService();

            _fixedWindowRateLimiterOptions = CreateFixedWindowRateLimiterOptions(
                _maxRequestsPerWindow ,
                _maxLimitRate
            );

            _fixedWindowRateLimiter = CreateFixedWindowRateLimiter(_fixedWindowRateLimiterOptions);

            _rateLimiterDelegate = args => _fixedWindowRateLimiter.AcquireAsync(1 , args.Context.CancellationToken);

            var onRateLimitRejectedCallback = new Func<OnRateLimiterRejectedArguments , ValueTask>(args =>
            {
                if(args.Context.Properties.TryGetValue(_rateLimiterKey , out var rateLimiter))
                {
                    var statistics = rateLimiter.GetStatistics();
                    if(statistics != null)
                    {
                        _currentRequestCount = _fixedWindowRateLimiterOptions.PermitLimit - (int)statistics.CurrentAvailablePermits;
                        double usage = (double)_currentRequestCount / _fixedWindowRateLimiterOptions.PermitLimit * 100;
                        _currentUsagePercentage = usage;
                    }
                    else
                    {
                        _currentRequestCount = int.MinValue;
                        _currentUsagePercentage = double.NaN;
                    }
                }
                return default;
            });

            // 4. 建立有效的 RateLimiterOptions (如果需要的話)
            var rateLimiterOptions = new RateLimiterStrategyOptions
            {
                OnRejected = args =>
                {
                    onRateLimitRejectedCallback?.Invoke(args);
                    return default;
                } ,
                RateLimiter = args => _rateLimiterDelegate?.Invoke(args) ?? ValueTask.FromResult(default(RateLimitLease)) ,
            };

            _pollyServiceHealthMetrics = CreateHealthMetrics();

            _pollyRateLimiterUtilityService = CreatePollyRateLimiterUtilityService(() => _fixedWindowRateLimiter);
    
            var fixedRateLimiter = new FixedWindowRateLimiter(_fixedWindowRateLimiterOptions);
            var pollyRateLimiterUtilityService = new PollyRateLimiterUtilityService(()=>_fixedWindowRateLimiter);
    
            _exceptionStrategyConfig.RateLimiterDelegate = _rateLimiterDelegate;
            
            _pollyStrategyConfig = _pollyStrategyConfigFactory.CreatePollyStrategyConfig<IDisposable>(
                _exceptionStrategyConfig,
                _cbSettings,
                _pollyServiceHealthMetricsUtilityService,
                _pollyRateLimiterUtilityService
            );
            return new PollySemaphoreSlimService<OperationModel , StatusJsonModel , LockTimeoutException>(
                _mockLoggerFactory ,
                _mockTaskService ,
                _pollyKeyBasedManagerUtilityService,
                _globalModel ,
                2 ,
                TimeSpan.FromSeconds(10) ,
                _watchdogSettings ,
                _cbSettings,
                _pollyStrategyConfig
            );
        }

        [Fact]
        public async Task ExecuteWithLockAsync_ShouldAcquireAndReleaseLock_Successfully()
        {
            Initialize();
            // Arrange
            var service = CreateService();
            var operationModel = new OperationModel { Key = "TestKey" , TaskName = "TestTask" };

            // Act
            var (releaser, statusJsonModel) = await service.ExecuteWithLockAsync(operationModel);

            releaser.Should().NotBeNull();
            var metrics = service.GetHealthMetrics(operationModel.Key);
            metrics.ActiveGlobalTasksCount.Should().Be(1);

            // Assert - After Dispose
            releaser?.Dispose();
            var finalMetrics = service.GetHealthMetrics(operationModel.Key);
            finalMetrics.ActiveGlobalTasksCount.Should().Be(0);
        }

        [Fact]
        public async Task ExecuteWithLockAsync_WhenCancelled_ShouldThrowOperationCanceledException()
        {
            Initialize();
            // Arrange
            var service = CreateService();
            var model = new OperationModel { Key = "CancelKey" };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var (releaser, statusJsonModel) = await service.ExecuteWithLockAsync(model , cts.Token);

            statusJsonModel.IsSuccess.Should().BeFalse();
            statusJsonModel.Result.ToLower().Should().BeEquivalentTo("error");
            statusJsonModel.OverallErrorMessage.ToLower().Should().Contain("cancel");

            releaser?.Dispose();
        }

        [Fact]
        public async Task GetHealthMetrics_ShouldReflectCurrentState()
        {
            Initialize();
            // Arrange
            var service = CreateService(globalLimit: 10);
            var operationModel = new OperationModel { Key = "MetricKey" };

            // Act
            var (releaser, statusJsonModel) = await service.ExecuteWithLockAsync(operationModel);
            var metrics = service.GetHealthMetrics(operationModel.Key);

            // Assert
            metrics.ActiveGlobalTasksCount.Should().Be(1);
            metrics.WaitingGlobalTasksCount.Should().Be(0);
            //metrics.CurrentAvailableGlobalTasksCount.Should().Be(9);

            releaser?.Dispose();

            var finalMetrics = service.GetHealthMetrics(operationModel.Key);
            finalMetrics.ActiveGlobalTasksCount.Should().Be(0);
            //finalMetrics.CurrentAvailableGlobalTasksCount.Should().Be(10);
            finalMetrics.WaitingGlobalTasksCount.Should().Be(0);
        }

        [Fact]
        public async Task ShutdownAsync_ShouldReturnTrue_WhenTasksAreCompleted()
        {
            Initialize();
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.ShutdownAsync(TimeSpan.FromSeconds(100));

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ConcurrentAccess_ShouldRespectMaxCount()
        {
            // Arrange
            int maxCount = 2;
            var service = CreateService(globalLimit: maxCount);
            var model = new OperationModel { Key = "ConcurrentKey" };
            var model1 = new OperationModel { Key = "ConcurrentKey" };
            var model2 = new OperationModel { Key = "ConcurrentKey" };
            var model3 = new OperationModel { Key = "ConcurrentKey" };

            // 不要用 await，讓它變成一個 Task
            var task1 = service.ExecuteWithLockAsync(model1); 
            var task2 = service.ExecuteWithLockAsync(model2); // 假設這兩個會佔滿 Global

            // 測試第三個是否真的在等
            var task3 = service.ExecuteWithLockAsync(model3);
            var completedTask = await Task.WhenAny(task3, Task.Delay(500));
            completedTask.Should().NotBe(task3);
        }
    }
}
