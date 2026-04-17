using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class ProductViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Product code is required")]
    [StringLength(50, ErrorMessage = "Code cannot be longer than 50 characters")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Product name is required")]
    [StringLength(200, ErrorMessage = "Name cannot be longer than 200 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required")]
    public int CategoryId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Denomination must be greater than or equal to 0")]
    public decimal? Denomination { get; set; }

    [StringLength(50, ErrorMessage = "Operator cannot be longer than 50 characters")]
    public string? Operator { get; set; }

    [StringLength(1000, ErrorMessage = "Description cannot be longer than 1000 characters")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<LevelPriceItem>? LevelPrices { get; set; }

    public List<CategoryItem> AvailableCategories { get; set; } = new();
    public List<LevelItem> AvailableLevels { get; set; } = new();

    public class CategoryItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class LevelItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class LevelPriceItem
    {
        public int LevelId { get; set; }
        public decimal Margin { get; set; } = 200;
    }
}
