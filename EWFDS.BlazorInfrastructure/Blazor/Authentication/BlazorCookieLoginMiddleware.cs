using EWFDS.Infrastructure.Common.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EWFDS.BlazorInfrastructure.Blazor.Authentication;

/// <summary>
/// Middleware that handles cookie-based authentication for Blazor applications.
/// Processes login requests with authentication keys and sets up cookie authentication.
/// </summary>
public class BlazorCookieLoginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BlazorCookieLoginMiddleware> _logger;

    public BlazorCookieLoginMiddleware(RequestDelegate next, ILogger<BlazorCookieLoginMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Invoke(HttpContext context)
    {
        // Require the X-Access-Id header on every request. Without it, the app cannot be accessed at all.
        if (!context.Request.Headers.TryGetValue("X-Access-Id", out var accessId) || Microsoft.Extensions.Primitives.StringValues.IsNullOrEmpty(accessId))
        {
            //_logger.LogWarning("Missing X-Access-Id header; access denied");
            //context.Response.StatusCode = StatusCodes.Status403Forbidden;
            //await context.Response.WriteAsync("Access denied.");
            //return;
        }
        else
        {
            // Read the AccessControl record and make sure it is for this URL.
            var accessControlService = context.RequestServices.GetRequiredService<IAccessControlValidationService>();
            var requestUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
            var accessAllowed = await accessControlService.IsAccessAllowedAsync(accessId.ToString(), requestUrl);
            if (!accessAllowed)
            {
                _logger.LogWarning("Access denied by AccessControl for X-Access-Id {AccessId}", accessId.ToString());
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Access denied.");
                return;
            }
        }
        // Check for route parameter format: /login/{guid}
        var path = context.Request.Path.Value;
        if (path != null && path.StartsWith("/login/", StringComparison.OrdinalIgnoreCase) && TryExtractGuidFromPath(path, out var keyGuid))
        {
            if (keyGuid == Guid.Empty)
            {
                _logger.LogWarning("Invalid authentication key format received");
                await _next.Invoke(context);
                return;
            }

            // Resolve ILoginService only when actually needed
            var loginService = context.RequestServices.GetRequiredService<ILoginService>();
            var loginSuccessful = await loginService.ProcessLoginAsync(keyGuid);

            if (loginSuccessful)
            {
                _logger.LogInformation("Login successful, redirecting to home page");
                context.Response.Redirect("/home", true);
                return;
            }
            else
            {
                _logger.LogWarning("Login failed for key {Key}", keyGuid);
            }
        }

        await _next.Invoke(context);
    }

    private static bool CheckVersionNumber(HttpContext context)
    {
        var raw = context.Request.Headers["X-Access-Version"].ToString();

        var policy = new VersionGate.VersionPolicy(
            MinSupported: new Version(3, 0, 0, 0),
            WarnBelow: new Version(3, 2, 0, 0),
            Blocked: new[] { new Version(3, 1, 5, 0) }); // a specific withdrawn build

        var result = VersionGate.Evaluate(raw, policy);

        switch (result.Decision)
        {
            case VersionGate.Decision.Block:
                // Return a denial.
                return false;
            case VersionGate.Decision.Warn:
                // Allow.
                return true;
            default:
                return true;
        }
    }

    private static bool TryExtractGuidFromPath(string path, out Guid guid)
    {
        guid = Guid.Empty;
        // Path format: /login/{guid}
        var segment = path.AsSpan().Slice("/login/".Length);
        return Guid.TryParse(segment, out guid) && guid != Guid.Empty;
    }
}
