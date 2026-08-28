using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EWFDS.BlazorInfrastructure.Services.Authentication;

/// <summary>
/// Middleware that handles cookie-based authentication for Blazor applications.
/// Processes login requests with authentication keys and sets up cookie authentication.
/// </summary>
public class BlazorCookieLoginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BlazorCookieLoginMiddleware> _logger;

    public BlazorCookieLoginMiddleware(
        RequestDelegate next,
        ILogger<BlazorCookieLoginMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Invoke(HttpContext context)
    {
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
            var loginSuccessful = await loginService.ProcessLoginAsync(keyGuid, context);

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

    private static bool TryExtractGuidFromPath(string path, out Guid guid)
    {
        guid = Guid.Empty;
        // Path format: /login/{guid}
        var segment = path.AsSpan().Slice("/login/".Length);
        return Guid.TryParse(segment, out guid) && guid != Guid.Empty;
    }
}
