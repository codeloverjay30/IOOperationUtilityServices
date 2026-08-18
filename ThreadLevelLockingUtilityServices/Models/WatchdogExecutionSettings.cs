using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices.Models
{
    /// <summary>
    /// The configuration of watch dog.
    /// </summary>
    public class WatchdogExecutionSettings
    {
        public CancellationTokenSource cancellationTokenSource { get; init; } = new CancellationTokenSource();
        
        /// <summary>
        /// Last activity time that indicates the last open of watch dog.
        /// </summary>
        public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The cool time to close the watch dog when it is opened.
        /// </summary>
        public TimeSpan PollingTime { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The timeout of watch dog.
        /// </summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Whether to enable the watch dog. 
        /// </summary>
        public bool IsEnabled { get; init; } = false;

    }
}
