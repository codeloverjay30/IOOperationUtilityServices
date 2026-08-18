using CustomDataAnnotations.Maintenance;

namespace ThreadLevelLockingUtilityServices.Models
{
    /// <remarks>
    /// Use <see cref="global::ThreadLevelLockingUtilityServices.Models.PollyServiceHealthMetrics"/> POCO instead.
    /// For reason, see the remarks of <see cref="global::ThreadLevelLockingUtilityServices.SemaphoreSlimService{TModel,TException}"/> class
    /// </remarks>
    [Obsolete("It can't retry to execute the task many times, and isn't integrated with Polly v8. Use <see cref=\"global::ThreadLevelLockingUtilityServices.Models.PollyServiceHealthMetrics\"/> POCO instead.")]
    [TechnicalDebt(CategoryType.NeedsChanged | CategoryType.InstableBehaviorInMultipleThreadsIssue,"global::ThreadLevelLockingUtilityServices.Models.PollyServiceHealthMetrics")]
    public class ServiceHealthMetrics
    {
        public int ActiveGlobalTasks { get; init; } = 0;
        public int ActiveNormalTasks { get; init; } = 0;
        public int RequestQueueCount { get; init; } = 0;
        public double RateLimitUsagePercentage { get; init; } = 0.7; // 70%
        public bool IsCircuitBreakerOpen { get; init; } = true;
        public DateTime LastActivityTime { get; init; } = DateTime.UtcNow;

        public int GlobalWaitingTasks { get; set; }
        public int NormalWaitingTasks { get; set; }
        public bool IsShuttingDown { get; set; }
    }
}
