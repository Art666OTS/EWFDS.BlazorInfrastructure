using Csla;
using EWFDS.BlazorInfrastructure.Services.Identity;
using EWFDSBL8BusinessLibrary;
using System.Net;

namespace EWFDS.BlazorInfrastructure.Services.Authorization;

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
    private const int TOKEN_EXPIRY_HOURS = 4;

    public ActivityTokenValidator(IDataPortalFactory dataPortalFactory)
    {
        _dataPortalFactory = dataPortalFactory;
    }

    public async Task<TokenValidationResult> ValidateTokenAsync(Guid loginToken, IPAddress? ipAddress)
    {
        try
        {
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
