using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.RateLimiting;
using Polly;
using Polly.RateLimiting;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices
{
    public interface IPollyServiceHealthMetricsUtilityService
    {
        PollyServiceHealthMetrics GetHealthMetrics();
    }
}
