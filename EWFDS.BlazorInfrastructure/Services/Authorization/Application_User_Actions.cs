using au.com.opttecsol;
using BL8DataBaseCore;
using BusinessLibrary;
using Csla;
using Csla.Rules;
using EWFDS.BlazorInfrastructure.Services.Identity;
using EWFDSBL8BusinessLibrary;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Data;
using System.Net;
using System.Reflection;
using System.Text;
using WFDSOrdersAPI8.Data.Entity;

namespace EWFDS.BlazorInfrastructure.Services.Authorization
{
    /// <summary>
    /// Application user actions - authorization, validation, and company checks.
    /// Used across all eWFDS applications.
    /// </summary>
    public class Application_User_Actions : CommonFeatures, IApplication_User_Actions
    {
        private const int Status200OK = 200;
        private const int Status500InternalServerError = 500;

        private readonly IConfiguration _config;
        private readonly EWFDSBL8.Library.Shared.Utils.IOTSUtils _OTSUtils;
        private readonly IBLSQL8Functions _blSQL8;
        private readonly ILogger<Application_User_Actions> _logger;

        public Application_User_Actions(
            IConfiguration config, 
            IDataPortalFactory dataPortalFactory, 
            IEmailService emailService, 
            IBLSQL8Functions blSQL8, 
            EWFDSBL8.Library.Shared.Utils.IOTSUtils oTSUtils,
            ILogger<Application_User_Actions> logger) : base(dataPortalFactory, emailService, blSQL8)
        {
            _config = config;
            _OTSUtils = oTSUtils;
            _blSQL8 = blSQL8;
            _logger = logger;
        }

        public OTSAPIResponse SeedActivity(IPAddress? ipaddr)
        {
            string moduleName = "SeedActivity";
            Assembly ass = Assembly.GetExecutingAssembly();
            AssemblyName assName = ass.GetName();
            Version? assVersion = assName.Version;
            string assVers = assVersion?.ToString() ?? "0.0.0.0";
            ACTIVITYEdit ai = _dataPortalFactory.GetPortal<ACTIVITYEdit>().Create();
            ai.BOIsError = false;
            ai.CreatedDateTime = DateTime.Now;
            ai.ActionText = string.Format("{0} {1}", assName.Name, moduleName);
            ai.CreatedByName = assName.Name;
            ai.Source = moduleName;
            ai.Controller = "RequestResponseLogging";
            ai.IP_Address = ipaddr?.ToString() ?? string.Empty;
            ai.State = "OK";
            ai.Extra = assVers;
            ai.LoginKey = Guid.Empty;
            ai.CreatedByID = -1;
            if (ai.IsSavable)
            {
                ai = ai.Save();
                return new OTSAPIResponse("Seed OK", ai.Id, Status200OK);
            }
            else
            {
                List<string> brd = new List<string>();
                foreach (BrokenRule br in ai.BrokenRulesCollection)
                {
                    brd.Add(br.Description);
                }
                return new OTSAPIResponse("Seed Failed", brd, Status500InternalServerError, true);
            }
        }

        public void LoadRoles(string pguid, IApplication_User _au)
        {
            // Role loading implementation
            // Placeholder for role loading logic
        }

