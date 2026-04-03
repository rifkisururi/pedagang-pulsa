using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Api.DTOs;

// Request: Membuat request topup baru (belum upload bukti)
public class CreateTopupRequestDto
{
    [Required]
    public int BankAccountId { get; set; }

    [Range(typeof(decimal), "10000", "999999999999999", ErrorMessage = "Amount must be at least 10000")]
    public decimal Amount { get; set; }
}

// Response: Detail pembayaran setelah request topup
public class TopupRequestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid TopupId { get; set; }
    public string Status { get; set; } = string.Empty;
    public PaymentDetail Payment { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class PaymentDetail
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal OriginalAmount { get; set; }
    public int UniqueCode { get; set; }
    public decimal TotalAmount { get; set; } // OriginalAmount + UniqueCode
    public DateTime? ExpiresAt { get; set; }
}

// Request: Upload bukti transfer
public class UploadTransferProofDto
{
    [Required]
    public IFormFile TransferProof { get; set; } = null!;
}

// Response: Setelah upload bukti transfer
public class TransferProofResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid TopupId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? UploadedAt { get; set; }
}

// Response: History topup
public class TopupHistoryItem
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public int UniqueCode { get; set; }
    public decimal TotalAmount { get; set; }
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

// Response untuk admin approval (opsional, sudah ada di service)
public class TopupResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid TopupId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
