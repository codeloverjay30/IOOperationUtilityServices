using System;
using System.Collections.Generic;
using System.Text;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices
{
    public interface ISemaphoreSlimManager
    {
        SemaphoreSlim SemaphoreSlimInstance { get; init; }
        SemaphoreSlimModel Model { get; init; }

        bool IsAllReleased { get; }

        int RealTimeWaitingCount { get; }
        int GetCurrentActiveCount();
        bool IsAvailable { get; }
        void IncrementWaiter();
        void DecrementWaiter();
        void TryToRelease();
        void TryToDispose();
    }
}
