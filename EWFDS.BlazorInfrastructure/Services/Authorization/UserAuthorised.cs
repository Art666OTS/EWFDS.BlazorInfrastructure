using Microsoft.AspNetCore.Components.Authorization;

namespace EWFDS.BlazorInfrastructure.Services.Authorization;

/// <summary>
/// Simple utility class for checking user authorization state.
/// </summary>
public class UserAuthorised
{
    public async Task<string> GetUserAuth(AuthenticationStateProvider authStateProvider)
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity is not null && user.Identity.IsAuthenticated)
        {
            var loginKeyClaim = user.Claims.FirstOrDefault(c => c.Type == "LoginKey");
            return $"{loginKeyClaim?.Value ?? "Not found"} is authenticated.";
        }
        else
        {
            return "The user is NOT authenticated.";
        }
    }
}
