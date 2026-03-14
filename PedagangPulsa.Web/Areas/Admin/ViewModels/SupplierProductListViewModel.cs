namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class SupplierProductListViewModel
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public List<SupplierProductItem> SupplierProducts { get; set; } = new();
    public List<AvailableSupplierItem> AvailableSuppliers { get; set; } = new();

    public class SupplierProductItem
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public string? SupplierSku { get; set; }
        public short Seq { get; set; }
        public bool IsActive { get; set; }

        // Alias for compatibility with views
        public int Sequence { get { return Seq; } }
    }

    public class AvailableSupplierItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
