namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class TransactionListViewModel
{
    public class TransactionDataRow
    {
        public Guid Id { get; set; }
        public string ReferenceId { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public decimal SellPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? SupplierName { get; set; }
    }
}
