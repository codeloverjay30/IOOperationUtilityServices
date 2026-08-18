namespace TaskUtilityServices
{
    public class TaskUtilityService: ITaskUtilityService
    {
        public Task<TIn> ToTaskQuickly<TIn>(ValueTask<TIn> valueTask)
        {
            if(valueTask.IsCompletedSuccessfully)
            {
                return Task.FromResult(valueTask.Result);
            }

            // 如果需要非同步等待，則轉為標準 Task
            return valueTask.AsTask();
        }

        public async Task<object?> HandleAsyncResult(object? result)
        {
            if(result is Task task)
            {
                await task;
                var taskType = task.GetType();

                // 檢查是否為泛型 Task (Task<T>)，且類型名稱不是特定的 Task 內部類別
                if(taskType.IsGenericType && taskType.GetGenericTypeDefinition() != typeof(Task))
                {
                    var resultProperty = taskType.GetProperty("Result");
                    var value = resultProperty?.GetValue(task);

                    // 再次檢查回傳值是否為 .NET 內部的 Void 標記
                    if(value != null && value.GetType().Name == "VoidTaskResult")
                    {
                        return null;
                    }

                    return value;
                }

                return null; // 處理標準 Task (void)
            }
            return result;
        }
    }
}
