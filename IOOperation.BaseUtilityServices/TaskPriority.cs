using System;
using System.Collections.Generic;
using System.Text;

namespace Tasks.Model
{
    /// <summary>
    /// Priority of tasks
    /// </summary>
    public enum TaskPriority
    {
        /// <summary>
        /// E.g. Collecting the log and cleaning it
        /// </summary>
        Low = 0,

        /// <summary>
        /// E.g. Testing use case.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// E.g. Invocation of critical API or external command (about OS-Level etc) 
        /// </summary>
        High = 2
    }
}
