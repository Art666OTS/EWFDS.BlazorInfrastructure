using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Serilog.Context;

namespace EWFDS.Infrastructure.Common.Hosting
{
    /// <summary>
    /// Pushes the routed controller name into Serilog's LogContext for the duration of the
    /// request. Every log written while the request is in flight - the controller itself and
    /// anything it calls (managers, CSLA data portal, etc.) - is tagged with a "Controller"
    /// property, so it can be routed to that controller's log folder when per-controller logging
    /// is enabled. Requests that do not map to a controller (and work done outside any request,
    /// e.g. startup) carry no such property and fall through to the General log.
    /// </summary>
    public class ControllerLogContextMiddleware
    {
        private readonly RequestDelegate _next;

        public ControllerLogContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            ControllerActionDescriptor? descriptor =
                context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();

            if (descriptor is not null)
            {
                using (LogContext.PushProperty("Controller", descriptor.ControllerName))
                {
                    await _next(context);
                }
            }
            else
            {
                await _next(context);
            }
        }
    }

    public static class ControllerLogContextMiddlewareExtensions
    {
        /// <summary>
        /// Adds <see cref="ControllerLogContextMiddleware"/> to the pipeline. Call after UseRouting
        /// (so the endpoint/controller is selected) and before request-logging middleware.
        /// </summary>
        public static IApplicationBuilder UseControllerLogContext(this IApplicationBuilder app)
            => app.UseMiddleware<ControllerLogContextMiddleware>();
    }
}
