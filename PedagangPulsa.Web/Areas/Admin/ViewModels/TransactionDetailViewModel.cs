namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class TransactionDetailViewModel
{
    public Guid Id { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public decimal SellPrice { get; set; }
    public string? SerialNumber { get; set; }
    public string? SupplierTrxId { get; set; }
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public List<AttemptItem> Attempts { get; set; } = new();

    public class AttemptItem
    {
        public long Id { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public short Seq { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime AttemptedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SupplierRefId { get; set; }
        public string? SupplierTrxId { get; set; }
    }
}
