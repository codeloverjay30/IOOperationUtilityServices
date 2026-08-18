using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.RateLimiting;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices
{
    public class PollyServiceHealthMetricsUtilityService : IPollyServiceHealthMetricsUtilityService
    {
        private readonly ISemaphoreSlimManager _globalSemaphoreSlimManager;
        private readonly ISemaphoreSlimManager _normalTaskSemaphoreManager;
        private readonly CircuitBreakerStateProvider _circuitBreakerStateProvider;
        private readonly FixedWindowRateLimiterOptions _fixedWindowRateLimiterOptions;
        private readonly WatchdogExecutionSettings _watchdogExecutionSettings;
        private readonly PollyServiceHealthMetrics _previousHealthMetrics;

        private readonly IPollyKeyBasedManagerUtilityService _pollyKeyBasedManagerUtilityService;
        private readonly IPollyRateLimiterUtilityService _pollyRateLimiterUtilityService;
        public PollyServiceHealthMetricsUtilityService(
            ISemaphoreSlimManager globalManager,
            ISemaphoreSlimManager normalManager,
            CircuitBreakerStateProvider circuitBreakerStateProvider ,
            FixedWindowRateLimiterOptions fixedWindowRateLimiterOptions,
            WatchdogExecutionSettings watchdogExecutionSettings ,
            PollyServiceHealthMetrics previousHealthMetrics ,
            IPollyKeyBasedManagerUtilityService pollyKeyBasedManagerUtilityService = null,
            IPollyRateLimiterUtilityService pollyRateLimiterUtilityService = null
        )
        {
            ArgumentNullException.ThrowIfNull(pollyKeyBasedManagerUtilityService);

            _globalSemaphoreSlimManager = globalManager;
            _normalTaskSemaphoreManager = normalManager;

            _circuitBreakerStateProvider = circuitBreakerStateProvider;
            _fixedWindowRateLimiterOptions = fixedWindowRateLimiterOptions;
            _watchdogExecutionSettings = watchdogExecutionSettings;
            _previousHealthMetrics = previousHealthMetrics;
            _pollyKeyBasedManagerUtilityService = pollyKeyBasedManagerUtilityService;
            _pollyRateLimiterUtilityService = pollyRateLimiterUtilityService;
        }
#pragma warning disable IDE1006 // Naming Styles
        public PollyServiceHealthMetrics GetHealthMetrics()
        {
            int currentUsageRequest = int.MinValue;        
            double currentUsagePercentage = double.NaN;
            
            RateLimiterStatistics? statistics = _pollyRateLimiterUtilityService.GetCurrentStatistics();
            if(statistics != null)
            {
                currentUsageRequest = _fixedWindowRateLimiterOptions.PermitLimit - (int)statistics.CurrentAvailablePermits;
                currentUsagePercentage = (currentUsageRequest / (double)_fixedWindowRateLimiterOptions.PermitLimit) * 100;
            }
            var currentState = _circuitBreakerStateProvider.CircuitState;
            bool isOpened = currentState == CircuitState.Open || currentState == CircuitState.Isolated;
            int activeGlobalTasksCount = _pollyKeyBasedManagerUtilityService.GetActiveCount(_previousHealthMetrics.CurrentKeyName);
            return new PollyServiceHealthMetrics
            {
                CurrentKeyName = _previousHealthMetrics.CurrentKeyName ,
                ActiveGlobalTasksCount = activeGlobalTasksCount ,
                ActiveNormalTasksCount = Math.Max(1 , _globalSemaphoreSlimManager.Model.InitialCount - 1) - _normalTaskSemaphoreManager.SemaphoreSlimInstance.CurrentCount ,
                RequestQueueCount = currentUsageRequest ,
                RateLimitUsagePercentage = currentUsagePercentage ,
                IsCircuitBreakerOpen = isOpened ,
                CurrentStateName = currentState.ToString() ,
                LastActivityTime = _previousHealthMetrics.LastActivityTime ,
                WaitingGlobalTasksCount = _globalSemaphoreSlimManager.RealTimeWaitingCount ,
                WaitingNormalTasksCount = _normalTaskSemaphoreManager.RealTimeWaitingCount ,
                IsShuttingDown = _previousHealthMetrics.IsShuttingDown,
                AvailablePermitsAtLastBreak = statistics != null ? (int)statistics.CurrentAvailablePermits : -1
            };
        }
#pragma warning restore IDE1006 // Naming Styles
    }
}
