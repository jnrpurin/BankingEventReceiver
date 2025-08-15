using BankingApi.EventReceiver.Interfaces;
using BankingApi.EventReceiver.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BankingApi.EventReceiver;

public class ServiceBusReceiver : IServiceBusReceiver
{
    private readonly ILogger<ServiceBusReceiver> _logger;
    private readonly Queue<EventMessage> _messageQueue = new();
    private readonly Random _random = new();

    public ServiceBusReceiver(ILogger<ServiceBusReceiver> logger)
    {
        _logger = logger;
        SeedTestMessages();
    }

    public async Task<EventMessage?> Peek()
    {
        await Task.Delay(100); // Simulate network delay

        if (_messageQueue.Count == 0)
        {
            // Occasionally add a test message for demonstration
            if (_random.Next(100) < 5) // 5% chance
            {
                SeedTestMessages();
            }
            return null;
        }

        return _messageQueue.Dequeue();
    }

    public async Task Abandon(EventMessage message)
    {
        _logger.LogWarning("Message {MessageId} abandoned", message.Id);
        
        // Simulate putting message back with increased processing count
        message.ProcessingCount++;
        _messageQueue.Enqueue(message);
        
        await Task.CompletedTask;
    }

    public async Task Complete(EventMessage message)
    {
        _logger.LogInformation("Message {MessageId} completed successfully", message.Id);
        await Task.CompletedTask;
    }

    public async Task ReSchedule(EventMessage message, DateTime nextAvailableTime)
    {
        var delay = nextAvailableTime - DateTime.UtcNow;
        _logger.LogInformation("Message {MessageId} rescheduled for {NextAvailableTime} (delay: {Delay})", 
            message.Id, nextAvailableTime, delay);
        
        // In a real implementation, I would schedule the message for future delivery
        // For this mock, we'll just put it back in the queue after the delay
        _ = Task.Run(async () =>
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }
            
            message.ProcessingCount++;
            _messageQueue.Enqueue(message);
        });
        
        await Task.CompletedTask;
    }

    public async Task MoveToDeadLetter(EventMessage message)
    {
        _logger.LogWarning("Message {MessageId} moved to dead letter queue", message.Id);
        // I will move to dead letter queue here
        await Task.CompletedTask;
    }

    private void SeedTestMessages()
    {
        var testAccounts = new[]
        {
            Guid.Parse("7d445724-24ec-4d52-aa7a-ff2bac9f191d"),
            Guid.Parse("3bbaf4ca-5bfa-4922-a395-d755beac475f"),
            Guid.Parse("f8e1a4b2-9c3d-4e5f-8a7b-1d2e3f4a5b6c")
        };

        // Add a few test messages
        for (int i = 0; i < 3; i++)
        {
            var transactionMessage = new TransactionMessage
            {
                Id = Guid.NewGuid(),
                MessageType = _random.Next(2) == 0 ? "Credit" : "Debit",
                BankAccountId = testAccounts[_random.Next(testAccounts.Length)],
                Amount = Math.Round((decimal)(_random.NextDouble() * 200 + 10), 2)
            };

            var eventMessage = new EventMessage
            {
                Id = transactionMessage.Id,
                MessageBody = JsonSerializer.Serialize(transactionMessage),
                ProcessingCount = 0
            };

            _messageQueue.Enqueue(eventMessage);
        }

        _logger.LogInformation("Seeded {Count} test messages", 3);
    }
}