using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices.Models
{
    /// <summary>
    /// A model that wrapps the arguments needed for `SemaphoreSlim` constructor
    /// </summary>
    public class SemaphoreSlimModel
    {
        /// <summary>
        /// Initial count of parallel threads
        /// </summary>
        public required int InitialCount { get; init; }

        /// <summary>
        /// Allowed maximum count of parallel threads at same time
        /// </summary>
        public required int MaxCount { get; init; }
    }
}
