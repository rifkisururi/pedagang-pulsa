namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class ProductListViewModel
{
    public class ProductDataRow
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Operator { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal? Denomination { get; set; }
        public string IsActive { get; set; } = string.Empty;
    }
}
