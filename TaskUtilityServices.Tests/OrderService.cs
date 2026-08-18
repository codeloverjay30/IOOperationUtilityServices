using System;
using System.Collections.Generic;
using System.Text;

namespace TaskUtilityServices.Tests
{
    public class OrderService
    {
        private readonly ITaskUtilityService _taskService;
        public OrderService(ITaskUtilityService taskService) => _taskService = taskService;

        public async Task<string> GetOrderProcessStatusAsync(object rawResult)
        {
            // 呼叫我們想要 Mock 的方法
            var processed = await _taskService.HandleAsyncResult(rawResult);
            return processed?.ToString() ?? "Empty";
        }
    }
}
