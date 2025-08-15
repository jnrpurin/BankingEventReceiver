using BankingApi.EventReceiver.Enums;

namespace BankingApi.EventReceiver.Infra;

public class TransactionAuditLog
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid BankAccountId { get; set; }
    public decimal Amount { get; set; }
    public ETransactionType TransactionType { get; set; }
    public EProcessingStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int ProcessingAttempt { get; set; }
    public decimal? PreviousBalance { get; set; }
    public decimal? NewBalance { get; set; }
}