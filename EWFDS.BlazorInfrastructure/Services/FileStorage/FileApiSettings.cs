namespace EWFDS.BlazorInfrastructure.Services.FileStorage;

/// <summary>
/// Configuration for the external File API.
/// Used by FileStorageService implementations across all eWFDS applications.
/// </summary>
public class FileApiSettings
{
    /// <summary>
    /// Base URL of the File API (e.g., "https://localhost:7136").
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Source identifier for this application (used by FileAPI for multi-tenant routing).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Timeout in seconds for HTTP requests. Defaults to 30.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts for failed requests. Defaults to 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds between retry attempts. Defaults to 500ms.
    /// Uses exponential backoff (500ms, 1000ms, 2000ms, etc.)
    /// </summary>
    public int RetryDelayMs { get; set; } = 500;
}
