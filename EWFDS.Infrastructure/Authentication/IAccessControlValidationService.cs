namespace EWFDS.Infrastructure.Common.Authentication;

/// <summary>
/// Service that validates an access identifier (supplied via the X-Access-Token header)
/// against the AccessControl records. Used by all applications to gate access based on
/// a token and the requesting URL.
/// </summary>
public interface IAccessControlValidationService
{
    /// <summary>
    /// Determines whether access is allowed for the supplied access id and request URL.
    /// Looks up the AccessControl record by token; access is granted only when a matching,
    /// active record exists and its URL matches the start of the request URL.
    /// </summary>
    /// <param name="accessId">The access identifier from the X-Access-Token header (a GUID token).</param>
    /// <param name="requestUrl">The URL of the running/incoming request.</param>
    /// <returns>True if access is allowed; otherwise false.</returns>
    Task<bool> IsAccessAllowedAsync(string? accessId, string requestUrl);
}
