namespace BankingApi.EventReceiver.Enums;
public enum EProcessingStatus
{
    Processing,
    Success,
    Failed,
    MovedToDeadLetter,
    Retry
}