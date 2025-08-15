namespace BankingApi.EventReceiver.Models;

public class EventMessage
{
    public Guid Id { get; set; }
    public string? MessageBody { get; set; }
    public int ProcessingCount { get; set; }
}