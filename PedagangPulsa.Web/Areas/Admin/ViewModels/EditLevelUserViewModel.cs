using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class EditLevelUserViewModel
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public int CurrentLevelId { get; set; }
    public string CurrentLevelName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a new level")]
    public int NewLevelId { get; set; }

    public List<UserLevelItem> AvailableLevels { get; set; } = new();

    public class UserLevelItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
