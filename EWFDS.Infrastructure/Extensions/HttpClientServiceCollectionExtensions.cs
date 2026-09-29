using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EWFDS.Infrastructure.Extensions
{
    /// <summary>
    /// API-safe registration for the named HTTP clients shared across EWFDS applications.
    /// These have no dependency on Blazor/SignalR and can be consumed by pure Web API hosts
    /// as well as Blazor applications.
    /// </summary>
    public static class EwfdsHttpClientServiceCollectionExtensions
    {
        /// <summary>
        /// The logical name of the remote eWFDS data API <see cref="System.Net.Http.HttpClient"/>.
        /// Resolve it via <c>IHttpClientFactory.CreateClient(EwfdsHttpClientServiceCollectionExtensions.DataApiClientName)</c>.
        /// </summary>
        public const string DataApiClientName = "eWFDSData";

        /// <summary>
        /// The configuration key holding the base address of the remote eWFDS data API.
        /// </summary>
        public const string DataApiBaseAddressKey = "SystemSettings:RemoteAPI";

        /// <summary>
        /// Registers the named HTTP client for the remote eWFDS data API using the base
        /// address configured under <see cref="DataApiBaseAddressKey"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration (provides the base address).</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when the <see cref="DataApiBaseAddressKey"/> setting is missing or empty.
        /// </exception>
        public static IServiceCollection AddEwfdsDataApiClient(this IServiceCollection services, IConfiguration configuration)
        {
            var baseAddress = configuration[DataApiBaseAddressKey];
            if (string.IsNullOrWhiteSpace(baseAddress))
            {
                throw new System.InvalidOperationException(
                    $"Missing configuration value '{DataApiBaseAddressKey}'. " +
                    $"Set it to the base address of the remote eWFDS data API before calling {nameof(AddEwfdsDataApiClient)}().");
            }

            services.AddHttpClient(DataApiClientName, client =>
            {
                client.BaseAddress = new System.Uri(baseAddress);
            });

            return services;
        }
    }
}
