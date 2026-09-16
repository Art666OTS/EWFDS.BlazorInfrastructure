namespace EWFDS.Common.ErrorHandling;

public interface IGlobalErrorHandler
{
    Task HandleErrorAsync(Exception exception, string? context = null);
    void LogError(Exception exception, string? context = null);
}
