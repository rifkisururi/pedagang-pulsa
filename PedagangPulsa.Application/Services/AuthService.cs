using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PedagangPulsa.Application.Abstractions.Auth;
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
    private readonly IGoogleTokenValidator? _googleTokenValidator;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public AuthService(
        IAppDbContext context,
        ILogger<AuthService> logger,
        IRedisService? redisService = null,
        IGoogleTokenValidator? googleTokenValidator = null,
        string? jwtSecret = null,
        string? jwtIssuer = null,
        string? jwtAudience = null)
    {
        _context = context;
        _logger = logger;
        _redisService = redisService;
        _googleTokenValidator = googleTokenValidator;
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

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .Include(u => u.Balance)
            .Include(u => u.Level)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<(bool Success, string ErrorMessage)> ChangePinAsync(Guid userId, string currentPin, string newPin)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return (false, "User not found");
        }

        // Check Redis lockout first
        var lockoutKey = $"pin_lockout:{userId}";
        if (_redisService != null && await _redisService.ExistsAsync(lockoutKey))
        {
            var ttl = await _redisService.TtlAsync(lockoutKey);
            return (false, $"Account is locked. Try again in {Math.Ceiling(Math.Max(ttl, 1) / 60d)} minutes");
        }

        // Check DB lockout
        if (user.PinLockedAt.HasValue && user.PinLockedAt.Value > DateTime.UtcNow.AddMinutes(-15))
        {
            var lockoutRemaining = (user.PinLockedAt.Value - DateTime.UtcNow.AddMinutes(-15)).TotalMinutes;
            return (false, $"Account is locked. Try again in {Math.Ceiling(lockoutRemaining)} minutes");
        }

        if (!BCrypt.Net.BCrypt.Verify(currentPin, user.PinHash))
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

                await _context.SaveChangesAsync();
                return (false, "Account locked for 15 minutes due to too many failed attempts");
            }

            await _context.SaveChangesAsync();

            var attemptsLeft = 3 - user.PinFailedAttempts;
            return (false, $"Invalid PIN. {attemptsLeft} attempts remaining");
        }

        // Reset failed attempts and update PIN
        user.PinFailedAttempts = 0;
        user.PinLockedAt = null;
        user.PinHash = BCrypt.Net.BCrypt.HashPassword(newPin, workFactor: 12);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("PIN changed successfully for user {UserId}", userId);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return (false, "User not found");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return (false, "Account has no password set");
        }

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return (false, "Current password is incorrect");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);

        return (true, string.Empty);
    }

    public async Task<(User? User, string AccessToken, string RefreshToken, string ErrorMessage, bool RequiresPinSetup)> LoginWithGoogleAsync(string googleIdToken)
    {
        if (_googleTokenValidator == null)
        {
            return (null, string.Empty, string.Empty, "Google login is not configured", false);
        }

        // 1. Validate Google ID token
        var (validationResult, validationError) = await _googleTokenValidator.ValidateAsync(googleIdToken);
        if (validationResult == null)
        {
            return (null, string.Empty, string.Empty, validationError, false);
        }

        if (!validationResult.EmailVerified)
        {
            return (null, string.Empty, string.Empty, "Google email is not verified", false);
        }

        // 2. Check if a UserExternalLogin already exists for this Google account
        var existingLogin = await _context.UserExternalLogins
            .Include(el => el.User)
                .ThenInclude(u => u.Level)
            .Include(el => el.User)
                .ThenInclude(u => u.Balance)
            .FirstOrDefaultAsync(el => el.Provider == "Google" && el.ProviderKey == validationResult.Subject);

        if (existingLogin?.User != null)
        {
            var user = existingLogin.User;
            existingLogin.LastUsedAt = DateTime.UtcNow;

            if (user.Status != UserStatus.Active)
            {
                return (null, string.Empty, string.Empty,
                    user.Status == UserStatus.Suspended ? "Account is suspended" : "Account is inactive",
                    false);
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

            _logger.LogInformation("Google login successful for user {UserId}", user.Id);
            return (user, accessToken, refreshToken, string.Empty, false);
        }

        // 3. No existing external login. Check if a user already exists with this email.
        var existingUserByEmail = await _context.Users
            .Include(u => u.Balance)
            .Include(u => u.Level)
            .FirstOrDefaultAsync(u => u.Email == validationResult.Email);

        if (existingUserByEmail != null)
        {
            _context.UserExternalLogins.Add(new UserExternalLogin
            {
                UserId = existingUserByEmail.Id,
                Provider = "Google",
                ProviderKey = validationResult.Subject,
                ProviderDisplayName = validationResult.Name,
                Email = validationResult.Email,
                AvatarUrl = validationResult.Picture,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            });

            var accessToken = GenerateAccessToken(existingUserByEmail);
            var refreshToken = GenerateRefreshToken();

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = existingUserByEmail.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Google account linked to existing user {UserId}", existingUserByEmail.Id);
            return (existingUserByEmail, accessToken, refreshToken, string.Empty, false);
        }

        // 4. Brand new user -- auto-register
        var defaultLevel = await _context.UserLevels
            .FirstOrDefaultAsync(l => l.Name == "Bronze");

        if (defaultLevel == null)
        {
            return (null, string.Empty, string.Empty, "Default user level not configured", false);
        }

        var baseUsername = validationResult.Email.Split('@')[0];
        var username = await GenerateUniqueUsernameAsync(baseUsername);

        var tempPin = Random.Shared.Next(100000, 999999).ToString();

        var newUser = new User
        {
            UserName = username,
            FullName = validationResult.Name ?? validationResult.GivenName,
            Email = validationResult.Email,
            Phone = null,
            PasswordHash = null,
            PinHash = BCrypt.Net.BCrypt.HashPassword(tempPin, workFactor: 12),
            PinFailedAttempts = 0,
            PinLockedAt = null,
            LevelId = defaultLevel.Id,
            CanTransferOverride = null,
            ReferralCode = GenerateReferralCode(username),
            ReferredBy = null,
            Status = UserStatus.Active,
            EmailVerifiedAt = DateTime.UtcNow,
            PhoneVerifiedAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        newUser.Level = defaultLevel;
        newUser.Balance = new UserBalance
        {
            UserId = newUser.Id,
            ActiveBalance = 0,
            HeldBalance = 0,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        _context.UserBalances.Add(newUser.Balance);
        _context.UserExternalLogins.Add(new UserExternalLogin
        {
            UserId = newUser.Id,
            Provider = "Google",
            ProviderKey = validationResult.Subject,
            ProviderDisplayName = validationResult.Name,
            Email = validationResult.Email,
            AvatarUrl = validationResult.Picture,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        });

        var newAccessToken = GenerateAccessToken(newUser);
        var newRefreshToken = GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = newUser.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("New user {Username} auto-registered via Google login", username);
        return (newUser, newAccessToken, newRefreshToken, string.Empty, true);
    }

    public async Task<(bool Success, string ErrorMessage)> InitializePinAsync(Guid userId, string newPin)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return (false, "User not found");
        }

        user.PinHash = BCrypt.Net.BCrypt.HashPassword(newPin, workFactor: 12);
        user.PinFailedAttempts = 0;
        user.PinLockedAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("PIN initialized for user {UserId}", userId);
        return (true, string.Empty);
    }

    private async Task<string> GenerateUniqueUsernameAsync(string baseUsername)
    {
        var username = baseUsername.Length > 50 ? baseUsername[..47] : baseUsername;
        username = new string(username.Where(char.IsLetterOrDigit).ToArray());

        if (string.IsNullOrWhiteSpace(username))
            username = "user";

        var exists = await _context.Users.AnyAsync(u => u.UserName == username);
        if (!exists)
            return username;

        for (var i = 0; i < 10; i++)
        {
            var candidate = $"{username}{Random.Shared.Next(100, 9999)}";
            if (candidate.Length > 50)
                candidate = candidate[..50];
            exists = await _context.Users.AnyAsync(u => u.UserName == candidate);
            if (!exists)
                return candidate;
        }

        return $"g_{Guid.NewGuid():N}";
    }

    public async Task<(UserExternalLogin? ExternalLogin, string ErrorMessage)> LinkGoogleAsync(Guid userId, string googleIdToken)
    {
        if (_googleTokenValidator == null)
        {
            return (null, "Google login is not configured");
        }

        var (validationResult, validationError) = await _googleTokenValidator.ValidateAsync(googleIdToken);
        if (validationResult == null)
        {
            return (null, validationError);
        }

        if (!validationResult.EmailVerified)
        {
            return (null, "Google email is not verified");
        }

        // Check if this Google account is already linked to any user
        var existingLink = await _context.UserExternalLogins
            .FirstOrDefaultAsync(el => el.Provider == "Google" && el.ProviderKey == validationResult.Subject);

        if (existingLink != null)
        {
            if (existingLink.UserId == userId)
            {
                return (null, "Google account is already linked to your account");
            }
            return (null, "Google account is already linked to another account");
        }

        // Check if user already has a Google login
        var userGoogleLink = await _context.UserExternalLogins
            .FirstOrDefaultAsync(el => el.UserId == userId && el.Provider == "Google");

        if (userGoogleLink != null)
        {
            return (null, "Your account already has a Google login linked");
        }

        var externalLogin = new UserExternalLogin
        {
            UserId = userId,
            Provider = "Google",
            ProviderKey = validationResult.Subject,
            ProviderDisplayName = validationResult.Name,
            Email = validationResult.Email,
            AvatarUrl = validationResult.Picture,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        };

        _context.UserExternalLogins.Add(externalLogin);

        // Update email verified status if not yet verified
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null && !user.EmailVerifiedAt.HasValue && user.Email == validationResult.Email)
        {
            user.EmailVerifiedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Google account linked for user {UserId}", userId);
        return (externalLogin, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> UnlinkGoogleAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return (false, "User not found");
        }

        var googleLink = await _context.UserExternalLogins
            .FirstOrDefaultAsync(el => el.UserId == userId && el.Provider == "Google");

        if (googleLink == null)
        {
            return (false, "No Google account linked");
        }

        // Prevent unlinking if user has no password set (Google-only account)
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return (false, "Cannot unlink Google account. Please set a password first.");
        }

        _context.UserExternalLogins.Remove(googleLink);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Google account unlinked for user {UserId}", userId);
        return (true, string.Empty);
    }

    public async Task<UserExternalLogin?> GetGoogleLinkStatusAsync(Guid userId)
    {
        return await _context.UserExternalLogins
            .FirstOrDefaultAsync(el => el.UserId == userId && el.Provider == "Google");
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
