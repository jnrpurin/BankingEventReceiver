using BankingApi.EventReceiver.Interfaces;
using BankingApi.EventReceiver.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankingApi.EventReceiver.Services;

public class MessageWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageWorkerService> _logger;
    private readonly IServiceBusReceiver _serviceBusReceiver;
    private readonly int[] _retryDelays = { 5, 25, 125 };
    private readonly int _pollIntervalSeconds;

    public MessageWorkerService(
        IServiceProvider serviceProvider,
        IServiceBusReceiver serviceBusReceiver,
        ILogger<MessageWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _serviceBusReceiver = serviceBusReceiver;
        _logger = logger;

        _pollIntervalSeconds = int.TryParse(Environment.GetEnvironmentVariable("WORKER_POLL_INTERVAL_SECONDS"), out var interval) ? interval : 10;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessageWorkerService starting with poll interval of {PollInterval} seconds", _pollIntervalSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessMessagesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("MessageWorkerService cancellation requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in message processing loop");
                    await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MessageWorkerService stopped");
        }
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var message = await _serviceBusReceiver.Peek();

        if (message == null)
        {
            _logger.LogDebug("No messages available, waiting {PollInterval} seconds", _pollIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), cancellationToken);
            return;
        }

        var correlationId = Guid.NewGuid().ToString();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["MessageId"] = message.Id,
            ["ProcessingCount"] = message.ProcessingCount
        });

        _logger.LogInformation("Processing message");

        using var serviceScope = _serviceProvider.CreateScope();
        var transactionProcessor = serviceScope.ServiceProvider.GetRequiredService<ITransactionProcessor>();

        try
        {
            var result = await transactionProcessor.ProcessTransactionAsync(message, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Message processed successfully, completing message");
                await _serviceBusReceiver.Complete(message);
            }
            else if (result.IsTransientFailure)
            {
                await HandleTransientFailure(message, result);
            }
            else
            {
                _logger.LogError("Permanent failure processing message: {ErrorMessage}", result.ErrorMessage);
                await _serviceBusReceiver.MoveToDeadLetter(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message");
            await HandleTransientFailure(message, ProcessingResult.TransientFailure("Unexpected processing error", ex));
        }
    }

    private async Task HandleTransientFailure(EventMessage message, ProcessingResult result)
    {
        var processingCount = message.ProcessingCount;
        var maxRetries = _retryDelays.Length;

        if (processingCount >= maxRetries)
        {
            _logger.LogWarning("Message exceeded max retry attempts ({MaxRetries}), moving to dead letter. Error: {ErrorMessage}", 
                maxRetries, result.ErrorMessage);
            await _serviceBusReceiver.MoveToDeadLetter(message);
            return;
        }

        var delaySeconds = _retryDelays[Math.Min(processingCount, _retryDelays.Length - 1)];
        var nextAvailableTime = DateTime.UtcNow.AddSeconds(delaySeconds);

        _logger.LogWarning("Transient failure (attempt {ProcessingCount}/{MaxRetries}), scheduling retry in {DelaySeconds} seconds. Error: {ErrorMessage}",
            processingCount + 1, maxRetries, delaySeconds, result.ErrorMessage);

        await _serviceBusReceiver.ReSchedule(message, nextAvailableTime);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MessageWorkerService stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("MessageWorkerService stopped");
    }
}