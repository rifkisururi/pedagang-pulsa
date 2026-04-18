using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Services;
using System.Security.Claims;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
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
                CreatedAt = user.CreatedAt
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
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory?)HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory));
        var logger = loggerFactory?.CreateLogger("PedagangPulsa.Api.Controllers.AuthController");

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            logger?.LogWarning("Failed login attempt. Username: {Username}, Reason: {Reason}, IP: {IPAddress}", request.Username, result.ErrorMessage, ipAddress);

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

        // Log successful login
        logger?.LogInformation("User logged in successfully. Username: {Username}, Email: {Email}, IP: {IPAddress}", user.UserName, user.Email, ipAddress);

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
                CreatedAt = user.CreatedAt
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

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        var username = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
        var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory?)HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory));
        var logger = loggerFactory?.CreateLogger("PedagangPulsa.Api.Controllers.AuthController");
        logger?.LogInformation("User logged out successfully. Username: {Username}", username);

        return Ok(new { Success = true, Message = "Logout successful" });
    }
}