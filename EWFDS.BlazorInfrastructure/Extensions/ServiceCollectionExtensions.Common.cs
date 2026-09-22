using EWFDS.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EWFDS.BlazorInfrastructure.Extensions
{
    /// <summary>
    /// Extension methods for registering EWFDS BlazorInfrastructure services.
    /// The API-safe core services now live in <see cref="EwfdsCoreServiceCollectionExtensions"/>
    /// (project EWFDS.Infrastructure); this partial only provides the Blazor aggregate.
    /// </summary>
    public static partial class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all EWFDS BlazorInfrastructure services to the service collection.
        /// Note: IApplicationConfig must be registered by the consuming application before calling this.
        /// This is a convenience aggregate for Blazor applications that combines
        /// <see cref="EwfdsCoreServiceCollectionExtensions.AddEwfdsCoreInfrastructure"/> (API-safe services) and
        /// <see cref="AddEwfdsBlazorUi"/> (Blazor-only services).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration (used to bind file storage settings).</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEwfdsBlazorInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddEwfdsCoreInfrastructure(configuration);
            services.AddEwfdsBlazorUi();

            return services;
        }
    }
}
