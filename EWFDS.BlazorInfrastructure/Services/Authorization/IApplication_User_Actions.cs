using au.com.opttecsol;
using EWFDS.BlazorInfrastructure.Services.Identity;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace EWFDS.BlazorInfrastructure.Services.Authorization
{
    /// <summary>
    /// Interface for application user actions - authorization, validation, and company checks.
    /// Used across all eWFDS applications.
    /// </summary>
    public interface IApplication_User_Actions
    {
        IApplication_User checkAssociatedCompany(IApplication_User au, string GroupBy);
        IApplication_User checkValidCompanyeWFDS(string pguid, string sguid, IApplication_User au);
        IApplication_User HeaderValidation(HttpRequest r, string VersNo, IApplication_User au);
        void LoadRoles(string pguid, IApplication_User _au);
        IApplication_User NoAuthorization(HttpRequest r, IApplication_User au);
        OTSAPIResponse SeedActivity(IPAddress? ipaddr);
    }
}
