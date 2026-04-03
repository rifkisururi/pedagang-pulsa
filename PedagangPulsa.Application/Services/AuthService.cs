using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public AuthService(
        AppDbContext context,
        ILogger<AuthService> logger,
        string jwtSecret = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
        string jwtIssuer = "PedagangPulsa",
        string jwtAudience = "PedagangPulsaMobile")
    {
        _context = context;
        _logger = logger;
        _jwtSecret = jwtSecret;
        _jwtIssuer = jwtIssuer;
        _jwtAudience = jwtAudience;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    public async Task<(User? User, string ErrorMessage)> RegisterAsync(
        string username,
        string? fullName,
        string? email,
        string? phone,
        string pin,
        string? referralCode = null)
    {
        // Check if username already exists
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.UserName == username);

        if (existingUser != null)
        {
            return (null, "Username already exists");
        }

        // Check if email already exists
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (existingEmail != null)
            {
                return (null, "Email already exists");
            }
        }

        // Check if phone already exists
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var existingPhone = await _context.Users
                .FirstOrDefaultAsync(u => u.Phone == phone);

            if (existingPhone != null)
            {
                return (null, "Phone number already exists");
            }
        }

        // Validate referral code and get referrer with level
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

        // Get default level
        var defaultLevel = await _context.UserLevels
            .FirstOrDefaultAsync(l => l.Name == "Member1");

        if (defaultLevel == null)
        {
            return (null, "Default user level not configured");
        }

        // Create user
        var user = new User
        {
            UserName = username,
            FullName = fullName,
            Email = email,
            Phone = phone,
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

        _context.Users.Add(user);

        // Create user balance
        var balance = new UserBalance
        {
            UserId = user.Id,
            ActiveBalance = 0,
            HeldBalance = 0,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserBalances.Add(balance);

        // Create referral log if applicable
        if (referredBy.HasValue && referrer != null)
        {
            var referralLog = new ReferralLog
            {
                ReferrerId = referredBy.Value,
                RefereeId = user.Id,
                BonusAmount = null, // Will be set when referral bonus is configured
                BonusStatus = ReferralBonusStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.ReferralLogs.Add(referralLog);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} registered successfully", username);

        return (user, string.Empty);
    }

    /// <summary>
    /// Login user and generate JWT token
    /// </summary>
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
            return (null, string.Empty, string.Empty, "Invalid username or PIN");
        }

        // Check if user is suspended
        if (user.Status == UserStatus.Suspended)
        {
            return (null, string.Empty, string.Empty, "Account is suspended");
        }

        // Check if user is inactive
        if (user.Status == UserStatus.Inactive)
        {
            return (null, string.Empty, string.Empty, "Account is inactive");
        }

        // Check if PIN is locked
        if (user.PinLockedAt.HasValue && user.PinLockedAt.Value > DateTime.UtcNow.AddMinutes(-15))
        {
            var lockoutRemaining = (user.PinLockedAt.Value - DateTime.UtcNow.AddMinutes(-15)).TotalMinutes;
            return (null, string.Empty, string.Empty, $"Account is locked. Try again in {Math.Ceiling(lockoutRemaining)} minutes");
        }

        // Verify PIN
        if (!BCrypt.Net.BCrypt.Verify(pin, user.PinHash))
        {
            // Increment failed attempts
            user.PinFailedAttempts++;
            user.UpdatedAt = DateTime.UtcNow;

            // Check if should lock
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

        // Reset failed attempts on successful login
        if (user.PinFailedAttempts > 0 || user.PinLockedAt.HasValue)
        {
            user.PinFailedAttempts = 0;
            user.PinLockedAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        // Generate tokens
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        // Save refresh token
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} logged in successfully", username);

        return (user, accessToken, refreshToken, string.Empty);
    }

    /// <summary>
    /// Verify PIN and generate session token
    /// </summary>
    public async Task<(bool Success, string PinSessionToken, string ErrorMessage)> VerifyPinAsync(
        Guid userId,
        string pin)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return (false, string.Empty, "User not found");
        }

        // Check if PIN is locked first (before checking PIN)
        if (user.PinLockedAt.HasValue && user.PinLockedAt.Value > DateTime.UtcNow.AddMinutes(-15))
        {
            var lockoutRemaining = (user.PinLockedAt.Value - DateTime.UtcNow.AddMinutes(-15)).TotalMinutes;
            return (false, string.Empty, $"Account is locked. Try again in {Math.Ceiling(lockoutRemaining)} minutes");
        }

        // Verify PIN
        if (!BCrypt.Net.BCrypt.Verify(pin, user.PinHash))
        {
            // Increment failed attempts
            user.PinFailedAttempts++;
            user.UpdatedAt = DateTime.UtcNow;

            // Check if should lock
            if (user.PinFailedAttempts >= 3)
            {
                user.PinLockedAt = DateTime.UtcNow;
                _logger.LogWarning("User {UserId} account locked due to 3 failed PIN attempts", userId);
            }

            await _context.SaveChangesAsync();

            var attemptsLeft = 3 - user.PinFailedAttempts;
            return (false, string.Empty, attemptsLeft > 0
                ? $"Invalid PIN. {attemptsLeft} attempts remaining"
                : "Account locked for 15 minutes due to too many failed attempts");
        }

        // Reset failed attempts on successful verification
        if (user.PinFailedAttempts > 0 || user.PinLockedAt.HasValue)
        {
            user.PinFailedAttempts = 0;
            user.PinLockedAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        // Generate PIN session token (valid for 5 minutes)
        var pinSessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        _logger.LogInformation("PIN verified for user {UserId}", userId);

        return (true, pinSessionToken, string.Empty);
    }

    /// <summary>
    /// Refresh access token
    /// </summary>
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

        // Check if user is still active
        if (token.User.Status != UserStatus.Active)
        {
            return (string.Empty, string.Empty, "User account is not active");
        }

        // Revoke old refresh token
        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var accessToken = GenerateAccessToken(token.User);
        var newRefreshToken = GenerateRefreshToken();

        // Save new refresh token
        var refreshTokenEntity = new RefreshToken
        {
            UserId = token.User.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Token refreshed for user {UserId}", token.User.Id);

        return (accessToken, newRefreshToken, string.Empty);
    }

    /// <summary>
    /// Generate JWT access token
    /// </summary>
    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
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
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generate refresh token
    /// </summary>
    private string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Generate referral code
    /// </summary>
    private string GenerateReferralCode(string username)
    {
        // Take first 6 chars of username and add 2 random chars
        var baseCode = username.ToUpper().Replace("[^A-Z0-9]", "")[..Math.Min(6, username.Length)];
        var random = new Random();
        var suffix = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 2)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        return $"{baseCode}{suffix}";
    }
}
