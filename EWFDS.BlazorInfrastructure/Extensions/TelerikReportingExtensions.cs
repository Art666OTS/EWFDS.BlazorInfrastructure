using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Telerik.Reporting.Services.AspNetCore;

namespace EWFDS.BlazorInfrastructure.Extensions
{
    /// <summary>
    /// Blazor-only registration for the Telerik Reporting host, shared across EWFDS
    /// Blazor applications. Telerik Reporting depends on Razor Pages and is only useful
    /// for interactive Blazor apps — it must NOT be registered by pure Web API hosts.
    /// </summary>
    public static class EwfdsTelerikReportingExtensions
    {
        /// <summary>
        /// The default configuration key that Telerik Reporting uses to resolve report
        /// settings (report source resolver, storage, etc.).
        /// </summary>
        public const string DefaultReportHostKey = "ReportHost";

        /// <summary>
        /// Registers the Telerik Reporting host (Razor Pages + Telerik reporting services)
        /// using the supplied reports folder.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="reportsPath">The physical path to the folder containing report definitions.</param>
        /// <param name="reportHostKey">The Telerik report host configuration key (defaults to <see cref="DefaultReportHostKey"/>).</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// This intentionally does NOT call <c>AddControllers()</c>; the consuming app remains
        /// responsible for registering its own controllers.
        /// </remarks>
        public static IServiceCollection AddEwfdsTelerikReporting(
            this IServiceCollection services,
            string reportsPath,
            string reportHostKey = DefaultReportHostKey)
        {
            services.AddRazorPages().AddTelerikReporting(reportHostKey, reportsPath);
            return services;
        }

        /// <summary>
        /// Maps the Telerik Reporting minimal API endpoints. Requires
        /// <see cref="AddEwfdsTelerikReporting(IServiceCollection, string, string)"/> to have
        /// been called during service registration.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication UseEwfdsTelerikReporting(this WebApplication app)
        {
            app.UseTelerikReporting();
            return app;
        }
    }
}
