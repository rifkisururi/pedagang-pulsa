using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class UserLevelDeleteViewModel
{
    [Required]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
