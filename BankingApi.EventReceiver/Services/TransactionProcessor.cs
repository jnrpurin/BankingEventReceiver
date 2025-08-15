using BankingApi.EventReceiver.Enums;
using BankingApi.EventReceiver.Infra;
using BankingApi.EventReceiver.Interfaces;
using BankingApi.EventReceiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BankingApi.EventReceiver.Services;

public class TransactionProcessor : ITransactionProcessor
{
    private readonly BankingApiDbContext _dbContext;
    private readonly ILogger<TransactionProcessor> _logger;

    public TransactionProcessor(BankingApiDbContext dbContext, ILogger<TransactionProcessor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ProcessingResult> ProcessTransactionAsync(EventMessage eventMessage, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["MessageId"] = eventMessage.Id,
            ["ProcessingCount"] = eventMessage.ProcessingCount
        });

        _logger.LogInformation("Starting transaction processing");

        try
        {
            // Check if message was already processed (idempotency)
            var existingTransaction = await _dbContext.ProcessedTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(pt => pt.MessageId == eventMessage.Id, cancellationToken);

            if (existingTransaction != null)
            {
                _logger.LogInformation("Message already processed, skipping");
                return ProcessingResult.Success();
            }

            // Parse message body
            if (string.IsNullOrWhiteSpace(eventMessage.MessageBody))
            {
                var error = "Message body is null or empty";
                _logger.LogError(error);
                await LogAuditAsync(eventMessage, EProcessingStatus.Failed, error);
                return ProcessingResult.PermanentFailure(error);
            }

            TransactionMessage? transactionMessage;
            try
            {
                transactionMessage = JsonSerializer.Deserialize<TransactionMessage>(eventMessage.MessageBody);
                if (transactionMessage == null)
                {
                    var error = "Failed to deserialize message body";
                    _logger.LogError(error);
                    await LogAuditAsync(eventMessage, EProcessingStatus.Failed, error);
                    return ProcessingResult.PermanentFailure(error);
                }
            }
            catch (JsonException ex)
            {
                var error = $"Invalid JSON format: {ex.Message}";
                _logger.LogError(ex, error);
                await LogAuditAsync(eventMessage, EProcessingStatus.Failed, error);
                return ProcessingResult.PermanentFailure(error);
            }

            // Validate transaction type
            if (!Enum.TryParse<ETransactionType>(transactionMessage.MessageType, true, out var transactionType))
            {
                var error = $"Invalid transaction type: {transactionMessage.MessageType}";
                _logger.LogError(error);
                await LogAuditAsync(eventMessage, EProcessingStatus.Failed, error, transactionMessage);
                return ProcessingResult.PermanentFailure(error);
            }

            // Validate amount
            if (transactionMessage.Amount <= 0)
            {
                var error = $"Invalid amount: {transactionMessage.Amount}";
                _logger.LogError(error);
                await LogAuditAsync(eventMessage, EProcessingStatus.Failed, error, transactionMessage);
                return ProcessingResult.PermanentFailure(error);
            }

            // Process transaction with optimistic concurrency control
            var result = await ProcessBankingTransactionAsync(eventMessage, transactionMessage, transactionType, cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var error = "Concurrency conflict occurred";
            _logger.LogWarning(ex, error);
            await LogAuditAsync(eventMessage, EProcessingStatus.Retry, error);
            return ProcessingResult.TransientFailure(error, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("timeout") == true)
        {
            var error = "Database timeout occurred";
            _logger.LogWarning(ex, error);
            await LogAuditAsync(eventMessage, EProcessingStatus.Retry, error);
            return ProcessingResult.TransientFailure(error, ex);
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error during processing: {ex.Message}";
            _logger.LogError(ex, error);
            await LogAuditAsync(eventMessage, EProcessingStatus.Failed, error);
            return ProcessingResult.TransientFailure(error, ex);
        }
    }

    private async Task<ProcessingResult> ProcessBankingTransactionAsync(
        EventMessage eventMessage, 
        TransactionMessage transactionMessage, 
        ETransactionType transactionType,
        CancellationToken cancellationToken)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Find and lock the bank account
            var bankAccount = await _dbContext.BankAccounts
                .FirstOrDefaultAsync(ba => ba.Id == transactionMessage.BankAccountId, cancellationToken);

            if (bankAccount == null)
            {
                var error = $"Bank account not found: {transactionMessage.BankAccountId}";
                _logger.LogError(error);
                await LogAuditAsync(eventMessage, EProcessingStatus.Failed, error, transactionMessage);
                return ProcessingResult.PermanentFailure(error);
            }

            var previousBalance = bankAccount.Balance;
            var newBalance = transactionType == ETransactionType.Credit
                ? previousBalance + transactionMessage.Amount
                : previousBalance - transactionMessage.Amount;

            // Validate sufficient funds for debit
            if (transactionType == ETransactionType.Debit && newBalance < 0)
            {
                var error = $"Insufficient funds. Current balance: {previousBalance}, Debit amount: {transactionMessage.Amount}";
                _logger.LogWarning(error);
                await LogAuditAsync(eventMessage, EProcessingStatus.Failed, error, transactionMessage);
                return ProcessingResult.PermanentFailure(error);
            }

            // Update account balance
            bankAccount.Balance = newBalance;
            _dbContext.BankAccounts.Update(bankAccount);

            // Record processed transaction
            var processedTransaction = new ProcessedTransaction
            {
                Id = Guid.NewGuid(),
                MessageId = eventMessage.Id,
                BankAccountId = transactionMessage.BankAccountId,
                Amount = transactionMessage.Amount,
                TransactionType = transactionType,
                ProcessedAt = DateTime.UtcNow,
                PreviousBalance = previousBalance,
                NewBalance = newBalance
            };

            _dbContext.ProcessedTransactions.Add(processedTransaction);

            // Save changes
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Log successful audit
            await LogAuditAsync(eventMessage, EProcessingStatus.Success, null, transactionMessage, previousBalance, newBalance);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Transaction processed successfully. Account: {AccountId}, Type: {Type}, Amount: {Amount}, Previous Balance: {PreviousBalance}, New Balance: {NewBalance}",
                transactionMessage.BankAccountId, transactionType, transactionMessage.Amount, previousBalance, newBalance);

            return ProcessingResult.Success();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task LogAuditAsync(
        EventMessage eventMessage, 
        EProcessingStatus status, 
        string? errorMessage = null,
        TransactionMessage? transactionMessage = null,
        decimal? previousBalance = null,
        decimal? newBalance = null)
    {
        try
        {
            var auditLog = new TransactionAuditLog
            {
                Id = Guid.NewGuid(),
                MessageId = eventMessage.Id,
                BankAccountId = transactionMessage?.BankAccountId ?? Guid.Empty,
                Amount = transactionMessage?.Amount ?? 0,
                TransactionType = transactionMessage != null && Enum.TryParse<ETransactionType>(transactionMessage.MessageType, true, out var type) 
                    ? type 
                    : ETransactionType.Credit,
                Status = status,
                ProcessedAt = DateTime.UtcNow,
                ErrorMessage = errorMessage,
                ProcessingAttempt = eventMessage.ProcessingCount,
                PreviousBalance = previousBalance,
                NewBalance = newBalance
            };

            _dbContext.TransactionAuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit record");
        }
    }
}