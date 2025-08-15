using System;
using System.Linq;
using System.Threading.Tasks;
using BankingApi.EventReceiver;
using BankingApi.EventReceiver.Models;
using Microsoft.Extensions.Logging;
using Moq.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BankingEventReceiver.UnitTests.ServiceTests
{
    public class ServiceBusReceiverTests
    {
        private readonly Mock<ILogger<ServiceBusReceiver>> _loggerMock = new();
        private ServiceBusReceiver CreateReceiver() => new ServiceBusReceiver(_loggerMock.Object);

        [Fact]
        public async Task Peek_ReturnsMessage_WhenQueueNotEmpty()
        {
            // Arrange
            var receiver = CreateReceiver();

            // Act
            var message = await receiver.Peek();

            // Assert
            Assert.NotNull(message);
            Assert.IsType<EventMessage>(message);
        }

        [Fact]
        public async Task Peek_ReturnsNull_WhenQueueEmpty()
        {
            // Arrange
            var receiver = CreateReceiver();
            // Dequeue all messages
            while (await receiver.Peek() != null) { }

            // Act & Assert
            var maxAttempts = 10;
            bool gotNull = false;
            for (int i = 0; i < maxAttempts; i++)
            {
                var result = await receiver.Peek();
                if (result == null)
                {
                    gotNull = true;
                    break;
                }
                await Task.Delay(50);
            }
            Assert.True(gotNull, "Expected to eventually get null from Peek when queue is empty.");
        }

        [Fact]
        public async Task Abandon_IncrementsProcessingCount_AndRequeues()
        {
            // Arrange
            var receiver = CreateReceiver();
            var message = await receiver.Peek();
            var originalCount = message!.ProcessingCount;

            // Act
            await receiver.Abandon(message);

            EventMessage? requeued = null;
            var maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(100);
                requeued = await receiver.Peek();
                if (requeued != null && requeued.ProcessingCount == originalCount + 1)
                    break;
            }

            // Assert
            Assert.NotNull(requeued);
            Assert.Equal(originalCount + 1, requeued!.ProcessingCount);
            Assert.Equal(message.Id, requeued.Id);
        }

        [Fact]
        public async Task Complete_LogsInformation()
        {
            // Arrange
            var receiver = CreateReceiver();
            var message = await receiver.Peek();

            // Act
            await receiver.Complete(message!);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task MoveToDeadLetter_LogsWarning()
        {
            // Arrange
            var receiver = CreateReceiver();
            var message = await receiver.Peek();

            // Act
            await receiver.MoveToDeadLetter(message!);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("dead letter queue")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task ReSchedule_EnqueuesMessageWithIncrementedCount()
        {
            // Arrange
            var receiver = CreateReceiver();
            var message = await receiver.Peek();
            var originalCount = message!.ProcessingCount;
            var nextTime = DateTime.UtcNow.AddMilliseconds(100);

            // Act
            await receiver.ReSchedule(message, nextTime);

            EventMessage? requeued = null;
            var maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(100);
                requeued = await receiver.Peek();
                if (requeued != null && requeued.ProcessingCount == originalCount + 1)
                    break;
            }

            // Assert
            Assert.NotNull(requeued);
            Assert.Equal(originalCount + 1, requeued!.ProcessingCount);
            Assert.Equal(message.Id, requeued.Id);
        }
    }
}
