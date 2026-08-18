using CustomDataAnnotations.Maintenance;
using IOOperation.BaseUtilityServices;
using LoggerFactoryUtilityServices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Tasks.Model;
using TaskUtilityServices;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices
{
    /// <typeparam name="TModel"></typeparam>
    /// <typeparam name="TException"></typeparam>
    /// <remarks>
    /// <inheritdoc cref="global::ThreadLevelLockingUtilityServices.ISemaphoreSlimService{TModel, TException}"/>
    /// </remarks>
    [Obsolete("It can't retry to execute the task many times, and isn't integrated with Polly v8. Use <see cref=\"global::ThreadLevelLockingUtilityServices.ISemaphoreSlimService{TModel, TException}\"/> instead.")]
    [TechnicalDebt(CategoryType.NeedsChanged | CategoryType.InstableBehaviorInMultipleThreadsIssue,"global::ThreadLevelLockingUtilityServices.ISemaphoreSlimService{TModel, TException}")]
    public interface ISemaphoreSlimService<TModel,TException>
        where TModel : OperationModel , new()
        where TException : Exception, new()
    {
        ILoggerFactoryBaseUtilityService LoggerFactoryService { get; }
        ITaskUtilityService TaskService { get; }
        SemaphoreSlimModel GlobalSemaphoreSlimModel { get; }

        ISemaphoreSlimManager GlobalConcurrencySemaphoreManager { get; }

        SemaphoreSlim GlobalConcurrencySemaphore { get; }

        SemaphoreSlimModel NormalSemaphoreSlimModel { get; }
        ISemaphoreSlimManager NormalTaskSemaphoreManager { get; }
        SemaphoreSlim NormalTaskSemaphore { get; }
        WatchdogExecutionSettings Watchdog { get; }

        ServiceHealthMetrics ServHealthMetrics { get; }

        CircuitBreakerExecutionSettings CircuitBreaker { get; }

        int MaxRequestsPerWindow { get; }


        TimeSpan MaxLimitRate { get; }

        int CurrentMaxRequestsPerWindow { get; }
        DateTime [ ] RateLimitBuffer { get; }

        ILogger Logger { get; }

        void ReportFailure();

        Task<IDisposable> ExecuteWithLockAsync(
            CancellationToken ct = default ,
            bool isEmergency = false
        );
        ValueTask<IDisposable> LockValueAsync(
            CancellationToken ct = default ,
            bool isEmergency = false
        );
        Task<IDisposable> LockWithCircuitBreakerAsync(
            CancellationToken ct = default ,
            TimeSpan timeout = default ,
            bool isEmergency = false
        );
        ValueTask<IDisposable> LockWithCircuitBreakerValueAsync(
            CancellationToken ct = default ,
            TimeSpan timeout = default ,
            bool isEmergency = false
        );
        Task<IDisposable> LockWithPriorityAsync(
            TaskPriority priority ,
            CancellationToken ct = default ,
            bool isEmergency = false
        );
        ValueTask<IDisposable> LockWithPriorityValueAsync(
            TaskPriority priority ,
            CancellationToken ct = default ,
            bool isEmergency = false
        );
        Task<IDisposable> LockWithTimeoutAsync(
            CancellationToken ct = default ,
            TimeSpan timeout = default ,
            bool isEmergency = false
        );
        ValueTask<IDisposable> LockWithTimeoutValueAsync(
            CancellationToken ct = default ,
            TimeSpan timeout = default ,
            bool isEmergency = false
        );

        Task ApplyRateLimitAsync(CancellationToken ct);
        ValueTask ApplyRateLimitValueAsync(CancellationToken ct);
        ServiceHealthMetrics GetHealthMetrics();
        void UpdateRateLimit(int newMaxRequests);
        Task<bool> ShutdownAsync(TimeSpan timeout);
        ValueTask<bool> ShutdownValueAsync(TimeSpan timeout);
    }
}
