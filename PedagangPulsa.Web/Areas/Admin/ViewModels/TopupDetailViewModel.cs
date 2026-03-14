namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class TopupDetailViewModel
{
    public Guid Id { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal FinalAmount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? TransferProof { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ApprovedNotes { get; set; }
    public string? Notes { get; set; }
    public string? RejectReason { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string AccountName { get; set; } = string.Empty;
}