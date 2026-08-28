namespace EWFDS.BlazorInfrastructure.Services.Authorization;

/// <summary>
/// User information for authentication state persistence.
/// </summary>
public class UserInfo
{
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required string LoginKey { get; set; }
    public int Act_ID { get; set; }
}
