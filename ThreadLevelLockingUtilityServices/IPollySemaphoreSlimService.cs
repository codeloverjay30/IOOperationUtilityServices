using CommonModels;
using IOOperation.BaseUtilityServices;
using LoggerFactoryUtilityServices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Tasks.Model;
using TaskUtilityServices;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices
{
    public interface IPollySemaphoreSlimService<TModel, TResultStatus, TException>
        where TModel : OperationModel, new()
        where TResultStatus: StatusJsonModel,new()
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
        WatchdogExecutionSettings WatchdogExecutionSettings { get; }

        PollyServiceHealthMetrics ServHealthMetrics { get; }

        PollyCircuitBreakerExecutionSettings CircuitBreakerSettings { get; }

        int MaxRequestsPerWindow { get; }
        ILogger Logger { get; }

        ConcurrentDictionary<string , ISemaphoreSlimManager> KeyBasedManagers { get; }


        Task<(IDisposable ,TResultStatus)> ExecuteWithLockAsync(
            TModel operation ,
            CancellationToken ct = default ,
            bool isEmergency = false
        );

        ValueTask<(IDisposable, TResultStatus)> ExecuteWithLockUsingPollyPollValueAsync(
            TModel operation ,
            CancellationToken ct ,
            bool isEmergency
        );

        Task<(IDisposable,TResultStatus)> LockWithPriorityAsync(
            TModel operation ,
            TaskPriority priority ,
            CancellationToken ct = default ,
            bool isEmergency = false
        );

        ValueTask<(IDisposable, TResultStatus)> LockWithPriorityValueAsync(
             TModel operation ,
             TaskPriority priority ,
             CancellationToken ct = default ,
             bool isEmergency = false
        );

        PollyServiceHealthMetrics GetHealthMetrics(string key);
        Task<bool> ShutdownAsync(TimeSpan timeout);
        ValueTask<bool> ShutdownValueAsync(TimeSpan timeout);

        Task OpenCircuitBreaker();
        Task CloseCircuitBreaker();
    }
}
