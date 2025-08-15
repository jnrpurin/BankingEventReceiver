namespace BankingApi.EventReceiver.Models;
public class ProcessingResult
{
    public bool IsSuccess { get; set; }
    
    public bool IsTransientFailure { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public Exception? Exception { get; set; }

    public static ProcessingResult Success() => new() { IsSuccess = true };
    
    public static ProcessingResult TransientFailure(string errorMessage, Exception? exception = null) => 
        new() { IsSuccess = false, IsTransientFailure = true, ErrorMessage = errorMessage, Exception = exception };
    
    public static ProcessingResult PermanentFailure(string errorMessage, Exception? exception = null) => 
        new() { IsSuccess = false, IsTransientFailure = false, ErrorMessage = errorMessage, Exception = exception };
}