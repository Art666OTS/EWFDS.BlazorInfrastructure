using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EWFDS.BlazorInfrastructure.Blazor.Authentication
{
    /// <summary>
    /// Extension methods for wiring up the shared EWFDS Blazor cookie authentication
    /// scheme and the Blazor cookie login middleware. These are Blazor-UI host concerns
    /// and should only be used by interactive Blazor applications.
    /// </summary>
    public static class BlazorCookieAuthExtensions
    {
        /// <summary>
        /// Adds the shared EWFDS cookie authentication scheme with secure defaults:
        /// HttpOnly, HTTPS-only (<see cref="CookieSecurePolicy.Always"/>),
        /// <see cref="SameSiteMode.Strict"/>, an 8-hour sliding expiration and the
        /// standard <c>/Account/Login</c>, <c>/Account/Logout</c> paths.
        /// Applications may override any of these via the optional <paramref name="configure"/>
        /// callback, which runs after the defaults are applied.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional callback to override the default cookie options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEwfdsBlazorCookieAuth(
            this IServiceCollection services,
            Action<CookieAuthenticationOptions>? configure = null)
        {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    // Security settings
                    // - HttpOnly: Prevents JavaScript access to cookie (XSS protection)
                    // - SecurePolicy.Always: Cookie only sent over HTTPS
                    // - SameSite.Strict: Cookie not sent with cross-site requests (CSRF protection)
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Strict;

                    // Redirect paths
                    options.AccessDeniedPath = new PathString("/Account/Login");
                    options.LoginPath = new PathString("/Account/Login");
                    options.LogoutPath = new PathString("/Account/Logout");

                    // Session expiration: 8 hours (typical work shift). Sliding expiration
                    // renews the cookie when more than half the window has elapsed.
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;

                    // Allow the consuming application to override the defaults.
                    configure?.Invoke(options);
                });

            return services;
        }

        /// <summary>
        /// Adds the <see cref="BlazorCookieLoginMiddleware"/> to the request pipeline.
        /// Should be registered before <c>UseAuthentication()</c> so login requests are
        /// processed and the auth cookie is established.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UseEwfdsBlazorCookieLogin(this IApplicationBuilder app)
        {
            app.UseMiddleware<BlazorCookieLoginMiddleware>();
            return app;
        }
    }
}
