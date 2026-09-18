using System;
using System.Threading.Tasks;

namespace EWFDS.BlazorInfrastructure.Common.Authentication;

/// <summary>
/// Service interface for managing login authentication flow.
/// </summary>
public interface ILoginService
{
    /// <summary>
    /// Attempts to process a login using the provided authentication key.
    /// Uses the ambient HttpContext (via IHttpContextAccessor) for cookie sign-in.
    /// </summary>
    /// <param name="keyGuid">The authentication key GUID</param>
    /// <returns>True if login was successful, false otherwise</returns>
    Task<bool> ProcessLoginAsync(Guid keyGuid);

    /// <summary>
    /// Registers a pending login attempt.
    /// </summary>
    /// <param name="key">The unique key for this login attempt</param>
    /// <param name="userName">The username</param>
    /// <param name="userId">The user ID</param>
    void RegisterLoginAttempt(Guid key, string userName, int userId);

    /// <summary>
    /// Removes a pending login attempt.
    /// </summary>
    /// <param name="key">The unique key to remove</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveLoginAttempt(Guid key);
}
