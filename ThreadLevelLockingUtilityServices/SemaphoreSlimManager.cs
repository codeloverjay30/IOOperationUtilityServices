using System;
using System.Collections.Generic;
using System.Text;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices
{
    /// <summary>
    /// The manager that manages the <see cref="global::System.Threading.RateLimiting"/>
    /// </summary>
    public class SemaphoreSlimManager: ISemaphoreSlimManager
    {
        private int _waitingCounter = 0;
        public SemaphoreSlim SemaphoreSlimInstance { get; init; }
        public SemaphoreSlimModel Model { get; init; }
        public SemaphoreSlimManager(SemaphoreSlimModel semaphoreSlimModel)
        {
            Model = semaphoreSlimModel;
            SemaphoreSlimInstance = new SemaphoreSlim(semaphoreSlimModel.InitialCount, semaphoreSlimModel.MaxCount);
        }

        /// <summary>
        /// All tasks in <see cref="global::System.Threading.RateLimiting"/> are released.
        /// </summary>
        public bool IsAllReleased => SemaphoreSlimInstance.CurrentCount >= Model.MaxCount;

        public int RealTimeWaitingCount => _waitingCounter;

        /// <summary>
        /// Current active tasks
        /// </summary>
        /// <returns></returns>
        public int GetCurrentActiveCount() => Model.MaxCount - SemaphoreSlimInstance.CurrentCount;
        public bool IsAvailable => this.IsAllReleased && this.RealTimeWaitingCount == 0;

        /// <summary>
        /// Increase the <see cref="global::ThreadLevelLockingUtilityServices.SemaphoreSlimManager._waitingCounter"/>
        /// </summary>
        public void IncrementWaiter() => Interlocked.Increment(ref _waitingCounter);
 
         /// <summary>
        /// Decrease the <see cref="global::ThreadLevelLockingUtilityServices.SemaphoreSlimManager._waitingCounter"/>
        /// </summary>
        public void DecrementWaiter() => Interlocked.Decrement(ref _waitingCounter);

        /// <summary>
        /// Try to release the <see cref="global::System.Threading.RateLimiting"/> instance.
        /// </summary>
        public void TryToRelease()
        {
            try
            {
                SemaphoreSlimInstance?.Release();
            }
            catch (Exception ex)
            {
                
            }
        }

        /// <summary>
        /// Try to dispose the <see cref="global::System.Threading.RateLimiting"/> instance.
        /// </summary>
        
        public void TryToDispose()
        {
            if(IsAllReleased)
            {
                SemaphoreSlimInstance?.Dispose();
            }
        }     
    }
}
