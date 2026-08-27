namespace EWFDS.BlazorInfrastructure.Services.ErrorHandling;

public interface IGlobalErrorHandler
{
    Task HandleErrorAsync(Exception exception, string? context = null);
    void LogError(Exception exception, string? context = null);
}
