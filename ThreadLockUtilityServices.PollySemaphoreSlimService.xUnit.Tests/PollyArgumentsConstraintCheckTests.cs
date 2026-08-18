using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Polly.RateLimiting;
using Polly.Fallback;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Arguments.Check.Tests
{
    public class ArgumentsCheckTests
    {
        // 在你的單元測試專案中加入這條規則
        [Fact]
        public void Polly_Arguments_Should_Not_Be_Persisted()
        {
            var notAllowed = new List<System.Type>
            {
               typeof(OnRetryArguments<object>),
               typeof(OnFallbackArguments<object>),
               typeof(OnCircuitOpenedArguments<object>),
               typeof(OnCircuitClosedArguments<object>),
               typeof(FallbackActionArguments<object>),
               typeof(RateLimiterArguments),
               typeof(OnRateLimiterRejectedArguments),
              
            };
            var architecture = new ArchLoader().LoadAssemblies(typeof(ThreadLevelLockingUtilityServices.PollySemaphoreSlimService<,,>).Assembly).Build();

            // 規則：任何類別都不應擁有類型名稱包含 "Arguments" 且來自 Polly 命名空間的欄位
            IArchRule rule = Classes()
            .Should()
            .NotHaveAnyAttributes(notAllowed)
            .Because("Holding Polly arguments causes memory leaks.");
        }
    }
}