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

    public int? ProductGroupId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Denomination must be greater than or equal to 0")]
    public decimal? Denomination { get; set; }

    [Range(1, 3650, ErrorMessage = "ValidityDays must be between 1 and 3650")]
    public int? ValidityDays { get; set; }

    [StringLength(50, ErrorMessage = "ValidityText cannot be longer than 50 characters")]
    public string? ValidityText { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "QuotaMb must be greater than 0")]
    public int? QuotaMb { get; set; }

    [StringLength(50, ErrorMessage = "QuotaText cannot be longer than 50 characters")]
    public string? QuotaText { get; set; }

    [StringLength(50, ErrorMessage = "Operator cannot be longer than 50 characters")]
    public string? Operator { get; set; }

    [StringLength(1000, ErrorMessage = "Description cannot be longer than 1000 characters")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<LevelPriceItem>? LevelPrices { get; set; }

    public List<CategoryItem> AvailableCategories { get; set; } = new();
    public List<GroupItem> AvailableGroups { get; set; } = new();
    public List<LevelItem> AvailableLevels { get; set; } = new();

    public class CategoryItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class GroupItem
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Operator { get; set; }
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
