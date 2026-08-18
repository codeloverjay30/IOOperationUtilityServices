using Polly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.RateLimiting;

namespace ThreadLevelLockingUtilityServices.Models
{
    /// <summary>
    /// Factory class to create <see cref="global::ThreadLevelLockingUtilityServices.Models.PollyStrategyConfig{TInput}"/> 
    /// </summary>
    public class PollyStrategyConfigFactory : IPollyStrategyConfigFactory
    {
        public PollyStrategyConfig<T> CreatePollyStrategyConfig<T>(
            PollyStrategyConfig<T> originalStrategyConfig,
            PollyCircuitBreakerExecutionSettings circuitBreakerExecutionSettings,
            IPollyServiceHealthMetricsUtilityService pollyServiceHealthMetricsUtilityService,
            IPollyRateLimiterUtilityService rateLimiterUtility // 注入統計工具
        )
        {
            return new PollyStrategyConfig<T>()
            {
                AdditionalExceptions = originalStrategyConfig.AdditionalExceptions , // 指定ShouldHandle應該要處理哪些例外
                OnRetry = null , // 使用預設的重試邏輯
                OnFallback = null , // 使用預設的降級邏輯
                OnCircuitBreakerOpenedCallback = args =>
                {
                    var openUntil = DateTime.UtcNow.Add(args.BreakDuration);
                    
                    // 在斷路器關閉時更新健康指標
                    var statistics = rateLimiterUtility.GetCurrentStatistics();
                    var healthMetrics = pollyServiceHealthMetricsUtilityService.GetHealthMetrics();
                    if (statistics != null)
                    {
                       healthMetrics.AvailablePermitsAtLastBreak = (int)statistics.CurrentAvailablePermits;
                    }

                    healthMetrics.IsCircuitBreakerOpen = true;
                    healthMetrics.CurrentStateName = "Open";
                    healthMetrics.LastActivityTime = openUntil;
                    circuitBreakerExecutionSettings.OpenUntil = openUntil; // 更新斷路器的打開時間

                    return default;
                },
                OnCircuitBreakerClosedCallback = args =>
                {
                    var lastActivityTime = DateTime.UtcNow;

                    // 在斷路器關閉時更新健康指標
                    var statistics = rateLimiterUtility.GetCurrentStatistics();
                    var healthMetrics = pollyServiceHealthMetricsUtilityService.GetHealthMetrics();
                    if (statistics != null)
                    {
                       healthMetrics.AvailablePermitsAtLastBreak = (int)statistics.CurrentAvailablePermits;
                    }
                    healthMetrics.IsCircuitBreakerOpen = false;
                    healthMetrics.CurrentStateName = "Closed";
                    healthMetrics.LastActivityTime = lastActivityTime;
                    circuitBreakerExecutionSettings.OpenUntil = lastActivityTime;

                    return default;
                } ,
                OnRateLimiterRejectedCallback = args =>
                {
                    var lastActivityTime = DateTime.UtcNow;
                    // 在斷路器關閉時更新健康指標
                    var statistics = rateLimiterUtility.GetCurrentStatistics();
                    var healthMetrics = pollyServiceHealthMetricsUtilityService.GetHealthMetrics();
                    if (statistics != null)
                    {
                       healthMetrics.AvailablePermitsAtLastBreak = (int)statistics.CurrentAvailablePermits;
                    }
                    healthMetrics.IsCircuitBreakerOpen = false;
                    healthMetrics.CurrentStateName = "Closed";
                    healthMetrics.LastActivityTime = lastActivityTime;
                    circuitBreakerExecutionSettings.OpenUntil = lastActivityTime;

                    return default;
                } ,
                RateLimiterDelegate = async args =>
                {
                    if(originalStrategyConfig.RateLimiterDelegate != null) // 這裡因為是 null，所以直接跳過
                    { 
                        return await originalStrategyConfig.RateLimiterDelegate(args);
                    }
                    return await ValueTask.FromResult(default(RateLimitLease)); // 最終回傳預設租約
                }
            };
        }
    }
}
