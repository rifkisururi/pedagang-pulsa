namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class ApproveTopupViewModel
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string? TransferProof { get; set; }
    public string Notes { get; set; } = string.Empty;

    // Added properties
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? BankName { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal FinalAmount { get; set; }
}