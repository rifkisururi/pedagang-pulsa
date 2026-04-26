namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class SupplierListViewModel
{
    public class SupplierDataRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ApiUrl { get; set; }
        public int TimeoutSeconds { get; set; }
        public string SupplierSoftware { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string IsActive { get; set; } = string.Empty;
    }
}
