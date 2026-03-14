namespace PedagangPulsa.Api.DTOs;

public class BalanceResponse
{
    public bool Success { get; set; }
    public decimal ActiveBalance { get; set; }
    public decimal HeldBalance { get; set; }
    public decimal TotalBalance { get; set; }
}

public class BalanceHistoryItem
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal ActiveBefore { get; set; }
    public decimal ActiveAfter { get; set; }
    public decimal HeldBefore { get; set; }
    public decimal HeldAfter { get; set; }
    public string? Description { get; set; }
}

public class BalanceHistoryResponse : PagedResponse<BalanceHistoryItem>
{
}
