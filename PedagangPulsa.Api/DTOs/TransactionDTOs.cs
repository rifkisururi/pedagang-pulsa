using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Api.DTOs;

public class CreateTransactionRequest
{
    [Required(ErrorMessage = "Product ID is required")]
    public Guid ProductId { get; set; }

    [Required(ErrorMessage = "Destination number is required")]
    [Phone(ErrorMessage = "Invalid phone number format")]
    public string DestinationNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "PIN session token is required")]
    public string PinSessionToken { get; set; } = string.Empty;
}

public class TransactionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ProductInfo? Product { get; set; }
    public string? Destination { get; set; }
    public decimal? SellPrice { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TransactionDetailItem
{
    public string ReferenceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string Destination { get; set; } = string.Empty;
    public decimal SellPrice { get; set; }
    public string? SerialNumber { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TransactionListResponse : PagedResponse<TransactionDetailItem>
{
}

public class ProductInfo
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Operator { get; set; }
    public decimal? Denomination { get; set; }
}
