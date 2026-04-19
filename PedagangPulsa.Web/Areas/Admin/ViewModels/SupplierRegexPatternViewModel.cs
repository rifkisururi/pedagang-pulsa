using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class SupplierRegexPatternViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Supplier wajib dipilih")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "SeqNo wajib diisi")]
    [Range(1, int.MaxValue, ErrorMessage = "SeqNo harus lebih dari 0")]
    public int SeqNo { get; set; }

    public bool IsTrxSukses { get; set; }

    [Required(ErrorMessage = "Label wajib diisi")]
    [StringLength(100, ErrorMessage = "Label maksimal 100 karakter")]
    public string Label { get; set; } = string.Empty;

    [Required(ErrorMessage = "Regex wajib diisi")]
    public string Regex { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Sample message maksimal 2000 karakter")]
    public string? SampleMessage { get; set; }

    public bool IsActive { get; set; } = true;

    // For display purposes
    public string? SupplierName { get; set; }
}
