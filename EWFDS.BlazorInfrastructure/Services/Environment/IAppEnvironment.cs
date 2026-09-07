namespace EWFDS.BlazorInfrastructure.Services.Hosting;

/// <summary>
/// Single source of truth for the running environment of the application.
/// Derived from <see cref="Microsoft.Extensions.Hosting.IHostEnvironment"/> (i.e. the
/// ASPNETCORE_ENVIRONMENT value) so that the environment is never configured
/// independently in more than one place (e.g. legacy ApplicationMode /
/// AzureBlobStorage:TargetEnvironment settings).
/// </summary>
public interface IAppEnvironment
{
    /// <summary>
    /// The environment name (e.g. "Development", "Staging", "Production").
    /// </summary>
    string Name { get; }

    /// <summary>True when running in the Development environment.</summary>
    bool IsDevelopment { get; }

    /// <summary>True when running in the Staging environment.</summary>
    bool IsStaging { get; }

    /// <summary>True when running in the Production environment.</summary>
    bool IsProduction { get; }
}
