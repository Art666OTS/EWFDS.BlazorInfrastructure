using Microsoft.Extensions.Hosting;
using EWFDS.BlazorInfrastructure.Services.Hosting;

namespace EWFDS.BlazorInfrastructure.Common.Hosting;

/// <summary>
/// Default <see cref="IAppEnvironment"/> implementation that resolves the running
/// environment from <see cref="IHostEnvironment"/> (ASPNETCORE_ENVIRONMENT).
/// This keeps the environment as a single source of truth across the application.
/// </summary>
public sealed class AppEnvironment : IAppEnvironment
{
    private readonly IHostEnvironment _environment;

    public AppEnvironment(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <inheritdoc />
    public string Name => _environment.EnvironmentName;

    /// <inheritdoc />
    public bool IsDevelopment => _environment.IsDevelopment();

    /// <inheritdoc />
    public bool IsStaging => _environment.IsStaging();

    /// <inheritdoc />
    public bool IsProduction => _environment.IsProduction();
}
