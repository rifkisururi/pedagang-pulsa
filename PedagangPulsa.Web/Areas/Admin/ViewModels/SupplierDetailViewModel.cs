namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class SupplierDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ApiUrl { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string? ApiSecret { get; set; }
    public int TimeoutSeconds { get; set; }
    public decimal Balance { get; set; }
    public decimal? BalanceThresholdLow { get; set; }
    public decimal? BalanceThresholdCritical { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
