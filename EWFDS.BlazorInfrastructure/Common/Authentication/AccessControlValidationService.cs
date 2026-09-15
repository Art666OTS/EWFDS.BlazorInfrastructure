using Csla;
using EWFDSBL8BusinessLibrary;
using Microsoft.Extensions.Logging;

namespace EWFDS.BlazorInfrastructure.Common.Authentication;

/// <summary>
/// Default implementation of <see cref="IAccessControlValidationService"/> that reads the
/// AccessControl list via CSLA and validates the token and URL for the current request.
/// </summary>
public class AccessControlValidationService : IAccessControlValidationService
{
    private readonly IDataPortalFactory _dataPortalFactory;
    private readonly ILogger<AccessControlValidationService> _logger;

    public AccessControlValidationService(
        IDataPortalFactory dataPortalFactory,
        ILogger<AccessControlValidationService> logger)
    {
        _dataPortalFactory = dataPortalFactory ?? throw new ArgumentNullException(nameof(dataPortalFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsAccessAllowedAsync(string? accessId, string requestUrl)
    {
        // The access id must be a valid, non-empty GUID token.
        if (string.IsNullOrWhiteSpace(accessId) || !Guid.TryParse(accessId, out var token) || token == Guid.Empty)
        {
            _logger.LogWarning("Access denied: missing or invalid X-Access-Id.");
            return false;
        }

        AccessControlList records;
        try
        {
            // SECURITY NOTE: This string interpolation is safe because token is a System.Guid.
            // Guid.ToString() can only produce characters [0-9a-fA-F-], making SQL injection impossible.
            records = await _dataPortalFactory.GetPortal<AccessControlList>().FetchAsync($"Token = '{token}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading AccessControl records for token {Token}.", token);
            return false;
        }

        if (records.Count == 0)
        {
            _logger.LogWarning("Access denied: no AccessControl record found for token {Token}.", token);
            return false;
        }

        var record = records[0];

        if (!record.Active)
        {
            _logger.LogWarning("Access denied: AccessControl record for token {Token} is inactive.", token);
            return false;
        }

        // The record URL must match the start of the running request URL.
        if (string.IsNullOrWhiteSpace(record.URL) ||
            string.IsNullOrWhiteSpace(requestUrl) ||
            !requestUrl.StartsWith(record.URL, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Access denied: request URL '{RequestUrl}' does not match AccessControl URL '{Url}' for token {Token}.",
                requestUrl, record.URL, token);
            return false;
        }

        return true;
    }
}
