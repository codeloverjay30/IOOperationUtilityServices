using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices
{
    /// <summary>
    /// Utility class for <see cref="gloabl::Polly."/>
    /// </summary>
    public class PollyKeyBasedManagerUtilityService : IPollyKeyBasedManagerUtilityService
    {
        public ConcurrentDictionary<string , ISemaphoreSlimManager> KeyBasedManagers { get; set; }

        public PollyKeyBasedManagerUtilityService(
            ConcurrentDictionary<string , ISemaphoreSlimManager>? keyBasedManagers = null
        )
        {
            KeyBasedManagers = keyBasedManagers ?? new();
        }

        /// <summary>
        /// Get the count of current active task given the <paramref name="key"/> 
        /// </summary>
        /// <param name="key">The key used to find <see cref="global::ThreadLevelLockingUtilityServices.PollyKeyBasedManagerUtilityService.KeyBasedManagers"/></param>
        /// <returns></returns>
        public int GetActiveCount(string key)
        {
            if (KeyBasedManagers.TryGetValue(key, out var manager))
            {
                return manager.GetCurrentActiveCount();
            }

            return 0;
        }
    }
}
