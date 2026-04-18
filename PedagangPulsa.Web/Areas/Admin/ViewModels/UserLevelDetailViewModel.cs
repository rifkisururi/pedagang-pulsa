namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class UserLevelDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MarkupType { get; set; } = string.Empty;
    public decimal MarkupValue { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
