using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class UpdatePriceViewModel
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public int LevelId { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Margin must be greater than or equal to 0")]
    public decimal Margin { get; set; } = 200;
}
