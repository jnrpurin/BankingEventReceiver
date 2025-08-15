using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BankingApi.EventReceiver.Interfaces;
using BankingApi.EventReceiver.Models;
using BankingApi.EventReceiver.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BankingEventReceiver.UnitTests.ServiceTests
{
    public class MessageWorkerServiceTests
    {
        private readonly Mock<IServiceProvider> _serviceProviderMock = new();
        private readonly Mock<IServiceBusReceiver> _serviceBusReceiverMock = new();
        private readonly Mock<ILogger<MessageWorkerService>> _loggerMock = new();
        private readonly Mock<IServiceScope> _serviceScopeMock = new();
        private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
        private readonly Mock<ITransactionProcessor> _transactionProcessorMock = new();

        public MessageWorkerServiceTests()
        {
            _serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(_scopeFactoryMock.Object);
            _scopeFactoryMock.Setup(x => x.CreateScope())
                .Returns(_serviceScopeMock.Object);
            _serviceScopeMock.Setup(x => x.ServiceProvider)
                .Returns(_serviceProviderMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(ITransactionProcessor)))
                .Returns(_transactionProcessorMock.Object);
        }

        [Fact]
        public async Task ProcessMessagesAsync_NoMessage_LogsDebugAndWaits()
        {
            // Arrange
            _serviceBusReceiverMock.Setup(x => x.Peek())
                .ReturnsAsync((EventMessage?)null);
            var service = new MessageWorkerService(_serviceProviderMock.Object, _serviceBusReceiverMock.Object, _loggerMock.Object);
            var token = new CancellationTokenSource(100).Token;

            // Act
            try
            {
                var task = (Task)service.GetType()
                    .GetMethod("ProcessMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(service, new object[] { token })!;
                await task;
            }
            catch (TaskCanceledException)
            {
                // CancellationToken known exception, ignore it
            }

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No messages available")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task ProcessMessagesAsync_SuccessfulProcessing_CompletesMessage()
        {
            // Arrange
            var message = new EventMessage { Id = Guid.NewGuid(), ProcessingCount = 0 };
            _serviceBusReceiverMock.Setup(x => x.Peek()).ReturnsAsync(message);
            _transactionProcessorMock.Setup(x => x.ProcessTransactionAsync(message, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ProcessingResult.Success());
            _serviceBusReceiverMock.Setup(x => x.Complete(message)).Returns(Task.CompletedTask);
            var service = new MessageWorkerService(_serviceProviderMock.Object, _serviceBusReceiverMock.Object, _loggerMock.Object);
            var token = new CancellationTokenSource(100).Token;

            // Act
            try
            {
                var task = (Task)service.GetType()
                    .GetMethod("ProcessMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(service, new object[] { token })!;
                await task;
            }
            catch (TaskCanceledException)
            {
                // Known exception because the CancellationToken
            }

            // Assert
            _serviceBusReceiverMock.Verify(x => x.Complete(message), Times.Once);
        }

        [Fact]
        public async Task ProcessMessagesAsync_TransientFailure_ReschedulesMessage()
        {
            // Arrange
            var message = new EventMessage { Id = Guid.NewGuid(), ProcessingCount = 0 };
            _serviceBusReceiverMock.Setup(x => x.Peek()).ReturnsAsync(message);
            _transactionProcessorMock.Setup(x => x.ProcessTransactionAsync(message, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ProcessingResult.TransientFailure("transient error"));
            _serviceBusReceiverMock.Setup(x => x.ReSchedule(message, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
            var service = new MessageWorkerService(_serviceProviderMock.Object, _serviceBusReceiverMock.Object, _loggerMock.Object);
            var token = new CancellationTokenSource(100).Token;

            // Act
            var task = (Task)service.GetType()
                .GetMethod("ProcessMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(service, new object[] { token })!;
            await task;

            // Assert
            _serviceBusReceiverMock.Verify(x => x.ReSchedule(message, It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task ProcessMessagesAsync_PermanentFailure_MovesToDeadLetter()
        {
            // Arrange
            var message = new EventMessage { Id = Guid.NewGuid(), ProcessingCount = 0 };
            _serviceBusReceiverMock.Setup(x => x.Peek()).ReturnsAsync(message);
            _transactionProcessorMock.Setup(x => x.ProcessTransactionAsync(message, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ProcessingResult.PermanentFailure("permanent error"));
            _serviceBusReceiverMock.Setup(x => x.MoveToDeadLetter(message)).Returns(Task.CompletedTask);
            var service = new MessageWorkerService(_serviceProviderMock.Object, _serviceBusReceiverMock.Object, _loggerMock.Object);
            var token = new CancellationTokenSource(100).Token;

            // Act
            var task = (Task)service.GetType()
                .GetMethod("ProcessMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(service, new object[] { token })!;
            await task;

            // Assert
            _serviceBusReceiverMock.Verify(x => x.MoveToDeadLetter(message), Times.Once);
        }
    }
}
