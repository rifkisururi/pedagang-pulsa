namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class TopupListViewModel
{
    public class TopupDataRow
    {
        public Guid Id { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string? TransferProof { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
