using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace TaskUtilityServices.Tests
{
    public class OrderServiceTests
    {
        [Fact]
        public async Task GetOrderProcessStatusAsync_ShouldReturnMockedValue()
        {
            // 1. Arrange: 建立 Mock 物件
            var mockTaskService = new Mock<ITaskUtilityService>();

            // 設定 HandleAsyncResult 當輸入特定物件時，回傳 Task<object> 結果
            var inputObj = new { Id = 123 };
            var expectedReturn = "Processed_123";

            mockTaskService
                .Setup(s => s.HandleAsyncResult(It.IsAny<object>()))
                .ReturnsAsync(expectedReturn);
            // ReturnsAsync 是 Moq 提供的語法糖，等同於 .Returns(Task.FromResult<object?>(expectedReturn))

            var orderService = new OrderService(mockTaskService.Object);

            // 2. Act
            var result = await orderService.GetOrderProcessStatusAsync(inputObj);

            // 3. Assert
            Assert.Equal(expectedReturn , result);

            // 驗證方法是否真的被呼叫過一次
            mockTaskService.Verify(s => s.HandleAsyncResult(inputObj) , Times.Once);
        }

        [Fact]
        public async Task ToTaskQuickly_MockingValueTask_ReturnTask()
        {
            // Arrange
            var mockTaskService = new Mock<ITaskUtilityService>();
            var valueTask = new ValueTask<int>(99);
            var expectedTask = Task.FromResult(99);

            // Mock 泛型方法
            mockTaskService
                .Setup(s => s.ToTaskQuickly(It.IsAny<ValueTask<int>>()))
                .Returns(expectedTask);

            // Act
            var resultTask = mockTaskService.Object.ToTaskQuickly(valueTask);
            var result = await resultTask;

            // Assert
            Assert.Equal(99 , result);
        }
    }
}
