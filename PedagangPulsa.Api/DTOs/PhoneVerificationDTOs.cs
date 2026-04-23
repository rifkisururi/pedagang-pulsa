using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Api.DTOs;

public class RequestPhoneOtpRequest
{
    [Required(ErrorMessage = "Nomor telepon wajib diisi")]
    [StringLength(20, ErrorMessage = "Nomor telepon maksimal 20 karakter")]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class RequestPhoneOtpResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class VerifyPhoneOtpRequest
{
    [Required(ErrorMessage = "Nomor telepon wajib diisi")]
    [StringLength(20, ErrorMessage = "Nomor telepon maksimal 20 karakter")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kode OTP wajib diisi")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP harus 6 digit")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP harus berupa 6 digit angka")]
    public string Otp { get; set; } = string.Empty;
}

public class VerifyPhoneOtpResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool PhoneVerified { get; set; }
}
