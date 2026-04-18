using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PedagangPulsa.Application.Services;

public class AuthService
{
    private const string DefaultJwtSecret = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
    private const string DefaultJwtIssuer = "PedagangPulsa";
    private const string DefaultJwtAudience = "PedagangPulsaMobile";

    private readonly IAppDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly IRedisService? _redisService;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public AuthService(
        IAppDbContext context,
        ILogger<AuthService> logger,
        IRedisService? redisService = null,
        string? jwtSecret = null,
        string? jwtIssuer = null,
        string? jwtAudience = null)
    {
        _context = context;
        _logger = logger;
        _redisService = redisService;
        _jwtSecret = jwtSecret ?? DefaultJwtSecret;
        _jwtIssuer = jwtIssuer ?? DefaultJwtIssuer;
        _jwtAudience = jwtAudience ?? DefaultJwtAudience;
    }

    public async Task<(User? User, string ErrorMessage)> RegisterAsync(
        string username,
        string? fullName,
        string? email,
        string? phone,
        string pin,
        string? referralCode = null)
    {
        return await RegisterAsync(username, fullName, email, phone, null, pin, referralCode);
    }

    public async Task<(User? User, string ErrorMessage)> RegisterAsync(
        string username,
        string? fullName,
        string? email,
        string? phone,
        string? password,
        string pin,
        string? referralCode = null)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.UserName == username);

        if (existingUser != null)
        {
            return (null, "Username already exists");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (existingEmail != null)
            {
                return (null, "Email already exists");
            }
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var existingPhone = await _context.Users
                .FirstOrDefaultAsync(u => u.Phone == phone);

            if (existingPhone != null)
            {
                return (null, "Phone number already exists");
            }
        }

        User? referrer = null;
        Guid? referredBy = null;
        if (!string.IsNullOrWhiteSpace(referralCode))
        {
            referrer = await _context.Users
                .Include(u => u.Level)
                .FirstOrDefaultAsync(u => u.ReferralCode == referralCode);

            if (referrer == null)
            {
                return (null, "Invalid referral code");
            }

            referredBy = referrer.Id;
        }

        var defaultLevel = await _context.UserLevels
            .FirstOrDefaultAsync(l => l.Name == "Bronze");

        if (defaultLevel == null)
        {
            return (null, "Default user level not configured");
        }

        var user = new User
        {
            UserName = username,
            FullName = fullName,
            Email = email,
            Phone = phone,
            PasswordHash = string.IsNullOrWhiteSpace(password)
                ? null
                : BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            PinHash = BCrypt.Net.BCrypt.HashPassword(pin, workFactor: 12),
            PinFailedAttempts = 0,
            PinLockedAt = null,
            LevelId = defaultLevel.Id,
            CanTransferOverride = null,
            ReferralCode = GenerateReferralCode(username),
            ReferredBy = referredBy,
            Status = UserStatus.Active,
            EmailVerifiedAt = null,
            PhoneVerifiedAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var balance = new UserBalance
        {
            UserId = user.Id,
            ActiveBalance = 0,
            HeldBalance = 0,
            UpdatedAt = DateTime.UtcNow
        };

        user.Level = defaultLevel;
        user.Balance = balance;

        _context.Users.Add(user);
        _context.UserBalances.Add(balance);

        if (referredBy.HasValue && referrer != null)
        {
            _context.ReferralLogs.Add(new ReferralLog
            {
                ReferrerId = referredBy.Value,
                RefereeId = user.Id,
                BonusAmount = null,
                BonusStatus = ReferralBonusStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} registered successfully", username);

        return (user, string.Empty);
    }

    public async Task<(User? User, string AccessToken, string RefreshToken, string ErrorMessage)> LoginAsync(
        string username,
        string pin)
    {
        var user = await _context.Users
            .Include(u => u.Balance)
            .Include(u => u.Level)
            .FirstOrDefaultAsync(u => u.UserName == username);

        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for username {Username}. Reason: {Reason}", username, "Invalid username or PIN");
            return (null, string.Empty, string.Empty, "Invalid username or PIN");
        }

        if (user.Status == UserStatus.Suspended)
        {
            return (null, string.Empty, string.Empty, "Account is suspended");
        }

        if (user.Status == UserStatus.Inactive)
        {
            return (null, string.Empty, string.Empty, "Account is inactive");
        }

        if (user.PinLockedAt.HasValue && user.PinLockedAt.Value > DateTime.UtcNow.AddMinutes(-15))
        {
            var lockoutRemaining = (user.PinLockedAt.Value - DateTime.UtcNow.AddMinutes(-15)).TotalMinutes;
            return (null, string.Empty, string.Empty, $"Account is locked. Try again in {Math.Ceiling(lockoutRemaining)} minutes");
        }

        if (!BCrypt.Net.BCrypt.Verify(pin, user.PinHash))
        {
            user.PinFailedAttempts++;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.PinFailedAttempts >= 3)
            {
                user.PinLockedAt = DateTime.UtcNow;
                _logger.LogWarning("User {Username} account locked due to 3 failed PIN attempts", username);
            }

            await _context.SaveChangesAsync();

            var attemptsLeft = 3 - user.PinFailedAttempts;
            return (null, string.Empty, string.Empty, attemptsLeft > 0
                ? $"Invalid PIN. {attemptsLeft} attempts remaining"
                : "Account locked for 15 minutes due to too many failed attempts");
        }

        if (user.PinFailedAttempts > 0 || user.PinLockedAt.HasValue)
        {
            user.PinFailedAttempts = 0;
            user.PinLockedAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} logged in successfully", username);

        return (user, accessToken, refreshToken, string.Empty);
    }

    public async Task<(User? User, string AccessToken, string RefreshToken, string ErrorMessage)> LoginWithPasswordAsync(
        string username,
        string password)
    {
        var user = await _context.Users
            .Include(u => u.Balance)
            .Include(u => u.Level)
            .FirstOrDefaultAsync(u => u.UserName == username);

        if (user == null)
        {
            _logger.LogWarning("Failed password login attempt for username {Username}. Reason: {Reason}", username, "Invalid username or password");
            return (null, string.Empty, string.Empty, "Invalid username or password");
        }

        if (user.Status == UserStatus.Suspended)
        {
            return (null, string.Empty, string.Empty, "Account is suspended");
        }

        if (user.Status == UserStatus.Inactive)
        {
            return (null, string.Empty, string.Empty, "Account is inactive");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("Failed password login attempt for user {Username}", username);
            return (null, string.Empty, string.Empty, "Invalid username or password");
        }

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} logged in successfully with password", username);

        return (user, accessToken, refreshToken, string.Empty);
    }

    public async Task<(bool Success, string PinSessionToken, string ErrorMessage, bool ShouldSetLockout)> VerifyPinAsync(
        Guid userId,
        string pin)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return (false, string.Empty, "User not found", false);
        }

        var lockoutKey = $"pin_lockout:{userId}";
        if (_redisService != null && await _redisService.ExistsAsync(lockoutKey))
        {
            var ttl = await _redisService.TtlAsync(lockoutKey);
            return (false, string.Empty, $"Account is locked. Try again in {Math.Ceiling(Math.Max(ttl, 1) / 60d)} minutes", false);
        }

        if (user.PinLockedAt.HasValue && user.PinLockedAt.Value > DateTime.UtcNow.AddMinutes(-15))
        {
            var lockoutRemaining = (user.PinLockedAt.Value - DateTime.UtcNow.AddMinutes(-15)).TotalMinutes;
            return (false, string.Empty, $"Account is locked. Try again in {Math.Ceiling(lockoutRemaining)} minutes", false);
        }

        if (!BCrypt.Net.BCrypt.Verify(pin, user.PinHash))
        {
            user.PinFailedAttempts++;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.PinFailedAttempts >= 3)
            {
                user.PinLockedAt = DateTime.UtcNow;
                if (_redisService != null)
                {
                    await _redisService.SetAsync(lockoutKey, "locked", TimeSpan.FromMinutes(15));
                }

                _logger.LogWarning("User {UserId} account locked due to 3 failed PIN attempts", userId);
            }

            await _context.SaveChangesAsync();

            var attemptsLeft = 3 - user.PinFailedAttempts;
            return (false, string.Empty, attemptsLeft > 0
                ? $"Invalid PIN. {attemptsLeft} attempts remaining"
                : "Account locked for 15 minutes due to too many failed attempts", false);
        }

        if (user.PinFailedAttempts > 0 || user.PinLockedAt.HasValue)
        {
            user.PinFailedAttempts = 0;
            user.PinLockedAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        var pinSessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        _logger.LogInformation("PIN verified for user {UserId}", userId);

        return (true, pinSessionToken, string.Empty, true);
    }

    public async Task<bool> IsPinLockedOutAsync(Guid userId)
    {
        var lockoutKey = $"pin_lockout:{userId}";
        return _redisService != null && await _redisService.ExistsAsync(lockoutKey);
    }

    public async Task<long> GetLockoutRemainingSecondsAsync(Guid userId)
    {
        var lockoutKey = $"pin_lockout:{userId}";
        if (_redisService == null)
        {
            return 0;
        }
        return await _redisService.TtlAsync(lockoutKey);
    }

    public async Task SetPinSessionAsync(Guid userId, string pinSessionToken)
    {
        if (_redisService == null)
        {
            return;
        }

        var sessionKey = $"pin_session:{userId}:{pinSessionToken}";
        await _redisService.SetAsync(sessionKey, userId.ToString(), TimeSpan.FromMinutes(5));
    }

    public async Task InvalidatePreviousPinSessionsAsync(Guid userId, string currentToken)
    {
        if (_redisService == null)
        {
            return;
        }

        var versionKey = $"pin_session_version:{userId}";
        await _redisService.SetAsync(versionKey, currentToken, TimeSpan.FromMinutes(5));
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateAndConsumePinSessionAsync(Guid userId, string pinSessionToken)
    {
        if (_redisService == null)
        {
            return (true, string.Empty);
        }

        var versionKey = $"pin_session_version:{userId}";
        var latestToken = await _redisService.GetAndRemoveAsync(versionKey);

        if (latestToken == null)
        {
            return (false, "No valid PIN session found. Please verify your PIN again.");
        }

        if (latestToken != pinSessionToken)
        {
            return (false, "PIN session has been superseded. Please verify your PIN again.");
        }

        var sessionKey = $"pin_session:{userId}:{pinSessionToken}";
        await _redisService.RemoveAsync(sessionKey);

        return (true, string.Empty);
    }

    public async Task<(string AccessToken, string RefreshToken, string ErrorMessage)> RefreshTokenAsync(
        string refreshToken)
    {
        var token = await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (token == null)
        {
            return (string.Empty, string.Empty, "Invalid refresh token");
        }

        if (token.IsRevoked)
        {
            return (string.Empty, string.Empty, "Refresh token has been revoked");
        }

        if (token.ExpiresAt < DateTime.UtcNow)
        {
            return (string.Empty, string.Empty, "Refresh token has expired");
        }

        if (token.User == null)
        {
            return (string.Empty, string.Empty, "User not found");
        }

        if (token.User.Status != UserStatus.Active)
        {
            return (string.Empty, string.Empty, "User account is not active");
        }

        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;

        var accessToken = GenerateAccessToken(token.User);
        var newRefreshToken = GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = token.User.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("Token refreshed for user {UserId}", token.User.Id);

        return (accessToken, newRefreshToken, string.Empty);
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new Claim("level_id", user.LevelId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    private static string GenerateReferralCode(string username)
    {
        var sanitized = new string(username
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
        var baseCode = string.IsNullOrWhiteSpace(sanitized)
            ? "USER"
            : sanitized[..Math.Min(6, sanitized.Length)];
        var random = new Random();
        var suffix = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 2)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        return $"{baseCode}{suffix}";
    }
}
