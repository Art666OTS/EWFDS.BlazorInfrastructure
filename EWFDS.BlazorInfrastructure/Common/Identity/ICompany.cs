namespace WFDSOrdersAPI8.Data.Entity
{
    public interface ICompany
    {
        string CODesc { get; set; }
        int COID { get; set; }
        int DefaultCostCentre { get; set; }
        int DefaultCustomer { get; set; }
        int ID { get; set; }
        public Boolean CanAccessProducts { get; set; }

        public Boolean CanAccessSDA { get; set; }

    }
}
