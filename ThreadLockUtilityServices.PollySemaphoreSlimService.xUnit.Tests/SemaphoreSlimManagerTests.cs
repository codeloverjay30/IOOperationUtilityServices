using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using ThreadLevelLockingUtilityServices;
using ThreadLevelLockingUtilityServices.Models;

namespace ThreadLockUtilityServices.PollySemaphoreSlimService.xUnit.Tests
{
    public class SemaphoreSlimManagerTests
    {
        [Fact]
        public void IncrementWaiter_ShouldIncreaseCount()
        {
            // Arrange
            var model = new SemaphoreSlimModel { InitialCount = 1 , MaxCount = 1 };
            var manager = new SemaphoreSlimManager(model);

            // Act
            manager.IncrementWaiter();
            manager.IncrementWaiter();

            // Assert
            manager.RealTimeWaitingCount.Should().Be(2);
        }

        [Fact]
        public void GetCurrentActiveCount_ShouldReflectSemaphoreState()
        {
            // Arrange
            var model = new SemaphoreSlimModel { InitialCount = 5 , MaxCount = 5 };
            var manager = new SemaphoreSlimManager(model);

            // Act
            manager.SemaphoreSlimInstance.Wait();
            manager.SemaphoreSlimInstance.Wait();

            // Assert
            manager.GetCurrentActiveCount().Should().Be(2);
        }

        [Fact]
        public void IsAvailable_ShouldBeFalse_WhenThereAreWaiters()
        {
            // Arrange
            var model = new SemaphoreSlimModel { InitialCount = 1 , MaxCount = 1 };
            var manager = new SemaphoreSlimManager(model);

            // Act
            manager.IncrementWaiter();

            // Assert
            manager.IsAvailable.Should().BeFalse();
        }
    }
}
