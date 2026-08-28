using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;

namespace EWFDS.BlazorInfrastructure.Services.Authorization;

/// <summary>
/// Persists authenticated user state for Blazor interactive rendering.
/// </summary>
public class PersistingAuthenticationStateProvider : ServerAuthenticationStateProvider, IDisposable
{
    private Task<AuthenticationState>? _authenticationStateTask;
    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;
    private readonly IdentityOptions _options;

    public PersistingAuthenticationStateProvider(
        PersistentComponentState persistentComponentState,
        IOptions<IdentityOptions> optionsAccessor)
    {
        _options = optionsAccessor.Value;
        _state = persistentComponentState;
        AuthenticationStateChanged += OnAuthenticationStateChanged;
        _subscription = _state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    private async Task OnPersistingAsync()
    {
        if (_authenticationStateTask is null)
        {
            throw new UnreachableException($"Authentication state not set in {nameof(OnPersistingAsync)}().");
        }

        var authenticationState = await _authenticationStateTask;
        var principal = authenticationState.User;

        if (principal.Identity?.IsAuthenticated == true)
        {
            var userId = string.Empty;
            var sidClaim = principal.FindFirst(ClaimTypes.PrimarySid) ?? principal.FindFirst(ClaimTypes.Sid);
            if (sidClaim != null)
            {
                userId = sidClaim.Value;
            }

            var name = principal.Identity.Name;
            if (userId != null && name != null)
            {
                _state.PersistAsJson(nameof(UserInfo), new UserInfo
                {
                    UserId = userId,
                    Name = name,
                    LoginKey = principal.FindFirst("loginKey")?.Value ?? string.Empty,
                    Act_ID = int.TryParse(principal.FindFirst("Act_ID")?.Value, out var actId) ? actId : 0
                });
            }
        }
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
    {
        _authenticationStateTask = authenticationStateTask;
    }

    public void Dispose()
    {
        AuthenticationStateChanged -= OnAuthenticationStateChanged;
        _subscription.Dispose();
    }
}
