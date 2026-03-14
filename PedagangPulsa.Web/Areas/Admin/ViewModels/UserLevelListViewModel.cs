namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class UserLevelListViewModel
{
    public class LevelDataRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MarkupType { get; set; } = string.Empty;
        public decimal MarkupValue { get; set; }
        public string IsActive { get; set; } = string.Empty;
        public int UserCount { get; set; }
    }
}
