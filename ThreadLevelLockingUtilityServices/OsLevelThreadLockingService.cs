using System;
using System.Collections.Generic;
using System.Text;
using TaskUtilityServices;

namespace ThreadLevelLockingUtilityServices
{
    public class OsLevelThreadLockingService(
        ITaskUtilityService taskUtilityService
    )
    : IOsLevelThreadLockingService
    {
        private readonly ITaskUtilityService _taskUtilityService = taskUtilityService;
        /// <summary>
        /// Lock the thread with key <paramref name="globalKey"/> on OS-Level
        /// and then executing an action <paramref name="func"/> with args <paramref name="args"/>
        /// After execution, release the thread.
        /// </summary>
        /// <typeparam name="T">The input type of <paramref name="func"/></typeparam>
        /// <typeparam name="TResult">The type of result of <paramref name="func"/></typeparam>
        /// <param name="globalKey">The key of thread on OS-Level</param>
        /// <param name="func">The action will be executed on the thread with key <paramref name="globalKey"/></param>
        /// <param name="args">The arguments that are passed to <paramref name="func"/></param>
        /// <returns></returns>
        /// <exception cref="TimeoutException"></exception>
        public Task<TResult> LockSystemWideAsync<T, TResult>(
            string globalKey , // 作業系統層級的 Key (建議加上 "Global\" 前綴)
            Func<T , Task<TResult>> func ,
            T args ,
            TimeSpan timeout = default
        )
        {
            var valueTask = LockSystemWideValueAsync(globalKey , func , args , timeout);
            return _taskUtilityService.ToTaskQuickly(valueTask);
        }
        public async ValueTask<TResult> LockSystemWideValueAsync<T, TResult>(
            string globalKey , // 作業系統層級的 Key (建議加上 "Global\" 前綴)
            Func<T , Task<TResult>> func ,
            T args ,
            TimeSpan timeout = default
        )
        {
            if(timeout.Equals(default))
            {
                // 預設Timeout為30秒
                timeout = TimeSpan.FromSeconds(30); 
            }

            // Mutex 不支援非同步等待，所以我們必須在 Task.Run 中封裝
            return await Task.Run(() =>
            {
                // 在名稱前加上 Global\ 可以跨 Session 運作 (需要管理員權限)
                using var mutex = new Mutex(false , $@"Global\{globalKey}");

                bool isAcquired = false; // 追蹤是否成功取得鎖定

                try
                {
                    // 嘗試取得 OS 層級鎖定
                    isAcquired = mutex.WaitOne(timeout);

                    if(!isAcquired)
                    {
                        throw new TimeoutException($"Can't get lock from OS with timeout: {timeout}");
                    }

                    // 重點：使用 GetAwaiter().GetResult() 強迫在當前執行緒同步等待 Task 完成
                    // 這樣可以確保鎖定在 Task 真正完成前不會被釋放，且 Release 發生在同一執行緒
                    return func.Invoke(args).GetAwaiter().GetResult();
                }
                finally
                {
                    // 只有在「確實取得鎖定」的情況下才嘗試釋放
                    if(isAcquired)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            });
        }
    }
}
