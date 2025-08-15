using BankingApi.EventReceiver.Models;

namespace BankingApi.EventReceiver.Interfaces;

public interface ITransactionProcessor
{
    Task<ProcessingResult> ProcessTransactionAsync(EventMessage eventMessage, CancellationToken cancellationToken = default);
}