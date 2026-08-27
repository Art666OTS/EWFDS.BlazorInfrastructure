using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EWFDS.BlazorInfrastructure.Services.ErrorHandling;

public class GlobalErrorHandler : IGlobalErrorHandler
{
    private readonly ILogger<GlobalErrorHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalErrorHandler(ILogger<GlobalErrorHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task HandleErrorAsync(Exception exception, string? context = null)
    {
        LogError(exception, context);

        // Add additional handling here (e.g., send to monitoring service, email alerts, etc.)
        await Task.CompletedTask;
    }

    public void LogError(Exception exception, string? context = null)
    {
        var activity = Activity.Current;
        var traceId = activity?.Id ?? Activity.Current?.TraceId.ToString();

        _logger.LogError(
            exception,
            "Unhandled exception occurred. Context: {Context}, TraceId: {TraceId}, Message: {Message}",
            context ?? "Unknown",
            traceId,
            exception.Message
        );
    }
}
