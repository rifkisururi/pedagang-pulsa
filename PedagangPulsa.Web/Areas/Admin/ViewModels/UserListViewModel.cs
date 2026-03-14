namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class UserListViewModel
{
    public class UserDataRow
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }
}