        public IApplication_User HeaderValidation(HttpRequest r, string VersNo, IApplication_User au)
        {
            string moduleName = "Headervalidation";
            string pguid = string.Empty;
            string sguid = string.Empty;
            string bData = "Header Check";
            if (r.HasFormContentType)
            {
                IFormCollection form;
                form = r.Form;
                StringBuilder sb = new StringBuilder();
                foreach (var s in form)
                {
                    sb.AppendLine(string.Format("{0}: {1}", s.Key, s.Value));
                }
                bData = sb.ToString();
            }
            var headers = r.Headers;
            #region Stage2 checkHeader
            HVProps hvp = new HVProps();
            if (headers.ContainsKey("Authorization"))
            {
                #region check Authorisation
                hvp = GetHeaderDetails(headers);
                string? ClientSecret = _config["Self:Secret"];
                if (string.IsNullOrEmpty(ClientSecret))
                {
                    au.ApiResponse = BuildResponse("Missing Client Secret", true, "OK");
                    return au;
                }
                else
                {
                    if (ClientSecret.Equals(hvp.sguid))
                    {
                        ACTIVITYEdit ae = _dataPortalFactory.GetPortal<ACTIVITYEdit>().Fetch(au.Act_ID);
                        ae.ActionText = " Login Creation";
                        ae.State = "OK";
                        ae.Source = moduleName;
                        if (ae.IsSavable)
                        {
                            ae = ae.Save();
                        }
                    }
                    else
                    {
                        string ErrMessage = $"Failed Checking System for {hvp.sguid} and Secret Key {ClientSecret} in {moduleName} with Authorization value of {hvp.hValues}";
                        au.ApiResponse = BuildResponse(ErrMessage, true, "OK");
                        return au;
                    }
                }
                #endregion check Authorisation
            }
            else
            {
                #region Unauthorised Reject
                string ErrMessage = $"Failed Checking Message System [{hvp.sguid}] for ]{hvp.pguid}]";
                au.ApiResponse = BuildResponse(ErrMessage, true, "Unauthorised");
                return au;
                #endregion Unauthorised Reject
            }
            #endregion Stage2 checkHeader
            au.sguid = hvp.sguid;
            au.pguid = hvp.pguid;
            au.ApiResponse = BuildResponse("Checking USERS OK", false, "OK");
            return au;
        }

        public IApplication_User NoAuthorization(HttpRequest r, IApplication_User au)
        {
            string moduleName = "NoAuthorization";
            string bData = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            if (r.HasFormContentType)
            {
                IFormCollection form;
                form = r.Form;
                StringBuilder sb = new StringBuilder();
                foreach (var s in form)
                {
                    sb.AppendLine(string.Format("{0}: {1}", s.Key, s.Value));
                }
                bData += string.Format("{0}{1}{2}", bData, System.Environment.NewLine, sb.ToString());
            }
            ACTIVITYEdit ae = _dataPortalFactory.GetPortal<ACTIVITYEdit>().Fetch(au.Act_ID);
            ae.ActionText = "No Authorisation Header";
            ae.State = "Error";
            ae.Source = moduleName;
            ae.IP_Address = r.HttpContext.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
            if (ae.IsSavable)
            {
                ae = ae.Save();
            }
            return au;
        }

        private HVProps GetHeaderDetails(IHeaderDictionary headers)
        {
            HVProps hvp = new HVProps();
            StringValues headervalues = default(StringValues);
            var m = headers.TryGetValue("Authorization", out headervalues);
            var auth = _OTSUtils.Base64Decode(headervalues!);
            try
            {
                hvp.sguid = auth.Split(':')[0];
                hvp.pguid = auth.Split(':')[1];
            }
            catch
            { }
            hvp.hValues = headervalues;
            return hvp;
        }

        public IApplication_User checkValidCompanyeWFDS(string pguid, string sguid, IApplication_User au)
        {
            au.IsError = false;
            Company c = new Company();
            string criteria = string.Format("SecretKey = '{0}'", pguid);
            CompanyeWFDSList PC = _dataPortalFactory.GetPortal<CompanyeWFDSList>().Fetch(criteria);
            if (PC.Count.Equals(0))
            {
                _logger.LogInformation("No Secret Key found {pguid} {sguid}", pguid, sguid);
                au.companyInfo = c;
                au.IsError = true;
                return au;
            }
            else
            {
                if (PC.Count.Equals(1))
                {
                    au.IsError = false;
                    c.ID = PC[0].ID;
                    c.COID = PC[0].COID;
                    c.DefaultCustomer = PC[0].DefaultCustomer;
                    c.DefaultCostCentre = PC[0].DefaultCostCentre;
                    c.CanAccessProducts = PC[0].APIProducts;
                    c.CanAccessSDA = PC[0].APISDA;
                    CompanyInfo co = _dataPortalFactory.GetPortal<CompanyInfo>().Fetch(c.COID);
                    c.CODesc = co.CODesc;
                    au.companyInfo = c;

                    ACTIVITYEdit AE = _dataPortalFactory.GetPortal<ACTIVITYEdit>().Fetch(au.Act_ID);
                    AE.COID = PC[0].COID;
                    AE.CreatedByID = PC[0].DefaultCustomer;
                    AE.ActionText = "checkValidCompanyeWFDS";
                    if (AE.IsSavable)
                    {
                        AE = AE.Save();
                    }
                }
                else
                {
                    _logger.LogInformation("ERROR: Found multiple Secret Key entries for {pguid} {sguid}", pguid, sguid);
                    au.companyInfo = c;
                    au.IsError = true;
                }
                return au;
            }
        }

