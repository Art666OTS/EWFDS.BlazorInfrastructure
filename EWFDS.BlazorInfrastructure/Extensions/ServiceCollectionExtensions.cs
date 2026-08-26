using EWFDS.BlazorInfrastructure.Services.Authorization;
using EWFDS.BlazorInfrastructure.Services.Identity;
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
        /// Adds identity and authorization services to the service collection.
        /// Requires IApplicationConfig to be registered first by the consuming application.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddIdentityServices(this IServiceCollection services)
        {
            services.AddScoped<ILoadApplicationUser, LoadApplicationUser>();
            services.AddScoped<IApplicationUserIdentity, ApplicationUserIdentity>();
            return services;
        }

        /// <summary>
        /// Adds all EWFDS BlazorInfrastructure services to the service collection.
        /// Note: IApplicationConfig must be registered by the consuming application before calling this.
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

            // Phase 3: Identity & Authorization
            services.AddIdentityServices();

            return services;
        }
    }
}
