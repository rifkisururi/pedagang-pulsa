using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Api.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Invalid phone number format")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "PIN is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "PIN must be exactly 6 digits")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN must be 6 digits")]
    public string Pin { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Referral code cannot exceed 20 characters")]
    public string? ReferralCode { get; set; }
}

public class RegisterResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserDto? User { get; set; }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public UserDto User { get; set; } = null!;
}

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class VerifyPinRequest
{
    [Required(ErrorMessage = "PIN is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "PIN must be exactly 6 digits")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN must be 6 digits")]
    public string Pin { get; set; } = string.Empty;
}

public class VerifyPinResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string PinSessionToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string Level { get; set; } = string.Empty;
    public int LevelId { get; set; }
    public decimal Balance { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
