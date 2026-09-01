using Csla;
using Csla.Rules;
using EWFDS.BlazorInfrastructure.Services.Authorization;
using EWFDS.BlazorInfrastructure.Services.Configuration;
using EWFDSBL8BusinessLibrary;
using EWFDSBL8DAL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Reflection;
using System.Security.Claims;
using static EWFDSBL8BusinessLibrary.USERLEVELSInfo;

namespace EWFDS.BlazorInfrastructure.Services.Identity
{
    /// <summary>
    /// Application user identity implementation.
    /// Provides user authentication state, claims, and identity information.
    /// </summary>
    [Serializable]
    public class ApplicationUserIdentity : IdentityUser, IApplicationUserIdentity
    {
        public const int auiTimeoutPeriod = 20;

        [NonSerialized]
        private readonly IDataPortalFactory _dataPortalFactory;

        [NonSerialized]
        private readonly ILoadApplicationUser _loadApplicationUser;

        [NonSerialized]
        private readonly IApplicationConfig _appConfig;

        public ApplicationUserIdentity(IDataPortalFactory dataPortalFactory, ILoadApplicationUser loadApplicationUser, IApplicationConfig appConfig)
        {
            _dataPortalFactory = dataPortalFactory;
            _loadApplicationUser = loadApplicationUser;
            _appConfig = appConfig;
            TimeOutPeriod = auiTimeoutPeriod;
        }

        #region Properties

        // Authentication & Authorization
        public bool IsAuthenticated { get; set; }
        public bool isAuthorised { get; set; }
        public string AuthenticationType => "OTS";

        // User Identity
        public string? Name { get; private set; }
        public string? FirstName { get; private set; }
        public string? LastName { get; private set; }
        public string FullName => $"{FirstName} {LastName}";
        public string? LogonID { get; private set; }
        public int Key { get; private set; }
        public Guid CustomerGUID { get; private set; }

        // Contact Information
        public string? Phone { get; private set; }
        public string? EMail { get; private set; }

        // User Type & Level
        public bool IsUser { get; private set; }
        public bool IsSuperUser { get; private set; }
        public bool WFDSStaff { get; private set; }
        public int UserLevel { get; private set; }
        public LevelCode ule { get; private set; } = LevelCode.ULAnonymous_EQU;

        // Company Information
        public int COID { get; private set; }
        public string? COName { get; private set; }
        public CompanyInfo? CoInfo { get; private set; }
        public bool hasCompany { get; private set; }

        // Customer Information
        public CUSTOMERInfo? Customer { get; private set; }
        public int CSOCustomerID { get; private set; }
        public int CSOCompanyID { get; set; }

        // Status Flags
        public bool Deleted { get; private set; }
        public bool Suspended { get; private set; }
        public bool hasCatalogues { get; private set; }
        public bool hasCostCentre { get; private set; }

        // Session & Activity
        public int ACT_ID { get; set; }
        public Guid LoginGUID { get; set; }
        public string? IPAddress { get; private set; }
        public int TimeOutPeriod { get; private set; }
        public string? PasswordDate { get; private set; }

        // Production & Integration
        public int UserProductionProcessID { get; private set; }
        public string? IMMSGUID { get; private set; }

        // Claims & Messages
        public List<Claim>? claims { get; set; }
        public string? message { get; set; }

        public bool IsInRole(string role) =>
            claims?.Any(c => c.Type == ClaimTypes.Role && string.Equals(c.Value, role, StringComparison.Ordinal)) == true;

