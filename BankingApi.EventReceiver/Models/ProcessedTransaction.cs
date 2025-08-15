using BankingApi.EventReceiver.Enums;

namespace BankingApi.EventReceiver.Models;

public class ProcessedTransaction
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid BankAccountId { get; set; }
    public decimal Amount { get; set; }
    public ETransactionType TransactionType { get; set; }
    public DateTime ProcessedAt { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal NewBalance { get; set; }
}
