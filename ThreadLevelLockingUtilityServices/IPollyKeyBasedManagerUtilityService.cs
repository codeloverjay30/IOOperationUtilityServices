using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices
{
    public interface IPollyKeyBasedManagerUtilityService
    {
        ConcurrentDictionary<string , ISemaphoreSlimManager> KeyBasedManagers { get; set; }
        int GetActiveCount(string key);
    }
}
