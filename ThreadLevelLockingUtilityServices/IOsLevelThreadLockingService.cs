using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices
{
    public interface IOsLevelThreadLockingService
    {
        Task<TResult> LockSystemWideAsync<T, TResult>(
            string globalKey , // 作業系統層級的 Key (建議加上 "Global\" 前綴)
            Func<T , Task<TResult>> func ,
            T args ,
            TimeSpan timeout = default
        );

        ValueTask<TResult> LockSystemWideValueAsync<T, TResult>(
            string globalKey , // 作業系統層級的 Key (建議加上 "Global\" 前綴)
            Func<T , Task<TResult>> func ,
            T args ,
            TimeSpan timeout = default
        );
    }
}
