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
        public string? ProductGroup { get; set; }
        public decimal? Denomination { get; set; }
        public int? ValidityDays { get; set; }
        public string? ValidityText { get; set; }
        public int? QuotaMb { get; set; }
        public string? QuotaText { get; set; }
        public string IsActive { get; set; } = string.Empty;
    }
}
