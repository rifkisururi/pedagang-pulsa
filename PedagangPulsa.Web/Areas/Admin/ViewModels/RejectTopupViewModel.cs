namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class RejectTopupViewModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string RejectReason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}