using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    private readonly ILogger<AuthController> _logger;
    private readonly IRedisService _redisService;

    public AuthController(
        IConfiguration configuration,
        AppDbContext context,
        ILogger<AuthController> logger,
        IRedisService redisService)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
        _redisService = redisService;
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

        // Check unique username
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email || u.Phone == request.Phone);

        if (existingUser != null)
        {
            var fieldName = existingUser.Username == request.Username ? "Username" :
                           existingUser.Email == request.Email ? "Email" : "Phone";
            return BadRequest(new ErrorResponse
            {
                Message = $"{fieldName} already exists",
                ErrorCode = "DUPLICATE_FIELD"
            });
        }

        // Get default level (first active level as default)
        var defaultLevel = await _context.UserLevels
            .FirstOrDefaultAsync(l => l.IsActive);

        if (defaultLevel == null)
        {
            _logger.LogError("No default user level configured");
            return StatusCode(500, new ErrorResponse
            {
                Message = "System configuration error",
                ErrorCode = "CONFIG_ERROR"
            });
        }

        // Hash PIN with BCrypt
        var pinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin, workFactor: 12);

        // Generate unique referral code
        var referralCode = await GenerateUniqueReferralCode();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Create user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                FullName = request.FullName,
                Email = request.Email,
                Phone = request.Phone,
                PinHash = pinHash,
                PinFailedAttempts = 0,
                LevelId = defaultLevel.Id,
                ReferralCode = referralCode,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            // Create user balance
            var userBalance = new UserBalance
            {
                UserId = user.Id,
                ActiveBalance = 0,
                HeldBalance = 0
            };
            _context.UserBalances.Add(userBalance);

            // Handle referral logic
            Guid? referrerId = null;
            if (!string.IsNullOrWhiteSpace(request.ReferralCode))
            {
                var referrer = await _context.Users
                    .FirstOrDefaultAsync(u => u.ReferralCode == request.ReferralCode);

                if (referrer != null && referrer.Id != user.Id)
                {
                    referrerId = referrer.Id;
                    user.ReferredBy = referrer.Id;

                    // Create referral log
                    var referralLog = new ReferralLog
                    {
                        Id = Guid.NewGuid(),
                        ReferrerId = referrer.Id,
                        RefereeId = user.Id,
                        BonusStatus = ReferralBonusStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ReferralLogs.Add(referralLog);

                    _logger.LogInformation("User {UserId} registered with referral code {ReferralCode} from {ReferrerId}",
                        user.Id, request.ReferralCode, referrer.Id);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Generate tokens
            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            // Store refresh token
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            var response = new RegisterResponse
            {
                Success = true,
                Message = "Registration successful",
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    FullName = user.FullName,
                    Phone = user.Phone,
                    Level = defaultLevel.Name,
                    LevelId = user.LevelId,
                    Balance = 0,
                    ReferralCode = user.ReferralCode,
                    CreatedAt = user.CreatedAt
                }
            };

            return StatusCode(201, response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error during user registration for {Username}", request.Username);
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred during registration",
                ErrorCode = "REGISTRATION_ERROR"
            });
        }
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

        var user = await _context.Users
            .Include(u => u.Balance)
            .Include(u => u.Level)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid username or PIN",
                ErrorCode = "INVALID_CREDENTIALS"
            });
        }

        if (user.Status != UserStatus.Active)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Account is not active",
                ErrorCode = "ACCOUNT_INACTIVE"
            });
        }

        // Verify PIN
        if (!BCrypt.Net.BCrypt.Verify(request.Pin, user.PinHash))
        {
            // Log failed login attempt
            _logger.LogWarning("Failed login attempt for user {Username}", request.Username);
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid username or PIN",
                ErrorCode = "INVALID_CREDENTIALS"
            });
        }

        // Generate tokens
        var accessToken = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        // Store refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        var response = new LoginResponse
        {
            Success = true,
            Message = "Login successful",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 900, // 15 minutes
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                Level = user.Level?.Name ?? "Regular",
                LevelId = user.LevelId,
                Balance = user.Balance?.ActiveBalance ?? 0,
                ReferralCode = user.ReferralCode,
                CreatedAt = user.CreatedAt
            }
        };

        return Ok(response);
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

        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .ThenInclude(u => u.Level)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);

        if (refreshToken == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid or expired refresh token",
                ErrorCode = "INVALID_REFRESH_TOKEN"
            });
        }

        if (refreshToken.User.Status != UserStatus.Active)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Account is not active",
                ErrorCode = "ACCOUNT_INACTIVE"
            });
        }

        // Generate new tokens
        var accessToken = GenerateJwtToken(refreshToken.User);
        var newRefreshToken = GenerateRefreshToken();

        // Revoke old refresh token
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;

        // Store new refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = refreshToken.User.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        var response = new RefreshTokenResponse
        {
            Success = true,
            Message = "Token refreshed successfully",
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 900 // 15 minutes
        };

        return Ok(response);
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

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var userGuid = Guid.Parse(userId);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userGuid);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "User not found",
                ErrorCode = "USER_NOT_FOUND"
            });
        }

        // Check Redis for lockout
        var lockoutKey = $"pin_lockout:{userGuid}";
        var isLockedOut = await _redisService.ExistsAsync(lockoutKey);

        if (isLockedOut)
        {
            var ttl = await _redisService.TtlAsync(lockoutKey);
            return StatusCode(429, new ErrorResponse
            {
                Message = $"Too many failed attempts. Account locked for {ttl} seconds",
                ErrorCode = "ACCOUNT_LOCKED"
            });
        }

        // Verify PIN
        if (!BCrypt.Net.BCrypt.Verify(request.Pin, user.PinHash))
        {
            // Increment failed attempts
            user.PinFailedAttempts++;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.PinFailedAttempts >= 3)
            {
                // Lock for 15 minutes
                user.PinLockedAt = DateTime.UtcNow;
                await _redisService.SetAsync(lockoutKey, "locked", TimeSpan.FromMinutes(15));
                user.PinFailedAttempts = 0; // Reset after lockout

                _logger.LogWarning("User {UserId} locked out due to 3 failed PIN attempts", userGuid);
            }

            await _context.SaveChangesAsync();

            var remainingAttempts = 3 - user.PinFailedAttempts;
            return Unauthorized(new ErrorResponse
            {
                Message = user.PinFailedAttempts >= 3
                    ? "Account locked for 15 minutes due to too many failed attempts"
                    : $"Invalid PIN. {remainingAttempts} attempts remaining",
                ErrorCode = "INVALID_PIN"
            });
        }

        // Reset failed attempts on successful verification
        user.PinFailedAttempts = 0;
        user.PinLockedAt = null;
        await _context.SaveChangesAsync();

        // Generate PIN session token (store in Redis, TTL 5 min)
        var pinSessionToken = Guid.NewGuid().ToString();
        var sessionKey = $"pin_session:{userGuid}:{pinSessionToken}";
        await _redisService.SetAsync(sessionKey, userGuid.ToString(), TimeSpan.FromMinutes(5));

        var response = new VerifyPinResponse
        {
            Success = true,
            Message = "PIN verified successfully",
            PinSessionToken = pinSessionToken,
            ExpiresIn = 300 // 5 minutes
        };

        return Ok(response);
    }

    private string GenerateJwtToken(User user)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("level_id", user.LevelId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private async Task<string> GenerateUniqueReferralCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Removed similar looking chars
        var code = string.Empty;

        do
        {
            code = System.Security.Cryptography.RandomNumberGenerator.GetString(chars, 8);

            // Check uniqueness
            var exists = await _context.Users
                .AnyAsync(u => u.ReferralCode == code);

            if (!exists)
                break;

        } while (true);

        return code;
    }
}

