using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class SupplierProductDeleteViewModel
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public int SupplierId { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
}
