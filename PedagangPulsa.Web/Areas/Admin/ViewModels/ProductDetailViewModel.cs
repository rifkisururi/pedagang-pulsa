namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class ProductDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? ProductGroupId { get; set; }
    public string? ProductGroupName { get; set; }
    public string? Operator { get; set; }
    public string? Description { get; set; }
    public decimal? Denomination { get; set; }
    public int? ValidityDays { get; set; }
    public string? ValidityText { get; set; }
    public int? QuotaMb { get; set; }
    public string? QuotaText { get; set; }
    public bool IsActive { get; set; }
    public List<PriceItem> LevelPrices { get; set; } = new();
    public List<SupplierProductItem> SupplierProducts { get; set; } = new();

    public class PriceItem
    {
        public int LevelId { get; set; }
        public string LevelName { get; set; } = string.Empty;
        public decimal Margin { get; set; }
        public decimal CostPrice { get; set; }
        public decimal ComputedSellPrice { get; set; }
        public decimal MarginPercent { get; set; }
    }

    public class SupplierProductItem
    {
        public int Id { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierProductCode { get; set; } = string.Empty;
        public string? SupplierProductName { get; set; }
        public decimal CostPrice { get; set; }
        public short Seq { get; set; }
        public bool IsActive { get; set; }
    }
}
