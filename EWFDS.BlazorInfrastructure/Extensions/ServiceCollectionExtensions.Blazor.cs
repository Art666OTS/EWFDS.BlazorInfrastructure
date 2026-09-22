using EWFDS.BlazorInfrastructure.Blazor.Authorization;
using EWFDS.BlazorInfrastructure.Blazor.Circuits;
using EWFDS.BlazorInfrastructure.Blazor.Identity;
using EWFDS.BlazorInfrastructure.Blazor.Theming;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;

namespace EWFDS.BlazorInfrastructure.Extensions
{
    /// <summary>
    /// Blazor-only registrations for EWFDS BlazorInfrastructure. These services depend on
    /// interactive Blazor/SignalR rendering and must only be registered by Blazor applications,
    /// not by pure Web API hosts.
    /// </summary>
    public static partial class ServiceCollectionExtensions
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
        /// Adds the Blazor authentication state provider that persists authenticated
        /// user state for interactive rendering.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddBlazorAuthenticationStateProvider(this IServiceCollection services)
        {
            services.AddScoped<AuthenticationStateProvider, PersistingAuthenticationStateProvider>();
            return services;
        }

        /// <summary>
        /// Adds the SignalR circuit handler that tracks Blazor Server connection state.
        /// Registers the concrete handler and forwards the framework CircuitHandler to the
        /// same scoped instance so components and Blazor share one handler per circuit.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCircuitHandler(this IServiceCollection services)
        {
            services.AddScoped<CircuitHandlerService>();
            services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<CircuitHandlerService>());
            return services;
        }

        /// <summary>
        /// Adds the Blazor-only EWFDS UI infrastructure services to the service collection.
        /// These services depend on interactive Blazor/SignalR rendering and must only be
        /// registered by Blazor applications (e.g. PickPack), not by pure Web API hosts.
        /// Includes theming, user state, the Blazor authentication state provider and the
        /// Blazor Server circuit handler.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEwfdsBlazorUi(this IServiceCollection services)
        {
            // Theming
            services.AddThemeService();

            // Blazor identity navigation helper (depends on NavigationManager)
            services.AddScoped<IdentityRedirectManager>();

            // Blazor authorization helper
            services.AddScoped<UserAuthorised>();

            // Blazor authentication state provider
            services.AddBlazorAuthenticationStateProvider();

            // Blazor Server circuit tracking
            services.AddCircuitHandler();

            return services;
        }
    }
}
