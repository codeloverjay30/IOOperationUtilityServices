using IOOperation.BaseUtilityServices;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.RateLimiting;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.RateLimiting;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLevelLockingUtilityServices
{
    public static class PollyStrategyExtensions
    {
        public static ResiliencePipelineBuilder<TOptions> AddStandardRetryStrategy<TOptions>(
            this ResiliencePipelineBuilder<TOptions> builder ,
            PollyCircuitBreakerExecutionSettings settings ,
            PollyStrategyConfig<TOptions> config,
            Action<double>? internalLogHook = null 
        )
        {
            var predicateBuilder = new PredicateBuilder<TOptions>();
            predicateBuilder = predicateBuilder.AppendExceptionPredicate(config.AdditionalExceptions);
            return builder.AddRetry(new RetryStrategyOptions<TOptions>
            {
                ShouldHandle = predicateBuilder ,
                MaxRetryAttempts = settings.MaxAllowedFailureCount ,
                BackoffType = DelayBackoffType.Exponential ,
                UseJitter = true ,
                OnRetry = async args =>
                {
                    // Hook: Log message
                    internalLogHook?.Invoke(args.RetryDelay.TotalMilliseconds);

                    // Hook: 執行使用者傳入的自定義邏輯
                    if(config.OnRetry != null)
                    {
                        await config.OnRetry(args);
                    }
                }
            });
        }

        public static ResiliencePipelineBuilder<T> AddStandardFallbackStrategy<T>(
            this ResiliencePipelineBuilder<T> builder ,
            PollyCircuitBreakerExecutionSettings settings ,
            PollyStrategyConfig<T> config,
            Action<string , Exception?,CancellationToken?>? internalLogHook = null
        )
        {
            var predicateBuilder = new PredicateBuilder<T>();
            predicateBuilder = predicateBuilder.AppendExceptionPredicate(config.AdditionalExceptions);
            return builder.AddFallback(new FallbackStrategyOptions<T>
            {
                ShouldHandle = predicateBuilder ,
                OnFallback = async args =>
                {
                    // 從 Context 提取 TaskName
                    // 這裡使用的 OperationKey 必須與你 Execute 時放入 Context 的 Key 一致
                    string taskName = "Unknown Task";
                    if(args.Context.Properties.TryGetValue(new ResiliencePropertyKey<OperationModel>("OperationKey") , out var operation))
                    {
                        taskName = operation.TaskName;
                    }

                    // Hook: 列印日誌，將判斷 Exception 的邏輯留在 Service 內部或這裡
                    internalLogHook?.Invoke(taskName , args.Outcome.Exception,args.Context.CancellationToken);
                    // Hook: 使用者傳入的自定義邏輯
                    if(config.OnFallback != null)
                    {
                        await config.OnFallback(args);
                    }
                } ,
                FallbackAction = args =>
                {
                    // 1. 工具包內建的標準行為：例如打 Log (這就是 Utility 的價值)

                    // 2. 執行使用者傳入的自定義邏輯
                    var result = config.FallbackActionCallback?.Invoke(args);
                    return result ?? Outcome.FromResultAsValueTask(default(T));
                }
            });
        }
        public static ResiliencePipelineBuilder<T> AddStandardCircuitBreakerStrategy<T>(
            this ResiliencePipelineBuilder<T> builder ,
            PollyCircuitBreakerExecutionSettings settings ,
            PollyStrategyConfig<T> config
        )
        {
            if(settings.IsEnabled)
            {
                var predicateBuilder = new PredicateBuilder<T>();
                predicateBuilder = predicateBuilder.AppendExceptionPredicate(config.AdditionalExceptions);
                var circuitBreakerStrategyOptions = new CircuitBreakerStrategyOptions<T>
                {
                    ShouldHandle = predicateBuilder ,
                    FailureRatio = 0.5 , // 50% 失敗就斷開
                    SamplingDuration = TimeSpan.FromSeconds(30) , // 只紀錄30 秒內的失敗率
                    MinimumThroughput = 5 , // 最小吞吐量，避免樣本太小造成誤觸斷路
                    BreakDuration = settings.CoolDown , // 斷路持續時間
                    OnOpened = async args =>
                    {
                        if(config.OnCircuitBreakerOpenedCallback != null)
                        {
                        await config.OnCircuitBreakerOpenedCallback(args);
                        }
                    } ,
                    OnClosed = async args =>
                    {
                        if(config.OnCircuitBreakerClosedCallback != null)
                        {
                            await config.OnCircuitBreakerClosedCallback(args);
                        }
                    },
                };

                if(config.ManualControl != null)
                {
                    circuitBreakerStrategyOptions.ManualControl = config.ManualControl;
                }
                builder.AddCircuitBreaker(circuitBreakerStrategyOptions);
            }
            return builder;
        }
        public static ResiliencePipelineBuilder<T> AddStandardRateLimiterStrategy<T>(
            this ResiliencePipelineBuilder<T> builder ,
            PollyCircuitBreakerExecutionSettings settings ,
            PollyStrategyConfig<T> config
        )
        {
            var predicateBuilder = new PredicateBuilder<T>();
            predicateBuilder = predicateBuilder.AppendExceptionPredicate(config.AdditionalExceptions);
            return builder.AddRateLimiter(new RateLimiterStrategyOptions
            {
                OnRejected = async args =>
                {
                    // 1. 工具包內建的標準行為：例如打 Log (這就是 Utility 的價值)
                    // 2. 執行使用者傳入的自定義邏輯
                    if(config.OnRateLimiterRejectedCallback != null)
                    {
                        await config.OnRateLimiterRejectedCallback(args);
                    }
                } ,
                RateLimiter = async args =>
                {
                    if(config.RateLimiterDelegate != null)
                    { 
                        return await config.RateLimiterDelegate(args);
                    }
                    return await ValueTask.FromResult(default(RateLimitLease));
                }
            });
        }
    }
}
