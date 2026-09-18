using BusinessLibrary;
using EWFDS.BlazorInfrastructure.Common.Authentication;
using EWFDS.BlazorInfrastructure.Common.Authorization;
using EWFDS.BlazorInfrastructure.Common.FileSystem;
using EWFDS.BlazorInfrastructure.Common.Identity;
using EWFDS.BlazorInfrastructure.Common.State;
using EWFDS.Common.Email;
using EWFDS.Common.ErrorHandling;
using EWFDS.Common.FileStorage;
using EWFDS.Common.Hosting;
using EWFDSBL8.Library.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EWFDS.Infrastructure.Extensions
{
    /// <summary>
    /// API-safe EWFDS infrastructure service registrations. These services have no dependency
    /// on Blazor/SignalR rendering and can be consumed by pure Web API hosts (e.g. EWFDSAPI8)
    /// as well as Blazor applications.
    /// </summary>
    public static class EwfdsCoreServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the user state service to the service collection. The user state is a
        /// host-agnostic in-memory holder for the current authenticated user and is
        /// consumed by shared services (e.g. UserAuthService).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddUserStateService(this IServiceCollection services)
        {
            services.AddScoped<IUserStateService, UserStateService>();
            return services;
        }

        /// <summary>
        /// Adds the API-safe identity services to the service collection.
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
            services.AddScoped<IFileApiStorageService, AzureBlobStorageService>();
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
            services.AddScoped<IAccessControlValidationService, AccessControlValidationService>();
            return services;
        }

        /// <summary>
        /// Adds the activity logging service to the service collection.
        /// Used across applications to record user activity (e.g. logout).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddActivityService(this IServiceCollection services)
        {
            services.AddScoped<IActivityService, ActivityService>();
            return services;
        }

        /// <summary>
        /// Adds the virtual directory service that resolves virtual paths to physical
        /// disk locations (similar to IIS virtual directories).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddVirtualDirectoryService(this IServiceCollection services)
        {
            services.AddSingleton<IVirtualDirectoryService, VirtualDirectoryService>();
            return services;
        }

        /// <summary>
        /// Adds the API-safe EWFDS core infrastructure services to the service collection.
        /// These services have no dependency on Blazor/SignalR rendering and can be used by
        /// pure Web API hosts (e.g. EWFDSAPI8) as well as Blazor applications.
        /// Includes environment, identity/authorization, activity, error handling, email,
        /// file storage and virtual directory services.
        /// Note: IApplicationConfig must be registered by the consuming application before calling this.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration (used to bind file storage settings).</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEwfdsCoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Phase 0: Environment (single source of truth derived from IHostEnvironment)
            services.AddSingleton<IAppEnvironment, AppEnvironment>();

            // HttpContext access for cookie sign-in/out and client IP resolution
            services.AddHttpContextAccessor();

            // Phase 1: Identity & Authorization
            services.AddUserStateService();
            services.AddIdentityServices();
            services.AddAuthorizationServices();
            services.AddAuthenticationServices();
            services.AddActivityService();

            // Phase 2: Error Handling
            services.AddErrorHandlingService();

            // Phase 3: Email Services
            services.AddEmailService();

            // Phase 4: File Storage (Azure Blob Storage)
            services.AddAzureBlobStorage(configuration);
            services.AddVirtualDirectoryService();

            return services;
        }
    }
}
