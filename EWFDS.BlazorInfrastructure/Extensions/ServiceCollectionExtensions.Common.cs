using BusinessLibrary;
using EWFDS.BlazorInfrastructure.Common.Authentication;
using EWFDS.BlazorInfrastructure.Common.Authorization;
using EWFDS.BlazorInfrastructure.Common.Email;
using EWFDS.BlazorInfrastructure.Common.Hosting;
using EWFDS.BlazorInfrastructure.Services.Hosting;
using EWFDS.BlazorInfrastructure.Common.ErrorHandling;
using EWFDS.BlazorInfrastructure.Common.FileStorage;
using EWFDS.BlazorInfrastructure.Common.FileSystem;
using EWFDS.BlazorInfrastructure.Common.Identity;
using EWFDS.BlazorInfrastructure.Common.State;
using EWFDS.BlazorInfrastructure.Blazor.Identity;
using EWFDSBL8.Library.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EWFDS.BlazorInfrastructure.Extensions
{
    /// <summary>
    /// Extension methods for registering EWFDS BlazorInfrastructure services.
    /// </summary>
    public static partial class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the user state service to the service collection. The user state is a
        /// host-agnostic in-memory holder for the current authenticated user and is
        /// consumed by shared services (e.g. UserAuthService), so it is registered as
        /// part of the API-safe core.
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
            services.AddScoped<Common.FileStorage.IFileApiStorageService, AzureBlobStorageService>();
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

        /// <summary>
        /// Adds all EWFDS BlazorInfrastructure services to the service collection.
        /// Note: IApplicationConfig must be registered by the consuming application before calling this.
        /// This is a convenience aggregate for Blazor applications that combines
        /// <see cref="AddEwfdsCoreInfrastructure"/> (API-safe services) and
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
