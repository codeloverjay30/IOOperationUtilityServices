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
    public partial class ConcurrentTaskDispatcher<TModel, TException> : IConcurrentTaskDispatcher<TModel,TException>
        where TModel : OperationModel, new()
        where TException : Exception, new()
    {
        private readonly ILoggerFactoryBaseUtilityService _loggerFactoryService;
        private readonly ITaskUtilityService _taskUtilityService;

        private readonly ISemaphoreSlimService<TModel,TException> _lockService;
        private ILogger _logger => _loggerFactoryService.Logger;

        [LoggerMessage(Level = LogLevel.Debug , Message = "Task started with priority: {Priority}")]
        static partial void LogAttemptingLock(ILogger logger , TaskPriority priority);

        [LoggerMessage(Level = LogLevel.Error , Message = "Task execution failed.")]
        static partial void LogTaskFailure(ILogger logger, Exception ex);
        public ConcurrentTaskDispatcher(
            ILoggerFactoryBaseUtilityService loggerFactoryService,
            ITaskUtilityService taskUtilityService,
            ISemaphoreSlimService<TModel,TException> lockService
        )
        {
            _loggerFactoryService = loggerFactoryService;
            _taskUtilityService = taskUtilityService;
            _lockService = lockService;
        }

       
        /// <summary>
        /// Execute the task with higher priority
        /// </summary>
        public Task<T> ExecuteAsync<T>(
            Func<CancellationToken , Task<T>> taskFunc ,
            TaskPriority priority = TaskPriority.Normal ,
            CancellationToken ct = default)
        {
            var valueTask = ExecuteValueAsync(taskFunc:taskFunc, priority: priority, ct: ct);
            return _taskUtilityService.ToTaskQuickly(valueTask);
        }
        public async ValueTask<T> ExecuteValueAsync<T>(
            Func<CancellationToken , Task<T>> taskFunc ,
            TaskPriority priority = TaskPriority.Normal ,
            CancellationToken ct = default)
        {
            // 1. 透過優先權隊列取得鎖
            using(await _lockService.LockWithPriorityAsync(priority , ct))
            {
                try
                {
                    LogAttemptingLock(_logger , priority);
                    return await taskFunc(ct);
                }
                catch(Exception ex)
                {
                    _lockService.ReportFailure(); // 整合斷路器
                    LogTaskFailure(_logger,ex);
                    throw;
                }
            }
        }
    }
}
