using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices.Models
{
    public class CircuitBreakerExecutionSettings
    {
        public long OpenUntilTicksField = 0;

        public DateTime OpenUntil
        {
            get => new DateTime(Volatile.Read(ref OpenUntilTicksField));
            set => Volatile.Write(ref OpenUntilTicksField , value.Ticks);
        }

        public int ContinuousFailureCount = 0;
        public int MaxAllowedFailureCount { get; init; } = 3;
        public TimeSpan CoolDown { get; init; } = TimeSpan.FromSeconds(30);

        public CircuitBreakerExecutionSettings() { }
    }
}
