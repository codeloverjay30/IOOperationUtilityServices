using System;
using System.Collections.Generic;
using System.Text;

namespace TaskUtilityServices
{
    public static class TaskExtensions
    {
#if NET10_0_OR_GREATER
        extension(TaskUtilityService service)
        {
            public Task<TIn> ToTaskQuickly<TIn>(ValueTask<TIn> valueTask) => service.ToTaskQuickly<TIn>(valueTask);
        }
#endif
    }
}
