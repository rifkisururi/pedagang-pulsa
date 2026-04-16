using System.ComponentModel.DataAnnotations;
using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class UserLevelViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Level name is required")]
    [StringLength(50, ErrorMessage = "Name cannot be longer than 50 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Markup type is required")]
    public MarkupType MarkupType { get; set; }

    [Required(ErrorMessage = "Markup value is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Markup value must be greater than or equal to 0")]
    public decimal MarkupValue { get; set; }


    [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
