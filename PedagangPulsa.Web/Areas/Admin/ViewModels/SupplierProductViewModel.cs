using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class SupplierProductViewModel
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    [Required]
    public int SupplierId { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Cost price must be greater than or equal to 0")]
    public decimal CostPrice { get; set; }

    [Required(ErrorMessage = "Supplier Product Code is required")]
    [StringLength(100, ErrorMessage = "Supplier Product Code cannot be longer than 100 characters")]
    public string SupplierProductCode { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Supplier Product Name cannot be longer than 150 characters")]
    public string? SupplierProductName { get; set; }

    // Alias for compatibility with older code
    public string? SupplierSku 
    { 
        get => SupplierProductCode; 
        set => SupplierProductCode = value ?? string.Empty; 
    }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Sequence must be at least 1")]
    public short Seq { get; set; }

    // Alias for compatibility with views
    public int Sequence { get { return Seq; } }

    public bool IsActive { get; set; } = true;

    // For display only
    public string? SupplierName { get; set; }
    public List<SupplierItem> AvailableSuppliers { get; set; } = new();

    public class SupplierItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
