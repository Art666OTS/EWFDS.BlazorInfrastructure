using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using EWFDS.BlazorInfrastructure.Services.Authorization;
using System.Threading.Tasks;

namespace EWFDS.BlazorInfrastructure.Services.Startup
{
    public static class SharedAppEndpointsExtensions
    {
        /// <summary>
        /// Map endpoints that are shared across applications, such as logout and reporting endpoints.
        /// This method should be called after UseAuthentication() and UseAuthorization() in Program.cs.
        /// </summary>
        public static WebApplication MapSharedAppEndpoints(this WebApplication app)
        {
            // Add additional endpoints required by Logout
            app.MapGet("Account/Logout", async (IUserAuthService SignInManager, [FromQuery] string returnUrl) =>
            {
                // This endpoint is required by the Identity Razor components defined in the /Components/Account/Pages directory of the app projects.
                await SignInManager.LogoutAsync();

                // Validate returnUrl to prevent open redirect attacks
                var safeReturnUrl = string.IsNullOrEmpty(returnUrl) || returnUrl.Contains("://") || returnUrl.StartsWith("//") ? string.Empty : returnUrl;
                return TypedResults.LocalRedirect($"~/{safeReturnUrl}");
            });

            // Map any controllers the app or reporting needs
            app.MapControllers();

            return app;
        }
    }
}
