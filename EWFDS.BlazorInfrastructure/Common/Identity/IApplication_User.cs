using WFDSOrdersAPI8.Data.Entity;

namespace au.com.opttecsol
{
    public interface IApplication_User
    {
        public Boolean IsError { get; set; }
        public String ErrMessage { get; set; }
        public Boolean IsWeb { get; set; }
        public long Id { get; set; }
        public Boolean IsValid { get; set; }
        public String sguid { get; set; }
        public String pguid { get; set; }
        public Company companyInfo { get; set; }

        public Int32 user_level { get; set; }
        public List<String> roles { get; set; }

        public Int32 Act_ID { get; set; }

        public Guid LoginKey {get; set;}

        public OTSAPIResponse ApiResponse {get; set;}
        public string IP_Address { get; set; }
    }
}
