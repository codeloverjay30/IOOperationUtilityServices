using IOOperation.BaseUtilityServices;
using System;
using System.Collections.Generic;
using System.Text;
using Tasks.Model;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices
{
    public interface IConcurrentTaskDispatcher<TModel, TException>
        where TModel : OperationModel, new()
        where TException : Exception, new()
    {
        Task<T> ExecuteAsync<T>(
            Func<CancellationToken , Task<T>> taskFunc ,
            TaskPriority priority = TaskPriority.Normal ,
            CancellationToken ct = default);

        ValueTask<T> ExecuteValueAsync<T>(
            Func<CancellationToken , Task<T>> taskFunc ,
            TaskPriority priority = TaskPriority.Normal ,
            CancellationToken ct = default);
    }
}
