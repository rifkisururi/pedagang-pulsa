namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class ProductDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Operator { get; set; }
    public string? Description { get; set; }
    public decimal? Denomination { get; set; }
    public bool IsActive { get; set; }
    public List<PriceItem> LevelPrices { get; set; } = new();

    public class PriceItem
    {
        public int LevelId { get; set; }
        public string LevelName { get; set; } = string.Empty;
        public decimal Margin { get; set; }
        public decimal CostPrice { get; set; }
        public decimal ComputedSellPrice { get; set; }
        public decimal MarginPercent { get; set; }
    }
}
