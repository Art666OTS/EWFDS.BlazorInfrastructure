using Csla;
using EWFDS.BlazorInfrastructure.Services.Identity;
using EWFDSBL8BusinessLibrary;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EWFDS.BlazorInfrastructure.Services.Authorization;

/// <summary>
/// Interface for token-based authentication.
/// </summary>
public interface ITokenBasedAuthService
{
    Task<LoginResult> LoginWithTokenAsync(Guid loginToken, IHttpContextAccessor context);
    Task SetupCookieAuthorization(IHttpContextAccessor httpContext, IApplicationUserIdentity AUI);
}

/// <summary>
/// Performs token-based login flow and cookie sign-in.
/// </summary>
public class TokenBasedAuthService : ITokenBasedAuthService
{
    private readonly IDataPortalFactory _dataPortalFactory;
    private readonly IActivityTokenValidator _tokenValidator;
    private readonly IApplicationUserIdentity _applicationUserIdentity;
    private readonly IUserAuthService _authService;

    public TokenBasedAuthService(
        IDataPortalFactory dataPortalFactory,
        IActivityTokenValidator tokenValidator,
        IApplicationUserIdentity applicationUserIdentity,
        IUserAuthService authService)
    {
        _dataPortalFactory = dataPortalFactory;
        _tokenValidator = tokenValidator;
        _applicationUserIdentity = applicationUserIdentity;
        _authService = authService;
    }

    public async Task<LoginResult> LoginWithTokenAsync(Guid loginToken, IHttpContextAccessor context)
    {
        // Step 1: Validate the token
        var validationResult = await _tokenValidator.ValidateTokenAsync(loginToken, context.HttpContext?.Connection?.RemoteIpAddress);

        if (!validationResult.IsValid)
        {
            return new LoginResult
            {
                Success = false,
                Message = validationResult.ErrorMessage
            };
        }

        int createdByID = validationResult.CreatedByID;
        CUSTOMERInfo c = await Task.Run(() => _dataPortalFactory.GetPortal<CUSTOMERInfo>().Fetch(createdByID));
        if (c.CustID < 1)
        {
            return new LoginResult
            {
                Success = false,
                Message = $"Invalid token: No customer record found for CreatedByID {createdByID}"
            };
        }

        IApplicationUserIdentity aui = await _authService.CheckIsValidLogin(c.CustCode, c.CustPassword);
        if (aui.IsAuthenticated)
        {
            return new LoginResult
            {
                Success = true,
                Message = "Login successful via token",
                UserIdentity = aui
            };
        }
        else
        {
            return new LoginResult
            {
                Success = false,
                Message = "Login not authenticated via token",
                UserIdentity = aui
            };
        }
    }

    public async Task SetupCookieAuthorization(IHttpContextAccessor httpContext, IApplicationUserIdentity AUI)
    {
        if (httpContext.HttpContext == null)
        {
            throw new InvalidOperationException("HttpContext is not available.");
        }

        var claimsIdentity = new ClaimsIdentity(AUI.claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        await httpContext.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);
    }
}

/// <summary>
/// Result of a login attempt.
/// </summary>
public class LoginResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IApplicationUserIdentity? UserIdentity { get; set; }
}
