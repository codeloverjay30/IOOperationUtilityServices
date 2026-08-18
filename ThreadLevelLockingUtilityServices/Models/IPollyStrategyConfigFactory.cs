using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices.Models
{
    public interface IPollyStrategyConfigFactory
    {
        PollyStrategyConfig<T> CreatePollyStrategyConfig<T>(
            PollyStrategyConfig<T> originalStrategyConfig,
            PollyCircuitBreakerExecutionSettings circuitBreakerExecutionSettings,
            IPollyServiceHealthMetricsUtilityService pollyServiceHealthMetricsUtilityService,
            IPollyRateLimiterUtilityService rateLimiterUtility // 注入統計工具
        );
    }
}
