using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class SupplierProductReorderViewModel
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public List<SupplierSequenceItem> Suppliers { get; set; } = new();
}

public class SupplierSequenceItem
{
    [Required]
    public int SupplierId { get; set; }

    [Required]
    public short Seq { get; set; }
}
