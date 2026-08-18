using Polly;
using Polly.RateLimiting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.RateLimiting;

namespace ThreadLevelLockingUtilityServices
{
    public class PollyRateLimiterUtilityService : IPollyRateLimiterUtilityService
    {
        private readonly Func<RateLimiter?> _rateLimiterProvider;

        public PollyRateLimiterUtilityService(Func<RateLimiter?> rateLimiterProvider)
        {
            _rateLimiterProvider = rateLimiterProvider;
        }

        public RateLimiterStatistics? GetCurrentStatistics()
        {
            return _rateLimiterProvider()?.GetStatistics();
        }
    }
}
