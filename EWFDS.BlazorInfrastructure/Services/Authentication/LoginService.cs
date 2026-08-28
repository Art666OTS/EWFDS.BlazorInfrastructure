using Csla;
using EWFDS.BlazorInfrastructure.Services.Identity;
using EWFDSBL8BusinessLibrary;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace EWFDS.BlazorInfrastructure.Services.Authentication;

/// <summary>
/// Information about a pending login attempt.
/// </summary>
public class LoginInfo
{
    public string UserName { get; set; } = string.Empty;
    public int ID { get; set; }
}

/// <summary>
/// Service that manages login authentication flow using token-based authentication.
/// Handles login token registration, validation, and cookie-based session setup.
/// </summary>
public class LoginService : ILoginService
{
    private readonly IMemoryCache _loginCache;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LoginService> _logger;

    /// <summary>
    /// Login tokens expire after this duration if not consumed.
    /// Prevents memory accumulation from abandoned login attempts.
    /// </summary>
    private static readonly TimeSpan TokenExpiration = TimeSpan.FromSeconds(60);

    public LoginService(
        IServiceProvider serviceProvider,
        ILogger<LoginService> logger,
        IMemoryCache memoryCache)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loginCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    public void RegisterLoginAttempt(Guid key, string userName, int userId)
    {
        var loginInfo = new LoginInfo
        {
            UserName = userName,
            ID = userId
        };

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TokenExpiration);

        _loginCache.Set(key, loginInfo, cacheOptions);
        _logger.LogInformation("Login attempt registered for user {UserName} with key {Key} (expires in {Seconds}s)", 
            userName, key, TokenExpiration.TotalSeconds);
    }

    public bool RemoveLoginAttempt(Guid key)
    {
        if (_loginCache.TryGetValue(key, out LoginInfo? info))
        {
            _loginCache.Remove(key);
            _logger.LogInformation("Login attempt removed for user {UserName} with key {Key}", info?.UserName, key);
            return true;
        }
        return false;
    }

    public async Task<bool> ProcessLoginAsync(Guid keyGuid, HttpContext httpContext)
    {
        if (keyGuid == Guid.Empty)
        {
            _logger.LogWarning("Invalid login key provided: empty GUID");
            return false;
        }

        // Try to get and remove the login token from cache
        if (!_loginCache.TryGetValue(keyGuid, out LoginInfo? info))
        {
            _logger.LogWarning("Login key not found or expired: {Key}", keyGuid);
            return false;
        }
        _loginCache.Remove(keyGuid);

        try
        {
            // Create a scope to resolve scoped services (IDataPortalFactory and IApplicationUserIdentity)
            using (var scope = _serviceProvider.CreateScope())
            {
                var dataPortalFactory = scope.ServiceProvider.GetRequiredService<IDataPortalFactory>();

                // Fetch activity record for this login key
                // SECURITY NOTE: This string interpolation is safe because keyGuid is a System.Guid type.
                // Guid.ToString() can only produce characters [0-9a-fA-F-], making SQL injection impossible.
                // If this pattern is copied for string inputs, use parameterized queries instead.
                var activities = dataPortalFactory.GetPortal<ACTIVITYList>().Fetch($"LoginKey = '{keyGuid}'");

                if (activities.Count == 0)
                {
                    _logger.LogWarning("No activity found for login key {Key}", keyGuid);
                    return false;
                }

                var appUserIdentity = scope.ServiceProvider.GetRequiredService<IApplicationUserIdentity>();

                // Reload the application user identity
                var userIdentity = appUserIdentity.ReloadAUI(activities[0], httpContext, keyGuid);

                // Setup cookie authentication
                await SetupCookieAuthorizationAsync(httpContext, userIdentity);
            }

            _logger.LogInformation("User {UserName} logged in successfully", info.UserName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing login for key {Key}", keyGuid);
            return false;
        }
    }

    private async Task SetupCookieAuthorizationAsync(HttpContext httpContext, IApplicationUserIdentity userIdentity)
    {
        var claimsIdentity = new ClaimsIdentity(userIdentity.claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);
    }
}
