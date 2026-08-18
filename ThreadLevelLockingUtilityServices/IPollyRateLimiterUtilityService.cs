using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.RateLimiting;
using Polly;
using Polly.RateLimiting;

namespace ThreadLevelLockingUtilityServices
{
    public interface IPollyRateLimiterUtilityService
    {
        RateLimiterStatistics? GetCurrentStatistics();
    }
}
