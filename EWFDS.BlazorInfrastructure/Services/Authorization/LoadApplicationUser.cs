using Csla;
using EWFDS.BlazorInfrastructure.Services.Identity;
using EWFDSBL8BusinessLibrary;
using System.Security.Claims;

namespace EWFDS.BlazorInfrastructure.Services.Authorization
{
    /// <summary>
    /// Service for loading and building application user claims.
    /// </summary>
    public class LoadApplicationUser : ILoadApplicationUser
    {
        private readonly IDataPortalFactory _dataPortalFactory;

        public LoadApplicationUser(IDataPortalFactory dataPortalFactory)
        {
            _dataPortalFactory = dataPortalFactory;
        }

        public IApplicationUserIdentity BuildClaims(IApplicationUserIdentity AUI, int ACT_ID, Guid keyGuid)
        {
            // Cast to concrete type for internal property access
            var aui = (ApplicationUserIdentity)AUI;

            var singleClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Sid, aui.Key.ToString()),
                new Claim(ClaimTypes.Name, aui.LoginGUID.ToString()),
                new Claim("ACT_ID", ACT_ID.ToString()),
                new Claim("COID", aui.COID.ToString()),
                new Claim("WFDSStaff", aui.WFDSStaff.ToString()),
                new Claim("ule", aui.ule.ToString()),
                new Claim("UserLevel", aui.UserLevel.ToString()),
                new Claim("IsSuperUser", aui.IsSuperUser.ToString()),
                new Claim("IPAddress", aui.IPAddress ?? string.Empty),
                new Claim("LoginKey", aui.LoginGUID == Guid.Empty ? keyGuid.ToString() : aui.LoginGUID.ToString())
            };

            var claims = singleClaims.Concat(BuildAllRoles(aui.UserLevel)).ToList();
            aui.claims = claims;

            // Check authorization based on roles
            aui.isAuthorised = aui.IsInRole("Pick") || aui.IsInRole("Pack");

            return aui;
        }

        private List<Claim> BuildAllRoles(int userLevel)
        {
            var claims = new List<Claim>();
            switch (userLevel)
            {
                case 60: // Super
                    claims.Add(new Claim(ClaimTypes.Role, "Super"));
                    claims.Add(new Claim(ClaimTypes.Role, "Company User"));
                    claims.Add(new Claim(ClaimTypes.Role, "Account Manager"));
                    claims.Add(new Claim(ClaimTypes.Role, "Warehouse Manager"));
                    claims.Add(new Claim(ClaimTypes.Role, "Pick"));
                    claims.Add(new Claim(ClaimTypes.Role, "Pack"));
                    claims.Add(new Claim(ClaimTypes.Role, "Call Centre"));
                    claims.Add(new Claim(ClaimTypes.Role, "Customer"));
                    return claims;
                case 29: // Company User
                    claims.Add(new Claim(ClaimTypes.Role, "Company User"));
                    claims.Add(new Claim(ClaimTypes.Role, "Customer"));
                    return claims;
                case 27: // Account Manager
                    claims.Add(new Claim(ClaimTypes.Role, "Account Manager"));
                    claims.Add(new Claim(ClaimTypes.Role, "Warehouse Manager"));
                    claims.Add(new Claim(ClaimTypes.Role, "Call Centre"));
                    claims.Add(new Claim(ClaimTypes.Role, "Customer"));
                    claims.Add(new Claim(ClaimTypes.Role, "Pick"));
                    claims.Add(new Claim(ClaimTypes.Role, "Pack"));
                    return claims;
                case 40: // Warehouse Manager
                    claims.Add(new Claim(ClaimTypes.Role, "Warehouse Manager"));
                    claims.Add(new Claim(ClaimTypes.Role, "Customer"));
                    return claims;
                case 45: // Call Centre
                    return claims;
                case 11: // Pick
                    claims.Add(new Claim(ClaimTypes.Role, "Pick"));
                    return claims;
                case 12: // Pack
                    claims.Add(new Claim(ClaimTypes.Role, "Pack"));
                    return claims;
                case 13: // Pick & Pack
                    claims.Add(new Claim(ClaimTypes.Role, "Pick"));
                    claims.Add(new Claim(ClaimTypes.Role, "Pack"));
                    return claims;
                default: // Customer
                    claims.Add(new Claim(ClaimTypes.Role, "Customer"));
                    return claims;
            }
        }

        public (bool OK, string eMsg) CheckDBRecords(IApplicationUserIdentity aui)
        {
            if (aui.WFDSStaff)
            {
                if (!ValidIPAddress(aui.IPAddress, aui.LogonID))
                {
                    return (false, "IP address outside valid range. Contact eWFDS System Administrator");
                }
            }

            if (!CheckForSuspensionOrDeletion(aui))
            {
                return (false, "User suspended or deleted");
            }

            if (aui.WFDSStaff)
            {
                return (true, string.Empty);
            }

            if (!CheckLoginHasCompany(aui))
            {
                return (false, "Company check failed");
            }

            if (!CheckIfCompanyOK(aui))
            {
                return (false, "Company check failed");
            }

            // Check for associated companies
            string ca = string.Format("CACOID = {0} AND NOT(CAGroupBy IS NULL)", aui.COID);
            CompanyAssociatedList CAC = _dataPortalFactory.GetPortal<CompanyAssociatedList>().Fetch(ca);
            // CAFlag logic preserved but not currently used

            if (aui.IsUser)
            {
                return (true, string.Empty);
            }

            return (true, string.Empty);
        }

        private bool CheckForSuspensionOrDeletion(IApplicationUserIdentity thisUser)
        {
            if (thisUser.Customer?.CustSuspendFlag == true || thisUser.Customer?.CustDeleted == true)
            {
                return false;
            }
            return true;
        }

        private bool CheckLoginHasCompany(IApplicationUserIdentity thisUser)
        {
            if (thisUser.COID < 1)
            {
                if (thisUser.IsUser)
                {
                    if (!thisUser.WFDSStaff)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            try
            {
                if (thisUser.CoInfo == null)
                {
                    return thisUser.WFDSStaff;
                }

                CompanyList CC = _dataPortalFactory.GetPortal<CompanyList>().Fetch(
                    string.Format("COID = {0} and CODeleted = 0", thisUser.CoInfo.COID));

                if (CC.Count == 0)
                {
                    return false;
                }

                if (CC.Count > 1)
                {
                    return false;
                }

                if (!thisUser.COID.Equals(CC[0].COID))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private bool CheckIfCompanyOK(IApplicationUserIdentity thisUser)
        {
            try
            {
                if (thisUser.COID > 0)
                {
                    if (thisUser.CoInfo?.CODeleted == true)
                    {
                        return false;
                    }
                }
                else
                {
                    if (!thisUser.WFDSStaff)
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        private bool ValidIPAddress(string? dest, string? un)
        {
            if (string.IsNullOrEmpty(dest))
            {
                return false;
            }

            if (dest.Equals("::1"))
            {
                return true;
            }

            if (dest.StartsWith("192."))
            {
                return true;
            }

            un = un?.ToLower() ?? string.Empty;
            if (un.Equals("suots") || un.Equals("suas") || un.Equals("summ"))
            {
                return true;
            }

            WFDSIPAddressesList IPC = _dataPortalFactory.GetPortal<WFDSIPAddressesList>().Fetch("1 = 1");
            if (IPC.Count == 0)
            {
                return true;
            }

            foreach (WFDSIPAddressesInfo ip in IPC)
            {
                if (ip.IPAddr.Equals(dest))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
