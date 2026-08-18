using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices
{
    public interface IInProcessThreadLockingService<TKey>
        where TKey : notnull
    {
        Task<TResult> LockAndExecuteAsync<T, TResult>(
            TKey key ,
            Func<T , Task<TResult>> func ,
            T args
        );

        ValueTask<TResult> LockAndExecuteValueAsync<T, TResult>(
            TKey key ,
            Func<T , Task<TResult>> func ,
            T args
        );
    }
}
