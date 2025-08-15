using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BankingApi.EventReceiver.Enums;
using BankingApi.EventReceiver.Infra;
using BankingApi.EventReceiver.Models;
using BankingApi.EventReceiver.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BankingEventReceiver.UnitTests.ServiceTests
{
    public class TransactionProcessorTests
    {
        private readonly Mock<ILogger<TransactionProcessor>> _loggerMock;
        private readonly List<ProcessedTransaction> _processedTransactions = new();
        private readonly List<TransactionAuditLog> _auditLogs = new();
        private readonly List<BankAccount> _bankAccounts = new();
        private readonly BankingApiDbContext _dbContext;
        private readonly TransactionProcessor _processor;

        public TransactionProcessorTests()
        {
            var options = new DbContextOptionsBuilder<BankingApiDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _dbContext = new BankingApiDbContext(options);
            _loggerMock = new Mock<ILogger<TransactionProcessor>>();
            _processor = new TransactionProcessor(_dbContext, _loggerMock.Object);
        }

        [Fact]
        public async Task ProcessTransactionAsync_ReturnsSuccess_WhenAlreadyProcessed()
        {
            // Arrange
            var eventMessage = new EventMessage { Id = Guid.NewGuid(), MessageBody = "{}", ProcessingCount = 0 };
            var processed = new ProcessedTransaction { MessageId = eventMessage.Id };
            _dbContext.ProcessedTransactions.Add(processed);
            _dbContext.SaveChanges();

            // Act
            var result = await _processor.ProcessTransactionAsync(eventMessage);

            // Assert
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task ProcessTransactionAsync_ReturnsPermanentFailure_WhenMessageBodyIsNull()
        {
            // Arrange
            var eventMessage = new EventMessage { Id = Guid.NewGuid(), MessageBody = null!, ProcessingCount = 0 };
            _dbContext.ProcessedTransactions.RemoveRange(_dbContext.ProcessedTransactions);
            _dbContext.SaveChanges();

            // Act
            var result = await _processor.ProcessTransactionAsync(eventMessage);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.False(result.IsTransientFailure);            
            Assert.Equal(ProcessingResult.PermanentFailure("Message body is null or empty").GetType(), result.GetType());
        }

        [Fact]
        public async Task ProcessTransactionAsync_ReturnsPermanentFailure_WhenInvalidJson()
        {
            // Arrange
            var eventMessage = new EventMessage { Id = Guid.NewGuid(), MessageBody = "{ invalid json }", ProcessingCount = 0 };
            _dbContext.ProcessedTransactions.RemoveRange(_dbContext.ProcessedTransactions);
            _dbContext.SaveChanges();

            // Act
            var result = await _processor.ProcessTransactionAsync(eventMessage);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.False(result.IsTransientFailure);            
            Assert.Equal(ProcessingResult.PermanentFailure("Invalid JSON format").GetType(), result.GetType());
        }

        [Fact]
        public async Task ProcessTransactionAsync_ReturnsPermanentFailure_WhenInvalidTransactionType()
        {
            // Arrange
            var transaction = new TransactionMessage { MessageType = "InvalidType", Amount = 100, BankAccountId = Guid.NewGuid(), Id = Guid.NewGuid() };
            var eventMessage = new EventMessage { Id = transaction.Id, MessageBody = System.Text.Json.JsonSerializer.Serialize(transaction), ProcessingCount = 0 };
            _dbContext.ProcessedTransactions.RemoveRange(_dbContext.ProcessedTransactions);
            _dbContext.SaveChanges();

            // Act
            var result = await _processor.ProcessTransactionAsync(eventMessage);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.False(result.IsTransientFailure);            
            Assert.Equal(ProcessingResult.PermanentFailure("Invalid transaction type").GetType(), result.GetType());
        }

        [Fact]
        public async Task ProcessTransactionAsync_ReturnsPermanentFailure_WhenInvalidAmount()
        {
            // Arrange
            var transaction = new TransactionMessage { MessageType = "Credit", Amount = -10, BankAccountId = Guid.NewGuid(), Id = Guid.NewGuid() };
            var eventMessage = new EventMessage { Id = transaction.Id, MessageBody = System.Text.Json.JsonSerializer.Serialize(transaction), ProcessingCount = 0 };
            _dbContext.ProcessedTransactions.RemoveRange(_dbContext.ProcessedTransactions);
            _dbContext.SaveChanges();

            // Act
            var result = await _processor.ProcessTransactionAsync(eventMessage);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.False(result.IsTransientFailure);
            Assert.Equal(ProcessingResult.PermanentFailure("Invalid amount").GetType(), result.GetType());
        }
    }
}
