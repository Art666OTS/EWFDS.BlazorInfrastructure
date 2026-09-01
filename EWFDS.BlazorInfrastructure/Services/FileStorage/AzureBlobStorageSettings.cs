namespace EWFDS.BlazorInfrastructure.Services.FileStorage;

/// <summary>
/// Configuration settings for Azure Blob Storage.
/// </summary>
public class AzureBlobStorageSettings
{
    /// <summary>
    /// The Azure Storage account name.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Connection string for local development (optional - use Managed Identity in production).
    /// If provided and UseManagedIdentity is false, this will be used for authentication.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Whether to use Azure Managed Identity for authentication.
    /// Defaults to true. Set to false to use ConnectionString instead.
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Override the target environment for container selection.
    /// When set (Development, Staging, or Production), this determines which container to use
    /// regardless of the actual hosting environment. When empty or not set, falls back to
    /// IHostEnvironment.EnvironmentName.
    /// Useful for testing against staging/production containers from a development machine.
    /// </summary>
    public string? TargetEnvironment { get; set; }

    /// <summary>
    /// Container names for each environment.
    /// </summary>
    public EnvironmentContainers Containers { get; set; } = new();

    /// <summary>
    /// SAS token validity duration in hours for download URLs.
    /// Defaults to 1 hour.
    /// </summary>
    public int SasTokenValidityHours { get; set; } = 1;

    /// <summary>
    /// Timeout in seconds for blob operations. Defaults to 60.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Container names per environment.
/// </summary>
public class EnvironmentContainers
{
    public string Development { get; set; } = "ewfds-dev";
    public string Staging { get; set; } = "ewfds-staging";
    public string Production { get; set; } = "ewfds-prod";
}
