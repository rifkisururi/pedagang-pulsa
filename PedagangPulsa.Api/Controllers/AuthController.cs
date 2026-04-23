using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Services;
using System.Security.Claims;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly PhoneVerificationService _phoneVerificationService;

    public AuthController(AuthService authService, PhoneVerificationService phoneVerificationService)
    {
        _authService = authService;
        _phoneVerificationService = phoneVerificationService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var result = await _authService.RegisterAsync(
            request.Username,
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            request.Pin,
            request.ReferralCode);

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            if (result.ErrorMessage == "Default user level not configured")
            {
                return StatusCode(500, new ErrorResponse
                {
                    Message = "System configuration error",
                    ErrorCode = "CONFIG_ERROR"
                });
            }

            return BadRequest(new ErrorResponse
            {
                Message = result.ErrorMessage,
                ErrorCode = result.ErrorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                    ? "DUPLICATE_FIELD"
                    : "REGISTRATION_ERROR"
            });
        }

        var user = result.User!;
        return StatusCode(201, new RegisterResponse
        {
            Success = true,
            Message = "Registration successful",
            User = new UserDto
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                Level = user.Level?.Name ?? string.Empty,
                LevelId = user.LevelId,
                Balance = user.Balance?.ActiveBalance ?? 0,
                ReferralCode = user.ReferralCode,
                CreatedAt = user.CreatedAt,
                PhoneVerified = user.PhoneVerifiedAt.HasValue,
                EmailVerified = user.EmailVerifiedAt.HasValue,
                HasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash)
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var result = await _authService.LoginWithPasswordAsync(request.Username, request.Password);
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = result.ErrorMessage,
                ErrorCode = result.ErrorMessage.Contains("active", StringComparison.OrdinalIgnoreCase)
                    || result.ErrorMessage.Contains("suspended", StringComparison.OrdinalIgnoreCase)
                    ? "ACCOUNT_INACTIVE"
                    : "INVALID_CREDENTIALS"
            });
        }

        var user = result.User!;
        return Ok(new LoginResponse
        {
            Success = true,
            Message = "Login successful",
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = 900,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                Level = user.Level?.Name ?? string.Empty,
                LevelId = user.LevelId,
                Balance = user.Balance?.ActiveBalance ?? 0,
                ReferralCode = user.ReferralCode,
                CreatedAt = user.CreatedAt,
                PhoneVerified = user.PhoneVerifiedAt.HasValue,
                EmailVerified = user.EmailVerifiedAt.HasValue,
                HasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash)
            }
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var result = await _authService.RefreshTokenAsync(request.RefreshToken);
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            var message = result.ErrorMessage switch
            {
                "Invalid refresh token" => "Invalid or expired refresh token",
                "Refresh token has expired" => "Invalid or expired refresh token",
                _ => result.ErrorMessage
            };

            return Unauthorized(new ErrorResponse
            {
                Message = message,
                ErrorCode = "INVALID_REFRESH_TOKEN"
            });
        }

        return Ok(new RefreshTokenResponse
        {
            Success = true,
            Message = "Token refreshed successfully",
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = 900
        });
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var result = await _authService.LoginWithGoogleAsync(request.IdToken);

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            var errorCode = result.ErrorMessage.Contains("not configured", StringComparison.OrdinalIgnoreCase)
                ? "GOOGLE_AUTH_NOT_CONFIGURED"
                : result.ErrorMessage.Contains("suspended", StringComparison.OrdinalIgnoreCase) || result.ErrorMessage.Contains("inactive", StringComparison.OrdinalIgnoreCase)
                    ? "ACCOUNT_INACTIVE"
                    : "GOOGLE_AUTH_FAILED";

            return Unauthorized(new ErrorResponse
            {
                Message = result.ErrorMessage,
                ErrorCode = errorCode
            });
        }

        var user = result.User!;
        return Ok(new GoogleLoginResponse
        {
            Success = true,
            Message = result.RequiresPinSetup
                ? "Account created successfully. Please set your PIN to continue."
                : "Login successful",
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = 900,
            RequiresPinSetup = result.RequiresPinSetup,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                Level = user.Level?.Name ?? string.Empty,
                LevelId = user.LevelId,
                Balance = user.Balance?.ActiveBalance ?? 0,
                ReferralCode = user.ReferralCode,
                CreatedAt = user.CreatedAt,
                PhoneVerified = user.PhoneVerifiedAt.HasValue,
                EmailVerified = user.EmailVerifiedAt.HasValue,
                HasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash)
            }
        });
    }

    [HttpPost("pin/initialize")]
    [Authorize]
    public async Task<IActionResult> InitializePin([FromBody] InitializePinRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse { Message = "Invalid token", ErrorCode = "INVALID_TOKEN" });
        }

        var result = await _authService.InitializePinAsync(userId, request.Pin);
        if (!result.Success)
        {
            return BadRequest(new ErrorResponse { Message = result.ErrorMessage, ErrorCode = "PIN_INIT_FAILED" });
        }

        return Ok(new InitializePinResponse
        {
            Success = true,
            Message = "PIN set successfully"
        });
    }

    [HttpPost("pin/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyPin([FromBody] VerifyPinRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
            });
        }

        // Check if user is locked out in Redis
        if (await _authService.IsPinLockedOutAsync(userId))
        {
            var ttl = await _authService.GetLockoutRemainingSecondsAsync(userId);
            return StatusCode(429, new ErrorResponse
            {
                Message = $"Too many failed attempts. Account locked for {ttl} seconds",
                ErrorCode = "ACCOUNT_LOCKED"
            });
        }

        var result = await _authService.VerifyPinAsync(userId, request.Pin);
        if (!result.Success)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = result.ErrorMessage,
                ErrorCode = "INVALID_PIN"
            });
        }

        // Set PIN session and invalidate any previous sessions
        await _authService.SetPinSessionAsync(userId, result.PinSessionToken);
        await _authService.InvalidatePreviousPinSessionsAsync(userId, result.PinSessionToken);

        return Ok(new VerifyPinResponse
        {
            Success = true,
            Message = "PIN verified successfully",
            PinSessionToken = result.PinSessionToken,
            ExpiresIn = 300
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
            });
        }

        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "User not found",
                ErrorCode = "USER_NOT_FOUND"
            });
        }

        return Ok(new UserMeResponse
        {
            Success = true,
            Message = "User profile retrieved successfully",
            User = new UserDto
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                Level = user.Level?.Name ?? string.Empty,
                LevelId = user.LevelId,
                Balance = user.Balance?.ActiveBalance ?? 0,
                ReferralCode = user.ReferralCode,
                CreatedAt = user.CreatedAt,
                PhoneVerified = user.PhoneVerifiedAt.HasValue,
                EmailVerified = user.EmailVerifiedAt.HasValue,
                HasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash)
            }
        });
    }

    [HttpPut("pin")]
    [Authorize]
    public async Task<IActionResult> ChangePin([FromBody] ChangePinRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
            });
        }

        var result = await _authService.ChangePinAsync(userId, request.CurrentPin, request.NewPin);
        if (!result.Success)
        {
            var errorCode = result.ErrorMessage.Contains("locked", StringComparison.OrdinalIgnoreCase)
                ? "ACCOUNT_LOCKED"
                : "INVALID_PIN";

            var statusCode = result.ErrorMessage.Contains("locked", StringComparison.OrdinalIgnoreCase)
                ? 429
                : 400;

            return StatusCode(statusCode, new ErrorResponse
            {
                Message = result.ErrorMessage,
                ErrorCode = errorCode
            });
        }

        return Ok(new ChangePinResponse
        {
            Success = true,
            Message = "PIN changed successfully"
        });
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
            });
        }

        var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        if (!result.Success)
        {
            var errorCode = result.ErrorMessage.Contains("incorrect", StringComparison.OrdinalIgnoreCase)
                ? "INVALID_PASSWORD"
                : result.ErrorMessage.Contains("no password", StringComparison.OrdinalIgnoreCase)
                    ? "NO_PASSWORD_SET"
                    : "CHANGE_PASSWORD_ERROR";

            return BadRequest(new ErrorResponse
            {
                Message = result.ErrorMessage,
                ErrorCode = errorCode
            });
        }

        return Ok(new ChangePasswordResponse
        {
            Success = true,
            Message = "Password changed successfully"
        });
    }

    [HttpPost("phone/request")]
    public async Task<IActionResult> RequestPhoneOtp([FromBody] RequestPhoneOtpRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var result = await _phoneVerificationService.RequestOtpAsync(request.PhoneNumber);

        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                "SMS_GATEWAY_UNAVAILABLE" => 503,
                "PHONE_OTP_RATE_LIMITED" => 429,
                _ => 400
            };

            return StatusCode(statusCode, new ErrorResponse
            {
                Message = result.Message!,
                ErrorCode = result.ErrorCode
            });
        }

        return Ok(new RequestPhoneOtpResponse
        {
            Success = true,
            Message = "OTP berhasil dikirim",
            ExpiresIn = 300
        });
    }

    [HttpPost("phone/verify")]
    public async Task<IActionResult> VerifyPhoneOtp([FromBody] VerifyPhoneOtpRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
            });
        }

        var result = await _phoneVerificationService.VerifyOtpAsync(request.PhoneNumber, request.Otp, userId);

        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                "OTP_MAX_ATTEMPTS" => 429,
                "USER_NOT_FOUND" => 404,
                _ => 400
            };

            return StatusCode(statusCode, new ErrorResponse
            {
                Message = result.Message!,
                ErrorCode = result.ErrorCode
            });
        }

        return Ok(new VerifyPhoneOtpResponse
        {
            Success = true,
            Message = "Nomor telepon berhasil diverifikasi",
            PhoneVerified = true
        });
    }

    [HttpGet("google/status")]
    [Authorize]
    public async Task<IActionResult> GoogleStatus()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse { Message = "Invalid token", ErrorCode = "INVALID_TOKEN" });
        }

        var link = await _authService.GetGoogleLinkStatusAsync(userId);
        return Ok(new GoogleStatusResponse
        {
            Success = true,
            IsLinked = link != null,
            Provider = link?.Provider,
            Email = link?.Email,
            DisplayName = link?.ProviderDisplayName,
            AvatarUrl = link?.AvatarUrl,
            LinkedAt = link?.CreatedAt
        });
    }

    [HttpPost("google/link")]
    [Authorize]
    public async Task<IActionResult> GoogleLink([FromBody] GoogleLinkRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse { Message = "Invalid token", ErrorCode = "INVALID_TOKEN" });
        }

        var result = await _authService.LinkGoogleAsync(userId, request.IdToken);
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            var errorCode = result.ErrorMessage.Contains("already linked", StringComparison.OrdinalIgnoreCase)
                ? "ALREADY_LINKED"
                : result.ErrorMessage.Contains("not configured", StringComparison.OrdinalIgnoreCase)
                    ? "GOOGLE_AUTH_NOT_CONFIGURED"
                    : "GOOGLE_LINK_FAILED";

            var statusCode = result.ErrorMessage.Contains("another account", StringComparison.OrdinalIgnoreCase)
                ? 409
                : 400;

            return StatusCode(statusCode, new ErrorResponse
            {
                Message = result.ErrorMessage,
                ErrorCode = errorCode
            });
        }

        return Ok(new GoogleLinkResponse
        {
            Success = true,
            Message = "Google account linked successfully"
        });
    }

    [HttpDelete("google/unlink")]
    [Authorize]
    public async Task<IActionResult> GoogleUnlink()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse { Message = "Invalid token", ErrorCode = "INVALID_TOKEN" });
        }

        var result = await _authService.UnlinkGoogleAsync(userId);
        if (!result.Success)
        {
            var errorCode = result.ErrorMessage.Contains("no password", StringComparison.OrdinalIgnoreCase)
                ? "PASSWORD_REQUIRED"
                : result.ErrorMessage.Contains("no Google", StringComparison.OrdinalIgnoreCase)
                    ? "NOT_LINKED"
                    : "GOOGLE_UNLINK_FAILED";

            return BadRequest(new ErrorResponse
            {
                Message = result.ErrorMessage,
                ErrorCode = errorCode
            });
        }

        return Ok(new GoogleUnlinkResponse
        {
            Success = true,
            Message = "Google account unlinked successfully"
        });
    }
}