        public object? GetClaimVaLue(string claimType) =>
            claims?.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.Ordinal))?.Value;

        #endregion Properties

        #region Factory Methods

        internal ApplicationUserIdentity UnauthenticatedIdentity()
        {
            return new ApplicationUserIdentity(_dataPortalFactory, _loadApplicationUser, _appConfig);
        }

        public IApplicationUserIdentity GetIdentityCreateActivity(string un, string pwd, HttpContext? context)
        {
            if (GetThisUser(un, pwd, context?.Connection?.RemoteIpAddress))
            {
                (bool OK, string eMsg) = _loadApplicationUser.CheckDBRecords(this);
                if (OK)
                {
                    // Create ACTIVITY
                    Assembly ass = Assembly.GetExecutingAssembly();
                    AssemblyName assName = ass.GetName();
                    Version? assVersion = assName.Version;
                    string assVers = assVersion?.ToString() ?? "1.0.0.0";

                    ACTIVITYEdit ai = _dataPortalFactory.GetPortal<ACTIVITYEdit>().Create();
                    ai.BOIsError = false;
                    ai.CreatedDateTime = DateTime.Now;
                    ai.ActionText = _appConfig.SeedActivityText;
                    ai.CreatedByName = _appConfig.ApplicationName;
                    ai.CreatedByID = Customer!.CustID;
                    ai.COID = Customer.CustCo;
                    ai.Source = "LoginAsync";
                    ai.Controller = "Login.razor";
                    ai.State = "OK";
                    ai.Extra = assVers;
                    ai.LoginKey = Guid.NewGuid();
                    ai.IP_Address = context?.Connection?.RemoteIpAddress?.ToString();

                    if (ai.IsSavable)
                    {
                        ai = ai.Save();
                        ACT_ID = ai.Id;
                        LoginGUID = ai.LoginKey;
                        _loadApplicationUser.BuildClaims(this, ai.Id, ai.LoginKey);
                    }
                    else
                    {
                        IsAuthenticated = false;
                        var brd = new List<string>();
                        foreach (BrokenRule br in ai.BrokenRulesCollection)
                        {
                            brd.Add(br.Description);
                        }
                        message = string.Join(Environment.NewLine, brd.ToArray());
                    }
                }
            }
            return this;
        }

        #endregion Factory Methods

        #region Data Access

        private Boolean GetThisUser(string un, string pw, System.Net.IPAddress? ip)
        {
            try
            {
                // Use parameterized query via ColumnNamesCriteria to prevent SQL injection
                var criteria = new CUSTOMERList.ColumnNamesCriteria(un, CUSTOMERDTO.ColumnNames.CUSTCODE);
                CUSTOMERList CC = _dataPortalFactory.GetPortal<CUSTOMERList>().Fetch(criteria);

                if (CC.Count == 0)
                {
                    LoginNotFound("No record in Database");
                    return false;
                }

                // Check case sensitive password match
                if (pw.Equals(CC[0].CustPassword))
                {
                    if (CC[0].CustDeleted)
                    {
                        LoginNotFound("Record is marked as Deleted");
                        return false;
                    }
                    if (CC[0].CustSuspendFlag)
                    {
                        LoginNotFound("Record is marked as Suspended");
                        return false;
                    }
                    // Set values
                    LoginFound(CC[0], ip);
                    return true;
                }
                else
                {
                    LoginNotFound("Invalid UserName/Password combination");
                    return false;
                }
            }
            catch(Exception ex)
            {
                LoginNotFound($"Error fetching user: {ex.Message}");
                return false;
            }
        }

        public bool ReloadCustomer(IApplicationUserIdentity aui, string cono, System.Net.IPAddress? ip)
        {
            // Use parameterized query via ColumnNamesCriteria to prevent SQL injection
            var criteria = new CUSTOMERList.ColumnNamesCriteria(aui.LogonID, CUSTOMERDTO.ColumnNames.CUSTCODE);
            CUSTOMERList CC = _dataPortalFactory.GetPortal<CUSTOMERList>().Fetch(criteria);

            // Filter by company ID in code (safe since cono is compared as parsed int)
            if (!int.TryParse(cono, out int companyId))
            {
                return false;
            }

            CUSTOMERInfo? customer = CC.FirstOrDefault(c => c.CustCo == companyId);
            if (customer == null)
            {
                return false;
            }
            LoginFound(customer, ip);
            return true;
        }

        public IApplicationUserIdentity ReloadAUI(ACTIVITYInfo ai, HttpContext? context, Guid keyGuid)
        {
            CUSTOMERInfo ci = _dataPortalFactory.GetPortal<CUSTOMERInfo>().Fetch(ai.CreatedByID);
            LoginFound(ci, context?.Connection?.RemoteIpAddress);
            IsAuthenticated = true;
            // Build Claims here
            IApplicationUserIdentity aui = _loadApplicationUser.BuildClaims(this, ai.Id, keyGuid);
            return aui;
        }

        public IApplicationUserIdentity RePopulateAUI(int actID)
        {
            ACTIVITYInfo ai = _dataPortalFactory.GetPortal<ACTIVITYInfo>().Fetch(actID);
            CUSTOMERInfo ci = _dataPortalFactory.GetPortal<CUSTOMERInfo>().Fetch(ai.CreatedByID);
            PopulateAUI(actID, ci, ai.IP_Address);
            // Build Claims here
            IApplicationUserIdentity aui = _loadApplicationUser.BuildClaims(this, ai.Id, ai.LoginKey);
            return aui;
        }

        private void LoginFound(CUSTOMERInfo cUSTOMER, System.Net.IPAddress? ip)
        {
            LoginFound(cUSTOMER, ip?.ToString() ?? string.Empty);
        }

        private void LoginFound(CUSTOMERInfo cUSTOMER, string ip)
        {
            Customer = cUSTOMER;
            IsAuthenticated = true;
            hasCompany = !cUSTOMER.CustCo.Equals(0);
            if (hasCompany)
            {
                COID = cUSTOMER.CustCo;
                CoInfo = _dataPortalFactory.GetPortal<CompanyInfo>().Fetch(COID);
            }
            UserLevel = cUSTOMER.CustLevel;
            LogonID = cUSTOMER.CustCode;
            IPAddress = ip ?? string.Empty;
            if (cUSTOMER.CustLevel.Equals(10))
            {
                IsUser = false;
                WFDSStaff = false;
            }
            else
            {
                IsUser = true;
                IsSuperUser = cUSTOMER.CustLevel.Equals(60);
                USERSInfo u = _dataPortalFactory.GetPortal<USERSInfo>().Fetch(cUSTOMER.CustOldUserID);
                WFDSStaff = u.UserWFDSStaff;
            }
        }

        private void PopulateAUI(int Act_ID, CUSTOMERInfo cUSTOMER, string? ip)
        {
            ACT_ID = Act_ID;
            Customer = cUSTOMER;
            IsAuthenticated = true;
            COID = cUSTOMER.CustCo;  // Set COID before using it
            hasCompany = !cUSTOMER.CustCo.Equals(0);
            if (hasCompany)
            {
                CoInfo = _dataPortalFactory.GetPortal<CompanyInfo>().Fetch(COID);
            }
            Key = cUSTOMER.CustID;
            Name = cUSTOMER.CustFullName;
            FirstName = cUSTOMER.CustFirstName;
            LastName = cUSTOMER.CustLastName;
            Phone = cUSTOMER.CustPhone;
            EMail = cUSTOMER.CustEMail;
            PasswordDate = string.Format("{0:dd/MMM/yyyy HH:mm:ss}", cUSTOMER.CustPasswordDate);
            Suspended = cUSTOMER.CustSuspendFlag;
            Deleted = cUSTOMER.CustDeleted;
            CustomerGUID = Guid.TryParse(cUSTOMER.CustGUID, out var guidValue) ? guidValue : Guid.Empty;
            UserLevel = cUSTOMER.CustLevel;
            ule = (LevelCode)UserLevel;  // Set after UserLevel is assigned
            LogonID = cUSTOMER.CustCode;
            IPAddress = ip ?? string.Empty;
            if (cUSTOMER.CustLevel.Equals(10))
            {
                IsUser = false;
                IsSuperUser = false;
                UserProductionProcessID = -1;
                ule = LevelCode.ULCustomer_EQU;
                WFDSStaff = false;
                IMMSGUID = string.Empty;
            }
            else
            {
                IsUser = true;
                IsSuperUser = cUSTOMER.CustLevel.Equals(60);
                USERSInfo u = _dataPortalFactory.GetPortal<USERSInfo>().Fetch(cUSTOMER.CustOldUserID);
                UserProductionProcessID = u.UserProductionProcessID;
                WFDSStaff = u.UserWFDSStaff;
                IMMSGUID = u.UserIMMSGUID;
            }
        }

        private void LoginNotFound(string mess)
        {
            Name = string.Empty;
            IsAuthenticated = false;
            IsUser = false;
            Key = int.MinValue;
            message = mess;
        }

        public void LoadCSOCustomerID(int cid)
        {
            CSOCustomerID = cid;
        }

        public void LoadCSOCompanyID(int cno)
        {
            CSOCompanyID = cno;
        }

        public void UnLoadCustomer()
        {
            Customer = null;
        }

        public void SetIMMSGUID(string pguid)
        {
            IMMSGUID = pguid;
        }

        public void SetLoginGUID(Guid guid)
        {
            LoginGUID = guid;
        }

        public void SetActivityID(int id)
        {
            ACT_ID = id;
        }

        #endregion Data Access

        #region Associated Company Methods

        public string checkAssociatedCompany(IApplicationUserIdentity au, string GroupBy, System.Net.IPAddress? ip)
        {
            string ErrMessage = string.Empty;
            string ac = string.Format("CACOID = {0}", au.COID);
            CompanyAssociatedList CAC = _dataPortalFactory.GetPortal<CompanyAssociatedList>().Fetch(ac);

            if (CAC.Count == 0)
            {
                // Do nothing as only this company
                return ErrMessage;
            }

            // See if you can access GroupBy here. Validate it and reset the person entry in au.
            ac = string.Format("CACOID = {0} AND CAGroupBy = '{1}'", au.COID, GroupBy);
            CAC = _dataPortalFactory.GetPortal<CompanyAssociatedList>().Fetch(ac);

            if (CAC.Count == 0)
            {
                ErrMessage = string.Format("No Associated Company with GroupBy of {0} for Company {1} - {2}", 
                    GroupBy, au.COID, _dataPortalFactory.GetPortal<CompanyInfo>().Fetch(au.COID).COCode);
                return ErrMessage;
            }

            if (CAC.Count == 1)
            {
                ErrMessage = string.Empty;
                // Reset the person entry for COID and DefaultCustomer in au
                CompanyeWFDSList CEC = _dataPortalFactory.GetPortal<CompanyeWFDSList>().Fetch(
                    string.Format("COID = {0}", CAC[0].CACOID));
                CUSTOMERInfo c = _dataPortalFactory.GetPortal<CUSTOMERInfo>().Fetch(CEC[0].DefaultCustomer);
                ac = string.Format("CustCode = '{0}' AND CustCo = {1}", c.CustCode, CAC[0].CAAssocCOID);
                CUSTOMERList CC = _dataPortalFactory.GetPortal<CUSTOMERList>().Fetch(ac);

                if (CC.Count == 0)
                {
                    ErrMessage = string.Format("No Customer in Database with GroupBy of {0} for Company {1} and CustCode {2}", 
                        GroupBy, CAC[0].CAAssocCOID, c.CustCode);
                    return ErrMessage;
                }

                if (CC.Count == 1)
                {
                    ReloadCustomer(au, CAC[0].CAAssocCOID.ToString(), ip);
                    return ErrMessage;
                }

                ErrMessage = string.Format("More than 1 Customer in Database with GroupBy of {0} for Company {1} and CustCode {2}", 
                    GroupBy, CAC[0].CAAssocCOID, c.CustCode);
                return ErrMessage;
            }

            ErrMessage = string.Format("More than one GroupBy {0} for this Associated Company {1}", GroupBy, au.COID);
            return ErrMessage;
        }

        #endregion Associated Company Methods
    }
}
