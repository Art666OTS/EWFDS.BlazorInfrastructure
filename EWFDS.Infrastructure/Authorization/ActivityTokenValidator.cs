using Csla;
using EWFDS.Infrastructure.Common.Identity;
using EWFDSBL8BusinessLibrary;
using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace EWFDS.Infrastructure.Common.Authorization;

/// <summary>
/// Interface for validating activity tokens.
/// </summary>
public interface IActivityTokenValidator
{
    Task<TokenValidationResult> ValidateTokenAsync(Guid loginToken, IPAddress? ipAddress);
}

/// <summary>
/// Validates login tokens against ACTIVITY records.
/// </summary>
public class ActivityTokenValidator : IActivityTokenValidator
{
    private readonly IDataPortalFactory _dataPortalFactory;
    private readonly IMemoryCache _consumedTokenCache;
    private const int TOKEN_EXPIRY_HOURS = 4;

    // Prefix for cache entries that mark a transfer token as already redeemed (single-use).
    private const string ConsumedTokenCacheKeyPrefix = "ConsumedTransferToken_";

    public ActivityTokenValidator(IDataPortalFactory dataPortalFactory, IMemoryCache consumedTokenCache)
    {
        _dataPortalFactory = dataPortalFactory;
        _consumedTokenCache = consumedTokenCache;
    }

    public async Task<TokenValidationResult> ValidateTokenAsync(Guid loginToken, IPAddress? ipAddress)
    {
        try
        {
            // Single-use enforcement: reject a token that has already been redeemed on this app.
            // This prevents the same transfer link (e.g. left in browser history or logs) from
            // being replayed to log in again after it has been used once.
            string consumedTokenKey = ConsumedTokenCacheKeyPrefix + loginToken;
            if (_consumedTokenCache.TryGetValue(consumedTokenKey, out _))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token already used: this single-use login link has already been redeemed"
                };
            }

            // Fetch ACTIVITY records with the specified LoginKey
            string criteria = $"LoginKey = '{loginToken}'";
            ACTIVITYList activityList = await Task.Run(() =>
                _dataPortalFactory.GetPortal<ACTIVITYList>().Fetch(criteria));

            if (activityList == null || activityList.Count == 0)
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid token: No activity record found"
                };
            }

            // Get the first (most recent) activity record
            var activity = activityList[0];

            // Check if CreationDateTime is older than 4 hours
            TimeSpan timeSinceCreation = DateTime.Now - activity.CreatedDateTime;
            if (timeSinceCreation.TotalHours > TOKEN_EXPIRY_HOURS)
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Token expired: Created {timeSinceCreation.TotalHours:F1} hours ago (maximum {TOKEN_EXPIRY_HOURS} hours)"
                };
            }

            // Check the IP address
            if (ipAddress != null && !activity.IP_Address.Equals(ipAddress.ToString()))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Invalid IP address: Token {activity.IP_Address} actual {ipAddress}"
                };
            }

            // Check that this Token is not logged out
            if (activity.ActionText.Equals("Logged Out"))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token is Logged Out"
                };
            }

            // Single-use: mark this token as redeemed so the same link cannot be replayed.
            // The entry is kept for the token's validity window so it self-evicts once the
            // token would have expired anyway.
            _consumedTokenCache.Set(
                consumedTokenKey,
                true,
                new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(TOKEN_EXPIRY_HOURS)));

            return new TokenValidationResult
            {
                IsValid = true,
                UserName = activity.CreatedByName,
                CreationDateTime = activity.CreatedDateTime,
                ActivityId = activity.Id,
                COID = activity.COID,
                CreatedByID = activity.CreatedByID
            };
        }
        catch (Exception ex)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Token validation failed: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// Result of token validation.
/// </summary>
public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime CreationDateTime { get; set; }
    public int ActivityId { get; set; }
    public int COID { get; set; }
    public int CreatedByID { get; set; }
    public IApplicationUserIdentity? UserIdentity { get; set; }
}
