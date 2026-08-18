using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices.Models
{
    /// <summary>
    /// The configuration about circuit breaker.
    /// </summary>
    public class PollyCircuitBreakerExecutionSettings
    {
        /// <summary>
        /// The time that the circuit breaker will be closed. 
        /// </summary>
        /// <remarks>
        /// It is useful only iff the circuit breaker is in opened state.
        /// </remarks>
        public DateTime OpenUntil { get; set; }

        /// <summary>
        /// The max failure available. 
        /// If the attempts failures exceeds than the max failure available, 
        /// then it will try to open the ciruit breaker.
        /// </summary>
        public int MaxAllowedFailureCount { get; init; } = 3;
        
        /// <summary>
        /// Cool down of circuit breaker.
        /// </summary>
        public TimeSpan CoolDown { get; init; } = TimeSpan.FromSeconds(30);
        
        /// <summary>
        /// Whether to enable the circuit breaker functionality.
        /// If it is specified to be true or not specified, it will open the circuit breaker if needed.
        /// Otherwise, the circuit breaker will be disabled.
        /// </summary>
        public bool IsEnabled { get; init; } = true;

        public PollyCircuitBreakerExecutionSettings() { }
    }
}
