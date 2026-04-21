using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class ProductGroupViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Nama grup wajib diisi")]
    [StringLength(200, ErrorMessage = "Nama tidak boleh lebih dari 200 karakter")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category wajib dipilih")]
    public int CategoryId { get; set; }

    [StringLength(50, ErrorMessage = "Operator tidak boleh lebih dari 50 karakter")]
    public string? Operator { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public List<CategoryItem> AvailableCategories { get; set; } = new();

    public class CategoryItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
