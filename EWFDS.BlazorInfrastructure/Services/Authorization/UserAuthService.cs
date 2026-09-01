using Csla;
using EWFDS.BlazorInfrastructure.Services.Identity;
using EWFDS.BlazorInfrastructure.Services.State;
using EWFDSBL8BusinessLibrary;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace EWFDS.BlazorInfrastructure.Services.Authorization;

/// <summary>
/// Interface for user authentication operations.
/// </summary>
public interface IUserAuthService
{
    Task<IApplicationUserIdentity> CheckIsValidLogin(string username, string password);
    Task LogoutAsync(HttpContext httpContext);
    Task LogoutAsync();
    Task<LoginResult> FindByLoginCodeAsync(string loginCode);
    Task<Guid> GeneratePasswordResetTokenAsync();
    Task<LoginResult> ResetPasswordAsync(string user, string code, string password);
}

/// <summary>
/// Shared login/password/reset/logout workflow.
/// </summary>
public class UserAuthService : IUserAuthService
{
    private readonly IDataPortalFactory _dataPortalFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IApplicationUserIdentity _applicationUserIdentity;
    private readonly IUserStateService _userStateService;

    public UserAuthService(IDataPortalFactory dataPortalFactory, IHttpContextAccessor httpContextAccessor, IApplicationUserIdentity applicationUserIdentity, IUserStateService userStateService)
    {
        _dataPortalFactory = dataPortalFactory;
        _httpContextAccessor = httpContextAccessor;
        _applicationUserIdentity = applicationUserIdentity;
        _userStateService = userStateService;
    }

    public async Task<IApplicationUserIdentity> CheckIsValidLogin(string username, string password)
    {
        await Task.CompletedTask;
        username = username.Replace("'", "");
        password = password.Replace("'", "");
        IApplicationUserIdentity AUI = _applicationUserIdentity.GetIdentityCreateActivity(username, password, _httpContextAccessor.HttpContext);
        return AUI;
    }

    private string FailedLogin(string failureMsg)
    {
        // Update the login count and go back to the login screen.
        return failureMsg;
    }

    public async Task LogoutAsync(HttpContext httpContext)
    {
        if (httpContext != null)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        // Clear the user state
        _userStateService.Clear();
    }

    public async Task LogoutAsync()
    {
        if (_httpContextAccessor.HttpContext != null)
        {
            await _httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        // Clear the user state
        _userStateService.Clear();
    }

    public async Task<LoginResult> FindByLoginCodeAsync(string loginCode)
    {
        await Task.CompletedTask;
        string criteria = $"CustCode = '{loginCode}'";
        CUSTOMERList customerList = _dataPortalFactory.GetPortal<CUSTOMERList>().Fetch(criteria);

        if (customerList.Count == 0)
        {
            return new LoginResult();
        }

        if (customerList.Count > 1)
        {
            return new LoginResult();
        }

        if (string.IsNullOrEmpty(customerList[0].CustPassword))
        {
            return new LoginResult();
        }

        LoginResult lr = new LoginResult();
        // TODO: need to reset the LoginResult here.
        return lr;
    }

    public async Task<LoginResult> ResetPasswordAsync(string user, string code, string password)
    {
        await Task.CompletedTask;
        LoginResult lr = new LoginResult();
        CUSTOMERInfo ci = _dataPortalFactory.GetPortal<CUSTOMERInfo>().Fetch(user);

        if (ci.CustID < 1)
        {
            lr.Success = false;
            lr.Message = "User not found.";
            return lr;
        }

        if (string.IsNullOrEmpty(password))
        {
            lr.Success = false;
            lr.Message = "Password cannot be empty.";
            return lr;
        }

        CUSTOMEREdit customerEdit = _dataPortalFactory.GetPortal<CUSTOMEREdit>().Fetch(user);
        customerEdit.CustPassword = password;

        if (customerEdit.IsSavable)
        {
            customerEdit = customerEdit.Save();
            lr.Success = true;
            lr.Message = "Password reset successful.";
        }
        else
        {
            lr.Success = false;
            lr.Message = "Unable to save password.";
        }

        return lr;
    }

    public async Task<Guid> GeneratePasswordResetTokenAsync()
    {
        await Task.CompletedTask;
        return Guid.NewGuid();
    }
}
