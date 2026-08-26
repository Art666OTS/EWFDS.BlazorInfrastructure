using EWFDS.BlazorInfrastructure.Services.Identity;

namespace EWFDS.BlazorInfrastructure.Services.Authorization
{
    /// <summary>
    /// Interface for loading and building application user claims.
    /// </summary>
    public interface ILoadApplicationUser
    {
        /// <summary>
        /// Builds claims for the authenticated user.
        /// </summary>
        /// <param name="AUI">The application user identity.</param>
        /// <param name="ACT_ID">The activity ID.</param>
        /// <param name="keyGuid">The login key GUID.</param>
        /// <returns>The updated application user identity with claims.</returns>
        IApplicationUserIdentity BuildClaims(IApplicationUserIdentity AUI, int ACT_ID, Guid keyGuid);

        /// <summary>
        /// Checks database records for the user (suspension, deletion, company status, etc.).
        /// </summary>
        /// <param name="aui">The application user identity to check.</param>
        /// <returns>A tuple indicating success and any error message.</returns>
        (bool OK, string eMsg) CheckDBRecords(IApplicationUserIdentity aui);
    }
}
