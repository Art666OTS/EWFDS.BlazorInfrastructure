using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace EWFDS.BlazorInfrastructure.Common.Hosting
{
    public static class SecurityHeadersExtensions
    {
        /// <summary>
        /// Adds a baseline set of security response headers to every response. These are
        /// browser-side, defence-in-depth protections and are independent of any network/gateway
        /// access control in front of the app.
        /// <para>
        /// Headers added (only when not already present, so a fronting gateway can still override):
        /// <list type="bullet">
        /// <item><c>X-Content-Type-Options: nosniff</c> - prevents MIME type sniffing.</item>
        /// <item><c>X-Frame-Options: DENY</c> and <c>Content-Security-Policy: frame-ancestors 'none'</c>
        /// - clickjacking protection. Safe as the strictest value because EWFDS Blazor apps are
        /// navigated as full pages and are never embedded in an iframe.</item>
        /// <item><c>Referrer-Policy: no-referrer</c> - avoids leaking URLs to external sites.</item>
        /// </list>
        /// </para>
        /// Call this early in the pipeline (immediately after <c>UseHttpsRedirection()</c>) so the
        /// headers apply to every response, including error pages.
        /// </summary>
        public static WebApplication UseEwfdsSecurityHeaders(this WebApplication app)
        {
            app.Use(async (HttpContext context, RequestDelegate next) =>
            {
                var headers = context.Response.Headers;

                if (!headers.ContainsKey("X-Content-Type-Options"))
                {
                    headers["X-Content-Type-Options"] = "nosniff";
                }

                if (!headers.ContainsKey("X-Frame-Options"))
                {
                    headers["X-Frame-Options"] = "DENY";
                }

                if (!headers.ContainsKey("Content-Security-Policy"))
                {
                    headers["Content-Security-Policy"] = "frame-ancestors 'none'";
                }

                if (!headers.ContainsKey("Referrer-Policy"))
                {
                    headers["Referrer-Policy"] = "no-referrer";
                }

                await next(context);
            });

            return app;
        }
    }
}
