namespace WFDSOrdersAPI8.Data.Entity
{
    public class Company : ICompany
    {
        public Int32 ID { get; set; }
        public Int32 COID { get; set; }

        public string CODesc { get; set; }

        public Int32 DefaultCustomer { get; set; }

        public Int32 DefaultCostCentre { get; set; }

        public Boolean CanAccessProducts { get; set; }

        public Boolean CanAccessSDA { get; set; }

    }
}
