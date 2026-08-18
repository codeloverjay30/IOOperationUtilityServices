using IOOperation.BaseUtilityServices;
using System;
using System.Collections.Generic;
using System.Text;
using Tasks.Model;

namespace ThreadLevelLockingUtilityServices.Models
{
    /// <summary>
    /// The waiter that waits of the queue by priority (see <see cref="global::Tasks.Model.TaskPriority"/>)
    /// </summary>
    /// <typeparam name="TModel"><see cref="global::IOOperation.BaseUtilityServices.OperationModel"/></typeparam>
    public class PriorityWaiter<TModel>
        where TModel : OperationModel, new()
    {
        public TaskCompletionSource<IDisposable> Tcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// <see cref="global::Tasks.Model.TaskPriority"/>
        /// </summary>
        public TaskPriority Priority { get; init; }
        public CancellationToken CancellationToken { get; init; }
        public TModel Operation { get; init; }

        public PriorityWaiter(
            TModel operation ,
            TaskPriority priority ,
            CancellationToken ct
        )
        {
            Operation = operation;
            Priority = priority;
            CancellationToken = ct;
        }
    }
}
