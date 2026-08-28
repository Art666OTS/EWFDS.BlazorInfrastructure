using BusinessLibrary;
using EWFDS.BlazorInfrastructure.Services.Authentication;
using EWFDS.BlazorInfrastructure.Services.Authorization;
using EWFDS.BlazorInfrastructure.Services.Email;
using EWFDS.BlazorInfrastructure.Services.ErrorHandling;
using EWFDS.BlazorInfrastructure.Services.FileStorage;
using EWFDS.BlazorInfrastructure.Services.Identity;
using EWFDS.BlazorInfrastructure.Services.State;
using EWFDS.BlazorInfrastructure.Services.Theming;
using Microsoft.Extensions.Configuration;
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
            services.AddScoped<IdentityRedirectManager>();
            return services;
        }

        /// <summary>
        /// Adds the error handling service to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddErrorHandlingService(this IServiceCollection services)
        {
            services.AddScoped<IGlobalErrorHandler, GlobalErrorHandler>();
            return services;
        }

        /// <summary>
        /// Adds Azure Blob Storage as the file storage provider.
        /// Uses Managed Identity in production and connection string in development.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration containing AzureBlobStorage section.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAzureBlobStorage(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AzureBlobStorageSettings>(configuration.GetSection("AzureBlobStorage"));
            services.AddScoped<Services.FileStorage.IFileApiStorageService, AzureBlobStorageService>();
            return services;
        }

        /// <summary>
        /// Adds shared authorization services to the service collection.
        /// Includes token validation, user authentication, and cookie-based auth.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAuthorizationServices(this IServiceCollection services)
        {
            services.AddScoped<IActivityTokenValidator, ActivityTokenValidator>();
            services.AddScoped<IUserAuthService, UserAuthService>();
            services.AddScoped<ITokenBasedAuthService, TokenBasedAuthService>();
            services.AddScoped<UserAuthorised>();
            return services;
        }

        /// <summary>
        /// Adds the email service to the service collection.
        /// Uses MailGun SMTP for sending emails.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEmailService(this IServiceCollection services)
        {
            services.AddScoped<IEmailService, MailGunEmailService>();
            return services;
        }

        /// <summary>
        /// Adds authentication services to the service collection.
        /// Includes login service for token-based authentication flow.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
        {
            services.AddSingleton<ILoginService, LoginService>();
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
            services.AddAuthorizationServices();
            services.AddAuthenticationServices();

            // Phase 4: Error Handling
            services.AddErrorHandlingService();

            // Phase 5: Email Services
            services.AddEmailService();

            return services;
        }
    }
}
