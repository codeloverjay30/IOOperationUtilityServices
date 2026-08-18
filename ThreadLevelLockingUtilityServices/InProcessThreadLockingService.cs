using AsyncKeyedLock;
using LoggerFactoryUtilityServices;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TaskUtilityServices;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices
{
    public partial class InProcessThreadLockingService<TKey>(
        ILoggerFactoryBaseUtilityService loggerFactoryService ,
        ITaskUtilityService taskUtilityService,
        AsyncKeyedLocker<TKey> keyedLocker
    ):
        ThreadLevelLockingBaseUtilityService(loggerFactoryService),
        IInProcessThreadLockingService<TKey>
        where TKey : notnull
    {
        private readonly AsyncKeyedLocker<TKey> _keyedLocker = keyedLocker;
        private readonly ITaskUtilityService _taskUtilityService = taskUtilityService;

        [LoggerMessage(Level = LogLevel.Debug , Message = "Attempting to acquire lock for key: {Key}")]
        static partial void LogAttemptingLock(ILogger logger , TKey key);

        [LoggerMessage(Level = LogLevel.Information , Message = "Lock acquired. Executing task for key: {Key}")]
        static partial void LogLockAcquired(ILogger logger , TKey key);

        [LoggerMessage(Level = LogLevel.Error , Message = "An error occurred during execution for key: {Key}")]
        static partial void LogErrorDuringExecution(ILogger logger , Exception ex , TKey key);

        [LoggerMessage(Level = LogLevel.Information , Message = "Task completed. Lock released for key: {Key}")]
        static partial void LogLockReleased(ILogger logger , TKey key);

        /// <summary>
        /// Locks the execution flow based on the provided <paramref name="key"/>, executes the task, 
        /// and ensures the lock is released after completion.
        /// </summary>
        /// <typeparam name="T">The type of the argument passed to the function.</typeparam>
        /// <typeparam name="TResult">The type of the result returned by the function.</typeparam>
        /// <param name="key">The unique identifier used as a key for the lock.</param>
        /// <param name="func">The asynchronous function to be executed within the lock.</param>
        /// <param name="args">The arguments to be passed into the <paramref name="func"/>.</param>
        /// <returns>A task representing the asynchronous operation, containing the result of <paramref name="func"/>.</returns>
        public Task<TResult> LockAndExecuteAsync<T, TResult>(
            TKey key ,
            Func<T , Task<TResult>> func ,
            T args
        )
        {
            var valueTask = LockAndExecuteValueAsync(key , func , args);
            return _taskUtilityService.ToTaskQuickly(valueTask);
        }
        public async ValueTask<TResult> LockAndExecuteValueAsync<T, TResult>(
            TKey key ,
            Func<T , Task<TResult>> func ,
            T args
        )
        {
            // 呼叫生成的效能優化方法
            LogAttemptingLock(loggerFactoryService.Logger , key);

            using(await _keyedLocker.LockAsync(key).ConfigureAwait(false))
            {
                try
                {
                    LogLockAcquired(loggerFactoryService.Logger , key);
                    return await func.Invoke(args).ConfigureAwait(false);
                }
                catch(Exception ex)
                {
                    LogErrorDuringExecution(loggerFactoryService.Logger , ex , key);
                    throw;
                }
                finally
                {
                    LogLockReleased(loggerFactoryService.Logger , key);
                }
            }
        }
    }
}
