using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Domain.Entities;

public class BalanceLedger
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public BalanceTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal ActiveBefore { get; set; }
    public decimal ActiveAfter { get; set; }
    public decimal HeldBefore { get; set; }
    public decimal HeldAfter { get; set; }
    public string? RefType { get; set; }
    public Guid? RefId { get; set; }
    public string? Notes { set; get; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
}
