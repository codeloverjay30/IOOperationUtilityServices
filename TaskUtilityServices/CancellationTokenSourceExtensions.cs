namespace TaskUtilityServices
{
    public static class CancellationTokenSourceExtensions
    {
        /// <summary>
        /// 安全地取消並釋放 CancellationTokenSource，防止多執行緒下的 ObjectDisposedException。
        /// </summary>
        public static void SafeCancelAndDispose(this CancellationTokenSource? cts)
        {
            if(cts == null) return;

            try
            {
                // 檢查是否已要求取消，若無則執行 Cancel
                if(!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
            }
            catch(ObjectDisposedException)
            {
                // 在併發環境下，物件可能在檢查後與 Cancel 前被 Dispose，此處可安全忽略
            }
            catch(AggregateException)
            {
                // 如果 Cancel 觸發了某些註冊回呼的異常，可視需求決定是否記錄
            }
            finally
            {
                try
                {
                    cts.Dispose();
                }
                catch(ObjectDisposedException) { /* 忽略重複 Dispose */ }
            }
        }
    }
}
