namespace PedagangPulsa.Api.DTOs;

public class CreateTopupRequest
{
    public int BankAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class TopupResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid TopupId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TopupHistoryItem
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TransferProofUrl { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TopupHistoryResponse : PagedResponse<TopupHistoryItem>
{
}
