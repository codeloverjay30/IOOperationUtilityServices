namespace ThreadLevelLockingUtilityServices.Models
{
    /// <summary>
    /// The health metrics that stores the info of global tasks and normal tasks etc.
    /// </summary>
    public class PollyServiceHealthMetrics
    {
        public int MaxGlobalTasksCount { get; init; } = 0;
        public int MaxNormalTasksCount { get; init; } = 0;

        public int WaitingGlobalTasksCount { get; set; } = 0;
        public int WaitingNormalTasksCount { get; set; } = 0;
        public int ActiveGlobalTasksCount { get; init; } = 0;
        public int ActiveNormalTasksCount { get; init; } = 0;
        public int RequestQueueCount { get; init; } = 0;
        
        /// <summary>
        /// The current usage percentage of rate limiter.
        /// </summary>
        /// <remarks>
        /// It is updated only iff the cirucit breaker is opened or closed.
        /// </remarks>
        public double RateLimitUsagePercentage { get; init; } = 0.7; // 70%

        /// <summary>
        /// The circuit beaker is opened.
        /// </summary>
        public bool IsCircuitBreakerOpen { get; set; } = true;

        /// <summary>
        /// Current status of circuit breaker.
        /// </summary>
        /// <remarks>
        /// It is updated only iff the circuit breaker is opened or closed.
        /// </remarks>
        public string CurrentStateName { get; set; } = "None";

        /// <summary>
        /// Current key name
        /// </summary>
        public string CurrentKeyName { get; init; } = string.Empty;

        /// <summary>
        /// The last activity time that indicates the recent open of circuit breaker 
        /// </summary>
        /// <remarks>
        /// It will be updated ONLY when the circuit breaker is opened.
        /// </remarks>
        public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The service is shutting down or not.
        /// </summary>
        public bool IsShuttingDown { get; set; }

        /// <summary>
        /// Available permits at the last open of circuit breaker.
        /// </summary>
        /// <remarks>
        /// It is updated only iff the circuit breaker is opened.
        /// </remarks>
        public int AvailablePermitsAtLastBreak { get; set; } = -1;
    }
}
