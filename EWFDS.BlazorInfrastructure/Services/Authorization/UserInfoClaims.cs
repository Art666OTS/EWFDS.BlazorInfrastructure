using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace EWFDS.BlazorInfrastructure.Services.Authorization;

/// <summary>
/// Claims transformation that adds default claims to authenticated users.
/// </summary>
public class UserInfoClaims : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!principal.HasClaim(c => c.Type == ClaimTypes.Country))
        {
            ClaimsIdentity id = new ClaimsIdentity();
            id.AddClaim(new Claim(ClaimTypes.Country, "Australia"));
            principal.AddIdentity(id);
        }
        return Task.FromResult(principal);
    }
}