        public IApplication_User checkAssociatedCompany(IApplication_User au, string GroupBy)
        {
            au.IsError = false;
            au.ErrMessage = string.Empty;
            string ac = string.Format("SELECT CACOID, CAAssocCOID FROM CompanyAssociated WHERE CACOID = {0}", au.companyInfo.COID);
            DataView dv = _blSQL8.ExecuteSQLStringDV(ac);
            if (dv.Table.Rows.Count.Equals(0))
            {
                return au;
            }
            else
            {
                ac = string.Format("SELECT CACOID, CAAssocCOID FROM CompanyAssociated WHERE CACOID = {0} AND CAGroupBy = '{1}'", au.companyInfo.COID, GroupBy);
                dv = _blSQL8.ExecuteSQLStringDV(ac);
                if (dv.Table.Rows.Count.Equals(0))
                {
                    au.IsError = true;
                    CompanyInfo ci = _dataPortalFactory.GetPortal<CompanyInfo>().Fetch(au.companyInfo.COID);
                    string cCode = ci.CODesc;
                    au.ErrMessage = string.Format("No Associated Company with GroupBy of {0} for Company {1} - {2}", GroupBy, au.companyInfo.COID, cCode);
                    return au;
                }
                else
                {
                    if (dv.Table.Rows.Count.Equals(1))
                    {
                        string AssocCOID = dv.Table.Rows[0]["CAAssocCOID"]?.ToString() ?? string.Empty;
                        au.IsError = false;
                        au.ErrMessage = string.Empty;
                        CUSTOMERInfo c = _dataPortalFactory.GetPortal<CUSTOMERInfo>().Fetch(au.companyInfo.DefaultCustomer);
                        ac = string.Format("CustCode = '{0}' AND CustCo = {1}", c.CustCode, AssocCOID);
                        CUSTOMERList CC = _dataPortalFactory.GetPortal<CUSTOMERList>().Fetch(ac);
                        if (CC.Count.Equals(0))
                        {
                            au.IsError = true;
                            au.ErrMessage = string.Format("No Customer in Database with GroupBy of {0} for Company {1} and CustCode {2}", GroupBy, AssocCOID, c.CustCode);
                            return au;
                        }
                        else
                        {
                            if (CC.Count.Equals(1))
                            {
                                au.companyInfo.COID = Convert.ToInt32(AssocCOID);
                                au.companyInfo.DefaultCustomer = CC[0].CustID;
                                ac = string.Format("CCCCustomerID = {0}", CC[0].CustID);
                                CustCostCentreList CCCC = _dataPortalFactory.GetPortal<CustCostCentreList>().Fetch(ac);
                                if (CCCC.Count.Equals(0))
                                {
                                    au.companyInfo.DefaultCostCentre = 0;
                                }
                                else
                                {
                                    au.companyInfo.DefaultCostCentre = CCCC[0].CCCCostKey;
                                }
                                return au;
                            }
                            else
                            {
                                au.ErrMessage = string.Format("More than 1 Customer in Database with GroupBy of {0} for Company {1} and CustCode {2}", GroupBy, AssocCOID, c.CustCode);
                                return au;
                            }
                        }
                    }
                    else
                    {
                        au.IsError = true;
                        au.ErrMessage = string.Format("More than one GroupBy {0} for this Associated Company {1}", GroupBy, au.companyInfo.COID);
                        return au;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Helper class for header validation properties
    /// </summary>
    internal class HVProps
    {
        public string sguid { get; set; } = string.Empty;
        public string pguid { get; set; } = string.Empty;
        public StringValues hValues { get; set; }
    }
}
