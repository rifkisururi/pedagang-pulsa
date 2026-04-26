using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Web.Areas.Admin.ViewModels;

public class UserDetailViewModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ActiveBalance { get; set; }
    public decimal HeldBalance { get; set; }
    public decimal TotalBalance => ActiveBalance + HeldBalance;
    public string CreatedAt { get; set; } = string.Empty;
    public string? ReferralCode { get; set; }
    public string? ReferredBy { get; set; }
    public DateTime? PinLockedAt { get; set; }

    // For balance ledger
    public class BalanceLedgerItem
    {
        public DateTime CreatedAt { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal ActiveBefore { get; set; }
        public decimal ActiveAfter { get; set; }
        public string? Notes { get; set; }
    }
    public List<BalanceLedgerItem> RecentTransactions { get; set; } = new();
}

public class ResetPasswordViewModel
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password baru wajib diisi")]
    [MinLength(6, ErrorMessage = "Password minimal 6 karakter")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Konfirmasi password wajib diisi")]
    [Compare("NewPassword", ErrorMessage = "Password tidak cocok")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ResetPinViewModel
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "PIN baru wajib diisi")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN harus 6 digit angka")]
    public string NewPin { get; set; } = string.Empty;

    [Required(ErrorMessage = "Konfirmasi PIN wajib diisi")]
    [Compare("NewPin", ErrorMessage = "PIN tidak cocok")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN harus 6 digit angka")]
    public string ConfirmPin { get; set; } = string.Empty;
}
