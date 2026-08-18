using System;
using System.Collections.Generic;
using System.Text;

namespace TaskUtilityServices
{
    public interface ITaskUtilityService
    {
        Task<TIn> ToTaskQuickly<TIn>(ValueTask<TIn> valueTask);

        Task<object?> HandleAsyncResult(object? result);
    }
}
