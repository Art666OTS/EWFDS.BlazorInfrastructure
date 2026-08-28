using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Primitives;
using WFDSOrdersAPI8.Data.Entity;

namespace au.com.opttecsol
{
    public class Application_User : IdentityUser, IApplication_User
    {
        public Application_User() : base()
        {
            Id = 0;
            IsError = false;
            ErrMessage = String.Empty;
            IsWeb = false;
            LoginKey = Guid.Empty;
            companyInfo = new Company();
            sguid = string.Empty;
            pguid = string.Empty;
            IsValid = true;
            user_level = 0;
            roles = new List<string>();
            Act_ID = 0;
            ApiResponse = new OTSAPIResponse();
            IP_Address = string.Empty;
        }
        public Boolean IsError { get; set; }
        public String ErrMessage { get; set; }
        public Boolean IsWeb { get; set; }
        public long Id { get; set; }
        public Boolean IsValid { get; set; }
        public String sguid { get; set; }
        public String pguid { get; set; }
        public Company companyInfo { get; set; }
        public Int32 user_level { get; set; }
        // Thinking here is my type of Roles ie: What can the user do.
        public List<String> roles { get; set; }

        public Int32 Act_ID { get; set; }

        public Guid LoginKey { get; set; }

        public OTSAPIResponse ApiResponse { get; set; }
        public string IP_Address { get; set; } = string.Empty;

    }

    public class HVProps
    {
        public HVProps()
        {
            sguid = string.Empty;
            pguid = string.Empty;
            hValues = default(StringValues);
        }
        public string sguid { get; set; }
        public string pguid { get; set; }
        public StringValues hValues { get; set; }
    }

}
