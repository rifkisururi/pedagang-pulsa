namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class SupplierDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ApiUrl { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
    public string SupplierSoftware { get; set; } = "Otomax";
    public decimal Balance { get; set; }
    public decimal? BalanceThresholdLow { get; set; }
    public decimal? BalanceThresholdCritical { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
