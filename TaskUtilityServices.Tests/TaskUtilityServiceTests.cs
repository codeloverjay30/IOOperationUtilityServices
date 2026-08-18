using System;
using System.Threading.Tasks;
using Xunit;
using TaskUtilityServices;

namespace TaskUtilityServices.Tests
{
    public class TaskUtilityServiceTests
    {
        private readonly TaskUtilityService _service;

        public TaskUtilityServiceTests()
        {
            _service = new TaskUtilityService();
        }

        #region ToTaskQuickly Tests

        [Fact]
        public async Task ToTaskQuickly_WhenValueTaskIsCompleted_ReturnsCompletedTask()
        {
            // Arrange
            var expectedValue = 42;
            var valueTask = new ValueTask<int>(expectedValue);

            // Act
            var task = _service.ToTaskQuickly(valueTask);

            // Assert
            Assert.True(task.IsCompletedSuccessfully);
            Assert.Equal(expectedValue , await task);
        }

        [Fact]
        public async Task ToTaskQuickly_WhenValueTaskIsNotCompleted_ReturnsAsTask()
        {
            // Arrange
            var expectedValue = "Hello";
            var tcs = new TaskCompletionSource<string>();
            var valueTask = new ValueTask<string>(tcs.Task);

            // Act
            var task = _service.ToTaskQuickly(valueTask);
            tcs.SetResult(expectedValue);
            var result = await task;

            // Assert
            Assert.Equal(expectedValue , result);
        }

        #endregion

        #region HandleAsyncResult Tests

        [Fact]
        public async Task HandleAsyncResult_WithGenericTask_ReturnsResultValue()
        {
            // Arrange
            var expectedValue = "Success";
            var task = Task.FromResult(expectedValue);

            // Act
            var result = await _service.HandleAsyncResult(task);

            // Assert
            Assert.Equal(expectedValue , result);
        }

        [Fact]
        public async Task HandleAsyncResult_WithNonGenericTask_ReturnsNull()
        {
            // Arrange
            var task = Task.CompletedTask;

            // Act
            var result = await _service.HandleAsyncResult(task);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleAsyncResult_WithPlainObject_ReturnsObjectDirectly()
        {
            // Arrange
            var input = new { Id = 1 , Name = "Test" };

            // Act
            var result = await _service.HandleAsyncResult(input);

            // Assert
            Assert.Equal(input , result);
        }

        [Fact]
        public async Task HandleAsyncResult_WithNull_ReturnsNull()
        {
            // Act
            var result = await _service.HandleAsyncResult(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleAsyncResult_WithAsyncGenericTask_UnwrapsCorrectValue()
        {
            // Arrange
            async Task<int> DelayedTask()
            {
                await Task.Delay(10);
                return 100;
            }
            var task = DelayedTask();

            // Act
            var result = await _service.HandleAsyncResult(task);

            // Assert
            Assert.Equal(100 , (int?)result);
        }

        #endregion
    }
}
