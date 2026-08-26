using EWFDSBL8BusinessLibrary;
using System.Security.Claims;

namespace EWFDS.BlazorInfrastructure.Services.Identity
{
    /// <summary>
    /// Interface for application user identity information.
    /// Provides access to user properties, authentication status, and role information.
    /// </summary>
    public interface IApplicationUserIdentity
    {
        /// <summary>
        /// Default timeout period in minutes.
        /// </summary>
        const int auiTimeoutPeriod = 20;

        /// <summary>
        /// Gets the authentication type.
        /// </summary>
        string AuthenticationType { get; }

        /// <summary>
        /// Gets the company ID.
        /// </summary>
        int COID { get; }

        /// <summary>
        /// Gets the company information.
        /// </summary>
        CompanyInfo CoInfo { get; }

        /// <summary>
        /// Gets the company name.
        /// </summary>
        string COName { get; }

        /// <summary>
        /// Gets or sets the CSO company ID.
        /// </summary>
        int CSOCompanyID { get; set; }

        /// <summary>
        /// Gets the CSO customer ID.
        /// </summary>
        int CSOCustomerID { get; }

        /// <summary>
        /// Gets the customer information.
        /// </summary>
        CUSTOMERInfo Customer { get; }

        /// <summary>
        /// Gets the customer GUID.
        /// </summary>
        Guid CustomerGUID { get; }

        /// <summary>
        /// Gets whether the user is deleted.
        /// </summary>
        bool Deleted { get; }

        /// <summary>
        /// Gets the user's email address.
        /// </summary>
        string EMail { get; }

        /// <summary>
        /// Gets the user's first name.
        /// </summary>
        string FirstName { get; }

        /// <summary>
        /// Gets the user's full name.
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// Gets whether the user has catalogues.
        /// </summary>
        bool hasCatalogues { get; }

        /// <summary>
        /// Gets whether the user has a company.
        /// </summary>
        bool hasCompany { get; }

        /// <summary>
        /// Gets whether the user has a cost centre.
        /// </summary>
        bool hasCostCentre { get; }

        /// <summary>
        /// Gets the IMMS GUID.
        /// </summary>
        string IMMSGUID { get; }

        /// <summary>
        /// Gets whether the user is authenticated.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Gets whether the user is a super user.
        /// </summary>
        bool IsSuperUser { get; }

        /// <summary>
        /// Gets whether this is a user type (vs other identity types).
        /// </summary>
        bool IsUser { get; }

        /// <summary>
        /// Gets the user's primary key.
        /// </summary>
        int Key { get; }

        /// <summary>
        /// Gets the user's last name.
        /// </summary>
        string LastName { get; }

        /// <summary>
        /// Gets the login GUID.
        /// </summary>
        Guid LoginGUID { get; }

        /// <summary>
        /// Gets the logon ID (username).
        /// </summary>
        string LogonID { get; }

        /// <summary>
        /// Gets the user's name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the password date.
        /// </summary>
        string PasswordDate { get; }

        /// <summary>
        /// Gets the user's phone number.
        /// </summary>
        string Phone { get; }

        /// <summary>
        /// Gets whether the user is suspended.
        /// </summary>
        bool Suspended { get; }

        /// <summary>
        /// Gets the session timeout period in minutes.
        /// </summary>
        int TimeOutPeriod { get; }

        /// <summary>
        /// Gets the user level enum code.
        /// </summary>
        USERLEVELSInfo.LevelCode ule { get; }

        /// <summary>
        /// Gets the user level integer value.
        /// </summary>
        int UserLevel { get; }

        /// <summary>
        /// Gets the user's production process ID.
        /// </summary>
        int UserProductionProcessID { get; }

        /// <summary>
        /// Gets whether the user is WFDS staff.
        /// </summary>
        bool WFDSStaff { get; }

        /// <summary>
        /// Gets the user's claims.
        /// </summary>
        List<Claim> claims { get; }

        /// <summary>
        /// Gets any message associated with the identity.
        /// </summary>
        string message { get; }

        /// <summary>
        /// Gets the user's IP address.
        /// </summary>
        string IPAddress { get; }

        /// <summary>
        /// Checks if the user is in the specified role.
        /// </summary>
        /// <param name="role">The role to check.</param>
        /// <returns>True if the user is in the role.</returns>
        bool IsInRole(string role);

        /// <summary>
        /// Loads the CSO company ID.
        /// </summary>
        /// <param name="cno">The company number.</param>
        void LoadCSOCompanyID(int cno);

        /// <summary>
        /// Loads the CSO customer ID.
        /// </summary>
        /// <param name="cid">The customer ID.</param>
        void LoadCSOCustomerID(int cid);

        /// <summary>
        /// Sets the IMMS GUID.
        /// </summary>
        /// <param name="pguid">The GUID string.</param>
        void SetIMMSGUID(string pguid);

        /// <summary>
        /// Sets the login GUID.
        /// </summary>
        /// <param name="guid">The login GUID.</param>
        void SetLoginGUID(Guid guid);

        /// <summary>
        /// Unloads the customer from the identity.
        /// </summary>
        void UnLoadCustomer();

        /// <summary>
        /// Sets the activity ID.
        /// </summary>
        /// <param name="id">The activity ID.</param>
        void SetActivityID(int id);

        /// <summary>
        /// Repopulates the ApplicationUserIdentity from an activity ID.
        /// </summary>
        /// <param name="actID">The activity ID.</param>
        /// <returns>The populated ApplicationUserIdentity.</returns>
        IApplicationUserIdentity RePopulateAUI(int actID);

        /// <summary>
        /// Gets a claim value by type.
        /// </summary>
        /// <param name="claimType">The claim type.</param>
        /// <returns>The claim value.</returns>
        object GetClaimVaLue(string claimType);
    }
}