// Redis Service Interface
public interface IRedisService
{
    Task SetAsync(string key, string value, TimeSpan? expiry = null);
    Task<string?> GetAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task RemoveAsync(string key);
    Task<long> TtlAsync(string key);
}

// Redis Service Implementation (using StackExchange.Redis)
public class RedisService : IRedisService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedisService> _logger;

    public RedisService(IConfiguration configuration, ILogger<RedisService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        // For now, we'll use in-memory implementation
        // TODO: Replace with actual Redis implementation
        _logger.LogDebug("Setting key {Key} in Redis", key);
        await Task.CompletedTask;
    }

    public async Task<string?> GetAsync(string key)
    {
        _logger.LogDebug("Getting key {Key} from Redis", key);
        await Task.CompletedTask;
        return null;
    }

    public async Task<bool> ExistsAsync(string key)
    {
        _logger.LogDebug("Checking if key {Key} exists in Redis", key);
        await Task.CompletedTask;
        return false;
    }

    public async Task RemoveAsync(string key)
    {
        _logger.LogDebug("Removing key {Key} from Redis", key);
        await Task.CompletedTask;
    }

    public async Task<long> TtlAsync(string key)
    {
        _logger.LogDebug("Getting TTL for key {Key} from Redis", key);
        await Task.CompletedTask;
        return 0;
    }
}
