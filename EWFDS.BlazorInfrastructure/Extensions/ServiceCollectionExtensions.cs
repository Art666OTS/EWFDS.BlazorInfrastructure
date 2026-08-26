using EWFDS.BlazorInfrastructure.Services.State;
using EWFDS.BlazorInfrastructure.Services.Theming;
using Microsoft.Extensions.DependencyInjection;

namespace EWFDS.BlazorInfrastructure.Extensions
{
    /// <summary>
    /// Extension methods for registering EWFDS BlazorInfrastructure services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the theme service to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddThemeService(this IServiceCollection services)
        {
            services.AddScoped<IThemeService, ThemeService>();
            return services;
        }

        /// <summary>
        /// Adds the user state service to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddUserStateService(this IServiceCollection services)
        {
            services.AddScoped<IUserStateService, UserStateService>();
            return services;
        }

        /// <summary>
        /// Adds all EWFDS BlazorInfrastructure services to the service collection.
        /// Call this method to register all shared infrastructure services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEwfdsBlazorInfrastructure(this IServiceCollection services)
        {
            // Phase 1: Theming
            services.AddThemeService();

            // Phase 2: User State
            services.AddUserStateService();

            // Future phases will add:
            // - Additional authorization services
            // - Navigation services
            // - Error handling services

            return services;
        }
    }
}
