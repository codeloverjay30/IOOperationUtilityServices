using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.RateLimiting;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.RateLimiting;

namespace ThreadLevelLockingUtilityServices.Models
{
    /// <summary>
    /// Configuration used in <see cref="global::Polly"/> strategies. 
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    public class PollyStrategyConfig<TInput>
    {
        /// <summary>
        /// logic when retrying.
        /// </summary>
        /// <remarks>
        /// If it is not specifed or is specifed to be null, 
        /// it will use default behaviour written in <see cref="global::ThreadLevelLockingUtilityServices.PollyStrategyExtensions.AddStandardRetryStrategy{TOptions}(ResiliencePipelineBuilder{TOptions}, PollyCircuitBreakerExecutionSettings, PollyStrategyConfig{TOptions}, Action{double}?)"/>
        /// </remarks>
        public Func<OnRetryArguments<TInput> , ValueTask>? OnRetry { get; set; } = null;

        /// <summary>
        /// logic when fallback occurs.
        /// </summary>
        /// <remarks>
        /// If it is not specifed or is specifed to be null, 
        /// it will use default behaviour written in <see cref="global::ThreadLevelLockingUtilityServices.PollyStrategyExtensions.AddStandardFallbackStrategy{T}(ResiliencePipelineBuilder{T}, PollyCircuitBreakerExecutionSettings, PollyStrategyConfig{T}, Action{string, Exception?, CancellationToken?}?)"/>
        /// </remarks>
        public Func<OnFallbackArguments<TInput> , ValueTask>? OnFallback { get; set; } = null;
        
        /// <summary>
        /// fallback callback
        /// </summary>
        /// <remarks>
        /// If it is not specifed or is specifed to be null, 
        /// it will use default behaviour written in <see cref="global::ThreadLevelLockingUtilityServices.PollyStrategyExtensions.AddStandardFallbackStrategy{TOptions}(ResiliencePipelineBuilder{TOptions}, PollyCircuitBreakerExecutionSettings, PollyStrategyConfig{TOptions}, Action{double}?)"/>
        /// </remarks>
        public Func<FallbackActionArguments<TInput> , ValueTask<Outcome<TInput>>>? FallbackActionCallback { get; set; } = null;

        /// <summary>
        /// callback when the circuit breaker is opened.
        /// </summary>
        /// <remarks>
        /// If it is not specifed or is specifed to be null, 
        /// it will use default behaviour written in <see cref="global::ThreadLevelLockingUtilityServices.PollyStrategyExtensions.AddStandardCircuitBreakerStrategy{T}(ResiliencePipelineBuilder{T}, PollyCircuitBreakerExecutionSettings, PollyStrategyConfig{T})"/>
        /// </remarks>
        public Func<OnCircuitOpenedArguments<TInput> , ValueTask>? OnCircuitBreakerOpenedCallback { get; set; } = null;

        /// <summary>
        /// callback when the circuit breaker is closed.
        /// </summary>
        /// <remarks>
        /// If it is not specifed or is specifed to be null, 
        /// it will use default behaviour written in <see cref="global::ThreadLevelLockingUtilityServices.PollyStrategyExtensions.AddStandardCircuitBreakerStrategy{T}(ResiliencePipelineBuilder{T}, PollyCircuitBreakerExecutionSettings, PollyStrategyConfig{T})"/>
        /// </remarks>
        public Func<OnCircuitClosedArguments<TInput> , ValueTask>? OnCircuitBreakerClosedCallback { get; set; } = null;

        /// <summary>
        /// Control that can manually open or close circuit breaker.
        /// </summary>
        public CircuitBreakerManualControl? ManualControl { get; init; } = null;
        
        /// <summary>
        /// callback when the rate limiter is rejected (i.e. the rate limiter limits the stream.)
        /// </summary>
        /// <remarks>
        /// If it is not specifed or is specifed to be null, 
        /// it will use default behaviour written in <see cref="global::ThreadLevelLockingUtilityServices.PollyStrategyExtensions.AddStandardRateLimiterStrategy{T}(ResiliencePipelineBuilder{T}, PollyCircuitBreakerExecutionSettings, PollyStrategyConfig{T})"/>
        /// </remarks>
        public Func<OnRateLimiterRejectedArguments , ValueTask>? OnRateLimiterRejectedCallback { get; set; } = null;

        /// <summary>
        /// Delegates when the rate limiter is rejected.
        /// </summary>
        /// <remarks>
        /// If it is not specifed or is specifed to be null, 
        /// it will use default behaviour written in <see cref="global::ThreadLevelLockingUtilityServices.PollyStrategyExtensions.AddStandardRateLimiterStrategy{T}(ResiliencePipelineBuilder{T}, PollyCircuitBreakerExecutionSettings, PollyStrategyConfig{T})"/>
        /// </remarks>
        public Func<RateLimiterArguments , ValueTask<RateLimitLease>>? RateLimiterDelegate { get; set; } = null;

        /// <summary>
        /// Additional <see cref="global::System.Exception"/> that should be handled by <see cref="global::Polly"/> 
        /// </summary>
        public IEnumerable<Type> AdditionalExceptions { get; set; } = new List<Type> { typeof(Exception) };
    }
}
